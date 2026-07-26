import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { Download, FileSpreadsheet, Pencil, Plus, RefreshCw, Trash2, Upload } from "lucide-react"
import {
    Alert,
    Button,
    Card,
    Col,
    Drawer,
    Form,
    Input,
    Modal,
    Radio,
    Row,
    Segmented,
    Select,
    Space,
    Statistic,
    Table,
    Tag,
    Typography,
    theme,
} from "antd"
import type { ColumnsType } from "antd/es/table"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { AccountsApi, AccountSetsApi, type AccountDto, type AccountImportResult } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"

const { Title, Text } = Typography

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

/** 树节点：在 AccountDto 基础上挂 children，供 antd Table 树形展开。 */
interface AccountNode extends AccountDto {
    children?: AccountNode[]
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

export default function AccountsPage() {
    const { token } = useAuth()
    const { accountSets, refresh: refreshAccountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { token: t } = theme.useToken()

    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [loading, setLoading] = useState(false)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [filter, setFilter] = useState<number | "全部">("全部")
    const [expandedKeys, setExpandedKeys] = useState<React.Key[]>([])
    const [deletingId, setDeletingId] = useState<string | null>(null)

    const [error, setError] = useState("")
    const [saving, setSaving] = useState(false)
    const [open, setOpen] = useState(false)
    const [form] = Form.useForm<CreateForm>()
    const watchedParentId = Form.useWatch("parentId", form)
    const watchedCategory = Form.useWatch("category", form)

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
    const [editForm] = Form.useForm<EditForm>()

    // ── 编码方案弹窗 ──
    const [schemeModalOpen, setSchemeModalOpen] = useState(false)
    const [schemeLevels, setSchemeLevels] = useState(4)
    const [schemeDigits, setSchemeDigits] = useState<number[]>([4, 2, 2, 2])
    const [schemeSaving, setSchemeSaving] = useState(false)
    const [schemeError, setSchemeError] = useState("")

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

    const parent = useMemo(
        () => (watchedParentId == null ? null : accounts.find((a) => a.id === watchedParentId) ?? null),
        [accounts, watchedParentId],
    )

    const newLevel = parent ? (parent.level ?? 0) + 1 : 1
    const expectedCodeLength = useMemo(() => {
        if (parent) {
            return schemeSegments.slice(0, newLevel).reduce((s, d) => s + d, 0)
        }
        return schemeSegments[0] ?? 4
    }, [parent, newLevel, schemeSegments])
    const levelDigits = schemeSegments[newLevel - 1] ?? 2

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

    // 上级科目下拉可选项：仅显示当前选中类别下的顶级科目（按编码排序）；
    // 已达编码方案最大级次的科目不可再挂下级。
    const parentOptions = useMemo(() => {
        const scheme = (currentSet as any)?.accountCodingScheme ?? "4-2-2-2"
        const maxLevel = scheme.split("-").length
        return [...accounts]
            .filter((a) => a.parentId == null && a.category === watchedCategory && (a.level ?? 0) < maxLevel)
            .sort((a, b) => (a.code ?? "").localeCompare(b.code ?? ""))
            .map((a) => ({ value: a.id as string, label: `${a.code} ${a.name}（${a.level} 级）` }))
    }, [accounts, watchedCategory, currentSet])

    function openDrawer() {
        form.setFieldsValue({ parentId: null, code: "", name: "", category: 1, direction: 1, mnemonic: "", isEnabled: true })
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

    async function saveScheme() {
        if (schemeSaving || !accountSetId || !currentSet) return
        const scheme = schemeDigits.slice(0, schemeLevels).join("-")

        // 若当前有科目，提示用户编码将按新方案重建。
        if (accounts.length > 0) {
            Modal.confirm({
                title: "更改编码方案",
                content: `当前账套下已有 ${accounts.length} 个科目。更改编码方案将按新规则重建所有科目编码（如二级 "01" 变为 "001"）。确定继续吗？`,
                okText: "确定",
                cancelText: "取消",
                okButtonProps: { danger: true },
                onOk: async () => {
                    await doSaveScheme(scheme)
                },
            })
            return
        }

        await doSaveScheme(scheme)
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
        const values = await form.validateFields()
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
        if (!window.confirm(`确定删除科目「${account.code} ${account.name}」吗？此操作不可撤销。`)) return
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
        editForm.setFieldsValue({
            name: account.name,
            direction: account.direction,
            mnemonic: account.mnemonic ?? "",
            isEnabled: account.isEnabled,
        })
        setEditError("")
        setEditOpen(true)
    }

    async function saveEdit() {
        if (editSaving || !editTarget) return
        const values = await editForm.validateFields()
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

    const columns: ColumnsType<AccountNode> = [
        { title: "科目编码", dataIndex: "code", width: 160 },
        { title: "科目名称", dataIndex: "name", render: (v: string, r) => (r.parentId == null ? <Text strong>{v}</Text> : v) },
        { title: "级次", dataIndex: "level", width: 64, align: "center" as const, render: (v: number) => v ?? "—" },
        { title: "类别", dataIndex: "category", width: 100, render: (v?: number) => CATEGORY_LABELS[v ?? 0] ?? "—" },
        { title: "余额方向", dataIndex: "direction", width: 100, render: (v?: number) => DIRECTION_LABELS[v ?? 0] ?? "—" },
        {
            title: "状态",
            dataIndex: "isEnabled",
            width: 80,
            align: "center",
            render: (v?: boolean) => (
                v ? <Tag color="success">启用</Tag> : <Tag color="default">停用</Tag>
            ),
        },
        {
            title: "",
            key: "action",
            width: 90,
            align: "right",
            render: (_, account) => (
                <Space size={0}>
                    <Button
                        type="text"
                        size="small"
                        icon={<Pencil size={16} />}
                        onClick={() => openEditDrawer(account)}
                        title="编辑科目"
                    />
                    <Button
                        type="text"
                        size="small"
                        danger
                        icon={<Trash2 size={16} />}
                        loading={deletingId === account.id}
                        onClick={() => void removeAccount(account)}
                        title={account.children ? "删除科目（需先删除下级）" : "删除科目"}
                    />
                </Space>
            ),
        },
    ]

    const filterOptions: { value: number | "全部"; label: string }[] = [
        { value: "全部", label: "全部" },
        ...CATEGORY_OPTIONS.map((c) => ({ value: c.value as number, label: c.label })),
    ]

    return (
        <Space direction="vertical" size="large" style={{ display: "flex" }}>
            <div>
                <Title level={3} style={{ margin: 0 }}>科目</Title>
                <Text type="secondary">
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    {((currentSet as any)?.accountCodingScheme) ? ` · 编码方案：${(currentSet as any).accountCodingScheme}` : ""}
                    。维护多级会计科目表，一级科目下可挂明细科目，用于凭证录入与账簿核算。
                </Text>
            </div>

            <Card
                title="科目列表"
                extra={
                    <Space>
                        <Button icon={<RefreshCw size={16} />} onClick={() => void load()} loading={loading} disabled={!accountSetId}>
                            刷新
                        </Button>
                        <Button icon={<Upload size={16} />} onClick={() => setImportOpen(true)} disabled={!accountSetId}>
                            导入科目
                        </Button>
                        <Button icon={<Pencil size={16} />} onClick={openSchemeModal} disabled={!accountSetId}>
                            编码方案
                        </Button>
                        <Button type="primary" icon={<Plus size={16} />} onClick={openDrawer} disabled={!accountSetId}>
                            新增科目
                        </Button>
                    </Space>
                }
            >
                <Space direction="vertical" size="middle" style={{ display: "flex" }}>
                    {loadError && <Alert type="error" showIcon message={loadError} />}

                    <Segmented
                        value={filter}
                        onChange={(v) => setFilter(v as number | "全部")}
                        options={filterOptions}
                    />

                    <Table<AccountNode>
                        rowKey="id"
                        size="middle"
                        columns={columns}
                        dataSource={treeData}
                        loading={loading && accounts.length === 0}
                        pagination={false}
                        expandable={{
                            expandedRowKeys: expandedKeys,
                            onExpandedRowsChange: (keys) => setExpandedKeys([...keys]),
                        }}
                        locale={{
                            emptyText: accountSetId
                                ? "暂无科目，可点击「新增科目」或通过凭证导入自动建立。"
                                : "请先选择账套。",
                        }}
                    />
                </Space>
            </Card>

            <Drawer
                title="新增科目"
                open={open}
                onClose={() => setOpen(false)}
                width={480}
                footer={
                    <Space style={{ display: "flex", justifyContent: "flex-end" }}>
                        <Button onClick={() => setOpen(false)}>取消</Button>
                        <Button type="primary" icon={<Plus size={16} />} loading={saving} onClick={() => void addAccount()}>
                            添加科目
                        </Button>
                    </Space>
                }
            >
                <Text type="secondary">先选择类别，再选择上级科目（可选）。选中上级后将创建明细科目，方向自动继承。</Text>
                <Form form={form} layout="vertical" style={{ marginTop: 16 }} requiredMark>
                    <Form.Item label="类别" name="category" rules={[{ required: true, message: "请选择类别" }]}>
                        <Select
                            options={CATEGORY_OPTIONS.map((c) => ({ value: c.value, label: c.label }))}
                            onChange={() => {
                                // 切换类别时清空上级科目，避免跨类别挂载。
                                form.setFieldsValue({ parentId: null })
                            }}
                        />
                    </Form.Item>
                    <Form.Item label="上级科目" name="parentId">
                        <Select
                            allowClear
                            placeholder="（顶级科目）"
                            options={parentOptions}
                            onChange={(v) => {
                                const p = accounts.find((a) => a.id === v)
                                if (p) {
                                    // 明细科目方向继承上级。
                                    form.setFieldsValue({ direction: p.direction })
                                }
                            }}
                        />
                    </Form.Item>
                    <Form.Item
                        label="科目编码"
                        name="code"
                        rules={[
                            { required: true, message: "请填写科目编码" },
                            {
                                len: expectedCodeLength,
                                message: `第 ${newLevel} 级科目编码固定为 ${expectedCodeLength} 位（方案：${codingScheme}）`,
                            },
                        ]}
                    >
                        <Input
                            placeholder={
                                parent
                                    ? `${parent.code}${"0".repeat(levelDigits)}（${expectedCodeLength} 位）`
                                    : `${"0".repeat(levelDigits)}（${expectedCodeLength} 位）`
                            }
                        />
                    </Form.Item>
                    <Form.Item label="科目名称" name="name" rules={[{ required: true, message: "请填写科目名称" }]}>
                        <Input placeholder="如 库存现金" />
                    </Form.Item>
                    <Form.Item label="余额方向" name="direction" rules={[{ required: true, message: "请选择余额方向" }]}>
                        <Radio.Group disabled={parent !== null}>
                            {DIRECTION_OPTIONS.map((d) => (
                                <Radio key={d.value} value={d.value}>
                                    {d.label}
                                </Radio>
                            ))}
                        </Radio.Group>
                    </Form.Item>
                    <Form.Item label="助记码" name="mnemonic">
                        <Input placeholder="拼音首字母等快速检索用，可留空" />
                    </Form.Item>
                    <Form.Item label="状态" name="isEnabled">
                        <Radio.Group>
                            <Radio value={true}>启用</Radio>
                            <Radio value={false}>停用</Radio>
                        </Radio.Group>
                    </Form.Item>
                    {parent && (
                        <Text type="secondary" style={{ fontSize: 12 }}>
                            将作为「{parent.code} {parent.name}」的第 {newLevel} 级明细科目（本级 {levelDigits} 位，编码方案：
                            {codingScheme}），余额方向已继承上级。
                        </Text>
                    )}
                    {!parent && (
                        <Text type="secondary" style={{ fontSize: 12 }}>
                            一级科目编码固定 {expectedCodeLength} 位（编码方案：{codingScheme}）。
                        </Text>
                    )}
                    {error && <Alert type="error" showIcon message={error} style={{ marginTop: 12 }} />}
                </Form>
            </Drawer>

            {/* 编辑科目抽屉 */}
            <Drawer
                title={`编辑科目 — ${editTarget?.code ?? ""}`}
                open={editOpen}
                onClose={() => {
                    setEditOpen(false)
                    setEditTarget(null)
                }}
                width={480}
                footer={
                    <Space style={{ display: "flex", justifyContent: "flex-end" }}>
                        <Button onClick={() => { setEditOpen(false); setEditTarget(null) }}>取消</Button>
                        <Button type="primary" loading={editSaving} onClick={() => void saveEdit()}>
                            保存
                        </Button>
                    </Space>
                }
            >
                {editTarget && (
                    <Form form={editForm} layout="vertical" requiredMark>
                        <Form.Item label="科目编码">
                            <Input value={editTarget.code} disabled />
                        </Form.Item>
                        <Form.Item label="类别">
                            <Input value={CATEGORY_LABELS[editTarget.category!] ?? "—"} disabled />
                        </Form.Item>
                        <Form.Item label="科目名称" name="name" rules={[{ required: true, message: "请填写科目名称" }]}>
                            <Input placeholder="如 库存现金" />
                        </Form.Item>
                        <Form.Item label="余额方向" name="direction" rules={[{ required: true }]}>
                            <Radio.Group disabled={!editTarget.isLeaf}>
                                {DIRECTION_OPTIONS.map((d) => (
                                    <Radio key={d.value} value={d.value}>
                                        {d.label}
                                    </Radio>
                                ))}
                            </Radio.Group>
                            {!editTarget.isLeaf && (
                                <div>
                                    <Text type="secondary" style={{ fontSize: 12 }}>
                                        非末级科目不允许修改余额方向。
                                    </Text>
                                </div>
                            )}
                        </Form.Item>
                        <Form.Item label="助记码" name="mnemonic">
                            <Input placeholder="拼音首字母等快速检索用，可留空" />
                        </Form.Item>
                        <Form.Item label="状态" name="isEnabled">
                            <Radio.Group>
                                <Radio value={true}>启用</Radio>
                                <Radio value={false}>停用</Radio>
                            </Radio.Group>
                        </Form.Item>
                        {editError && <Alert type="error" showIcon message={editError} style={{ marginTop: 12 }} />}
                    </Form>
                )}
            </Drawer>

            {/* 导入科目抽屉 */}
            <Drawer
                open={importOpen}
                onClose={() => {
                    setImportOpen(false)
                    pickImportFile(null)
                }}
                width={640}
                title="导入科目"
            >
                <div className="flex flex-col gap-4">
                    <Text type="secondary">
                        支持从 Excel 文件批量导入会计科目。系统按编码查重，已存在的科目自动跳过并告警。
                    </Text>

                    <div className="flex flex-col gap-2">
                        <Text strong>导入到账套</Text>
                        <Text type="secondary">
                            {currentSet?.name ?? accountSetId}
                            {currentSet?.isCurrent ? "（当前）" : ""}
                        </Text>
                    </div>

                    {/* 下载模板 */}
                    <Button
                        icon={<Download size={16} />}
                        onClick={() => void downloadTemplate()}
                        disabled={!accountSetId}
                    >
                        下载导入模板
                    </Button>

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
                        style={{
                            borderColor: importDragging ? t.colorPrimary : t.colorBorder,
                            background: importDragging ? t.colorPrimaryBg : undefined,
                        }}
                        className="flex cursor-pointer flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-10 text-center transition-colors"
                    >
                        <FileSpreadsheet size={40} color={t.colorTextSecondary} />
                        {importFile ? (
                            <div className="flex flex-col gap-1">
                                <Text strong>{importFile.name}</Text>
                                <Text type="secondary" style={{ fontSize: 12 }}>
                                    {(importFile.size / 1024).toFixed(1)} KB · 点击可重新选择
                                </Text>
                            </div>
                        ) : (
                            <div className="flex flex-col gap-1">
                                <Text strong>点击选择，或拖拽文件到此处</Text>
                                <Text type="secondary" style={{ fontSize: 12 }}>支持 .xls / .xlsx，最大 10MB</Text>
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
                            icon={<Upload size={16} />}
                            loading={importing}
                            onClick={() => void handleImport()}
                            disabled={!importFile || !accountSetId}
                        >
                            {importing ? "导入中…" : "开始导入"}
                        </Button>
                        {importFile && !importing && <Button onClick={() => pickImportFile(null)}>清除</Button>}
                    </div>

                    {importError && <Alert type="error" showIcon message={importError} />}

                    {/* 导入结果 */}
                    {importResult && (
                        <Card title="导入结果" size="small">
                            <Row gutter={[16, 16]}>
                                <ImportStat label="解析行数" value={importResult.totalRows ?? 0} />
                                <ImportStat label="导入科目" value={importResult.importedAccounts ?? 0} />
                                <ImportStat label="跳过科目" value={importResult.skippedAccounts ?? 0} muted />
                            </Row>

                            {importResult.warnings && importResult.warnings.length > 0 && (
                                <div className="mt-4 flex flex-col gap-2">
                                    <Text type="secondary">告警（{importResult.warnings.length}）</Text>
                                    <ul
                                        className="max-h-64 overflow-auto rounded-md p-3 text-sm"
                                        style={{ border: `1px solid ${t.colorBorderSecondary}`, background: t.colorFillQuaternary }}
                                    >
                                        {importResult.warnings.map((w, i) => (
                                            <li key={i} className={cn("py-0.5")} style={{ color: t.colorTextSecondary }}>
                                                · {w}
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            )}
                        </Card>
                    )}
                </div>
            </Drawer>

            {/* 编码方案弹窗 */}
            <Modal
                title="科目编码方案"
                open={schemeModalOpen}
                onCancel={() => setSchemeModalOpen(false)}
                onOk={() => void saveScheme()}
                confirmLoading={schemeSaving}
                okText="保存"
                cancelText="取消"
                destroyOnClose
                width={640}
            >
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    <div>
                        <Text strong>科目级次</Text>
                        <div style={{ marginTop: 8 }}>
                            <Select
                                value={schemeLevels}
                                onChange={handleSchemeLevelsChange}
                                style={{ width: 120 }}
                                options={Array.from({ length: 7 }, (_, i) => ({ value: i + 4, label: `${i + 4} 级` }))}
                            />
                        </div>
                    </div>

                    <div>
                        <Text strong>各级位数</Text>
                        <div style={{ marginTop: 8, display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
                            {Array.from({ length: schemeLevels }, (_, i) => (
                                <div key={i} style={{ display: "flex", alignItems: "center", gap: 6 }}>
                                    {i > 0 && <Text type="secondary">-</Text>}
                                    <Select
                                        value={schemeDigits[i] ?? 2}
                                        onChange={(v) => handleSchemeDigitChange(i, v)}
                                        style={{ width: 56 }}
                                        options={[2, 3, 4].map((d) => ({ value: d, label: `${d}` }))}
                                    />
                                </div>
                            ))}
                        </div>
                    </div>

                    {/* 示例预览 */}
                    <Card size="small" styles={{ body: { padding: "12px 16px" } }}>
                        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                            <Text type="secondary" style={{ fontSize: 12 }}>编码方案：{schemeDigits.slice(0, schemeLevels).join("-")}</Text>
                            <Text code style={{ fontSize: 14, letterSpacing: 1 }}>{schemePreview}</Text>
                        </div>
                        <div style={{ marginTop: 6 }}>
                            <Text type="secondary" style={{ fontSize: 12 }}>
                                各级总位数：{schemeTotalDigits.join(" → ")}
                            </Text>
                        </div>
                    </Card>
                </Space>

                {schemeError && <Alert type="error" showIcon message={schemeError} style={{ marginTop: 12 }} />}
            </Modal>
        </Space>
    )
}

function ImportStat({ label, value, muted }: { label: string; value: number; muted?: boolean }) {
    return (
        <Col xs={12} sm={8} flex="auto">
            <Card size="small">
                <Statistic
                    title={label}
                    value={value}
                    valueStyle={muted ? { color: "rgba(0,0,0,0.45)" } : undefined}
                />
            </Card>
        </Col>
    )
}
