import { useMemo, useRef, useState } from "react"
import { ChevronDown, ChevronRight, Plus, Trash2 } from "lucide-react"

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

/** 会计科目类别。 */
type AccountCategory = "资产" | "负债" | "权益" | "成本" | "损益"
/** 余额方向。 */
type BalanceDirection = "借" | "贷"

interface Account {
    id: number
    code: string
    name: string
    category: AccountCategory
    direction: BalanceDirection
    /** 上级科目 id；顶级科目为 null。 */
    parentId: number | null
}

const CATEGORIES: AccountCategory[] = ["资产", "负债", "权益", "成本", "损益"]
const DIRECTIONS: BalanceDirection[] = ["借", "贷"]

/** 常用会计科目初始数据（含明细科目，示例，后续接入科目表接口后替换）。 */
const INITIAL_ACCOUNTS: Account[] = [
    { id: 1, code: "1001", name: "库存现金", category: "资产", direction: "借", parentId: null },
    { id: 2, code: "1002", name: "银行存款", category: "资产", direction: "借", parentId: null },
    { id: 21, code: "100201", name: "工商银行", category: "资产", direction: "借", parentId: 2 },
    { id: 22, code: "100202", name: "建设银行", category: "资产", direction: "借", parentId: 2 },
    { id: 3, code: "1122", name: "应收账款", category: "资产", direction: "借", parentId: null },
    { id: 4, code: "1123", name: "预付账款", category: "资产", direction: "借", parentId: null },
    { id: 5, code: "1601", name: "固定资产", category: "资产", direction: "借", parentId: null },
    { id: 6, code: "2202", name: "应付账款", category: "负债", direction: "贷", parentId: null },
    { id: 7, code: "2203", name: "预收账款", category: "负债", direction: "贷", parentId: null },
    { id: 8, code: "2221", name: "应交税费", category: "负债", direction: "贷", parentId: null },
    { id: 81, code: "222101", name: "应交增值税", category: "负债", direction: "贷", parentId: 8 },
    { id: 811, code: "22210101", name: "进项税额", category: "负债", direction: "贷", parentId: 81 },
    { id: 812, code: "22210102", name: "销项税额", category: "负债", direction: "贷", parentId: 81 },
    { id: 9, code: "4001", name: "实收资本", category: "权益", direction: "贷", parentId: null },
    { id: 10, code: "5001", name: "生产成本", category: "成本", direction: "借", parentId: null },
    { id: 11, code: "6001", name: "主营业务收入", category: "损益", direction: "贷", parentId: null },
    { id: 12, code: "6401", name: "主营业务成本", category: "损益", direction: "借", parentId: null },
    { id: 13, code: "6601", name: "销售费用", category: "损益", direction: "借", parentId: null },
    { id: 131, code: "660101", name: "职工薪酬", category: "损益", direction: "借", parentId: 13 },
    { id: 132, code: "660102", name: "差旅费", category: "损益", direction: "借", parentId: 13 },
    { id: 14, code: "6602", name: "管理费用", category: "损益", direction: "借", parentId: null },
]

const selectClassName = cn(
    "h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs outline-none",
    "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
    "disabled:pointer-events-none disabled:opacity-50 dark:bg-input/30",
)

/** 展开树时携带的层级与是否有子节点信息。 */
interface FlatRow {
    account: Account
    level: number
    hasChildren: boolean
}

export default function AccountsPage() {
    const nextId = useRef(1000)
    const [accounts, setAccounts] = useState<Account[]>(INITIAL_ACCOUNTS)
    const [filter, setFilter] = useState<AccountCategory | "全部">("全部")
    const [expanded, setExpanded] = useState<Set<number>>(
        () => new Set(INITIAL_ACCOUNTS.filter((a) => a.parentId !== null).map((a) => a.parentId as number)),
    )

    // 新增表单字段。
    const [code, setCode] = useState("")
    const [name, setName] = useState("")
    const [category, setCategory] = useState<AccountCategory>("资产")
    const [direction, setDirection] = useState<BalanceDirection>("借")
    const [parentId, setParentId] = useState<number | null>(null)
    const [error, setError] = useState("")
    const [open, setOpen] = useState(false)

    const parent = useMemo(
        () => (parentId === null ? null : accounts.find((a) => a.id === parentId) ?? null),
        [accounts, parentId],
    )

    // 按父子关系展开成带层级的扁平行（顶级按类别筛选，明细随父级展开）。
    const rows = useMemo(() => {
        const byCode = (a: Account, b: Account) => a.code.localeCompare(b.code)
        const out: FlatRow[] = []
        const walk = (pid: number | null, level: number) => {
            const children = accounts.filter((a) => a.parentId === pid).sort(byCode)
            for (const acc of children) {
                if (pid === null && filter !== "全部" && acc.category !== filter) continue
                const hasChildren = accounts.some((a) => a.parentId === acc.id)
                out.push({ account: acc, level, hasChildren })
                if (hasChildren && expanded.has(acc.id)) walk(acc.id, level + 1)
            }
        }
        walk(null, 0)
        return out
    }, [accounts, filter, expanded])

    // 上级科目下拉可选项（按编码排序，展示编码 + 名称）。
    const parentOptions = useMemo(
        () => [...accounts].sort((a, b) => a.code.localeCompare(b.code)),
        [accounts],
    )

    function toggleExpand(id: number) {
        setExpanded((prev) => {
            const next = new Set(prev)
            if (next.has(id)) next.delete(id)
            else next.add(id)
            return next
        })
    }

    function addAccount() {
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
        // 明细科目继承上级的类别与余额方向。
        const finalCategory = parent ? parent.category : category
        const finalDirection = parent ? parent.direction : direction
        const id = nextId.current++
        setAccounts((prev) => [
            ...prev,
            { id, code: trimmedCode, name: trimmedName, category: finalCategory, direction: finalDirection, parentId },
        ])
        if (parentId !== null) setExpanded((prev) => new Set(prev).add(parentId))
        setCode("")
        setName("")
        setCategory("资产")
        setDirection("借")
        setParentId(null)
        setError("")
        setOpen(false)
    }

    function handleOpenChange(next: boolean) {
        setOpen(next)
        if (!next) setError("")
    }

    function removeAccount(id: number) {
        // 级联删除该科目及其所有下级明细科目。
        setAccounts((prev) => {
            const toRemove = new Set<number>([id])
            let changed = true
            while (changed) {
                changed = false
                for (const a of prev) {
                    if (a.parentId !== null && toRemove.has(a.parentId) && !toRemove.has(a.id)) {
                        toRemove.add(a.id)
                        changed = true
                    }
                }
            }
            return prev.filter((a) => !toRemove.has(a.id))
        })
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">科目</h1>
                <p className="text-sm text-muted-foreground">
                    维护多级会计科目表，一级科目下可挂明细科目，用于凭证录入与账簿核算。
                </p>
            </div>

            <Card>
                <CardHeader className="flex-row items-center justify-between gap-2">
                    <CardTitle>科目列表</CardTitle>
                    <Button size="sm" onClick={() => setOpen(true)}>
                        <Plus />
                        新增科目
                    </Button>
                </CardHeader>
                <CardContent className="flex flex-col gap-4">
                    {/* 类别 Tab 切换 */}
                    <div className="inline-flex w-fit items-center gap-1 rounded-lg bg-muted p-1 text-muted-foreground">
                        {(["全部", ...CATEGORIES] as const).map((tab) => (
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
                                {tab}
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
                                {rows.map(({ account, level, hasChildren }) => (
                                    <tr key={account.id} className="border-b last:border-b-0 hover:bg-muted/40">
                                        <td className="px-3 py-2 tabular-nums">{account.code}</td>
                                        <td className="px-3 py-2">
                                            <div className="flex items-center" style={{ paddingLeft: `${level * 1.25}rem` }}>
                                                {hasChildren ? (
                                                    <button
                                                        type="button"
                                                        onClick={() => toggleExpand(account.id)}
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
                                        <td className="px-3 py-2">{account.category}</td>
                                        <td className="px-3 py-2">{account.direction}</td>
                                        <td className="px-3 py-2 text-center">
                                            <Button
                                                variant="ghost"
                                                size="icon-sm"
                                                onClick={() => removeAccount(account.id)}
                                                title={hasChildren ? "删除科目（含下级）" : "删除科目"}
                                            >
                                                <Trash2 />
                                            </Button>
                                        </td>
                                    </tr>
                                ))}
                                {rows.length === 0 && (
                                    <tr>
                                        <td className="px-3 py-6 text-center text-muted-foreground" colSpan={5}>
                                            暂无科目
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
                                value={parentId === null ? "" : String(parentId)}
                                onChange={(e) => setParentId(e.target.value === "" ? null : Number(e.target.value))}
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
                                onChange={(e) => setCategory(e.target.value as AccountCategory)}
                            >
                                {CATEGORIES.map((c) => (
                                    <option key={c} value={c}>
                                        {c}
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
                                onChange={(e) => setDirection(e.target.value as BalanceDirection)}
                            >
                                {DIRECTIONS.map((d) => (
                                    <option key={d} value={d}>
                                        {d}
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
                        <Button onClick={addAccount}>
                            <Plus />
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
