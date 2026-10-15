import { useCallback, useEffect, useState } from "react"
import dayjs from "dayjs"
import {
    CheckCircleOutlined,
    DiffOutlined,
    FileDoneOutlined,
    ReloadOutlined,
    WalletOutlined,
    WarningOutlined,
} from "@ant-design/icons"
import { Alert, Button, Table } from "antd"
import type { TableColumnsType } from "antd"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { FundReportsApi, type FundReconciliationDto, type FundReconciliationRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtMoney } from "@/lib/format"
import { PageHeader, SectionCard } from "@/components/page/section"
import { Tile, type TileTone } from "@/components/page/tile"
import { StatusPill } from "@/components/page/status-pill"
import { AMOUNT_COL, ICON_STYLE } from "@/components/page/styles"
import { DateField } from "@/components/fields/date-fields"

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
        { label: "资金账户", value: `${rows.length}`, tone: "gray", icon: WalletOutlined },
        { label: "已核平", value: `${rows.length - diffCount}`, tone: "green", icon: CheckCircleOutlined },
        { label: "有差异", value: `${diffCount}`, tone: diffCount > 0 ? "red" : "gray", icon: WarningOutlined },
        { label: "差异金额合计", value: fmtMoney(absDiff), tone: "blue", icon: DiffOutlined, hint: "各账户差异绝对值之和" },
    ]

    const columns: TableColumnsType<FundReconciliationRowDto> = [
        {
            title: "账户",
            render: (_, r) => <span className="truncate font-medium text-slate-700">{r.journalName}</span>,
        },
        {
            title: "类型",
            render: (_, r) => (
                <StatusPill tone={r.journalType === 1 ? "green" : "blue"}>
                    {r.journalType === 1 ? "现金" : "银行"}
                </StatusPill>
            ),
        },
        {
            title: "对应科目",
            className: "whitespace-nowrap text-slate-500",
            render: (_, r) => (r.accountCode ? `${r.accountCode} ${r.accountName}` : ""),
        },
        { title: "出纳余额", render: (_, r) => fmtMoney(r.journalBalance ?? 0), ...AMOUNT_COL },
        { title: "总账余额", render: (_, r) => fmtMoney(r.ledgerBalance ?? 0), ...AMOUNT_COL },
        {
            title: "差异",
            ...AMOUNT_COL,
            render: (_, r) =>
                r.balanced ? (
                    <span className="text-slate-300">0.00</span>
                ) : (
                    <span className="font-medium text-[#ef4444]">{fmtMoney(r.difference ?? 0)}</span>
                ),
        },
        {
            title: "状态",
            align: "center",
            render: (_, r) =>
                r.balanced ? <StatusPill tone="green">已平</StatusPill> : <StatusPill tone="amber">有差异</StatusPill>,
        },
        {
            title: "备注",
            className: "text-[12px] text-[#b45309]",
            render: (_, r) => r.note ?? "",
        },
    ]

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={FileDoneOutlined}
                title="核对总账"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。逐账户对照出纳余额与总账科目余额：出纳侧为手工登记台账（全量流水），总账侧仅统计已过账凭证与科目期初。
                    </>
                }
                actions={
                    <Button
                        icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                        disabled={!accountSetId || loading}
                        onClick={() => void load()}
                    >
                        刷新
                    </Button>
                }
            />

            {error && <Alert type="error" showIcon closable title={error} onClose={() => setError(null)} />}

            {data &&
                rows.length > 0 &&
                (data.allBalanced ? (
                    <Alert
                        type="success"
                        showIcon
                        title={`出纳账与总账全部核对一致（截至 ${dayjs(asOf).format("YYYY-MM-DD")}）。`}
                    />
                ) : (
                    <Alert
                        type="warning"
                        showIcon
                        title={`存在 ${diffCount} 个账户余额不一致（截至 ${dayjs(asOf).format("YYYY-MM-DD")}），请逐项核对。`}
                    />
                ))}

            <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
                {tiles.map((t) => (
                    <Tile key={t.label} {...t} />
                ))}
            </section>

            {rows.length === 0 && !loading ? (
                <SectionCard bodyClassName="p-0">
                    <div className="flex flex-col items-center gap-3 py-12">
                        <FileDoneOutlined className="text-[40px]" style={ICON_STYLE.placeholder} />
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
                            <DateField value={asOf} onChange={(v) => v && setAsOf(v)} allowClear={false} />
                        </div>
                    }
                    bodyClassName="p-0"
                >
                    <Table
                        columns={columns}
                        dataSource={rows}
                        rowKey={(r) => r.journalId ?? ""}
                        pagination={false}
                        loading={loading}
                        size="small"
                        locale={{ emptyText: "暂无数据" }}
                    />
                </SectionCard>
            )}
        </div>
    )
}
