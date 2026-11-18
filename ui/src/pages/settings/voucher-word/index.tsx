import { useCallback, useEffect, useMemo, useState } from "react"
import {
    Alert,
    App,
    Button,
    Card,
    Checkbox,
    Input,
    Modal,
    Table,
    Tooltip,
    Typography,
} from "antd"
import type { TableColumnsType } from "antd"
import { PlusOutlined, ReloadOutlined, SearchOutlined, StarOutlined } from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { VoucherWordsApi, type SaveVoucherWordRequest, type VoucherWordDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { useConfirm } from "@/hooks/useConfirm"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"

/** 与后端 VoucherWord.MaxWordLength / MaxPrintTitleLength 保持一致。 */
const WORD_MAX_LENGTH = 8
const TITLE_MAX_LENGTH = 50

/** 凭证字会拼进账套的逗号串（供制单下拉取值），不能含分隔符与空白。 */
const WORD_INVALID_PATTERN = /[,，、\s]/

/** 常用凭证字：新建时一键填入字与打印标题。 */
const PRESETS = [
    { word: "记", printTitle: "记账凭证" },
    { word: "收", printTitle: "收款凭证" },
    { word: "付", printTitle: "付款凭证" },
    { word: "转", printTitle: "转账凭证" },
]

function defaultPrintTitle(word: string) {
    return PRESETS.find((p) => p.word === word)?.printTitle ?? `${word}凭证`
}

interface WordForm {
    word: string
    printTitle: string
    isDefault: boolean
}

const emptyForm: WordForm = { word: "", printTitle: "", isDefault: false }

export default function VoucherWordPage() {
    const { message } = App.useApp()
    const { token } = useAuth()
    const { refresh: refreshAccountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { confirm, confirmDialog } = useConfirm()

    const [words, setWords] = useState<VoucherWordDto[]>([])
    const [loading, setLoading] = useState(false)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [keyword, setKeyword] = useState("")
    const [busyId, setBusyId] = useState<string | null>(null)

    const [formOpen, setFormOpen] = useState(false)
    const [editing, setEditing] = useState<VoucherWordDto | null>(null)
    const [form, setForm] = useState<WordForm>(emptyForm)
    const [formError, setFormError] = useState("")
    const [saving, setSaving] = useState(false)
    /** 打印标题是否被手动改过；没改过时跟随凭证字自动联动。 */
    const [titleTouched, setTitleTouched] = useState(false)

    const load = useCallback(async () => {
        if (!accountSetId) {
            setWords([])
            return
        }
        setLoading(true)
        setLoadError(null)
        try {
            setWords(await new VoucherWordsApi(apiConfig(token)).accountsetsAccountSetIdVoucherwordsGet({ accountSetId }))
        } catch (err) {
            setLoadError(await apiErrorMessage(err, "加载凭证字失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void load()
    }, [load])

    const defaultWord = words.find((w) => w.isDefault)

    const term = keyword.trim()
    const visibleWords = useMemo(
        () =>
            term
                ? words.filter((w) => [w.word, w.printTitle].some((v) => (v ?? "").includes(term)))
                : words,
        [words, term],
    )

    function openCreate() {
        setEditing(null)
        setForm(emptyForm)
        setTitleTouched(false)
        setFormError("")
        setFormOpen(true)
    }

    function openEdit(w: VoucherWordDto) {
        setEditing(w)
        setForm({ word: w.word ?? "", printTitle: w.printTitle ?? "", isDefault: w.isDefault === true })
        setTitleTouched(true)
        setFormError("")
        setFormOpen(true)
    }

    /** 输入凭证字：标题尚未手动修改时，按常用字自动带出打印标题。 */
    function changeWord(next: string) {
        setForm((prev) => ({
            ...prev,
            word: next,
            printTitle: titleTouched ? prev.printTitle : next.trim() ? defaultPrintTitle(next.trim()) : "",
        }))
    }

    function applyPreset(preset: (typeof PRESETS)[number]) {
        setForm((prev) => ({ ...prev, word: preset.word, printTitle: preset.printTitle }))
        setTitleTouched(false)
    }

    function validate(): string | null {
        const word = form.word.trim()
        if (!word) return "请填写凭证字。"
        if (word.length > WORD_MAX_LENGTH) return `凭证字不能超过 ${WORD_MAX_LENGTH} 个字符。`
        if (WORD_INVALID_PATTERN.test(form.word)) return "凭证字不能包含逗号、顿号或空格。"
        if (words.some((w) => w.id !== editing?.id && w.word === word)) return `凭证字「${word}」已存在。`
        const title = form.printTitle.trim()
        if (!title) return "请填写打印标题。"
        if (title.length > TITLE_MAX_LENGTH) return `打印标题不能超过 ${TITLE_MAX_LENGTH} 个字符。`
        return null
    }

    async function save() {
        if (saving) return
        const invalid = validate()
        if (invalid) {
            setFormError(invalid)
            return
        }
        setSaving(true)
        setFormError("")
        const payload: SaveVoucherWordRequest = {
            word: form.word.trim(),
            printTitle: form.printTitle.trim(),
            isDefault: form.isDefault,
        }
        try {
            const api = new VoucherWordsApi(apiConfig(token))
            if (editing?.id) {
                await api.accountsetsAccountSetIdVoucherwordsIdPut({ accountSetId, id: editing.id, saveVoucherWordRequest: payload })
            } else {
                await api.accountsetsAccountSetIdVoucherwordsPost({ accountSetId, saveVoucherWordRequest: payload })
            }
            setFormOpen(false)
            message.success(editing ? "凭证字已更新。" : "凭证字已新增。")
            await load()
            refreshAccountSets()
        } catch (err) {
            setFormError(await apiErrorMessage(err, "保存凭证字失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function setDefault(w: VoucherWordDto) {
        if (busyId || w.isDefault) return
        setBusyId(w.id ?? null)
        try {
            await new VoucherWordsApi(apiConfig(token)).accountsetsAccountSetIdVoucherwordsIdPut({
                accountSetId,
                id: w.id ?? "",
                saveVoucherWordRequest: {
                    word: w.word ?? "",
                    printTitle: w.printTitle ?? "",
                    isDefault: true,
                },
            })
            message.success(`已将「${w.word}」设为默认凭证字。`)
            await load()
            refreshAccountSets()
        } catch (err) {
            setLoadError(await apiErrorMessage(err, "设置默认凭证字失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    async function remove(w: VoucherWordDto) {
        if (!w.id || w.isDefault) return
        const ok = await confirm(
            `确定删除凭证字「${w.word}」吗？已使用该字的历史凭证不受影响，但制单下拉将不再提供该字。`,
        )
        if (!ok) return
        setBusyId(w.id)
        try {
            await new VoucherWordsApi(apiConfig(token)).accountsetsAccountSetIdVoucherwordsIdDelete({
                accountSetId,
                id: w.id,
            })
            message.success("凭证字已删除。")
            await load()
            refreshAccountSets()
        } catch (err) {
            setLoadError(await apiErrorMessage(err, "删除凭证字失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    const columns: TableColumnsType<VoucherWordDto> = [
        {
            title: "凭证字",
            width: 120,
            render: (_, w) => (
                <span
                    className="inline-flex h-7 min-w-7 items-center justify-center rounded-[4px] px-1.5 text-[14px] font-medium"
                    style={{ background: "color-mix(in srgb, var(--brand) 10%, white)", color: "var(--brand)" }}
                >
                    {w.word}
                </span>
            ),
        },
        {
            title: "打印标题",
            render: (_, w) =>
                w.printTitle ? <span className="text-(--foreground)">{w.printTitle}</span> : <span className="text-(--muted-foreground)">—</span>,
        },
        {
            title: "默认",
            width: 100,
            render: (_, w) =>
                w.isDefault ? <StatusPill tone="blue">默认</StatusPill> : <span className="text-(--muted-foreground)">—</span>,
        },
        {
            title: "操作",
            width: 200,
            render: (_, w) => {
                const id = w.id ?? ""
                return (
                    <span className="flex items-center gap-3 text-[13px]">
                        {!w.isDefault && (
                            <Typography.Link
                                className={busyId === id ? "pointer-events-none opacity-50" : undefined}
                                onClick={() => void setDefault(w)}
                            >
                                设为默认
                            </Typography.Link>
                        )}
                        <Typography.Link className="text-[13px]" onClick={() => openEdit(w)}>
                            编辑
                        </Typography.Link>
                        {w.isDefault ? (
                            <Tooltip title="默认凭证字不可删除，请先将其他凭证字设为默认。">
                                <span className="cursor-not-allowed text-(--muted-foreground)">删除</span>
                            </Tooltip>
                        ) : (
                            <Typography.Link
                                className={busyId === id ? "pointer-events-none opacity-50" : undefined}
                                style={{ color: "var(--tone-red)" }}
                                onClick={() => void remove(w)}
                            >
                                删除
                            </Typography.Link>
                        )}
                    </span>
                )
            },
        },
    ]

    const emptyText = term ? (
        <div className="flex flex-col items-center gap-2 py-10">
            <SearchOutlined style={{ fontSize: 26, color: "var(--icon-placeholder)" }} />
            <span className="text-[13px] text-(--muted-foreground)">没有匹配「{term}」的凭证字</span>
            <Button type="link" size="small" onClick={() => setKeyword("")}>
                清除查询条件
            </Button>
        </div>
    ) : (
        <div className="flex flex-col items-center gap-3 py-10">
            <StarOutlined style={{ fontSize: 30, color: "var(--icon-placeholder)" }} />
            <div className="text-center">
                <div className="text-[13px] text-(--muted-foreground)">还没有凭证字</div>
                <div className="mt-1 text-[12px] text-(--muted-foreground)">新增后制单时可选用，并作为凭证编号前缀</div>
            </div>
            <Button type="primary" size="small" icon={<PlusOutlined />} onClick={openCreate}>
                新增第一个凭证字
            </Button>
        </div>
    )

    return (
        <>
            <Card>
                <div className="flex flex-wrap items-start justify-between gap-3 px-4 pt-4">
                    <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                            <h3 className="text-[15px] font-semibold text-(--foreground)">凭证字</h3>
                            <StatusPill tone="gray">制单 · 打印共用</StatusPill>
                        </div>
                        <p className="mt-1 text-[12.5px] leading-5 text-(--muted-foreground)">
                            凭证字是凭证编号的前缀（如「记-1」），同时决定凭证的打印标题；默认凭证字会作为新建凭证的预选值。
                            这里的改动会立即同步到制单下拉。
                        </p>
                    </div>
                    <Button icon={<ReloadOutlined spin={loading} />} onClick={() => void load()} disabled={loading}>
                        刷新
                    </Button>
                </div>

                <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 px-4 py-3">
                    <Input
                        allowClear
                        style={{ width: 220 }}
                        prefix={<SearchOutlined style={{ color: "var(--icon-muted)" }} />}
                        placeholder="输入凭证字 / 打印标题"
                        value={keyword}
                        onChange={(e) => setKeyword(e.target.value)}
                    />
                    <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                        新增凭证字
                    </Button>
                </div>

                {loadError && (
                    <div className="px-4 pb-3">
                        <Alert type="error" showIcon title={loadError} closable onClose={() => setLoadError(null)} />
                    </div>
                )}

                <div className="px-3">
                    <Table
                        columns={columns}
                        dataSource={visibleWords}
                        rowKey={(w) => w.id ?? ""}
                        rowClassName={(w) => (w.isDefault ? "bg-(--brand-soft)" : "")}
                        pagination={false}
                        loading={loading}
                        locale={{ emptyText }}
                    />
                </div>

                <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-[12.5px] text-(--muted-foreground)">
                    <span>
                        {term ? (
                            <>
                                筛选出 <span className="font-medium tabular-nums text-(--foreground)">{visibleWords.length}</span> 个
                                · 全部 <span className="font-medium tabular-nums text-(--foreground)">{words.length}</span> 个
                            </>
                        ) : (
                            <>
                                共 <span className="font-medium tabular-nums text-(--foreground)">{words.length}</span> 个凭证字
                            </>
                        )}
                        {defaultWord && <> · 默认「{defaultWord.word}」</>}
                    </span>
                    <span className="text-(--muted-foreground)">至少保留一个默认凭证字</span>
                </div>
            </Card>

            <Modal
                open={formOpen}
                width={464}
                title={editing ? `编辑凭证字 — ${editing.word}` : "新增凭证字"}
                okText="保存"
                cancelText="取消"
                confirmLoading={saving}
                onOk={() => void save()}
                onCancel={() => setFormOpen(false)}
            >
                <div className="flex flex-col gap-4">
                    <FormField
                        label="凭证字"
                        htmlFor="voucher-word"
                        required
                        error={formError && !form.word.trim() ? formError : undefined}
                        description={`1–${WORD_MAX_LENGTH} 个字符，不能含逗号、顿号或空格。`}
                    >
                        <Input
                            id="voucher-word"
                            value={form.word}
                            maxLength={WORD_MAX_LENGTH}
                            showCount
                            placeholder="如 记、收、付、转"
                            status={formError && !form.word.trim() ? "error" : undefined}
                            onChange={(e) => changeWord(e.target.value)}
                            onPressEnter={() => void save()}
                        />
                    </FormField>

                    <div className="flex flex-wrap items-center gap-2">
                        <span className="text-[12.5px] text-(--muted-foreground)">常用：</span>
                        {PRESETS.map((preset) => {
                            const taken = words.some((w) => w.id !== editing?.id && w.word === preset.word)
                            return (
                                <Button
                                    key={preset.word}
                                    size="small"
                                    disabled={taken}
                                    title={taken ? "已存在同名凭证字" : `填入「${preset.word}」`}
                                    onClick={() => applyPreset(preset)}
                                >
                                    {preset.word}
                                </Button>
                            )
                        })}
                    </div>

                    <FormField
                        label="打印标题"
                        htmlFor="voucher-print-title"
                        required
                        description="打印凭证时显示在表头的标题，如「记账凭证」。"
                    >
                        <Input
                            id="voucher-print-title"
                            value={form.printTitle}
                            maxLength={TITLE_MAX_LENGTH}
                            showCount
                            placeholder="如 记账凭证"
                            onChange={(e) => {
                                setTitleTouched(true)
                                setForm((prev) => ({ ...prev, printTitle: e.target.value }))
                            }}
                        />
                    </FormField>

                    <div className="flex flex-col gap-1">
                        <Checkbox
                            checked={form.isDefault}
                            disabled={editing?.isDefault === true}
                            onChange={(e) => setForm((prev) => ({ ...prev, isDefault: e.target.checked }))}
                        >
                            设为默认凭证字
                        </Checkbox>
                        <span className="pl-6 text-[12px] text-(--muted-foreground)">
                            {editing?.isDefault
                                ? "当前默认凭证字不能取消默认，请先把其他凭证字设为默认。"
                                : "新建凭证默认选中该字；设置后会取消原默认凭证字。"}
                        </span>
                    </div>

                    {formError && form.word.trim() && (
                        <Alert type="error" showIcon title={formError} />
                    )}
                </div>
            </Modal>

            {confirmDialog}
        </>
    )
}
