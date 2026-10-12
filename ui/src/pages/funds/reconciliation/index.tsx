import { useCallback, useEffect, useState } from "react"
import dayjs from "dayjs"
import { CircleAlert, CircleCheck, FileCheck, RefreshCw, Scale, TriangleAlert, Wallet, X } from "lucide-react"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { FundReportsApi, type FundReconciliationDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { PageHeader, SectionCard } from "@/components/page/section"
import { Tile, type TileTone } from "@/components/page/tile"
import { StatusPill } from "@/components/page/status-pill"
import { DATE_FIELD, PAGE_BTN_GHOST } from "@/components/page/styles"
import { Alert, AlertAction, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { DateField } from "@/components/fields/date-fields"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"

function formatMoney(value: number): string {
    return value.toLocaleString("zh-CN", { style: "currency", currency: "CNY", minimumFractionDigits: 2 })
}

export default function ReconciliationPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    // 对账截止日（默认今天）。
    const [asOf, setAsOf] = useState<Date>(() => dayjs().toDate())
    const [data, setData] = useState<FundReconciliationDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new FundReportsApi(apiConfig(token)).accountsetsAccountSetIdFundReportsReconciliationGet({
                accountSetId,
                // 传 UTC 零点避免本地时区在序列化时回退一天。
                date: new Date(dayjs(asOf).format("YYYY-MM-DD")),
            })
            setData(res)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载核对总账数据失败，请重试。"))
            setData(null)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, asOf])

    useEffect(() => {
        void load()
    }, [load])

    const rows = data?.rows ?? []
    const diffCount = rows.filter((r) => !r.balanced).length
    const absDiff = rows.reduce((sum, r) => sum + Math.abs(r.difference ?? 0), 0)

    const tiles: {
        label: string
        value: string
        tone: TileTone
        icon: React.ComponentType<{ className?: string }>
        hint?: string
    }[] = [
        { label: "资金账户", value: `${rows.length}`, tone: "gray", icon: Wallet },
        { label: "已核平", value: `${rows.length - diffCount}`, tone: "green", icon: CircleCheck },
        { label: "有差异", value: `${diffCount}`, tone: diffCount > 0 ? "red" : "gray", icon: TriangleAlert },
        { label: "差异金额合计", value: formatMoney(absDiff), tone: "blue", icon: Scale, hint: "各账户差异绝对值之和" },
    ]

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={FileCheck}
                title="核对总账"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。逐账户对照出纳余额与总账科目余额：出纳侧为手工登记台账（全量流水），总账侧仅统计已过账凭证与科目期初。
                    </>
                }
                actions={
                    <Button variant="outline" className={PAGE_BTN_GHOST} onClick={() => void load()} disabled={!accountSetId || loading}>
                        {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                        刷新
                    </Button>
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

            {data && rows.length > 0 && (
                data.allBalanced ? (
                    <Alert className="rounded-2xl border-[#a7f3d0] bg-[#ecfdf5] text-[#059669]">
                        <CircleCheck />
                        <AlertTitle>出纳账与总账全部核对一致（截至 {dayjs(asOf).format("YYYY-MM-DD")}）。</AlertTitle>
                    </Alert>
                ) : (
                    <Alert className="rounded-2xl border-[#fde68a] bg-[#fffbeb] text-[#b45309]">
                        <TriangleAlert />
                        <AlertTitle>
                            {`存在 ${diffCount} 个账户余额不一致（截至 ${dayjs(asOf).format("YYYY-MM-DD")}），请逐项核对。`}
                        </AlertTitle>
                    </Alert>
                )
            )}

            <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
                {tiles.map((t) => (
                    <Tile key={t.label} {...t} />
                ))}
            </section>

            {rows.length === 0 && !loading ? (
                <SectionCard bodyClassName="p-0">
                    <div className="flex flex-col items-center gap-3 py-12">
                        <FileCheck size={40} strokeWidth={1.2} className="text-slate-300" />
                        <p className="text-[13px] text-slate-400">
                            {accountSetId ? "还没有资金账户，无法核对。请先到「资金流水」创建账户。" : "请先选择账套。"}
                        </p>
                    </div>
                </SectionCard>
            ) : (
                <SectionCard
                    title={<h3 className="text-[14px] font-semibold text-slate-800">账户余额对照</h3>}
                    action={
                        <div className="flex flex-wrap items-center gap-2">
                            <span className="text-[12px] text-slate-400">对账截止日</span>
                            <DateField
                                value={asOf}
                                onChange={(v) => v && setAsOf(v)}
                                allowClear={false}
                                className={DATE_FIELD}
                            />
                        </div>
                    }
                    bodyClassName="p-0"
                >
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead className="text-left">账户</TableHead>
                                <TableHead className="text-left">类型</TableHead>
                                <TableHead className="text-left">对应科目</TableHead>
                                <TableHead className="text-right">出纳余额</TableHead>
                                <TableHead className="text-right">总账余额</TableHead>
                                <TableHead className="text-right">差异</TableHead>
                                <TableHead className="text-center">状态</TableHead>
                                <TableHead className="text-left">备注</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {rows.map((r) => (
                                <TableRow key={r.journalId}>
                                    <TableCell className="w-full">
                                        <div className="truncate font-medium text-slate-700">{r.journalName}</div>
                                    </TableCell>
                                    <TableCell>
                                        <StatusPill tone={r.journalType === 1 ? "green" : "blue"}>
                                            {r.journalType === 1 ? "现金" : "银行"}
                                        </StatusPill>
                                    </TableCell>
                                    <TableCell className="whitespace-nowrap text-slate-500">
                                        {r.accountCode ? `${r.accountCode} ${r.accountName}` : ""}
                                    </TableCell>
                                    <TableCell className="text-right tabular-nums">{formatMoney(r.journalBalance ?? 0)}</TableCell>
                                    <TableCell className="text-right tabular-nums">{formatMoney(r.ledgerBalance ?? 0)}</TableCell>
                                    <TableCell className="text-right tabular-nums">
                                        {r.balanced
                                            ? <span className="text-slate-300">0.00</span>
                                            : <span className="font-medium text-[#ef4444]">{formatMoney(r.difference ?? 0)}</span>}
                                    </TableCell>
                                    <TableCell className="text-center">
                                        {r.balanced
                                            ? <StatusPill tone="green">已平</StatusPill>
                                            : <StatusPill tone="amber">有差异</StatusPill>}
                                    </TableCell>
                                    <TableCell className="text-[12px] text-[#b45309]">{r.note ?? ""}</TableCell>
                                </TableRow>
                            ))}
                            {rows.length === 0 && !loading && (
                                <TableRow>
                                    <TableCell colSpan={8} className="py-8 text-center text-slate-400">
                                        暂无数据
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </SectionCard>
            )}
        </div>
    )
}
