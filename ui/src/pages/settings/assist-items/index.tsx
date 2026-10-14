import { useCallback, useEffect, useMemo, useState } from "react"
import { Briefcase, ChevronDown, ChevronRight, CircleAlert, Plus, RefreshCw, Trash2, UserPen } from "lucide-react"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { AccountSetsApi, AssistItemsApi, type AssistCategoryStatDto, type AssistItemDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { useConfirm } from "@/hooks/useConfirm"
import { cn } from "@/lib/utils"
import { ASSIST_CODE_MAX_LENGTH, assistExpectedCodeLength, assistSupportsHierarchy } from "@/components/assist/constants"
import { invalidateAssistItems } from "@/components/assist/assist-store"
import { PageHeader, SectionCard } from "@/components/page/section"
import { StatusPill } from "@/components/page/status-pill"
import { CARD_BTN, CARD_BTN_PRIMARY, ICON_BTN, ICON_BTN_DANGER } from "@/components/page/styles"
import { Alert, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "@/components/ui/dialog"
import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Textarea } from "@/components/ui/textarea"

/** 层级缩进类（档案最多十几级，够用即止）。 */
const INDENT_CLASS = ["", "pl-4", "pl-8", "pl-12", "pl-16", "pl-20", "pl-24", "pl-28"]
const indentClass = (depth: number) => INDENT_CLASS[Math.min(depth, INDENT_CLASS.length - 1)]

/** 档案树节点 + 渲染深度。 */
interface ItemNode extends AssistItemDto {
    children?: ItemNode[]
}
interface VisibleRow {
    node: ItemNode
    depth: number
}

/** 新增 / 编辑档案的表单值。 */
interface ItemForm {
    code: string
    name: string
    contact: string
    remark: string
}

const emptyForm: ItemForm = { code: "", name: "", contact: "", remark: "" }

/** 列表列数（编码 / 名称 / 联系资料 / 备注 / 状态 / 操作）。 */
const COLUMN_COUNT = 6

export default function AssistItemsPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [categories, setCategories] = useState<AssistCategoryStatDto[]>([])
    const [categoriesLoading, setCategoriesLoading] = useState(false)
    const [categoriesError, setCategoriesError] = useState<string | null>(null)
    const [switchingType, setSwitchingType] = useState<number | null>(null)
    const [activeType, setActiveType] = useState(1)

    const loadCategories = useCallback(async () => {
        if (!accountSetId) {
            setCategories([])
            return
        }
        setCategoriesLoading(true)
        setCategoriesError(null)
        try {
            setCategories(
                await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsCategoriesGet({
                    accountSetId,
                }),
            )
        } catch (err) {
            setCategoriesError(await apiErrorMessage(err, "加载辅助核算类别失败，请重试。"))
        } finally {
            setCategoriesLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void loadCategories()
    }, [loadCategories])

    /**
     * 账套级类别开关：整体覆盖 PUT /AccountSets/{id}/assist-types（body 为启用的类别编号数组）。
     * 已有档案的类别由后端拒绝关闭。
     */
    async function toggleCategory(type: number, next: boolean) {
        if (!accountSetId || switchingType !== null) return
        const desired = categories
            .filter((c) => (c.type === type ? next : c.isEnabled))
            .map((c) => c.type as number)
        setSwitchingType(type)
        setCategoriesError(null)
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdAssistTypesPut({
                id: accountSetId,
                requestBody: desired,
            })
            await loadCategories()
        } catch (err) {
            setCategoriesError(await apiErrorMessage(err, "更新类别启用状态失败，请重试。"))
        } finally {
            setSwitchingType(null)
        }
    }

    const active = categories.find((c) => c.type === activeType)

    return (
        <div className="flex flex-col gap-5">
            <PageHeader
                icon={Briefcase}
                title="辅助核算"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。维护客户、供应商、部门等核算项目档案；科目挂上辅助核算后，凭证分录必须录入对应档案。
                    </>
                }
            />

            {!accountSetId ? (
                <SectionCard>
                    <span className="text-[13px] text-slate-400">请先在左上角选择账套。</span>
                </SectionCard>
            ) : (
                <div className="grid gap-5 lg:grid-cols-[16rem_1fr]">
                    <SectionCard
                        title={<h3 className="text-[14px] font-semibold text-slate-800">核算类别</h3>}
                        bodyClassName="flex flex-col gap-1 p-3"
                    >
                        {categoriesLoading && categories.length === 0 && <Spinner className="mx-auto size-5 text-slate-300" />}
                        {categories.map((c) => {
                            const type = c.type as number
                            const hierarchical = assistSupportsHierarchy(type)
                            return (
                                <div
                                    key={type}
                                    className={cn(
                                        "flex items-center gap-2 rounded-lg px-2 py-1.5",
                                        type === activeType ? "bg-[#dbeafe]" : "hover:bg-muted",
                                    )}
                                >
                                    <button
                                        type="button"
                                        className="flex flex-1 items-center gap-2 text-left"
                                        onClick={() => setActiveType(type)}
                                    >
                                        <span className={cn("text-[13px]", type === activeType ? "font-medium text-[#2f6bff]" : "text-slate-600")}>
                                            {c.typeText}
                                        </span>
                                        {hierarchical && (
                                            <span className="text-[11px] text-slate-400">层级</span>
                                        )}
                                        <span className="ml-auto text-[11.5px] text-slate-400 tabular-nums">
                                            {c.itemCount ?? 0}
                                        </span>
                                    </button>
                                    <label className="flex items-center gap-1 text-[11.5px] text-slate-400">
                                        <Checkbox
                                            checked={c.isEnabled === true}
                                            disabled={switchingType !== null}
                                            onCheckedChange={(v) => void toggleCategory(type, v === true)}
                                            aria-label={`启用${c.typeText ?? ""}类别`}
                                        />
                                        启用
                                    </label>
                                </div>
                            )
                        })}
                        {categoriesError && (
                            <Alert variant="destructive" className="mt-2 rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{categoriesError}</AlertTitle>
                            </Alert>
                        )}
                    </SectionCard>

                    <AssistItemListPanel
                        key={`${accountSetId}-${activeType}`}
                        accountSetId={accountSetId}
                        token={token}
                        type={activeType}
                        categoryEnabled={active?.isEnabled === true}
                        onCategoryEnabledChange={() => void toggleCategory(activeType, true)}
                        onChanged={loadCategories}
                    />
                </div>
            )}
        </div>
    )
}

/** 某类别的档案列表：层级型可展开树表，平铺型为列表。 */
function AssistItemListPanel({
    accountSetId,
    token,
    type,
    categoryEnabled,
    onCategoryEnabledChange,
    onChanged,
}: {
    accountSetId: string
    token?: string | null
    type: number
    categoryEnabled: boolean
    onCategoryEnabledChange: () => void
    /** 档案增删改后刷新类别统计（档案数）。 */
    onChanged: () => void
}) {
    const hierarchical = assistSupportsHierarchy(type)
    const { confirm, confirmDialog } = useConfirm()
    const [items, setItems] = useState<AssistItemDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [expandedKeys, setExpandedKeys] = useState<string[]>([])
    const [busyId, setBusyId] = useState<string | null>(null)

    const [formOpen, setFormOpen] = useState(false)
    const [formMode, setFormMode] = useState<"create" | "edit">("create")
    const [parentItem, setParentItem] = useState<AssistItemDto | null>(null)
    const [editTarget, setEditTarget] = useState<AssistItemDto | null>(null)
    const [form, setForm] = useState<ItemForm>(emptyForm)
    const [formError, setFormError] = useState<string | null>(null)
    const [formFieldError, setFormFieldError] = useState<string | null>(null)
    const [saving, setSaving] = useState(false)

    const load = useCallback(async () => {
        setLoading(true)
        setError(null)
        try {
            const list = await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsGet({
                accountSetId,
                type,
            })
            setItems(list)
            setExpandedKeys(list.filter((i) => i.parentId).map((i) => i.parentId as string))
        } catch (err) {
            setError(await apiErrorMessage(err, "加载档案失败，请重试。"))
            setItems([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, type, token])

    useEffect(() => {
        void load()
    }, [load])

    /** 变更后同时让档案选择器的共享缓存失效，凭证页立即看到最新档案。 */
    async function refresh() {
        invalidateAssistItems(accountSetId, type)
        await load()
        onChanged()
    }

    const treeData = useMemo<ItemNode[]>(() => {
        const byCode = (a: AssistItemDto, b: AssistItemDto) => (a.code ?? "").localeCompare(b.code ?? "")
        const build = (pid: string | null): ItemNode[] =>
            items
                .filter((i) => (i.parentId ?? null) === pid)
                .sort(byCode)
                .map((i) => {
                    const sub = hierarchical ? build(i.id ?? null) : []
                    return { ...i, children: sub.length > 0 ? sub : undefined }
                })
        return build(null)
    }, [items, hierarchical])

    const visibleRows = useMemo<VisibleRow[]>(() => {
        const expanded = new Set(expandedKeys)
        const out: VisibleRow[] = []
        const walk = (nodes: ItemNode[], depth: number) => {
            for (const node of nodes) {
                out.push({ node, depth })
                if (node.children && expanded.has(String(node.id))) walk(node.children, depth + 1)
            }
        }
        walk(treeData, 0)
        return out
    }, [treeData, expandedKeys])

    const topRows = useMemo<VisibleRow[]>(
        () => (hierarchical ? visibleRows : treeData.map((node) => ({ node, depth: 0 }))),
        [hierarchical, visibleRows, treeData],
    )

    /** 打开新增弹窗并按 next-code 预填建议编码。 */
    async function openCreate(parent: AssistItemDto | null) {
        setFormMode("create")
        setParentItem(parent)
        setEditTarget(null)
        setForm(emptyForm)
        setFormError(null)
        setFormFieldError(null)
        setFormOpen(true)
        try {
            const code = await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsNextCodeGet({
                accountSetId,
                type,
                parentId: parent?.id ?? undefined,
            })
            setForm((prev) => ({ ...prev, code }))
        } catch (err) {
            setFormError(await apiErrorMessage(err, "获取建议编码失败，可手动填写。"))
        }
    }

    function openEdit(item: AssistItemDto) {
        setFormMode("edit")
        setParentItem(null)
        setEditTarget(item)
        setForm({
            code: item.code ?? "",
            name: item.name ?? "",
            contact: item.contact ?? "",
            remark: item.remark ?? "",
        })
        setFormError(null)
        setFormFieldError(null)
        setFormOpen(true)
    }

    /** 本地只做轻量校验（必填 + 编码长度上限），编码位数与前缀规则交给后端给出准确中文提示。 */
    function validateForm(): boolean {
        if (formMode === "create" && !form.code.trim()) {
            setFormFieldError("请填写档案编码")
            return false
        }
        if (formMode === "create" && form.code.trim().length > ASSIST_CODE_MAX_LENGTH) {
            setFormFieldError(`档案编码不能超过 ${ASSIST_CODE_MAX_LENGTH} 个字符`)
            return false
        }
        if (!form.name.trim()) {
            setFormFieldError("请填写档案名称")
            return false
        }
        setFormFieldError(null)
        return true
    }

    async function submitForm() {
        if (saving || !validateForm()) return
        setSaving(true)
        setFormError(null)
        try {
            const api = new AssistItemsApi(apiConfig(token))
            if (formMode === "edit" && editTarget?.id) {
                await api.accountsetsAccountSetIdAssistItemsIdPut({
                    accountSetId,
                    id: editTarget.id,
                    updateAssistItemRequest: {
                        name: form.name.trim(),
                        contact: form.contact.trim(),
                        remark: form.remark.trim(),
                    },
                })
            } else {
                await api.accountsetsAccountSetIdAssistItemsPost({
                    accountSetId,
                    createAssistItemRequest: {
                        type,
                        code: form.code.trim(),
                        name: form.name.trim(),
                        parentId: parentItem?.id ?? null,
                        contact: form.contact.trim() || null,
                        remark: form.remark.trim() || null,
                    },
                })
            }
            setFormOpen(false)
            await refresh()
        } catch (err) {
            setFormError(await apiErrorMessage(err, "保存档案失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function toggleEnabled(item: AssistItemDto) {
        if (busyId || !item.id) return
        setBusyId(item.id)
        setError(null)
        try {
            await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsIdEnabledPatch({
                accountSetId,
                id: item.id,
                setAssistItemEnabledRequest: { enabled: item.isEnabled !== true },
            })
            await refresh()
        } catch (err) {
            setError(await apiErrorMessage(err, "更新档案状态失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    async function removeItem(item: AssistItemDto) {
        if (busyId || !item.id) return
        if (!(await confirm(`确定删除档案「${item.code} ${item.name}」吗？此操作不可撤销。`))) return
        setBusyId(item.id)
        setError(null)
        try {
            await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsIdDelete({
                accountSetId,
                id: item.id,
            })
            await refresh()
        } catch (err) {
            setError(await apiErrorMessage(err, "删除档案失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    const levelOf = (item: AssistItemDto) => item.level ?? 1
    const createLevel = (parentItem?.level ?? 0) + 1
    const expectedLength = assistExpectedCodeLength(type, createLevel)

    return (
        <>
            <SectionCard
                title={<h3 className="text-[14px] font-semibold text-slate-800">{hierarchical ? "档案结构" : "档案列表"}</h3>}
                action={
                    <div className="flex items-center gap-2">
                        <Button variant="outline" className={CARD_BTN} onClick={() => void load()} disabled={loading}>
                            {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                            刷新
                        </Button>
                        <Button className={CARD_BTN_PRIMARY} onClick={() => void openCreate(null)} disabled={!categoryEnabled}>
                            <Plus data-icon="inline-start" />
                            新增档案
                        </Button>
                    </div>
                }
                bodyClassName="flex flex-col gap-4"
            >
                {!categoryEnabled && (
                    <Alert className="rounded-xl border-[#fde68a] bg-[#fef3c7] text-[#b45309]">
                        <AlertTitle>
                            该类别尚未在账套启用，不能新增档案。
                            <Button variant="link" size="xs" onClick={onCategoryEnabledChange}>
                                立即启用
                            </Button>
                        </AlertTitle>
                    </Alert>
                )}
                {error && (
                    <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                        <CircleAlert />
                        <AlertTitle>{error}</AlertTitle>
                    </Alert>
                )}

                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>编码</TableHead>
                            <TableHead>名称</TableHead>
                            <TableHead>联系资料</TableHead>
                            <TableHead>备注</TableHead>
                            <TableHead className="text-center">状态</TableHead>
                            <TableHead className="text-right">操作</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {loading && items.length === 0 ? (
                            <TableRow>
                                <TableCell colSpan={COLUMN_COUNT} className="py-8 text-center">
                                    <Spinner className="mx-auto size-5 text-slate-300" />
                                </TableCell>
                            </TableRow>
                        ) : topRows.length === 0 ? (
                            <TableRow>
                                <TableCell colSpan={COLUMN_COUNT} className="py-8 text-center text-slate-400">
                                    该类别暂无档案，可点击「新增档案」建立。
                                </TableCell>
                            </TableRow>
                        ) : (
                            topRows.map(({ node, depth }) => {
                                const id = String(node.id)
                                const expanded = expandedKeys.includes(id)
                                return (
                                    <TableRow key={id} className={node.isEnabled ? undefined : "text-slate-400"}>
                                        <TableCell>
                                            <div className={cn("flex items-center gap-1", indentClass(depth))}>
                                                {node.children ? (
                                                    <Button
                                                        variant="ghost"
                                                        size="icon-xs"
                                                        className={ICON_BTN}
                                                        aria-label={expanded ? "收起下级" : "展开下级"}
                                                        aria-expanded={expanded}
                                                        onClick={() =>
                                                            setExpandedKeys((prev) =>
                                                                prev.includes(id)
                                                                    ? prev.filter((k) => k !== id)
                                                                    : [...prev, id],
                                                            )
                                                        }
                                                    >
                                                        {expanded ? <ChevronDown /> : <ChevronRight />}
                                                    </Button>
                                                ) : (
                                                    <span className="size-6 shrink-0" />
                                                )}
                                                <span className="whitespace-nowrap">{node.code}</span>
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <span className={node.children ? "font-medium text-slate-800" : undefined}>{node.name}</span>
                                            <span className="ml-2 text-[11.5px] text-slate-400">{levelOf(node)} 级</span>
                                        </TableCell>
                                        <TableCell>{node.contact || "—"}</TableCell>
                                        <TableCell>{node.remark || "—"}</TableCell>
                                        <TableCell className="text-center">
                                            <StatusPill tone={node.isEnabled ? "green" : "gray"}>{node.isEnabled ? "启用" : "停用"}</StatusPill>
                                        </TableCell>
                                        <TableCell className="text-right">
                                            <div className="flex items-center justify-end gap-0">
                                                {hierarchical && (
                                                    <Button
                                                        variant="ghost"
                                                        size="icon-xs"
                                                        className={ICON_BTN}
                                                        title="新增下级档案"
                                                        onClick={() => void openCreate(node)}
                                                    >
                                                        <Plus />
                                                    </Button>
                                                )}
                                                <Button
                                                    variant="ghost"
                                                    size="icon-xs"
                                                    className={ICON_BTN}
                                                    title="编辑档案"
                                                    onClick={() => openEdit(node)}
                                                >
                                                    <UserPen />
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="xs"
                                                    className="text-[12px] text-slate-400 hover:bg-muted hover:text-slate-700"
                                                    disabled={busyId === id}
                                                    onClick={() => void toggleEnabled(node)}
                                                >
                                                    {node.isEnabled ? "停用" : "启用"}
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="icon-xs"
                                                    className={ICON_BTN_DANGER}
                                                    title={node.children ? "删除档案（需先删除下级）" : "删除档案"}
                                                    disabled={busyId === id}
                                                    onClick={() => void removeItem(node)}
                                                >
                                                    {busyId === id ? <Spinner /> : <Trash2 />}
                                                </Button>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                )
                            })
                        )}
                    </TableBody>
                </Table>
            </SectionCard>

            {/* 新增 / 编辑档案 */}
            <Dialog
                open={formOpen}
                onOpenChange={(next) => {
                    setFormOpen(next)
                    if (!next) {
                        setParentItem(null)
                        setEditTarget(null)
                    }
                }}
            >
                <DialogContent aria-describedby="assist-item-form-desc" className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>
                            {formMode === "edit"
                                ? `编辑档案 — ${editTarget?.code ?? ""}`
                                : parentItem
                                  ? `新增下级档案 — ${parentItem.code} ${parentItem.name}`
                                  : "新增档案"}
                        </DialogTitle>
                        <DialogDescription id="assist-item-form-desc">
                            {formMode === "create"
                                ? `编码在同类别内唯一，第 ${createLevel} 级应为 ${expectedLength} 位${
                                      hierarchical && parentItem ? `（以上级编码 ${parentItem.code} 为前缀）` : ""
                                  }。`
                                : "档案编码与所属类别不可变更。"}
                        </DialogDescription>
                    </DialogHeader>
                    <div className="flex flex-col gap-4">
                        {formMode === "create" && (
                            <Field data-invalid={Boolean(formFieldError && !form.code.trim())}>
                                <FieldLabel htmlFor="assist-code">档案编码</FieldLabel>
                                <Input
                                    id="assist-code"
                                    value={form.code}
                                    onChange={(e) => setForm((p) => ({ ...p, code: e.target.value }))}
                                    placeholder="建议编码已自动带出，可修改"
                                />
                            </Field>
                        )}
                        <Field data-invalid={Boolean(formFieldError && !form.name.trim())}>
                            <FieldLabel htmlFor="assist-name">档案名称</FieldLabel>
                            <Input
                                id="assist-name"
                                value={form.name}
                                onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))}
                                placeholder="如 杭州某某贸易有限公司"
                            />
                        </Field>
                        <Field>
                            <FieldLabel htmlFor="assist-contact">联系资料</FieldLabel>
                            <Input
                                id="assist-contact"
                                value={form.contact}
                                onChange={(e) => setForm((p) => ({ ...p, contact: e.target.value }))}
                                placeholder="联系人 / 税号等，可留空"
                            />
                        </Field>
                        <Field>
                            <FieldLabel htmlFor="assist-remark">备注</FieldLabel>
                            <Textarea
                                id="assist-remark"
                                value={form.remark}
                                onChange={(e) => setForm((p) => ({ ...p, remark: e.target.value }))}
                                rows={2}
                            />
                        </Field>
                        {formFieldError && <FieldError>{formFieldError}</FieldError>}
                        {formError && (
                            <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{formError}</AlertTitle>
                            </Alert>
                        )}
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setFormOpen(false)}>
                            取消
                        </Button>
                        <Button onClick={() => void submitForm()} disabled={saving}>
                            {saving ? <Spinner data-icon="inline-start" /> : <Plus data-icon="inline-start" />}
                            保存
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {confirmDialog}
        </>
    )
}
