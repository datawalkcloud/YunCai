import { useCallback, useEffect, useMemo, useState } from "react"
import { CircleAlert, Layers, Pencil, Plus, RefreshCw, Trash2 } from "lucide-react"

import {
    AccountsApi,
    ClosingApi,
    type AccountDto,
    type ClosingTemplateDto,
    type SaveClosingTemplateLineRequest,
    type SaveClosingTemplateRequest,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { Alert, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia } from "@/components/ui/empty"
import { Field, FieldLabel } from "@/components/ui/field"
import { NumberField } from "@/components/fields/number-field"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet"
import { Spinner } from "@/components/ui/spinner"
import { CARD_BTN_PRIMARY, ICON_BTN, ICON_BTN_DANGER, PAGE_BTN_GHOST } from "@/components/page/styles"

/** 取数方式（与后端 ClosingTemplateSourceType 对应）。 */
const SOURCE_TYPES = [
    { value: 1, label: "固定金额" },
    { value: 2, label: "科目本期借方发生额" },
    { value: 3, label: "科目本期贷方发生额" },
    { value: 4, label: "科目期末余额" },
] as const

/** Radix Select 不接受空串作为选项值，用哨兵表示「未选择」。 */
const NONE = "__closing_template_none__"

/** 编辑器行（category 与后端枚举一致：1 借 / 2 贷）。 */
interface EditorLine {
    key: number
    summary: string
    accountId: string
    direction: number
    sourceType: number
    sourceAccountId: string
    amount?: number
}

let lineKeySeq = 1

function newLine(): EditorLine {
    return { key: lineKeySeq++, summary: "", accountId: "", direction: 1, sourceType: 1, sourceAccountId: "", amount: undefined }
}

/**
 * 结转模板管理（列表 + 编辑器）。
 * 模板用于月末结转时复用生成凭证（如计提工资、结转成本）；金额在结转时按当月数据计算。
 */
export function ClosingTemplatesSheet({
    accountSetId,
    token,
    open,
    onOpenChange,
}: {
    accountSetId: string
    token?: string | null
    open: boolean
    onOpenChange: (open: boolean) => void
}) {
    const api = useMemo(() => new ClosingApi(apiConfig(token)), [token])
    const accountsApi = useMemo(() => new AccountsApi(apiConfig(token)), [token])

    const [templates, setTemplates] = useState<ClosingTemplateDto[]>([])
    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [busyId, setBusyId] = useState<string | null>(null)

    const [editorOpen, setEditorOpen] = useState(false)
    const [editingId, setEditingId] = useState<string | null>(null)
    const [name, setName] = useState("")
    const [isEnabled, setIsEnabled] = useState(true)
    const [lines, setLines] = useState<EditorLine[]>([])
    const [saving, setSaving] = useState(false)
    const [editorError, setEditorError] = useState<string | null>(null)

    /** 可选科目：末级且启用。 */
    const leafAccounts = useMemo(
        () => accounts.filter((a) => a.isLeaf !== false && a.isEnabled !== false),
        [accounts],
    )

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const [list, accountList] = await Promise.all([
                api.accountsetsAccountSetIdClosingTemplatesGet({ accountSetId }),
                accountsApi.accountsetsAccountSetIdAccountsGet({ accountSetId }),
            ])
            setTemplates(list)
            setAccounts(accountList)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载结转模板失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, api, accountsApi])

    useEffect(() => {
        if (open) void load()
    }, [open, load])

    function openNew() {
        setEditingId(null)
        setName("")
        setIsEnabled(true)
        setLines([newLine(), newLine()])
        setEditorError(null)
        setEditorOpen(true)
    }

    function openEdit(template: ClosingTemplateDto) {
        setEditingId(template.id ?? null)
        setName(template.name ?? "")
        setIsEnabled(template.isEnabled !== false)
        setLines(
            (template.lines ?? []).map((l) => ({
                key: lineKeySeq++,
                summary: l.summary ?? "",
                accountId: l.accountId ?? "",
                direction: l.direction ?? 1,
                sourceType: l.sourceType ?? 1,
                sourceAccountId: l.sourceAccountId ?? "",
                amount: l.amount ? l.amount : undefined,
            })),
        )
        setEditorError(null)
        setEditorOpen(true)
    }

    function setLine(key: number, patch: Partial<EditorLine>) {
        setLines((rows) => rows.map((r) => (r.key === key ? { ...r, ...patch } : r)))
    }

    async function save() {
        if (saving) return
        setEditorError(null)
        const trimmed = name.trim()
        if (!trimmed) {
            setEditorError("请填写模板名称")
            return
        }
        if (lines.length === 0) {
            setEditorError("模板至少需要一行分录")
            return
        }

        const payloadLines: SaveClosingTemplateLineRequest[] = []
        for (let i = 0; i < lines.length; i++) {
            const l = lines[i]
            if (!l.summary.trim()) {
                setEditorError(`第 ${i + 1} 行：请填写摘要`)
                return
            }
            if (!l.accountId) {
                setEditorError(`第 ${i + 1} 行：请选择会计科目`)
                return
            }
            if (l.sourceType === 1 && (!l.amount || l.amount <= 0)) {
                setEditorError(`第 ${i + 1} 行：请填写大于 0 的固定金额`)
                return
            }
            if (l.sourceType !== 1 && !l.sourceAccountId) {
                setEditorError(`第 ${i + 1} 行：取数方式需要选择来源科目`)
                return
            }

            payloadLines.push({
                summary: l.summary.trim(),
                accountId: l.accountId,
                direction: l.direction,
                sourceType: l.sourceType,
                sourceAccountId: l.sourceType === 1 ? null : l.sourceAccountId,
                amount: l.sourceType === 1 ? (l.amount ?? 0) : 0,
            })
        }

        const request: SaveClosingTemplateRequest = { name: trimmed, isEnabled, lines: payloadLines }
        setSaving(true)
        try {
            if (editingId) {
                await api.accountsetsAccountSetIdClosingTemplatesTemplateIdPut({
                    accountSetId,
                    templateId: editingId,
                    saveClosingTemplateRequest: request,
                })
            } else {
                await api.accountsetsAccountSetIdClosingTemplatesPost({ accountSetId, saveClosingTemplateRequest: request })
            }
            setEditorOpen(false)
            await load()
        } catch (err) {
            setEditorError(await apiErrorMessage(err, "保存模板失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function remove(template: ClosingTemplateDto) {
        if (!template.id || busyId) return
        if (!window.confirm(`确定删除结转模板「${template.name}」吗？`)) return
        setBusyId(template.id)
        setError(null)
        try {
            await api.accountsetsAccountSetIdClosingTemplatesTemplateIdDelete({ accountSetId, templateId: template.id })
            await load()
        } catch (err) {
            setError(await apiErrorMessage(err, "删除模板失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    return (
        <>
            <Sheet open={open} onOpenChange={onOpenChange}>
                <SheetContent className="sm:max-w-2xl">
                    <SheetHeader>
                        <SheetTitle>结转模板</SheetTitle>
                        <SheetDescription>
                            月末结转时可复用生成凭证（如计提工资、结转成本）。金额在结转时按当月数据计算，结转前会先预览平衡情况。
                        </SheetDescription>
                    </SheetHeader>

                    <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-auto px-4">
                        <div className="flex items-center justify-between gap-2">
                            <span className="text-[12.5px] text-slate-400">共 {templates.length} 个模板</span>
                            <div className="flex items-center gap-2">
                                <Button variant="outline" className={PAGE_BTN_GHOST} onClick={() => void load()} disabled={loading}>
                                    {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                                    刷新
                                </Button>
                                <Button className={CARD_BTN_PRIMARY} onClick={openNew}>
                                    <Plus data-icon="inline-start" />
                                    新建模板
                                </Button>
                            </div>
                        </div>

                        {error && (
                            <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{error}</AlertTitle>
                            </Alert>
                        )}

                        {loading && templates.length === 0 ? (
                            <div className="flex justify-center py-10">
                                <Spinner className="size-5 text-slate-300" />
                            </div>
                        ) : templates.length === 0 ? (
                            <Empty>
                                <EmptyHeader>
                                    <EmptyMedia variant="icon">
                                        <Layers />
                                    </EmptyMedia>
                                    <EmptyDescription>暂无结转模板，点击「新建模板」创建</EmptyDescription>
                                </EmptyHeader>
                            </Empty>
                        ) : (
                            <div className="flex flex-col gap-2.5">
                                {templates.map((t) => (
                                    <div key={t.id} className="rounded-xl border border-slate-100 bg-white p-3.5">
                                        <div className="flex items-center justify-between gap-2">
                                            <div className="flex min-w-0 items-center gap-2">
                                                <span className="truncate text-[13.5px] font-medium text-slate-800">{t.name}</span>
                                                {t.isEnabled ? (
                                                    <span className="rounded-full bg-[#d1fae5] px-2 py-0.5 text-[11px] font-medium text-[#10b981]">启用</span>
                                                ) : (
                                                    <span className="rounded-full bg-[#eef1f6] px-2 py-0.5 text-[11px] font-medium text-[#64748b]">停用</span>
                                                )}
                                            </div>
                                            <div className="flex items-center gap-0">
                                                <Button variant="ghost" size="icon-xs" className={ICON_BTN} onClick={() => openEdit(t)} title="编辑">
                                                    <Pencil />
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="icon-xs"
                                                    className={ICON_BTN_DANGER}
                                                    disabled={busyId === t.id}
                                                    onClick={() => void remove(t)}
                                                    title="删除"
                                                >
                                                    <Trash2 />
                                                </Button>
                                            </div>
                                        </div>
                                        <div className="mt-2 flex flex-col gap-1">
                                            {(t.lines ?? []).map((l) => (
                                                <div key={l.id} className="flex items-center gap-2 text-[12.5px] text-slate-500">
                                                    <span className="w-6 shrink-0 text-center text-slate-400">{l.directionText}</span>
                                                    <span className="w-44 shrink-0 truncate text-slate-700">
                                                        {l.accountCode} {l.accountName}
                                                    </span>
                                                    <span className="min-w-0 flex-1 truncate">{l.summary}</span>
                                                    <span className="shrink-0 text-slate-400">
                                                        {l.sourceType === 1
                                                            ? `固定 ${l.amount ?? 0}`
                                                            : `${l.sourceTypeText}：${l.sourceAccountCode ?? ""} ${l.sourceAccountName ?? ""}`}
                                                    </span>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </SheetContent>
            </Sheet>

            <Dialog open={editorOpen} onOpenChange={setEditorOpen}>
                <DialogContent className="max-h-[86vh] max-w-3xl overflow-hidden">
                    <DialogHeader>
                        <DialogTitle>{editingId ? "编辑结转模板" : "新建结转模板"}</DialogTitle>
                        <DialogDescription>
                            每行一条分录；全部为固定金额时保存即校验借贷平衡，取数型模板在结转时计算并校验。
                        </DialogDescription>
                    </DialogHeader>

                    <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-auto pr-1">
                        <div className="flex items-end gap-3">
                            <Field className="min-w-0 flex-1">
                                <FieldLabel htmlFor="closing-template-name">模板名称</FieldLabel>
                                <Input
                                    id="closing-template-name"
                                    value={name}
                                    onChange={(e) => setName(e.target.value)}
                                    placeholder="如：计提工资"
                                    maxLength={50}
                                />
                            </Field>
                            <label className="flex items-center gap-2 pb-2 text-[13px] text-slate-600">
                                <Checkbox checked={isEnabled} onCheckedChange={(v) => setIsEnabled(v === true)} />
                                启用（结转时默认勾选）
                            </label>
                        </div>

                        <div className="flex flex-col gap-2">
                            {lines.map((l, index) => (
                                <div key={l.key} className="rounded-xl border border-slate-100 p-2.5">
                                    <div className="flex items-center justify-between">
                                        <span className="text-[12px] text-slate-400">第 {index + 1} 行</span>
                                        <Button
                                            variant="ghost"
                                            size="icon-xs"
                                            className={ICON_BTN_DANGER}
                                            onClick={() => setLines((rows) => rows.filter((r) => r.key !== l.key))}
                                            title="删除该行"
                                        >
                                            <Trash2 />
                                        </Button>
                                    </div>
                                    <div className="mt-1 grid grid-cols-12 gap-2">
                                        <Input
                                            className="col-span-4"
                                            value={l.summary}
                                            onChange={(e) => setLine(l.key, { summary: e.target.value })}
                                            placeholder="摘要"
                                            maxLength={100}
                                        />
                                        <div className="col-span-4">
                                            <Select
                                                value={l.accountId || NONE}
                                                onValueChange={(v) => setLine(l.key, { accountId: v === NONE ? "" : v })}
                                            >
                                                <SelectTrigger size="sm" className="w-full">
                                                    <SelectValue placeholder="会计科目" />
                                                </SelectTrigger>
                                                <SelectContent className="max-h-64">
                                                    <SelectItem value={NONE}>（选择科目）</SelectItem>
                                                    {leafAccounts.map((a) => (
                                                        <SelectItem key={a.id} value={a.id ?? ""}>
                                                            {a.code} {a.name}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="col-span-2">
                                            <Select value={String(l.direction)} onValueChange={(v) => setLine(l.key, { direction: Number(v) })}>
                                                <SelectTrigger size="sm" className="w-full">
                                                    <SelectValue />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    <SelectGroup>
                                                        <SelectItem value="1">借</SelectItem>
                                                        <SelectItem value="2">贷</SelectItem>
                                                    </SelectGroup>
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="col-span-2">
                                            <Select value={String(l.sourceType)} onValueChange={(v) => setLine(l.key, { sourceType: Number(v) })}>
                                                <SelectTrigger size="sm" className="w-full">
                                                    <SelectValue />
                                                </SelectTrigger>
                                                <SelectContent className="max-h-64">
                                                    <SelectGroup>
                                                        {SOURCE_TYPES.map((s) => (
                                                            <SelectItem key={s.value} value={String(s.value)}>
                                                                {s.label}
                                                            </SelectItem>
                                                        ))}
                                                    </SelectGroup>
                                                </SelectContent>
                                            </Select>
                                        </div>
                                    </div>
                                    <div className="mt-2 grid grid-cols-12 gap-2">
                                        {l.sourceType === 1 ? (
                                            <>
                                                <span className="col-span-8 flex items-center text-[12px] text-slate-400">
                                                    固定金额将原样记入该行方向
                                                </span>
                                                <NumberField
                                                    className="col-span-4"
                                                    value={l.amount}
                                                    onChange={(v) => setLine(l.key, { amount: v })}
                                                    min={0.01}
                                                    max={99999999.99}
                                                    precision={2}
                                                    placeholder="金额"
                                                />
                                            </>
                                        ) : (
                                            <>
                                                <div className="col-span-8">
                                                    <Select
                                                        value={l.sourceAccountId || NONE}
                                                        onValueChange={(v) => setLine(l.key, { sourceAccountId: v === NONE ? "" : v })}
                                                    >
                                                        <SelectTrigger size="sm" className="w-full">
                                                            <SelectValue placeholder="来源科目" />
                                                        </SelectTrigger>
                                                        <SelectContent className="max-h-64">
                                                            <SelectItem value={NONE}>（选择来源科目）</SelectItem>
                                                            {leafAccounts.map((a) => (
                                                                <SelectItem key={a.id} value={a.id ?? ""}>
                                                                    {a.code} {a.name}
                                                                </SelectItem>
                                                            ))}
                                                        </SelectContent>
                                                    </Select>
                                                </div>
                                                <span className="col-span-4 flex items-center text-[12px] text-slate-400">按结转期间计算金额</span>
                                            </>
                                        )}
                                    </div>
                                </div>
                            ))}
                            <Button variant="outline" className={PAGE_BTN_GHOST} onClick={() => setLines((rows) => [...rows, newLine()])}>
                                <Plus data-icon="inline-start" />
                                添加行
                            </Button>
                        </div>

                        {editorError && (
                            <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{editorError}</AlertTitle>
                            </Alert>
                        )}
                    </div>

                    <DialogFooter>
                        <Button variant="outline" onClick={() => setEditorOpen(false)}>
                            取消
                        </Button>
                        <Button className={CARD_BTN_PRIMARY} disabled={saving} onClick={() => void save()}>
                            {saving ? <Spinner data-icon="inline-start" /> : <Plus data-icon="inline-start" />}
                            保存
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </>
    )
}
