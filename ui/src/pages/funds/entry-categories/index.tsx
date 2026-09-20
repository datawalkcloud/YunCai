import { useCallback, useEffect, useMemo, useState } from "react"
import { createColumnHelper, tableFeatures, useTable } from "@tanstack/react-table"
import { CircleAlert, Pencil, Plus, RefreshCw, Tags, Trash2, X } from "lucide-react"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { EntryCategoriesApi, type EntryCategoryDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { Alert, AlertAction, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group"

/** 新建/编辑类别表单值。 */
interface CategoryFormValues {
    type: 1 | 2
    name: string
}

/** 类型筛选值：全部 / 收入 / 支出（替代原表头 filters）。 */
type TypeFilter = "all" | "1" | "2"

const emptyForm: CategoryFormValues = { type: 1, name: "" }

/** 表单校验，提示文案与原 antd rules 逐条一致。 */
function validate(values: CategoryFormValues): Partial<Record<keyof CategoryFormValues, string>> {
    const errors: Partial<Record<keyof CategoryFormValues, string>> = {}
    // 类型是单选，state 里必有值，等价于原 required 规则
    // required + whitespace：全是空格也算未填
    if (!values.name.trim()) errors.name = "请填写类别名称，如「销售收入」「办公费」。"
    return errors
}

/** 各列对齐方式按列 id 取用，避免写行内样式。 */
const COLUMN_CLASS: Record<string, { head: string, cell: string }> = {
    type: { head: "text-left", cell: "" },
    name: { head: "text-left", cell: "w-full" },
    actions: { head: "text-right", cell: "text-right" },
}

const features = tableFeatures({})
const column = createColumnHelper<typeof features, EntryCategoryDto>()

export default function EntryCategoriesPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [categories, setCategories] = useState<EntryCategoryDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    // 类型筛选：原 antd 列头 filters 改成显式筛选控件，过滤在传给表格之前完成。
    const [typeFilter, setTypeFilter] = useState<TypeFilter>("all")

    // ── 类别新建/编辑 ──
    const [modalOpen, setModalOpen] = useState(false)
    const [editing, setEditing] = useState<EntryCategoryDto | null>(null)
    const [saving, setSaving] = useState(false)
    const [formValues, setFormValues] = useState<CategoryFormValues>(emptyForm)
    const [formErrors, setFormErrors] = useState<Partial<Record<keyof CategoryFormValues, string>>>({})

    /** 加载当前账套的全部收支类别。 */
    const loadCategories = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const list = await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesGet({ accountSetId })
            setCategories(list)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载收支类别失败，请重试。"))
            setCategories([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void loadCategories()
    }, [loadCategories])

    /** 改单个字段：对应原控件 onChange，已有错误时即时重算。 */
    function setFormValue<K extends keyof CategoryFormValues>(key: K, value: CategoryFormValues[K]) {
        const next: CategoryFormValues = { ...formValues, [key]: value }
        setFormValues(next)
        if (Object.keys(formErrors).length > 0) setFormErrors(validate(next))
    }

    /** 提交前校验，对应原 validateFields。 */
    function checkForm() {
        const next = validate(formValues)
        setFormErrors(next)
        return Object.keys(next).length === 0
    }

    function openCreate() {
        setEditing(null)
        setFormValues({ type: 1, name: "" })
        setFormErrors({})
        setModalOpen(true)
    }

    function openEdit(c: EntryCategoryDto) {
        setEditing(c)
        setFormValues({ type: (c.type as 1 | 2) ?? 1, name: c.name ?? "" })
        setFormErrors({})
        setModalOpen(true)
    }

    async function handleSubmit() {
        if (!checkForm()) return
        const values = formValues
        setSaving(true)
        setError(null)
        try {
            const api = new EntryCategoriesApi(apiConfig(token))
            if (editing?.id) {
                await api.accountsetsAccountSetIdEntrycategoriesIdPut({
                    accountSetId,
                    id: editing.id,
                    updateEntryCategoryRequest: { name: values.name },
                })
            } else {
                await api.accountsetsAccountSetIdEntrycategoriesPost({
                    accountSetId,
                    createEntryCategoryRequest: { type: values.type, name: values.name },
                })
            }
            setModalOpen(false)
            await loadCategories()
        } catch (err) {
            setError(await apiErrorMessage(err, editing ? "保存类别失败，请重试。" : "新建类别失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function handleDelete(c: EntryCategoryDto) {
        if (!c.id) return
        if (!window.confirm(`确定删除${c.typeText}类别「${c.name}」吗？既有流水中已登记的类别不受影响。`)) return
        setError(null)
        try {
            await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesIdDelete({ accountSetId, id: c.id })
            await loadCategories()
        } catch (err) {
            setError(await apiErrorMessage(err, "删除类别失败，请重试。"))
        }
    }

    // 客户端过滤：等价原列头 filters + onFilter。
    const visibleCategories = useMemo(
        () => (typeFilter === "all" ? categories : categories.filter((c) => c.type === Number(typeFilter))),
        [categories, typeFilter],
    )

    const columns = [
        column.display({
            id: "type",
            header: "类型",
            cell: ({ row }) => (
                <Badge variant={row.original.type === 1 ? "default" : "destructive"}>
                    {row.original.type === 1 ? "收入" : "支出"}
                </Badge>
            ),
        }),
        column.display({
            id: "name",
            header: "类别名称",
            cell: ({ row }) => <div className="truncate">{row.original.name}</div>,
        }),
        column.display({
            id: "actions",
            header: "操作",
            cell: ({ row }) => (
                <div className="flex items-center justify-end gap-0">
                    <Button variant="ghost" size="icon-xs" title="编辑类别" onClick={() => openEdit(row.original)}>
                        <Pencil />
                    </Button>
                    <Button variant="destructive" size="icon-xs" title="删除类别" onClick={() => void handleDelete(row.original)}>
                        <Trash2 />
                    </Button>
                </div>
            ),
        }),
    ]

    const table = useTable({
        features,
        columns,
        data: visibleCategories,
        getRowId: (r) => r.id ?? r.name ?? "",
    })
    const headerGroups = table.getHeaderGroups()
    const rows = table.getRowModel().rows

    return (
        <div className="flex w-full flex-col gap-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex min-w-0 flex-col gap-1">
                    <h1 className="text-lg font-semibold">收支类别</h1>
                    <p className="text-muted-foreground">
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。维护记账时可选择的收入 / 支出类别，日记账登记流水时按类别归类统计。
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <Button variant="outline" onClick={() => void loadCategories()} disabled={!accountSetId || loading}>
                        {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                        刷新
                    </Button>
                    <Button onClick={openCreate} disabled={!accountSetId}>
                        <Plus data-icon="inline-start" />
                        新建类别
                    </Button>
                </div>
            </div>

            {error && (
                <Alert variant="destructive">
                    <CircleAlert />
                    <AlertTitle>{error}</AlertTitle>
                    <AlertAction>
                        <Button variant="ghost" size="icon-xs" title="关闭" onClick={() => setError(null)}>
                            <X />
                        </Button>
                    </AlertAction>
                </Alert>
            )}

            {categories.length === 0 && !loading ? (
                <Card>
                    <CardContent>
                        <div className="flex flex-col items-center gap-3 py-10">
                            <Tags size={40} strokeWidth={1.2} />
                            <p className="text-muted-foreground">
                                {accountSetId ? "还没有收支类别。点击「新建类别」添加，如「销售收入」「办公费」。" : "请先选择账套。"}
                            </p>
                            {accountSetId && (
                                <Button onClick={openCreate}>
                                    <Plus data-icon="inline-start" />
                                    新建类别
                                </Button>
                            )}
                        </div>
                    </CardContent>
                </Card>
            ) : (
                <Card>
                    <CardContent className="flex flex-col gap-4">
                        <div className="flex flex-wrap items-center gap-2">
                            <span className="text-muted-foreground">类型筛选：</span>
                            <ToggleGroup
                                type="single"
                                variant="outline"
                                value={typeFilter}
                                onValueChange={(v) => v && setTypeFilter(v as TypeFilter)}
                            >
                                <ToggleGroupItem value="all">全部</ToggleGroupItem>
                                <ToggleGroupItem value="1">收入</ToggleGroupItem>
                                <ToggleGroupItem value="2">支出</ToggleGroupItem>
                            </ToggleGroup>
                        </div>

                        <Table>
                            <TableHeader>
                                {headerGroups.map((hg) => (
                                    <TableRow key={hg.id}>
                                        {hg.headers.map((h) => (
                                            <TableHead key={h.id} className={COLUMN_CLASS[h.column.id]?.head}>
                                                <table.FlexRender header={h} />
                                            </TableHead>
                                        ))}
                                    </TableRow>
                                ))}
                            </TableHeader>
                            <TableBody>
                                {loading && rows.length === 0 ? (
                                    <TableRow>
                                        <TableCell colSpan={columns.length} className="py-8 text-center">
                                            <Spinner />
                                        </TableCell>
                                    </TableRow>
                                ) : (
                                    rows.map((row) => (
                                        <TableRow key={row.id}>
                                            {row.getAllCells().map((cell) => (
                                                <TableCell key={cell.id} className={COLUMN_CLASS[cell.column.id]?.cell}>
                                                    <table.FlexRender cell={cell} />
                                                </TableCell>
                                            ))}
                                        </TableRow>
                                    ))
                                )}
                                {!loading && rows.length === 0 && (
                                    <TableRow>
                                        <TableCell colSpan={columns.length} className="py-8 text-center text-muted-foreground">
                                            暂无数据
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                        </Table>
                    </CardContent>
                </Card>
            )}

            {/* 新建/编辑类别 */}
            <Dialog open={modalOpen} onOpenChange={setModalOpen}>
                <DialogContent aria-describedby={undefined}>
                    <DialogHeader>
                        <DialogTitle>{editing ? "编辑类别" : "新建类别"}</DialogTitle>
                    </DialogHeader>
                    <div className="flex flex-col gap-4">
                        <Field>
                            <FieldLabel htmlFor="entry-category-type">类型</FieldLabel>
                            <RadioGroup
                                id="entry-category-type"
                                className="flex flex-row items-center gap-4"
                                disabled={!!editing}
                                value={String(formValues.type)}
                                onValueChange={(v) => setFormValue("type", v === "1" ? 1 : 2)}
                            >
                                <div className="flex items-center gap-2">
                                    <RadioGroupItem value="1" id="entry-category-type-income" />
                                    <FieldLabel htmlFor="entry-category-type-income">收入</FieldLabel>
                                </div>
                                <div className="flex items-center gap-2">
                                    <RadioGroupItem value="2" id="entry-category-type-expense" />
                                    <FieldLabel htmlFor="entry-category-type-expense">支出</FieldLabel>
                                </div>
                            </RadioGroup>
                        </Field>
                        <Field data-invalid={Boolean(formErrors.name)}>
                            <FieldLabel htmlFor="entry-category-name">类别名称</FieldLabel>
                            <Input
                                id="entry-category-name"
                                value={formValues.name}
                                onChange={(e) => setFormValue("name", e.target.value)}
                                placeholder="如：销售收入、办公费、差旅费"
                                maxLength={50}
                                aria-invalid={Boolean(formErrors.name)}
                            />
                            <FieldError>{formErrors.name}</FieldError>
                        </Field>
                        {editing && (
                            <FieldDescription>
                                改名不影响既有流水中已登记的类别文本。
                            </FieldDescription>
                        )}
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setModalOpen(false)}>取消</Button>
                        <Button onClick={() => void handleSubmit()} disabled={saving}>
                            {saving && <Spinner data-icon="inline-start" />}
                            {editing ? "保存" : "创建"}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    )
}
