import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, Button, Input, Modal, Radio, Segmented, Select, Table } from "antd"
import type { TableColumnsType } from "antd"
import {
    DeleteOutlined,
    EditOutlined,
    PlusOutlined,
    ReloadOutlined,
    TagsOutlined,
} from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { AccountsApi, EntryCategoriesApi, type AccountDto, type EntryCategoryDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { useConfirm } from "@/hooks/useConfirm"
import { PageHeader, SectionCard } from "@/components/page/section"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"
import { ICON_STYLE } from "@/components/page/styles"

/** 新建/编辑类别表单值。 */
interface CategoryFormValues {
    type: 1 | 2
    name: string
    /** 智能匹配关键字（逗号分隔）；空串表示不设置 / 清除。 */
    smartKeywords: string
    /** 默认对方科目；空串表示不指定（更新时按后端三态协议解释为空 GUID 清除）。 */
    voucherSubjectId: string
}

/** 更新请求中「清除默认对方科目」的哨兵值（后端约定 Guid.Empty 表示清除）。 */
const CLEAR_SUBJECT_GUID = "00000000-0000-0000-0000-000000000000"

/** 类型筛选值：全部 / 收入 / 支出（替代原表头 filters）。 */
type TypeFilter = "all" | "1" | "2"

const TYPE_FILTER_OPTIONS = [
    { value: "all", label: "全部" },
    { value: "1", label: "收入" },
    { value: "2", label: "支出" },
]

const emptyForm: CategoryFormValues = { type: 1, name: "", smartKeywords: "", voucherSubjectId: "" }

/** 表单校验，提示文案与原 antd rules 逐条一致。 */
function validate(values: CategoryFormValues): Partial<Record<keyof CategoryFormValues, string>> {
    const errors: Partial<Record<keyof CategoryFormValues, string>> = {}
    // 类型是单选，state 里必有值，等价于原 required 规则
    // required + whitespace：全是空格也算未填
    if (!values.name.trim()) errors.name = "请填写类别名称，如「销售收入」「办公费」。"
    return errors
}

export default function EntryCategoriesPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { confirm, confirmDialog } = useConfirm()

    const [categories, setCategories] = useState<EntryCategoryDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    // 类型筛选：原 antd 列头 filters 改成显式筛选控件，过滤在传给表格之前完成。
    const [typeFilter, setTypeFilter] = useState<TypeFilter>("all")
    // 默认对方科目可选项（末级科目），打开对话框时惰性加载。
    const [subjectAccounts, setSubjectAccounts] = useState<AccountDto[]>([])

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

    /** 惰性加载末级科目列表，作为「默认对方科目」可选项。 */
    async function ensureSubjectAccounts() {
        if (subjectAccounts.length > 0) return
        try {
            const accs = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({
                accountSetId,
                leafOnly: true,
            })
            setSubjectAccounts(accs)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载科目列表失败，请重试。"))
        }
    }

    function openCreate() {
        setEditing(null)
        setFormValues(emptyForm)
        setFormErrors({})
        setModalOpen(true)
        void ensureSubjectAccounts()
    }

    function openEdit(c: EntryCategoryDto) {
        setEditing(c)
        setFormValues({
            type: (c.type as 1 | 2) ?? 1,
            name: c.name ?? "",
            smartKeywords: c.smartKeywords ?? "",
            voucherSubjectId: c.voucherSubjectId ?? "",
        })
        setFormErrors({})
        setModalOpen(true)
        void ensureSubjectAccounts()
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
                    updateEntryCategoryRequest: {
                        name: values.name,
                        // 关键字：整值提交（空串 = 清除）；科目：空 = 空 GUID（清除），否则为所选科目。
                        smartKeywords: values.smartKeywords,
                        voucherSubjectId: values.voucherSubjectId || CLEAR_SUBJECT_GUID,
                    },
                })
            } else {
                await api.accountsetsAccountSetIdEntrycategoriesPost({
                    accountSetId,
                    createEntryCategoryRequest: {
                        type: values.type,
                        name: values.name,
                        smartKeywords: values.smartKeywords || undefined,
                        voucherSubjectId: values.voucherSubjectId || undefined,
                    },
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
        if (!(await confirm(`确定删除${c.typeText}类别「${c.name}」吗？既有流水中已登记的类别不受影响。`))) return
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

    const columns: TableColumnsType<EntryCategoryDto> = [
        {
            title: "类型",
            render: (_, c) => (
                <StatusPill tone={c.type === 1 ? "green" : "red"}>{c.type === 1 ? "收入" : "支出"}</StatusPill>
            ),
        },
        { title: "类别名称", render: (_, c) => <div className="truncate">{c.name}</div> },
        {
            title: "智能匹配关键字",
            render: (_, c) =>
                c.smartKeywords
                    ? <div className="truncate text-slate-600">{c.smartKeywords}</div>
                    : <span className="text-slate-300">—</span>,
        },
        {
            title: "默认对方科目",
            render: (_, c) =>
                c.voucherSubjectId
                    ? (
                        <span className="whitespace-nowrap text-slate-600">
                            {c.voucherSubjectCode} {c.voucherSubjectName}
                        </span>
                    )
                    : <span className="text-slate-300">—</span>,
        },
        {
            title: "操作",
            align: "right",
            render: (_, c) => (
                <div className="flex items-center justify-end gap-0">
                    <Button type="text" size="small" title="编辑类别" icon={<EditOutlined />} onClick={() => openEdit(c)} />
                    <Button
                        type="text"
                        size="small"
                        danger
                        title="删除类别"
                        icon={<DeleteOutlined />}
                        onClick={() => void handleDelete(c)}
                    />
                </div>
            ),
        },
    ]

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={TagsOutlined}
                title="收支类别"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。维护记账时可选择的收入 / 支出类别，日记账登记流水时按类别归类统计。
                    </>
                }
                actions={
                    <>
                        <Button
                            icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                            disabled={!accountSetId || loading}
                            onClick={() => void loadCategories()}
                        >
                            刷新
                        </Button>
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!accountSetId}>
                            新建类别
                        </Button>
                    </>
                }
            />

            {error && <Alert type="error" showIcon closable title={error} onClose={() => setError(null)} />}

            {categories.length === 0 && !loading ? (
                <SectionCard bodyClassName="p-0">
                    <div className="flex flex-col items-center gap-3 py-12">
                        <TagsOutlined className="text-[34px]" style={ICON_STYLE.placeholder} />
                        <p className="text-[13px] text-slate-400">
                            {accountSetId ? "还没有收支类别。点击「新建类别」添加，如「销售收入」「办公费」。" : "请先选择账套。"}
                        </p>
                        {accountSetId && (
                            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                                新建类别
                            </Button>
                        )}
                    </div>
                </SectionCard>
            ) : (
                <SectionCard
                    title={<h3 className="text-[14px] font-semibold text-slate-800">类别列表</h3>}
                    action={
                        <div className="flex flex-wrap items-center gap-2">
                            <span className="text-[12px] text-slate-400">类型筛选</span>
                            <Segmented
                                size="small"
                                options={TYPE_FILTER_OPTIONS}
                                value={typeFilter}
                                onChange={(v) => setTypeFilter(v as TypeFilter)}
                            />
                        </div>
                    }
                    bodyClassName="p-0"
                >
                    <Table<EntryCategoryDto>
                        columns={columns}
                        dataSource={visibleCategories}
                        rowKey={(c) => c.id ?? c.name ?? ""}
                        pagination={false}
                        size="small"
                        loading={loading && visibleCategories.length === 0}
                        locale={{ emptyText: "暂无数据" }}
                    />
                </SectionCard>
            )}

            {/* 新建/编辑类别 */}
            <Modal
                open={modalOpen}
                title={editing ? "编辑类别" : "新建类别"}
                onCancel={() => setModalOpen(false)}
                onOk={() => void handleSubmit()}
                okText={editing ? "保存" : "创建"}
                cancelText="取消"
                confirmLoading={saving}
            >
                <div className="flex flex-col gap-4 py-2">
                    <FormField label="类型" required>
                        <Radio.Group
                            disabled={!!editing}
                            value={formValues.type}
                            onChange={(e) => setFormValue("type", e.target.value as 1 | 2)}
                            options={[
                                { value: 1, label: "收入" },
                                { value: 2, label: "支出" },
                            ]}
                        />
                    </FormField>
                    <FormField label="类别名称" htmlFor="entry-category-name" required error={formErrors.name}>
                        <Input
                            id="entry-category-name"
                            value={formValues.name}
                            onChange={(e) => setFormValue("name", e.target.value)}
                            placeholder="如：销售收入、办公费、差旅费"
                            maxLength={50}
                            status={formErrors.name ? "error" : undefined}
                        />
                    </FormField>
                    <FormField
                        label="智能匹配关键字（选填）"
                        htmlFor="entry-category-keywords"
                        description="导入流水时，摘要包含任一关键字将自动归入该类别（同类命中取关键字最长者）。"
                    >
                        <Input
                            id="entry-category-keywords"
                            value={formValues.smartKeywords}
                            onChange={(e) => setFormValue("smartKeywords", e.target.value)}
                            placeholder="逗号分隔，如：货款,销售"
                            maxLength={200}
                        />
                    </FormField>
                    <FormField
                        label="默认对方科目（选填）"
                        htmlFor="entry-category-subject"
                        description="由流水生成凭证时，该类别默认使用的对方科目（仅末级科目）。"
                    >
                        <Select
                            id="entry-category-subject"
                            className="w-full"
                            allowClear
                            placeholder="不指定"
                            value={formValues.voucherSubjectId || undefined}
                            onChange={(v) => setFormValue("voucherSubjectId", v ?? "")}
                            options={subjectAccounts.map((a) => ({
                                value: a.id ?? "",
                                label: `${a.code} ${a.name}`,
                            }))}
                        />
                    </FormField>
                    {editing && <p className="text-sm text-slate-400">改名不影响既有流水中已登记的类别文本。</p>}
                </div>
            </Modal>

            {confirmDialog}
        </div>
    )
}
