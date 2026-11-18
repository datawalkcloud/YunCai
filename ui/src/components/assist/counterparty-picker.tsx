/**
 * 「选择往来单位」弹窗：从客户 / 供应商 / 职员三类辅助核算档案中挑选往来单位。
 * 现金日记账：三类页签 + 查询 + 新增 + 编码/名称/备注表格 + 分页 + 清空/取消。
 * 档案本身落库在 Assist 模块，选中后把「名称」回填到流水的 counterparty 自由文本字段。
 */
import { useCallback, useEffect, useMemo, useState } from "react"
import { App, Button, Flex, Input, Modal, Pagination, Space, Table, Tabs } from "antd"
import type { TableColumnsType } from "antd"
import { PlusOutlined } from "@ant-design/icons"

import { AssistItemsApi, type AssistItemDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { invalidateAssistItems } from "@/components/assist/assist-store"

/** 可作为往来单位的辅助核算类别。 */
const PARTNER_TABS = [
    { key: "1", label: "客户", type: 1 },
    { key: "2", label: "供应商", type: 2 },
    { key: "3", label: "职员", type: 3 },
] as const

type PartnerType = (typeof PARTNER_TABS)[number]["type"]

const PAGE_SIZE_OPTIONS = [20, 50, 100]

interface CreateForm {
    code: string
    name: string
    remark: string
}

const blankCreate = (): CreateForm => ({ code: "", name: "", remark: "" })

export interface CounterpartyPickerProps {
    open: boolean
    accountSetId: string
    token: string
    /** 选中档案后的回调，参数为档案名称。 */
    onSelect: (name: string) => void
    /** 「清空」回调；不传时隐藏清空按钮。 */
    onClear?: () => void
    onClose: () => void
}

export function CounterpartyPicker({ open, accountSetId, token, onSelect, onClear, onClose }: CounterpartyPickerProps) {
    const { message } = App.useApp()

    const [activeType, setActiveType] = useState<PartnerType>(1)
    const [items, setItems] = useState<AssistItemDto[]>([])
    const [loading, setLoading] = useState(false)
    const [keyword, setKeyword] = useState("")
    const [applied, setApplied] = useState("")
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(PAGE_SIZE_OPTIONS[0])

    const [createOpen, setCreateOpen] = useState(false)
    const [createForm, setCreateForm] = useState<CreateForm>(blankCreate)
    const [creating, setCreating] = useState(false)

    const tab = PARTNER_TABS.find((t) => t.type === activeType) ?? PARTNER_TABS[0]

    const loadItems = useCallback(async () => {
        if (!token || !accountSetId) {
            setItems([])
            return
        }
        setLoading(true)
        try {
            const list = await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsGet({
                accountSetId,
                type: activeType,
            })
            setItems(
                list.filter(
                    (item) => item.isEnabled !== false && item.isLeaf !== false && item.type === activeType,
                ),
            )
        } catch (err) {
            message.error(await apiErrorMessage(err, "加载往来单位失败，请重试。"))
            setItems([])
        } finally {
            setLoading(false)
        }
    }, [token, accountSetId, activeType, message])

    useEffect(() => {
        if (!open) return
        setKeyword("")
        setApplied("")
        setPage(1)
        void loadItems()
    }, [open, loadItems])

    const visible = useMemo(() => {
        const key = applied.trim().toLowerCase()
        if (!key) return items
        return items.filter((item) =>
            [item.code, item.name, item.remark].some((v) => (v ?? "").toLowerCase().includes(key)),
        )
    }, [items, applied])

    const pageRows = visible.slice((page - 1) * pageSize, page * pageSize)

    const columns: TableColumnsType<AssistItemDto> = [
        { key: "code", title: `${tab.label}编码`, dataIndex: "code", width: 160 },
        { key: "name", title: `${tab.label}名称`, dataIndex: "name", ellipsis: true },
        { key: "remark", title: "备注", dataIndex: "remark", width: 220, ellipsis: true },
    ]

    function choose(item: AssistItemDto) {
        onSelect(item.name ?? "")
        onClose()
    }

    async function openCreate() {
        setCreateForm(blankCreate())
        setCreateOpen(true)
        if (!token || !accountSetId) return
        try {
            const code = await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsNextCodeGet({
                accountSetId,
                type: activeType,
            })
            setCreateForm((prev) => ({ ...prev, code }))
        } catch (err) {
            message.warning(await apiErrorMessage(err, "获取建议编码失败，可手动填写。"))
        }
    }

    async function submitCreate() {
        if (!token || !accountSetId || creating) return
        if (!createForm.name.trim()) {
            message.warning(`请填写${tab.label}名称。`)
            return
        }
        setCreating(true)
        try {
            const created = await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsPost({
                accountSetId,
                createAssistItemRequest: {
                    type: activeType,
                    code: createForm.code.trim(),
                    name: createForm.name.trim(),
                    remark: createForm.remark.trim() || null,
                },
            })
            invalidateAssistItems(accountSetId, activeType)
            setCreateOpen(false)
            await loadItems()
            // 新建档案通常就是为了当前这笔流水，直接回填省去再次点选。
            onSelect(created.name ?? "")
            onClose()
            message.success(`${tab.label}已新增。`)
        } catch (err) {
            message.error(await apiErrorMessage(err, `新增${tab.label}失败，请重试。`))
        } finally {
            setCreating(false)
        }
    }

    return (
        <>
            <Modal
                open={open}
                title="选择往来单位"
                width={880}
                onCancel={onClose}
                mask={{ closable: false }}
                footer={
                    <Flex justify="center" gap={12}>
                        {onClear && (
                            <Button
                                onClick={() => {
                                    onClear()
                                    onClose()
                                }}
                            >
                                清空
                            </Button>
                        )}
                        <Button onClick={onClose}>取消</Button>
                    </Flex>
                }
            >
                <Tabs
                    activeKey={String(activeType)}
                    onChange={(key) => {
                        setActiveType(Number(key) as PartnerType)
                        setKeyword("")
                        setApplied("")
                        setPage(1)
                    }}
                    items={PARTNER_TABS.map((t) => ({ key: String(t.type), label: t.label }))}
                />
                <Flex justify="space-between" align="center" gap={12} wrap style={{ marginBottom: 12 }}>
                    <Space size={8}>
                        <Input
                            allowClear
                            value={keyword}
                            placeholder={`输入${tab.label}编码或名称`}
                            style={{ width: 220 }}
                            onChange={(e) => setKeyword(e.target.value)}
                            onPressEnter={() => {
                                setApplied(keyword)
                                setPage(1)
                            }}
                        />
                        <Button
                            type="primary"
                            onClick={() => {
                                setApplied(keyword)
                                setPage(1)
                            }}
                        >
                            查询
                        </Button>
                    </Space>
                    <Button type="primary" icon={<PlusOutlined />} onClick={() => void openCreate()}>
                        新增
                    </Button>
                </Flex>

                <Table<AssistItemDto>
                    rowKey="id"
                    size="small"
                    columns={columns}
                    dataSource={pageRows}
                    loading={loading}
                    pagination={false}
                    scroll={{ x: "max-content" }}
                    locale={{ emptyText: `暂无${tab.label}档案，点击右上角「新增」创建。` }}
                    onRow={(record) => ({
                        onClick: () => choose(record),
                        style: { cursor: "pointer" },
                    })}
                />

                <Flex justify="space-between" align="center" gap={12} wrap style={{ marginTop: 12 }}>
                    <span className="text-[12px] text-(--muted-foreground)">
                        显示 {visible.length ? (page - 1) * pageSize + 1 : 0} 到 {Math.min(page * pageSize, visible.length)}，共{" "}
                        {visible.length} 条记录
                    </span>
                    <Pagination
                        size="small"
                        current={page}
                        pageSize={pageSize}
                        total={visible.length}
                        showSizeChanger
                        pageSizeOptions={PAGE_SIZE_OPTIONS}
                        showQuickJumper={false}
                        onChange={(nextPage, nextSize) => {
                            setPage(nextSize !== pageSize ? 1 : nextPage)
                            setPageSize(nextSize)
                        }}
                    />
                </Flex>
            </Modal>

            {/* 新增客户 / 供应商 / 职员：编码取后端建议值，可手改 */}
            <Modal
                open={createOpen}
                title={`新增${tab.label}`}
                width={480}
                onCancel={() => setCreateOpen(false)}
                onOk={() => void submitCreate()}
                okText="保存"
                cancelText="取消"
                confirmLoading={creating}
                mask={{ closable: false }}
            >
                <div className="flex flex-col gap-3 py-2">
                    {(
                        [
                            ["code", `${tab.label}编码`, "系统自动编号，可修改"],
                            ["name", `${tab.label}名称`, `请输入${tab.label}名称`],
                            ["remark", "备注", "选填"],
                        ] as const
                    ).map(([key, label, placeholder]) => (
                        <label key={key} className="flex items-center gap-3 text-[13px]">
                            <span className="w-[92px] shrink-0 text-right text-(--foreground)">{label}：</span>
                            <Input
                                className="flex-1"
                                value={createForm[key]}
                                placeholder={placeholder}
                                onChange={(e) => setCreateForm((prev) => ({ ...prev, [key]: e.target.value }))}
                            />
                        </label>
                    ))}
                </div>
            </Modal>
        </>
    )
}
