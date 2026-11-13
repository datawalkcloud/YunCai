import { useCallback, useEffect, useMemo, useState } from "react"
import type { ReactNode } from "react"
import { Alert, Button, Checkbox, Drawer, Steps, Tabs, Tag } from "antd"
import {
    CheckCircleOutlined,
    CloseCircleOutlined,
    InfoCircleOutlined,
    PrinterOutlined,
    ProfileOutlined,
    ReloadOutlined,
    SettingOutlined,
    ThunderboltOutlined,
    UndoOutlined,
} from "@ant-design/icons"
import { useNavigate } from "react-router"

import { useAuth } from "@/auth/AuthContext"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import {
    cancelCarry,
    carryBatch,
    carryForward,
    closeBatch,
    closePeriod,
    generatePreCloseVoucher,
    getClosingChecks,
    getClosingStatus,
    getClosingTemplates,
    getPreCloseList,
    previewCarryForward,
    renumberGaps,
    reopenPeriod,
    transferRetainedEarnings,
    type CarryForwardOptions,
    type CarryForwardPreviewDto,
    type CarryPreviewVoucherDto,
    type CarryVoucherRefDto,
    type ClosingCheckItemDto,
    type ClosingCheckResultDto,
    type ClosingPeriodDto,
    type ClosingStatusDto,
    type ClosingTemplateOptionDto,
    type PreCloseItemDto,
    type PreCloseListDto,
} from "@/api/closing"
import { cn } from "@/lib/utils"
import { useConfirm } from "@/hooks/useConfirm"
import { SectionCard } from "@/components/page/section"
import { ICON_STYLE } from "@/components/page/styles"
import { VoucherPaper } from "@/components/voucher/voucher-paper"
import { ClosingTemplatesSheet } from "./templates-sheet"
import { PrintVoucherSheet } from "./print-sheet"

/** 期间状态展示口径（与后端 ClosingPeriodDto.State 对应）。 */
const STATE_META: Record<string, { label: string; short: string; dot: string; card: string }> = {
    NotCarried: {
        label: "未结转损益、未结账",
        short: "待结转",
        dot: "bg-(--success)",
        card: "border-(--border) bg-(--success-soft)",
    },
    Carried: {
        label: "已结转损益、未结账",
        short: "待结账",
        dot: "bg-(--brand-fill)",
        card: "border-(--brand-border) bg-(--brand-soft)",
    },
    Closed: {
        label: "已结转损益、已结账",
        short: "已结账",
        dot: "bg-(--muted)",
        card: "border-(--border) bg-(--muted)",
    },
}

function stateMeta(state?: string) {
    return STATE_META[state ?? "NotCarried"] ?? STATE_META.NotCarried
}

/** "2026-01" → yyyyMM 整数；无效时返回 0。 */
function periodKey(text?: string | null): number {
    if (!text) return 0
    const [y, m] = text.split("-").map(Number)
    return y && m ? y * 100 + m : 0
}

function money(value: number): string {
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function dateText(value: string | Date): string {
    return String(value).slice(0, 10)
}

/** 按年份分组的期间卡片（期末处理与反结账共用）。 */
function groupByYear(periods: ClosingPeriodDto[]): [number, ClosingPeriodDto[]][] {
    const groups = new Map<number, ClosingPeriodDto[]>()
    for (const period of periods) {
        const list = groups.get(period.year) ?? []
        list.push(period)
        groups.set(period.year, list)
    }
    return [...groups.entries()].sort((a, b) => a[0] - b[0])
}

interface PeriodCardProps {
    period: ClosingPeriodDto
    active?: boolean
    onClick: () => void
}

function PeriodCard({ period, active, onClick }: PeriodCardProps) {
    const meta = stateMeta(period.state)
    return (
        <button
            type="button"
            onClick={onClick}
            className={cn(
                "flex w-[124px] flex-col items-start gap-1 rounded-xl border px-3 py-2.5 text-left transition-colors",
                meta.card,
                active ? "border-(--brand) ring-2 ring-(--brand-border)" : "hover:border-(--border)",
            )}
        >
            <span className="flex items-center gap-1.5 text-[15px] font-semibold text-(--foreground)">{period.month} 月</span>
            <span className="flex items-center gap-1.5 text-[11.5px] text-(--muted-foreground)">
                <span className={cn("size-1.5 rounded-full", meta.dot)} />
                {meta.short}
            </span>
            {!!period.unpostedCount && (
                <span className="text-[11px] font-medium text-(--danger)">{period.unpostedCount} 张未过账</span>
            )}
            {period.voucherCount > 0 && (
                <span className="text-[11px] text-(--muted-foreground)">{period.voucherCount} 张凭证</span>
            )}
        </button>
    )
}

function StateLegend() {
    return (
        <div className="flex flex-wrap items-center gap-3 text-[12px] text-(--muted-foreground)">
            {[
                { dot: "bg-(--success)", text: "未结转损益、未结账" },
                { dot: "bg-(--brand-fill)", text: "已结转损益、未结账" },
                { dot: "bg-(--muted)", text: "已结转损益、已结账" },
            ].map((item) => (
                <span key={item.text} className="inline-flex items-center gap-1.5">
                    <span className={cn("size-2 rounded-full", item.dot)} />
                    {item.text}
                </span>
            ))}
        </div>
    )
}

/** 检查项图标（ok / warn / error 三种状态）。 */
function CheckIcon({ status }: { status: string }) {
    if (status === "ok") return <CheckCircleOutlined className="text-[16px]" style={ICON_STYLE.ok} />
    if (status === "warn") return <InfoCircleOutlined className="text-[16px]" style={ICON_STYLE.warn} />
    return <CloseCircleOutlined className="text-[16px]" style={ICON_STYLE.danger} />
}

function CheckRow({
    item,
    onAction,
    extra,
}: {
    item: ClosingCheckItemDto
    onAction?: (actionKey: string) => void
    extra?: ReactNode
}) {
    return (
        <div className="flex items-start gap-2.5 px-4 py-2.5">
            <CheckIcon status={item.status} />
            <div className="min-w-0 flex-1">
                <div className="text-[13px] font-medium text-(--foreground)">{item.title}</div>
                <div className="text-[12.5px] text-(--muted-foreground)">{item.message}</div>
            </div>
            <div className="flex shrink-0 items-center gap-1">
                {item.actionKey && onAction && (
                    <Button size="small" type="link" className="pk-link-flush" onClick={() => onAction(item.actionKey as string)}>
                        {item.actionText ?? "点击查看"}
                    </Button>
                )}
                {extra}
            </div>
        </div>
    )
}

/** 结转损益预览：走只读凭证纸样，与凭证录入 / 打印共用同一套版式。 */
function CarryVoucherPreview({
    voucher,
    preparer,
    loading,
}: {
    voucher: CarryPreviewVoucherDto
    preparer?: string
    loading?: boolean
}) {
    return (
        <VoucherPaper
            word={voucher.word}
            number={voucher.number}
            date={dateText(voucher.date)}
            loading={loading}
            preparer={preparer}
            totalDebit={voucher.totalDebit}
            totalCredit={voucher.totalCredit}
            lines={voucher.lines.map((line, index) => ({
                key: `${voucher.label}-${index}`,
                summary: line.summary,
                accountCode: line.accountCode,
                accountName: line.accountName,
                debit: line.debit,
                credit: line.credit,
                balance: line.balance,
            }))}
        />
    )
}

interface WizardProps {
    accountSetId: string
    token?: string | null
    period: ClosingPeriodDto
    /** 进入时停留的步骤：0 期末检查 / 1 结转损益 / 2 结账。 */
    initialStep: 0 | 1 | 2
    /** 当前已结转至（如 "2026-02"）；撤销时连同其后期间一起回退。 */
    carriedThrough?: string | null
    onClose: () => void
    /** 数据发生变化（生成凭证 / 结转 / 结账）后通知页面刷新期间状态。 */
    onChanged: () => void
    /** 结账完成后携带提示文案关闭。 */
    onClosed: (message: string) => void
}

function ClosingWizard({ accountSetId, token, period, initialStep, carriedThrough, onClose, onChanged, onClosed }: WizardProps) {
    const navigate = useNavigate()
    const { user } = useAuth()
    const { confirm, confirmDialog } = useConfirm()

    const [step, setStep] = useState<0 | 1 | 2>(initialStep)
    const [error, setError] = useState<string | null>(null)
    const [notice, setNotice] = useState<string | null>(null)
    const [lastVouchers, setLastVouchers] = useState<CarryVoucherRefDto[]>([])
    const [busy, setBusy] = useState<string | null>(null)

    const [preClose, setPreClose] = useState<PreCloseListDto | null>(null)
    const [preCloseLoading, setPreCloseLoading] = useState(false)
    const [templatesOpen, setTemplatesOpen] = useState(false)
    const [printVoucherId, setPrintVoucherId] = useState<string | null>(null)

    const [templates, setTemplates] = useState<ClosingTemplateOptionDto[]>([])
    const [splitIncomeCost, setSplitIncomeCost] = useState(false)
    const [selectedTemplateIds, setSelectedTemplateIds] = useState<string[]>([])
    const [preview, setPreview] = useState<CarryForwardPreviewDto | null>(null)
    const [previewLoading, setPreviewLoading] = useState(false)

    const [checks, setChecks] = useState<ClosingCheckResultDto | null>(null)
    const [checksLoading, setChecksLoading] = useState(false)

    // 撤销结转后本期间的快照仍是 Carried，用本地状态驱动界面即时回到可结转。
    const [periodState, setPeriodState] = useState(period.state)
    const carried = periodState !== "NotCarried"
    const locked = periodState === "Closed"

    const loadPreClose = useCallback(async () => {
        setPreCloseLoading(true)
        setError(null)
        try {
            setPreClose(await getPreCloseList(accountSetId, period.year, period.month, token))
        } catch (err) {
            setPreClose(null)
            setError(err instanceof Error ? err.message : "期末检查加载失败，请重试。")
        } finally {
            setPreCloseLoading(false)
        }
    }, [accountSetId, period.year, period.month, token])

    const loadTemplates = useCallback(async () => {
        try {
            const list = await getClosingTemplates(accountSetId, token)
            setTemplates(list)
            setSelectedTemplateIds(list.filter((t) => t.isEnabled).map((t) => t.id))
        } catch {
            setTemplates([])
        }
    }, [accountSetId, token])

    useEffect(() => {
        void loadPreClose()
        void loadTemplates()
    }, [loadPreClose, loadTemplates])

    const loadPreview = useCallback(async () => {
        setPreviewLoading(true)
        setError(null)
        const options: CarryForwardOptions = {
            year: period.year,
            month: period.month,
            splitIncomeCost,
            // 年末的「本年利润 → 未分配利润」单独确认，不并入结转损益凭证。
            transferToRetainedEarnings: false,
            templateIds: selectedTemplateIds,
        }
        try {
            setPreview(await previewCarryForward(accountSetId, options, token))
        } catch (err) {
            setPreview(null)
            setError(err instanceof Error ? err.message : "结转预览加载失败，请重试。")
        } finally {
            setPreviewLoading(false)
        }
    }, [accountSetId, token, period.year, period.month, splitIncomeCost, selectedTemplateIds])

    // 进入第 2 步时按当前选项计算结转预览（选项变化即重算）。
    useEffect(() => {
        if (step === 1) void loadPreview()
    }, [step, loadPreview])

    const loadChecks = useCallback(async () => {
        setChecksLoading(true)
        setError(null)
        try {
            setChecks(await getClosingChecks(accountSetId, period.year, period.month, token))
        } catch (err) {
            setChecks(null)
            setError(err instanceof Error ? err.message : "期末检查失败，请重试。")
        } finally {
            setChecksLoading(false)
        }
    }, [accountSetId, period.year, period.month, token])

    useEffect(() => {
        if (step === 2) void loadChecks()
    }, [step, loadChecks])

    async function doGenerate(item: PreCloseItemDto) {
        if (busy || locked || carried) return
        setBusy(item.key)
        setError(null)
        setNotice(null)
        try {
            const result = await generatePreCloseVoucher(
                accountSetId,
                { year: period.year, month: period.month, key: item.key },
                token,
            )
            setNotice(result.message)
            setLastVouchers(result.voucher ? [result.voucher] : [])
            await loadPreClose()
            onChanged()
        } catch (err) {
            setError(err instanceof Error ? err.message : "生成凭证失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    async function doCarry() {
        if (busy || locked || !preview || preview.alreadyCarried) return
        const question = preview.needsCarry
            ? `确定对 ${period.year} 年 ${period.month} 月执行结转损益吗？结转凭证将自动过账。`
            : `本期无损益发生额，确定标记为「已结转损益」并进入结账吗？`
        if (!(await confirm(question))) {
            return
        }
        setBusy("carry")
        setError(null)
        setNotice(null)
        try {
            const result = await carryForward(
                accountSetId,
                {
                    year: period.year,
                    month: period.month,
                    splitIncomeCost,
                    transferToRetainedEarnings: false,
                    templateIds: selectedTemplateIds,
                },
                token,
            )
            setNotice(result.message)
            setLastVouchers(result.vouchers)
            setPeriodState("Carried")
            onChanged()
            if (period.month === 12) {
                // 年末：留在本步展示结转凭证，再把「本年利润 → 未分配利润」单独确认。
                await loadPreview()
            } else {
                setStep(2)
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : "结转失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    /** 撤销本期间及其后的结转，随后回到可结转状态重新生成凭证。 */
    async function doCancelCarry() {
        if (busy || locked || !canUndoCarry) return
        const latest = carriedThrough ?? period.periodText
        const range = latest === period.periodText ? period.periodText : `${period.periodText} 起至 ${latest}`
        if (!(await confirm(`确定撤销 ${range} 的结转吗？系统生成的结转凭证将被删除，可重新生成。`))) {
            return
        }
        setBusy("cancelCarry")
        setError(null)
        setNotice(null)
        try {
            const result = await cancelCarry(accountSetId, period.year, period.month, token)
            setPeriodState("NotCarried")
            setLastVouchers([])
            setNotice(result.message)
            await loadPreview()
            await loadPreClose()
            onChanged()
        } catch (err) {
            setError(err instanceof Error ? err.message : "撤销结转失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    async function doTransferRetained() {
        if (busy || locked || !preview) return
        const amount = money(Math.abs(preview.retainedProfitAmount))
        if (
            !(await confirm(
                `确定生成 ${period.year} 年的年末结转凭证吗？将把「本年利润」余额 ${amount} 元转入「未分配利润」。`,
            ))
        ) {
            return
        }
        setBusy("retained")
        setError(null)
        setNotice(null)
        try {
            const result = await transferRetainedEarnings(accountSetId, period.year, period.month, token)
            setNotice(result.message)
            setLastVouchers(result.voucher ? [result.voucher] : [])
            await loadPreview()
            onChanged()
        } catch (err) {
            setError(err instanceof Error ? err.message : "生成年末结转凭证失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    async function doClose() {
        if (busy || locked) return
        if (!(await confirm(`确定对 ${period.year} 年 ${period.month} 月执行结账吗？结账后该期间凭证将不可增删改。`))) {
            return
        }
        setBusy("close")
        setError(null)
        try {
            const status = await closePeriod(accountSetId, period.year, period.month, token)
            onClosed(`已结账至 ${status.closedPeriod ?? period.periodText}。`)
        } catch (err) {
            setError(err instanceof Error ? err.message : "结账失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    // 一键整理断号：只改凭证号、不动金额；确认后调用后端重排并刷新检查结果。
    async function doRenumber() {
        if (busy) return
        if (!(await confirm(`确定整理 ${period.year} 年 ${period.month} 月的断号凭证吗？仅调整凭证号，不影响金额与分录。`))) {
            return
        }
        setBusy("renumber")
        setError(null)
        setNotice(null)
        try {
            const result = await renumberGaps(accountSetId, period.year, period.month, token)
            setNotice(result.message)
            await loadChecks()
            onChanged()
        } catch (err) {
            setError(err instanceof Error ? err.message : "整理断号失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    function goAction(actionKey: string) {
        if (actionKey === "funds") navigate(`/${accountSetId}/funds/cash-journal`)
        else if (actionKey === "depreciation") navigate(`/${accountSetId}/assets/depreciation`)
        else if (actionKey === "numbering") {
            const prefix = `${period.year}-${String(period.month).padStart(2, "0")}`
            const lastDay = new Date(period.year, period.month, 0).getDate()
            navigate(`/${accountSetId}/vouchers/manage?periodFrom=${prefix}-01&periodTo=${prefix}-${lastDay}`)
        }
    }

    // 撤销会连同其后已结转期间一起回退；最新一次结转若是空期间则没有可删凭证（自动补标记），不提供入口。
    const carriedThroughKey = periodKey(carriedThrough)
    const canUndoCarry =
        carried &&
        !locked &&
        carriedThroughKey >= periodKey(period.periodText) &&
        (carriedThroughKey > periodKey(period.periodText) || (preview?.vouchers.length ?? 0) > 0)
    const blockingItems = (checks?.items ?? []).filter((item) => item.group !== "info")
    const infoItems = (checks?.items ?? []).filter((item) => item.group === "info")

    const voucherButtons = lastVouchers.length > 0 && (
        <div className="mt-2 flex flex-wrap items-center gap-2">
            {lastVouchers.map((voucher) => (
                <Button
                    key={voucher.id}
                    size="small"
                    icon={<PrinterOutlined />}
                    onClick={() => setPrintVoucherId(voucher.id)}
                >
                    {voucher.label}
                    {voucher.kind === "Template" ? "（模板）" : ""}
                </Button>
            ))}
        </div>
    )

    const footer = (
        <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-wrap gap-2">
                {step === 0 && (
                    <>
                        <Button icon={<SettingOutlined />} onClick={() => setTemplatesOpen(true)}>
                            自定义结转模板
                        </Button>
                        <Button icon={<ReloadOutlined />} disabled={preCloseLoading} onClick={() => void loadPreClose()}>
                            重新检查
                        </Button>
                    </>
                )}
                {step === 1 && (
                    <Button icon={<ReloadOutlined />} loading={previewLoading} onClick={() => void loadPreview()}>
                        重新检查
                    </Button>
                )}
                {step === 2 && (
                    <Button icon={<ReloadOutlined />} loading={checksLoading} onClick={() => void loadChecks()}>
                        重新检查
                    </Button>
                )}
            </div>
            <div className="flex flex-wrap gap-2">
                {step === 2 && <Button onClick={() => setStep(1)}>上一步</Button>}
                {step === 0 && (
                    <Button type="primary" onClick={() => setStep(1)}>
                        下一步
                    </Button>
                )}
                {step === 1 && !preview?.alreadyCarried && (
                    <>
                        <Button onClick={onClose}>取消</Button>
                        <Button
                            type="primary"
                            loading={busy === "carry"}
                            disabled={!!busy || locked || !preview}
                            onClick={() => void doCarry()}
                        >
                            {preview && !preview.needsCarry ? "标记已结转" : "保存凭证"}
                        </Button>
                    </>
                )}
                {step === 1 && preview?.alreadyCarried && (
                    <>
                        {canUndoCarry && (
                            <Button
                                danger
                                icon={<UndoOutlined />}
                                loading={busy === "cancelCarry"}
                                disabled={!!busy}
                                onClick={() => void doCancelCarry()}
                            >
                                撤销结转
                            </Button>
                        )}
                        <Button onClick={() => setStep(0)}>上一步</Button>
                        <Button type="primary" ghost onClick={() => setStep(2)}>
                            下一步
                        </Button>
                    </>
                )}
                {step === 2 && (
                    <Button
                        type="primary"
                        icon={<CheckCircleOutlined />}
                        loading={busy === "close"}
                        disabled={!!busy || locked || !checks?.canClose}
                        title={checks?.canClose ? "结账后该期间凭证不可增删改" : "需先完成结转且检查项无阻断"}
                        onClick={() => void doClose()}
                    >
                        结账
                    </Button>
                )}
            </div>
        </div>
    )

    return (
        <>
            <Drawer
                open
                size={980}
                title={`期末处理 · ${period.year} 年 ${period.month} 月`}
                onClose={onClose}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16, overflow: "auto" } }}
                footer={footer}
            >
                <Steps
                    size="small"
                    current={step}
                    items={[{ title: "期末检查" }, { title: "结转损益" }, { title: "结账" }]}
                />

                {locked && <Alert type="info" showIcon title={`${period.periodText} 已结账，仅供查看。`} />}
                {error && <Alert type="error" showIcon title={error} />}
                {notice && (
                    <Alert
                        type="success"
                        showIcon
                        title={
                            <div>
                                <div>{notice}</div>
                                {voucherButtons}
                            </div>
                        }
                    />
                )}

                {step === 0 && (
                    <div className="flex flex-col gap-3">
                        <Alert
                            type="warning"
                            showIcon
                            title="请按下列结转项逐项确认金额并生成凭证；本期无发生额的项目可直接进入下一步。"
                        />
                        <div className="flex flex-col divide-y divide-(--border) rounded-xl border border-(--border)">
                            {preClose?.items.map((item) => (
                                <div key={item.key} className="flex flex-wrap items-center gap-3 px-4 py-3">
                                    <div className="min-w-0 flex-1">
                                        <div className="flex flex-wrap items-center gap-2 text-[13px] font-medium text-(--foreground)">
                                            {item.title}
                                            {item.generated && <Tag color="green">已生成</Tag>}
                                            {item.source === "check" && <Tag color="green">已入账</Tag>}
                                            {item.source === "salary" && <Tag color="blue">工资模块</Tag>}
                                            {item.source === "template" && <Tag color="blue">模板</Tag>}
                                            {item.source === "unsupported" && <Tag>需模板/手工</Tag>}
                                        </div>
                                        <div className="mt-0.5 text-[12px] text-(--muted-foreground)">{item.hint}</div>
                                    </div>
                                    <div className="w-32 text-right text-[13px] font-semibold text-(--foreground)">
                                        ¥ {money(item.amount)}
                                    </div>
                                    <div className="w-28 text-right">
                                        {item.source === "salary" ? (
                                            <Button size="small" onClick={() => navigate(`/${accountSetId}/salary/manage`)}>
                                                去工资模块
                                            </Button>
                                        ) : item.generated ? (
                                            item.voucherId ? (
                                                <Button
                                                    size="small"
                                                    icon={<PrinterOutlined />}
                                                    onClick={() => setPrintVoucherId(item.voucherId as string)}
                                                >
                                                    {item.source === "check" ? "查看凭证" : item.voucherLabel}
                                                </Button>
                                            ) : (
                                                <span className="text-[12px] text-(--muted-foreground)">{item.voucherLabel ?? "已生成"}</span>
                                            )
                                        ) : item.available ? (
                                            <Button
                                                size="small"
                                                type="primary"
                                                loading={busy === item.key}
                                                disabled={!!busy || carried}
                                                onClick={() => void doGenerate(item)}
                                            >
                                                生成凭证
                                            </Button>
                                        ) : (
                                            <Button size="small" disabled>
                                                {item.source === "unsupported"
                                                    ? "需手工处理"
                                                    : item.source === "check"
                                                      ? "本期无发生额"
                                                      : "暂无金额"}
                                            </Button>
                                        )}
                                    </div>
                                </div>
                            ))}
                            {!preClose && (
                                <div className="flex justify-center py-8 text-[13px] text-(--muted-foreground)">
                                    {preCloseLoading ? "正在检查…" : "暂无结转项"}
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {step === 1 && (
                    <div className="flex flex-col gap-3">
                        {previewLoading && !preview && (
                            <div className="py-6 text-center text-[13px] text-(--muted-foreground)">正在计算结转金额…</div>
                        )}
                        {preview?.warnings.map((warning) => (
                            <Alert key={warning} type="warning" showIcon title={warning} />
                        ))}
                        {!previewLoading && preview?.alreadyCarried && (
                            <Alert
                                type="success"
                                showIcon
                                title={
                                    canUndoCarry
                                        ? `${period.periodText} 已完成结转损益。如需调整，可撤销结转后重新生成凭证。`
                                        : `${period.periodText} 已完成结转损益${
                                              (preview?.vouchers.length ?? 0) > 0 ? "，结转凭证仅供查看。" : "（本期无损益发生额）"
                                          }`
                                }
                            />
                        )}
                        {!previewLoading && preview?.alreadyCarried && period.month === 12 && !locked && (
                            preview.hasRetainedVoucher ? (
                                <Alert type="info" showIcon title="已生成年末结转未分配利润凭证。" />
                            ) : preview.retainedProfitAmount !== 0 ? (
                                <div className="flex flex-col gap-3">
                                    <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-(--border) p-3">
                                        <div className="text-[13px] text-(--foreground)">
                                            年末结转未分配利润：
                                            <span className="font-semibold text-(--foreground)">
                                                {preview.retainedProfitAmount > 0 ? "净利润" : "净亏损"} ¥{" "}
                                                {money(Math.abs(preview.retainedProfitAmount))}
                                            </span>
                                            <span className="ml-1 text-[12px] text-(--muted-foreground)">（本年利润余额转入未分配利润）</span>
                                        </div>
                                        <Button
                                            type="primary"
                                            loading={busy === "retained"}
                                            disabled={!!busy}
                                            onClick={() => void doTransferRetained()}
                                        >
                                            生成年末结转凭证
                                        </Button>
                                    </div>
                                    {preview.retainedVoucher && (
                                        <CarryVoucherPreview
                                            voucher={preview.retainedVoucher}
                                            preparer={user?.displayName}
                                            loading={previewLoading}
                                        />
                                    )}
                                </div>
                            ) : null
                        )}
                        {!previewLoading && preview && !preview.alreadyCarried && !preview.needsCarry && (
                            <div className="flex flex-col items-center gap-1.5 py-12">
                                <span className="text-[15px] font-medium text-(--foreground)">亲！本期不需要结转损益。</span>
                                <span className="text-[12px] text-(--muted-foreground)">本期没有损益类余额，标记已结转后即可结账。</span>
                            </div>
                        )}
                        {preview && (preview.needsCarry || preview.vouchers.length > 0) && (
                            <>
                                {!preview.alreadyCarried && (
                                    <div className="flex justify-end">
                                        <Checkbox
                                            checked={splitIncomeCost}
                                            onChange={(e) => setSplitIncomeCost(e.target.checked)}
                                        >
                                            收入和成本分开结转损益
                                        </Checkbox>
                                    </div>
                                )}
                                {preview.vouchers.length > 0 && (
                                    <div className="flex flex-wrap items-center gap-x-6 gap-y-1 rounded-xl bg-(--table-zebra-bg) px-4 py-2.5 text-[13px] text-(--muted-foreground)">
                                        <span>
                                            本次结转{" "}
                                            <span className="font-medium text-(--foreground)">{preview.vouchers.length}</span> 张凭证
                                        </span>
                                        <span>
                                            借方合计{" "}
                                            <span className="font-semibold tabular-nums text-(--foreground)">
                                                ¥{money(preview.vouchers.reduce((sum, voucher) => sum + (voucher.totalDebit ?? 0), 0))}
                                            </span>
                                        </span>
                                        {preview.alreadyCarried && <span className="text-[12px] text-(--success)">已结转，仅查看</span>}
                                    </div>
                                )}
                                {preview.vouchers
                                    // 12 月已结转后本步只确认年末凭证，已生成的结转损益凭证不再重复展示。
                                    .filter(
                                        (voucher) =>
                                            !(preview.alreadyCarried && period.month === 12)
                                            || voucher.kind === "Retained",
                                    )
                                    .map((voucher) => (
                                        <CarryVoucherPreview
                                            key={voucher.label}
                                            voucher={voucher}
                                            preparer={user?.displayName}
                                            loading={previewLoading}
                                        />
                                    ))}
                                {!preview.alreadyCarried && templates.length > 0 && (
                                    <div className="flex flex-col gap-1.5 rounded-xl border border-(--border) p-3">
                                        <div className="text-[12.5px] font-medium text-(--foreground)">结转模板</div>
                                        {templates.map((template) => (
                                            <Checkbox
                                                key={template.id}
                                                checked={selectedTemplateIds.includes(template.id)}
                                                onChange={(e) =>
                                                    setSelectedTemplateIds((ids) =>
                                                        e.target.checked
                                                            ? [...ids, template.id]
                                                            : ids.filter((id) => id !== template.id),
                                                    )
                                                }
                                            >
                                                {template.name}
                                                {!template.isEnabled && (
                                                    <span className="ml-1 text-[11.5px] text-(--muted-foreground)">（已停用）</span>
                                                )}
                                            </Checkbox>
                                        ))}
                                    </div>
                                )}
                            </>
                        )}
                        {!previewLoading && !preview && !error && (
                            <div className="flex justify-center py-8 text-[13px] text-(--muted-foreground)">暂无结转数据</div>
                        )}
                    </div>
                )}

                {step === 2 && (
                    <div className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,1.35fr)_minmax(0,1fr)]">
                        <SectionCard title={<span className="text-[13.5px] font-semibold text-(--foreground)">影响结账</span>}>
                            <div className="-mx-4 divide-y divide-(--border)">
                                {blockingItems.map((item) => (
                                    <CheckRow
                                        key={item.key}
                                        item={item}
                                        onAction={goAction}
                                        extra={
                                            item.key === "numbering" && item.status === "warn" ? (
                                                <Button
                                                    size="small"
                                                    type="link"
                                                    className="pk-link-flush"
                                                    loading={busy === "renumber"}
                                                    disabled={!!busy || locked}
                                                    onClick={() => void doRenumber()}
                                                >
                                                    一键整理断号
                                                </Button>
                                            ) : undefined
                                        }
                                    />
                                ))}
                                {checks && blockingItems.length === 0 && (
                                    <div className="flex items-center gap-2 px-4 py-4 text-[13px] text-(--success)">
                                        <CheckCircleOutlined className="text-(--success)" />
                                        全部检查通过，可以结账。
                                    </div>
                                )}
                                {!checks && (
                                    <div className="flex justify-center py-8 text-[13px] text-(--muted-foreground)">
                                        {checksLoading ? "正在检查…" : "暂无检查结果"}
                                    </div>
                                )}
                            </div>
                        </SectionCard>
                        <SectionCard title={<span className="text-[13.5px] font-semibold text-(--foreground)">仅提示</span>}>
                            <div className="-mx-4 divide-y divide-(--border)">
                                {infoItems.map((item) => (
                                    <CheckRow key={item.key} item={item} onAction={goAction} />
                                ))}
                                {checks && infoItems.length === 0 && (
                                    <div className="flex justify-center py-6 text-[13px] text-(--muted-foreground)">暂无提示</div>
                                )}
                            </div>
                        </SectionCard>
                    </div>
                )}
            </Drawer>

            <ClosingTemplatesSheet
                accountSetId={accountSetId}
                token={token}
                open={templatesOpen}
                onOpenChange={setTemplatesOpen}
            />
            <PrintVoucherSheet
                accountSetId={accountSetId}
                token={token}
                voucherId={printVoucherId}
                open={!!printVoucherId}
                onOpenChange={(open) => {
                    if (!open) setPrintVoucherId(null)
                }}
            />
            {confirmDialog}
        </>
    )
}

export default function ClosingPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { confirm, confirmDialog } = useConfirm()

    const [status, setStatus] = useState<ClosingStatusDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [busy, setBusy] = useState<string | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [notice, setNotice] = useState<string | null>(null)
    const [tab, setTab] = useState<"process" | "reopen">("process")
    const [templatesOpen, setTemplatesOpen] = useState(false)
    const [wizard, setWizard] = useState<{ period: ClosingPeriodDto; initialStep: 0 | 1 | 2 } | null>(null)

    const loadStatus = useCallback(
        async (keepNotice = false) => {
            if (!accountSetId) return null
            setLoading(true)
            setError(null)
            if (!keepNotice) setNotice(null)
            try {
                const next = await getClosingStatus(accountSetId, token)
                setStatus(next)
                return next
            } catch (err) {
                setError(err instanceof Error ? err.message : "加载期末结转状态失败，请重试。")
                return null
            } finally {
                setLoading(false)
            }
        },
        [accountSetId, token],
    )

    useEffect(() => {
        void loadStatus()
    }, [loadStatus])

    function openWizard(period: ClosingPeriodDto) {
        const initialStep: 0 | 1 | 2 =
            period.state === "NotCarried" ? 0 : period.state === "Carried" ? 1 : 2
        setWizard({ period, initialStep })
    }

    /** 批量结账：从下一个待结期间起连续结账至最新已结转期间。 */
    async function doCloseBatch() {
        if (busy) return
        const carried = status?.carriedPeriod ?? "—"
        const closed = status?.closedPeriod ?? "未结账"
        if (
            !(await confirm(
                `确定批量结账吗？将从下一个待结期间连续结账至最新已结转期间（已结转至 ${carried}，当前已结账至 ${closed}）。`,
            ))
        ) {
            return
        }
        setBusy("batch")
        setError(null)
        setNotice(null)
        try {
            const result = await closeBatch(accountSetId, token)
            setNotice(result.message)
            setStatus(result.status)
        } catch (err) {
            setError(err instanceof Error ? err.message : "批量结账失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    /** 批量标记结转：把损益已为 0 的连续期间（通常是导入的外部结转凭证）一次标记为已结转。 */
    async function doCarryBatch() {
        if (busy) return
        if (
            !(await confirm(
                "将把损益余额已为 0 的连续期间批量标记为已结转（不生成凭证）；遇到仍有损益余额或未过账凭证的期间会停下。继续吗？",
            ))
        ) {
            return
        }
        setBusy("carryBatch")
        setError(null)
        setNotice(null)
        try {
            const result = await carryBatch(accountSetId, token)
            setNotice(result.message)
            setStatus(result.status)
        } catch (err) {
            setError(err instanceof Error ? err.message : "批量标记结转失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    /** 反结账：从指定已结期间起整体退回（该期间及其后的已结期间全部恢复可修改）。 */
    async function doReopen(period: ClosingPeriodDto) {
        if (busy) return
        const latest = status?.closedPeriod ?? period.periodText
        const range =
            latest === period.periodText
                ? `${period.periodText}`
                : `${period.periodText} 起的已结账期间（至 ${latest}）`
        if (!(await confirm(`确定从 ${period.year} 年 ${period.month} 月反结账吗？${range}将恢复为可修改状态。`))) {
            return
        }
        setBusy(`reopen-${period.periodText}`)
        setError(null)
        setNotice(null)
        try {
            const next = await reopenPeriod(accountSetId, period.year, period.month, token)
            setStatus(next)
            setNotice(`已反结账，当前结账进度：${next.closedPeriod ?? "未结账"}。`)
        } catch (err) {
            setError(err instanceof Error ? err.message : "反结账失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    /** 撤销最近一次结转：删除系统生成的结转凭证。 */
    async function doCancelCarry() {
        if (busy || !status?.canCancelCarry || !status.carriedPeriod) return
        const [year, month] = status.carriedPeriod.split("-").map(Number)
        if (!(await confirm(`确定撤销 ${status.carriedPeriod} 的结转损益吗？系统生成的结转凭证将被删除，结转进度退回。`))) {
            return
        }
        setBusy("cancel")
        setError(null)
        setNotice(null)
        try {
            const result = await cancelCarry(accountSetId, year, month, token)
            setNotice(result.message)
            await loadStatus(true)
        } catch (err) {
            setError(err instanceof Error ? err.message : "撤销结转失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    const processGroups = useMemo(() => groupByYear(status?.periods ?? []), [status])
    const closedGroups = useMemo(
        () => groupByYear((status?.periods ?? []).filter((period) => period.state === "Closed")),
        [status],
    )
    const canBatch = periodKey(status?.carriedPeriod) > periodKey(status?.closedPeriod)
    const latestVoucherKey = Math.max(
        0,
        ...(status?.periods ?? [])
            .filter((period) => period.voucherCount > 0)
            .map((period) => period.year * 100 + period.month),
    )
    const canCarryBatch =
        !!status && status.nextCarryYear * 100 + status.nextCarryMonth <= latestVoucherKey

    return (
        <div className="flex w-full flex-col gap-5">
            <Tabs
                activeKey={tab}
                onChange={(key) => setTab(key as "process" | "reopen")}
                items={[
                    { key: "process", label: "期末处理" },
                    { key: "reopen", label: "反结账" },
                ]}
                tabBarExtraContent={
                    <div className="flex flex-wrap items-center gap-2">
                        <Button icon={<SettingOutlined />} onClick={() => setTemplatesOpen(true)}>
                            结转模板
                        </Button>
                        {tab === "process" && (
                            <>
                                <Button
                                    icon={<ThunderboltOutlined />}
                                    loading={busy === "carryBatch"}
                                    disabled={loading || !canCarryBatch || !!busy}
                                    title="把损益已为 0 的连续期间批量标记为已结转"
                                    onClick={() => void doCarryBatch()}
                                >
                                    批量标记已结转
                                </Button>
                                <Button
                                    icon={busy === "batch" ? <ProfileOutlined spin /> : <ProfileOutlined />}
                                    disabled={loading || !canBatch || !!busy}
                                    title="从下一个待结期间连续结账至最新已结转期间"
                                    onClick={() => void doCloseBatch()}
                                >
                                    批量结账
                                </Button>
                            </>
                        )}
                        <Button
                            icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                            disabled={loading}
                            onClick={() => void loadStatus(true)}
                        >
                            刷新
                        </Button>
                    </div>
                }
            />

            {error && <Alert type="error" showIcon title={error} />}
            {notice && <Alert type="success" showIcon title={notice} />}

            {tab === "process" ? (
                <div className="flex flex-col gap-5">
                    <SectionCard
                        title={<h3 className="text-[14px] font-semibold text-(--foreground)">期间状态</h3>}
                        action={<StateLegend />}
                    >
                        {processGroups.length === 0 ? (
                            <div className="flex justify-center py-8 text-[13px] text-(--muted-foreground)">
                                {loading ? "正在加载…" : "暂无期间数据"}
                            </div>
                        ) : (
                            <div className="flex flex-col gap-4">
                                {processGroups.map(([year, yearPeriods]) => (
                                    <div key={year} className="flex flex-col gap-2">
                                        <div className="text-[15px] font-bold text-(--foreground)">{year}</div>
                                        <div className="flex flex-wrap gap-2.5">
                                            {yearPeriods.map((period) => (
                                                <PeriodCard
                                                    key={period.periodText}
                                                    period={period}
                                                    active={
                                                        wizard?.period.year === period.year &&
                                                        wizard?.period.month === period.month
                                                    }
                                                    onClick={() => openWizard(period)}
                                                />
                                            ))}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </SectionCard>

                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                        {[
                            { label: "启用期间", value: status?.startPeriod ?? "—" },
                            { label: "已结转损益至", value: status?.carriedPeriod ?? "未结转" },
                            { label: "已结账至", value: status?.closedPeriod ?? "未结账" },
                        ].map((item) => (
                            <div key={item.label} className="rounded-xl border border-(--border) bg-(--table-zebra-bg) px-4 py-3">
                                <div className="text-[12px] text-(--muted-foreground)">{item.label}</div>
                                <div className="mt-1 text-[15px] font-semibold text-(--foreground)">{item.value}</div>
                            </div>
                        ))}
                    </div>
                </div>
            ) : (
                <SectionCard
                    title={<h3 className="text-[14px] font-semibold text-(--foreground)">反结账</h3>}
                    action={
                        <Button
                            icon={<UndoOutlined />}
                            loading={busy === "cancel"}
                            disabled={!status?.canCancelCarry || !!busy}
                            title={status?.canCancelCarry ? `撤销结转（${status?.carriedPeriod}）` : "最近一次结转已被结账覆盖"}
                            onClick={() => void doCancelCarry()}
                        >
                            撤销结转
                        </Button>
                    }
                >
                    <p className="mb-3 text-[13px] leading-6 text-(--muted-foreground)">
                        点击任意已结账的月份即可从该月起反结账——该月份及其后所有已结账期间都会恢复为可修改状态；如需重做结转，请先反结账，再撤销结转。
                    </p>
                    {closedGroups.length === 0 ? (
                        <div className="flex justify-center py-8 text-[13px] text-(--muted-foreground)">暂无已结账期间</div>
                    ) : (
                        <div className="flex flex-col gap-4">
                            {closedGroups.map(([year, yearPeriods]) => (
                                <div key={year} className="flex flex-col gap-2">
                                    <div className="text-[15px] font-bold text-(--foreground)">{year}</div>
                                    <div className="flex flex-wrap gap-2.5">
                                        {yearPeriods.map((period) => (
                                            <button
                                                key={period.periodText}
                                                type="button"
                                                disabled={!!busy}
                                                onClick={() => void doReopen(period)}
                                                className={cn(
                                                    "flex w-[124px] flex-col items-start gap-1 rounded-xl border border-(--border) bg-(--muted) px-3 py-2.5 text-left transition-colors hover:border-(--border)",
                                                    busy === `reopen-${period.periodText}` && "opacity-60",
                                                )}
                                            >
                                                <span className="text-[15px] font-semibold text-(--foreground)">{period.month} 月</span>
                                                <span className="flex items-center gap-1.5 text-[11.5px] text-(--muted-foreground)">
                                                    <span className="size-1.5 rounded-full bg-(--muted)" />
                                                    已结账
                                                </span>
                                            </button>
                                        ))}
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </SectionCard>
            )}

            {wizard && (
                <ClosingWizard
                    key={`${wizard.period.periodText}-${wizard.initialStep}`}
                    accountSetId={accountSetId}
                    token={token}
                    period={wizard.period}
                    initialStep={wizard.initialStep}
                    carriedThrough={status?.carriedPeriod}
                    onClose={() => setWizard(null)}
                    onChanged={() => {
                        void loadStatus(true)
                    }}
                    onClosed={(message) => {
                        setWizard(null)
                        setNotice(message)
                        void loadStatus(true)
                    }}
                />
            )}

            <ClosingTemplatesSheet
                accountSetId={accountSetId}
                token={token}
                open={templatesOpen}
                onOpenChange={setTemplatesOpen}
            />

            {confirmDialog}
        </div>
    )
}
