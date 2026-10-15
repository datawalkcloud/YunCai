import { useCallback, useEffect, useState } from "react"
import dayjs from "dayjs"
import { Alert, Button, Input, Modal, Select, Table } from "antd"
import type { TableColumnsType } from "antd"
import {
    DeleteOutlined,
    EditOutlined,
    FileTextOutlined,
    PlusOutlined,
    ReloadOutlined,
    SwapOutlined,
} from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    JournalsApi,
    TransfersApi,
    type JournalListItemDto,
    type SaveTransferRequest,
    type TransferDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtMoney, toISODate } from "@/lib/format"
import { useConfirm } from "@/hooks/useConfirm"
import { PageHeader, SectionCard } from "@/components/page/section"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"
import { AMOUNT_COL, ICON_STYLE } from "@/components/page/styles"
import { DateField } from "@/components/fields/date-fields"
import { NumberField } from "@/components/fields/number-field"

/** 新建/编辑转账表单值。 */
interface TransferFormValues {
    date?: Date
    summary: string
    fromJournalId: string
    toJournalId: string
    amount?: number
    remark: string
}

const emptyForm: TransferFormValues = {
    date: dayjs().toDate(),
    summary: "",
    fromJournalId: "",
    toJournalId: "",
    amount: undefined,
    remark: "",
}

/** 表单校验；提示文案与页面语义一致。 */
function validate(values: TransferFormValues): Partial<Record<keyof TransferFormValues, string>> {
    const errors: Partial<Record<keyof TransferFormValues, string>> = {}
    if (!values.date) errors.date = "请选择转账日期。"
    if (!values.summary.trim()) errors.summary = "请填写摘要。"
    if (!values.fromJournalId) errors.fromJournalId = "请选择转出账户。"
    if (!values.toJournalId) errors.toJournalId = "请选择转入账户。"
    if (values.fromJournalId && values.fromJournalId === values.toJournalId) {
        errors.toJournalId = "转出与转入账户不能相同。"
    }
    if (values.amount == null || values.amount <= 0) errors.amount = "请填写大于 0 的转账金额。"
    return errors
}

export default function TransferPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { confirm, confirmDialog } = useConfirm()

    const [transfers, setTransfers] = useState<TransferDto[]>([])
    const [journals, setJournals] = useState<JournalListItemDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    // 正在生成凭证的转账（行内按钮 loading）。
    const [voucherBusyId, setVoucherBusyId] = useState<string | null>(null)

    // ── 新建/编辑转账 ──
    const [modalOpen, setModalOpen] = useState(false)
    const [editing, setEditing] = useState<TransferDto | null>(null)
    const [saving, setSaving] = useState(false)
    const [formValues, setFormValues] = useState<TransferFormValues>(emptyForm)
    const [formErrors, setFormErrors] = useState<Partial<Record<keyof TransferFormValues, string>>>({})

    const loadTransfers = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const [list, journalList] = await Promise.all([
                new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersGet({ accountSetId }),
                new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId }),
            ])
            setTransfers(list)
            setJournals(journalList)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载内部转账失败，请重试。"))
            setTransfers([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void loadTransfers()
    }, [loadTransfers])

    // 停用的账户不能参与转账；仅启用账户出现在下拉。
    const enabledJournals = journals.filter((j) => j.isEnabled !== false)
    const journalOptions = enabledJournals.map((j) => ({
        value: j.id ?? "",
        label: `${j.name ?? ""}${j.type === 1 ? "（现金）" : "（银行）"}`,
    }))

    function setFormValue<K extends keyof TransferFormValues>(key: K, value: TransferFormValues[K]) {
        const next: TransferFormValues = { ...formValues, [key]: value }
        setFormValues(next)
        if (Object.keys(formErrors).length > 0) setFormErrors(validate(next))
    }

    function openCreate() {
        setEditing(null)
        setFormValues({ ...emptyForm, date: dayjs().toDate() })
        setFormErrors({})
        setModalOpen(true)
    }

    function openEdit(t: TransferDto) {
        setEditing(t)
        setFormValues({
            date: t.date ? dayjs(t.date).toDate() : undefined,
            summary: t.summary ?? "",
            fromJournalId: t.fromJournalId ?? "",
            toJournalId: t.toJournalId ?? "",
            amount: t.amount ?? undefined,
            remark: t.remark ?? "",
        })
        setFormErrors({})
        setModalOpen(true)
    }

    async function handleSubmit() {
        const nextErrors = validate(formValues)
        setFormErrors(nextErrors)
        if (Object.keys(nextErrors).length > 0) return
        if (!formValues.date) return

        setSaving(true)
        setError(null)
        try {
            const request: SaveTransferRequest = {
                // toJSON 按 toISOString 截取日期；先归一到 UTC 零点，避免本地时区回退一天。
                date: new Date(dayjs(formValues.date).format("YYYY-MM-DD")),
                summary: formValues.summary,
                fromJournalId: formValues.fromJournalId,
                toJournalId: formValues.toJournalId,
                amount: formValues.amount,
                remark: formValues.remark || null,
            }
            const api = new TransfersApi(apiConfig(token))
            if (editing?.id) {
                await api.accountsetsAccountSetIdTransfersIdPut({ accountSetId, id: editing.id, saveTransferRequest: request })
            } else {
                await api.accountsetsAccountSetIdTransfersPost({ accountSetId, saveTransferRequest: request })
            }
            setModalOpen(false)
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, editing ? "保存转账失败，请重试。" : "创建转账失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function handleGenerateVoucher(t: TransferDto) {
        if (!t.id) return
        setVoucherBusyId(t.id)
        setError(null)
        try {
            await new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersIdVoucherPost({ accountSetId, id: t.id })
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "生成转账凭证失败，请重试。"))
        } finally {
            setVoucherBusyId(null)
        }
    }

    async function handleDelete(t: TransferDto) {
        if (!t.id) return
        if (!(await confirm(`确定删除转账「${t.summary}」吗？两端账户中对应的转出 / 转入流水将一并删除。`))) return
        setError(null)
        try {
            await new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersIdDelete({ accountSetId, id: t.id })
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "删除转账失败，请重试。"))
        }
    }

    const columns: TableColumnsType<TransferDto> = [
        { title: "日期", className: "whitespace-nowrap", render: (_, t) => toISODate(t.date) },
        { title: "摘要", render: (_, t) => <div className="truncate">{t.summary}</div> },
        {
            title: "转出账户",
            className: "whitespace-nowrap",
            render: (_, t) => <span className="text-slate-600">{t.fromJournalName}</span>,
        },
        {
            title: "转入账户",
            className: "whitespace-nowrap",
            render: (_, t) => <span className="text-slate-600">{t.toJournalName}</span>,
        },
        {
            title: "金额",
            ...AMOUNT_COL,
            render: (_, t) => <span className="font-medium text-slate-700">{fmtMoney(t.amount ?? 0)}</span>,
        },
        { title: "备注", render: (_, t) => <div className="truncate text-slate-500">{t.remark ?? ""}</div> },
        {
            title: "凭证",
            align: "center",
            render: (_, t) =>
                t.voucherNo
                    ? <StatusPill tone="blue">{t.voucherNo}</StatusPill>
                    : <span className="text-slate-300">—</span>,
        },
        {
            title: "操作",
            align: "right",
            render: (_, t) => {
                const hasVoucher = Boolean(t.voucherId)
                const busy = voucherBusyId === t.id
                return (
                    <div className="flex items-center justify-end gap-0">
                        <Button
                            type="text"
                            size="small"
                            title={hasVoucher ? "已生成凭证，不能修改" : "编辑转账"}
                            disabled={hasVoucher}
                            icon={<EditOutlined />}
                            onClick={() => openEdit(t)}
                        />
                        <Button
                            type="text"
                            size="small"
                            title={hasVoucher ? "已生成凭证" : "生成转账凭证（借 转入科目 / 贷 转出科目）"}
                            disabled={hasVoucher || busy}
                            loading={busy}
                            icon={<FileTextOutlined />}
                            onClick={() => void handleGenerateVoucher(t)}
                        />
                        <Button
                            type="text"
                            size="small"
                            danger
                            title={hasVoucher ? "已生成凭证，不能删除" : "删除转账"}
                            disabled={hasVoucher}
                            icon={<DeleteOutlined />}
                            onClick={() => void handleDelete(t)}
                        />
                    </div>
                )
            },
        },
    ]

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={SwapOutlined}
                title="内部转账"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。资金账户之间的划转台账：创建时自动在两端登记转出 / 转入流水，可生成转账凭证。
                    </>
                }
                actions={
                    <>
                        <Button
                            icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                            disabled={!accountSetId || loading}
                            onClick={() => void loadTransfers()}
                        >
                            刷新
                        </Button>
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!accountSetId || enabledJournals.length < 2}>
                            新建转账
                        </Button>
                    </>
                }
            />

            {error && <Alert type="error" showIcon closable title={error} onClose={() => setError(null)} />}

            {transfers.length === 0 && !loading ? (
                <SectionCard bodyClassName="p-0">
                    <div className="flex flex-col items-center gap-3 py-12">
                        <SwapOutlined className="text-[34px]" style={ICON_STYLE.placeholder} />
                        <p className="text-[13px] text-slate-400">
                            {enabledJournals.length < 2
                                ? "至少需要两个启用的资金账户才能登记内部转账，请先到「资金流水」创建或启用账户。"
                                : "还没有内部转账记录。点击「新建转账」在资金账户之间划转。"}
                        </p>
                        {accountSetId && enabledJournals.length >= 2 && (
                            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                                新建转账
                            </Button>
                        )}
                    </div>
                </SectionCard>
            ) : (
                <SectionCard
                    title={<h3 className="text-[14px] font-semibold text-slate-800">转账记录</h3>}
                    bodyClassName="p-0"
                >
                    <Table<TransferDto>
                        columns={columns}
                        dataSource={transfers}
                        rowKey={(t) => t.id ?? t.summary ?? ""}
                        pagination={false}
                        size="small"
                        loading={loading && transfers.length === 0}
                        locale={{ emptyText: "暂无数据" }}
                    />
                </SectionCard>
            )}

            {/* 新建/编辑转账 */}
            <Modal
                open={modalOpen}
                title={editing ? "编辑转账" : "新建转账"}
                onCancel={() => setModalOpen(false)}
                onOk={() => void handleSubmit()}
                okText={editing ? "保存" : "创建"}
                cancelText="取消"
                confirmLoading={saving}
                width={560}
            >
                <p className="mb-4 text-[12.5px] text-slate-400">
                    保存后自动在两端账户登记流水（转出记支出、转入记收入，类别「内部转账」）。
                </p>
                <div className="flex flex-col gap-4">
                    <div className="grid grid-cols-2 gap-3">
                        <FormField label="日期" required error={formErrors.date}>
                            <DateField
                                value={formValues.date}
                                onChange={(v) => setFormValue("date", v)}
                                allowClear={false}
                                className="w-full"
                            />
                        </FormField>
                        <FormField label="金额" htmlFor="transfer-amount" required error={formErrors.amount}>
                            <NumberField
                                id="transfer-amount"
                                value={formValues.amount}
                                onChange={(v) => setFormValue("amount", v)}
                                precision={2}
                                min={0}
                                placeholder="0.00"
                                className="w-full"
                            />
                        </FormField>
                    </div>
                    <FormField label="摘要" htmlFor="transfer-summary" required error={formErrors.summary}>
                        <Input
                            id="transfer-summary"
                            value={formValues.summary}
                            onChange={(e) => setFormValue("summary", e.target.value)}
                            placeholder="如：提取现金、内部调拨"
                            maxLength={200}
                            status={formErrors.summary ? "error" : undefined}
                        />
                    </FormField>
                    <div className="grid grid-cols-2 gap-3">
                        <FormField label="转出账户" htmlFor="transfer-from" required error={formErrors.fromJournalId}>
                            <Select
                                id="transfer-from"
                                className="w-full"
                                placeholder="选择转出账户"
                                value={formValues.fromJournalId || undefined}
                                onChange={(v) => setFormValue("fromJournalId", v)}
                                options={journalOptions}
                                status={formErrors.fromJournalId ? "error" : undefined}
                            />
                        </FormField>
                        <FormField label="转入账户" htmlFor="transfer-to" required error={formErrors.toJournalId}>
                            <Select
                                id="transfer-to"
                                className="w-full"
                                placeholder="选择转入账户"
                                value={formValues.toJournalId || undefined}
                                onChange={(v) => setFormValue("toJournalId", v)}
                                options={journalOptions}
                                status={formErrors.toJournalId ? "error" : undefined}
                            />
                        </FormField>
                    </div>
                    <FormField label="备注（选填）" htmlFor="transfer-remark">
                        <Input
                            id="transfer-remark"
                            value={formValues.remark}
                            onChange={(e) => setFormValue("remark", e.target.value)}
                            placeholder="补充说明"
                            maxLength={200}
                        />
                    </FormField>
                </div>
            </Modal>

            {confirmDialog}
        </div>
    )
}
