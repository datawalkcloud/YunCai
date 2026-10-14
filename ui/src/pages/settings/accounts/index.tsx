import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import {
    ChevronDown,
    ChevronRight,
    CircleAlert,
    Download,
    FileSpreadsheet,
    ListTree,
    Pencil,
    Plus,
    RefreshCw,
    Tags,
    Trash2,
    Upload,
} from "lucide-react"
import { createColumnHelper, tableFeatures, useTable } from "@tanstack/react-table"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { AccountsApi, AccountSetsApi, type AccountAssistTypeDto, type AccountDto, type AccountImportResult } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { useLocalForm } from "@/hooks/useLocalForm"
import { useConfirm } from "@/hooks/useConfirm"
import { ASSIST_TYPES, assistLabel } from "@/components/assist/constants"
import { PageHeader, SectionCard } from "@/components/page/section"
import { Tile } from "@/components/page/tile"
import { StatusPill } from "@/components/page/status-pill"
import { ICON_BTN, ICON_BTN_DANGER, PAGE_BTN_GHOST, PAGE_BTN_PRIMARY, SOFT_PANEL } from "@/components/page/styles"
import {
    fetchAccountAssistTypes,
    setAccountAssistTypesCache,
    useAccountAssistConfigs,
    useAssistCategories,
} from "@/components/assist/assist-store"
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
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import {
    Sheet,
    SheetContent,
    SheetDescription,
    SheetFooter,
    SheetHeader,
    SheetTitle,
} from "@/components/ui/sheet"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group"

/** 会计科目类别（数值对应后端枚举 AccountCategory）。 */
const CATEGORY_OPTIONS = [
    { value: 1, label: "资产" },
    { value: 2, label: "负债" },
    { value: 3, label: "权益" },
    { value: 4, label: "成本" },
    { value: 5, label: "损益" },
] as const

/** 余额方向（数值对应后端枚举 BalanceDirection）。 */
const DIRECTION_OPTIONS = [
    { value: 1, label: "借" },
    { value: 2, label: "贷" },
] as const

const CATEGORY_LABELS: Record<number, string> = Object.fromEntries(
    CATEGORY_OPTIONS.map((c) => [c.value, c.label]),
)
const DIRECTION_LABELS: Record<number, string> = Object.fromEntries(
    DIRECTION_OPTIONS.map((d) => [d.value, d.label]),
)

/** 科目导入支持的文件格式。 */
const ACCEPT = ".xls,.xlsx"

/** Radix Select 不允许空串作为选项值，用这个哨兵表示「不选上级（顶级科目）」，等价于原来的 allowClear。 */
const TOP_PARENT = "__top__"

/** 列表列数，用于空态与加载态的跨列单元格。 */
const COLUMN_COUNT = 7

/** 各列对齐方式按列 id 取用，避免写行内样式。 */
const COLUMN_CLASS: Record<string, string> = {
    level: "text-center",
    isEnabled: "text-center",
    actions: "text-right",
}

/** 树形层级缩进：按可见深度取 pl-* 结构类，最多 10 级（编码方案上限）。 */
const INDENT_CLASS = [
    "",
    "pl-4",
    "pl-8",
    "pl-12",
    "pl-16",
    "pl-20",
    "pl-24",
    "pl-28",
    "pl-32",
    "pl-36",
    "pl-40",
]

const indentClass = (depth: number) => INDENT_CLASS[Math.min(depth, INDENT_CLASS.length - 1)]

/** 树节点：在 AccountDto 基础上挂 children，供树形展开使用。 */
interface AccountNode extends AccountDto {
    children?: AccountNode[]
}

/** 展开后用于渲染的一行：节点 + 在树中的深度。 */
interface VisibleRow {
    node: AccountNode
    depth: number
}

/** 新增表单字段。 */
interface CreateForm {
    parentId: string | null
    code: string
    name: string
    category: number
    direction: number
    mnemonic: string
    isEnabled: boolean
}

/** 编辑表单字段。 */
interface EditForm {
    name: string
    direction: number
    mnemonic: string
    isEnabled: boolean
}

/** 辅助核算设置弹窗的一行：七个固定类别各一行，勾选即挂到该科目上。 */
interface AssistRow {
    type: number
    checked: boolean
    isRequired: boolean
}

/** 把科目已有的维度配置映射为七个固定类别的展示行（未配置的默认必录）。 */
function toAssistRows(configured: AccountAssistTypeDto[]): AssistRow[] {
    const byType = new Map(configured.map((c) => [c.type as number, c]))
    return ASSIST_TYPES.map((t) => ({
        type: t.value,
        checked: byType.has(t.value),
        isRequired: byType.get(t.value)?.isRequired ?? true,
    }))
}

const emptyCreateForm: CreateForm = {
    parentId: null,
    code: "",
    name: "",
    category: 1,
    direction: 1,
    mnemonic: "",
    isEnabled: true,
}

const emptyEditForm: EditForm = { name: "", direction: 1, mnemonic: "", isEnabled: true }

/** 按上级科目推导级次、期望编码长度与本级位数（原 Form.useWatch 联动改为直接读受控 state）。 */
function resolveLevel(accounts: AccountDto[], schemeSegments: number[], parentId: string | null) {
    const parent = parentId == null ? null : accounts.find((a) => a.id === parentId) ?? null
    const newLevel = parent ? (parent.level ?? 0) + 1 : 1
    const expectedCodeLength = parent
        ? schemeSegments.slice(0, newLevel).reduce((s, d) => s + d, 0)
        : schemeSegments[0] ?? 4
    const levelDigits = schemeSegments[newLevel - 1] ?? 2
    return { parent, newLevel, expectedCodeLength, levelDigits }
}

const features = tableFeatures({})
const helper = createColumnHelper<typeof features, VisibleRow>()

/** 行以科目 id 为键，等价原先的 rowKey="id"。 */
const getRowId = (row: VisibleRow) => row.node.id ?? ""

export default function AccountsPage() {
    const { token } = useAuth()
    const { accountSets, refresh: refreshAccountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { confirm, confirmDialog } = useConfirm()

    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [loading, setLoading] = useState(false)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [filter, setFilter] = useState<number | "全部">("全部")
    const [expandedKeys, setExpandedKeys] = useState<React.Key[]>([])
    const [deletingId, setDeletingId] = useState<string | null>(null)

    const [error, setError] = useState("")
    const [saving, setSaving] = useState(false)
    const [open, setOpen] = useState(false)

    // ── 科目导入相关状态 ──
    const importInputRef = useRef<HTMLInputElement>(null)
    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<File | null>(null)
    const [importDragging, setImportDragging] = useState(false)
    const [importing, setImporting] = useState(false)
    const [importResult, setImportResult] = useState<AccountImportResult | null>(null)
    const [importError, setImportError] = useState<string | null>(null)

    // ── 科目编辑相关状态 ──
    const [editOpen, setEditOpen] = useState(false)
    const [editTarget, setEditTarget] = useState<AccountDto | null>(null)
    const [editSaving, setEditSaving] = useState(false)
    const [editError, setEditError] = useState("")

    // ── 编码方案弹窗 ──
    const [schemeModalOpen, setSchemeModalOpen] = useState(false)
    const [schemeLevels, setSchemeLevels] = useState(4)
    const [schemeDigits, setSchemeDigits] = useState<number[]>([4, 2, 2, 2])
    const [schemeSaving, setSchemeSaving] = useState(false)
    const [schemeError, setSchemeError] = useState("")
    // 原 Modal.confirm（已有科目时二次确认）改为受控 Dialog。
    const [schemeConfirmOpen, setSchemeConfirmOpen] = useState(false)
    const [pendingScheme, setPendingScheme] = useState("")

    // ── 辅助核算设置（类别开关在账套上，维度挂在科目上）──
    const { categories: assistCategories } = useAssistCategories(accountSetId, token)
    // 账套一个类别都没启用时不必逐科目回读维度，避免无谓请求。
    const assistAnyEnabled = assistCategories.some((c) => c.isEnabled)
    const assistAccountIds = useMemo(
        () => (assistAnyEnabled ? accounts.map((a) => a.id) : []),
        [assistAnyEnabled, accounts],
    )
    const { configs: assistConfigs, applyConfig: applyAssistConfig } = useAccountAssistConfigs(
        accountSetId,
        assistAccountIds,
        token,
    )
    const [assistOpen, setAssistOpen] = useState(false)
    const [assistTarget, setAssistTarget] = useState<AccountDto | null>(null)
    const [assistRows, setAssistRows] = useState<AssistRow[]>([])
    const [assistLoading, setAssistLoading] = useState(false)
    const [assistSaving, setAssistSaving] = useState(false)
    const [assistError, setAssistError] = useState("")

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setLoadError(null)
        try {
            const list = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({ accountSetId })
            setAccounts(list)
            // 默认展开所有含下级的科目，便于一览多级结构。
            setExpandedKeys(list.filter((a) => a.parentId != null).map((a) => a.parentId as string))
        } catch (err) {
            setLoadError(await apiErrorMessage(err, "加载科目失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void load()
    }, [load])

    // 根据编码方案和上级科目，计算新增科目的期望编码长度。
    const codingScheme = useMemo(
        () => ((currentSet as any)?.accountCodingScheme ?? "4-2-2-2") as string,
        [currentSet],
    )
    const schemeSegments = useMemo(() => codingScheme.split("-").map(Number), [codingScheme])

    // 新增表单：校验依赖编码方案与上级科目，故在校验函数内按当前值重新推导。
    const createForm = useLocalForm<CreateForm>(emptyCreateForm, (values) => {
        const errs: Partial<Record<keyof CreateForm, string>> = {}
        const { newLevel, expectedCodeLength } = resolveLevel(accounts, schemeSegments, values.parentId)
        // required：与原 antd 一致，仅拦截空值（不 trim）；类别与余额方向为必选控件，state 里必有值。
        if (!values.code) errs.code = "请填写科目编码"
        else if (values.code.length !== expectedCodeLength) {
            errs.code = `第 ${newLevel} 级科目编码固定为 ${expectedCodeLength} 位（方案：${codingScheme}）`
        }
        if (!values.name) errs.name = "请填写科目名称"
        return errs
    })

    const editForm = useLocalForm<EditForm>(emptyEditForm, (values) => {
        const errs: Partial<Record<keyof EditForm, string>> = {}
        // required：与原 antd 一致，仅拦截空值（不 trim）；余额方向为单选，state 里必有值。
        if (!values.name) errs.name = "请填写科目名称"
        return errs
    })

    const { parent, newLevel, expectedCodeLength, levelDigits } = useMemo(
        () => resolveLevel(accounts, schemeSegments, createForm.values.parentId),
        [accounts, schemeSegments, createForm.values.parentId],
    )

    // 按父子关系构建树（顶级按类别筛选，明细挂在父级 children 下）。
    const treeData = useMemo<AccountNode[]>(() => {
        const byCode = (a: AccountDto, b: AccountDto) => (a.code ?? "").localeCompare(b.code ?? "")
        const build = (pid: string | null): AccountNode[] => {
            const children = accounts.filter((a) => (a.parentId ?? null) === pid).sort(byCode)
            return children
                .filter((acc) => !(pid === null && filter !== "全部" && acc.category !== filter))
                .map((acc) => {
                    const sub = build(acc.id ?? null)
                    return { ...acc, children: sub.length > 0 ? sub : undefined }
                })
        }
        return build(null)
    }, [accounts, filter])

    // 受控展开：由 treeData + expandedKeys 算出要渲染的扁平可见行序列（含深度）。
    const visibleRows = useMemo<VisibleRow[]>(() => {
        const expanded = new Set(expandedKeys.map((k) => String(k)))
        const out: VisibleRow[] = []
        const walk = (nodes: AccountNode[], depth: number) => {
            for (const node of nodes) {
                out.push({ node, depth })
                if (node.children && expanded.has(String(node.id))) walk(node.children, depth + 1)
            }
        }
        walk(treeData, 0)
        return out
    }, [treeData, expandedKeys])

    function toggleExpand(id: string) {
        setExpandedKeys((prev) => (prev.includes(id) ? prev.filter((k) => k !== id) : [...prev, id]))
    }

    // 上级科目下拉可选项：仅显示当前选中类别下的顶级科目（按编码排序）；
    // 已达编码方案最大级次的科目不可再挂下级。
    const parentOptions = useMemo(() => {
        const scheme = (currentSet as any)?.accountCodingScheme ?? "4-2-2-2"
        const maxLevel = scheme.split("-").length
        return [...accounts]
            .filter((a) => a.parentId == null && a.category === createForm.values.category && (a.level ?? 0) < maxLevel)
            .sort((a, b) => (a.code ?? "").localeCompare(b.code ?? ""))
            .map((a) => ({ value: a.id as string, label: `${a.code} ${a.name}（${a.level} 级）` }))
    }, [accounts, createForm.values.category, currentSet])

    function openDrawer() {
        createForm.fill({ parentId: null, code: "", name: "", category: 1, direction: 1, mnemonic: "", isEnabled: true })
        setError("")
        setOpen(true)
    }

    // ── 编码方案弹窗 ──
    function openSchemeModal() {
        const raw = (currentSet as any)?.accountCodingScheme ?? "4-2-2-2"
        const parts = raw.split("-").map(Number)
        setSchemeLevels(parts.length)
        setSchemeDigits(parts)
        setSchemeError("")
        setSchemeModalOpen(true)
    }

    /** 生成编码示例：一级固定 1001，其余用 "1" 补零。如 4-2-2-2 → "1001 01 01 01" */
    const schemePreview = useMemo(() => {
        return schemeDigits.slice(0, schemeLevels).map((d, i) => {
            if (i === 0) return "1001"
            return "1".padStart(d, "0")
        }).join(" ")
    }, [schemeLevels, schemeDigits])

    /** 各层级累计总位数。如 4-2-2-2 → [4, 6, 8, 10] */
    const schemeTotalDigits = useMemo(() => {
        let sum = 0
        return schemeDigits.slice(0, schemeLevels).map((d) => { sum += d; return sum })
    }, [schemeLevels, schemeDigits])

    function handleSchemeLevelsChange(levels: number) {
        setSchemeLevels(levels)
        setSchemeDigits((prev) => {
            if (levels > prev.length) {
                // 增加级次，新级默认 2 位
                return [...prev, ...Array(levels - prev.length).fill(2)]
            }
            return prev.slice(0, levels)
        })
    }

    function handleSchemeDigitChange(index: number, digit: number) {
        setSchemeDigits((prev) => {
            const next = [...prev]
            next[index] = digit
            return next
        })
    }

    function saveScheme() {
        if (schemeSaving || !accountSetId || !currentSet) return
        const scheme = schemeDigits.slice(0, schemeLevels).join("-")

        // 若当前有科目，提示用户编码将按新方案重建。
        if (accounts.length > 0) {
            setPendingScheme(scheme)
            setSchemeConfirmOpen(true)
            return
        }

        void doSaveScheme(scheme)
    }

    async function doSaveScheme(scheme: string) {
        if (!currentSet) return
        setSchemeSaving(true)
        setSchemeError("")
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdPut({
                id: accountSetId,
                updateAccountSetRequest: {
                    name: currentSet.name ?? "",
                    companyName: (currentSet as any).companyName ?? "",
                    standard: (currentSet as any).standard ?? 1,
                    taxpayerType: (currentSet as any).taxpayerType ?? 1,
                    baseCurrency: currentSet.baseCurrency ?? "人民币",
                    accountCodingScheme: scheme,
                },
            })
            setSchemeModalOpen(false)
            refreshAccountSets()
            await load()
        } catch (err) {
            setSchemeError(await apiErrorMessage(err, "保存编码方案失败，请重试。"))
        } finally {
            setSchemeSaving(false)
        }
    }

    async function addAccount() {
        if (saving) return
        if (!createForm.check()) return
        const values = createForm.values
        const trimmedCode = values.code.trim()
        if (accounts.some((a) => a.code === trimmedCode)) {
            setError("科目编码已存在。")
            return
        }
        setSaving(true)
        setError("")
        try {
            await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsPost({
                accountSetId,
                // 明细科目的类别与方向由后端继承上级，这里仍按顶级取值传入（后端忽略）。
                createAccountRequest: {
                    parentId: values.parentId,
                    code: trimmedCode,
                    name: values.name.trim(),
                    category: values.category,
                    direction: values.direction,
                    mnemonic: values.mnemonic.trim() || undefined,
                    isEnabled: values.isEnabled,
                },
            })
            if (values.parentId != null) setExpandedKeys((prev) => [...new Set([...prev, values.parentId as string])])
            setOpen(false)
            await load()
        } catch (err) {
            setError(await apiErrorMessage(err, "新增科目失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function removeAccount(account: AccountDto) {
        if (deletingId || !account.id) return
        if (!(await confirm(`确定删除科目「${account.code} ${account.name}」吗？此操作不可撤销。`))) return
        setDeletingId(account.id)
        setLoadError(null)
        try {
            await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsIdDelete({
                accountSetId,
                id: account.id,
            })
            await load()
        } catch (err) {
            setLoadError(await apiErrorMessage(err, "删除科目失败，请重试。"))
        } finally {
            setDeletingId(null)
        }
    }

    // ── 科目导入处理 ──
    function pickImportFile(next: File | null) {
        setImportFile(next)
        setImportResult(null)
        setImportError(null)
    }

    function onImportDrop(e: React.DragEvent<HTMLDivElement>) {
        e.preventDefault()
        setImportDragging(false)
        const dropped = e.dataTransfer.files?.[0]
        if (dropped) pickImportFile(dropped)
    }

    async function handleImport() {
        if (!importFile || importing) return
        if (!accountSetId) {
            setImportError("请先选择账套。")
            return
        }
        setImporting(true)
        setImportError(null)
        setImportResult(null)
        try {
            const res = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsImportPost({
                accountSetId,
                file: importFile,
            })
            setImportResult(res)
            void load()
        } catch (err) {
            setImportError(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    async function downloadTemplate() {
        if (!accountSetId) return
        try {
            const response = await fetch(
                `/api/accountsets/${accountSetId}/Accounts/import/template`,
                { headers: { Authorization: `Bearer ${token}` } },
            )
            if (!response.ok) throw new Error("下载失败")
            const blob = await response.blob()
            const url = URL.createObjectURL(blob)
            const a = document.createElement("a")
            a.href = url
            a.download = "科目导入模板.xlsx"
            a.click()
            URL.revokeObjectURL(url)
        } catch (err) {
            console.error("下载模板失败:", err)
        }
    }

    // ── 科目编辑处理 ──
    function openEditDrawer(account: AccountDto) {
        setEditTarget(account)
        editForm.fill({
            name: account.name ?? "",
            direction: account.direction ?? 1,
            mnemonic: account.mnemonic ?? "",
            isEnabled: account.isEnabled ?? true,
        })
        setEditError("")
        setEditOpen(true)
    }

    async function saveEdit() {
        if (editSaving || !editTarget) return
        if (!editForm.check()) return
        const values = editForm.values
        setEditSaving(true)
        setEditError("")
        try {
            const response = await fetch(
                `/api/accountsets/${accountSetId}/Accounts/${editTarget.id}`,
                {
                    method: "PUT",
                    headers: {
                        "Content-Type": "application/json",
                        Authorization: `Bearer ${token}`,
                    },
                    body: JSON.stringify({
                        name: values.name.trim(),
                        direction: values.direction,
                        mnemonic: values.mnemonic.trim() || null,
                        isEnabled: values.isEnabled,
                    }),
                },
            )
            if (!response.ok) {
                const body = await response.json().catch(() => null)
                throw new Error(body?.message ?? `更新失败（${response.status}）`)
            }
            setEditOpen(false)
            setEditTarget(null)
            void load()
        } catch (err) {
            setEditError(err instanceof Error ? err.message : "更新科目失败，请重试。")
        } finally {
            setEditSaving(false)
        }
    }

    // ── 辅助核算设置处理 ──
    async function openAssistDialog(account: AccountDto) {
        if (!account.id) return
        setAssistTarget(account)
        setAssistRows([])
        setAssistError("")
        setAssistOpen(true)
        setAssistLoading(true)
        try {
            // force 回读，确保弹窗里显示的是后端最新的必录口径。
            setAssistRows(toAssistRows(await fetchAccountAssistTypes(accountSetId, account.id, token, true)))
        } catch (err) {
            setAssistError(await apiErrorMessage(err, "加载辅助核算设置失败，请重试。"))
        } finally {
            setAssistLoading(false)
        }
    }

    async function saveAssistDialog() {
        if (assistSaving || !assistTarget?.id) return
        setAssistSaving(true)
        setAssistError("")
        try {
            const list = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsIdAssistTypesPut({
                accountSetId,
                id: assistTarget.id,
                accountAssistTypeRequest: assistRows
                    .filter((r) => r.checked)
                    .map((r) => ({ type: r.type, isRequired: r.isRequired })),
            })
            setAccountAssistTypesCache(accountSetId, assistTarget.id, list)
            applyAssistConfig(assistTarget.id, list)
            setAssistOpen(false)
            setAssistTarget(null)
        } catch (err) {
            setAssistError(await apiErrorMessage(err, "保存辅助核算设置失败，请重试。"))
        } finally {
            setAssistSaving(false)
        }
    }

    const expandedSet = useMemo(() => new Set(expandedKeys.map((k) => String(k))), [expandedKeys])

    const columns = [
        helper.display({
            id: "code",
            header: "科目编码",
            cell: ({ row }) => {
                const { node, depth } = row.original
                const id = String(node.id)
                const expanded = expandedSet.has(id)
                return (
                    <div className={cn("flex items-center gap-1", indentClass(depth))}>
                        {node.children ? (
                            <Button
                                variant="ghost"
                                size="icon-xs"
                                className={ICON_BTN}
                                aria-label={expanded ? "收起下级科目" : "展开下级科目"}
                                aria-expanded={expanded}
                                onClick={() => toggleExpand(id)}
                            >
                                {expanded ? <ChevronDown /> : <ChevronRight />}
                            </Button>
                        ) : (
                            // 占位，保持与可展开行同宽，编码列纵向对齐。
                            <span className="size-6 shrink-0" />
                        )}
                        <span className="whitespace-nowrap">{node.code}</span>
                    </div>
                )
            },
        }),
        helper.display({
            id: "name",
            header: "科目名称",
            cell: ({ row }) => {
                const node = row.original.node
                // 已挂辅助核算的科目给一个轻量标记（星号表示该维度必录）。
                const types = assistConfigs[node.id ?? ""] ?? []
                return (
                    <div className="flex flex-wrap items-center gap-1">
                        {node.parentId == null ? (
                            <span className="font-medium text-slate-800">{node.name}</span>
                        ) : (
                            <span>{node.name}</span>
                        )}
                        {types.length > 0 && (
                            <StatusPill tone="gray">
                                {types.map((t) => `${assistLabel(t.type)}${t.isRequired === false ? "" : "*"}`).join(" ")}
                            </StatusPill>
                        )}
                    </div>
                )
            },
        }),
        helper.display({
            id: "level",
            header: "级次",
            cell: ({ row }) => <span>{row.original.node.level ?? "—"}</span>,
        }),
        helper.display({
            id: "category",
            header: "类别",
            cell: ({ row }) => <span>{CATEGORY_LABELS[row.original.node.category ?? 0] ?? "—"}</span>,
        }),
        helper.display({
            id: "direction",
            header: "余额方向",
            cell: ({ row }) => <span>{DIRECTION_LABELS[row.original.node.direction ?? 0] ?? "—"}</span>,
        }),
        helper.display({
            id: "isEnabled",
            header: "状态",
            cell: ({ row }) => (
                row.original.node.isEnabled
                    ? <StatusPill tone="green">启用</StatusPill>
                    : <StatusPill tone="gray">停用</StatusPill>
            ),
        }),
        helper.display({
            id: "actions",
            header: "",
            cell: ({ row }) => {
                const account = row.original.node
                return (
                    <div className="flex items-center justify-end gap-0">
                        <Button
                            variant="ghost"
                            size="icon-xs"
                            className={ICON_BTN}
                            title="设置辅助核算"
                            onClick={() => void openAssistDialog(account)}
                        >
                            <Tags />
                        </Button>
                        <Button variant="ghost" size="icon-xs" className={ICON_BTN} title="编辑科目" onClick={() => openEditDrawer(account)}>
                            <Pencil />
                        </Button>
                        <Button
                            variant="ghost"
                            size="icon-xs"
                            className={ICON_BTN_DANGER}
                            title={account.children ? "删除科目（需先删除下级）" : "删除科目"}
                            disabled={deletingId === account.id}
                            onClick={() => void removeAccount(account)}
                        >
                            {deletingId === account.id
                                ? <Spinner data-icon="inline-start" />
                                : <Trash2 data-icon="inline-start" />}
                        </Button>
                    </div>
                )
            },
        }),
    ]

    const table = useTable({ features, columns, data: visibleRows, getRowId })
    const headerGroups = table.getHeaderGroups()
    const rows = table.getRowModel().rows

    const filterOptions: { value: string; label: string }[] = [
        { value: "全部", label: "全部" },
        ...CATEGORY_OPTIONS.map((c) => ({ value: String(c.value), label: c.label })),
    ]

    return (
        <div className="flex flex-col gap-5">
            <PageHeader
                icon={ListTree}
                title="科目"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        {((currentSet as any)?.accountCodingScheme) ? ` · 编码方案：${(currentSet as any).accountCodingScheme}` : ""}
                        。维护多级会计科目表，一级科目下可挂明细科目，用于凭证录入与账簿核算。
                    </>
                }
                actions={
                    <>
                        <Button variant="outline" className={PAGE_BTN_GHOST} onClick={() => void load()} disabled={!accountSetId || loading}>
                            {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                            刷新
                        </Button>
                        <Button variant="outline" className={PAGE_BTN_GHOST} onClick={() => setImportOpen(true)} disabled={!accountSetId}>
                            <Upload data-icon="inline-start" />
                            导入科目
                        </Button>
                        <Button variant="outline" className={PAGE_BTN_GHOST} onClick={openSchemeModal} disabled={!accountSetId}>
                            <Pencil data-icon="inline-start" />
                            编码方案
                        </Button>
                        <Button className={PAGE_BTN_PRIMARY} onClick={openDrawer} disabled={!accountSetId}>
                            <Plus data-icon="inline-start" />
                            新增科目
                        </Button>
                    </>
                }
            />

            <SectionCard
                title={<h3 className="text-[14px] font-semibold text-slate-800">科目列表</h3>}
                bodyClassName="flex flex-col gap-4"
            >
                {loadError && (
                    <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                        <CircleAlert />
                        <AlertTitle>{loadError}</AlertTitle>
                    </Alert>
                )}

                <div className={cn(SOFT_PANEL, "flex flex-wrap items-center gap-2 px-3 py-2")}>
                    <ToggleGroup
                        type="single"
                        variant="outline"
                        size="sm"
                        spacing={0}
                        value={filter === "全部" ? "全部" : String(filter)}
                        onValueChange={(v) => {
                            if (!v) return
                            setFilter(v === "全部" ? "全部" : Number(v))
                        }}
                        className="flex-wrap"
                    >
                        {filterOptions.map((o) => (
                            <ToggleGroupItem key={o.value} value={o.value}>
                                {o.label}
                            </ToggleGroupItem>
                        ))}
                    </ToggleGroup>
                </div>

                <Table>
                    <TableHeader>
                        {headerGroups.map((headerGroup) => (
                            <TableRow key={headerGroup.id}>
                                {headerGroup.headers.map((header) => (
                                    <TableHead key={header.id} className={COLUMN_CLASS[header.column.id]}>
                                        <table.FlexRender header={header} />
                                    </TableHead>
                                ))}
                            </TableRow>
                        ))}
                    </TableHeader>
                    <TableBody>
                        {loading && accounts.length === 0 ? (
                            <TableRow>
                                <TableCell colSpan={COLUMN_COUNT} className="py-8 text-center">
                                    <Spinner className="mx-auto size-5 text-slate-300" />
                                </TableCell>
                            </TableRow>
                        ) : rows.length === 0 ? (
                            <TableRow>
                                <TableCell
                                    colSpan={COLUMN_COUNT}
                                    className="py-8 text-center text-slate-400"
                                >
                                    {accountSetId
                                        ? "暂无科目，可点击「新增科目」或通过凭证导入自动建立。"
                                        : "请先选择账套。"}
                                </TableCell>
                            </TableRow>
                        ) : (
                            rows.map((row) => (
                                <TableRow key={row.id}>
                                    {row.getAllCells().map((cell) => (
                                        <TableCell key={cell.id} className={COLUMN_CLASS[cell.column.id]}>
                                            <table.FlexRender cell={cell} />
                                        </TableCell>
                                    ))}
                                </TableRow>
                            ))
                        )}
                    </TableBody>
                </Table>
            </SectionCard>

            {/* 新增科目抽屉 */}
            <Sheet open={open} onOpenChange={setOpen}>
                <SheetContent className="sm:max-w-lg">
                    <SheetHeader>
                        <SheetTitle>新增科目</SheetTitle>
                        <SheetDescription>
                            先选择类别，再选择上级科目（可选）。选中上级后将创建明细科目，方向自动继承。
                        </SheetDescription>
                    </SheetHeader>
                    <div className="flex flex-1 flex-col gap-4 overflow-auto px-4">
                        <Field data-invalid={Boolean(createForm.errors.category)}>
                            <FieldLabel htmlFor="create-category">类别</FieldLabel>
                            <Select
                                value={String(createForm.values.category)}
                                onValueChange={(v) => {
                                    // 切换类别时清空上级科目，避免跨类别挂载。
                                    createForm.patch({ category: Number(v), parentId: null })
                                }}
                            >
                                <SelectTrigger id="create-category">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    {CATEGORY_OPTIONS.map((c) => (
                                        <SelectItem key={c.value} value={String(c.value)}>{c.label}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                            <FieldError>{createForm.errors.category}</FieldError>
                        </Field>

                        <Field>
                            <FieldLabel htmlFor="create-parent">上级科目</FieldLabel>
                            <Select
                                value={createForm.values.parentId ?? TOP_PARENT}
                                onValueChange={(v) => {
                                    if (v === TOP_PARENT) {
                                        createForm.patch({ parentId: null })
                                        return
                                    }
                                    const p = accounts.find((a) => a.id === v)
                                    if (p) {
                                        // 明细科目方向继承上级。
                                        createForm.patch({ parentId: v, direction: p.direction })
                                    } else {
                                        createForm.patch({ parentId: v })
                                    }
                                }}
                            >
                                <SelectTrigger id="create-parent">
                                    <SelectValue placeholder="（顶级科目）" />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value={TOP_PARENT}>（顶级科目）</SelectItem>
                                    {parentOptions.map((o) => (
                                        <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </Field>

                        <Field data-invalid={Boolean(createForm.errors.code)}>
                            <FieldLabel htmlFor="create-code">科目编码</FieldLabel>
                            <Input
                                id="create-code"
                                value={createForm.values.code}
                                onChange={(e) => createForm.patch({ code: e.target.value })}
                                placeholder={
                                    parent
                                        ? `${parent.code}${"0".repeat(levelDigits)}（${expectedCodeLength} 位）`
                                        : `${"0".repeat(levelDigits)}（${expectedCodeLength} 位）`
                                }
                                aria-invalid={Boolean(createForm.errors.code)}
                            />
                            <FieldError>{createForm.errors.code}</FieldError>
                        </Field>

                        <Field data-invalid={Boolean(createForm.errors.name)}>
                            <FieldLabel htmlFor="create-name">科目名称</FieldLabel>
                            <Input
                                id="create-name"
                                value={createForm.values.name}
                                onChange={(e) => createForm.patch({ name: e.target.value })}
                                placeholder="如 库存现金"
                                aria-invalid={Boolean(createForm.errors.name)}
                            />
                            <FieldError>{createForm.errors.name}</FieldError>
                        </Field>

                        <Field data-invalid={Boolean(createForm.errors.direction)}>
                            <FieldLabel htmlFor="create-direction">余额方向</FieldLabel>
                            <RadioGroup
                                id="create-direction"
                                className="flex flex-row items-center gap-4"
                                disabled={parent !== null}
                                value={String(createForm.values.direction)}
                                onValueChange={(v) => createForm.patch({ direction: Number(v) })}
                            >
                                {DIRECTION_OPTIONS.map((d) => (
                                    <div key={d.value} className="flex items-center gap-2">
                                        <RadioGroupItem
                                            value={String(d.value)}
                                            id={`create-direction-${d.value}`}
                                        />
                                        <FieldLabel htmlFor={`create-direction-${d.value}`}>{d.label}</FieldLabel>
                                    </div>
                                ))}
                            </RadioGroup>
                            <FieldError>{createForm.errors.direction}</FieldError>
                        </Field>

                        <Field>
                            <FieldLabel htmlFor="create-mnemonic">助记码</FieldLabel>
                            <Input
                                id="create-mnemonic"
                                value={createForm.values.mnemonic}
                                onChange={(e) => createForm.patch({ mnemonic: e.target.value })}
                                placeholder="拼音首字母等快速检索用，可留空"
                            />
                        </Field>

                        <Field>
                            <FieldLabel htmlFor="create-enabled">状态</FieldLabel>
                            <RadioGroup
                                id="create-enabled"
                                className="flex flex-row items-center gap-4"
                                value={createForm.values.isEnabled ? "true" : "false"}
                                onValueChange={(v) => createForm.patch({ isEnabled: v === "true" })}
                            >
                                <div className="flex items-center gap-2">
                                    <RadioGroupItem value="true" id="create-enabled-true" />
                                    <FieldLabel htmlFor="create-enabled-true">启用</FieldLabel>
                                </div>
                                <div className="flex items-center gap-2">
                                    <RadioGroupItem value="false" id="create-enabled-false" />
                                    <FieldLabel htmlFor="create-enabled-false">停用</FieldLabel>
                                </div>
                            </RadioGroup>
                        </Field>

                        {parent && (
                            <FieldDescription>
                                将作为「{parent.code} {parent.name}」的第 {newLevel} 级明细科目（本级 {levelDigits} 位，编码方案：
                                {codingScheme}），余额方向已继承上级。
                            </FieldDescription>
                        )}
                        {!parent && (
                            <FieldDescription>
                                一级科目编码固定 {expectedCodeLength} 位（编码方案：{codingScheme}）。
                            </FieldDescription>
                        )}

                        {error && (
                            <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{error}</AlertTitle>
                            </Alert>
                        )}
                    </div>
                    <SheetFooter className="flex-row justify-end">
                        <Button variant="outline" onClick={() => setOpen(false)}>取消</Button>
                        <Button onClick={() => void addAccount()} disabled={saving}>
                            {saving ? <Spinner data-icon="inline-start" /> : <Plus data-icon="inline-start" />}
                            添加科目
                        </Button>
                    </SheetFooter>
                </SheetContent>
            </Sheet>

            {/* 编辑科目抽屉 */}
            <Sheet
                open={editOpen}
                onOpenChange={(next) => {
                    setEditOpen(next)
                    if (!next) setEditTarget(null)
                }}
            >
                <SheetContent aria-describedby={undefined} className="sm:max-w-lg">
                    <SheetHeader>
                        <SheetTitle>{`编辑科目 — ${editTarget?.code ?? ""}`}</SheetTitle>
                    </SheetHeader>
                    {editTarget && (
                        <>
                            <div className="flex flex-1 flex-col gap-4 overflow-auto px-4">
                                <Field>
                                    <FieldLabel>科目编码</FieldLabel>
                                    <span>{editTarget.code}</span>
                                </Field>
                                <Field>
                                    <FieldLabel>类别</FieldLabel>
                                    <span>{CATEGORY_LABELS[editTarget.category!] ?? "—"}</span>
                                </Field>
                                <Field data-invalid={Boolean(editForm.errors.name)}>
                                    <FieldLabel htmlFor="edit-name">科目名称</FieldLabel>
                                    <Input
                                        id="edit-name"
                                        value={editForm.values.name}
                                        onChange={(e) => editForm.patch({ name: e.target.value })}
                                        placeholder="如 库存现金"
                                        aria-invalid={Boolean(editForm.errors.name)}
                                    />
                                    <FieldError>{editForm.errors.name}</FieldError>
                                </Field>
                                <Field data-invalid={Boolean(editForm.errors.direction)}>
                                    <FieldLabel htmlFor="edit-direction">余额方向</FieldLabel>
                                    <RadioGroup
                                        id="edit-direction"
                                        className="flex flex-row items-center gap-4"
                                        disabled={!editTarget.isLeaf}
                                        value={String(editForm.values.direction)}
                                        onValueChange={(v) => editForm.patch({ direction: Number(v) })}
                                    >
                                        {DIRECTION_OPTIONS.map((d) => (
                                            <div key={d.value} className="flex items-center gap-2">
                                                <RadioGroupItem
                                                    value={String(d.value)}
                                                    id={`edit-direction-${d.value}`}
                                                />
                                                <FieldLabel htmlFor={`edit-direction-${d.value}`}>{d.label}</FieldLabel>
                                            </div>
                                        ))}
                                    </RadioGroup>
                                    {!editTarget.isLeaf && (
                                        <FieldDescription>
                                            非末级科目不允许修改余额方向。
                                        </FieldDescription>
                                    )}
                                    <FieldError>{editForm.errors.direction}</FieldError>
                                </Field>
                                <Field>
                                    <FieldLabel htmlFor="edit-mnemonic">助记码</FieldLabel>
                                    <Input
                                        id="edit-mnemonic"
                                        value={editForm.values.mnemonic}
                                        onChange={(e) => editForm.patch({ mnemonic: e.target.value })}
                                        placeholder="拼音首字母等快速检索用，可留空"
                                    />
                                </Field>
                                <Field>
                                    <FieldLabel htmlFor="edit-enabled">状态</FieldLabel>
                                    <RadioGroup
                                        id="edit-enabled"
                                        className="flex flex-row items-center gap-4"
                                        value={editForm.values.isEnabled ? "true" : "false"}
                                        onValueChange={(v) => editForm.patch({ isEnabled: v === "true" })}
                                    >
                                        <div className="flex items-center gap-2">
                                            <RadioGroupItem value="true" id="edit-enabled-true" />
                                            <FieldLabel htmlFor="edit-enabled-true">启用</FieldLabel>
                                        </div>
                                        <div className="flex items-center gap-2">
                                            <RadioGroupItem value="false" id="edit-enabled-false" />
                                            <FieldLabel htmlFor="edit-enabled-false">停用</FieldLabel>
                                        </div>
                                    </RadioGroup>
                                </Field>
                                {editError && (
                                    <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                        <CircleAlert />
                                        <AlertTitle>{editError}</AlertTitle>
                                    </Alert>
                                )}
                            </div>
                            <SheetFooter className="flex-row justify-end">
                                <Button
                                    variant="outline"
                                    onClick={() => {
                                        setEditOpen(false)
                                        setEditTarget(null)
                                    }}
                                >
                                    取消
                                </Button>
                                <Button onClick={() => void saveEdit()} disabled={editSaving}>
                                    {editSaving && <Spinner data-icon="inline-start" />}
                                    保存
                                </Button>
                            </SheetFooter>
                        </>
                    )}
                </SheetContent>
            </Sheet>

            {/* 导入科目抽屉 */}
            <Sheet
                open={importOpen}
                onOpenChange={(next) => {
                    setImportOpen(next)
                    if (!next) pickImportFile(null)
                }}
            >
                <SheetContent className="sm:max-w-2xl">
                    <SheetHeader>
                        <SheetTitle>导入科目</SheetTitle>
                        <SheetDescription>
                            支持从 Excel 文件批量导入会计科目。系统按编码查重，已存在的科目自动跳过并告警。
                        </SheetDescription>
                    </SheetHeader>
                    <div className="flex flex-1 flex-col gap-4 overflow-auto px-4 pb-4">
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">导入到账套</span>
                            <span className="text-[12.5px] text-slate-400">
                                {currentSet?.name ?? accountSetId}
                                {currentSet?.isCurrent ? "（当前）" : ""}
                            </span>
                        </div>

                        {/* 下载模板 */}
                        <div className="flex items-center gap-3">
                            <Button variant="outline" onClick={() => void downloadTemplate()} disabled={!accountSetId}>
                                <Download data-icon="inline-start" />
                                下载导入模板
                            </Button>
                        </div>

                        {/* 文件拖放 / 选择区 */}
                        <div
                            role="button"
                            tabIndex={0}
                            onClick={() => importInputRef.current?.click()}
                            onKeyDown={(e) => {
                                if (e.key === "Enter" || e.key === " ") importInputRef.current?.click()
                            }}
                            onDragOver={(e) => {
                                e.preventDefault()
                                setImportDragging(true)
                            }}
                            onDragLeave={() => setImportDragging(false)}
                            onDrop={onImportDrop}
                            className={cn(
                                "flex cursor-pointer flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-10 text-center transition-colors",
                                importDragging ? "border-[#bfdbfe] bg-[#eef4ff]" : "border-slate-200",
                            )}
                        >
                            <FileSpreadsheet size={40} strokeWidth={1.2} className="text-slate-300" />
                            {importFile ? (
                                <div className="flex flex-col gap-1">
                                    <span className="font-medium">{importFile.name}</span>
                                    <FieldDescription>
                                        {(importFile.size / 1024).toFixed(1)} KB · 点击可重新选择
                                    </FieldDescription>
                                </div>
                            ) : (
                                <div className="flex flex-col gap-1">
                                    <span className="font-medium">点击选择，或拖拽文件到此处</span>
                                    <FieldDescription>支持 .xls / .xlsx，最大 10MB</FieldDescription>
                                </div>
                            )}
                            <input
                                ref={importInputRef}
                                type="file"
                                accept={ACCEPT}
                                className="hidden"
                                onChange={(e) => pickImportFile(e.target.files?.[0] ?? null)}
                            />
                        </div>

                        <div className="flex items-center gap-3">
                            <Button
                                onClick={() => void handleImport()}
                                disabled={!importFile || !accountSetId || importing}
                            >
                                {importing ? <Spinner data-icon="inline-start" /> : <Upload data-icon="inline-start" />}
                                {importing ? "导入中…" : "开始导入"}
                            </Button>
                            {importFile && !importing && (
                                <Button variant="outline" onClick={() => pickImportFile(null)}>清除</Button>
                            )}
                        </div>

                        {importError && (
                            <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{importError}</AlertTitle>
                            </Alert>
                        )}

                        {/* 导入结果 */}
                        {importResult && (
                            <div className={cn(SOFT_PANEL, "flex flex-col gap-4 p-4")}>
                                <h3 className="text-[13px] font-semibold text-slate-800">导入结果</h3>
                                <div className="grid grid-cols-3 gap-3">
                                    <Tile label="解析行数" value={importResult.totalRows ?? 0} tone="blue" icon={FileSpreadsheet} />
                                    <Tile label="导入科目" value={importResult.importedAccounts ?? 0} tone="green" icon={Plus} />
                                    <Tile label="跳过科目" value={importResult.skippedAccounts ?? 0} tone="gray" icon={ChevronRight} />
                                </div>

                                {importResult.warnings && importResult.warnings.length > 0 && (
                                    <div className="flex flex-col gap-2">
                                        <span className="text-[12.5px] text-slate-400">
                                            告警（{importResult.warnings.length}）
                                        </span>
                                        <ul className="max-h-64 overflow-auto rounded-lg border border-slate-100 bg-white p-3 text-[12.5px] text-slate-500">
                                            {importResult.warnings.map((w, i) => (
                                                <li key={i} className={cn("py-0.5")}>
                                                    · {w}
                                                </li>
                                            ))}
                                        </ul>
                                    </div>
                                )}
                            </div>
                        )}
                    </div>
                </SheetContent>
            </Sheet>

            {/* 编码方案弹窗 */}
            <Dialog open={schemeModalOpen} onOpenChange={setSchemeModalOpen}>
                <DialogContent aria-describedby={undefined} className="sm:max-w-2xl">
                    <DialogHeader>
                        <DialogTitle>科目编码方案</DialogTitle>
                    </DialogHeader>
                    <div className="flex flex-col gap-4">
                        <Field>
                            <FieldLabel htmlFor="scheme-levels">科目级次</FieldLabel>
                            <Select
                                value={String(schemeLevels)}
                                onValueChange={(v) => handleSchemeLevelsChange(Number(v))}
                            >
                                <SelectTrigger id="scheme-levels">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    {Array.from({ length: 7 }, (_, i) => (
                                        <SelectItem key={i} value={String(i + 4)}>{`${i + 4} 级`}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </Field>

                        <Field>
                            <FieldLabel>各级位数</FieldLabel>
                            <div className="flex flex-wrap items-center gap-2">
                                {Array.from({ length: schemeLevels }, (_, i) => (
                                    <div key={i} className="flex items-center gap-2">
                                        {i > 0 && <span className="text-slate-400">-</span>}
                                        <Select
                                            value={String(schemeDigits[i] ?? 2)}
                                            onValueChange={(v) => handleSchemeDigitChange(i, Number(v))}
                                        >
                                            <SelectTrigger>
                                                <SelectValue />
                                            </SelectTrigger>
                                            <SelectContent>
                                                {[2, 3, 4].map((d) => (
                                                    <SelectItem key={d} value={String(d)}>{`${d}`}</SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                    </div>
                                ))}
                            </div>
                        </Field>

                        {/* 示例预览 */}
                        <div className={cn(SOFT_PANEL, "flex flex-col gap-1 px-4 py-3")}>
                            <div className="flex flex-wrap items-center justify-between gap-2 text-[12.5px]">
                                <span className="text-slate-400">
                                    编码方案：{schemeDigits.slice(0, schemeLevels).join("-")}
                                </span>
                                <code className="whitespace-nowrap text-[13px] font-medium text-slate-700 tabular-nums">{schemePreview}</code>
                            </div>
                            <span className="text-[12.5px] text-slate-400">
                                各级总位数：{schemeTotalDigits.join(" → ")}
                            </span>
                        </div>

                        {schemeError && (
                            <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{schemeError}</AlertTitle>
                            </Alert>
                        )}
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setSchemeModalOpen(false)}>取消</Button>
                        <Button onClick={() => saveScheme()} disabled={schemeSaving}>
                            {schemeSaving && <Spinner data-icon="inline-start" />}
                            保存
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 更改编码方案的二次确认（原 Modal.confirm） */}
            <Dialog open={schemeConfirmOpen} onOpenChange={setSchemeConfirmOpen}>
                <DialogContent aria-describedby="scheme-confirm-desc">
                    <DialogHeader>
                        <DialogTitle>更改编码方案</DialogTitle>
                        <DialogDescription id="scheme-confirm-desc">
                            {`当前账套下已有 ${accounts.length} 个科目。更改编码方案将按新规则重建所有科目编码（如二级 "01" 变为 "001"）。确定继续吗？`}
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setSchemeConfirmOpen(false)}>取消</Button>
                        <Button
                            variant="destructive"
                            disabled={schemeSaving}
                            onClick={() => {
                                setSchemeConfirmOpen(false)
                                void doSaveScheme(pendingScheme)
                            }}
                        >
                            {schemeSaving && <Spinner data-icon="inline-start" />}
                            确定
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 辅助核算设置弹窗：勾选该科目启用的类别，并可关闭「必录」 */}
            <Dialog
                open={assistOpen}
                onOpenChange={(next) => {
                    setAssistOpen(next)
                    if (!next) setAssistTarget(null)
                }}
            >
                <DialogContent aria-describedby="assist-setting-desc" className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>{`辅助核算 — ${assistTarget?.code ?? ""} ${assistTarget?.name ?? ""}`}</DialogTitle>
                        <DialogDescription id="assist-setting-desc">
                            挂上辅助核算后，凭证里该科目分录必须录入对应类别的核算项目档案（标「必录」）。改动只影响后续制单，历史凭证取值不变。
                        </DialogDescription>
                    </DialogHeader>
                    {assistLoading ? (
                        <div className="flex items-center gap-2 py-4 text-slate-400">
                            <Spinner />
                            加载中…
                        </div>
                    ) : (
                        <div className="flex flex-col gap-2">
                            {assistRows.map((row) => {
                                const meta = ASSIST_TYPES.find((t) => t.value === row.type)
                                const enabledInSet = assistCategories.find((c) => c.type === row.type)?.isEnabled === true
                                return (
                                    <div key={row.type} className="flex items-center gap-3">
                                        <label className="flex flex-1 items-center gap-2">
                                            <Checkbox
                                                checked={row.checked}
                                                disabled={!enabledInSet && !row.checked}
                                                onCheckedChange={(v) =>
                                                    setAssistRows((prev) =>
                                                        prev.map((r) => (r.type === row.type ? { ...r, checked: v === true } : r)),
                                                    )
                                                }
                                            />
                                            <span>{meta?.label ?? row.type}</span>
                                            {!enabledInSet && !row.checked && (
                                                <span className="text-[11px] text-slate-400">账套未启用</span>
                                            )}
                                        </label>
                                        {row.checked && (
                                            <label className="flex items-center gap-2 text-sm">
                                                <Checkbox
                                                    checked={row.isRequired}
                                                    onCheckedChange={(v) =>
                                                        setAssistRows((prev) =>
                                                            prev.map((r) =>
                                                                r.type === row.type ? { ...r, isRequired: v === true } : r,
                                                            ),
                                                        )
                                                    }
                                                />
                                                必录
                                            </label>
                                        )}
                                    </div>
                                )
                            })}
                        </div>
                    )}
                    {assistError && (
                        <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                            <CircleAlert />
                            <AlertTitle>{assistError}</AlertTitle>
                        </Alert>
                    )}
                    <DialogFooter>
                        <Button
                            variant="outline"
                            onClick={() => {
                                setAssistOpen(false)
                                setAssistTarget(null)
                            }}
                        >
                            取消
                        </Button>
                        <Button onClick={() => void saveAssistDialog()} disabled={assistSaving || assistLoading}>
                            {assistSaving && <Spinner data-icon="inline-start" />}
                            保存
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {confirmDialog}
        </div>
    )
}
