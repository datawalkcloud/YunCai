import { useNavigate, useParams } from "react-router"
import { BookOpen, FileText, ListTree, Scale, Table2, Upload } from "lucide-react"

import { useAuth } from "@/auth/AuthContext"
import { useAccountSets } from "@/accountSets/AccountSetsContext"
import { Button } from "@/components/ui/button"
import {
    Card,
    CardAction,
    CardContent,
    CardHeader,
    CardTitle,
} from "@/components/ui/card"

/** 常用功能入口定义。 */
const shortcuts = [
    {
        title: "凭证管理",
        description: "录入、编辑、查看记账凭证",
        path: "vouchers/manage",
        icon: FileText,
    },
    {
        title: "导入凭证",
        description: "从 Excel 批量导入历史凭证",
        path: "vouchers/manage",
        icon: Upload,
        hash: "#import",
    },
    {
        title: "科目余额表",
        description: "按期间查看各科目发生额与余额",
        path: "ledger/trial-balance",
        icon: Table2,
    },
    {
        title: "现金日记账",
        description: "查看库存现金序时流水",
        path: "funds/cash-journal",
        icon: BookOpen,
    },
    {
        title: "资产负债表",
        description: "查看标准报表",
        path: "reports/balance-sheet",
        icon: Scale,
    },
    {
        title: "科目管理",
        description: "维护会计科目与账套设置",
        path: "settings/accounts",
        icon: ListTree,
    },
]

export default function Home() {
    const { user } = useAuth()
    const { accountSets } = useAccountSets()
    const navigate = useNavigate()
    const accountSetId = useParams().accountSetId ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const base = `/${accountSetId}`

    return (
        <div className="flex flex-col gap-6">
            {/* 欢迎区 */}
            <div className="flex flex-col gap-1">
                <h1 className="text-lg font-semibold">
                    欢迎回来，{user?.displayName ?? "用户"}
                </h1>
                <span className="text-muted-foreground">
                    当前账套：{currentSet?.name ?? "未选择"}
                    {currentSet?.isCurrent ? "（当前）" : ""}
                </span>
            </div>

            {/* 常用功能 */}
            <div className="flex flex-col gap-3">
                <h2 className="text-sm font-medium">常用功能</h2>
                <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
                    {shortcuts.map((s) => {
                        const Icon = s.icon
                        return (
                            <Card
                                key={s.title + (s.hash ?? "")}
                                className="cursor-pointer"
                                onClick={() => navigate(`${base}/${s.path}${s.hash ?? ""}`)}
                            >
                                <CardContent>
                                    <div className="flex flex-col items-center gap-3 text-center">
                                        <span className="bg-muted text-primary flex size-11 shrink-0 items-center justify-center rounded-xl">
                                            <Icon className="size-5" />
                                        </span>
                                        <div className="flex flex-col gap-0.5">
                                            <span className="font-medium">{s.title}</span>
                                            <span className="text-muted-foreground">
                                                {s.description}
                                            </span>
                                        </div>
                                    </div>
                                </CardContent>
                            </Card>
                        )
                    })}
                </div>
            </div>

            {/* 快捷信息 */}
            <div className="grid gap-4 md:grid-cols-2">
                <Card size="sm">
                    <CardHeader>
                        <CardTitle>账套信息</CardTitle>
                        <CardAction>
                            <Button
                                variant="link"
                                onClick={() => navigate(`${base}/settings/account-sets`)}
                            >
                                查看详情
                            </Button>
                        </CardAction>
                    </CardHeader>
                    <CardContent className="flex flex-col gap-2">
                        <InfoRow label="账套名称" value={currentSet?.name ?? "—"} />
                        <InfoRow label="公司名称" value={currentSet?.companyName ?? "—"} />
                        <InfoRow label="本位币" value={currentSet?.baseCurrency ?? "—"} />
                    </CardContent>
                </Card>
                <Card size="sm">
                    <CardHeader>
                        <CardTitle>设置</CardTitle>
                        <CardAction>
                            <Button
                                variant="link"
                                onClick={() => navigate(`${base}/settings/accounts`)}
                            >
                                管理科目
                            </Button>
                        </CardAction>
                    </CardHeader>
                    <CardContent className="flex flex-col gap-2">
                        <InfoRow label="启用期间" value={currentSet?.startPeriod ?? "—"} />
                    </CardContent>
                </Card>
            </div>
        </div>
    )
}

function InfoRow({ label, value }: { label: string; value: string }) {
    return (
        <div className="flex items-center justify-between">
            <span className="text-muted-foreground">{label}</span>
            <span>{value}</span>
        </div>
    )
}
