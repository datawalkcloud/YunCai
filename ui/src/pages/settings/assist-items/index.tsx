import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, Button, Checkbox, Input, Modal, Spin, Table } from "antd"
import type { TableColumnsType } from "antd"
import {
    ApartmentOutlined,
    DeleteOutlined,
    DownOutlined,
    EditOutlined,
    PlusOutlined,
    ReloadOutlined,
    RightOutlined,
} from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { AccountSetsApi, AssistItemsApi, type AssistCategoryStatDto, type AssistItemDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { useConfirm } from "@/hooks/useConfirm"
import { cn } from "@/lib/utils"
import { ASSIST_CODE_MAX_LENGTH, assistExpectedCodeLength, assistSupportsHierarchy } from "@/components/assist/constants"
import { invalidateAssistItems } from "@/components/assist/assist-store"
import { PageHeader, SectionCard } from "@/components/page/section"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"

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
                icon={ApartmentOutlined}
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
                        {categoriesLoading && categories.length === 0 && (
                            <div className="flex justify-center py-4">
                                <Spin />
                            </div>
                        )}
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
                                        <span
                                            className={cn(
                                                "text-[13px]",
                                                type === activeType
                                                    ? "font-medium text-[#2f6bff]"
                                                    : "text-slate-600",
                                            )}
                                        >
                                            {c.typeText}
                                        </span>
                                        {hierarchical && <span className="text-[11px] text-slate-400">层级</span>}
                                        <span className="ml-auto text-[11.5px] text-slate-400 tabular-nums">
                                            {c.itemCount ?? 0}
                                        </span>
                                    </button>
                                    <label className="flex items-center gap-1 text-[11.5px] text-slate-400">
                                        <Checkbox
                                            checked={c.isEnabled === true}
                                            disabled={switchingType !== null}
                                            onChange={(e) => void toggleCategory(type, e.target.checked)}
                                            aria-label={`启用${c.typeText ?? ""}类别`}
                                        />
                                        启用
                                    </label>
                                </div>
                            )
                        })}
                        {categoriesError && <Alert className="mt-2" type="error" showIcon title={categoriesError} />}
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

    const rows = useMemo<VisibleRow[]>(
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

    const columns: TableColumnsType<VisibleRow> = [
        {
            title: "编码",
            render: (_, { node, depth }) => (
                <div className={cn("flex items-center gap-1", indentClass(depth))}>
                    {node.children ? (
                        <Button
                            type="text"
                            size="small"
                            aria-label={
                                expandedKeys.includes(String(node.id)) ? "收起下级" : "展开下级"
                            }
                            aria-expanded={expandedKeys.includes(String(node.id))}
                            icon={
                                expandedKeys.includes(String(node.id)) ? (
                                    <DownOutlined />
                                ) : (
                                    <RightOutlined />
                                )
                            }
                            onClick={() =>
                                setExpandedKeys((prev) =>
                                    prev.includes(String(node.id))
                                        ? prev.filter((k) => k !== String(node.id))
                                        : [...prev, String(node.id)],
                                )
                            }
                        />
                    ) : (
                        <span className="size-6 shrink-0" />
                    )}
                    <span className="whitespace-nowrap">{node.code}</span>
                </div>
            ),
        },
        {
            title: "名称",
            render: (_, { node }) => (
                <>
                    <span className={node.children ? "font-medium text-slate-800" : undefined}>
                        {node.name}
                    </span>
                    <span className="ml-2 text-[11.5px] text-slate-400">{levelOf(node)} 级</span>
                </>
            ),
        },
        {
            title: "联系资料",
            render: (_, { node }) => node.contact || "—",
        },
        {
            title: "备注",
            render: (_, { node }) => node.remark || "—",
        },
        {
            title: "状态",
            align: "center",
            render: (_, { node }) => (
                <StatusPill tone={node.isEnabled ? "green" : "gray"}>
                    {node.isEnabled ? "启用" : "停用"}
                </StatusPill>
            ),
        },
        {
            title: "操作",
            align: "right",
            render: (_, { node }) => {
                const id = String(node.id)
                return (
                    <div className="flex items-center justify-end gap-0">
                        {hierarchical && (
                            <Button
                                type="text"
                                size="small"
                                title="新增下级档案"
                                icon={<PlusOutlined />}
                                onClick={() => void openCreate(node)}
                            />
                        )}
                        <Button
                            type="text"
                            size="small"
                            title="编辑档案"
                            icon={<EditOutlined />}
                            onClick={() => openEdit(node)}
                        />
                        <Button
                            type="text"
                            size="small"
                            className="text-[12px] text-slate-400"
                            disabled={busyId === id}
                            onClick={() => void toggleEnabled(node)}
                        >
                            {node.isEnabled ? "停用" : "启用"}
                        </Button>
                        <Button
                            type="text"
                            size="small"
                            danger
                            title={node.children ? "删除档案（需先删除下级）" : "删除档案"}
                            disabled={busyId === id}
                            icon={<DeleteOutlined />}
                            onClick={() => void removeItem(node)}
                        />
                    </div>
                )
            },
        },
    ]

    return (
        <>
            <SectionCard
                title={<h3 className="text-[14px] font-semibold text-slate-800">{hierarchical ? "档案结构" : "档案列表"}</h3>}
                action={
                    <div className="flex items-center gap-2">
                        <Button icon={<ReloadOutlined />} onClick={() => void load()} disabled={loading}>
                            刷新
                        </Button>
                        <Button
                            type="primary"
                            icon={<PlusOutlined />}
                            onClick={() => void openCreate(null)}
                            disabled={!categoryEnabled}
                        >
                            新增档案
                        </Button>
                    </div>
                }
                bodyClassName="flex flex-col gap-4"
            >
                {!categoryEnabled && (
                    <Alert
                        type="warning"
                        showIcon
                        title={
                            <>
                                该类别尚未在账套启用，不能新增档案。
                                <Button type="link" size="small" onClick={onCategoryEnabledChange}>
                                    立即启用
                                </Button>
                            </>
                        }
                    />
                )}
                {error && <Alert type="error" showIcon title={error} />}

                {loading && items.length === 0 ? (
                    <div className="flex justify-center py-8">
                        <Spin />
                    </div>
                ) : (
                    <>
                        <Table
                            columns={columns}
                            dataSource={rows}
                            rowKey={(row) => String(row.node.id)}
                            rowClassName={(row) => (row.node.isEnabled ? "" : "text-slate-400")}
                            pagination={false}
                            size="small"
                            locale={{ emptyText: "该类别暂无档案，可点击「新增档案」建立。" }}
                        />
                        {/* 页脚：与柠檬云一致的「显示 x 到 y，共 z 条」 */}
                        <div className="flex items-center justify-between border-t border-slate-100 px-4 py-2.5 text-[12.5px] text-slate-500">
                            <span>
                                显示 {rows.length ? 1 : 0} 到 {Math.min(rows.length, 20)}，共 {rows.length} 条
                            </span>
                            <span className="flex items-center gap-2">
                                <span className="inline-flex h-7 items-center rounded border border-slate-200 px-2">20</span>
                                <span className="inline-flex h-7 items-center rounded border border-slate-200 px-2 text-slate-400">‹</span>
                                <span className="inline-flex h-7 w-8 items-center justify-center rounded border border-[#2f6bff] text-[#2f6bff]">1</span>
                                <span className="inline-flex h-7 items-center rounded border border-slate-200 px-2 text-slate-400">›</span>
                            </span>
                        </div>
                    </>
                )}
            </SectionCard>

            {/* 新增 / 编辑档案 */}
            <Modal
                open={formOpen}
                width={448}
                title={
                    formMode === "edit"
                        ? `编辑档案 — ${editTarget?.code ?? ""}`
                        : parentItem
                          ? `新增下级档案 — ${parentItem.code} ${parentItem.name}`
                          : "新增档案"
                }
                okText="保存"
                confirmLoading={saving}
                onOk={() => void submitForm()}
                onCancel={() => {
                    setFormOpen(false)
                    setParentItem(null)
                    setEditTarget(null)
                }}
            >
                <p className="mb-4 text-sm text-slate-400">
                    {formMode === "create"
                        ? `编码在同类别内唯一，第 ${createLevel} 级应为 ${expectedLength} 位${
                              hierarchical && parentItem ? `（以上级编码 ${parentItem.code} 为前缀）` : ""
                          }。`
                        : "档案编码与所属类别不可变更。"}
                </p>
                <div className="flex flex-col gap-4">
                    {formMode === "create" && (
                        <FormField label="档案编码" htmlFor="assist-code">
                            <Input
                                id="assist-code"
                                value={form.code}
                                onChange={(e) => setForm((p) => ({ ...p, code: e.target.value }))}
                                placeholder="建议编码已自动带出，可修改"
                            />
                        </FormField>
                    )}
                    <FormField label="档案名称" htmlFor="assist-name">
                        <Input
                            id="assist-name"
                            value={form.name}
                            onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))}
                            placeholder="如 杭州某某贸易有限公司"
                        />
                    </FormField>
                    <FormField label="联系资料" htmlFor="assist-contact">
                        <Input
                            id="assist-contact"
                            value={form.contact}
                            onChange={(e) => setForm((p) => ({ ...p, contact: e.target.value }))}
                            placeholder="联系人 / 税号等，可留空"
                        />
                    </FormField>
                    <FormField label="备注" htmlFor="assist-remark">
                        <Input.TextArea
                            id="assist-remark"
                            value={form.remark}
                            onChange={(e) => setForm((p) => ({ ...p, remark: e.target.value }))}
                            rows={2}
                        />
                    </FormField>
                    {formFieldError && <p className="text-sm text-[#ef4444]">{formFieldError}</p>}
                    {formError && <Alert type="error" showIcon title={formError} />}
                </div>
            </Modal>

            {confirmDialog}
        </>
    )
}
