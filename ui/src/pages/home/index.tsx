import { useNavigate, useParams } from "react-router"
import { BookOpen, FileText, ListTree, Scale, Table2, Upload } from "lucide-react"
import { Card, Col, Row, Typography, theme } from "antd"

import { useAuth } from "@/auth/AuthContext"
import { useAccountSets } from "@/accountSets/AccountSetsContext"

const { Title, Text } = Typography

/** 常用功能入口定义。 */
const shortcuts = [
    {
        title: "凭证管理",
        description: "录入、编辑、查看记账凭证",
        path: "vouchers/manage",
        icon: FileText,
        color: "#27bd51",
    },
    {
        title: "导入凭证",
        description: "从 Excel 批量导入历史凭证",
        path: "vouchers/manage",
        icon: Upload,
        color: "#1677ff",
        hash: "#import",
    },
    {
        title: "科目余额表",
        description: "按期间查看各科目发生额与余额",
        path: "ledger/trial-balance",
        icon: Table2,
        color: "#722ed1",
    },
    {
        title: "现金日记账",
        description: "查看库存现金序时流水",
        path: "funds/cash-journal",
        icon: BookOpen,
        color: "#fa8c16",
    },
    {
        title: "资产负债表",
        description: "查看标准报表",
        path: "reports/balance-sheet",
        icon: Scale,
        color: "#13c2c2",
    },
    {
        title: "科目管理",
        description: "维护会计科目与账套设置",
        path: "settings/accounts",
        icon: ListTree,
        color: "#eb2f96",
    },
]

export default function Home() {
    const { user } = useAuth()
    const { accountSets } = useAccountSets()
    const navigate = useNavigate()
    const accountSetId = useParams().accountSetId ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { token } = theme.useToken()

    const base = `/${accountSetId}`

    return (
        <div className="flex flex-col gap-6">
            {/* 欢迎区 */}
            <div className="flex flex-col gap-1">
                <Title level={3} style={{ margin: 0 }}>
                    欢迎回来，{user?.displayName ?? "用户"}
                </Title>
                <Text type="secondary">
                    当前账套：{currentSet?.name ?? "未选择"}
                    {currentSet?.isCurrent ? "（当前）" : ""}
                </Text>
            </div>

            {/* 常用功能 */}
            <div className="flex flex-col gap-3">
                <Title level={5} style={{ margin: 0 }}>常用功能</Title>
                <Row gutter={[16, 16]}>
                    {shortcuts.map((s) => {
                        const Icon = s.icon
                        return (
                            <Col xs={12} sm={8} lg={6} xl={4} key={s.title + (s.hash ?? "")}>
                                <Card
                                    hoverable
                                    onClick={() => navigate(`${base}/${s.path}${s.hash ?? ""}`)}
                                    styles={{ body: { padding: "20px 16px" } }}
                                >
                                    <div className="flex flex-col items-center gap-3 text-center">
                                        <span
                                            className="flex size-11 shrink-0 items-center justify-center rounded-xl"
                                            style={{
                                                background: `${s.color}14`,
                                                color: s.color,
                                            }}
                                        >
                                            <Icon className="size-5" />
                                        </span>
                                        <div className="flex flex-col gap-0.5">
                                            <Text strong>{s.title}</Text>
                                            <Text
                                                type="secondary"
                                                style={{ fontSize: 12, lineHeight: 1.4 }}
                                            >
                                                {s.description}
                                            </Text>
                                        </div>
                                    </div>
                                </Card>
                            </Col>
                        )
                    })}
                </Row>
            </div>

            {/* 快捷信息 */}
            <Row gutter={[16, 16]}>
                <Col xs={24} md={12}>
                    <Card
                        title="账套信息"
                        size="small"
                        extra={
                            <a
                                onClick={() => navigate(`${base}/settings/account-sets`)}
                                style={{ cursor: "pointer", color: token.colorPrimary }}
                            >
                                查看详情
                            </a>
                        }
                    >
                        <div className="flex flex-col gap-2">
                            <InfoRow label="账套名称" value={currentSet?.name ?? "—"} />
                            <InfoRow label="公司名称" value={currentSet?.companyName ?? "—"} />
                            <InfoRow label="本位币" value={currentSet?.baseCurrency ?? "—"} />
                        </div>
                    </Card>
                </Col>
                <Col xs={24} md={12}>
                    <Card
                        title="设置"
                        size="small"
                        extra={
                            <a
                                onClick={() => navigate(`${base}/settings/accounts`)}
                                style={{ cursor: "pointer", color: token.colorPrimary }}
                            >
                                管理科目
                            </a>
                        }
                    >
                        <div className="flex flex-col gap-2">
                            <InfoRow label="启用期间" value={currentSet?.startPeriod ?? "—"} />
                        </div>
                    </Card>
                </Col>
            </Row>
        </div>
    )
}

function InfoRow({ label, value }: { label: string; value: string }) {
    return (
        <div className="flex items-center justify-between">
            <Text type="secondary">{label}</Text>
            <Text>{value}</Text>
        </div>
    )
}
