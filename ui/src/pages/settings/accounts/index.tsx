import { useCallback, useEffect, useMemo, useState } from "react"
import { ChevronDown, ChevronRight, Loader2, Plus, RefreshCw, Trash2 } from "lucide-react"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { AccountsApi, type AccountDto } from "@/apis-gen"
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

/** 会计科目类别（数值对应后端枚举 AccountCategory）。 */
const CATEGORY_OPTIONS = [
    { value: 1, label: "资产" },
    { value: 2, label: "负债" },
    { value: 3, label: "权益" },
    { value: 4, label: "成本" },
    { value: 5, label: "损益" },
] as const

/** 余额方向（数值对应后端枚举 BalanceDirection）。 */
const DIRECTION_OPTIONS = [
    { value: 1, label: "借" },
    { value: 2, label: "贷" },
] as const

const CATEGORY_LABELS: Record<number, string> = Object.fromEntries(
    CATEGORY_OPTIONS.map((c) => [c.value, c.label]),
)
const DIRECTION_LABELS: Record<number, string> = Object.fromEntries(
    DIRECTION_OPTIONS.map((d) => [d.value, d.label]),
)

const selectClassName = cn(
    "h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs outline-none",
    "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
    "disabled:pointer-events-none disabled:opacity-50 dark:bg-input/30",
)

/** 展开树时携带的层级与是否有子节点信息。 */
interface FlatRow {
    account: AccountDto
    level: number
    hasChildren: boolean
}

export default function AccountsPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [loading, setLoading] = useState(false)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [filter, setFilter] = useState<number | "全部">("全部")
    const [expanded, setExpanded] = useState<Set<string>>(new Set())
    const [deletingId, setDeletingId] = useState<string | null>(null)

    // 新增表单字段。
    const [code, setCode] = useState("")
    const [name, setName] = useState("")
    const [category, setCategory] = useState<number>(1)
    const [direction, setDirection] = useState<number>(1)
    const [parentId, setParentId] = useState<string | null>(null)
    const [error, setError] = useState("")
    const [saving, setSaving] = useState(false)
    const [open, setOpen] = useState(false)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setLoadError(null)
        try {
            const list = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({ accountSetId })
            setAccounts(list)
            // 默认展开所有含下级的科目，便于一览多级结构。
            setExpanded(new Set(list.filter((a) => a.parentId != null).map((a) => a.parentId as string)))
        } catch (err) {
            setLoadError(await apiErrorMessage(err, "加载科目失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void load()
    }, [load])

    const parent = useMemo(
        () => (parentId === null ? null : accounts.find((a) => a.id === parentId) ?? null),
        [accounts, parentId],
    )

    // 按父子关系展开成带层级的扁平行（顶级按类别筛选，明细随父级展开）。
    const rows = useMemo(() => {
        const byCode = (a: AccountDto, b: AccountDto) => (a.code ?? "").localeCompare(b.code ?? "")
        const out: FlatRow[] = []
        const walk = (pid: string | null, level: number) => {
            const children = accounts.filter((a) => (a.parentId ?? null) === pid).sort(byCode)
            for (const acc of children) {
                if (pid === null && filter !== "全部" && acc.category !== filter) continue
                const hasChildren = accounts.some((a) => a.parentId === acc.id)
                out.push({ account: acc, level, hasChildren })
                if (hasChildren && acc.id && expanded.has(acc.id)) walk(acc.id, level + 1)
            }
        }
        walk(null, 0)
        return out
    }, [accounts, filter, expanded])

    // 上级科目下拉可选项（按编码排序，展示编码 + 名称）。
    const parentOptions = useMemo(
        () => [...accounts].sort((a, b) => (a.code ?? "").localeCompare(b.code ?? "")),
        [accounts],
    )

    function toggleExpand(id: string) {
        setExpanded((prev) => {
            const next = new Set(prev)
            if (next.has(id)) next.delete(id)
            else next.add(id)
            return next
        })
    }

    async function addAccount() {
        if (saving) return
        const trimmedCode = code.trim()
        const trimmedName = name.trim()
        if (!trimmedCode || !trimmedName) {
            setError("请填写科目编码和名称。")
            return
        }
        if (accounts.some((a) => a.code === trimmedCode)) {
            setError("科目编码已存在。")
            return
        }
        setSaving(true)
        setError("")
        try {
            await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsPost({
                accountSetId,
                // 明细科目的类别与方向由后端继承上级，这里仍按顶级取值传入（后端忽略）。
                createAccountRequest: {
                    parentId,
                    code: trimmedCode,
                    name: trimmedName,
                    category,
                    direction,
                },
            })
            if (parentId !== null) setExpanded((prev) => new Set(prev).add(parentId))
            setCode("")
            setName("")
            setCategory(1)
            setDirection(1)
            setParentId(null)
            setOpen(false)
            await load()
        } catch (err) {
            setError(await apiErrorMessage(err, "新增科目失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    function handleOpenChange(next: boolean) {
        setOpen(next)
        if (!next) setError("")
    }

    async function removeAccount(account: AccountDto) {
        if (deletingId || !account.id) return
        if (!window.confirm(`确定删除科目「${account.code} ${account.name}」吗？此操作不可撤销。`)) return
        setDeletingId(account.id)
        setLoadError(null)
        try {
            await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsIdDelete({
                accountSetId,
                id: account.id,
            })
            await load()
        } catch (err) {
            setLoadError(await apiErrorMessage(err, "删除科目失败，请重试。"))
        } finally {
            setDeletingId(null)
        }
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">科目</h1>
                <p className="text-sm text-muted-foreground">
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    。维护多级会计科目表，一级科目下可挂明细科目，用于凭证录入与账簿核算。
                </p>
            </div>

            <Card>
                <CardHeader className="flex-row items-center justify-between gap-2">
                    <CardTitle>科目列表</CardTitle>
                    <div className="flex items-center gap-2">
                        <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || !accountSetId}>
                            <RefreshCw className={cn(loading && "animate-spin")} />
                            刷新
                        </Button>
                        <Button size="sm" onClick={() => setOpen(true)} disabled={!accountSetId}>
                            <Plus />
                            新增科目
                        </Button>
                    </div>
                </CardHeader>
                <CardContent className="flex flex-col gap-4">
                    {loadError && <p className="text-sm text-destructive">{loadError}</p>}

                    {/* 类别 Tab 切换 */}
                    <div className="inline-flex w-fit items-center gap-1 rounded-lg bg-muted p-1 text-muted-foreground">
                        {(["全部", ...CATEGORY_OPTIONS.map((c) => c.value)] as const).map((tab) => (
                            <button
                                key={tab}
                                type="button"
                                onClick={() => setFilter(tab)}
                                className={cn(
                                    "rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                    filter === tab
                                        ? "bg-background text-foreground shadow-sm"
                                        : "hover:text-foreground",
                                )}
                            >
                                {tab === "全部" ? "全部" : CATEGORY_LABELS[tab]}
                            </button>
                        ))}
                    </div>

                    <div className="overflow-x-auto rounded-md border">
                        <table className="w-full text-sm">
                            <thead>
                                <tr className="border-b bg-muted/50 text-left text-muted-foreground">
                                    <th className="px-3 py-2 font-medium">科目编码</th>
                                    <th className="px-3 py-2 font-medium">科目名称</th>
                                    <th className="px-3 py-2 font-medium">类别</th>
                                    <th className="px-3 py-2 font-medium">余额方向</th>
                                    <th className="w-12 px-3 py-2" />
                                </tr>
                            </thead>
                            <tbody>
                                {loading && accounts.length === 0 ? (
                                    <tr>
                                        <td className="px-3 py-10 text-center text-muted-foreground" colSpan={5}>
                                            <Loader2 className="mx-auto size-5 animate-spin" />
                                        </td>
                                    </tr>
                                ) : (
                                    rows.map(({ account, level, hasChildren }) => (
                                        <tr key={account.id} className="border-b last:border-b-0 hover:bg-muted/40">
                                            <td className="px-3 py-2 tabular-nums">{account.code}</td>
                                            <td className="px-3 py-2">
                                                <div className="flex items-center" style={{ paddingLeft: `${level * 1.25}rem` }}>
                                                    {hasChildren && account.id ? (
                                                        <button
                                                            type="button"
                                                            onClick={() => toggleExpand(account.id as string)}
                                                            className="mr-1 flex size-5 items-center justify-center rounded text-muted-foreground hover:bg-muted hover:text-foreground"
                                                            title={expanded.has(account.id) ? "折叠" : "展开"}
                                                        >
                                                            {expanded.has(account.id) ? (
                                                                <ChevronDown className="size-4" />
                                                            ) : (
                                                                <ChevronRight className="size-4" />
                                                            )}
                                                        </button>
                                                    ) : (
                                                        <span className="mr-1 inline-block size-5" />
                                                    )}
                                                    <span className={cn(level === 0 && "font-medium")}>{account.name}</span>
                                                </div>
                                            </td>
                                            <td className="px-3 py-2">{CATEGORY_LABELS[account.category ?? 0] ?? "—"}</td>
                                            <td className="px-3 py-2">{DIRECTION_LABELS[account.direction ?? 0] ?? "—"}</td>
                                            <td className="px-3 py-2 text-center">
                                                <Button
                                                    variant="ghost"
                                                    size="icon-sm"
                                                    onClick={() => void removeAccount(account)}
                                                    disabled={deletingId === account.id}
                                                    title={hasChildren ? "删除科目（需先删除下级）" : "删除科目"}
                                                >
                                                    {deletingId === account.id ? (
                                                        <Loader2 className="animate-spin" />
                                                    ) : (
                                                        <Trash2 />
                                                    )}
                                                </Button>
                                            </td>
                                        </tr>
                                    ))
                                )}
                                {!loading && rows.length === 0 && (
                                    <tr>
                                        <td className="px-3 py-6 text-center text-muted-foreground" colSpan={5}>
                                            {accountSetId ? "暂无科目，可点击「新增科目」或通过凭证导入自动建立。" : "请先选择账套。"}
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>

            <Sheet open={open} onOpenChange={handleOpenChange}>
                <SheetContent side="right" className="w-full sm:max-w-md">
                    <SheetHeader>
                        <SheetTitle>新增科目</SheetTitle>
                        <SheetDescription>选择上级科目可创建明细科目，其类别与方向自动继承上级。</SheetDescription>
                    </SheetHeader>
                    <div className="flex flex-1 flex-col gap-4 overflow-y-auto px-4">
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="account-parent">上级科目</Label>
                            <select
                                id="account-parent"
                                className={selectClassName}
                                value={parentId ?? ""}
                                onChange={(e) => setParentId(e.target.value === "" ? null : e.target.value)}
                            >
                                <option value="">（顶级科目）</option>
                                {parentOptions.map((a) => (
                                    <option key={a.id} value={a.id}>
                                        {a.code} {a.name}
                                    </option>
                                ))}
                            </select>
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="account-code">科目编码</Label>
                            <Input
                                id="account-code"
                                placeholder={parent ? `如 ${parent.code}01` : "如 1001"}
                                value={code}
                                onChange={(e) => setCode(e.target.value)}
                            />
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="account-name">科目名称</Label>
                            <Input
                                id="account-name"
                                placeholder="如 库存现金"
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                            />
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="account-category">类别</Label>
                            <select
                                id="account-category"
                                className={selectClassName}
                                value={parent ? parent.category : category}
                                disabled={parent !== null}
                                onChange={(e) => setCategory(Number(e.target.value))}
                            >
                                {CATEGORY_OPTIONS.map((c) => (
                                    <option key={c.value} value={c.value}>
                                        {c.label}
                                    </option>
                                ))}
                            </select>
                        </div>
                        <div className="flex flex-col gap-2">
                            <Label htmlFor="account-direction">余额方向</Label>
                            <select
                                id="account-direction"
                                className={selectClassName}
                                value={parent ? parent.direction : direction}
                                disabled={parent !== null}
                                onChange={(e) => setDirection(Number(e.target.value))}
                            >
                                {DIRECTION_OPTIONS.map((d) => (
                                    <option key={d.value} value={d.value}>
                                        {d.label}
                                    </option>
                                ))}
                            </select>
                        </div>
                        {parent && (
                            <span className="text-xs text-muted-foreground">
                                将作为「{parent.code} {parent.name}」的明细科目，类别与方向已继承上级。
                            </span>
                        )}
                        {error && <span className="text-sm text-destructive">{error}</span>}
                    </div>
                    <SheetFooter>
                        <Button onClick={() => void addAccount()} disabled={saving}>
                            {saving ? <Loader2 className="animate-spin" /> : <Plus />}
                            添加科目
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
