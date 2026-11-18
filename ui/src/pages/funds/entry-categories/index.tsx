/**
 * 收支类别（对应「资金 → 收支类别」）。
 *
 * 布局：收入 / 支出 两个页签 → 输入编码或名称 + 查询｜导入 / 导出 / 新增 / 刷新；
 * 表格列 编码 / 名称 / 智能匹配关键字 / 关联现金流 / 启用状态（开关）/ 操作（新增·编辑·删除）；
 * 新增与编辑弹窗字段：编码（默认下一个序号）、名称、上级类别、关联现金流、智能匹配关键字。
 */
import { useCallback, useEffect, useMemo, useState } from "react"
import type { ReactNode } from "react"
import {
    App,
    Button,
    Card,
    Input,
    Modal,
    Pagination,
    Select,
    Space,
    Switch,
    Table,
    Tabs,
    Tooltip,
    Typography,
    Upload,
} from "antd"
import type { TableColumnsType, UploadFile } from "antd"
import { ReloadOutlined, SearchOutlined } from "@ant-design/icons"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AssistItemsApi,
    EntryCategoriesApi,
    type AssistItemDto,
    type EntryCategoryDto,
    type EntryCategoryImportResult,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

const PAGE_SIZE_OPTIONS = [20, 50, 100, 200]

/** 类型页签：1=收入，2=支出。 */
type CategoryType = 1 | 2

/** 表单模型（与新增弹窗字段一致）。 */
interface CategoryForm {
    code: string
    name: string
    parentId: string
    cashFlowItemId: string
    smartKeywords: string
}

const emptyForm: CategoryForm = { code: "", name: "", parentId: "", cashFlowItemId: "", smartKeywords: "" }

export default function EntryCategoriesPage() {
    const { message } = App.useApp()
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

    const [type, setType] = useState<CategoryType>(1)
    const [all, setAll] = useState<EntryCategoryDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [keyword, setKeyword] = useState("")
    const [applied, setApplied] = useState("")
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(20)

    const [cashFlows, setCashFlows] = useState<AssistItemDto[]>([])

    const [formOpen, setFormOpen] = useState(false)
    const [editing, setEditing] = useState<EntryCategoryDto | null>(null)
    const [form, setForm] = useState<CategoryForm>(emptyForm)
    const [saving, setSaving] = useState(false)

    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<UploadFile | null>(null)
    const [importing, setImporting] = useState(false)
    const [importResult, setImportResult] = useState<EntryCategoryImportResult | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const list = await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesGet({ accountSetId })
            setAll(list ?? [])
        } catch (err) {
            setError(await apiErrorMessage(err, "加载收支类别失败，请重试。"))
            setAll([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void load()
    }, [load])

    /** 关联现金流可选项（辅助核算「现金流」类别的档案）。 */
    useEffect(() => {
        if (!accountSetId) return
        void (async () => {
            try {
                const items = await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsGet({
                    accountSetId,
                    type: 7,
                })
                setCashFlows(items ?? [])
            } catch {
                // 未启用现金流核算时忽略
            }
        })()
    }, [accountSetId, token])

    /* ---------------- 列表过滤与分页 ---------------- */

    const typeRows = useMemo(() => all.filter((c) => c.type === type), [all, type])

    const filtered = useMemo(() => {
        const term = applied.trim()
        if (!term) return typeRows
        return typeRows.filter((c) => (c.code ?? "").includes(term) || (c.name ?? "").includes(term))
    }, [typeRows, applied])

    const paged = filtered.slice((page - 1) * pageSize, page * pageSize)

    /** 下一个编码：现有纯数字编码的最大值 + 1。 */
    const nextCode = useMemo(() => {
        const max = all.reduce((acc, c) => {
            const n = Number.parseInt(c.code ?? "", 10)
            return Number.isFinite(n) && n > acc ? n : acc
        }, 0)
        return String(max + 1)
    }, [all])

    /* ---------------- 表单 ---------------- */

    const openCreate = (presetType: CategoryType = type) => {
        setType(presetType)
        setEditing(null)
        setForm({ ...emptyForm, code: nextCode })
        setFormOpen(true)
    }

    const openEdit = (row: EntryCategoryDto) => {
        setEditing(row)
        setForm({
            code: row.code ?? "",
            name: row.name ?? "",
            parentId: row.parentId ?? "",
            cashFlowItemId: row.cashFlowItemId ?? "",
            smartKeywords: row.smartKeywords ?? "",
        })
        setFormOpen(true)
    }

    const submitForm = async () => {
        if (!accountSetId) return
        if (!form.name.trim()) {
            message.warning("请输入类别名称。")
            return
        }

        setSaving(true)
        try {
            const api = new EntryCategoriesApi(apiConfig(token))
            if (editing?.id) {
                await api.accountsetsAccountSetIdEntrycategoriesIdPut({
                    accountSetId,
                    id: editing.id,
                    updateEntryCategoryRequest: {
                        name: form.name.trim(),
                        code: form.code.trim() || undefined,
                        parentId: form.parentId || "00000000-0000-0000-0000-000000000000",
                        cashFlowItemId: form.cashFlowItemId || "00000000-0000-0000-0000-000000000000",
                        smartKeywords: form.smartKeywords,
                    },
                })
                message.success("类别已保存")
            } else {
                await api.accountsetsAccountSetIdEntrycategoriesPost({
                    accountSetId,
                    createEntryCategoryRequest: {
                        type,
                        code: form.code.trim() || undefined,
                        name: form.name.trim(),
                        parentId: form.parentId || undefined,
                        cashFlowItemId: form.cashFlowItemId || undefined,
                        smartKeywords: form.smartKeywords || undefined,
                    },
                })
                message.success("类别已新增")
            }
            setFormOpen(false)
            await load()
        } catch (err) {
            message.error(await apiErrorMessage(err, "保存类别失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    const removeCategory = (row: EntryCategoryDto) => {
        if (!row.id) return
        Modal.confirm({
            title: "删除确认",
            content: `确定删除类别「${row.name}」吗？既有流水中已登记的类别文本不受影响。`,
            okText: "确定",
            cancelText: "取消",
            onOk: async () => {
                try {
                    await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesIdDelete({
                        accountSetId,
                        id: row.id!,
                    })
                    message.success("类别已删除")
                    await load()
                } catch (err) {
                    message.error(await apiErrorMessage(err, "删除类别失败，请重试。"))
                }
            },
        })
    }

    const toggleEnabled = async (row: EntryCategoryDto, enabled: boolean) => {
        if (!row.id) return
        try {
            await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesIdEnabledPatch({
                accountSetId,
                id: row.id,
                setEntryCategoryEnabledRequest: { enabled },
            })
            setAll((prev) => prev.map((c) => (c.id === row.id ? { ...c, isEnabled: enabled } : c)))
            message.success(enabled ? "已启用" : "已停用")
        } catch (err) {
            message.error(await apiErrorMessage(err, "切换启用状态失败，请重试。"))
        }
    }

    /* ---------------- 导入 / 导出 ---------------- */

    const exportCategories = async () => {
        if (!accountSetId) return
        try {
            const res = await fetch(`/api/accountsets/${accountSetId}/entrycategories/export?type=${type}`, {
                headers: { Authorization: `Bearer ${token}` },
            })
            if (!res.ok) throw new Error(await apiErrorMessage(await res.json().catch(() => null), "导出失败，请重试。"))
            const blob = await res.blob()
            const url = URL.createObjectURL(blob)
            const anchor = document.createElement("a")
            anchor.href = url
            const disposition = res.headers.get("Content-Disposition") ?? ""
            const star = /filename\*=UTF-8''([^;\n]+)/i.exec(disposition)
            const plain = /filename="?([^";\n]+)"?/i.exec(disposition)
            anchor.download = star ? decodeURIComponent(star[1]) : plain ? plain[1] : "收支类别.xlsx"
            document.body.appendChild(anchor)
            anchor.click()
            anchor.remove()
            URL.revokeObjectURL(url)
        } catch (err) {
            message.error(err instanceof Error ? err.message : "导出失败，请重试。")
        }
    }

    const downloadTemplate = async () => {
        if (!accountSetId) return
        const res = await fetch(`/api/accountsets/${accountSetId}/entrycategories/import/template`, {
            headers: { Authorization: `Bearer ${token}` },
        })
        if (!res.ok) {
            message.error("模板下载失败，请重试。")
            return
        }
        const blob = await res.blob()
        const url = URL.createObjectURL(blob)
        const anchor = document.createElement("a")
        anchor.href = url
        anchor.download = "收支类别导入模板.xlsx"
        document.body.appendChild(anchor)
        anchor.click()
        anchor.remove()
        URL.revokeObjectURL(url)
    }

    const submitImport = async () => {
        if (!accountSetId || !importFile?.originFileObj) {
            message.warning("请选择要导入的文件。")
            return
        }
        setImporting(true)
        try {
            const body = new FormData()
            body.append("file", importFile.originFileObj as File)
            const res = await fetch(`/api/accountsets/${accountSetId}/entrycategories/import`, {
                method: "POST",
                headers: { Authorization: `Bearer ${token}` },
                body,
            })
            if (!res.ok) throw new Error(await apiErrorMessage(await res.json().catch(() => null), "导入失败，请重试。"))
            const result = (await res.json()) as EntryCategoryImportResult
            setImportResult(result)
            setImportFile(null)
            setImportOpen(false)
            await load()
        } catch (err) {
            message.error(err instanceof Error ? err.message : "导入失败，请重试。")
        } finally {
            setImporting(false)
        }
    }

    /* ---------------- 表格 ---------------- */

    const columns: TableColumnsType<EntryCategoryDto> = [
        {
            title: "编码",
            dataIndex: "code",
            width: 120,
            render: (_, r) => <span className="text-(--muted-foreground)">{r.code}</span>,
        },
        {
            title: "名称",
            dataIndex: "name",
            render: (_, r) => (
                <span className="flex items-center gap-2">
                    <span className="font-medium text-(--foreground)">{r.name}</span>
                    {r.parentName && <span className="text-[11.5px] text-(--muted-foreground)">（上级：{r.parentName}）</span>}
                </span>
            ),
        },
        {
            title: (
                <Tooltip title="导入流水时，摘要包含任一关键字即可自动归入该类别；不同关键字用逗号隔开。">
                    <span className="border-b border-dashed border-(--border)">智能匹配关键字</span>
                </Tooltip>
            ),
            dataIndex: "smartKeywords",
            render: (_, r) => r.smartKeywords || <span className="text-(--muted-foreground)">—</span>,
        },
        {
            title: "关联现金流",
            dataIndex: "cashFlowName",
            width: 160,
            render: (_, r) => r.cashFlowName || <span className="text-(--muted-foreground)">—</span>,
        },
        {
            title: "启用状态",
            dataIndex: "isEnabled",
            width: 110,
            align: "center",
            render: (_, r) => <Switch checked={r.isEnabled} onChange={(v) => void toggleEnabled(r, v)} />,
        },
        {
            title: "操作",
            width: 190,
            render: (_, r) => (
                <Space size={10}>
                    <Typography.Link className="text-[13px]" onClick={() => openCreate((r.type as CategoryType) ?? type)}>
                        新增
                    </Typography.Link>
                    <Typography.Link className="text-[13px]" onClick={() => openEdit(r)}>
                        编辑
                    </Typography.Link>
                    <Typography.Link className="text-[13px] text-(--danger)" onClick={() => removeCategory(r)}>
                        删除
                    </Typography.Link>
                </Space>
            ),
        },
    ]

    const parentOptions = useMemo(
        () => [
            { value: "", label: "全部类别" },
            ...all
                .filter((c) => c.type === type && c.id !== editing?.id)
                .map((c) => ({ value: c.id ?? "", label: `${c.code} ${c.name}` })),
        ],
        [all, type, editing],
    )

    const currentName = type === 1 ? "收入类别" : "支出类别"

    return (
        <div className="flex flex-col gap-3">
            {error && <div className="rounded border border-(--danger-border) bg-(--table-neg-bg) px-3 py-2 text-[13px] text-(--danger)">{error}</div>}

            <Card>
                <Tabs
                    className="app-sub-tabs"
                    activeKey={String(type)}
                    onChange={(key) => {
                        setType(Number(key) as CategoryType)
                        setPage(1)
                    }}
                    items={[
                        { key: "1", label: "收入" },
                        { key: "2", label: "支出" },
                    ]}
                    tabBarStyle={{ margin: 0, paddingInline: 16 }}
                />

                {/* 工具行 */}
                <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 px-4 py-2.5" style={{ marginBottom: 16 }}>
                    <Space size={8} wrap>
                        <span className="text-[13px] text-(--foreground)">输入编码或名称：</span>
                        <Input
                                                       allowClear
                            className="w-[220px]"
                            value={keyword}
                            prefix={<SearchOutlined className="text-(--muted-foreground)" />}
                            onChange={(e) => setKeyword(e.target.value)}
                            onPressEnter={() => {
                                setApplied(keyword)
                                setPage(1)
                            }}
                        />
                        <Button
                            type="primary"
                                                       autoInsertSpace={false}
                            onClick={() => {
                                setApplied(keyword)
                                setPage(1)
                            }}
                        >
                            查询
                        </Button>
                    </Space>

                    <Space size={8} wrap>
                        <Button autoInsertSpace={false} onClick={() => setImportOpen(true)}>
                            导入
                        </Button>
                        <Button autoInsertSpace={false} onClick={() => void exportCategories()}>
                            导出
                        </Button>
                        <Button type="primary" autoInsertSpace={false} onClick={() => openCreate()}>
                            新增
                        </Button>
                        <Tooltip title="刷新">
                            <Button icon={<ReloadOutlined spin={loading} />} onClick={() => void load()} />
                        </Tooltip>
                    </Space>
                </div>

                {/* 表格 */}
                <div className="p-3">
                    <Table
                        columns={columns}
                        dataSource={paged}
                        rowKey={(r) => r.id ?? ""}
                        pagination={false}
                                               loading={loading}
                        locale={{ emptyText: <div className="py-10 text-[13px] text-(--muted-foreground)">暂无数据</div> }}
                    />
                    <div className="flex flex-wrap items-center justify-between gap-2 text-[12.5px] text-(--muted-foreground)" style={{ marginTop: 16 }}>
                        <span>
                            显示 {filtered.length === 0 ? 0 : (page - 1) * pageSize + 1} 到 {Math.min(page * pageSize, filtered.length)}，共{" "}
                            {filtered.length} 条
                        </span>
                        <Pagination
                                                       current={page}
                            pageSize={pageSize}
                            total={filtered.length}
                            pageSizeOptions={PAGE_SIZE_OPTIONS}
                            showSizeChanger
                            onChange={(next, size) => {
                                if (size !== pageSize) {
                                    setPageSize(size)
                                    setPage(1)
                                    return
                                }
                                setPage(next)
                            }}
                        />
                    </div>
                </div>
            </Card>

            {/* 新增 / 编辑类别 */}
            <Modal
                open={formOpen}
                title={editing ? `编辑${currentName}` : `新增${currentName}`}
                width={520}
                onCancel={() => setFormOpen(false)}
                footer={
                    <Space>
                        <Button autoInsertSpace={false} onClick={() => setFormOpen(false)}>
                            取消
                        </Button>
                        <Button type="primary" autoInsertSpace={false} loading={saving} onClick={() => void submitForm()}>
                            保存
                        </Button>
                    </Space>
                }
            >
                <div className="flex flex-col gap-3 py-2">
                    <FieldRow label="编码" required>
                        <Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} maxLength={32} />
                    </FieldRow>
                    <FieldRow label="名称" required>
                        <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} maxLength={50} />
                    </FieldRow>
                    <FieldRow label="上级类别">
                        <Select
                            className="w-full"
                            value={form.parentId}
                            options={parentOptions}
                            onChange={(v) => setForm({ ...form, parentId: v })}
                        />
                    </FieldRow>
                    <FieldRow label="关联现金流">
                        <Select
                            className="w-full"
                            allowClear
                            placeholder="不关联"
                            value={form.cashFlowItemId || undefined}
                            options={cashFlows.map((c) => ({ value: c.id ?? "", label: `${c.code} ${c.name}` }))}
                            onChange={(v) => setForm({ ...form, cashFlowItemId: v ?? "" })}
                        />
                    </FieldRow>
                    <FieldRow label="智能匹配关键字">
                        <Input.TextArea
                            rows={3}
                            value={form.smartKeywords}
                            maxLength={200}
                            onChange={(e) => setForm({ ...form, smartKeywords: e.target.value })}
                        />
                    </FieldRow>
                    <ul className="pl-5 text-[12px] leading-6 text-(--warn-text)">
                        <li>当日记账的摘要中有对应关键字时，可以自动匹配出收支类别</li>
                        <li>不同关键字之间用逗号隔开</li>
                    </ul>
                </div>
            </Modal>

            {/* 导入 */}
            <Modal
                open={importOpen}
                title="导入收支类别"
                width={520}
                onCancel={() => setImportOpen(false)}
                footer={
                    <Space>
                        <Button autoInsertSpace={false} onClick={() => setImportOpen(false)}>
                            取消
                        </Button>
                        <Button type="primary" autoInsertSpace={false} loading={importing} onClick={() => void submitImport()}>
                            导入
                        </Button>
                    </Space>
                }
            >
                <div className="flex flex-col gap-3 py-2 text-[13px] leading-6 text-(--foreground)">
                    <p>1.请选择下面任意一种方式导入收支类别</p>
                    <p className="pl-4">
                        (1)在收支类别列表导出所需数据，确认后直接导入
                        <br />
                        (2)点击
                        <Typography.Link className="mx-1 text-[13px]" onClick={() => void downloadTemplate()}>
                            下载模板
                        </Typography.Link>
                        ，按照模板格式进行数据整理再导入
                    </p>
                    <p>2.选择文件导入</p>
                    <Upload
                        accept=".xlsx"
                        maxCount={1}
                        fileList={importFile ? [importFile] : []}
                        beforeUpload={() => false}
                        onChange={({ fileList }) => setImportFile(fileList[0] ?? null)}
                        onRemove={() => setImportFile(null)}
                    >
                        <Button>选择文件</Button>
                    </Upload>
                </div>
            </Modal>

            {/* 导入结果 */}
            {importResult && (
                <Modal
                    open
                    title="导入结果"
                    width={520}
                    onCancel={() => setImportResult(null)}
                    footer={
                        <Button type="primary" autoInsertSpace={false} onClick={() => setImportResult(null)}>
                            知道了
                        </Button>
                    }
                >
                    <p className="text-[13px] text-(--foreground)">
                        共 {importResult.totalRows} 行，成功导入 {importResult.imported} 条，跳过 {importResult.skipped} 条。
                    </p>
                    {(importResult.warnings ?? []).length > 0 && (
                        <ul className="mt-2 max-h-[240px] overflow-y-auto pl-5 text-[12.5px] leading-6 text-(--warn-text)">
                            {(importResult.warnings ?? []).map((w, i) => (
                                <li key={i}>{w}</li>
                            ))}
                        </ul>
                    )}
                </Modal>
            )}
        </div>
    )
}

/** 弹窗里的一行「标签 + 控件」。 */
function FieldRow({ label, required, children }: { label: string; required?: boolean; children: ReactNode }) {
    return (
        <div className="flex items-start gap-3">
            <span className="w-[92px] shrink-0 pt-1.5 text-right text-[13px] text-(--foreground)">
                {required && <span className="mr-1 text-(--danger)">*</span>}
                {label}
            </span>
            <div className="min-w-0 flex-1">{children}</div>
        </div>
    )
}
