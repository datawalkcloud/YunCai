import { useCallback, useEffect, useState } from "react"
import { createColumnHelper, tableFeatures, useTable } from "@tanstack/react-table"
import dayjs from "dayjs"
import { ArrowLeftRight, CircleAlert, FileText, Pencil, Plus, RefreshCw, Trash2, X } from "lucide-react"

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
import { StatusPill } from "@/components/page/status-pill"
import { ICON_BTN, ICON_BTN_DANGER, PAGE_BTN_GHOST, PAGE_BTN_PRIMARY } from "@/components/page/styles"
import { Alert, AlertAction, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { DateField } from "@/components/fields/date-fields"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { NumberField } from "@/components/fields/number-field"
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"

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

/** 各列对齐方式按列 id 取用。 */
const COLUMN_CLASS: Record<string, { head: string, cell: string }> = {
    date: { head: "text-left", cell: "whitespace-nowrap" },
    summary: { head: "text-left", cell: "w-full" },
    from: { head: "text-left", cell: "whitespace-nowrap" },
    to: { head: "text-left", cell: "whitespace-nowrap" },
    amount: { head: "text-right", cell: "text-right tabular-nums" },
    remark: { head: "text-left", cell: "" },
    voucher: { head: "text-center", cell: "text-center" },
    actions: { head: "text-right", cell: "text-right" },
}

const features = tableFeatures({})
const column = createColumnHelper<typeof features, TransferDto>()

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

    const columns = [
        column.display({
            id: "date",
            header: "日期",
            cell: ({ row }) => <span>{toISODate(row.original.date)}</span>,
        }),
        column.display({
            id: "summary",
            header: "摘要",
            cell: ({ row }) => <div className="truncate">{row.original.summary}</div>,
        }),
        column.display({
            id: "from",
            header: "转出账户",
            cell: ({ row }) => <span className="text-slate-600">{row.original.fromJournalName}</span>,
        }),
        column.display({
            id: "to",
            header: "转入账户",
            cell: ({ row }) => <span className="text-slate-600">{row.original.toJournalName}</span>,
        }),
        column.display({
            id: "amount",
            header: "金额",
            cell: ({ row }) => <span className="font-medium text-slate-700">{fmtMoney(row.original.amount ?? 0)}</span>,
        }),
        column.display({
            id: "remark",
            header: "备注",
            cell: ({ row }) => <div className="truncate text-slate-500">{row.original.remark ?? ""}</div>,
        }),
        column.display({
            id: "voucher",
            header: "凭证",
            cell: ({ row }) =>
                row.original.voucherNo
                    ? <StatusPill tone="blue">{row.original.voucherNo}</StatusPill>
                    : <span className="text-slate-300">—</span>,
        }),
        column.display({
            id: "actions",
            header: "操作",
            cell: ({ row }) => {
                const t = row.original
                const hasVoucher = Boolean(t.voucherId)
                return (
                    <div className="flex items-center justify-end gap-0">
                        <Button variant="ghost" size="icon-xs" title={hasVoucher ? "已生成凭证，不能修改" : "编辑转账"} disabled={hasVoucher} onClick={() => openEdit(t)} className={ICON_BTN}>
                            <Pencil />
                        </Button>
                        <Button
                            variant="ghost"
                            size="icon-xs"
                            title={hasVoucher ? "已生成凭证" : "生成转账凭证（借 转入科目 / 贷 转出科目）"}
                            disabled={hasVoucher || voucherBusyId === t.id}
                            onClick={() => void handleGenerateVoucher(t)}
                            className={ICON_BTN}
                        >
                            {voucherBusyId === t.id ? <Spinner className="size-3.5" /> : <FileText />}
                        </Button>
                        <Button variant="ghost" size="icon-xs" title={hasVoucher ? "已生成凭证，不能删除" : "删除转账"} disabled={hasVoucher} onClick={() => void handleDelete(t)} className={ICON_BTN_DANGER}>
                            <Trash2 />
                        </Button>
                    </div>
                )
            },
        }),
    ]

    const table = useTable({
        features,
        data: transfers,
        columns,
        getRowId: (r) => r.id ?? r.summary ?? "",
    })
    const headerGroups = table.getHeaderGroups()
    const rows = table.getRowModel().rows

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={ArrowLeftRight}
                title="内部转账"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。资金账户之间的划转台账：创建时自动在两端登记转出 / 转入流水，可生成转账凭证。
                    </>
                }
                actions={
                    <>
                        <Button variant="outline" className={PAGE_BTN_GHOST} onClick={() => void loadTransfers()} disabled={!accountSetId || loading}>
                            {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                            刷新
                        </Button>
                        <Button className={PAGE_BTN_PRIMARY} onClick={openCreate} disabled={!accountSetId || enabledJournals.length < 2}>
                            <Plus data-icon="inline-start" />
                            新建转账
                        </Button>
                    </>
                }
            />

            {error && (
                <Alert variant="destructive" className="rounded-2xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                    <CircleAlert />
                    <AlertTitle>{error}</AlertTitle>
                    <AlertAction>
                        <Button variant="ghost" size="icon-xs" title="关闭" onClick={() => setError(null)}>
                            <X />
                        </Button>
                    </AlertAction>
                </Alert>
            )}

            {transfers.length === 0 && !loading ? (
                <SectionCard bodyClassName="p-0">
                    <div className="flex flex-col items-center gap-3 py-12">
                        <ArrowLeftRight size={40} strokeWidth={1.2} className="text-slate-300" />
                        <p className="text-[13px] text-slate-400">
                            {enabledJournals.length < 2
                                ? "至少需要两个启用的资金账户才能登记内部转账，请先到「资金流水」创建或启用账户。"
                                : "还没有内部转账记录。点击「新建转账」在资金账户之间划转。"}
                        </p>
                        {accountSetId && enabledJournals.length >= 2 && (
                            <Button className={PAGE_BTN_PRIMARY} onClick={openCreate}>
                                <Plus data-icon="inline-start" />
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
                    <Table>
                        <TableHeader>
                            {headerGroups.map((hg) => (
                                <TableRow key={hg.id}>
                                    {hg.headers.map((h) => (
                                        <TableHead key={h.id} className={COLUMN_CLASS[h.column.id]?.head}>
                                            <table.FlexRender header={h} />
                                        </TableHead>
                                    ))}
                                </TableRow>
                            ))}
                        </TableHeader>
                        <TableBody>
                            {rows.map((row) => (
                                <TableRow key={row.id}>
                                    {row.getAllCells().map((cell) => (
                                        <TableCell key={cell.id} className={COLUMN_CLASS[cell.column.id]?.cell}>
                                            <table.FlexRender cell={cell} />
                                        </TableCell>
                                    ))}
                                </TableRow>
                            ))}
                            {rows.length === 0 && !loading && (
                                <TableRow>
                                    <TableCell colSpan={columns.length} className="py-8 text-center text-slate-400">
                                        暂无数据
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </SectionCard>
            )}

            {/* 新建/编辑转账 */}
            <Dialog open={modalOpen} onOpenChange={setModalOpen}>
                <DialogContent aria-describedby={undefined}>
                    <DialogHeader>
                        <DialogTitle>{editing ? "编辑转账" : "新建转账"}</DialogTitle>
                        <DialogDescription>
                            保存后自动在两端账户登记流水（转出记支出、转入记收入，类别「内部转账」）。
                        </DialogDescription>
                    </DialogHeader>
                    <div className="flex flex-col gap-4">
                        <div className="grid grid-cols-2 gap-3">
                            <Field data-invalid={Boolean(formErrors.date)}>
                                <FieldLabel htmlFor="transfer-date">日期</FieldLabel>
                                <DateField
                                    value={formValues.date}
                                    onChange={(v) => setFormValue("date", v)}
                                    allowClear={false}
                                    className="w-full"
                                />
                                <FieldError>{formErrors.date}</FieldError>
                            </Field>
                            <Field data-invalid={Boolean(formErrors.amount)}>
                                <FieldLabel htmlFor="transfer-amount">金额</FieldLabel>
                                <NumberField
                                    id="transfer-amount"
                                    value={formValues.amount}
                                    onChange={(v) => setFormValue("amount", v)}
                                    precision={2}
                                    min={0}
                                    placeholder="0.00"
                                />
                                <FieldError>{formErrors.amount}</FieldError>
                            </Field>
                        </div>
                        <Field data-invalid={Boolean(formErrors.summary)}>
                            <FieldLabel htmlFor="transfer-summary">摘要</FieldLabel>
                            <Input
                                id="transfer-summary"
                                value={formValues.summary}
                                onChange={(e) => setFormValue("summary", e.target.value)}
                                placeholder="如：提取现金、内部调拨"
                                maxLength={200}
                                aria-invalid={Boolean(formErrors.summary)}
                            />
                            <FieldError>{formErrors.summary}</FieldError>
                        </Field>
                        <div className="grid grid-cols-2 gap-3">
                            <Field data-invalid={Boolean(formErrors.fromJournalId)}>
                                <FieldLabel htmlFor="transfer-from">转出账户</FieldLabel>
                                <Select value={formValues.fromJournalId} onValueChange={(v) => setFormValue("fromJournalId", v)}>
                                    <SelectTrigger id="transfer-from" className="w-full" aria-invalid={Boolean(formErrors.fromJournalId)}>
                                        <SelectValue placeholder="选择转出账户" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectGroup>
                                            {enabledJournals.map((j) => (
                                                <SelectItem key={j.id} value={j.id ?? ""}>
                                                    {j.name}{j.type === 1 ? "（现金）" : "（银行）"}
                                                </SelectItem>
                                            ))}
                                        </SelectGroup>
                                    </SelectContent>
                                </Select>
                                <FieldError>{formErrors.fromJournalId}</FieldError>
                            </Field>
                            <Field data-invalid={Boolean(formErrors.toJournalId)}>
                                <FieldLabel htmlFor="transfer-to">转入账户</FieldLabel>
                                <Select value={formValues.toJournalId} onValueChange={(v) => setFormValue("toJournalId", v)}>
                                    <SelectTrigger id="transfer-to" className="w-full" aria-invalid={Boolean(formErrors.toJournalId)}>
                                        <SelectValue placeholder="选择转入账户" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectGroup>
                                            {enabledJournals.map((j) => (
                                                <SelectItem key={j.id} value={j.id ?? ""}>
                                                    {j.name}{j.type === 1 ? "（现金）" : "（银行）"}
                                                </SelectItem>
                                            ))}
                                        </SelectGroup>
                                    </SelectContent>
                                </Select>
                                <FieldError>{formErrors.toJournalId}</FieldError>
                            </Field>
                        </div>
                        <Field>
                            <FieldLabel htmlFor="transfer-remark">备注（选填）</FieldLabel>
                            <Input
                                id="transfer-remark"
                                value={formValues.remark}
                                onChange={(e) => setFormValue("remark", e.target.value)}
                                placeholder="补充说明"
                                maxLength={200}
                            />
                        </Field>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setModalOpen(false)}>取消</Button>
                        <Button onClick={() => void handleSubmit()} disabled={saving}>
                            {saving && <Spinner data-icon="inline-start" />}
                            {editing ? "保存" : "创建"}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {confirmDialog}
        </div>
    )
}
