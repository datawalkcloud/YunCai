import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, Button, Checkbox, Drawer, Tabs } from "antd"
import {
    CalendarOutlined,
    CheckCircleOutlined,
    CloseCircleOutlined,
    InfoCircleOutlined,
    PrinterOutlined,
    ProfileOutlined,
    ReloadOutlined,
    RollbackOutlined,
    SettingOutlined,
    ThunderboltOutlined,
    UndoOutlined,
} from "@ant-design/icons"

import { useAuth } from "@/auth/AuthContext"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import {
    ClosingApi,
    type CarryVoucherRefDto,
    type ClosingCheckResultDto,
    type ClosingPeriodDto,
    type ClosingStatusDto,
    type ClosingTemplateDto,
    type ClosingTemplatePreviewDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { useConfirm } from "@/hooks/useConfirm"
import { PageHeader, SectionCard } from "@/components/page/section"
import { ICON_STYLE } from "@/components/page/styles"
import { ClosingTemplatesSheet } from "./templates-sheet"
import { PrintVoucherSheet } from "./print-sheet"

/** 期间状态展示口径（与后端 ClosingPeriodDto.State 对应）。 */
const STATE_META: Record<string, { label: string; short: string; dot: string; card: string }> = {
    NotCarried: {
        label: "未结转损益、未结账",
        short: "待结转",
        dot: "bg-[#22c55e]",
        card: "border-slate-200 bg-[#f8fdf9]",
    },
    Carried: {
        label: "已结转损益、未结账",
        short: "待结账",
        dot: "bg-[#2f6bff]",
        card: "border-[#bfdbfe] bg-[#f5f9ff]",
    },
    Closed: {
        label: "已结转损益、已结账",
        short: "已结账",
        dot: "bg-slate-300",
        card: "border-slate-200 bg-slate-50",
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

export default function ClosingPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { confirm, confirmDialog } = useConfirm()

    const [status, setStatus] = useState<ClosingStatusDto | null>(null)
    const [checks, setChecks] = useState<ClosingCheckResultDto | null>(null)
    const [loading, setLoading] = useState(false)
    /** 页签：期末处理 / 反结账（与柠檬云一致） */
    const [tab, setTab] = useState<"process" | "reopen">("process")
    const [busy, setBusy] = useState<string | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [notice, setNotice] = useState<string | null>(null)
    const [selected, setSelected] = useState<{ year: number; month: number } | null>(null)

    const [refreshTick, setRefreshTick] = useState(0)
    const [carryOpen, setCarryOpen] = useState(false)
    const [splitIncomeCost, setSplitIncomeCost] = useState(false)
    const [transferToRetained, setTransferToRetained] = useState(false)
    const [templatesOpen, setTemplatesOpen] = useState(false)
    const [printVoucherId, setPrintVoucherId] = useState<string | null>(null)
    const [templates, setTemplates] = useState<ClosingTemplateDto[]>([])
    const [templatePreview, setTemplatePreview] = useState<ClosingTemplatePreviewDto | null>(null)
    const [selectedTemplateIds, setSelectedTemplateIds] = useState<string[]>([])
    const [lastVouchers, setLastVouchers] = useState<CarryVoucherRefDto[]>([])

    const api = useMemo(() => new ClosingApi(apiConfig(token)), [token])

    /** 默认选中期间：有「已结转未结账」的期间时优先选中它（下一步动作是结账）；否则选中下一个待结转期间。 */
    const defaultPeriod = useCallback((s: ClosingStatusDto) => {
        const carriedKey = periodKey(s.carriedPeriod)
        const closedKey = periodKey(s.closedPeriod)
        if (carriedKey > closedKey && s.nextCloseYear && s.nextCloseMonth) {
            return { year: s.nextCloseYear, month: s.nextCloseMonth }
        }
        if (s.nextCarryYear && s.nextCarryMonth) {
            return { year: s.nextCarryYear, month: s.nextCarryMonth }
        }
        return null
    }, [])

    const loadStatus = useCallback(
        async (keepSelection: boolean) => {
            if (!accountSetId) return null
            setLoading(true)
            setError(null)
            try {
                const s = await api.accountsetsAccountSetIdClosingGet({ accountSetId })
                setStatus(s)
                setSelected((cur) => (keepSelection && cur ? cur : defaultPeriod(s)))
                return s
            } catch (err) {
                setError(await apiErrorMessage(err, "加载期末结转状态失败，请重试。"))
                return null
            } finally {
                setLoading(false)
            }
        },
        [accountSetId, api, defaultPeriod],
    )

    const loadChecks = useCallback(
        async (year: number, month: number) => {
            if (!accountSetId) return
            setError(null)
            try {
                const c = await api.accountsetsAccountSetIdClosingChecksGet({ accountSetId, year, month })
                setChecks(c)
            } catch (err) {
                setChecks(null)
                setError(await apiErrorMessage(err, "期末检查失败，请重试。"))
            }
        },
        [accountSetId, api],
    )

    useEffect(() => {
        void loadStatus(false)
    }, [loadStatus])

    // 选中期间变化、或数据发生变化（refreshTick，如刚完成结转）时，重新执行期末检查。
    useEffect(() => {
        if (selected) void loadChecks(selected.year, selected.month)
    }, [selected, loadChecks, refreshTick])

    // 打开发起结转的弹层时，装载结转模板与按当前期间的预览（金额随月份数据计算）。
    useEffect(() => {
        if (!carryOpen || !selected || !accountSetId) return
        let cancelled = false
        void (async () => {
            try {
                const [list, preview] = await Promise.all([
                    api.accountsetsAccountSetIdClosingTemplatesGet({ accountSetId }),
                    api.accountsetsAccountSetIdClosingTemplatesPreviewGet({
                        accountSetId,
                        year: selected.year,
                        month: selected.month,
                    }),
                ])
                if (cancelled) return
                setTemplates(list)
                setTemplatePreview(preview)
                setSelectedTemplateIds(list.filter((t) => t.isEnabled && t.id).map((t) => t.id as string))
            } catch {
                if (!cancelled) {
                    setTemplates([])
                    setTemplatePreview(null)
                    setSelectedTemplateIds([])
                }
            }
        })()
        return () => {
            cancelled = true
        }
    }, [carryOpen, selected, accountSetId, api])

    /** 执行结转损益（生成结转凭证并自动过账）。 */
    async function doCarryForward() {
        if (!selected || busy) return
        setBusy("carry")
        setError(null)
        setNotice(null)
        try {
            const result = await api.accountsetsAccountSetIdClosingCarryForwardPost({
                accountSetId,
                carryForwardRequest: {
                    year: selected.year,
                    month: selected.month,
                    splitIncomeCost,
                    transferToRetainedEarnings: transferToRetained,
                    templateIds: selectedTemplateIds,
                },
            })
            setCarryOpen(false)
            setLastVouchers(result.vouchers ?? [])
            setNotice(result.message ?? "结转完成。")
            // 结转后停留在本期间，便于直接结账；并刷新检查结果（canClose 等）。
            await loadStatus(true)
            setRefreshTick((t) => t + 1)
        } catch (err) {
            setError(await apiErrorMessage(err, "结转失败，请重试。"))
        } finally {
            setBusy(null)
        }
    }

    /** 结账：锁定期间内凭证。 */
    async function doClose() {
        if (!selected || busy) return
        if (!(await confirm(`确定对 ${selected.year} 年 ${selected.month} 月执行结账吗？结账后该期间凭证将不可增删改。`))) {
            return
        }
        setBusy("close")
        setError(null)
        setNotice(null)
        try {
            const s = await api.accountsetsAccountSetIdClosingClosePost({
                accountSetId,
                closingPeriodRequest: { year: selected.year, month: selected.month },
            })
            setStatus(s)
            setSelected((cur) => (cur ? cur : defaultPeriod(s)))
            setNotice(`已结账至 ${s.closedPeriod ?? ""}。`)
            await loadStatus(false)
            setRefreshTick((t) => t + 1)
        } catch (err) {
            setError(await apiErrorMessage(err, "结账失败，请重试。"))
        } finally {
            setBusy(null)
        }
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
        setLastVouchers([])
        try {
            const result = await api.accountsetsAccountSetIdClosingCloseBatchPost({ accountSetId })
            if (result.status) {
                setStatus(result.status)
                setSelected(defaultPeriod(result.status))
            }
            setNotice(result.message ?? "批量结账完成。")
            setRefreshTick((t) => t + 1)
        } catch (err) {
            setError(await apiErrorMessage(err, "批量结账失败，请重试。"))
        } finally {
            setBusy(null)
        }
    }

    /** 反结账：退回最近一个已结期间。 */
    async function doReopen() {
        if (busy || !status?.canReopen) return
        if (!(await confirm(`确定反结账吗？将退回最近一个已结期间（${status?.closedPeriod ?? ""}），该期间凭证恢复可修改。`))) {
            return
        }
        setBusy("reopen")
        setError(null)
        setNotice(null)
        try {
            const s = await api.accountsetsAccountSetIdClosingReopenPost({ accountSetId })
            setStatus(s)
            setSelected(defaultPeriod(s))
            setNotice(`已反结账，当前结账进度：${s.closedPeriod ?? "未结账"}。`)
            setRefreshTick((t) => t + 1)
        } catch (err) {
            setError(await apiErrorMessage(err, "反结账失败，请重试。"))
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
            const result = await api.accountsetsAccountSetIdClosingCancelCarryPost({
                accountSetId,
                closingPeriodRequest: { year, month },
            })
            setNotice(result.message ?? "已撤销结转。")
            await loadStatus(false)
            setRefreshTick((t) => t + 1)
        } catch (err) {
            setError(await apiErrorMessage(err, "撤销结转失败，请重试。"))
        } finally {
            setBusy(null)
        }
    }

    const selectedKey = selected ? selected.year * 100 + selected.month : 0
    const carriedKey = periodKey(status?.carriedPeriod)
    const closedKey = periodKey(status?.closedPeriod)

    const yearGroups = useMemo(() => {
        const groups = new Map<number, ClosingPeriodDto[]>()
        for (const p of status?.periods ?? []) {
            const year = p.year ?? 0
            const list = groups.get(year) ?? []
            list.push(p)
            groups.set(year, list)
        }
        return [...groups.entries()].sort((a, b) => a[0] - b[0])
    }, [status])

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={CalendarOutlined}
                title="期末结转"
                subtitle="按「期末检查 → 结转损益 → 结账」顺序完成月末处理；结账后该期间凭证不可增删改。"
                actions={
                    <>
                        <Button icon={<SettingOutlined />} onClick={() => setTemplatesOpen(true)}>
                            结转模板
                        </Button>
                        <Button
                            icon={busy === "batch" ? <ProfileOutlined spin /> : <ProfileOutlined />}
                            disabled={!status || periodKey(status.carriedPeriod) <= periodKey(status.closedPeriod) || !!busy}
                            title="从下一个待结期间连续结账至最新已结转期间"
                            onClick={() => void doCloseBatch()}
                        >
                            批量结账
                        </Button>
                        <Button
                            icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                            disabled={loading}
                            onClick={() => void loadStatus(true)}
                        >
                            刷新
                        </Button>
                    </>
                }
            />

            <Tabs
                activeKey={tab}
                onChange={(key) => setTab(key as "process" | "reopen")}
                items={[
                    { key: "process", label: "期末处理" },
                    { key: "reopen", label: "反结账" },
                ]}
            />

            {tab === "reopen" && (
                <div className="flex flex-col gap-5">
                    <SectionCard title={<h3 className="text-[14px] font-semibold text-slate-800">反结账</h3>}>
                        <p className="text-[13px] leading-6 text-slate-500">
                            反结账用于把「已结账至」退回上一个期间；若该期间还做过结转，需先「撤销结转」删除结转凭证，再反结账。
                            结账与反结账都会记入操作日志。
                        </p>
                        <div className="mt-3 flex flex-wrap gap-2">
                            <Button
                                icon={busy === "cancel" ? <RollbackOutlined spin /> : <RollbackOutlined />}
                                disabled={!status?.canCancelCarry || !!busy}
                                onClick={() => void doCancelCarry()}
                                title={status?.canCancelCarry ? `撤销结转（${status?.carriedPeriod}）` : "最近一次结转已被结账覆盖"}
                            >
                                撤销结转
                            </Button>
                            <Button
                                icon={busy === "reopen" ? <UndoOutlined spin /> : <UndoOutlined />}
                                disabled={!status?.canReopen || !!busy}
                                onClick={() => void doReopen()}
                                title={status?.canReopen ? `反结账（退回 ${status?.closedPeriod}）` : "尚未结账"}
                            >
                                反结账
                            </Button>
                        </div>
                    </SectionCard>
                </div>
            )}

            <div className={tab === "reopen" ? "hidden" : "flex flex-col gap-5"}>
            {error && <Alert type="error" showIcon title={error} />}
            {notice && (
                <Alert
                    type="success"
                    showIcon
                    title={
                        <div className="flex flex-wrap items-center gap-2">
                            <span>{notice}</span>
                            {lastVouchers.map((v) => (
                                <button
                                    key={v.id}
                                    type="button"
                                    onClick={() => v.id && setPrintVoucherId(v.id)}
                                    className="inline-flex items-center gap-1 border border-[#86efac] bg-white px-2.5 py-0.5 text-[12px] text-[#15803d] transition-colors hover:bg-[#f0fdf4]"
                                    title="打印该凭证"
                                >
                                    <PrinterOutlined className="text-[12px]" />
                                    {v.label}
                                    {v.kind === "Template" ? "（模板）" : ""}
                                </button>
                            ))}
                        </div>
                    }
                />
            )}

            <SectionCard title={<h3 className="text-[14px] font-semibold text-slate-800">结转进度</h3>}>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                    {[
                        { label: "启用期间", value: status?.startPeriod ?? "—" },
                        { label: "已结转损益至", value: status?.carriedPeriod ?? "未结转" },
                        { label: "已结账至", value: status?.closedPeriod ?? "未结账" },
                    ].map((item) => (
                        <div key={item.label} className="rounded-xl border border-slate-100 bg-[#fafbfe] px-4 py-3">
                            <div className="text-[12px] text-slate-400">{item.label}</div>
                            <div className="mt-1 text-[15px] font-semibold text-slate-800">{item.value}</div>
                        </div>
                    ))}
                </div>
            </SectionCard>

            <SectionCard
                title={<h3 className="text-[14px] font-semibold text-slate-800">期间状态</h3>}
                action={
                    <div className="flex flex-wrap items-center gap-3 text-[12px] text-slate-500">
                        {[
                            { dot: "bg-[#22c55e]", text: "未结转损益、未结账" },
                            { dot: "bg-[#2f6bff]", text: "已结转损益、未结账" },
                            { dot: "bg-slate-300", text: "已结转损益、已结账" },
                        ].map((l) => (
                            <span key={l.text} className="inline-flex items-center gap-1.5">
                                <span className={cn("size-2 rounded-full", l.dot)} />
                                {l.text}
                            </span>
                        ))}
                    </div>
                }
            >
                {yearGroups.length === 0 ? (
                    <div className="flex justify-center py-8 text-[13px] text-slate-400">
                        {loading ? "正在加载…" : "暂无期间数据"}
                    </div>
                ) : (
                    <div className="flex flex-col gap-4">
                        {yearGroups.map(([year, periods]) => (
                            <div key={year} className="flex flex-col gap-2">
                                <div className="text-[15px] font-bold text-slate-800">{year}</div>
                                <div className="flex flex-wrap gap-2.5">
                                    {periods.map((p) => {
                                        const meta = stateMeta(p.state)
                                        const active =
                                            selected?.year === p.year && selected?.month === p.month
                                        return (
                                            <button
                                                key={p.periodText}
                                                type="button"
                                                onClick={() => setSelected({ year: p.year ?? 0, month: p.month ?? 0 })}
                                                className={cn(
                                                    "flex w-[124px] flex-col items-start gap-1 rounded-xl border px-3 py-2.5 text-left transition-colors",
                                                    meta.card,
                                                    active
                                                        ? "border-[#2f6bff] ring-2 ring-[#dbeafe]"
                                                        : "hover:border-slate-300",
                                                )}
                                            >
                                                <span className="flex items-center gap-1.5 text-[15px] font-semibold text-slate-800">
                                                    {p.month} 月
                                                </span>
                                                <span className="flex items-center gap-1.5 text-[11.5px] text-slate-500">
                                                    <span className={cn("size-1.5 rounded-full", meta.dot)} />
                                                    {meta.short}
                                                </span>
                                                {!!p.unpostedCount && (
                                                    <span className="text-[11px] font-medium text-[#ef4444]">
                                                        {p.unpostedCount} 张未过账
                                                    </span>
                                                )}
                                            </button>
                                        )
                                    })}
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </SectionCard>

            <SectionCard
                title={
                    <h3 className="text-[14px] font-semibold text-slate-800">
                        期末处理{selected ? ` · ${selected.year} 年 ${selected.month} 月` : ""}
                    </h3>
                }
            >
                <div className="flex flex-col gap-4">
                    {/* 三步状态 */}
                    <div className="flex flex-wrap items-center gap-2 text-[12.5px]">
                        <span className="inline-flex items-center gap-1.5 bg-[#eef1f6] px-3 py-1 text-slate-600">
                            ① 期末检查
                        </span>
                        <span className="text-slate-300">→</span>
                        <span
                            className={cn(
                                "inline-flex items-center gap-1.5 px-3 py-1",
                                selectedKey <= carriedKey
                                    ? "bg-[#d1fae5] text-[#10b981]"
                                    : "bg-[#dbeafe] text-[#3b82f6]",
                            )}
                        >
                            ② 结转损益 {selectedKey <= carriedKey ? "· 已完成" : "· 待结转"}
                        </span>
                        <span className="text-slate-300">→</span>
                        <span
                            className={cn(
                                "inline-flex items-center gap-1.5 px-3 py-1",
                                selectedKey <= closedKey
                                    ? "bg-[#eef1f6] text-slate-500"
                                    : selectedKey <= carriedKey
                                        ? "bg-[#dbeafe] text-[#3b82f6]"
                                        : "bg-[#eef1f6] text-slate-500",
                            )}
                        >
                            ③ 结账{" "}
                            {selectedKey <= closedKey
                                ? "· 已完成"
                                : selectedKey <= carriedKey
                                    ? "· 待结账"
                                    : "· 需先结转"}
                        </span>
                    </div>

                    {/* 检查清单 */}
                    <div className="flex flex-col divide-y divide-slate-100 rounded-xl border border-slate-100">
                        {(checks?.items ?? []).map((item) => (
                            <div key={item.key} className="flex items-start gap-2.5 px-4 py-2.5">
                                {item.status === "ok" ? (
                                    <CheckCircleOutlined className="mt-0.5 shrink-0 text-[16px]" style={ICON_STYLE.ok} />
                                ) : item.status === "warn" ? (
                                    <InfoCircleOutlined className="mt-0.5 shrink-0 text-[16px]" style={ICON_STYLE.warn} />
                                ) : (
                                    <CloseCircleOutlined className="mt-0.5 shrink-0 text-[16px]" style={ICON_STYLE.danger} />
                                )}
                                <div className="min-w-0">
                                    <div className="text-[13px] font-medium text-slate-700">{item.title}</div>
                                    <div className="text-[12.5px] text-slate-500">{item.message}</div>
                                </div>
                            </div>
                        ))}
                        {(checks?.items ?? []).length === 0 && (
                            <div className="flex justify-center py-6 text-[13px] text-slate-400">
                                {selected ? "正在检查…" : "请先选择期间"}
                            </div>
                        )}
                    </div>

                    {/* 操作按钮 */}
                    <div className="flex flex-wrap items-center gap-2">
                        <Button
                            icon={<ReloadOutlined />}
                            disabled={!selected}
                            onClick={() => selected && void loadChecks(selected.year, selected.month)}
                        >
                            重新检查
                        </Button>
                        <Button
                            type="primary"
                            icon={<ThunderboltOutlined />}
                            disabled={!checks?.canCarry || !!busy}
                            title={checks?.canCarry ? "结转本期损益并自动生成、过账结转凭证" : "请先处理检查清单中的阻断项"}
                            onClick={() => setCarryOpen(true)}
                        >
                            结转损益
                        </Button>
                        <Button
                            type="primary"
                            icon={busy === "close" ? <CheckCircleOutlined spin /> : <CheckCircleOutlined />}
                            disabled={!checks?.canClose || !!busy}
                            title={
                                checks?.canClose
                                    ? "结账后该期间凭证不可增删改"
                                    : "需先完成结转且检查项无阻断"
                            }
                            onClick={() => void doClose()}
                        >
                            结账
                        </Button>
                    </div>
                </div>
            </SectionCard>

            {/* 结转选项 */}
            </div>

            <Drawer
                open={carryOpen}
                size={512}
                title={`结转损益${selected ? ` · ${selected.year} 年 ${selected.month} 月` : ""}`}
                onClose={() => setCarryOpen(false)}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16, overflow: "auto" } }}
                footer={
                    <div className="flex justify-end gap-2">
                        <Button onClick={() => setCarryOpen(false)}>取消</Button>
                        <Button
                            type="primary"
                            icon={<ThunderboltOutlined />}
                            loading={busy === "carry"}
                            disabled={busy === "carry"}
                            onClick={() => void doCarryForward()}
                        >
                            确认结转
                        </Button>
                    </div>
                }
            >
                <p className="text-[12.5px] text-slate-400">
                    将本期损益类科目余额结平并转入「本年利润」，生成的结转凭证自动过账并进入账簿与报表。
                </p>

                <label className="flex items-start gap-2.5 text-[13px]">
                    <Checkbox
                        className="mt-0.5"
                        checked={splitIncomeCost}
                        onChange={(e) => setSplitIncomeCost(e.target.checked)}
                    />
                    <span>
                        <span className="block font-medium text-slate-700">收入与成本分开结转</span>
                        <span className="block text-[12px] text-slate-400">
                            分别生成「收入类」与「成本费用类」两张结转凭证（默认合并为一张）。
                        </span>
                    </span>
                </label>

                <label className="flex items-start gap-2.5 text-[13px]">
                    <Checkbox
                        className="mt-0.5"
                        checked={transferToRetained}
                        onChange={(e) => setTransferToRetained(e.target.checked)}
                    />
                    <span>
                        <span className="block font-medium text-slate-700">
                            同时结转「本年利润」至「未分配利润」
                        </span>
                        <span className="block text-[12px] text-slate-400">
                            将「本年利润」余额一次性转入「未分配利润」（通常用于年末）。
                        </span>
                    </span>
                </label>

                {templates.length > 0 ? (
                    <div className="flex flex-col gap-2 rounded-xl border border-slate-100 p-3">
                        <div className="text-[12.5px] font-medium text-slate-600">结转模板（勾选后随本次结转一起生成凭证）</div>
                        {templates.map((t) => {
                            const item = templatePreview?.templates?.find((x) => x.templateId === t.id)
                            const checked = !!t.id && selectedTemplateIds.includes(t.id)
                            return (
                                <label key={t.id} className="flex items-start gap-2.5 text-[13px]">
                                    <Checkbox
                                        className="mt-0.5"
                                        checked={checked}
                                        onChange={(e) =>
                                            setSelectedTemplateIds((ids) =>
                                                e.target.checked ? [...ids, t.id as string] : ids.filter((x) => x !== t.id),
                                            )
                                        }
                                    />
                                    <span className="flex min-w-0 flex-wrap items-center gap-2">
                                        <span className="font-medium text-slate-700">{t.name}</span>
                                        {t.isEnabled === false && (
                                            <span className="bg-[#eef1f6] px-1.5 py-0.5 text-[10.5px] text-slate-400">
                                                已停用
                                            </span>
                                        )}
                                        {item &&
                                            (item.balanced ? (
                                                <span className="text-[11.5px] text-slate-400">
                                                    借 {item.totalDebit ?? 0} / 贷 {item.totalCredit ?? 0}
                                                </span>
                                            ) : (
                                                <span className="text-[11.5px] text-[#ef4444]">
                                                    {item.error ?? "借贷不平衡"}
                                                </span>
                                            ))}
                                    </span>
                                </label>
                            )
                        })}
                        <p className="text-[11.5px] text-slate-400">金额按所选期间自动计算；不平衡的模板会在结转时被拒绝。</p>
                    </div>
                ) : (
                    <p className="rounded-lg bg-slate-50 px-3 py-2 text-[12px] text-slate-400">
                        尚无结转模板（如计提工资、结转成本）；可先在「结转模板」中创建。
                    </p>
                )}

                <p className="rounded-lg bg-slate-50 px-3 py-2 text-[12px] text-slate-500">
                    结转凭证自动过账；如需重做，可先使用「撤销结转」删除本次生成的凭证。
                </p>
            </Drawer>

            {/* 结转模板管理 */}
            <ClosingTemplatesSheet
                accountSetId={accountSetId}
                token={token}
                open={templatesOpen}
                onOpenChange={setTemplatesOpen}
            />

            {/* 结转凭证打印 */}
            <PrintVoucherSheet
                accountSetId={accountSetId}
                token={token}
                voucherId={printVoucherId}
                open={!!printVoucherId}
                onOpenChange={(o) => {
                    if (!o) setPrintVoucherId(null)
                }}
            />

            {confirmDialog}
        </div>
    )
}
