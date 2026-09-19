import { useCallback, useEffect, useRef, useState } from "react"
import { Download, FileSpreadsheet, FileText, Pencil, Plus, RefreshCw, Trash2, Upload } from "lucide-react"
import { Alert, Button, Card, Col, DatePicker, Form, Input, InputNumber, Modal, Radio, Row, Select, Space, Statistic, Table, Tabs, Tag, Typography } from "antd"
import type { ColumnsType } from "antd/es/table"
import dayjs, { type Dayjs } from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AccountsApi,
    EntryCategoriesApi,
    JournalsApi,
    type AccountDto,
    type EntryCategoryDto,
    type JournalDetailDto,
    type JournalEntryDto,
    type JournalImportResult,
    type JournalListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

const { Title, Text } = Typography

const ACCEPT = ".xls,.xlsx"

function formatMoney(value: number): string {
    return value.toLocaleString("zh-CN", {
        style: "currency",
        currency: "CNY",
        minimumFractionDigits: 2,
    })
}

/** 金额为 0 时留空，避免表格被 0.00 填满。 */
function fmt(value: number): string {
    if (!value) return ""
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 将后端返回的日期（解析为 UTC 零点的 Date）格式化为 yyyy-MM-dd。 */
function formatDate(d?: Date): string {
    return d ? d.toISOString().slice(0, 10) : ""
}

/** 新建/编辑账户表单值。 */
interface AccountFormValues {
    name: string
    type: 1 | 2
    openingBalance: number
}

/** 新增/编辑流水表单值。 */
interface EntryFormValues {
    date: Dayjs
    summary: string
    category?: string
    counterparty?: string
    income?: number
    expense?: number
}

export default function CashJournalPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [journals, setJournals] = useState<JournalListItemDto[]>([])
    const [selectedId, setSelectedId] = useState<string | null>(null)
    const [detail, setDetail] = useState<JournalDetailDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [detailLoading, setDetailLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    // ── 账户（账簿）新建/编辑 ──
    const [accountModalOpen, setAccountModalOpen] = useState(false)
    const [editingJournal, setEditingJournal] = useState<JournalListItemDto | null>(null)
    const [accountSaving, setAccountSaving] = useState(false)
    const [accountForm] = Form.useForm<AccountFormValues>()

    // ── 流水新增/编辑 ──
    const [entryModalOpen, setEntryModalOpen] = useState(false)
    const [editingEntry, setEditingEntry] = useState<JournalEntryDto | null>(null)
    const [entrySaving, setEntrySaving] = useState(false)
    const [entryForm] = Form.useForm<EntryFormValues>()
    // 收支类别字典；记账时从中选择，按已填收入/支出方向联动过滤。
    const [categories, setCategories] = useState<EntryCategoryDto[]>([])
    const entryIncome = Form.useWatch("income", entryForm)
    const entryExpense = Form.useWatch("expense", entryForm)

    // ── 由流水生成凭证 ──
    const [voucherEntry, setVoucherEntry] = useState<JournalEntryDto | null>(null)
    const [counterAccounts, setCounterAccounts] = useState<AccountDto[]>([])
    const [voucherSaving, setVoucherSaving] = useState(false)
    const [voucherForm] = Form.useForm<{ counterAccountId: string }>()

    // ── 流水导入 ──
    const importInputRef = useRef<HTMLInputElement>(null)
    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<File | null>(null)
    const [importing, setImporting] = useState(false)
    const [importResult, setImportResult] = useState<JournalImportResult | null>(null)
    const [importError, setImportError] = useState<string | null>(null)

    /** 加载账户（账簿）列表；keepSelection 为 false 或当前选中项已不存在时自动选中第一个。 */
    const loadJournals = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const list = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId })
            setJournals(list)
            setSelectedId((prev) => (prev && list.some((j) => j.id === prev) ? prev : (list[0]?.id ?? null)))
        } catch (err) {
            setError(await apiErrorMessage(err, "加载日记账列表失败，请重试。"))
            setJournals([])
            setSelectedId(null)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    /** 加载当前选中账户的账簿详情（含全部流水）。 */
    const loadDetail = useCallback(async () => {
        if (!accountSetId || !selectedId) {
            setDetail(null)
            return
        }
        setDetailLoading(true)
        try {
            const res = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdGet({
                accountSetId,
                id: selectedId,
            })
            setDetail(res)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载日记账详情失败，请重试。"))
            setDetail(null)
        } finally {
            setDetailLoading(false)
        }
    }, [accountSetId, token, selectedId])

    useEffect(() => {
        void loadJournals()
    }, [loadJournals])

    useEffect(() => {
        void loadDetail()
    }, [loadDetail])

    // 加载收支类别字典（失败不阻断记账，仅无可选项）。
    useEffect(() => {
        if (!accountSetId) return
        void (async () => {
            try {
                const list = await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesGet({ accountSetId })
                setCategories(list)
            } catch {
                setCategories([])
            }
        })()
    }, [accountSetId, token])

    /** 账簿详情更新后同步列表项的合计数据，避免整表重拉。 */
    function applyDetail(updated: JournalDetailDto) {
        setDetail(updated)
        setJournals((prev) =>
            prev.map((j) =>
                j.id === updated.id
                    ? {
                        ...j,
                        name: updated.name,
                        openingBalance: updated.openingBalance,
                        totalIncome: updated.totalIncome,
                        totalExpense: updated.totalExpense,
                        closingBalance: updated.closingBalance,
                        entryCount: updated.entries?.length ?? 0,
                    }
                    : j,
            ),
        )
    }

    // ── 账户（账簿）操作 ──

    function openCreateAccount() {
        setEditingJournal(null)
        accountForm.setFieldsValue({ name: "", type: 2, openingBalance: 0 })
        setAccountModalOpen(true)
    }

    function openEditAccount(j: JournalListItemDto) {
        setEditingJournal(j)
        accountForm.setFieldsValue({
            name: j.name ?? "",
            type: (j.type as 1 | 2) ?? 2,
            openingBalance: j.openingBalance ?? 0,
        })
        setAccountModalOpen(true)
    }

    async function handleAccountSubmit() {
        const values = await accountForm.validateFields()
        setAccountSaving(true)
        setError(null)
        try {
            const api = new JournalsApi(apiConfig(token))
            if (editingJournal?.id) {
                const updated = await api.accountsetsAccountSetIdJournalsIdPut({
                    accountSetId,
                    id: editingJournal.id,
                    updateJournalRequest: { name: values.name, openingBalance: values.openingBalance },
                })
                applyDetail(updated)
                setJournals((prev) => prev.map((j) => (j.id === editingJournal.id ? { ...j, name: updated.name, openingBalance: updated.openingBalance, closingBalance: updated.closingBalance } : j)))
            } else {
                // 不传 accountId，由后端自动在 1001/1002 下创建同名明细科目并绑定。
                const created = await api.accountsetsAccountSetIdJournalsPost({
                    accountSetId,
                    createJournalRequest: { name: values.name, type: values.type, openingBalance: values.openingBalance },
                })
                await loadJournals()
                if (created.id) setSelectedId(created.id)
            }
            setAccountModalOpen(false)
        } catch (err) {
            setError(await apiErrorMessage(err, editingJournal ? "保存账户失败，请重试。" : "创建账户失败，请重试。"))
        } finally {
            setAccountSaving(false)
        }
    }

    async function handleDeleteAccount(j: JournalListItemDto) {
        if (!j.id) return
        if (!window.confirm(`确定删除账户「${j.name}」吗？其全部流水将一并删除，此操作不可撤销。`)) return
        setError(null)
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdDelete({ accountSetId, id: j.id })
            await loadJournals()
        } catch (err) {
            setError(await apiErrorMessage(err, "删除账户失败，请重试。"))
        }
    }

    // ── 流水操作 ──

    function openCreateEntry() {
        setEditingEntry(null)
        entryForm.setFieldsValue({
            date: dayjs(),
            summary: "",
            category: undefined,
            counterparty: undefined,
            income: undefined,
            expense: undefined,
        })
        setEntryModalOpen(true)
    }

    function openEditEntry(e: JournalEntryDto) {
        setEditingEntry(e)
        entryForm.setFieldsValue({
            date: dayjs(formatDate(e.date)),
            summary: e.summary ?? "",
            category: e.category ?? undefined,
            counterparty: e.counterparty ?? undefined,
            income: e.income || undefined,
            expense: e.expense || undefined,
        })
        setEntryModalOpen(true)
    }

    async function handleEntrySubmit() {
        if (!selectedId) return
        const values = await entryForm.validateFields()
        setEntrySaving(true)
        setError(null)
        try {
            const api = new JournalsApi(apiConfig(token))
            const request = {
                date: new Date(values.date.format("YYYY-MM-DD")),
                summary: values.summary,
                category: values.category || null,
                counterparty: values.counterparty || null,
                income: values.income ?? 0,
                expense: values.expense ?? 0,
            }
            const updated = editingEntry?.id
                ? await api.accountsetsAccountSetIdJournalsIdEntriesEntryIdPut({
                    accountSetId,
                    id: selectedId,
                    entryId: editingEntry.id,
                    journalEntryRequest: request,
                })
                : await api.accountsetsAccountSetIdJournalsIdEntriesPost({
                    accountSetId,
                    id: selectedId,
                    journalEntryRequest: request,
                })
            applyDetail(updated)
            setEntryModalOpen(false)
        } catch (err) {
            setError(await apiErrorMessage(err, editingEntry ? "保存流水失败，请重试。" : "登记流水失败，请重试。"))
        } finally {
            setEntrySaving(false)
        }
    }

    async function handleDeleteEntry(e: JournalEntryDto) {
        if (!selectedId || !e.id) return
        if (!window.confirm(`确定删除第 ${e.sequence} 笔流水「${e.summary}」吗？`)) return
        setError(null)
        try {
            const updated = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesEntryIdDelete({
                accountSetId,
                id: selectedId,
                entryId: e.id,
            })
            applyDetail(updated)
        } catch (err) {
            setError(await apiErrorMessage(err, "删除流水失败，请重试。"))
        }
    }

    // ── 由流水生成凭证 ──

    async function openVoucherModal(e: JournalEntryDto) {
        setVoucherEntry(e)
        voucherForm.resetFields()
        if (counterAccounts.length === 0) {
            try {
                const accs = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({
                    accountSetId,
                    leafOnly: true,
                })
                setCounterAccounts(accs)
            } catch (err) {
                setError(await apiErrorMessage(err, "加载科目列表失败，请重试。"))
            }
        }
    }

    async function handleGenerateVoucher() {
        if (!selectedId || !voucherEntry?.id) return
        const values = await voucherForm.validateFields()
        setVoucherSaving(true)
        setError(null)
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesEntryIdVoucherPost({
                accountSetId,
                id: selectedId,
                entryId: voucherEntry.id,
                generateVoucherRequest: { counterAccountId: values.counterAccountId },
            })
            setVoucherEntry(null)
            await loadDetail()
        } catch (err) {
            setError(await apiErrorMessage(err, "生成凭证失败，请重试。"))
        } finally {
            setVoucherSaving(false)
        }
    }

    // ── 导入 / 导出 ──

    /** 携带认证头下载后端文件（模板/导出），从 Content-Disposition 解析文件名。 */
    async function downloadFile(path: string, fallbackName: string) {
        setError(null)
        try {
            const res = await fetch(`/api${path}`, {
                headers: token ? { Authorization: `Bearer ${token}` } : undefined,
            })
            if (!res.ok) throw new Error("下载失败，请重试。")
            const blob = await res.blob()
            const disposition = res.headers.get("Content-Disposition") ?? ""
            const match = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
            const fileName = match ? decodeURIComponent(match[1]) : fallbackName
            const url = URL.createObjectURL(blob)
            const a = document.createElement("a")
            a.href = url
            a.download = fileName
            a.click()
            URL.revokeObjectURL(url)
        } catch (err) {
            setError(await apiErrorMessage(err, "下载失败，请重试。"))
        }
    }

    function pickImportFile(next: File | null) {
        setImportFile(next)
        setImportResult(null)
        setImportError(null)
    }

    async function handleImport() {
        if (!importFile || importing || !selectedId) return
        setImporting(true)
        setImportError(null)
        setImportResult(null)
        try {
            const res = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdImportPost({
                accountSetId,
                id: selectedId,
                file: importFile,
            })
            setImportResult(res)
            await loadDetail()
            await loadJournals()
        } catch (err) {
            setImportError(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    // ── 渲染 ──

    const selected = journals.find((j) => j.id === selectedId)

    // 记账可选类别：填了收入只列收入类别，填了支出只列支出类别，否则分组全列。
    const toCategoryOptions = (type: number) =>
        categories.filter((c) => c.type === type).map((c) => ({ value: c.name ?? "", label: c.name ?? "" }))
    const categoryOptions =
        (entryIncome ?? 0) > 0
            ? [{ label: "收入类别", options: toCategoryOptions(1) }]
            : (entryExpense ?? 0) > 0
                ? [{ label: "支出类别", options: toCategoryOptions(2) }]
                : [
                    { label: "收入类别", options: toCategoryOptions(1) },
                    { label: "支出类别", options: toCategoryOptions(2) },
                ]

    const summary = [
        { label: "期初余额", value: detail?.openingBalance ?? 0 },
        { label: "本期收入", value: detail?.totalIncome ?? 0 },
        { label: "本期支出", value: detail?.totalExpense ?? 0 },
        { label: "期末余额", value: detail?.closingBalance ?? 0 },
    ]

    const columns: ColumnsType<JournalEntryDto> = [
        { title: "序号", dataIndex: "sequence", align: "center", width: 64 },
        { title: "日期", dataIndex: "date", width: 110, render: (v: Date) => formatDate(v) },
        { title: "摘要", dataIndex: "summary", ellipsis: true },
        { title: "类别", dataIndex: "category", width: 110, render: (v?: string) => v ?? "" },
        { title: "往来", dataIndex: "counterparty", width: 130, render: (v?: string) => v ?? "" },
        { title: "收入", dataIndex: "income", align: "right", width: 120, render: (v: number) => fmt(v ?? 0) },
        { title: "支出", dataIndex: "expense", align: "right", width: 120, render: (v: number) => fmt(v ?? 0) },
        {
            title: "余额",
            dataIndex: "balance",
            align: "right",
            width: 140,
            render: (v: number) => <span style={{ fontWeight: 500 }}>{formatMoney(v ?? 0)}</span>,
        },
        {
            title: "凭证",
            dataIndex: "voucherNo",
            width: 90,
            render: (v?: string) => (v ? <Tag color="blue">{v}</Tag> : <Text type="secondary">—</Text>),
        },
        {
            title: "操作",
            key: "actions",
            width: 120,
            render: (_: unknown, r: JournalEntryDto) => (
                <Space size={0}>
                    <Button type="text" size="small" icon={<Pencil size={16} />} onClick={() => openEditEntry(r)} title="编辑流水" />
                    {!r.voucherId && (
                        <Button
                            type="text"
                            size="small"
                            icon={<FileText size={16} />}
                            onClick={() => void openVoucherModal(r)}
                            title="由此流水生成凭证"
                        />
                    )}
                    <Button type="text" size="small" danger icon={<Trash2 size={16} />} onClick={() => void handleDeleteEntry(r)} title="删除流水" />
                </Space>
            ),
        },
    ]

    return (
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                    <Title level={3} style={{ marginBottom: 4 }}>现金日记账</Title>
                    <Text type="secondary">
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。可创建多个资金账户（现金、各银行账户）分别登记序时流水，流水可一键生成记账凭证。
                    </Text>
                </div>
                <Space>
                    <Button icon={<RefreshCw size={16} />} onClick={() => { void loadJournals(); void loadDetail() }} loading={loading} disabled={!accountSetId}>
                        刷新
                    </Button>
                    <Button type="primary" icon={<Plus size={16} />} onClick={openCreateAccount} disabled={!accountSetId}>
                        新建账户
                    </Button>
                </Space>
            </div>

            {error && <Alert type="error" message={error} showIcon closable onClose={() => setError(null)} />}

            {journals.length === 0 && !loading ? (
                <Card>
                    <div className="flex flex-col items-center gap-3 py-10">
                        <FileSpreadsheet size={40} strokeWidth={1.2} />
                        <Text type="secondary">
                            {accountSetId ? "还没有资金账户。点击「新建账户」创建现金或银行账户，开始登记流水。" : "请先选择账套。"}
                        </Text>
                        {accountSetId && (
                            <Button type="primary" icon={<Plus size={16} />} onClick={openCreateAccount}>
                                新建账户
                            </Button>
                        )}
                    </div>
                </Card>
            ) : (
                <>
                    <Tabs
                        activeKey={selectedId ?? undefined}
                        onChange={(key) => setSelectedId(key)}
                        items={journals.map((j) => ({
                            key: j.id ?? "",
                            label: (
                                <Space size={6}>
                                    <span>{j.name}</span>
                                    <Tag style={{ marginInlineEnd: 0, fontSize: 11, lineHeight: "18px", padding: "0 4px" }} color={j.type === 1 ? "gold" : "geekblue"}>
                                        {j.type === 1 ? "现金" : "银行"}
                                    </Tag>
                                </Space>
                            ),
                        }))}
                    />

                    <Row gutter={[16, 16]}>
                        {summary.map((item) => (
                            <Col key={item.label} xs={12} lg={6}>
                                <Card>
                                    <Statistic title={item.label} value={item.value} precision={2} prefix="¥" />
                                </Card>
                            </Col>
                        ))}
                    </Row>

                    <Card
                        title={
                            <Space size={8}>
                                <span>{selected?.name ?? "明细流水"}</span>
                                {selected?.accountCode && (
                                    <Text type="secondary" style={{ fontSize: 12, fontWeight: 400 }}>
                                        对应科目：{selected.accountCode} {selected.accountName}
                                    </Text>
                                )}
                            </Space>
                        }
                        extra={
                            <Space wrap>
                                <Button size="small" icon={<Download size={14} />} onClick={() => void downloadFile(`/accountsets/${accountSetId}/Journals/import/template`, "日记账导入模板.xlsx")}>
                                    模板
                                </Button>
                                <Button size="small" icon={<Upload size={14} />} onClick={() => { setImportOpen(true); pickImportFile(null) }} disabled={!selectedId}>
                                    导入
                                </Button>
                                <Button size="small" icon={<Download size={14} />} onClick={() => selectedId && void downloadFile(`/accountsets/${accountSetId}/Journals/${selectedId}/export`, `${selected?.name ?? "日记账"}.xlsx`)} disabled={!selectedId}>
                                    导出
                                </Button>
                                <Button size="small" icon={<Pencil size={14} />} onClick={() => selected && openEditAccount(selected)} disabled={!selected}>
                                    账户设置
                                </Button>
                                <Button size="small" danger icon={<Trash2 size={14} />} onClick={() => selected && void handleDeleteAccount(selected)} disabled={!selected}>
                                    删除账户
                                </Button>
                                <Button size="small" type="primary" icon={<Plus size={14} />} onClick={openCreateEntry} disabled={!selectedId}>
                                    登记流水
                                </Button>
                            </Space>
                        }
                    >
                        <Table<JournalEntryDto>
                            rowKey={(r) => r.id ?? String(r.sequence)}
                            columns={columns}
                            dataSource={detail?.entries ?? []}
                            loading={detailLoading}
                            pagination={false}
                            size="small"
                            scroll={{ x: 1080 }}
                            locale={{ emptyText: "该账户暂无流水，点击「登记流水」或「导入」添加。" }}
                            summary={() =>
                                (detail?.entries?.length ?? 0) > 0 ? (
                                    <Table.Summary fixed>
                                        <Table.Summary.Row>
                                            <Table.Summary.Cell index={0} colSpan={5}>本期合计</Table.Summary.Cell>
                                            <Table.Summary.Cell index={5} align="right">{formatMoney(detail?.totalIncome ?? 0)}</Table.Summary.Cell>
                                            <Table.Summary.Cell index={6} align="right">{formatMoney(detail?.totalExpense ?? 0)}</Table.Summary.Cell>
                                            <Table.Summary.Cell index={7} align="right">{formatMoney(detail?.closingBalance ?? 0)}</Table.Summary.Cell>
                                            <Table.Summary.Cell index={8} colSpan={2} />
                                        </Table.Summary.Row>
                                    </Table.Summary>
                                ) : null
                            }
                        />
                    </Card>
                </>
            )}

            {/* 新建/编辑账户 */}
            <Modal
                title={editingJournal ? "账户设置" : "新建账户"}
                open={accountModalOpen}
                onOk={() => void handleAccountSubmit()}
                confirmLoading={accountSaving}
                onCancel={() => setAccountModalOpen(false)}
                okText={editingJournal ? "保存" : "创建"}
                destroyOnHidden
            >
                <Form form={accountForm} layout="vertical" requiredMark={false}>
                    <Form.Item
                        name="name"
                        label="账户名称"
                        rules={[{ required: true, whitespace: true, message: "请填写账户名称，如「工商银行」「现金」。" }]}
                    >
                        <Input placeholder="如：工商银行、农业银行、现金" maxLength={100} />
                    </Form.Item>
                    <Form.Item name="type" label="账户类型" rules={[{ required: true }]}>
                        <Radio.Group
                            disabled={!!editingJournal}
                            options={[
                                { label: "银行存款", value: 2 },
                                { label: "现金", value: 1 },
                            ]}
                        />
                    </Form.Item>
                    <Form.Item name="openingBalance" label="期初余额">
                        <InputNumber style={{ width: "100%" }} precision={2} min={0} placeholder="0.00" />
                    </Form.Item>
                    {!editingJournal && (
                        <Text type="secondary" style={{ fontSize: 12 }}>
                            创建后系统会自动在「库存现金 1001 / 银行存款 1002」下建立同名明细科目并与账户绑定。
                        </Text>
                    )}
                </Form>
            </Modal>

            {/* 登记/编辑流水 */}
            <Modal
                title={editingEntry ? "编辑流水" : "登记流水"}
                open={entryModalOpen}
                onOk={() => void handleEntrySubmit()}
                confirmLoading={entrySaving}
                onCancel={() => setEntryModalOpen(false)}
                okText="保存"
                destroyOnHidden
            >
                <Form form={entryForm} layout="vertical" requiredMark={false}>
                    <Form.Item name="date" label="日期" rules={[{ required: true, message: "请选择日期。" }]}>
                        <DatePicker style={{ width: "100%" }} allowClear={false} />
                    </Form.Item>
                    <Form.Item name="summary" label="摘要" rules={[{ required: true, whitespace: true, message: "请填写摘要。" }]}>
                        <Input placeholder="如：收到货款、支付办公费" maxLength={200} />
                    </Form.Item>
                    <Row gutter={12}>
                        <Col span={12}>
                            <Form.Item name="category" label="收支类别（选填）">
                                <Select
                                    showSearch
                                    allowClear
                                    placeholder={categories.length > 0 ? "选择收支类别" : "先在「收支类别」页新建类别"}
                                    optionFilterProp="label"
                                    options={categoryOptions}
                                />
                            </Form.Item>
                        </Col>
                        <Col span={12}>
                            <Form.Item name="counterparty" label="往来（选填）">
                                <Input placeholder="往来单位或个人" maxLength={100} />
                            </Form.Item>
                        </Col>
                    </Row>
                    <Row gutter={12}>
                        <Col span={12}>
                            <Form.Item
                                name="income"
                                label="收入"
                                dependencies={["expense"]}
                                rules={[
                                    ({ getFieldValue }) => ({
                                        validator(_, value) {
                                            const income = value ?? 0
                                            const expense = getFieldValue("expense") ?? 0
                                            if (income === 0 && expense === 0) return Promise.reject(new Error("收入与支出请填写其一。"))
                                            if (income > 0 && expense > 0) return Promise.reject(new Error("收入与支出只能填写其一。"))
                                            return Promise.resolve()
                                        },
                                    }),
                                ]}
                            >
                                <InputNumber style={{ width: "100%" }} precision={2} min={0} placeholder="0.00" />
                            </Form.Item>
                        </Col>
                        <Col span={12}>
                            <Form.Item name="expense" label="支出">
                                <InputNumber style={{ width: "100%" }} precision={2} min={0} placeholder="0.00" />
                            </Form.Item>
                        </Col>
                    </Row>
                </Form>
            </Modal>

            {/* 由流水生成凭证 */}
            <Modal
                title="由流水生成凭证"
                open={voucherEntry !== null}
                onOk={() => void handleGenerateVoucher()}
                confirmLoading={voucherSaving}
                onCancel={() => setVoucherEntry(null)}
                okText="生成凭证"
                destroyOnHidden
            >
                {voucherEntry && (
                    <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                        <Alert
                            type="info"
                            showIcon
                            message={
                                voucherEntry.income
                                    ? `收入 ${formatMoney(voucherEntry.income)}：借 ${selected?.accountName ?? "货币资金"} / 贷 对方科目`
                                    : `支出 ${formatMoney(voucherEntry.expense ?? 0)}：借 对方科目 / 贷 ${selected?.accountName ?? "货币资金"}`
                            }
                            description={`${formatDate(voucherEntry.date)} ${voucherEntry.summary}`}
                        />
                        <Form form={voucherForm} layout="vertical" requiredMark={false}>
                            <Form.Item name="counterAccountId" label="对方科目" rules={[{ required: true, message: "请选择对方科目。" }]}>
                                <Select
                                    showSearch
                                    placeholder="选择对方科目（末级科目）"
                                    optionFilterProp="label"
                                    options={counterAccounts
                                        .filter((a) => a.id !== detail?.accountId)
                                        .map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` }))}
                                />
                            </Form.Item>
                        </Form>
                    </Space>
                )}
            </Modal>

            {/* 导入流水 */}
            <Modal
                title={`导入流水${selected ? `：${selected.name}` : ""}`}
                open={importOpen}
                onCancel={() => setImportOpen(false)}
                footer={null}
                destroyOnHidden
            >
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    <Text type="secondary">
                        支持 .xls / .xlsx 文件，列格式与导入模板一致（日期、摘要必填）。非法行会跳过并提示，不影响其余行导入。
                    </Text>
                    <Space wrap>
                        <Button icon={<FileSpreadsheet size={16} />} onClick={() => importInputRef.current?.click()}>
                            {importFile ? importFile.name : "选择文件"}
                        </Button>
                        <input
                            ref={importInputRef}
                            type="file"
                            accept={ACCEPT}
                            className="hidden"
                            onChange={(e) => pickImportFile(e.target.files?.[0] ?? null)}
                        />
                        <Button type="primary" icon={<Upload size={16} />} loading={importing} onClick={() => void handleImport()} disabled={!importFile}>
                            {importing ? "导入中…" : "开始导入"}
                        </Button>
                        {importFile && !importing && <Button onClick={() => pickImportFile(null)}>清除</Button>}
                    </Space>
                    {importError && <Alert type="error" showIcon message={importError} />}
                    {importResult && (
                        <Alert
                            type={(importResult.skippedRows ?? 0) > 0 ? "warning" : "success"}
                            showIcon
                            message={`共 ${importResult.totalRows ?? 0} 行，成功导入 ${importResult.importedEntries ?? 0} 条，跳过 ${importResult.skippedRows ?? 0} 行。`}
                            description={
                                (importResult.warnings?.length ?? 0) > 0 ? (
                                    <ul style={{ margin: 0, paddingInlineStart: 18 }}>
                                        {importResult.warnings?.map((w, i) => <li key={i}>{w}</li>)}
                                    </ul>
                                ) : undefined
                            }
                        />
                    )}
                </Space>
            </Modal>
        </Space>
    )
}
