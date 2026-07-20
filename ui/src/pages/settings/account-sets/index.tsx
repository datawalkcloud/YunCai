import { useCallback, useEffect, useState } from "react"
import { Building2, Check, CheckCircle2, Loader2, Plus, RefreshCw, Trash2 } from "lucide-react"

import { useAuth } from "@/auth/AuthContext"
import { AccountSetsApi, type AccountSetDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
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

/** 会计准则枚举值（与后端 AccountingStandard 对应）及其显示名。 */
const STANDARDS: { value: number; label: string }[] = [
    { value: 1, label: "企业会计准则" },
    { value: 2, label: "小企业会计准则" },
    { value: 3, label: "民间非营利组织会计制度" },
]
/** 纳税人性质枚举值（与后端 TaxpayerType 对应）及其显示名。 */
const TAXPAYERS: { value: number; label: string }[] = [
    { value: 1, label: "一般纳税人" },
    { value: 2, label: "小规模纳税人" },
]

function standardLabel(value?: number): string {
    return STANDARDS.find((s) => s.value === value)?.label ?? "—"
}

function taxpayerLabel(value?: number): string {
    return TAXPAYERS.find((t) => t.value === value)?.label ?? "—"
}

const selectClassName = cn(
    "h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs outline-none",
    "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
    "disabled:pointer-events-none disabled:opacity-50 dark:bg-input/30",
)

function currentPeriod(): string {
    return new Date().toISOString().slice(0, 7)
}

/** 将 yyyy-MM 形式的启用期间拆成年、月。 */
function parsePeriod(period: string): { year: number; month: number } {
    const [y, m] = period.split("-")
    return { year: Number(y), month: Number(m) }
}

export default function AccountSetsPage() {
    const { token } = useAuth()

    const [sets, setSets] = useState<AccountSetDto[]>([])
    const [loading, setLoading] = useState(false)
    const [listError, setListError] = useState<string | null>(null)
    const [busyId, setBusyId] = useState<string | null>(null)

    // 新增表单字段。
    const [name, setName] = useState("")
    const [company, setCompany] = useState("")
    const [standard, setStandard] = useState(STANDARDS[0].value)
    const [currency, setCurrency] = useState("人民币")
    const [taxpayer, setTaxpayer] = useState(TAXPAYERS[0].value)
    const [startPeriod, setStartPeriod] = useState(currentPeriod())
    const [error, setError] = useState("")
    const [submitting, setSubmitting] = useState(false)
    const [open, setOpen] = useState(false)

    const load = useCallback(async () => {
        setLoading(true)
        setListError(null)
        try {
            const list = await new AccountSetsApi(apiConfig(token)).accountSetsGet()
            setSets(list)
        } catch (err) {
            setListError(await apiErrorMessage(err, "加载账套失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [token])

    useEffect(() => {
        void load()
    }, [load])

    async function setCurrent(id?: string) {
        if (!id || busyId) return
        setBusyId(id)
        setListError(null)
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdCurrentPost({ id })
            await load()
        } catch (err) {
            setListError(await apiErrorMessage(err, "设为当前账套失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    async function removeSet(set: AccountSetDto) {
        if (!set.id || busyId) return
        if (!window.confirm(`确定删除账套「${set.name}」吗？此操作不可撤销。`)) return
        setBusyId(set.id)
        setListError(null)
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdDelete({ id: set.id })
            await load()
        } catch (err) {
            setListError(await apiErrorMessage(err, "删除账套失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    function resetForm() {
        setName("")
        setCompany("")
        setStandard(STANDARDS[0].value)
        setCurrency("人民币")
        setTaxpayer(TAXPAYERS[0].value)
        setStartPeriod(currentPeriod())
        setError("")
    }

    async function addSet() {
        const trimmedName = name.trim()
        const trimmedCompany = company.trim()
        if (!trimmedName || !trimmedCompany) {
            setError("请填写账套名称和单位名称。")
            return
        }
        const { year, month } = parsePeriod(startPeriod)
        if (!year || !month) {
            setError("请选择有效的启用期间。")
            return
        }
        setSubmitting(true)
        setError("")
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsPost({
                createAccountSetRequest: {
                    name: trimmedName,
                    companyName: trimmedCompany,
                    standard,
                    taxpayerType: taxpayer,
                    baseCurrency: currency.trim() || "人民币",
                    startYear: year,
                    startMonth: month,
                },
            })
            resetForm()
            setOpen(false)
            await load()
        } catch (err) {
            setError(await apiErrorMessage(err, "创建账套失败，请重试。"))
        } finally {
            setSubmitting(false)
        }
    }

    function handleOpenChange(next: boolean) {
        setOpen(next)
        if (!next) setError("")
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex flex-col gap-1">
                    <h1 className="text-2xl font-bold">账套</h1>
                    <p className="text-sm text-muted-foreground">
                        管理各单位的账簿账套，可在多套账之间切换当前账套。
                    </p>
                </div>
                <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading}>
                    <RefreshCw className={cn(loading && "animate-spin")} />
                    刷新
                </Button>
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
                    {listError && (
                        <div className="mb-4 rounded-md border border-destructive/50 bg-destructive/5 px-3 py-2 text-sm text-destructive">
                            {listError}
                        </div>
                    )}
                    {loading && sets.length === 0 ? (
                        <div className="flex items-center justify-center gap-2 py-10 text-sm text-muted-foreground">
                            <Loader2 className="size-4 animate-spin" />
                            加载中…
                        </div>
                    ) : (
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
                                                <span className="text-xs text-muted-foreground">{set.companyName}</span>
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
                                        <dd className="text-right text-foreground">{standardLabel(set.standard)}</dd>
                                        <dt>本位币</dt>
                                        <dd className="text-right text-foreground">{set.baseCurrency}</dd>
                                        <dt>纳税人性质</dt>
                                        <dd className="text-right text-foreground">{taxpayerLabel(set.taxpayerType)}</dd>
                                        <dt>启用期间</dt>
                                        <dd className="text-right tabular-nums text-foreground">{set.startPeriod}</dd>
                                    </dl>

                                    <div className="mt-auto flex items-center justify-between gap-2 pt-1">
                                        <Button
                                            variant={set.isCurrent ? "secondary" : "outline"}
                                            size="sm"
                                            disabled={set.isCurrent || busyId === set.id}
                                            onClick={() => void setCurrent(set.id)}
                                        >
                                            {busyId === set.id ? <Loader2 className="animate-spin" /> : <Check />}
                                            {set.isCurrent ? "当前账套" : "设为当前"}
                                        </Button>
                                        <Button
                                            variant="ghost"
                                            size="icon-sm"
                                            disabled={set.isCurrent || busyId === set.id}
                                            onClick={() => void removeSet(set)}
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
                    )}
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
                                onChange={(e) => setStandard(Number(e.target.value))}
                            >
                                {STANDARDS.map((s) => (
                                    <option key={s.value} value={s.value}>
                                        {s.label}
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
                                onChange={(e) => setTaxpayer(Number(e.target.value))}
                            >
                                {TAXPAYERS.map((t) => (
                                    <option key={t.value} value={t.value}>
                                        {t.label}
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
                        <Button onClick={() => void addSet()} disabled={submitting}>
                            {submitting ? <Loader2 className="animate-spin" /> : <Plus />}
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
