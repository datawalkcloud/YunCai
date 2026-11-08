import { useCallback, useEffect, useMemo, useState } from "react"
import type { ReactNode } from "react"
import {
    Alert,
    App,
    Button,
    Card,
    Input,
    Modal,
    Spin,
    Switch,
    Table,
    Tag,
    Tooltip,
    Typography,
    Upload,
} from "antd"
import type { TableColumnsType, UploadFile } from "antd"
import {
    ApartmentOutlined,
    BankOutlined,
    DownOutlined,
    DownloadOutlined,
    IdcardOutlined,
    InboxOutlined,
    PlusOutlined,
    ProjectOutlined,
    ReloadOutlined,
    RightOutlined,
    SearchOutlined,
    ShopOutlined,
    UploadOutlined,
    UserOutlined,
} from "@ant-design/icons"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { AccountSetsApi, AssistItemsApi, type AssistCategoryStatDto, type AssistItemDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { useConfirm } from "@/hooks/useConfirm"
import { cn } from "@/lib/utils"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"
import {
    ASSIST_CODE_MAX_LENGTH,
    ASSIST_HIERARCHY_LEVEL_LENGTH,
    ASSIST_SEQUENCE_CODE_LENGTH,
    assistExpectedCodeLength,
    assistLabel,
    assistSupportsHierarchy,
} from "@/components/assist/constants"
import { invalidateAssistItems } from "@/components/assist/assist-store"

/** 类别图标与说明（类别名与是否层级统一取自 assist/constants）。 */
const CATEGORY_META: Record<number, { icon: ReactNode; description: string }> = {
    1: { icon: <UserOutlined />, description: "销售往来客户，同时作为资金往来的对方单位" },
    2: { icon: <ShopOutlined />, description: "采购与付款往来单位" },
    3: { icon: <IdcardOutlined />, description: "员工档案，用于员工往来与费用报销" },
    4: { icon: <ApartmentOutlined />, description: "按组织架构归集费用，支持多级" },
    5: { icon: <ProjectOutlined />, description: "按项目归集收入与费用，支持多级" },
    6: { icon: <InboxOutlined />, description: "存货档案，用于库存与成本核算，支持多级" },
    7: { icon: <BankOutlined />, description: "现金流量项目，现金类科目凭证使用，支持多级" },
}

/** 层级缩进：层级型类别最多十几级，够用即止。 */
const INDENT_CLASS = ["", "pl-5", "pl-10", "pl-15", "pl-20", "pl-25", "pl-30", "pl-[8.75rem]"]
const indentClass = (depth: number) => INDENT_CLASS[Math.min(depth, INDENT_CLASS.length - 1)]

/** 档案树节点 + 渲染深度。 */
interface ItemNode extends AssistItemDto {
    children?: ItemNode[]
}
interface VisibleRow {
    node: ItemNode
    depth: number
}

interface ItemForm {
    code: string
    name: string
    contact: string
    remark: string
}

const emptyForm: ItemForm = { code: "", name: "", contact: "", remark: "" }

/** 导入文件解析后的一行。 */
interface ImportRow {
    line: number
    code: string
    name: string
    parentCode: string
    contact: string
    remark: string
}

interface ImportResult {
    totalRows: number
    imported: number
    skipped: number
    warnings: string[]
}

export default function AssistItemsPage() {
    const { message } = App.useApp()
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

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
     * 已有档案的类别由后端拒绝关闭，卡片上的开关也会提前禁用。
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
            message.success(next ? `已启用「${assistLabel(type)}」辅助核算` : `已关闭「${assistLabel(type)}」辅助核算`)
            await loadCategories()
        } catch (err) {
            message.error(await apiErrorMessage(err, "更新类别启用状态失败，请重试。"))
            await loadCategories()
        } finally {
            setSwitchingType(null)
        }
    }

    const active = categories.find((c) => c.type === activeType)
    const enabledCount = categories.filter((c) => c.isEnabled).length

    if (!accountSetId) {
        return (
            <Card>
                <span className="text-[13px] text-(--muted-foreground)">请先在左上角选择账套。</span>
            </Card>
        )
    }

    return (
        <div className="flex flex-col gap-3">
            <Card>
                <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                        <h2 className="text-[15px] font-semibold text-(--foreground)">辅助核算类别</h2>
                        <p className="mt-1 text-[12.5px] leading-5 text-(--muted-foreground)">
                            选择类别维护档案。客户 / 供应商 / 职员是往来单位基础档案，可直接维护；部门 / 项目 /
                            存货 / 现金流需先启用类别，才能在科目上挂该维度。
                        </p>
                    </div>
                    <span className="shrink-0 text-[12.5px] text-(--muted-foreground)">
                        已启用 <span className="font-semibold tabular-nums text-(--foreground)">{enabledCount}</span> /{" "}
                        {categories.length || 7} 个类别
                    </span>
                </div>

                {categoriesLoading && categories.length === 0 ? (
                    <div className="flex justify-center py-8">
                        <Spin />
                    </div>
                ) : (
                    <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-7">
                        {categories.map((c) => {
                            const type = c.type as number
                            const selected = type === activeType
                            const meta = CATEGORY_META[type]
                            const lockedOn = c.isEnabled === true && (c.itemCount ?? 0) > 0
                            return (
                                <div
                                    key={type}
                                    role="button"
                                    tabIndex={0}
                                    title={meta?.description}
                                    aria-pressed={selected}
                                    onClick={() => setActiveType(type)}
                                    onKeyDown={(e) => {
                                        if (e.key === "Enter" || e.key === " ") setActiveType(type)
                                    }}
                                    className={cn(
                                        "group flex cursor-pointer flex-col gap-3 border p-3 text-left transition-colors outline-none",
                                        selected
                                            ? "border-(--brand) bg-(--accent)"
                                            : "border-(--border) bg-(--card) hover:border-(--border) hover:bg-(--muted)",
                                    )}
                                >
                                    <div className="flex items-center justify-between gap-2">
                                        <span className="flex min-w-0 items-center gap-2">
                                            <span
                                                className="flex size-7 shrink-0 items-center justify-center text-[15px]"
                                                style={{
                                                    background: selected ? "var(--brand)" : "var(--muted)",
                                                    color: selected ? "var(--primary-foreground)" : "var(--tone-gray)",
                                                }}
                                            >
                                                {meta?.icon}
                                            </span>
                                            <span
                                                className={cn(
                                                    "truncate text-[13px] font-medium",
                                                    selected ? "text-(--brand)" : "text-(--foreground)",
                                                )}
                                            >
                                                {c.typeText ?? assistLabel(type)}
                                            </span>
                                        </span>
                                        <span onClick={(e) => e.stopPropagation()}>
                                            <Tooltip
                                                title={
                                                    lockedOn
                                                        ? "该类别下已有档案，需先删除档案才能关闭"
                                                        : c.isEnabled
                                                          ? "关闭后该维度不能在新凭证中使用，历史数据保留"
                                                          : assistSupportsHierarchy(type)
                                                            ? "启用后可在科目上挂该辅助核算维度"
                                                            : "客户 / 供应商 / 职员不启用也可维护往来单位档案"
                                                }
                                            >
                                                <Switch
                                                    size="small"
                                                    checked={c.isEnabled === true}
                                                    loading={switchingType === type}
                                                    disabled={switchingType !== null || lockedOn}
                                                    onChange={(next) => void toggleCategory(type, next)}
                                                    aria-label={`${c.typeText ?? ""}类别启用`}
                                                />
                                            </Tooltip>
                                        </span>
                                    </div>
                                    <div className="flex items-end justify-between gap-2">
                                        <span className="text-[22px] leading-none font-semibold text-(--foreground) tabular-nums">
                                            {c.itemCount ?? 0}
                                        </span>
                                        <span className="flex items-center gap-1">
                                            {assistSupportsHierarchy(type) && (
                                                <Tag>
                                                    多级
                                                </Tag>
                                            )}
                                            <span
                                                className={cn(
                                                    "text-[11.5px]",
                                                    c.isEnabled ? "text-(--success)" : "text-(--muted-foreground)",
                                                )}
                                            >
                                                {c.isEnabled ? "已启用" : "未启用"}
                                            </span>
                                        </span>
                                    </div>
                                </div>
                            )
                        })}
                    </div>
                )}
                {categoriesError && <Alert className="mt-3" type="error" showIcon title={categoriesError} />}
            </Card>

            <AssistItemPanel
                key={`${accountSetId}-${activeType}`}
                accountSetId={accountSetId}
                token={token}
                type={activeType}
                categoryEnabled={active?.isEnabled === true}
                onCategoryEnabledChange={() => void toggleCategory(activeType, true)}
                onChanged={() => void loadCategories()}
            />
        </div>
    )
}

/** 某类别的档案列表：层级型可展开树表，平铺型为列表；工具栏承载查询、导入导出与新增。 */
function AssistItemPanel({
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
    const { message } = App.useApp()
    const hierarchical = assistSupportsHierarchy(type)
    const typeName = assistLabel(type) || "辅助核算"
    const { confirm, confirmDialog } = useConfirm()

    const [items, setItems] = useState<AssistItemDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [expandedKeys, setExpandedKeys] = useState<string[]>([])
    /** 关键字查询（编码/名称/联系资料/备注），点「查询」或回车生效。 */
    const [keywordInput, setKeywordInput] = useState("")
    const [keyword, setKeyword] = useState("")
    const [busyId, setBusyId] = useState<string | null>(null)

    const [formOpen, setFormOpen] = useState(false)
    const [formMode, setFormMode] = useState<"create" | "edit">("create")
    const [parentItem, setParentItem] = useState<AssistItemDto | null>(null)
    const [editTarget, setEditTarget] = useState<AssistItemDto | null>(null)
    const [form, setForm] = useState<ItemForm>(emptyForm)
    const [formError, setFormError] = useState<string | null>(null)
    const [formFieldError, setFormFieldError] = useState<string | null>(null)
    const [codeLoading, setCodeLoading] = useState(false)
    const [saving, setSaving] = useState(false)

    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<UploadFile | null>(null)
    const [importing, setImporting] = useState(false)
    const [importResult, setImportResult] = useState<ImportResult | null>(null)

    /** 层级型类别未启用时后端会拒绝新增，平铺型类别（往来单位）不受类别开关限制。 */
    const requiresEnabledCategory = hierarchical
    const createBlocked = requiresEnabledCategory && !categoryEnabled

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
    const refresh = useCallback(async () => {
        invalidateAssistItems(accountSetId, type)
        await load()
        onChanged()
    }, [accountSetId, type, load, onChanged])

    /* ---------------- 查询与树 ---------------- */

    const term = keyword.trim()
    const matchedIds = useMemo(() => {
        if (!term) return null
        return new Set(
            items
                .filter((i) =>
                    [i.code, i.name, i.contact, i.remark].some((value) => (value ?? "").includes(term)),
                )
                .map((i) => String(i.id)),
        )
    }, [items, term])

    const treeData = useMemo<ItemNode[]>(() => {
        const byCode = (a: AssistItemDto, b: AssistItemDto) => (a.code ?? "").localeCompare(b.code ?? "")
        const build = (pid: string | null): ItemNode[] =>
            items
                .filter((i) => (i.parentId ?? null) === pid)
                .sort(byCode)
                .map((i) => {
                    const children = hierarchical ? build(i.id ?? null) : []
                    return { ...i, children: children.length > 0 ? children : undefined }
                })
                // 查询时保留命中节点与其祖先，未命中的整枝剪掉。
                .filter((node) => {
                    if (!matchedIds) return true
                    if (matchedIds.has(String(node.id))) return true
                    return (node.children ?? []).some((child) => containsMatch(child, matchedIds))
                })
        return build(null)
    }, [items, hierarchical, matchedIds])

    const rows = useMemo<VisibleRow[]>(() => {
        const expanded = term ? null : new Set(expandedKeys)
        const out: VisibleRow[] = []
        const walk = (nodes: ItemNode[], depth: number) => {
            for (const node of nodes) {
                out.push({ node, depth })
                if (node.children && (expanded === null || expanded.has(String(node.id)))) {
                    walk(node.children, depth + 1)
                }
            }
        }
        walk(treeData, 0)
        return out
    }, [treeData, expandedKeys, term])

    const filteredItems = useMemo(
        () =>
            (matchedIds ? items.filter((i) => matchedIds.has(String(i.id))) : items)
                .slice()
                .sort((a, b) => (a.code ?? "").localeCompare(b.code ?? "")),
        [items, matchedIds],
    )

    const enabledCount = items.filter((i) => i.isEnabled !== false).length
    const parentCodeById = useMemo(() => {
        const codeById = new Map<string, string>()
        for (const item of items) if (item.id && item.code) codeById.set(item.id, item.code)
        const map = new Map<string, string>()
        for (const item of items) {
            if (!item.id || !item.parentId) continue
            const parentCode = codeById.get(item.parentId)
            if (parentCode) map.set(item.id, parentCode)
        }
        return map
    }, [items])

    /* ---------------- 新增 / 编辑 ---------------- */

    /** 打开新增弹窗并按 next-code 预填建议编码；层级型未启用时不发请求。 */
    async function openCreate(parent: AssistItemDto | null) {
        if (createBlocked) return
        setFormMode("create")
        setParentItem(parent)
        setEditTarget(null)
        setForm(emptyForm)
        setFormError(null)
        setFormFieldError(null)
        setFormOpen(true)
        setCodeLoading(true)
        try {
            const code = await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsNextCodeGet({
                accountSetId,
                type,
                parentId: parent?.id ?? undefined,
            })
            setForm((prev) => ({ ...prev, code }))
        } catch (err) {
            setFormError(await apiErrorMessage(err, "获取建议编码失败，可手动填写。"))
        } finally {
            setCodeLoading(false)
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

    function closeForm() {
        setFormOpen(false)
        setParentItem(null)
        setEditTarget(null)
        setFormFieldError(null)
    }

    const createLevel = (parentItem?.level ?? 0) + 1
    const expectedLength = assistExpectedCodeLength(type, createLevel)

    /** 本地校验编码规则与必填，给出与后端一致的中文提示；保存仍以后端校验为准。 */
    function validateForm(): boolean {
        if (formMode === "create") {
            const code = form.code.trim()
            if (!code) {
                setFormFieldError("请填写档案编码")
                return false
            }
            if (code.length > ASSIST_CODE_MAX_LENGTH) {
                setFormFieldError(`档案编码不能超过 ${ASSIST_CODE_MAX_LENGTH} 个字符`)
                return false
            }
            if (code.length !== expectedLength) {
                setFormFieldError(
                    `第 ${createLevel} 级档案编码应为 ${expectedLength} 位，当前为 ${code.length} 位`,
                )
                return false
            }
            if (parentItem?.code && !code.startsWith(parentItem.code)) {
                setFormFieldError(`编码须以上级编码「${parentItem.code}」为前缀`)
                return false
            }
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
        const creating = formMode === "create"
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
            message.success(creating ? "档案已新增" : "档案已保存")
            closeForm()
            await refresh()
        } catch (err) {
            setFormError(await apiErrorMessage(err, "保存档案失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    /* ---------------- 启停 / 删除 ---------------- */

    async function toggleEnabled(item: AssistItemDto, enabled: boolean) {
        if (busyId || !item.id) return
        setBusyId(item.id)
        setError(null)
        try {
            await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsIdEnabledPatch({
                accountSetId,
                id: item.id,
                setAssistItemEnabledRequest: { enabled },
            })
            setItems((prev) => prev.map((i) => (i.id === item.id ? { ...i, isEnabled: enabled } : i)))
            message.success(enabled ? "档案已启用" : "档案已停用")
        } catch (err) {
            message.error(await apiErrorMessage(err, "更新档案状态失败，请重试。"))
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
            message.success("档案已删除")
            await refresh()
        } catch (err) {
            message.error(await apiErrorMessage(err, "删除档案失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    /* ---------------- 导出 / 导入 ---------------- */

    const exportCsv = () => {
        const header = ["编码", "名称", "级次", "上级编码", "联系资料", "备注", "状态"]
        const lines = [header.join(",")]
        for (const item of filteredItems) {
            const row = [
                item.code ?? "",
                item.name ?? "",
                String(item.level ?? 1),
                item.id ? (parentCodeById.get(item.id) ?? "") : "",
                item.contact ?? "",
                item.remark ?? "",
                item.isEnabled === false ? "停用" : "启用",
            ]
            lines.push(row.map((v) => `"${v.replace(/"/g, '""')}"`).join(","))
        }
        downloadText(`${typeName}档案.csv`, lines.join("\r\n"))
    }

    const downloadTemplate = () => {
        const header = "编码,名称,上级编码,联系资料,备注"
        const sample = hierarchical
            ? ["01,示例部门,,,", "0101,示例子部门,01,,"].join("\r\n")
            : "0001,示例往来单位,,联系人 13800000000,"
        downloadText(`${typeName}档案导入模板.csv`, `${header}\r\n${sample}`)
    }

    async function submitImport() {
        if (importing) return
        const file = importFile?.originFileObj
        if (!file) {
            message.warning("请选择要导入的 CSV 文件。")
            return
        }
        setImporting(true)
        try {
            const text = await readCsvFile(file)
            const parsed = parseCsv(text)
            const rows = toImportRows(parsed)
            if (rows.length === 0) throw new Error("文件中没有可导入的数据行。")

            const result = await importRows(rows)
            setImportResult(result)
            setImportOpen(false)
            setImportFile(null)
            await refresh()
        } catch (err) {
            message.error(err instanceof Error ? err.message : "导入失败，请重试。")
        } finally {
            setImporting(false)
        }
    }

    /** 逐行创建：编码留空自动生成，多级先建上级；单行失败记录告警后继续。 */
    async function importRows(rows: ImportRow[]): Promise<ImportResult> {
        const api = new AssistItemsApi(apiConfig(token))
        const known = new Map<string, AssistItemDto>()
        for (const item of items) if (item.code) known.set(item.code, item)
        const taken = new Set(known.keys())

        const warnings: string[] = []
        let imported = 0
        let skipped = 0

        const ordered = [...rows].sort(
            (a, b) => a.code.trim().length - b.code.trim().length || a.code.trim().localeCompare(b.code.trim()),
        )

        for (const row of ordered) {
            const name = row.name.trim()
            if (!name) {
                warnings.push(`第 ${row.line} 行：名称不能为空，已跳过。`)
                skipped++
                continue
            }

            let code = normalizeNumericCode(row.code.trim(), hierarchical)
            let parentCode = normalizeNumericCode(row.parentCode.trim(), hierarchical)
            if (!code) {
                code = localNextCode(type, hierarchical ? parentCode || null : null, taken)
            }

            if (hierarchical) {
                if (code.length > ASSIST_CODE_MAX_LENGTH || code.length % ASSIST_HIERARCHY_LEVEL_LENGTH !== 0) {
                    warnings.push(
                        `第 ${row.line} 行：编码「${code}」应为 ${ASSIST_HIERARCHY_LEVEL_LENGTH} 的整数倍位，已跳过。`,
                    )
                    skipped++
                    continue
                }
                if (code.length > ASSIST_HIERARCHY_LEVEL_LENGTH) {
                    const inferred = code.slice(0, -ASSIST_HIERARCHY_LEVEL_LENGTH)
                    if (parentCode && parentCode !== inferred) {
                        warnings.push(`第 ${row.line} 行：上级编码「${parentCode}」与编码前缀「${inferred}」不一致，已跳过。`)
                        skipped++
                        continue
                    }
                    parentCode = inferred
                }
            } else if (code.length !== ASSIST_SEQUENCE_CODE_LENGTH) {
                warnings.push(
                    `第 ${row.line} 行：${typeName}编码应为 ${ASSIST_SEQUENCE_CODE_LENGTH} 位，已跳过。`,
                )
                skipped++
                continue
            }

            if (taken.has(code)) {
                warnings.push(`第 ${row.line} 行：编码「${code}」已存在，已跳过。`)
                skipped++
                continue
            }

            let parentId: string | undefined
            if (hierarchical && parentCode) {
                const parent = known.get(parentCode)
                if (!parent?.id) {
                    warnings.push(`第 ${row.line} 行：上级档案「${parentCode}」不存在，请先导入上级，已跳过。`)
                    skipped++
                    continue
                }
                parentId = parent.id
            }

            try {
                const created = await api.accountsetsAccountSetIdAssistItemsPost({
                    accountSetId,
                    createAssistItemRequest: {
                        type,
                        code,
                        name,
                        parentId: parentId ?? null,
                        contact: row.contact.trim() || null,
                        remark: row.remark.trim() || null,
                    },
                })
                known.set(code, created)
                taken.add(code)
                imported++
            } catch (err) {
                warnings.push(`第 ${row.line} 行：${await apiErrorMessage(err, "导入失败")}`)
                skipped++
            }
        }

        return { totalRows: rows.length, imported, skipped, warnings }
    }

    /** 解析导入 CSV：表头顺序不限，「名称」列必填。 */
    function toImportRows(csvRows: string[][]): ImportRow[] {
        const header = csvRows[0]?.map((c) => c.trim()) ?? []
        const aliases: Record<string, keyof Omit<ImportRow, "line">> = {
            编码: "code",
            档案编码: "code",
            名称: "name",
            档案名称: "name",
            上级编码: "parentCode",
            上级: "parentCode",
            联系资料: "contact",
            联系人: "contact",
            备注: "remark",
        }
        const columns = new Map<keyof Omit<ImportRow, "line">, number>()
        header.forEach((h, index) => {
            const key = aliases[h]
            if (key && !columns.has(key)) columns.set(key, index)
        })
        if (!columns.has("name")) {
            throw new Error("模板不正确：缺少「名称」列。请先下载导入模板。")
        }

        return csvRows.slice(1).map((cells, index) => ({
            line: index + 2,
            code: cells[columns.get("code") ?? -1] ?? "",
            name: cells[columns.get("name") ?? -1] ?? "",
            parentCode: cells[columns.get("parentCode") ?? -1] ?? "",
            contact: cells[columns.get("contact") ?? -1] ?? "",
            remark: cells[columns.get("remark") ?? -1] ?? "",
        }))
    }

    /* ---------------- 表格 ---------------- */

    const columns: TableColumnsType<VisibleRow> = [
        {
            title: `${typeName}编码`,
            width: hierarchical ? 260 : 180,
            render: (_, { node, depth }) => {
                const expanded = expandedKeys.includes(String(node.id))
                return (
                    <div className={cn("flex items-center gap-1", indentClass(depth))}>
                        {node.children ? (
                            <Button
                                type="text"
                                size="small"
                                aria-label={expanded ? "收起下级" : "展开下级"}
                                aria-expanded={expanded}
                                icon={expanded ? <DownOutlined /> : <RightOutlined />}
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
                        <span className="whitespace-nowrap font-mono text-[13px] tabular-nums text-(--foreground)">
                            {node.code}
                        </span>
                    </div>
                )
            },
        },
        {
            title: `${typeName}名称`,
            render: (_, { node }) => (
                <span className="flex flex-wrap items-center gap-2">
                    <span className={node.children ? "font-medium text-(--foreground)" : "text-(--foreground)"}>{node.name}</span>
                    {hierarchical && node.level != null && (
                        <span className="text-[11.5px] text-(--muted-foreground)">{node.level} 级</span>
                    )}
                </span>
            ),
        },
        {
            title: "联系资料",
            width: 190,
            render: (_, { node }) => node.contact || <span className="text-(--muted-foreground)">—</span>,
        },
        {
            title: "备注",
            render: (_, { node }) => node.remark || <span className="text-(--muted-foreground)">—</span>,
        },
        {
            title: "启用",
            width: 90,
            align: "center",
            render: (_, { node }) => (
                <Switch
                    size="small"
                    checked={node.isEnabled !== false}
                    loading={busyId === String(node.id)}
                    onChange={(next) => void toggleEnabled(node, next)}
                />
            ),
        },
        {
            title: "操作",
            width: 190,
            align: "right",
            render: (_, { node }) => {
                const id = String(node.id)
                return (
                    <span className="flex items-center justify-end gap-3">
                        {hierarchical && (
                            <Typography.Link className="text-[13px]" onClick={() => void openCreate(node)}>
                                新增下级
                            </Typography.Link>
                        )}
                        <Typography.Link className="text-[13px]" onClick={() => openEdit(node)}>
                            编辑
                        </Typography.Link>
                        <Typography.Link
                            className={cn("text-[13px]", busyId === id ? "pointer-events-none opacity-50" : "")}
                            style={{ color: "var(--tone-red)" }}
                            onClick={() => void removeItem(node)}
                        >
                            删除
                        </Typography.Link>
                    </span>
                )
            },
        },
    ]

    const emptyText = term ? (
        <div className="flex flex-col items-center gap-2 py-10">
            <SearchOutlined style={{ fontSize: 26, color: "var(--icon-placeholder)" }} />
            <span className="text-[13px] text-(--muted-foreground)">没有匹配「{term}」的档案</span>
            <Button
                type="link"
                size="small"
                onClick={() => {
                    setKeywordInput("")
                    setKeyword("")
                }}
            >
                清除查询条件
            </Button>
        </div>
    ) : (
        <div className="flex flex-col items-center gap-3 py-10">
            <InboxOutlined style={{ fontSize: 30, color: "var(--icon-placeholder)" }} />
            <div className="text-center">
                <div className="text-[13px] text-(--muted-foreground)">还没有{typeName}档案</div>
                <div className="mt-1 text-[12px] text-(--muted-foreground)">
                    {createBlocked
                        ? `请先启用「${typeName}」辅助核算类别`
                        : "新增后可在此维护，也能在凭证与资金往来中选用"}
                </div>
            </div>
            {!createBlocked && (
                <span className="flex items-center gap-2">
                    <Button type="primary" size="small" icon={<PlusOutlined />} onClick={() => void openCreate(null)}>
                        新增第一个档案
                    </Button>
                    <Button size="small" icon={<UploadOutlined />} onClick={() => setImportOpen(true)}>
                        批量导入
                    </Button>
                </span>
            )}
        </div>
    )

    return (
        <>
            <Card>
                <div className="flex flex-wrap items-start justify-between gap-3 px-4 pt-4">
                    <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                            <h3 className="text-[15px] font-semibold text-(--foreground)">{typeName}档案</h3>
                            <Tag>
                                {hierarchical ? "多级档案" : "平铺档案"}
                            </Tag>
                            {!categoryEnabled && (
                                <StatusPill tone="gray">{hierarchical ? "类别未启用" : "未参与辅助核算"}</StatusPill>
                            )}
                        </div>
                        <p className="mt-1 text-[12.5px] leading-5 text-(--muted-foreground)">
                            {hierarchical
                                ? `编码每级 ${ASSIST_HIERARCHY_LEVEL_LENGTH} 位（如 01 / 0101），仅启用的末级档案可在凭证中选用。`
                                : `编码为 ${ASSIST_SEQUENCE_CODE_LENGTH} 位流水号（如 0001），启用后可在凭证与资金往来中选用。`}
                        </p>
                    </div>
                    <Button
                        icon={<ReloadOutlined spin={loading} />}
                        onClick={() => void refresh()}
                        disabled={loading}
                    >
                        刷新
                    </Button>
                </div>

                {!categoryEnabled && (
                    <div className="px-4 pt-3">
                        <Alert
                            type={hierarchical ? "warning" : "info"}
                            showIcon
                            title={
                                hierarchical
                                    ? `「${typeName}」类别尚未启用，启用后才能新增与导入档案。`
                                    : `「${typeName}」尚未参与辅助核算：科目暂时不能挂该维度，档案仍可维护并在资金往来中使用。`
                            }
                            action={
                                <Button size="small" type="link" onClick={onCategoryEnabledChange}>
                                    启用类别
                                </Button>
                            }
                        />
                    </div>
                )}

                {/* 工具栏 */}
                <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 px-4 py-3">
                    <div className="flex items-center gap-2">
                        <Input
                            allowClear
                            style={{ width: 220 }}
                            prefix={<SearchOutlined style={{ color: "var(--icon-muted)" }} />}
                            placeholder="输入编码 / 名称 / 联系人"
                            value={keywordInput}
                            onChange={(e) => {
                                setKeywordInput(e.target.value)
                                if (!e.target.value) setKeyword("")
                            }}
                            onPressEnter={() => setKeyword(keywordInput)}
                        />
                        <Button type="primary" onClick={() => setKeyword(keywordInput)}>
                            查询
                        </Button>
                        {keyword && (
                            <Button
                                type="text"
                                onClick={() => {
                                    setKeywordInput("")
                                    setKeyword("")
                                }}
                            >
                                重置
                            </Button>
                        )}
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                        {hierarchical && items.length > 0 && (
                            <>
                                <Button
                                    type="text"
                                    className="text-(--muted-foreground)"
                                    onClick={() =>
                                        setExpandedKeys(
                                            items
                                                .filter((i) => i.parentId)
                                                .map((i) => i.parentId as string),
                                        )
                                    }
                                >
                                    展开全部
                                </Button>
                                <Button type="text" className="text-(--muted-foreground)" onClick={() => setExpandedKeys([])}>
                                    收起全部
                                </Button>
                            </>
                        )}
                        <Tooltip title={createBlocked ? `请先启用「${typeName}」类别` : ""}>
                            <Button icon={<UploadOutlined />} disabled={createBlocked} onClick={() => setImportOpen(true)}>
                                导入
                            </Button>
                        </Tooltip>
                        <Button icon={<DownloadOutlined />} disabled={filteredItems.length === 0} onClick={exportCsv}>
                            导出
                        </Button>
                        <Tooltip title={createBlocked ? `请先启用「${typeName}」类别` : ""}>
                            <Button
                                type="primary"
                                icon={<PlusOutlined />}
                                disabled={createBlocked}
                                onClick={() => void openCreate(null)}
                            >
                                新增
                            </Button>
                        </Tooltip>
                    </div>
                </div>

                {error && (
                    <div className="px-4 pb-3">
                        <Alert type="error" showIcon title={error} />
                    </div>
                )}

                <div className="px-3">
                    <Table
                        columns={columns}
                        dataSource={rows}
                        rowKey={(row) => String(row.node.id)}
                        rowClassName={(row) => (row.node.isEnabled === false ? "text-(--muted-foreground)" : "")}
                        pagination={false}
                        loading={loading}
                        scroll={{ x: 820 }}
                        locale={{ emptyText }}
                    />
                </div>

                <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-[12.5px] text-(--muted-foreground)">
                    <span>
                        {term ? (
                            <>
                                筛选出 <span className="font-medium tabular-nums text-(--foreground)">{rows.length}</span> 个
                                {rows.length !== filteredItems.length
                                    ? `（命中 ${filteredItems.length} 个，含上级路径）`
                                    : ""}{" "}
                                · 全部 <span className="font-medium tabular-nums text-(--foreground)">{items.length}</span> 个
                            </>
                        ) : (
                            <>
                                共 <span className="font-medium tabular-nums text-(--foreground)">{items.length}</span> 个档案
                            </>
                        )}{" "}
                        · 启用 {enabledCount} · 停用 {items.length - enabledCount}
                    </span>
                    <span className="text-(--muted-foreground)">
                        {hierarchical
                            ? `编码规则：每级 ${ASSIST_HIERARCHY_LEVEL_LENGTH} 位`
                            : `编码规则：${ASSIST_SEQUENCE_CODE_LENGTH} 位流水`}
                    </span>
                </div>
            </Card>

            {/* 新增 / 编辑档案 */}
            <Modal
                open={formOpen}
                width={464}
                title={
                    formMode === "edit"
                        ? `编辑${typeName}档案`
                        : parentItem
                          ? `新增下级${typeName}档案`
                          : `新增${typeName}档案`
                }
                okText="保存"
                confirmLoading={saving}
                onOk={() => void submitForm()}
                onCancel={closeForm}
            >
                <p className="mb-4 text-[12.5px] leading-5 text-(--muted-foreground)">
                    {formMode === "edit"
                        ? `编码与所属类别不可变更${editTarget?.code ? `，当前编码 ${editTarget.code}` : ""}。`
                        : parentItem
                          ? `第 ${createLevel} 级编码应为 ${expectedLength} 位，以上级编码「${parentItem.code}」为前缀。`
                          : `第 1 级编码应为 ${expectedLength} 位，同类别内唯一。`}
                </p>
                <div className="flex flex-col gap-4">
                    {formMode === "create" ? (
                        <FormField
                            label="档案编码"
                            htmlFor="assist-code"
                            required
                            description={codeLoading ? "正在生成建议编码…" : "建议编码已自动带出，可修改"}
                        >
                            <Input
                                id="assist-code"
                                value={form.code}
                                onChange={(e) => setForm((p) => ({ ...p, code: e.target.value }))}
                                placeholder={codeLoading ? "正在生成…" : "如 0001"}
                            />
                        </FormField>
                    ) : (
                        <FormField label="档案编码" htmlFor="assist-code-view">
                            <Input id="assist-code-view" value={form.code} disabled />
                        </FormField>
                    )}
                    <FormField label="档案名称" htmlFor="assist-name" required>
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
                            placeholder="联系人 / 电话 / 税号等，可留空"
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
                    {formFieldError && <p className="text-sm text-(--danger)">{formFieldError}</p>}
                    {formError && <Alert type="error" showIcon title={formError} />}
                </div>
            </Modal>

            {/* 导入 */}
            <Modal
                open={importOpen}
                width={520}
                title={`导入${typeName}档案`}
                okText="导入"
                cancelText="取消"
                confirmLoading={importing}
                okButtonProps={{ disabled: !importFile }}
                onOk={() => void submitImport()}
                onCancel={() => {
                    setImportOpen(false)
                    setImportFile(null)
                }}
            >
                <div className="flex flex-col gap-3 py-2 text-[13px] leading-6 text-(--foreground)">
                    <p>
                        1. 下载模板（CSV，可直接用 Excel 打开编辑），
                        <Typography.Link className="mx-1" onClick={downloadTemplate}>
                            下载模板
                        </Typography.Link>
                        。编码留空时按规则自动生成。
                    </p>
                    <p>2. 选择填写好的文件导入：</p>
                    <Upload
                        accept=".csv,text/csv"
                        maxCount={1}
                        fileList={importFile ? [importFile] : []}
                        beforeUpload={() => false}
                        onChange={({ fileList }) => setImportFile(fileList[0] ?? null)}
                        onRemove={() => setImportFile(null)}
                    >
                        <Button icon={<UploadOutlined />}>选择 CSV 文件</Button>
                    </Upload>
                    <ul className="pl-5 text-[12px] leading-6 text-(--muted-foreground)">
                        <li>表头需包含「编码」「名称」，可选「上级编码」「联系资料」「备注」。</li>
                        {hierarchical && <li>多级类别请先导入上级档案，或把上级行放在同一文件中。</li>}
                        <li>编码重复或不合规的行会被跳过，导入完成后会列出原因。</li>
                    </ul>
                </div>
            </Modal>

            {/* 导入结果 */}
            <Modal
                open={importResult !== null}
                title="导入结果"
                width={520}
                onCancel={() => setImportResult(null)}
                footer={
                    <Button type="primary" onClick={() => setImportResult(null)}>
                        知道了
                    </Button>
                }
            >
                {importResult && (
                    <div className="text-[13px] leading-6 text-(--foreground)">
                        <p>
                            共 {importResult.totalRows} 行，成功导入{" "}
                            <span className="font-medium text-(--success)">{importResult.imported}</span> 条，跳过{" "}
                            <span className="font-medium text-(--warn-icon)">{importResult.skipped}</span> 条。
                        </p>
                        {importResult.warnings.length > 0 && (
                            <ul className="mt-2 max-h-[240px] overflow-y-auto pl-5 text-[12.5px] leading-6 text-(--warn-text)">
                                {importResult.warnings.map((w, i) => (
                                    <li key={i}>{w}</li>
                                ))}
                            </ul>
                        )}
                    </div>
                )}
            </Modal>

            {confirmDialog}
        </>
    )
}

/** 节点或其任意后代是否命中查询。 */
function containsMatch(node: ItemNode, matched: Set<string>): boolean {
    if (matched.has(String(node.id))) return true
    return (node.children ?? []).some((child) => containsMatch(child, matched))
}

/** 本地建议编码：与后端 AssistCodingScheme 同一规则（层级型每级 2 位，平铺型 4 位流水）。 */
function localNextCode(type: number, parentCode: string | null, taken: Iterable<string>): string {
    const hierarchical = assistSupportsHierarchy(type)
    const prefix = hierarchical ? (parentCode ?? "") : ""
    const length = hierarchical ? ASSIST_HIERARCHY_LEVEL_LENGTH : ASSIST_SEQUENCE_CODE_LENGTH
    let max = 0
    for (const code of taken) {
        if (code.length !== prefix.length + length || !code.startsWith(prefix)) continue
        const n = Number(code.slice(prefix.length))
        if (Number.isInteger(n) && n > max) max = n
    }
    return prefix + String(max + 1).padStart(length, "0")
}

/**
 * Excel 另存 CSV 会把「0001」当成数字 1，导入前把纯数字编码补回规则位数：
 * 平铺型补到 4 位；层级型补到最小的偶数位（1 → 01，101 → 0101）。
 */
function normalizeNumericCode(code: string, hierarchical: boolean): string {
    if (!code || !/^\d+$/.test(code)) return code
    if (!hierarchical) return code.length < ASSIST_SEQUENCE_CODE_LENGTH ? code.padStart(ASSIST_SEQUENCE_CODE_LENGTH, "0") : code
    const length = Math.max(ASSIST_HIERARCHY_LEVEL_LENGTH, Math.ceil(code.length / ASSIST_HIERARCHY_LEVEL_LENGTH) * ASSIST_HIERARCHY_LEVEL_LENGTH)
    return code.padStart(length, "0")
}

/** 下载文本文件（加 BOM，Excel 打开中文不乱码）。 */
function downloadText(filename: string, content: string) {
    const blob = new Blob(["\ufeff" + content], { type: "text/csv;charset=utf-8" })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement("a")
    anchor.href = url
    anchor.download = filename
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(url)
}

/** 读取 CSV：优先 UTF-8，失败再按 GBK（Excel 另存为 CSV 的常见编码）。 */
async function readCsvFile(file: File): Promise<string> {
    const buffer = await file.arrayBuffer()
    try {
        return new TextDecoder("utf-8", { fatal: true }).decode(buffer)
    } catch {
        return new TextDecoder("gbk").decode(buffer)
    }
}

/** 最小 CSV 解析：支持双引号包裹、转义引号与 CRLF；空行忽略。 */
function parseCsv(text: string): string[][] {
    const rows: string[][] = []
    let row: string[] = []
    let field = ""
    let quoted = false

    for (let i = 0; i < text.length; i++) {
        const ch = text[i]
        if (quoted) {
            if (ch === '"') {
                if (text[i + 1] === '"') {
                    field += '"'
                    i++
                } else {
                    quoted = false
                }
            } else {
                field += ch
            }
            continue
        }
        if (ch === '"') quoted = true
        else if (ch === ",") {
            row.push(field)
            field = ""
        } else if (ch === "\n") {
            row.push(field)
            rows.push(row)
            row = []
            field = ""
        } else if (ch !== "\r") field += ch
    }
    if (field !== "" || row.length > 0) {
        row.push(field)
        rows.push(row)
    }
    return rows.filter((r) => r.some((c) => c.trim() !== ""))
}
