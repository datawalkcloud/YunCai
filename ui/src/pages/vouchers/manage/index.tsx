import { useCallback, useEffect, useState } from "react"
import { FileText, Loader2, Pencil, Plus, RefreshCw, Trash2 } from "lucide-react"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AccountsApi,
    VouchersApi,
    type AccountDto,
    type VoucherDetailDto,
    type VoucherListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import {
    Sheet,
    SheetContent,
    SheetDescription,
    SheetHeader,
    SheetTitle,
} from "@/components/ui/sheet"
import { cn } from "@/lib/utils"
import VoucherForm from "./voucher-form"

function formatAmount(n: number): string {
    return n.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 将后端返回的日期（解析为 UTC 零点的 Date）格式化为 yyyy-MM-dd。 */
function formatDate(d?: Date): string {
    return d ? d.toISOString().slice(0, 10) : ""
}

export default function VoucherManagePage() {
    const { token, user } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [vouchers, setVouchers] = useState<VoucherListItemDto[]>([])
    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [open, setOpen] = useState(false)
    const [editing, setEditing] = useState<VoucherDetailDto | null>(null)
    const [openingId, setOpeningId] = useState<string | null>(null)
    const [deletingId, setDeletingId] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const config = apiConfig(token)
            const [list, accs] = await Promise.all([
                new VouchersApi(config).accountsetsAccountSetIdVouchersGet({ accountSetId }),
                new AccountsApi(config).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true }),
            ])
            setVouchers(list)
            setAccounts(accs)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void load()
    }, [load])

    async function handleDelete(v: VoucherListItemDto) {
        if (deletingId || !v.id) return
        if (!window.confirm(`确定删除凭证「${v.voucherNo}」（${formatDate(v.date)}）吗？此操作不可撤销。`)) return
        setDeletingId(v.id)
        setError(null)
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdDelete({
                accountSetId,
                id: v.id,
            })
            setVouchers((prev) => prev.filter((x) => x.id !== v.id))
        } catch (err) {
            setError(await apiErrorMessage(err, "删除失败，请重试。"))
        } finally {
            setDeletingId(null)
        }
    }

    async function handleEdit(v: VoucherListItemDto) {
        if (openingId || !v.id) return
        setOpeningId(v.id)
        setError(null)
        try {
            const detail = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdGet({
                accountSetId,
                id: v.id,
            })
            setEditing(detail)
            setOpen(true)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载凭证详情失败，请重试。"))
        } finally {
            setOpeningId(null)
        }
    }

    function handleCreate() {
        setEditing(null)
        setOpen(true)
    }

    function handleSaved() {
        setOpen(false)
        setEditing(null)
        void load()
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex flex-col gap-1">
                    <h1 className="text-2xl font-bold">凭证管理</h1>
                    <p className="text-sm text-muted-foreground">
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        {currentSet?.isCurrent ? "（当前）" : ""}
                        ，共 {vouchers.length} 张凭证。
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || !accountSetId}>
                        <RefreshCw className={cn(loading && "animate-spin")} />
                        刷新
                    </Button>
                    <Button size="sm" onClick={handleCreate} disabled={!accountSetId}>
                        <Plus />
                        新增凭证
                    </Button>
                </div>
            </div>

            {error && <p className="text-sm text-destructive">{error}</p>}

            <Card>
                <CardContent className="p-0">
                    <div className="overflow-x-auto">
                        <table className="w-full min-w-[880px] border-collapse text-sm">
                            <thead>
                                <tr className="border-b border-border text-left text-muted-foreground">
                                    <th className="px-4 py-3 font-medium">日期</th>
                                    <th className="px-4 py-3 font-medium">凭证号</th>
                                    <th className="px-4 py-3 text-center font-medium">分录数</th>
                                    <th className="px-4 py-3 text-right font-medium">借方合计</th>
                                    <th className="px-4 py-3 text-right font-medium">贷方合计</th>
                                    <th className="px-4 py-3 text-center font-medium">状态</th>
                                    <th className="px-4 py-3 font-medium">制单人</th>
                                    <th className="px-4 py-3 text-right font-medium">操作</th>
                                </tr>
                            </thead>
                            <tbody>
                                {loading && vouchers.length === 0 ? (
                                    <tr>
                                        <td colSpan={8} className="px-4 py-10 text-center text-muted-foreground">
                                            <Loader2 className="mx-auto size-5 animate-spin" />
                                        </td>
                                    </tr>
                                ) : vouchers.length === 0 ? (
                                    <tr>
                                        <td colSpan={8} className="px-4 py-16 text-center text-muted-foreground">
                                            <FileText className="mx-auto mb-2 size-8 opacity-50" />
                                            暂无凭证，点击右上角「新增凭证」开始记账。
                                        </td>
                                    </tr>
                                ) : (
                                    vouchers.map((v) => (
                                        <tr key={v.id} className="border-b border-border last:border-0 hover:bg-accent/30">
                                            <td className="px-4 py-3 tabular-nums">{formatDate(v.date)}</td>
                                            <td className="px-4 py-3 font-medium">{v.voucherNo}</td>
                                            <td className="px-4 py-3 text-center tabular-nums">{v.entryCount}</td>
                                            <td className="px-4 py-3 text-right tabular-nums">{formatAmount(v.totalDebit ?? 0)}</td>
                                            <td className="px-4 py-3 text-right tabular-nums">{formatAmount(v.totalCredit ?? 0)}</td>
                                            <td className="px-4 py-3 text-center">
                                                <span
                                                    className={cn(
                                                        "inline-flex items-center rounded-full px-2 py-0.5 text-xs",
                                                        v.isBalanced
                                                            ? "bg-muted text-muted-foreground"
                                                            : "bg-destructive/10 text-destructive",
                                                    )}
                                                >
                                                    {v.isBalanced ? v.statusText : "不平衡"}
                                                </span>
                                            </td>
                                            <td className="px-4 py-3">{v.preparer}</td>
                                            <td className="px-4 py-3 text-right">
                                                {v.status === 1 && (
                                                    <Button
                                                        variant="ghost"
                                                        size="icon-xs"
                                                        onClick={() => void handleEdit(v)}
                                                        disabled={openingId === v.id}
                                                        title="编辑凭证"
                                                        className="text-muted-foreground hover:text-foreground"
                                                    >
                                                        {openingId === v.id ? (
                                                            <Loader2 className="animate-spin" />
                                                        ) : (
                                                            <Pencil />
                                                        )}
                                                    </Button>
                                                )}
                                                <Button
                                                    variant="ghost"
                                                    size="icon-xs"
                                                    onClick={() => void handleDelete(v)}
                                                    disabled={deletingId === v.id}
                                                    title="删除凭证"
                                                    className="text-muted-foreground hover:text-destructive"
                                                >
                                                    {deletingId === v.id ? (
                                                        <Loader2 className="animate-spin" />
                                                    ) : (
                                                        <Trash2 />
                                                    )}
                                                </Button>
                                            </td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    </div>
                </CardContent>
            </Card>

            {/* 新增 / 编辑凭证抽屉 */}
            <Sheet
                open={open}
                onOpenChange={(next) => {
                    setOpen(next)
                    if (!next) setEditing(null)
                }}
            >
                <SheetContent side="right" className="w-full gap-0 overflow-y-auto sm:max-w-[1120px]">
                    <SheetHeader className="border-b border-border">
                        <SheetTitle>{editing ? `编辑凭证 ${editing.voucherNo}` : "新增凭证"}</SheetTitle>
                        <SheetDescription>
                            {editing
                                ? "凭证字与号不可更改；修改日期、附单据数或分录后，借贷需保持平衡方可保存。"
                                : "仿纸质记账凭证录入，借贷金额需保持平衡后方可保存。凭证号留空时自动续号。"}
                        </SheetDescription>
                    </SheetHeader>
                    <div className="p-4">
                        {open && (
                            <VoucherForm
                                key={editing?.id ?? "new"}
                                accountSetId={accountSetId}
                                accounts={accounts}
                                preparer={user?.displayName}
                                token={token ?? undefined}
                                initial={editing ?? undefined}
                                onSaved={handleSaved}
                                onCancel={() => {
                                    setOpen(false)
                                    setEditing(null)
                                }}
                            />
                        )}
                    </div>
                </SheetContent>
            </Sheet>
        </div>
    )
}
