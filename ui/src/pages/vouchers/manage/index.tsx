import { useCallback, useEffect, useRef, useState } from "react"
import { CheckCircle2, ClipboardCheck, FileSpreadsheet, Pencil, Plus, RefreshCw, Trash2, Undo2, Upload } from "lucide-react"
import { Alert, Button, Card, Col, DatePicker, Drawer, Radio, Row, Space, Statistic, Table, Tag, Typography, theme } from "antd"
import dayjs, { type Dayjs } from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AccountsApi,
    VouchersApi,
    type AccountDto,
    type VoucherDetailDto,
    type VoucherEntryDto,
    type VoucherImportResult,
    type VoucherListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import VoucherForm from "./voucher-form"

const { Title, Text } = Typography
const { RangePicker } = DatePicker

/** 日期范围。 */
interface DateRange {
    startDate: string
    endDate: string
}

function formatAmount(n: number): string {
    return Math.abs(n).toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 将后端返回的日期（解析为 UTC 零点的 Date）格式化为 yyyy-MM-dd。 */
function formatDate(d?: Date): string {
    return d ? d.toISOString().slice(0, 10) : ""
}

function toDayjsArray(dr: DateRange | null): [Dayjs, Dayjs] | null {
    return dr ? [dayjs(dr.startDate), dayjs(dr.endDate)] : null
}

const ACCEPT = ".xls,.xlsx"

/** 单条分录的表格列（卡片内嵌用）。 */
const ENTRY_COLUMNS = [
    { title: "摘要", dataIndex: "summary", ellipsis: true },
    {
        title: "科目",
        key: "account",
        render: (_: unknown, r: VoucherEntryDto) => (
            <span>
                <Text style={{ fontSize: 12 }} type="secondary">{r.accountCode} </Text>
                {r.accountName}
            </span>
        ),
    },
    {
        title: "借方",
        dataIndex: "debit",
        align: "right" as const,
        width: 100,
        render: (v?: number) =>
            v != null && v !== 0 ? <Text style={{ color: v < 0 ? "#ff4d4f" : undefined }}>{formatAmount(v)}</Text> : null,
    },
    {
        title: "贷方",
        dataIndex: "credit",
        align: "right" as const,
        width: 100,
        render: (v?: number) =>
            v != null && v !== 0 ? <Text style={{ color: v < 0 ? "#ff4d4f" : undefined }}>{formatAmount(v)}</Text> : null,
    },
]

/** 凭证列表主表格列（接收操作回调）。 */
function getVoucherColumns(ops: {
    onEdit: (v: VoucherListItemDto) => void
    onDelete: (v: VoucherListItemDto) => void
    onReview: (v: VoucherListItemDto) => void
    onUnreview: (v: VoucherListItemDto) => void
    onPost: (v: VoucherListItemDto) => void
    openingId: string | null
    deletingId: string | null
    workflowId: string | null
}) {
    return [
        {
            title: "凭证号",
            dataIndex: "voucherNo",
            width: 140,
            render: (_: unknown, r: VoucherListItemDto) => (
                <Space size={4}>
                    <Text strong>{r.voucherNo}</Text>
                    <Tag color={r.isBalanced ? (r.status === 1 ? "blue" : r.status === 2 ? "green" : "purple") : "red"} style={{ fontSize: 11, lineHeight: "18px", padding: "0 4px" }}>
                        {r.statusText}
                    </Tag>
                </Space>
            ),
        },
        {
            title: "日期",
            dataIndex: "date",
            width: 110,
            render: (_: unknown, r: VoucherListItemDto) => <Text>{formatDate(r.date)}</Text>,
        },
        {
            title: "借方合计",
            dataIndex: "totalDebit",
            align: "right" as const,
            width: 120,
            render: (v?: number) => (
                <Text strong style={{ color: (v ?? 0) < 0 ? "#ff4d4f" : "#2adb5c" }}>
                    {formatAmount(v ?? 0)}
                </Text>
            ),
        },
        {
            title: "贷方合计",
            dataIndex: "totalCredit",
            align: "right" as const,
            width: 120,
            render: (v?: number) => (
                <Text strong style={{ color: (v ?? 0) < 0 ? "#ff4d4f" : "#2adb5c" }}>
                    {formatAmount(v ?? 0)}
                </Text>
            ),
        },
        {
            title: "制单人",
            dataIndex: "preparer",
            width: 90,
            render: (_: unknown, r: VoucherListItemDto) => (
                <Text type="secondary">{r.preparer ?? "—"}</Text>
            ),
        },
        {
            title: "操作",
            key: "actions",
            width: 130,
            render: (_: unknown, r: VoucherListItemDto) => (
                <Space size={0}>
                    {r.status === 1 && (
                        <Button
                            type="text"
                            size="small"
                            icon={<Pencil size={16} />}
                            loading={ops.openingId === r.id}
                            onClick={() => ops.onEdit(r)}
                            title="编辑凭证"
                        />
                    )}
                    {r.status === 1 && (
                        <Button
                            type="text"
                            size="small"
                            icon={<CheckCircle2 size={16} />}
                            loading={ops.workflowId === r.id}
                            onClick={() => ops.onReview(r)}
                            title="审核凭证"
                        />
                    )}
                    {r.status === 2 && (
                        <Button
                            type="text"
                            size="small"
                            icon={<Undo2 size={16} />}
                            loading={ops.workflowId === r.id}
                            onClick={() => ops.onUnreview(r)}
                            title="反审核（退回草稿）"
                        />
                    )}
                    {r.status === 2 && (
                        <Button
                            type="text"
                            size="small"
                            icon={<ClipboardCheck size={16} />}
                            loading={ops.workflowId === r.id}
                            onClick={() => ops.onPost(r)}
                            title="过账（登记入账锁定）"
                        />
                    )}
                    {r.status !== 3 && (
                        <Button
                            type="text"
                            size="small"
                            danger
                            icon={<Trash2 size={16} />}
                            loading={ops.deletingId === r.id}
                            onClick={() => ops.onDelete(r)}
                            title="删除凭证"
                        />
                    )}
                </Space>
            ),
        },
    ]
}

export default function VoucherManagePage() {
    const { token, user } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { token: t } = theme.useToken()

    const [vouchers, setVouchers] = useState<VoucherListItemDto[]>([])
    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [open, setOpen] = useState(false)
    const [editing, setEditing] = useState<VoucherDetailDto | null>(null)
    const [openingId, setOpeningId] = useState<string | null>(null)
    const [deletingId, setDeletingId] = useState<string | null>(null)
    const [workflowId, setWorkflowId] = useState<string | null>(null)
    const [dateRange, setDateRange] = useState<DateRange | null>(null)

    // ── 凭证导入相关状态 ──
    const importInputRef = useRef<HTMLInputElement>(null)
    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<File | null>(null)
    const [importDragging, setImportDragging] = useState(false)
    const [importing, setImporting] = useState(false)
    const [importConflict, setImportConflict] = useState<0 | 1>(0)
    const [importResult, setImportResult] = useState<VoucherImportResult | null>(null)
    const [importError, setImportError] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const config = apiConfig(token)
            const [list, accs] = await Promise.all([
                new VouchersApi(config).accountsetsAccountSetIdVouchersGet({
                    accountSetId,
                    startDate: dateRange?.startDate ? new Date(dateRange.startDate) : undefined,
                    endDate: dateRange?.endDate ? new Date(dateRange.endDate) : undefined,
                }),
                new AccountsApi(config).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true }),
            ])
            setVouchers(list)
            setAccounts(accs)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, dateRange])

    useEffect(() => {
        void load()
    }, [load])

    // 当 URL hash 为 #import 时自动打开导入抽屉并清除 hash
    useEffect(() => {
        if (window.location.hash === "#import") {
            setImportOpen(true)
            window.history.replaceState(null, "", window.location.pathname + window.location.search)
        }
    }, [])

    async function handleDelete(v: VoucherListItemDto) {
        if (deletingId || !v.id) return
        if (!window.confirm(`确定删除凭证「${v.voucherNo}」（${formatDate(v.date)}）吗？此操作不可撤销。`)) return
        setDeletingId(v.id)
        setError(null)
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdDelete({
                accountSetId,
                id: v.id,
            })
            setVouchers((prev) => prev.filter((x) => x.id !== v.id))
        } catch (err) {
            setError(await apiErrorMessage(err, "删除失败，请重试。"))
        } finally {
            setDeletingId(null)
        }
    }

    async function handleEdit(v: VoucherListItemDto) {
        if (openingId || !v.id) return
        setOpeningId(v.id)
        setError(null)
        try {
            const detail = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdGet({
                accountSetId,
                id: v.id,
            })
            setEditing(detail)
            setOpen(true)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载凭证详情失败，请重试。"))
        } finally {
            setOpeningId(null)
        }
    }

    function handleCreate() {
        setEditing(null)
        setOpen(true)
    }

    /** 凭证状态流转（审核/反审核/过账）：调用后刷新列表。 */
    async function handleWorkflow(v: VoucherListItemDto, action: "review" | "unreview" | "post") {
        if (workflowId || !v.id) return
        if (action === "post" && !window.confirm(`确定过账凭证「${v.voucherNo}」吗？过账后凭证将登记入账并锁定，不可再修改或删除。`)) return
        setWorkflowId(v.id)
        setError(null)
        try {
            const api = new VouchersApi(apiConfig(token))
            const params = { accountSetId, id: v.id }
            const updated =
                action === "review"
                    ? await api.accountsetsAccountSetIdVouchersIdReviewPost(params)
                    : action === "unreview"
                        ? await api.accountsetsAccountSetIdVouchersIdUnreviewPost(params)
                        : await api.accountsetsAccountSetIdVouchersIdPostPost(params)
            // 就地更新该张凭证的状态与审核人，避免整表重拉。
            setVouchers((prev) =>
                prev.map((x) =>
                    x.id === v.id
                        ? { ...x, status: updated.status, statusText: updated.statusText, reviewer: updated.reviewer }
                        : x,
                ),
            )
        } catch (err) {
            const fallback = action === "review" ? "审核失败，请重试。" : action === "unreview" ? "反审核失败，请重试。" : "过账失败，请重试。"
            setError(await apiErrorMessage(err, fallback))
        } finally {
            setWorkflowId(null)
        }
    }

    function handleSaved() {
        setOpen(false)
        setEditing(null)
        void load()
    }

    // ── 凭证导入处理 ──
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
            const res = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersImportPost({
                accountSetId,
                file: importFile,
                conflict: importConflict,
            })
            setImportResult(res)
            void load()
        } catch (err) {
            setImportError(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex flex-col gap-1">
                    <Title level={3} style={{ margin: 0 }}>凭证管理</Title>
                    <Text type="secondary">
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        {currentSet?.isCurrent ? "（当前）" : ""}
                        ，共 {vouchers.length} 张凭证。
                    </Text>
                </div>
                <Space>
                    <Button icon={<RefreshCw size={16} />} onClick={() => void load()} loading={loading} disabled={!accountSetId}>
                        刷新
                    </Button>
                    <Button icon={<Upload size={16} />} onClick={() => setImportOpen(true)} disabled={!accountSetId}>
                        导入凭证
                    </Button>
                    <Button type="primary" icon={<Plus size={16} />} onClick={handleCreate} disabled={!accountSetId}>
                        新增凭证
                    </Button>
                </Space>
            </div>

            <Space wrap>
                <span>日期范围：</span>
                <RangePicker
                    value={toDayjsArray(dateRange)}
                    onChange={(dates) => {
                        if (dates && dates[0] && dates[1]) {
                            setDateRange({
                                startDate: dates[0].format("YYYY-MM-DD"),
                                endDate: dates[1].format("YYYY-MM-DD"),
                            })
                        }
                    }}
                    disabled={!accountSetId}
                    allowClear
                />
            </Space>

            {error && <Alert type="error" showIcon message={error} />}

            {/* 凭证列表 */}
            <Table<VoucherListItemDto>
                rowKey="id"
                dataSource={vouchers}
                loading={loading}
                columns={getVoucherColumns({
                    onEdit: handleEdit,
                    onDelete: handleDelete,
                    onReview: (v) => void handleWorkflow(v, "review"),
                    onUnreview: (v) => void handleWorkflow(v, "unreview"),
                    onPost: (v) => void handleWorkflow(v, "post"),
                    openingId,
                    deletingId,
                    workflowId,
                })}
                expandable={{
                    defaultExpandAllRows: true,
                    expandIcon: () => null,
                    indentSize: 0,
                    expandedRowRender: (record) => (
                        <div className="pb-2">
                            {/* 分录明细 */}
                            {record.entries && record.entries.length > 0 && (
                                <Table<VoucherEntryDto>
                                    rowKey="id"
                                    columns={ENTRY_COLUMNS}
                                    dataSource={record.entries}
                                    pagination={false}
                                    size="small"
                                    showHeader={false}
                                    locale={{ emptyText: null }}
                                />
                            )}
                            {/* 合计行 */}
                            <div
                                className="flex items-center justify-between px-4 py-2 text-sm"
                                style={{ borderTop: `1px solid #f0f0f0`, background: "rgba(0,0,0,0.02)" }}
                            >
                                <Text type="secondary" style={{ fontSize: 12 }}>
                                    制单：{record.preparer ?? "—"}
                                    {record.reviewer ? ` · 审核：${record.reviewer}` : ""}
                                </Text>
                                <Space size={16}>
                                    <span>
                                        <Text type="secondary" style={{ fontSize: 12 }}>借方 </Text>
                                        <Text strong style={{ color: (record.totalDebit ?? 0) < 0 ? "#ff4d4f" : "#2adb5c" }}>{formatAmount(record.totalDebit ?? 0)}</Text>
                                    </span>
                                    <span>
                                        <Text type="secondary" style={{ fontSize: 12 }}>贷方 </Text>
                                        <Text strong style={{ color: (record.totalCredit ?? 0) < 0 ? "#ff4d4f" : "#2adb5c" }}>{formatAmount(record.totalCredit ?? 0)}</Text>
                                    </span>
                                </Space>
                            </div>
                        </div>
                    ),
                }}
                pagination={{
                    pageSize: 20,
                    showSizeChanger: true,
                    showTotal: (total) => `共 ${total} 张凭证`,
                }}
                size="middle"
                locale={{ emptyText: "暂无凭证，点击右上角「新增凭证」开始记账。" }}
            />

            {/* 新增 / 编辑凭证抽屉 */}
            <Drawer
                open={open}
                onClose={() => {
                    setOpen(false)
                    setEditing(null)
                }}
                width={1120}
                title={editing ? `编辑凭证 ${editing.voucherNo}` : "新增凭证"}
            >
                {open && (
                    <VoucherForm
                        key={editing?.id ?? "new"}
                        accountSetId={accountSetId}
                        accounts={accounts}
                        preparer={user?.displayName}
                        token={token ?? undefined}
                        initial={editing ?? undefined}
                        onSaved={handleSaved}
                        onCancel={() => {
                            setOpen(false)
                            setEditing(null)
                        }}
                    />
                )}
            </Drawer>

            {/* 导入凭证抽屉 */}
            <Drawer
                open={importOpen}
                onClose={() => {
                    setImportOpen(false)
                    pickImportFile(null)
                }}
                width={640}
                title="导入凭证"
            >
                <div className="flex flex-col gap-4">
                    <Text type="secondary">
                        支持导入其他平台导出的记账凭证文件（.xls / .xlsx）。系统按凭证号聚合分录、自动补建缺失科目，
                        借贷金额原样保留。
                    </Text>

                    <div className="flex flex-col gap-2">
                        <Text strong>导入到账套</Text>
                        <Text type="secondary">
                            {currentSet?.name ?? accountSetId}
                            {currentSet?.isCurrent ? "（当前）" : ""}
                        </Text>
                    </div>

                    {/* 冲突策略 */}
                    <div className="flex flex-col gap-2">
                        <Text strong>重复凭证处理</Text>
                        <Radio.Group
                            value={importConflict}
                            onChange={(e) => setImportConflict(e.target.value)}
                            optionType="button"
                            buttonStyle="solid"
                            size="small"
                        >
                            <Radio.Button value={0}>跳过</Radio.Button>
                            <Radio.Button value={1}>覆盖</Radio.Button>
                        </Radio.Group>
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
                            onClick={handleImport}
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
                                <ImportStat label="导入凭证" value={importResult.importedVouchers ?? 0} />
                                <ImportStat label="导入分录" value={importResult.importedEntries ?? 0} />
                                <ImportStat label="新建科目" value={importResult.createdAccounts ?? 0} />
                                <ImportStat label="覆盖凭证" value={importResult.overwrittenVouchers ?? 0} muted />
                                <ImportStat label="跳过凭证" value={importResult.skippedVouchers ?? 0} muted />
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
        </div>
    )
}

function ImportStat({ label, value, muted }: { label: string; value: number; muted?: boolean }) {
    return (
        <Col xs={12} sm={8} lg={muted ? 24 : 4} flex="auto">
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
