import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import {
    Alert,
    Button,
    Card,
    Checkbox,
    Drawer,
    Input,
    Modal,
    Radio,
    Select,
    Spin,
    Table,
    Tabs,
} from "antd"
import type { TableColumnsType } from "antd"
import {
    DownOutlined,
    DownloadOutlined,
    FileExcelOutlined,
    PlusOutlined,
    ReloadOutlined,
    RightOutlined,
    SearchOutlined,
    UploadOutlined,
} from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { AccountsApi, AccountSetsApi, type AccountAssistTypeDto, type AccountDto, type AccountImportResult } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { useLocalForm } from "@/hooks/useLocalForm"
import { useConfirm } from "@/hooks/useConfirm"
import { ASSIST_TYPES, assistLabel } from "@/components/assist/constants"
import { Tile } from "@/components/page/tile"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"
import { ICON_STYLE, SOFT_PANEL } from "@/components/page/styles"
import {
    fetchAccountAssistTypes,
    setAccountAssistTypesCache,
    useAccountAssistConfigs,
    useAssistCategories,
} from "@/components/assist/assist-store"

/** 会计科目类别（数值对应后端枚举 AccountCategory）。 */
const CATEGORY_OPTIONS: { value: number; label: string }[] = [
    { value: 1, label: "资产" },
    { value: 2, label: "负债" },
    { value: 3, label: "权益" },
    { value: 4, label: "成本" },
    { value: 5, label: "损益" },
]

/** 余额方向（数值对应后端枚举 BalanceDirection）。 */
const DIRECTION_OPTIONS: { value: number; label: string }[] = [
    { value: 1, label: "借" },
    { value: 2, label: "贷" },
]

const CATEGORY_LABELS: Record<number, string> = Object.fromEntries(
    CATEGORY_OPTIONS.map((c) => [c.value, c.label]),
)
const DIRECTION_LABELS: Record<number, string> = Object.fromEntries(
    DIRECTION_OPTIONS.map((d) => [d.value, d.label]),
)

/** 科目导入支持的文件格式。 */
const ACCEPT = ".xls,.xlsx"

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

export default function AccountsPage() {
    const { token } = useAuth()
    const { accountSets, refresh: refreshAccountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { confirm, confirmDialog } = useConfirm()

    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [loading, setLoading] = useState(false)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [filter, setFilter] = useState<number>(1)
    const [keyword, setKeyword] = useState("")
    const [showDisabled, setShowDisabled] = useState(true)
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
            // 与柠檬云一致：默认收起，只显示一级科目；展开由用户点击行首箭头
            setExpandedKeys([])
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

    // 关键字匹配编码 / 名称 / 助记码 / 状态；命中明细时保留其上级作为路径。
    const normalizedKeyword = keyword.trim().toLowerCase()
    const matchesKeyword = useCallback(
        (account: AccountDto) =>
            !normalizedKeyword ||
            [account.code, account.name, account.mnemonic, account.isEnabled ? "启用" : "停用"].some((value) =>
                (value ?? "").toLowerCase().includes(normalizedKeyword),
            ),
        [normalizedKeyword],
    )

    // 按父子关系构建树（顶级按类别筛选，明细挂在父级 children 下）。
    const treeData = useMemo<AccountNode[]>(() => {
        const byCode = (a: AccountDto, b: AccountDto) => (a.code ?? "").localeCompare(b.code ?? "")
        const build = (pid: string | null): AccountNode[] => {
            const children = accounts
                .filter((a) => (a.parentId ?? null) === pid)
                .filter((a) => pid !== null || a.category === filter)
                .filter((a) => showDisabled || a.isEnabled)
                .sort(byCode)
            const nodes: AccountNode[] = []
            for (const acc of children) {
                const sub = build(acc.id ?? null)
                if (matchesKeyword(acc) || sub.length > 0) {
                    nodes.push({ ...acc, children: sub.length > 0 ? sub : undefined })
                }
            }
            return nodes
        }
        return build(null)
    }, [accounts, filter, showDisabled, matchesKeyword])

    // 当前树上可展开的节点，供「展开所有级次」使用。
    const expandableIds = useMemo(() => {
        const ids: string[] = []
        const walk = (nodes: AccountNode[]) => {
            for (const node of nodes) {
                if (node.children?.length) {
                    ids.push(String(node.id))
                    walk(node.children)
                }
            }
        }
        walk(treeData)
        return ids
    }, [treeData])

    const expandedSet = useMemo(() => new Set(expandedKeys.map((k) => String(k))), [expandedKeys])
    const allExpanded = expandableIds.length > 0 && expandableIds.every((id) => expandedSet.has(id))

    // 受控展开：由 treeData + expandedKeys 算出要渲染的扁平可见行序列（含深度）。
    // 搜索时强制展开命中路径，直接露出匹配结果。
    const visibleRows = useMemo<VisibleRow[]>(() => {
        const searching = normalizedKeyword.length > 0
        const out: VisibleRow[] = []
        const walk = (nodes: AccountNode[], depth: number) => {
            for (const node of nodes) {
                out.push({ node, depth })
                if (node.children && (searching || expandedSet.has(String(node.id)))) walk(node.children, depth + 1)
            }
        }
        walk(treeData, 0)
        return out
    }, [treeData, expandedSet, normalizedKeyword])

    function toggleExpand(id: string) {
        setExpandedKeys((prev) => (prev.includes(id) ? prev.filter((k) => k !== id) : [...prev, id]))
    }

    // 上级科目下拉可选项：当前类别下所有还能再开下级的科目（按编码排序）。
    const parentOptions = useMemo(() => {
        const maxLevel = schemeSegments.length
        return [...accounts]
            .filter((a) => a.category === createForm.values.category && (a.level ?? 0) < maxLevel)
            .sort((a, b) => (a.code ?? "").localeCompare(b.code ?? ""))
            .map((a) => ({
                value: a.id as string,
                label: `${"　".repeat(Math.max((a.level ?? 1) - 1, 0))}${a.code} ${a.name}`,
            }))
    }, [accounts, createForm.values.category, schemeSegments])

    /** 打开新增抽屉；从某行「新增」进入时预填上级科目（类别、方向随上级）。 */
    function openDrawer(parentNode?: AccountDto) {
        createForm.fill(
            parentNode
                ? {
                      parentId: parentNode.id ?? null,
                      code: "",
                      name: "",
                      category: parentNode.category ?? 1,
                      direction: parentNode.direction ?? 1,
                      mnemonic: "",
                      isEnabled: true,
                  }
                : { parentId: null, code: "", name: "", category: 1, direction: 1, mnemonic: "", isEnabled: true },
        )
        if (parentNode?.id) {
            setExpandedKeys((prev) => [...new Set([...prev, parentNode.id as string])])
        }
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

    /** 导出账套全部科目（.xlsx，列与导入模板一致，可原样回传导入）。 */
    async function exportAccounts() {
        if (!accountSetId) return
        try {
            const response = await fetch(`/api/accountsets/${accountSetId}/Accounts/export`, {
                headers: { Authorization: `Bearer ${token}` },
            })
            if (!response.ok) throw new Error(`导出失败（${response.status}）`)
            const blob = await response.blob()
            const url = URL.createObjectURL(blob)
            const a = document.createElement("a")
            a.href = url
            a.download = `${currentSet?.name ?? "科目"}-科目.xlsx`
            a.click()
            URL.revokeObjectURL(url)
        } catch (err) {
            setLoadError(err instanceof Error ? err.message : "导出科目失败，请重试。")
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

    const columns: TableColumnsType<VisibleRow> = [
        {
            title: "序号",
            width: 64,
            align: "center",
            render: (_, __, index) => <span className="tabular-nums text-slate-500">{index + 1}</span>,
        },
        {
            title: "科目编码",
            width: 230,
            render: (_, { node, depth }) => {
                const id = String(node.id)
                // 搜索时可见行强制展开，箭头随之显示为展开态。
                const expanded = normalizedKeyword.length > 0 || expandedSet.has(id)
                return (
                    <div className={cn("flex items-center gap-1", indentClass(depth))}>
                        {node.children ? (
                            <Button
                                type="text"
                                size="small"
                                className="size-5 min-w-5 p-0"
                                aria-label={expanded ? "收起下级科目" : "展开下级科目"}
                                aria-expanded={expanded}
                                icon={expanded ? <DownOutlined /> : <RightOutlined />}
                                onClick={() => toggleExpand(id)}
                            />
                        ) : (
                            // 占位，保持与可展开行同宽，编码列纵向对齐。
                            <span className="size-5 shrink-0" />
                        )}
                        <span className="whitespace-nowrap tabular-nums">{node.code}</span>
                    </div>
                )
            },
        },
        {
            title: "科目名称",
            render: (_, { node }) => {
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
        },
        {
            title: "助记码",
            width: 150,
            render: (_, { node }) =>
                node.mnemonic ? <span className="text-slate-600">{node.mnemonic}</span> : <span className="text-slate-300">—</span>,
        },
        {
            title: "余额方向",
            width: 96,
            align: "center",
            render: (_, { node }) => DIRECTION_LABELS[node.direction ?? 0] ?? "—",
        },
        {
            title: "状态",
            width: 88,
            align: "center",
            render: (_, { node }) =>
                node.isEnabled ? <span className="text-slate-700">启用</span> : <span className="text-slate-400">停用</span>,
        },
        {
            title: "操作",
            width: 176,
            render: (_, { node }) => {
                const canAddChild = (node.level ?? 0) < schemeSegments.length
                return (
                    <div className="flex items-center gap-2">
                        {canAddChild && (
                            <Button type="link" size="small" style={{ padding: 0 }} onClick={() => openDrawer(node)}>
                                新增
                            </Button>
                        )}
                        <Button type="link" size="small" style={{ padding: 0 }} onClick={() => openEditDrawer(node)}>
                            编辑
                        </Button>
                        <Button
                            type="link"
                            size="small"
                            style={{ padding: 0 }}
                            title="设置辅助核算"
                            onClick={() => void openAssistDialog(node)}
                        >
                            辅助
                        </Button>
                        <Button
                            type="link"
                            size="small"
                            danger
                            style={{ padding: 0 }}
                            loading={deletingId === node.id}
                            title={node.children ? "删除科目（需先删除下级）" : "删除科目"}
                            onClick={() => void removeAccount(node)}
                        >
                            删除
                        </Button>
                    </div>
                )
            },
        },
    ]

    // 科目类别页签：与柠檬云「科目」一致（资产/负债/共同/权益/成本/损益）。
    // 「共同」类在快记无对应枚举，作为空页签保留位置。
    const filterOptions = [
        { value: "1", label: "资产" },
        { value: "2", label: "负债" },
        { value: "-1", label: "共同" },
        { value: "3", label: "权益" },
        { value: "4", label: "成本" },
        { value: "5", label: "损益" },
    ]

    const emptyText = !accountSetId
        ? "请先选择账套。"
        : normalizedKeyword
          ? "没有匹配的科目，请调整搜索关键字。"
          : !showDisabled && accounts.length > 0
            ? "当前分类下没有启用的科目，可勾选「显示停用科目」查看。"
            : "暂无科目，可点击「新增科目」或通过凭证导入自动建立。"

    return (
        <div className="flex flex-col gap-4">
            {loadError && <Alert type="error" showIcon title={loadError} closable onClose={() => setLoadError(null)} />}

            <Card
                variant="borderless"
                styles={{
                    root: { boxShadow: "0 1px 2px rgba(16,24,40,0.04)" },
                    body: { padding: 0 },
                }}
            >
                <Tabs
                    activeKey={String(filter)}
                    items={filterOptions.map((option) => ({ key: option.value, label: option.label }))}
                    onChange={(key) => setFilter(Number(key))}
                    tabBarStyle={{ margin: 0, paddingInline: 16 }}
                />

                <div className="flex flex-wrap items-center gap-x-4 gap-y-2 border-b border-slate-100 px-4 py-2.5">
                    <Input
                        allowClear
                        prefix={<SearchOutlined className="text-slate-400" />}
                        placeholder="输入编码/名称/助记码/状态等关键字搜索"
                        value={keyword}
                        onChange={(e) => setKeyword(e.target.value)}
                        disabled={!accountSetId}
                        style={{ width: 280 }}
                    />
                    <Checkbox
                        checked={allExpanded}
                        disabled={!accountSetId || expandableIds.length === 0}
                        onChange={(e) => setExpandedKeys(e.target.checked ? expandableIds : [])}
                    >
                        展开所有级次
                    </Checkbox>
                    <div className="ml-auto flex flex-wrap items-center gap-2">
                        <Checkbox
                            checked={showDisabled}
                            disabled={!accountSetId}
                            onChange={(e) => setShowDisabled(e.target.checked)}
                        >
                            显示停用科目
                        </Checkbox>
                        <Button type="primary" icon={<PlusOutlined />} disabled={!accountSetId} onClick={() => openDrawer()}>
                            新增科目
                        </Button>
                        <Button disabled={!accountSetId} onClick={openSchemeModal}>
                            编码设置
                        </Button>
                        <Button disabled={!accountSetId} onClick={() => void exportAccounts()}>
                            导出
                        </Button>
                        <Button icon={<UploadOutlined />} disabled={!accountSetId} onClick={() => setImportOpen(true)}>
                            导入
                        </Button>
                        <Button
                            type="text"
                            title="刷新"
                            aria-label="刷新"
                            icon={<ReloadOutlined spin={loading} />}
                            disabled={!accountSetId || loading}
                            onClick={() => void load()}
                        />
                    </div>
                </div>

                <Table<VisibleRow>
                    rowKey={(row) => row.node.id ?? ""}
                    columns={columns}
                    dataSource={visibleRows}
                    pagination={false}
                    bordered
                    size="middle"
                    loading={loading && accounts.length === 0}
                    locale={{ emptyText }}
                />
            </Card>

            {/* 新增科目抽屉 */}
            <Drawer
                open={open}
                size={512}
                title="新增科目"
                onClose={() => setOpen(false)}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
                footer={
                    <div className="flex justify-end gap-2">
                        <Button onClick={() => setOpen(false)}>取消</Button>
                        <Button type="primary" loading={saving} icon={<PlusOutlined />} onClick={() => void addAccount()}>
                            添加科目
                        </Button>
                    </div>
                }
            >
                <p className="text-[12.5px] text-slate-400">
                    先选择类别，再选择上级科目（可选）。选中上级后将创建明细科目，方向自动继承。
                </p>

                <FormField label="类别" htmlFor="create-category" error={createForm.errors.category}>
                    <Select
                        id="create-category"
                        className="w-full"
                        value={createForm.values.category}
                        options={CATEGORY_OPTIONS}
                        onChange={(v) => {
                            // 切换类别时清空上级科目，避免跨类别挂载。
                            createForm.patch({ category: v, parentId: null })
                        }}
                    />
                </FormField>

                <FormField label="上级科目" htmlFor="create-parent">
                    <Select
                        id="create-parent"
                        className="w-full"
                        allowClear
                        placeholder="（顶级科目）"
                        value={createForm.values.parentId ?? undefined}
                        options={parentOptions}
                        onChange={(v) => {
                            // 清空即不选上级，创建顶级科目。
                            if (v == null) {
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
                    />
                </FormField>

                <FormField label="科目编码" htmlFor="create-code" required error={createForm.errors.code}>
                    <Input
                        id="create-code"
                        value={createForm.values.code}
                        onChange={(e) => createForm.patch({ code: e.target.value })}
                        placeholder={
                            parent
                                ? `${parent.code}${"0".repeat(levelDigits)}（${expectedCodeLength} 位）`
                                : `${"0".repeat(levelDigits)}（${expectedCodeLength} 位）`
                        }
                        status={createForm.errors.code ? "error" : undefined}
                    />
                </FormField>

                <FormField label="科目名称" htmlFor="create-name" required error={createForm.errors.name}>
                    <Input
                        id="create-name"
                        value={createForm.values.name}
                        onChange={(e) => createForm.patch({ name: e.target.value })}
                        placeholder="如 库存现金"
                        status={createForm.errors.name ? "error" : undefined}
                    />
                </FormField>

                <FormField label="余额方向" htmlFor="create-direction">
                    <Radio.Group
                        id="create-direction"
                        disabled={parent !== null}
                        value={createForm.values.direction}
                        options={DIRECTION_OPTIONS}
                        onChange={(e) => createForm.patch({ direction: e.target.value })}
                    />
                </FormField>

                <FormField label="助记码" htmlFor="create-mnemonic">
                    <Input
                        id="create-mnemonic"
                        value={createForm.values.mnemonic}
                        onChange={(e) => createForm.patch({ mnemonic: e.target.value })}
                        placeholder="拼音首字母等快速检索用，可留空"
                    />
                </FormField>

                <FormField label="状态" htmlFor="create-enabled">
                    <Radio.Group
                        id="create-enabled"
                        value={createForm.values.isEnabled ? 1 : 2}
                        options={[{ value: 1, label: "启用" }, { value: 2, label: "停用" }]}
                        onChange={(e) => createForm.patch({ isEnabled: e.target.value === 1 })}
                    />
                </FormField>

                {parent ? (
                    <p className="text-sm leading-normal font-normal text-slate-400">
                        将作为「{parent.code} {parent.name}」的第 {newLevel} 级明细科目（本级 {levelDigits} 位，
                        编码方案：{codingScheme}），余额方向已继承上级。
                    </p>
                ) : (
                    <p className="text-sm leading-normal font-normal text-slate-400">
                        一级科目编码固定 {expectedCodeLength} 位（编码方案：{codingScheme}）。
                    </p>
                )}

                {error && <Alert type="error" showIcon title={error} />}
            </Drawer>

            {/* 编辑科目抽屉 */}
            <Drawer
                open={editOpen}
                size={512}
                title={`编辑科目 — ${editTarget?.code ?? ""}`}
                onClose={() => {
                    setEditOpen(false)
                    setEditTarget(null)
                }}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
                footer={
                    <div className="flex justify-end gap-2">
                        <Button
                            onClick={() => {
                                setEditOpen(false)
                                setEditTarget(null)
                            }}
                        >
                            取消
                        </Button>
                        <Button type="primary" loading={editSaving} onClick={() => void saveEdit()}>
                            保存
                        </Button>
                    </div>
                }
            >
                {editTarget && (
                    <>
                        <FormField label="科目编码">
                            <span>{editTarget.code}</span>
                        </FormField>
                        <FormField label="类别">
                            <span>{CATEGORY_LABELS[editTarget.category!] ?? "—"}</span>
                        </FormField>
                        <FormField label="科目名称" htmlFor="edit-name" required error={editForm.errors.name}>
                            <Input
                                id="edit-name"
                                value={editForm.values.name}
                                onChange={(e) => editForm.patch({ name: e.target.value })}
                                placeholder="如 库存现金"
                                status={editForm.errors.name ? "error" : undefined}
                            />
                        </FormField>
                        <FormField
                            label="余额方向"
                            htmlFor="edit-direction"
                            error={editForm.errors.direction}
                            description={!editTarget.isLeaf ? "非末级科目不允许修改余额方向。" : undefined}
                        >
                            <Radio.Group
                                id="edit-direction"
                                disabled={!editTarget.isLeaf}
                                value={editForm.values.direction}
                                options={DIRECTION_OPTIONS}
                                onChange={(e) => editForm.patch({ direction: e.target.value })}
                            />
                        </FormField>
                        <FormField label="助记码" htmlFor="edit-mnemonic">
                            <Input
                                id="edit-mnemonic"
                                value={editForm.values.mnemonic}
                                onChange={(e) => editForm.patch({ mnemonic: e.target.value })}
                                placeholder="拼音首字母等快速检索用，可留空"
                            />
                        </FormField>
                        <FormField label="状态" htmlFor="edit-enabled">
                            <Radio.Group
                                id="edit-enabled"
                                value={editForm.values.isEnabled ? 1 : 2}
                                options={[{ value: 1, label: "启用" }, { value: 2, label: "停用" }]}
                                onChange={(e) => editForm.patch({ isEnabled: e.target.value === 1 })}
                            />
                        </FormField>
                        {editError && <Alert type="error" showIcon title={editError} />}
                    </>
                )}
            </Drawer>

            {/* 导入科目抽屉 */}
            <Drawer
                open={importOpen}
                size={672}
                title="导入科目"
                onClose={() => {
                    setImportOpen(false)
                    pickImportFile(null)
                }}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
            >
                <p className="text-[12.5px] text-slate-400">
                    支持从 Excel 文件批量导入会计科目。系统按编码查重，已存在的科目自动跳过并告警。
                </p>

                <div className="flex flex-col gap-1">
                    <span className="font-medium">导入到账套</span>
                    <span className="text-[12.5px] text-slate-400">
                        {currentSet?.name ?? accountSetId}
                        {currentSet?.isCurrent ? "（当前）" : ""}
                    </span>
                </div>

                {/* 下载模板 */}
                <div className="flex items-center gap-3">
                    <Button icon={<DownloadOutlined />} disabled={!accountSetId} onClick={() => void downloadTemplate()}>
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
                    <FileExcelOutlined className="text-[40px]" style={ICON_STYLE.placeholder} />
                    {importFile ? (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">{importFile.name}</span>
                            <span className="text-sm leading-normal font-normal text-slate-400">
                                {(importFile.size / 1024).toFixed(1)} KB · 点击可重新选择
                            </span>
                        </div>
                    ) : (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">点击选择，或拖拽文件到此处</span>
                            <span className="text-sm leading-normal font-normal text-slate-400">支持 .xls / .xlsx，最大 10MB</span>
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
                        type="primary"
                        loading={importing}
                        icon={importing ? undefined : <UploadOutlined />}
                        disabled={!importFile || !accountSetId}
                        onClick={() => void handleImport()}
                    >
                        {importing ? "导入中…" : "开始导入"}
                    </Button>
                    {importFile && !importing && <Button onClick={() => pickImportFile(null)}>清除</Button>}
                </div>

                {importError && <Alert type="error" showIcon title={importError} />}

                {/* 导入结果 */}
                {importResult && (
                    <div className={cn(SOFT_PANEL, "flex flex-col gap-4 p-4")}>
                        <h3 className="text-[13px] font-semibold text-slate-800">导入结果</h3>
                        <div className="grid grid-cols-3 gap-3">
                            <Tile label="解析行数" value={importResult.totalRows ?? 0} tone="blue" icon={FileExcelOutlined} />
                            <Tile label="导入科目" value={importResult.importedAccounts ?? 0} tone="green" icon={PlusOutlined} />
                            <Tile label="跳过科目" value={importResult.skippedAccounts ?? 0} tone="gray" icon={RightOutlined} />
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
            </Drawer>

            {/* 编码方案弹窗 */}
            <Modal
                open={schemeModalOpen}
                width={768}
                title="科目编码设置"
                okText="保存"
                cancelText="取消"
                confirmLoading={schemeSaving}
                onOk={() => saveScheme()}
                onCancel={() => setSchemeModalOpen(false)}
            >
                <div className="flex flex-col gap-4">
                    <FormField label="科目级次" htmlFor="scheme-levels">
                        <Select
                            id="scheme-levels"
                            className="w-full"
                            value={schemeLevels}
                            options={Array.from({ length: 7 }, (_, i) => ({ value: i + 4, label: `${i + 4} 级` }))}
                            onChange={(v) => handleSchemeLevelsChange(v)}
                        />
                    </FormField>

                    <FormField label="各级位数">
                        <div className="flex flex-wrap items-center gap-2">
                            {Array.from({ length: schemeLevels }, (_, i) => (
                                <div key={i} className="flex items-center gap-2">
                                    {i > 0 && <span className="text-slate-400">-</span>}
                                    <Select
                                        className="w-16"
                                        value={schemeDigits[i] ?? 2}
                                        options={[2, 3, 4].map((d) => ({ value: d, label: `${d}` }))}
                                        onChange={(v) => handleSchemeDigitChange(i, v)}
                                    />
                                </div>
                            ))}
                        </div>
                    </FormField>

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

                    {schemeError && <Alert type="error" showIcon title={schemeError} />}
                </div>
            </Modal>

            {/* 更改编码方案的二次确认 */}
            <Modal
                open={schemeConfirmOpen}
                width={384}
                title="更改编码方案"
                okText="确定"
                cancelText="取消"
                okButtonProps={{ danger: true, loading: schemeSaving }}
                onOk={() => {
                    setSchemeConfirmOpen(false)
                    void doSaveScheme(pendingScheme)
                }}
                onCancel={() => setSchemeConfirmOpen(false)}
            >
                <p className="text-[13px] text-slate-500">
                    {`当前账套下已有 ${accounts.length} 个科目。更改编码方案将按新规则重建所有科目编码（如二级 "01" 变为 "001"）。确定继续吗？`}
                </p>
            </Modal>

            {/* 辅助核算设置弹窗：勾选该科目启用的类别，并可关闭「必录」 */}
            <Modal
                open={assistOpen}
                width={448}
                title={`辅助核算 — ${assistTarget?.code ?? ""} ${assistTarget?.name ?? ""}`}
                okText="保存"
                cancelText="取消"
                confirmLoading={assistSaving}
                okButtonProps={{ disabled: assistLoading }}
                onOk={() => void saveAssistDialog()}
                onCancel={() => {
                    setAssistOpen(false)
                    setAssistTarget(null)
                }}
            >
                <p className="mb-4 text-[12.5px] text-slate-400">
                    挂上辅助核算后，凭证里该科目分录必须录入对应类别的核算项目档案（标「必录」）。改动只影响后续制单，历史凭证取值不变。
                </p>
                {assistLoading ? (
                    <div className="flex items-center gap-2 py-4 text-slate-400">
                        <Spin />
                        加载中…
                    </div>
                ) : (
                    <div className="flex flex-col gap-2">
                        {assistRows.map((row) => {
                            const meta = ASSIST_TYPES.find((t) => t.value === row.type)
                            const enabledInSet = assistCategories.find((c) => c.type === row.type)?.isEnabled === true
                            return (
                                <div key={row.type} className="flex items-center gap-3">
                                    <Checkbox
                                        className="flex-1"
                                        checked={row.checked}
                                        disabled={!enabledInSet && !row.checked}
                                        onChange={(e) =>
                                            setAssistRows((prev) =>
                                                prev.map((r) => (r.type === row.type ? { ...r, checked: e.target.checked } : r)),
                                            )
                                        }
                                    >
                                        {meta?.label ?? row.type}
                                        {!enabledInSet && !row.checked && (
                                            <span className="ml-1 text-[11px] text-slate-400">账套未启用</span>
                                        )}
                                    </Checkbox>
                                    {row.checked && (
                                        <Checkbox
                                            checked={row.isRequired}
                                            onChange={(e) =>
                                                setAssistRows((prev) =>
                                                    prev.map((r) =>
                                                        r.type === row.type ? { ...r, isRequired: e.target.checked } : r,
                                                    ),
                                                )
                                            }
                                        >
                                            必录
                                        </Checkbox>
                                    )}
                                </div>
                            )
                        })}
                    </div>
                )}
                {assistError && <Alert className="mt-4" type="error" showIcon title={assistError} />}
            </Modal>

            {confirmDialog}
        </div>
    )
}
