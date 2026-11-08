import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, Button, Checkbox, Drawer, Empty, Input, Modal, Select, Spin } from "antd"
import {
    AppstoreOutlined,
    DeleteOutlined,
    EditOutlined,
    PlusOutlined,
    ReloadOutlined,
} from "@ant-design/icons"

import {
    AccountsApi,
    ClosingApi,
    type AccountDto,
    type ClosingTemplateDto,
    type SaveClosingTemplateLineRequest,
    type SaveClosingTemplateRequest,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { useConfirm } from "@/hooks/useConfirm"
import { NumberField } from "@/components/fields/number-field"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"
import { ICON_STYLE } from "@/components/page/styles"

/** 取数方式（与后端 ClosingTemplateSourceType 对应）。 */
const SOURCE_OPTIONS = [
    { value: 1, label: "固定金额" },
    { value: 2, label: "科目本期借方发生额" },
    { value: 3, label: "科目本期贷方发生额" },
    { value: 4, label: "科目期末余额" },
]

/** 分录方向（与后端枚举一致：1 借 / 2 贷）。 */
const DIRECTION_OPTIONS = [
    { value: 1, label: "借" },
    { value: 2, label: "贷" },
]

/** 编辑器行。 */
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
    const { confirm, confirmDialog } = useConfirm()

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

    /** 可选科目下拉项：末级且启用。 */
    const accountOptions = useMemo(
        () =>
            accounts
                .filter((a) => a.isLeaf !== false && a.isEnabled !== false)
                .map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
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
        if (!(await confirm(`确定删除结转模板「${template.name}」吗？`))) return
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
            <Drawer
                open={open}
                size={672}
                title="结转模板"
                onClose={() => onOpenChange(false)}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16, overflow: "auto" } }}
            >
                <p className="text-[12.5px] text-(--muted-foreground)">
                    月末结转时可复用生成凭证（如计提工资、结转成本）。金额在结转时按当月数据计算，结转前会先预览平衡情况。
                </p>

                <div className="flex items-center justify-between gap-2">
                    <span className="text-[12.5px] text-(--muted-foreground)">共 {templates.length} 个模板</span>
                    <div className="flex items-center gap-2">
                        <Button
                            icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                            onClick={() => void load()}
                            disabled={loading}
                        >
                            刷新
                        </Button>
                        <Button type="primary" icon={<PlusOutlined />} onClick={openNew}>
                            新建模板
                        </Button>
                    </div>
                </div>

                {error && <Alert type="error" showIcon title={error} />}

                {loading && templates.length === 0 ? (
                    <div className="flex justify-center py-10">
                        <Spin />
                    </div>
                ) : templates.length === 0 ? (
                    <Empty image={<AppstoreOutlined className="text-[40px]" style={ICON_STYLE.placeholder} />} description="暂无结转模板，点击「新建模板」创建" />
                ) : (
                    <div className="flex flex-col gap-2.5">
                        {templates.map((t) => (
                            <div key={t.id} className="rounded-xl border border-(--border) bg-(--card) p-3.5">
                                <div className="flex items-center justify-between gap-2">
                                    <div className="flex min-w-0 items-center gap-2">
                                        <span className="truncate text-[13.5px] font-medium text-(--foreground)">{t.name}</span>
                                        <StatusPill tone={t.isEnabled ? "green" : "gray"}>{t.isEnabled ? "启用" : "停用"}</StatusPill>
                                    </div>
                                    <div className="flex items-center gap-0">
                                        <Button
                                            type="text"
                                            title="编辑"
                                            icon={<EditOutlined />}
                                            onClick={() => openEdit(t)}
                                        />
                                        <Button
                                            type="text"
                                            danger
                                            title="删除"
                                            disabled={busyId === t.id}
                                            icon={<DeleteOutlined />}
                                            onClick={() => void remove(t)}
                                        />
                                    </div>
                                </div>
                                <div className="mt-2 flex flex-col gap-1">
                                    {(t.lines ?? []).map((l) => (
                                        <div key={l.id} className="flex items-center gap-2 text-[12.5px] text-(--muted-foreground)">
                                            <span className="w-6 shrink-0 text-center text-(--muted-foreground)">{l.directionText}</span>
                                            <span className="w-44 shrink-0 truncate text-(--foreground)">
                                                {l.accountCode} {l.accountName}
                                            </span>
                                            <span className="min-w-0 flex-1 truncate">{l.summary}</span>
                                            <span className="shrink-0 text-(--muted-foreground)">
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
            </Drawer>

            <Modal
                open={editorOpen}
                title={editingId ? "编辑结转模板" : "新建结转模板"}
                width={768}
                onCancel={() => setEditorOpen(false)}
                onOk={() => void save()}
                okText="保存"
                cancelText="取消"
                confirmLoading={saving}
                styles={{ body: { maxHeight: "62vh", overflow: "auto" } }}
            >
                <p className="mb-4 text-[12.5px] text-(--muted-foreground)">
                    每行一条分录；全部为固定金额时保存即校验借贷平衡，取数型模板在结转时计算并校验。
                </p>

                <div className="flex flex-col gap-3">
                    <div className="flex items-end gap-3">
                        <FormField label="模板名称" htmlFor="closing-template-name" className="min-w-0 flex-1">
                            <Input
                                id="closing-template-name"
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                                placeholder="如：计提工资"
                                maxLength={50}
                            />
                        </FormField>
                        <label className="flex items-center gap-2 pb-2 text-[13px] text-(--foreground)">
                            <Checkbox checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} />
                            启用（结转时默认勾选）
                        </label>
                    </div>

                    <div className="flex flex-col gap-2">
                        {lines.map((l, index) => (
                            <div key={l.key} className="rounded-xl border border-(--border) p-2.5">
                                <div className="flex items-center justify-between">
                                    <span className="text-[12px] text-(--muted-foreground)">第 {index + 1} 行</span>
                                    <Button
                                        type="text"
                                        danger
                                        title="删除该行"
                                        icon={<DeleteOutlined />}
                                        onClick={() => setLines((rows) => rows.filter((r) => r.key !== l.key))}
                                    />
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
                                        <Select<string>
                                            className="w-full"
                                            placeholder="会计科目"
                                            allowClear
                                            value={l.accountId || undefined}
                                            options={accountOptions}
                                            onChange={(v) => setLine(l.key, { accountId: v ?? "" })}
                                        />
                                    </div>
                                    <div className="col-span-2">
                                        <Select<number>
                                            className="w-full"
                                            value={l.direction}
                                            options={DIRECTION_OPTIONS}
                                            onChange={(v) => setLine(l.key, { direction: v })}
                                        />
                                    </div>
                                    <div className="col-span-2">
                                        <Select<number>
                                            className="w-full"
                                            value={l.sourceType}
                                            options={SOURCE_OPTIONS}
                                            onChange={(v) => setLine(l.key, { sourceType: v })}
                                        />
                                    </div>
                                </div>
                                <div className="mt-2 grid grid-cols-12 gap-2">
                                    {l.sourceType === 1 ? (
                                        <>
                                            <span className="col-span-8 flex items-center text-[12px] text-(--muted-foreground)">
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
                                                <Select<string>
                                                    className="w-full"
                                                    placeholder="来源科目"
                                                    allowClear
                                                    value={l.sourceAccountId || undefined}
                                                    options={accountOptions}
                                                    onChange={(v) => setLine(l.key, { sourceAccountId: v ?? "" })}
                                                />
                                            </div>
                                            <span className="col-span-4 flex items-center text-[12px] text-(--muted-foreground)">按结转期间计算金额</span>
                                        </>
                                    )}
                                </div>
                            </div>
                        ))}
                        <Button icon={<PlusOutlined />} onClick={() => setLines((rows) => [...rows, newLine()])}>
                            添加行
                        </Button>
                    </div>

                    {editorError && <Alert type="error" showIcon title={editorError} />}
                </div>
            </Modal>

            {confirmDialog}
        </>
    )
}
