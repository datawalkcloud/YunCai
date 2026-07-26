import { useCallback, useEffect, useMemo, useState } from "react"
import { RefreshCw, Save } from "lucide-react"
import { Alert, Button, Card, InputNumber, Space, Spin, Table, Tag, Typography } from "antd"
import type { ColumnsType } from "antd/es/table"

import { useAuth } from "@/auth/AuthContext"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { AccountsApi, type OpeningBalanceRowDto, type OpeningBalancesDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

const { Title, Text } = Typography

/** 金额展示：0 显示为空，避免表格满屏 0.00。 */
function fmt(value?: number): string {
    if (!value) return ""
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
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
    // 本地未保存的修改：accountId -> 期初余额。
    const [edits, setEdits] = useState<Record<string, number>>({})

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

    const columns: ColumnsType<OpeningBalanceRowDto> = [
        {
            title: "科目编码",
            dataIndex: "code",
            width: 140,
            render: (code: string, row) => (
                <span style={{ paddingLeft: ((row.level ?? 1) - 1) * 16 }}>{code}</span>
            ),
        },
        {
            title: "科目名称",
            dataIndex: "name",
            render: (name: string, row) => (row.isLeaf ? name : <Text strong>{name}</Text>),
        },
        {
            title: "方向",
            dataIndex: "direction",
            width: 70,
            align: "center",
            render: (direction: number) => (
                <Tag color={direction === 1 ? "blue" : "orange"}>{direction === 1 ? "借" : "贷"}</Tag>
            ),
        },
        {
            title: "期初余额（按方向）",
            dataIndex: "openingBalance",
            width: 190,
            align: "right",
            render: (_: number, row) => {
                if (!row.isLeaf || !row.accountId) {
                    return <Text type="secondary">{fmt(row.openingBalance)}</Text>
                }
                const id = row.accountId
                return (
                    <InputNumber
                        size="small"
                        style={{ width: 160 }}
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
            title: "借方",
            dataIndex: "debit",
            width: 130,
            align: "right",
            render: (value: number) => fmt(value),
        },
        {
            title: "贷方",
            dataIndex: "credit",
            width: 130,
            align: "right",
            render: (value: number) => fmt(value),
        },
    ]

    return (
        <Space direction="vertical" size="large" style={{ display: "flex" }}>
            <div style={{ display: "flex", flexWrap: "wrap", alignItems: "flex-start", justifyContent: "space-between", gap: 12 }}>
                <div>
                    <Title level={3} style={{ margin: 0 }}>期初余额</Title>
                    <Text type="secondary">录入账套启用期间的科目期初余额；末级科目按余额方向录入，反方向余额用负数。</Text>
                </div>
                <Space>
                    <Button icon={<RefreshCw size={16} className={loading ? "spin" : undefined} />} onClick={() => void load()} loading={loading}>
                        刷新
                    </Button>
                    <Button type="primary" icon={<Save size={16} />} disabled={!dirty} loading={saving} onClick={() => void save()}>
                        保存
                    </Button>
                </Space>
            </div>

            {error && <Alert type="error" showIcon message={error} />}
            {savedTip && !dirty && <Alert type="success" showIcon message="期初余额已保存。" closable onClose={() => setSavedTip(false)} />}

            <Alert
                type={totals.balanced ? "success" : "warning"}
                showIcon
                message={
                    totals.balanced
                        ? `试算平衡：借方合计 ${fmt(totals.debit) || "0.00"}，贷方合计 ${fmt(totals.credit) || "0.00"}。`
                        : `试算不平衡：借方合计 ${fmt(totals.debit) || "0.00"}，贷方合计 ${fmt(totals.credit) || "0.00"}，差额 ${fmt(Math.abs(totals.debit - totals.credit))}。`
                }
            />

            <Card>
                {loading && !data ? (
                    <div style={{ display: "flex", justifyContent: "center", padding: "40px 0" }}>
                        <Spin />
                    </div>
                ) : (
                    <Table<OpeningBalanceRowDto>
                        rowKey={(row) => row.accountId ?? row.code ?? ""}
                        columns={columns}
                        dataSource={data?.rows ?? []}
                        size="small"
                        pagination={false}
                        scroll={{ x: 900 }}
                    />
                )}
            </Card>
        </Space>
    )
}
