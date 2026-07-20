import { useRef, useState } from "react"
import { Building2, Check, CheckCircle2, Plus, Trash2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
    Sheet,
    SheetClose,
    SheetContent,
    SheetDescription,
    SheetFooter,
    SheetHeader,
    SheetTitle,
} from "@/components/ui/sheet"
import { cn } from "@/lib/utils"

/** 会计准则。 */
type AccountingStandard = "企业会计准则" | "小企业会计准则" | "民间非营利组织会计制度"
/** 纳税人性质。 */
type TaxpayerType = "一般纳税人" | "小规模纳税人"

/** 账套：一家单位的一套完整账簿。 */
interface AccountSet {
    id: number
    name: string
    company: string
    standard: AccountingStandard
    currency: string
    taxpayer: TaxpayerType
    startPeriod: string
    isCurrent: boolean
}

const STANDARDS: AccountingStandard[] = [
    "企业会计准则",
    "小企业会计准则",
    "民间非营利组织会计制度",
]
const TAXPAYERS: TaxpayerType[] = ["一般纳税人", "小规模纳税人"]

/** 示例账套（后续接入账套接口后替换）。 */
const INITIAL_SETS: AccountSet[] = [
    {
        id: 1,
        name: "华信科技 2026",
        company: "华信科技有限公司",
        standard: "企业会计准则",
        currency: "人民币",
        taxpayer: "一般纳税人",
        startPeriod: "2026-01",
        isCurrent: true,
    },
    {
        id: 2,
        name: "远大商贸 2025",
        company: "远大商贸有限公司",
        standard: "小企业会计准则",
        currency: "人民币",
        taxpayer: "小规模纳税人",
        startPeriod: "2025-01",
        isCurrent: false,
    },
]

const selectClassName = cn(
    "h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs outline-none",
    "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
    "disabled:pointer-events-none disabled:opacity-50 dark:bg-input/30",
)

function currentPeriod(): string {
    return new Date().toISOString().slice(0, 7)
}

export default function AccountSetsPage() {
    const nextId = useRef(INITIAL_SETS.length + 1)
    const [sets, setSets] = useState<AccountSet[]>(INITIAL_SETS)

    // 新增表单字段。
    const [name, setName] = useState("")
    const [company, setCompany] = useState("")
    const [standard, setStandard] = useState<AccountingStandard>("企业会计准则")
    const [currency, setCurrency] = useState("人民币")
    const [taxpayer, setTaxpayer] = useState<TaxpayerType>("一般纳税人")
    const [startPeriod, setStartPeriod] = useState(currentPeriod())
    const [error, setError] = useState("")
    const [open, setOpen] = useState(false)

    function setCurrent(id: number) {
        setSets((prev) => prev.map((s) => ({ ...s, isCurrent: s.id === id })))
    }

    function removeSet(id: number) {
        setSets((prev) => prev.filter((s) => s.id !== id))
    }

    function resetForm() {
        setName("")
        setCompany("")
        setStandard("企业会计准则")
        setCurrency("人民币")
        setTaxpayer("一般纳税人")
        setStartPeriod(currentPeriod())
        setError("")
    }

    function addSet() {
        const trimmedName = name.trim()
        const trimmedCompany = company.trim()
        if (!trimmedName || !trimmedCompany) {
            setError("请填写账套名称和单位名称。")
            return
        }
        if (sets.some((s) => s.name === trimmedName)) {
            setError("账套名称已存在。")
            return
        }
        const id = nextId.current++
        setSets((prev) => [
            ...prev,
            {
                id,
                name: trimmedName,
                company: trimmedCompany,
                standard,
                currency: currency.trim() || "人民币",
                taxpayer,
                startPeriod,
                // 首个账套自动设为当前账套。
                isCurrent: prev.length === 0,
            },
        ])
        resetForm()
        setOpen(false)
    }

    function handleOpenChange(next: boolean) {
        setOpen(next)
        if (!next) setError("")
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">账套</h1>
                <p className="text-sm text-muted-foreground">
                    管理各单位的账簿账套，可在多套账之间切换当前账套。
                </p>
            </div>

            <Card>
                <CardHeader className="flex-row items-center justify-between gap-2">
                    <CardTitle>账套列表</CardTitle>
                    <Button size="sm" onClick={() => setOpen(true)}>
                        <Plus />
                        新增账套
                    </Button>
                </CardHeader>
                <CardContent>
                    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                        {sets.map((set) => (
                            <div
                                key={set.id}
                                className={cn(
                                    "flex flex-col gap-3 rounded-lg border p-4 transition-colors",
                                    set.isCurrent ? "border-primary bg-primary/5" : "hover:bg-muted/40",
                                )}
                            >
                                <div className="flex items-start justify-between gap-2">
                                    <div className="flex items-center gap-2">
                                        <div className="flex size-9 items-center justify-center rounded-md bg-muted text-muted-foreground">
                                            <Building2 className="size-5" />
                                        </div>
                                        <div className="flex flex-col">
                                            <span className="font-medium">{set.name}</span>
                                            <span className="text-xs text-muted-foreground">{set.company}</span>
                                        </div>
                                    </div>
                                    {set.isCurrent && (
                                        <span className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary">
                                            <CheckCircle2 className="size-3" />
                                            当前
                                        </span>
                                    )}
                                </div>

                                <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-xs text-muted-foreground">
                                    <dt>会计准则</dt>
                                    <dd className="text-right text-foreground">{set.standard}</dd>
                                    <dt>本位币</dt>
                                    <dd className="text-right text-foreground">{set.currency}</dd>
                                    <dt>纳税人性质</dt>
                                    <dd className="text-right text-foreground">{set.taxpayer}</dd>
                                    <dt>启用期间</dt>
                                    <dd className="text-right tabular-nums text-foreground">{set.startPeriod}</dd>
                                </dl>

                                <div className="mt-auto flex items-center justify-between gap-2 pt-1">
                                    <Button
                                        variant={set.isCurrent ? "secondary" : "outline"}
                                        size="sm"
                                        disabled={set.isCurrent}
                                        onClick={() => setCurrent(set.id)}
                                    >
                                        <Check />
                                        {set.isCurrent ? "当前账套" : "设为当前"}
                                    </Button>
                                    <Button
                                        variant="ghost"
                                        size="icon-sm"
                                        disabled={set.isCurrent}
                                        onClick={() => removeSet(set.id)}
                                        title={set.isCurrent ? "当前账套不可删除" : "删除账套"}
                                    >
                                        <Trash2 />
                                    </Button>
                                </div>
                            </div>
                        ))}
                        {sets.length === 0 && (
                            <div className="col-span-full rounded-lg border border-dashed py-10 text-center text-sm text-muted-foreground">
                                暂无账套，点击“新增账套”创建
                            </div>
                        )}
                    </div>
                </CardContent>
            </Card>

            <Sheet open={open} onOpenChange={handleOpenChange}>
                <SheetContent side="right" className="w-full sm:max-w-md">
                    <SheetHeader>
                        <SheetTitle>新增账套</SheetTitle>
                        <SheetDescription>填写单位与核算信息后点击“创建账套”保存。</SheetDescription>
                    </SheetHeader>
                    <div className="flex flex-1 flex-col gap-4 overflow-y-auto px-4">
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="set-name">账套名称</Label>
                            <Input
                                id="set-name"
                                placeholder="如 华信科技 2026"
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                            />
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="set-company">单位名称</Label>
                            <Input
                                id="set-company"
                                placeholder="如 华信科技有限公司"
                                value={company}
                                onChange={(e) => setCompany(e.target.value)}
                            />
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="set-standard">会计准则</Label>
                            <select
                                id="set-standard"
                                className={selectClassName}
                                value={standard}
                                onChange={(e) => setStandard(e.target.value as AccountingStandard)}
                            >
                                {STANDARDS.map((s) => (
                                    <option key={s} value={s}>
                                        {s}
                                    </option>
                                ))}
                            </select>
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="set-currency">记账本位币</Label>
                            <Input
                                id="set-currency"
                                placeholder="人民币"
                                value={currency}
                                onChange={(e) => setCurrency(e.target.value)}
                            />
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="set-taxpayer">纳税人性质</Label>
                            <select
                                id="set-taxpayer"
                                className={selectClassName}
                                value={taxpayer}
                                onChange={(e) => setTaxpayer(e.target.value as TaxpayerType)}
                            >
                                {TAXPAYERS.map((t) => (
                                    <option key={t} value={t}>
                                        {t}
                                    </option>
                                ))}
                            </select>
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="set-period">启用期间</Label>
                            <Input
                                id="set-period"
                                type="month"
                                value={startPeriod}
                                onChange={(e) => setStartPeriod(e.target.value)}
                            />
                        </div>
                        {error && <span className="text-sm text-destructive">{error}</span>}
                    </div>
                    <SheetFooter>
                        <Button onClick={addSet}>
                            <Plus />
                            创建账套
                        </Button>
                        <SheetClose asChild>
                            <Button variant="outline">取消</Button>
                        </SheetClose>
                    </SheetFooter>
                </SheetContent>
            </Sheet>
        </div>
    )
}
