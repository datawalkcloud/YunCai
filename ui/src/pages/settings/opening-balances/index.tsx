import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { Alert, Button, Spin, Table, Tabs } from "antd"
import type { TableColumnsType } from "antd"
import {
    CalculatorOutlined,
    CheckCircleOutlined,
    CloseOutlined,
    DownloadOutlined,
    ReloadOutlined,
    SaveOutlined,
    UploadOutlined,
    WarningFilled,
} from "@ant-design/icons"

import { useAuth } from "@/auth/AuthContext"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { AccountsApi, type OpeningBalanceRowDto, type OpeningBalancesDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { cn } from "@/lib/utils"
import { SectionCard } from "@/components/page/section"
import { StatusPill } from "@/components/page/status-pill"
import { NumberField } from "@/components/fields/number-field"

/** 科目层级缩进：每级 1rem，等价原来的 paddingLeft: (level - 1) * 16。 */
const LEVEL_INDENT = ["", "pl-4", "pl-8", "pl-12", "pl-16", "pl-20", "pl-24", "pl-28", "pl-32", "pl-36"]

/** 科目类别页签：与柠檬云「期初」一致（资产/负债/共同/权益/成本/损益）；共同类在快记无对应枚举，暂列空页签。 */
const CATEGORY_TABS = [
    { value: 1, label: "资产" },
    { value: 2, label: "负债" },
    { value: -1, label: "共同" },
    { value: 3, label: "权益" },
    { value: 4, label: "成本" },
    { value: 5, label: "损益" },
]
function indentFor(level?: number): string {
    return LEVEL_INDENT[Math.max(0, Math.min(LEVEL_INDENT.length - 1, (level ?? 1) - 1))]
}

/**
 * 期初余额录入页：末级科目按余额方向录入期初余额（反方向余额用负数），
 * 非末级科目由下级自动汇总，底部实时试算平衡（借方合计 = 贷方合计）。
 */
export default function OpeningBalancesPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId()

    const [data, setData] = useState<OpeningBalancesDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [savedTip, setSavedTip] = useState(false)
    /** 试算平衡提示文案（点按钮时刷新）。 */
    const [checkMessage, setCheckMessage] = useState<string | null>(null)
    /** 期初导入：文件选择框与导入中标记。 */
    const importInputRef = useRef<HTMLInputElement>(null)
    const [importing, setImporting] = useState(false)
    // 本地未保存的修改：accountId -> 期初余额。
    const [edits, setEdits] = useState<Record<string, number>>({})
    /** 当前科目类别页签（与柠檬云一致） */
    const [activeCategory, setActiveCategory] = useState(1)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const dto = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsOpeningBalancesGet({ accountSetId })
            setData(dto)
            setEdits({})
        } catch (err) {
            setError(await apiErrorMessage(err, "加载期初余额失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [token, accountSetId])

    useEffect(() => {
        void load()
    }, [load])

    const dirty = Object.keys(edits).length > 0

    // 本地实时试算：以末级科目的（编辑值优先）期初余额按方向换算净借方后汇总。
    const totals = useMemo(() => {
        let debit = 0
        let credit = 0
        for (const row of data?.rows ?? []) {
            if (!row.isLeaf || !row.accountId) continue
            const value = edits[row.accountId] ?? row.openingBalance ?? 0
            const netDebit = row.direction === 1 ? value : -value
            if (netDebit > 0) debit += netDebit
            else credit += -netDebit
        }
        return { debit, credit, balanced: Math.abs(debit - credit) < 0.005 }
    }, [data, edits])

    async function save() {
        if (!accountSetId || !dirty) return
        setSaving(true)
        setError(null)
        setSavedTip(false)
        try {
            const dto = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsOpeningBalancesPut({
                accountSetId,
                saveOpeningBalancesRequest: {
                    items: Object.entries(edits).map(([accountId, amount]) => ({ accountId, amount })),
                },
            })
            setData(dto)
            setEdits({})
            setSavedTip(true)
        } catch (err) {
            setError(await apiErrorMessage(err, "保存期初余额失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    const columns: TableColumnsType<OpeningBalanceRowDto> = useMemo(
        () => [
            {
                title: "科目编码",
                dataIndex: "code",
                className: "whitespace-nowrap",
                render: (_, row) => <span className={indentFor(row.level)}>{row.code}</span>,
            },
            {
                title: "科目名称",
                dataIndex: "name",
                render: (_, row) => (
                    <div className={cn("truncate", !row.isLeaf && "font-medium")}>{row.name}</div>
                ),
            },
            {
                title: "方向",
                dataIndex: "direction",
                align: "center",
                render: (_, row) => (
                    <StatusPill tone={row.direction === 1 ? "blue" : "purple"}>
                        {row.direction === 1 ? "借" : "贷"}
                    </StatusPill>
                ),
            },
            {
                title: "期初余额",
                dataIndex: "openingBalance",
                align: "right",
                render: (_, row) => {
                    if (!row.isLeaf || !row.accountId) {
                        return <span className="text-slate-400">{fmtAmount(row.openingBalance)}</span>
                    }
                    const id = row.accountId
                    return (
                        <NumberField
                            className="w-40"
                            precision={2}
                            value={edits[id] ?? row.openingBalance ?? 0}
                            onChange={(value) => {
                                setEdits((prev) => ({ ...prev, [id]: value ?? 0 }))
                                setSavedTip(false)
                            }}
                        />
                    )
                },
            },
            {
                title: "借方累计",
                dataIndex: "debit",
                align: "right",
                className: "tabular-nums",
                render: (v) => fmtAmount(v),
            },
            {
                title: "贷方累计",
                dataIndex: "credit",
                align: "right",
                className: "tabular-nums",
                render: (v) => fmtAmount(v),
            },
            {
                // 与柠檬云期初页一致：年初余额 = 期初余额扣掉本年累计发生
                title: "年初余额",
                dataIndex: "yearOpeningBalance",
                align: "right",
                className: "tabular-nums",
                render: (v) => fmtAmount(v),
            },
        ],
        [edits],
    )

                    /** 导出期初模板（后端生成 xlsx，含科目与期初列）。 */
    const exportTemplate = useCallback(async () => {
        if (!token || !accountSetId) return
        const res = await fetch(`/api/accountsets/${accountSetId}/Accounts/opening-balances/template`, {
            headers: { Authorization: `Bearer ${token}` },
        })
        if (!res.ok) return
        const url = URL.createObjectURL(await res.blob())
        const a = document.createElement("a")
        a.href = url
        a.download = "期初余额导入模板.xlsx"
        document.body.appendChild(a)
        a.click()
        a.remove()
        URL.revokeObjectURL(url)
    }, [token, accountSetId])

    /** 导入期初余额 xlsx 并刷新。 */
    const importFile = useCallback(
        async (file: File | null) => {
            if (!file || !token || !accountSetId) return
            setImporting(true)
            try {
                const body = new FormData()
                body.append("file", file)
                const res = await fetch(`/api/accountsets/${accountSetId}/Accounts/opening-balances/import`, {
                    method: "POST",
                    headers: { Authorization: `Bearer ${token}` },
                    body,
                })
                if (!res.ok) {
                    setError(await apiErrorMessage(res, "导入失败，请检查模板格式。"))
                } else {
                    await load()
                    setSavedTip(true)
                }
            } finally {
                setImporting(false)
                if (importInputRef.current) importInputRef.current.value = ""
            }
        },
        [token, accountSetId, load],
    )

    return (
        <div className="flex w-full flex-col gap-5">
            <div className="flex flex-wrap items-center justify-end gap-2">
                {/* 与柠檬云「期初」一致：导出模板 / 导入 / 试算平衡 */}
                <Button icon={<DownloadOutlined />} onClick={() => void exportTemplate()} disabled={!accountSetId}>
                    导出
                </Button>
                <Button
                    icon={<UploadOutlined />}
                    onClick={() => importInputRef.current?.click()}
                    disabled={!accountSetId || importing}
                >
                    导入
                </Button>
                <Button
                    icon={<CalculatorOutlined />}
                    onClick={() =>
                        setCheckMessage(
                            totals.balanced
                                ? `试算平衡：借方合计 ${fmtAmount(totals.debit, true)}，贷方合计 ${fmtAmount(totals.credit, true)}。`
                                : `试算不平衡：借方合计 ${fmtAmount(totals.debit, true)}，贷方合计 ${fmtAmount(totals.credit, true)}，差额 ${fmtAmount(Math.abs(totals.debit - totals.credit))}。`,
                        )
                    }
                >
                    试算平衡
                </Button>
                <input
                    ref={importInputRef}
                    type="file"
                    accept=".xlsx"
                    className="hidden"
                    onChange={(e) => void importFile(e.target.files?.[0] ?? null)}
                />
                <Button
                    icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                    disabled={loading}
                    onClick={() => void load()}
                >
                    刷新
                </Button>
                <Button
                    type="primary"
                    icon={saving ? <SaveOutlined spin /> : <SaveOutlined />}
                    disabled={!dirty || saving}
                    onClick={() => void save()}
                >
                    保存
                </Button>
            </div>

            {error && <Alert type="error" showIcon title={error} />}

            {savedTip && !dirty && (
                <Alert
                    type="success"
                    showIcon
                    title="期初余额已保存。"
                    action={
                        <Button
                            type="text"
                            title="关闭"
                            icon={<CloseOutlined />}
                            onClick={() => setSavedTip(false)}
                        />
                    }
                />
            )}

            <SectionCard bodyClassName="flex flex-col gap-4">
                <div
                    className={cn(
                        "flex flex-wrap items-center gap-2 rounded-xl border px-4 py-2.5 text-[12.5px]",
                        totals.balanced
                            ? "border-slate-200 bg-muted text-slate-500"
                            : "border-[#fecaca] bg-[#fef2f2] text-[#ef4444]",
                    )}
                >
                    {checkMessage && <span className="sr-only">{checkMessage}</span>}
                {totals.balanced ? (
                        <CheckCircleOutlined className="text-[16px]" />
                    ) : (
                        <WarningFilled className="text-[16px]" />
                    )}
                    <span>
                        {totals.balanced
                            ? `试算平衡：借方合计 ${fmtAmount(totals.debit, true)}，贷方合计 ${fmtAmount(totals.credit, true)}。`
                            : `试算不平衡：借方合计 ${fmtAmount(totals.debit, true)}，贷方合计 ${fmtAmount(totals.credit, true)}，差额 ${fmtAmount(Math.abs(totals.debit - totals.credit))}。`}
                    </span>
                </div>

                {loading && !data ? (
                    <div className="flex justify-center py-10">
                        <Spin />
                    </div>
                ) : (
                    <>
                        {/* 科目类别页签：与柠檬云「期初」一致（资产/负债/共同/权益/成本/损益） */}
                        <Tabs
                            activeKey={String(activeCategory)}
                            onChange={(key) => setActiveCategory(Number(key))}
                            items={CATEGORY_TABS.map((t) => ({ key: String(t.value), label: t.label }))}
                        />
                        <Table
                            columns={columns}
                            dataSource={(data?.rows ?? []).filter((row) => (row.category ?? 1) === activeCategory)}
                            rowKey={(row) => row.accountId ?? row.code ?? ""}
                            pagination={false}
                        />
                    </>
                )}
            </SectionCard>
        </div>
    )
}
