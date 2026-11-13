/**
 * 会计电子档案（对应柠檬云 /ERecord/ERecord，已接真实后端 ArchivesApi）：
 * 档案列表（电脑上传 / 审核 / 归档 / 删除 / 下载 / 搜索 / 按目录类别过滤）+ 归档页签 + 右侧空间面板。
 * 收票宝上传跳转收票宝页；关联凭证/生成凭证/标记纸质档案依赖未建模型，保持原样。
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router";
import dayjs from "dayjs";
import { App, Button, DatePicker, Dropdown, Empty, Input, Popover, Select as AntSelect, Table } from "antd";
import {
    CaretDownOutlined,
    CaretRightOutlined,
    CrownOutlined,
    DeleteOutlined,
    EditOutlined,
    FileTextOutlined,
    FolderOpenOutlined,
    FolderOutlined,
    LeftOutlined,
    MoreOutlined,
    PictureOutlined,
    PlusOutlined,
    RightOutlined,
} from "@ant-design/icons";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { StatusPill } from "@/components/page/status-pill";
import { FilterTriggerButton } from "@/components/page/filter-trigger-button";
import { ICON_STYLE, type PillTone, FORM_CONTROL_W } from "@/components/page/styles";

import {
    Btn,
    CheckBox,
    Dialog,
    HintBox,
    Page,
    RefreshIcon,
    Select,
    SubTabs,
    TextInput,
    Toast,
    ToolRow,
} from "./components/ui";
import { AccountsApi, ArchivesApi, VouchersApi, type AccountDto, type ArchiveCategoryDto, type ArchiveItemDto, type VoucherListItemDto } from "@/apis-gen";

/** 前端上传大小上限（8 MB）：整文件 Base64 会再膨胀约 1/3，后端单文件/额度均为 10 MB，留出余量。 */
const MAX_UPLOAD_BYTES = 8 * 1024 * 1024;

/** 字节数格式化。 */
function fmtBytes(n: number): string {
    if (n < 1024) {
        return `${n} B`;
    }
    if (n < 1024 * 1024) {
        return `${(n / 1024).toFixed(2)} KB`;
    }
    return `${(n / 1024 / 1024).toFixed(2)} MB`;
}

/** 上传日期（运行时为 Date），列表与筛选统一按 yyyy-MM-dd。 */
function fmtDate(value?: Date | null): string {
    return value ? new Date(value).toISOString().substring(0, 10) : "";
}

/** 「文件名增加凭证号」：勾选后展示名在扩展名前插入关联凭证号。 */
function nameWithVoucher(fileName: string, voucherText?: string | null): string {
    if (!voucherText) {
        return fileName;
    }
    const dot = fileName.lastIndexOf(".");
    const suffix = `-${voucherText}`;
    return dot > 0 ? `${fileName.slice(0, dot)}${suffix}${fileName.slice(dot)}` : `${fileName}${suffix}`;
}

/** 档案编码：后端未单独存编码，按「归档日期 + 档案 id 前 8 位」生成稳定可读的标识。 */
function archiveCode(it: ArchiveItemDto): string {
    const date = fmtDate(it.archivedAt).replace(/-/g, "");
    return `DA${date}${(it.id ?? "").replace(/-/g, "").slice(0, 8).toUpperCase()}`;
}

function timeOf(value?: Date | null): number {
    return value ? new Date(value).getTime() : 0;
}

/** 内置根节点与路径分隔符，须与后端 ArchiveCategoryService / ArchiveCategory.PathSeparator 一致。 */
const ALL_FILES = "所有文件";
const BACKUP_FOLDER = "数据备份";
const PATH_SEP = ">";

/** 分类树节点：后端扁平 DTO + 前端算出的父子关系、层级与完整路径。 */
type CategoryNode = ArchiveCategoryDto & {
    key: string;
    path: string;
    depth: number;
    children: CategoryNode[];
};

/** 把后端返回的扁平节点列表组成树，并自顶向下算出每个节点的分类路径。 */
function buildCategoryTree(list: ArchiveCategoryDto[]): CategoryNode[] {
    const nodes = new Map<string, CategoryNode>();
    for (const c of list) {
        const key = c.id ?? "";
        nodes.set(key, { ...c, key, path: "", depth: 0, children: [] });
    }

    const roots: CategoryNode[] = [];
    for (const c of list) {
        const node = nodes.get(c.id ?? "");
        if (!node) {
            continue;
        }

        const parent = c.parentId ? nodes.get(c.parentId) : undefined;
        if (parent) {
            parent.children.push(node);
        } else {
            roots.push(node);
        }
    }

    const walk = (items: CategoryNode[], depth: number, prefix: string) => {
        items.sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
        for (const n of items) {
            n.depth = depth;
            n.path = prefix ? `${prefix}${PATH_SEP}${n.name ?? ""}` : (n.name ?? "");
            walk(n.children, depth + 1, n.path);
        }
    };
    walk(roots, 0, "");
    return roots;
}

/** 深度优先摊平整棵树（上传路径下拉、默认展开状态用）。 */
function flattenCategoryTree(roots: CategoryNode[]): CategoryNode[] {
    const out: CategoryNode[] = [];
    const walk = (items: CategoryNode[]) => {
        for (const n of items) {
            out.push(n);
            walk(n.children);
        }
    };
    walk(roots);
    return out;
}

/**
 * 档案类别归一化成完整路径：早期数据只存单层名称（「凭证附件」或旧版自建目录名），
 * 统一视为挂在「所有文件」下，避免改成树之后历史档案筛选不到。
 */
function normalizeCategoryPath(category?: string | null): string {
    const raw = (category ?? "").trim();
    if (!raw) {
        return ALL_FILES;
    }
    return raw.includes(PATH_SEP) ? raw : `${ALL_FILES}${PATH_SEP}${raw}`;
}

/** 路径的最后一段（列表「文件类别」列显示叶子名，「路径」列显示全路径）。 */
function categoryLeaf(path: string): string {
    const parts = path.split(PATH_SEP);
    return parts[parts.length - 1] ?? path;
}

/** 档案状态文案 → 胶囊色调：完成语义绿、异常红、待处理琥珀，其余浅灰。 */
function statusTone(text?: string | null): PillTone {
    const s = text ?? "";
    if (/已归档|已审核|通过|已校验|正常/.test(s)) return "green";
    if (/驳回|失败|异常|缺失/.test(s)) return "red";
    if (/待|未|审核中/.test(s)) return "amber";
    return "gray";
}

/** 状态列胶囊：无内容时保持空白（与原纯文本渲染一致）。 */
function StatusCell({ text }: { text?: string | null }) {
    return text ? <StatusPill tone={statusTone(text)}>{text}</StatusPill> : <>{text}</>;
}

/** 默认筛选区间：当月 1 号至今天（与柠檬云默认口径一致）。 */
function defaultRange(): [string, string] {
    const now = dayjs();
    return [now.startOf("month").format("YYYY-MM-DD"), now.format("YYYY-MM-DD")];
}

/** 三态筛选（全部 / 是 / 否）。 */
type TriState = "all" | "yes" | "no";

/**
 * 电子档案列表高级筛选条件（对照柠檬云「电子档案 → 漏斗」面板）。
 * 凭证日期 / 凭证字 / 凭证号不直接落在档案上，通过档案关联的 voucherText 到凭证索引里取。
 */
type ERecordFilters = {
    uploadFrom: string | null;
    uploadTo: string | null;
    voucherFrom: string | null;
    voucherTo: string | null;
    voucherWord: string | null;
    voucherNoFrom: string;
    voucherNoTo: string;
    uploader: string;
    matter: string;
    source: string;
    status: string;
    docType: string;
    generated: TriState;
    paper: TriState;
    checkStatus: string;
    fileName: string;
    category: string;
};

/** 下拉里的「全部」哨兵值。 */
const ALL_OPTION = "__all__";

const EMPTY_ERECORD_FILTERS: ERecordFilters = {
    uploadFrom: null,
    uploadTo: null,
    voucherFrom: null,
    voucherTo: null,
    voucherWord: null,
    voucherNoFrom: "",
    voucherNoTo: "",
    uploader: "",
    matter: "",
    source: "",
    status: ALL_OPTION,
    docType: "",
    generated: "all",
    paper: "all",
    checkStatus: "",
    fileName: "",
    category: "",
};

/** 默认筛选：上传日期 = 当月 1 号至今天，其余留空（与柠檬云默认口径一致）。 */
function defaultERecordFilters(): ERecordFilters {
    const [from, to] = defaultRange();
    return { ...EMPTY_ERECORD_FILTERS, uploadFrom: from, uploadTo: to };
}

/** 文本条件：空条件不过滤，否则做不区分大小写的包含匹配。 */
function matchText(value: string | null | undefined, cond: string): boolean {
    const q = cond.trim().toLowerCase();
    return !q || (value ?? "").toLowerCase().includes(q);
}

/** 关联凭证文本形如「2026年9月 记-1」：凭证列表取不到时兜底解析凭证字与号。 */
function parseVoucherWord(text?: string | null): string {
    const m = /(?:^|\s)([^\s\d-]+)-\d+\s*$/.exec(text ?? "");
    return m ? m[1] : "";
}

function parseVoucherNo(text?: string | null): number | null {
    const m = /-(\d+)\s*$/.exec(text ?? "");
    return m ? Number(m[1]) : null;
}

/** 筛选面板一行：右对齐标题 + 控件列（两列式，与全站筛选面板一致）。 */
function FilterRow({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <div className="flex items-center gap-3" data-filter-row={label}>
            <span className="w-[100px] shrink-0 text-right text-[13px] text-(--foreground)">{label}</span>
            <div className="flex min-w-0 flex-1 items-center gap-2">{children}</div>
        </div>
    );
}

/** 三态下拉固定选项。 */
const TRI_OPTIONS = [
    { value: "all", label: "全部" },
    { value: "yes", label: "是" },
    { value: "no", label: "否" },
];

/**
 * 电子档案筛选面板：13 行条件与柠檬云参考图一一对应。
 * 底部「确定 / 取消 / 重置 / 恢复默认排序」由调用方渲染（与凭证管理页同款）。
 */
function ERecordFilterPanel({
    value,
    onChange,
    statusOptions,
    wordOptions,
}: {
    value: ERecordFilters;
    onChange: (next: ERecordFilters) => void;
    statusOptions: string[];
    wordOptions: string[];
}) {
    const patch = (part: Partial<ERecordFilters>) => onChange({ ...value, ...part });
    const dateField = (v: string | null, set: (s: string | null) => void) => (
        <DatePicker
            style={{ flex: 1, minWidth: 0 }}
            allowClear
            placeholder=""
            value={v ? dayjs(v) : null}
            onChange={(d) => set(d ? d.format("YYYY-MM-DD") : null)}
        />
    );
    const textField = (v: string, set: (s: string) => void, placeholder?: string) => (
        <Input
            allowClear
            style={{ width: "100%" }}
            placeholder={placeholder}
            value={v}
            onChange={(e) => set(e.target.value)}
        />
    );
    const sep = <span className="shrink-0 text-[13px] text-(--muted-foreground)">至</span>;

    return (
        <div className="flex max-h-[560px] flex-col gap-2.5 overflow-y-auto py-1 pr-1">
            <FilterRow label="上传日期：">
                {dateField(value.uploadFrom, (s) => patch({ uploadFrom: s }))}
                {sep}
                {dateField(value.uploadTo, (s) => patch({ uploadTo: s }))}
            </FilterRow>
            <FilterRow label="凭证日期：">
                {dateField(value.voucherFrom, (s) => patch({ voucherFrom: s }))}
                {sep}
                {dateField(value.voucherTo, (s) => patch({ voucherTo: s }))}
            </FilterRow>
            <FilterRow label="凭证字号：">
                <AntSelect
                    style={{ width: 130 }}
                    allowClear
                    placeholder="请选择"
                    value={value.voucherWord ?? undefined}
                    options={wordOptions.map((w) => ({ value: w, label: w }))}
                    onChange={(v) => patch({ voucherWord: v ?? null, voucherNoFrom: "", voucherNoTo: "" })}
                />
                <Input
                    disabled={!value.voucherWord}
                    style={{ flex: 1, minWidth: 0 }}
                    value={value.voucherNoFrom}
                    onChange={(e) => patch({ voucherNoFrom: e.target.value.replace(/\D/g, "") })}
                />
                {sep}
                <Input
                    disabled={!value.voucherWord}
                    style={{ flex: 1, minWidth: 0 }}
                    value={value.voucherNoTo}
                    onChange={(e) => patch({ voucherNoTo: e.target.value.replace(/\D/g, "") })}
                />
            </FilterRow>
            <FilterRow label="上传人：">{textField(value.uploader, (s) => patch({ uploader: s }))}</FilterRow>
            <FilterRow label="事项：">{textField(value.matter, (s) => patch({ matter: s }), "请输入")}</FilterRow>
            <FilterRow label="上传方式：">{textField(value.source, (s) => patch({ source: s }))}</FilterRow>
            <FilterRow label="文件状态：">
                <AntSelect
                    style={{ width: "100%" }}
                    value={value.status}
                    options={[{ value: ALL_OPTION, label: "全部" }, ...statusOptions.map((s) => ({ value: s, label: s }))]}
                    onChange={(v) => patch({ status: v })}
                />
            </FilterRow>
            <FilterRow label="单据类型：">{textField(value.docType, (s) => patch({ docType: s }))}</FilterRow>
            <FilterRow label="生成凭证：">
                <AntSelect
                    style={{ width: "100%" }}
                    value={value.generated}
                    options={TRI_OPTIONS}
                    onChange={(v) => patch({ generated: v as TriState })}
                />
            </FilterRow>
            <FilterRow label="纸质档案：">
                <AntSelect
                    style={{ width: "100%" }}
                    value={value.paper}
                    options={TRI_OPTIONS}
                    onChange={(v) => patch({ paper: v as TriState })}
                />
            </FilterRow>
            <FilterRow label="查验状态：">{textField(value.checkStatus, (s) => patch({ checkStatus: s }))}</FilterRow>
            <FilterRow label="文件名称：">{textField(value.fileName, (s) => patch({ fileName: s }), "请输入")}</FilterRow>
            <FilterRow label="文件类别：">{textField(value.category, (s) => patch({ category: s }))}</FilterRow>
        </div>
    );
}

/** 分类编辑弹窗：add = 在 node 下新增子节点；rename = 改 node 自己的名字。 */
type CategoryDialog = { mode: "add" | "rename"; node: CategoryNode | null; fromUpload?: boolean };

type ListColumn = {
    key: string;
    title: string;
    width?: number;
    render: (it: ArchiveItemDto) => React.ReactNode;
};

export default function LemonERecord() {
    const navigate = useNavigate();
    const { modal } = App.useApp();
    const { token, user } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [tab, setTab] = useState(0);
    const [items, setItems] = useState<ArchiveItemDto[]>([]);
    const [archived, setArchived] = useState<ArchiveItemDto[]>([]);
    const [usage, setUsage] = useState<{ used: number; total: number } | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [keyword, setKeyword] = useState("");
    /** 当前选中的分类路径；null = 「所有文件」（不过滤）。 */
    const [folderPath, setFolderPath] = useState<string | null>(null);
    /** 高级筛选：filters = 已生效条件，draft = 面板里未点「确定」的草稿。 */
    const [filters, setFilters] = useState<ERecordFilters>(defaultERecordFilters);
    const [draft, setDraft] = useState<ERecordFilters>(defaultERecordFilters);
    const [filterOpen, setFilterOpen] = useState(false);
    /** 凭证索引：给「凭证日期 / 凭证字 / 凭证号」提供真实数据，首次打开筛选面板时懒加载。 */
    const [voucherList, setVoucherList] = useState<VoucherListItemDto[]>([]);
    const [withVoucherNo, setWithVoucherNo] = useState(false);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [pickerOpen, setPickerOpen] = useState(false);
    const [pickIds, setPickIds] = useState<Set<string>>(new Set());
    /** 电脑上传弹窗：文件路径（分类树节点全路径）、待上传文件、是否标记为纸质档案。 */
    const [uploadOpen, setUploadOpen] = useState(false);
    const [uploadFolder, setUploadFolder] = useState(`${ALL_FILES}${PATH_SEP}凭证附件`);
    const [uploadFiles, setUploadFiles] = useState<File[]>([]);
    const [uploadPaper, setUploadPaper] = useState(false);
    /** 分类树：后端扁平节点、展开中的节点、新增/重命名弹窗状态。 */
    const [categories, setCategories] = useState<ArchiveCategoryDto[]>([]);
    const [expanded, setExpanded] = useState<Set<string>>(new Set());
    const [catDialog, setCatDialog] = useState<CategoryDialog | null>(null);
    const [catName, setCatName] = useState("");
    /** 关联凭证弹窗选中的凭证文本（如 2026年9月 记-1）。 */
    const [linkOpen, setLinkOpen] = useState(false);
    const [linkVoucher, setLinkVoucher] = useState("");
    const [linkChoices, setLinkChoices] = useState<{ id: string; text: string }[]>([]);
    /** 由档案生成凭证弹窗：日期 / 摘要 / 金额 / 借贷科目。 */
    const [genOpen, setGenOpen] = useState(false);
    const [genSaving, setGenSaving] = useState(false);
    const [genForm, setGenForm] = useState({ date: dayjs().format("YYYY-MM-DD"), summary: "", amount: "", debitAccountId: "", creditAccountId: "" });
    const [leafAccounts, setLeafAccounts] = useState<AccountDto[]>([]);
    const addRef = useRef<HTMLInputElement>(null);
    /** 旧版 localStorage 自建目录只迁移一次到后端分类树；展开状态与凭证索引只初始化一次。 */
    const migratedRef = useRef(false);
    const expandedInitRef = useRef(false);
    const voucherLoadedRef = useRef(false);

    const load = useCallback(async () => {
        if (!token || !accountSetId) {
            return;
        }
        try {
            const api = new ArchivesApi(apiConfig(token));
            const [list, archivedList, u] = await Promise.all([
                api.accountsetsAccountSetIdArchivesGet({ accountSetId }),
                api.accountsetsAccountSetIdArchivesArchivedGet({ accountSetId }),
                api.accountsetsAccountSetIdArchivesUsageGet({ accountSetId }),
            ]);
            setItems(list ?? []);
            setArchived(archivedList ?? []);
            setSelected(new Set());
            setUsage({ used: u.usedBytes ?? 0, total: u.totalBytes ?? 0 });
        } catch (e) {
            setToast(await apiErrorMessage(e, "加载电子档案失败"));
        }
    }, [token, accountSetId]);

    /** 加载分类树（后端首次访问自动播种内置节点），并把旧版本机自建目录迁移过来。 */
    const loadCategories = useCallback(async () => {
        if (!token || !accountSetId) {
            return;
        }
        try {
            const api = new ArchivesApi(apiConfig(token));
            let list = await api.accountsetsAccountSetIdArchivesCategoriesGet({ accountSetId });
            if (!migratedRef.current) {
                migratedRef.current = true;
                const key = "yuncai.erecord.folders." + accountSetId;
                try {
                    const raw = window.localStorage.getItem(key);
                    if (raw) {
                        const names = JSON.parse(raw) as string[];
                        const root = (list ?? []).find((c) => !c.parentId && c.name === ALL_FILES);
                        const existing = new Set((list ?? []).map((c) => c.name ?? ""));
                        for (const name of names) {
                            const trimmed = (name ?? "").trim();
                            if (trimmed && root?.id && !existing.has(trimmed)) {
                                await api.accountsetsAccountSetIdArchivesCategoriesPost({
                                    accountSetId,
                                    saveArchiveCategoryRequest: { parentId: root.id, name: trimmed },
                                });
                            }
                        }
                        window.localStorage.removeItem(key);
                        list = await api.accountsetsAccountSetIdArchivesCategoriesGet({ accountSetId });
                    }
                } catch {
                    /* 本机存储不可用或旧数据格式异常时忽略迁移 */
                }
            }

            const safe = list ?? [];
            setCategories(safe);
            if (!expandedInitRef.current) {
                expandedInitRef.current = true;
                const roots = buildCategoryTree(safe);
                setExpanded(new Set(flattenCategoryTree(roots).filter((n) => n.children.length > 0).map((n) => n.key)));
            }
        } catch (e) {
            setToast(await apiErrorMessage(e, "加载分类失败"));
        }
    }, [token, accountSetId]);

    /** 首次展开筛选面板时拉取凭证列表：档案只存 voucherText，日期/字/号要从凭证反查。 */
    const loadVoucherIndex = useCallback(async () => {
        if (voucherLoadedRef.current || !token || !accountSetId) {
            return;
        }
        voucherLoadedRef.current = true;
        try {
            const list = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersGet({ accountSetId });
            setVoucherList(list ?? []);
        } catch {
            setVoucherList([]);
        }
    }, [token, accountSetId]);

    useEffect(() => {
        void load();
        void loadCategories();
    }, [load, loadCategories]);

    useEffect(() => {
        setPage(1);
    }, [tab, folderPath, keyword, filters, pageSize]);

    /** 展开时用已生效条件初始化草稿，避免上次未确认的编辑残留。 */
    const changeFilterOpen = (open: boolean) => {
        setFilterOpen(open);
        if (open) {
            setDraft(filters);
            void loadVoucherIndex();
        }
    };

    /** 确定 / 重置 / 取消 / 恢复默认排序。 */
    const applyFilters = () => {
        setFilters(draft);
        setFilterOpen(false);
        setPage(1);
    };
    const resetDraft = () => setDraft(EMPTY_ERECORD_FILTERS);
    const restoreDefault = () => {
        const def = defaultERecordFilters();
        setFilters(def);
        setDraft(def);
        setFilterOpen(false);
        setPage(1);
    };

    /** 打开「由档案生成凭证」弹窗：需要先勾选档案，并加载末级科目供借贷选择。 */
    const openGen = async () => {
        if (selected.size === 0) {
            setToast("请先勾选要生成凭证的档案。");
            return;
        }
        setGenForm({ date: dayjs().format("YYYY-MM-DD"), summary: "", amount: "", debitAccountId: "", creditAccountId: "" });
        setGenOpen(true);
        try {
            const list = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true });
            setLeafAccounts(list.filter((a) => a.isEnabled));
        } catch {
            setLeafAccounts([]);
        }
    };

    /** 保存：把勾选档案合并为一张借贷凭证。 */
    const saveGen = async () => {
        if (!token || !accountSetId) return;
        if (!genForm.amount || Number(genForm.amount) <= 0) {
            setToast("请填写大于 0 的凭证金额。");
            return;
        }
        if (!genForm.debitAccountId || !genForm.creditAccountId) {
            setToast("请选择借方与贷方科目。");
            return;
        }
        if (genForm.debitAccountId === genForm.creditAccountId) {
            setToast("借方与贷方科目不能相同。");
            return;
        }
        setGenSaving(true);
        try {
            const res = await fetch(`/api/accountsets/${accountSetId}/archives/voucher-from`, {
                method: "POST",
                headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
                body: JSON.stringify({
                    ids: [...selected],
                    date: genForm.date,
                    word: "记",
                    summary: genForm.summary || null,
                    amount: Number(genForm.amount),
                    debitAccountId: genForm.debitAccountId,
                    creditAccountId: genForm.creditAccountId,
                }),
            });
            const body = await res.json().catch(() => null);
            if (!res.ok) {
                const message = body && typeof body === "object" && "message" in body ? String((body as { message?: unknown }).message ?? "") : "";
                setToast(message || `生成凭证失败（HTTP ${res.status}）`);
                return;
            }
            setToast(`已生成凭证 ${String(body?.voucherText ?? "")}，并回写到 ${selected.size} 份档案`);
            setGenOpen(false);
            await load();
        } catch (e) {
            setToast(await apiErrorMessage(e, "生成凭证失败"));
        } finally {
            setGenSaving(false);
        }
    };

    /** 上传：本地文件转 base64 交给后端存档；批量时逐个提交。 */
    const submitUpload = async () => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (uploadFiles.length === 0) {
            setToast("请先选择或拖入要上传的文件");
            return;
        }
        const category = uploadFolder;
        setBusy(true);
        let ok = 0;
        let failed = "";
        const api = new ArchivesApi(apiConfig(token));
        for (const file of uploadFiles) {
            if (file.size > MAX_UPLOAD_BYTES) {
                failed = `「${file.name}」超过 8 MB，已跳过。`;
                continue;
            }
            try {
                const dataUrl = await new Promise<string>((resolve, reject) => {
                    const reader = new FileReader();
                    reader.onload = () => resolve(String(reader.result ?? ""));
                    reader.onerror = () => reject(new Error("读取文件失败"));
                    reader.readAsDataURL(file);
                });
                await api.accountsetsAccountSetIdArchivesPost({
                    accountSetId,
                    saveArchiveRequest: {
                        fileName: file.name,
                        category,
                        docType: categoryLeaf(uploadFolder),
                        fileBase64: dataUrl.includes(",") ? dataUrl.split(",")[1] : dataUrl,
                        uploader: user?.displayName || user?.username || "",
                        source: "电脑上传",
                        isPaper: uploadPaper,
                    },
                });
                ok++;
            } catch (e) {
                failed = await apiErrorMessage(e, `「${file.name}」上传失败`);
            }
        }
        setBusy(false);
        if (addRef.current) {
            addRef.current.value = "";
        }
        if (ok > 0) {
            setUploadOpen(false);
            setUploadFiles([]);
        }
        setToast(failed || `已上传 ${ok} 份档案`);
        if (ok > 0) {
            await load();
        }
    };

    /** 把 FileList 追加进待上传队列（拖拽与粘贴都走这里）。 */
    const addFiles = (files: FileList | File[] | null) => {
        if (!files) {
            return;
        }
        setUploadFiles((prev) => [...prev, ...Array.from(files)]);
    };

    /** 剪贴板粘贴上传：从 clipboardData 里取文件。 */
    const onPaste = (e: React.ClipboardEvent) => {
        const files = e.clipboardData?.files;
        if (files && files.length > 0) {
            e.preventDefault();
            addFiles(files);
        }
    };

    /* ---------------- 分类树：新增 / 重命名 / 删除 ---------------- */

    /** 展开 / 折叠节点。 */
    const toggleExpanded = (key: string) => {
        setExpanded((prev) => {
            const next = new Set(prev);
            if (next.has(key)) {
                next.delete(key);
            } else {
                next.add(key);
            }
            return next;
        });
    };

    /** 在指定节点下新增子分类；内置节点也允许挂子节点。 */
    const openAddCategory = (node: CategoryNode | null, fromUpload = false) => {
        setCatName("");
        setCatDialog({ mode: "add", node, fromUpload });
    };

    /** 重命名自建分类。 */
    const openRenameCategory = (node: CategoryNode) => {
        setCatName(node.name ?? "");
        setCatDialog({ mode: "rename", node });
    };

    /** 提交新增 / 重命名：刷新树，并同步上传路径与当前筛选路径。 */
    const saveCategory = async () => {
        if (!token || !accountSetId || !catDialog || busy) {
            return;
        }
        const name = catName.trim();
        if (!name) {
            setToast("请输入分类名称");
            return;
        }
        if (name.includes(PATH_SEP) || name.includes("/") || name.includes("\\")) {
            setToast("分类名称不能包含路径分隔符");
            return;
        }

        setBusy(true);
        try {
            const api = new ArchivesApi(apiConfig(token));
            if (catDialog.mode === "add") {
                await api.accountsetsAccountSetIdArchivesCategoriesPost({
                    accountSetId,
                    saveArchiveCategoryRequest: { parentId: catDialog.node?.id ?? null, name },
                });
                if (catDialog.node) {
                    setExpanded((prev) => new Set(prev).add(catDialog.node!.key));
                }
                if (catDialog.fromUpload) {
                    setUploadFolder(catDialog.node ? `${catDialog.node.path}${PATH_SEP}${name}` : name);
                }
                setToast(`已新增分类「${name}」`);
            } else {
                const node = catDialog.node;
                if (!node?.id) {
                    return;
                }
                await api.accountsetsAccountSetIdArchivesCategoriesCategoryIdPut({
                    accountSetId,
                    categoryId: node.id,
                    saveArchiveCategoryRequest: { name },
                });
                const newPath = `${node.path.slice(0, node.path.length - (node.name ?? "").length)}${name}`;
                const shift = (p: string | null): string | null =>
                    p === node.path || (p !== null && p.startsWith(node.path + PATH_SEP))
                        ? newPath + (p ?? "").slice(node.path.length)
                        : p;
                setFolderPath(shift(folderPath));
                setUploadFolder(shift(uploadFolder) ?? `${ALL_FILES}${PATH_SEP}凭证附件`);
                setToast(`分类已重命名为「${name}」`);
            }
            setCatDialog(null);
            await loadCategories();
        } catch (e) {
            setToast(await apiErrorMessage(e, "分类操作失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 删除自建分类：后端会校验子节点与档案占用，失败时直接透出中文原因。 */
    const confirmDeleteCategory = (node: CategoryNode) => {
        if (!node.id) {
            return;
        }
        modal.confirm({
            title: `确认删除分类「${node.name}」？`,
            content: "只有该分类下没有子分类、也没有档案时才能删除。",
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: async () => {
                if (!token || !accountSetId || !node.id) {
                    return;
                }
                setBusy(true);
                try {
                    const api = new ArchivesApi(apiConfig(token));
                    await api.accountsetsAccountSetIdArchivesCategoriesCategoryIdDelete({
                        accountSetId,
                        categoryId: node.id,
                    });
                    if (folderPath === node.path || folderPath?.startsWith(node.path + PATH_SEP)) {
                        setFolderPath(null);
                    }
                    if (uploadFolder === node.path || uploadFolder.startsWith(node.path + PATH_SEP)) {
                        setUploadFolder(`${ALL_FILES}${PATH_SEP}凭证附件`);
                    }
                    setToast(`已删除分类「${node.name}」`);
                    await loadCategories();
                } catch (e) {
                    setToast(await apiErrorMessage(e, "删除分类失败"));
                } finally {
                    setBusy(false);
                }
            },
        });
    };

    /** 节点右侧 ⋮ 菜单：内置节点只能新增子节点，自建节点可增删改。 */
    const categoryMenu = (node: CategoryNode) => ({
        items: [
            { key: "add", icon: <PlusOutlined />, label: "新增" },
            ...(node.isSystem
                ? []
                : [
                      { key: "rename", icon: <EditOutlined />, label: "重命名" },
                      { key: "delete", icon: <DeleteOutlined />, label: "删除", danger: true },
                  ]),
        ],
        onClick: ({ key }: { key: string }) => {
            if (key === "add") {
                openAddCategory(node);
            } else if (key === "rename") {
                openRenameCategory(node);
            } else {
                confirmDeleteCategory(node);
            }
        },
    });

    /** 批量动作：审核（通过 / 取消审核）/ 归档；ids 缺省取列表勾选项。 */
    const bulk = async (action: "audit" | "archive", audited = true, ids: string[] = [...selected]) => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (ids.length === 0) {
            setToast(action === "archive" ? "请先勾选要归档的档案" : "请先勾选档案");
            return;
        }
        setBusy(true);
        try {
            const api = new ArchivesApi(apiConfig(token));
            if (action === "audit") {
                const n = await api.accountsetsAccountSetIdArchivesAuditPost({
                    accountSetId,
                    auditArchiveRequest: { ids, audited },
                });
                setToast(`${audited ? "已审核" : "已取消审核"} ${n} 份`);
            } else {
                const n = await api.accountsetsAccountSetIdArchivesArchivePost({
                    accountSetId,
                    archiveIdsRequest: { ids },
                });
                setToast(`已归档 ${n} 份`);
            }

            await load();
        } catch (e) {
            setToast(await apiErrorMessage(e, "操作失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 批量标记 / 取消标记为纸质档案；ids 缺省取列表勾选项。 */
    const setPaper = async (isPaper: boolean, ids: string[] = [...selected]) => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (ids.length === 0) {
            setToast("请先勾选档案");
            return;
        }
        setBusy(true);
        try {
            const api = new ArchivesApi(apiConfig(token));
            const n = await api.accountsetsAccountSetIdArchivesPaperPost({
                accountSetId,
                paperArchiveRequest: { ids, isPaper },
            });
            setToast(`${isPaper ? "已标记" : "已取消标记"} ${n} 份纸质档案`);
            await load();
        } catch (e) {
            setToast(await apiErrorMessage(e, "操作失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 批量关联凭证；voucherText 为空表示取消关联。 */
    const linkVouchers = async (voucherText: string, ids: string[] = [...selected]) => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (ids.length === 0) {
            setToast("请先勾选档案");
            return;
        }
        setBusy(true);
        try {
            const api = new ArchivesApi(apiConfig(token));
            const n = await api.accountsetsAccountSetIdArchivesLinkVoucherPost({
                accountSetId,
                linkVoucherRequest: { ids, voucherText: voucherText || null },
            });
            setToast(voucherText ? `已为 ${n} 份档案关联凭证「${voucherText}」` : `已取消 ${n} 份档案的凭证关联`);
            setLinkOpen(false);
            setLinkVoucher("");
            await load();
        } catch (e) {
            setToast(await apiErrorMessage(e, "关联凭证失败"));
        } finally {
            setBusy(false);
        }
    };

    /** 打开关联凭证弹窗：拉取当前账套凭证作为可选项。 */
    const openLink = async () => {
        if (!token || !accountSetId) {
            return;
        }
        try {
            const api = new VouchersApi(apiConfig(token));
            const list = await api.accountsetsAccountSetIdVouchersGet({ accountSetId });
            setLinkChoices(
                (list ?? []).map((v) => ({
                    id: v.id ?? "",
                    text: `${dayjs(v.date).format("YYYY年M月")} ${v.voucherNo ?? ""}`.trim(),
                })),
            );
            setLinkVoucher("");
            setLinkOpen(true);
        } catch (e) {
            setToast(await apiErrorMessage(e, "加载凭证失败"));
        }
    };

    /** 删除（不可撤销）：先确认再提交，逐条删除并统计成功数。 */
    const remove = async (ids: string[]) => {
        if (!token || !accountSetId || busy || ids.length === 0) {
            return;
        }
        setBusy(true);
        try {
            const api = new ArchivesApi(apiConfig(token));
            const results = await Promise.allSettled(
                ids.map((id) => api.accountsetsAccountSetIdArchivesIdDelete({ accountSetId, id })),
            );
            const ok = results.filter((r) => r.status === "fulfilled").length;
            setToast(`已删除 ${ok} 份`);
            await load();
        } catch (e) {
            setToast(await apiErrorMessage(e, "删除失败"));
        } finally {
            setBusy(false);
        }
    };

    const confirmRemove = (targets: ArchiveItemDto[]) => {
        if (targets.length === 0) {
            setToast("请先勾选档案");
            return;
        }
        modal.confirm({
            title: `确认删除 ${targets.length} 份档案？`,
            content: "删除后文件内容不可恢复。",
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: () => remove(targets.map((it) => it.id ?? "")),
        });
    };

    /* ---------------- 归档页签的「归档」选择弹窗 ---------------- */

    const openPicker = () => {
        setPickIds(new Set());
        setPickerOpen(true);
    };

    const archivePicked = async () => {
        if (pickIds.size === 0) {
            setToast("请先勾选要归档的档案");
            return;
        }
        await bulk("archive", true, [...pickIds]);
        setPickerOpen(false);
        setPickIds(new Set());
    };

    /** 下载档案文件（二进制流，带认证头）。 */
    const download = async (it: ArchiveItemDto) => {
        if (!token || !accountSetId || !it.id) {
            return;
        }
        try {
            const res = await fetch(`/api/accountsets/${accountSetId}/archives/${it.id}/download`, {
                headers: { Authorization: `Bearer ${token}` },
            });
            if (!res.ok) {
                throw new Error("下载失败");
            }

            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            const anchor = document.createElement("a");
            anchor.href = url;
            anchor.download = it.fileName ?? "archive";
            document.body.appendChild(anchor);
            anchor.click();
            anchor.remove();
            URL.revokeObjectURL(url);
        } catch (e) {
            setToast(e instanceof Error ? e.message : "下载失败");
        }
    };

    const changeTab = (i: number) => {
        setTab(i);
        setSelected(new Set());
    };

    /** 点分类节点：根「所有文件」显示全部，其余按该节点路径及其子树过滤。 */
    const changeFolder = (node: CategoryNode | null) => {
        setFolderPath(node && node.name !== ALL_FILES ? node.path : null);
        setSelected(new Set());
    };

    /** 凭证文本 → 日期 / 字 / 号：档案上只有 voucherText，筛选条件靠这张索引落成真实值。 */
    const voucherIndex = useMemo(() => {
        const map = new Map<string, { date: string; word: string; number: number }>();
        for (const v of voucherList) {
            const text = `${dayjs(v.date).format("YYYY年M月")} ${v.voucherNo ?? ""}`.trim();
            map.set(text, { date: fmtDate(v.date), word: v.word ?? "", number: v.number ?? 0 });
        }
        return map;
    }, [voucherList]);

    /** 文件状态 / 凭证字下拉选项取自当前账套已有数据，不写死可能与实际不符的枚举。 */
    const statusOptions = useMemo(
        () => [...new Set(items.map((it) => (it.statusText ?? "").trim()).filter(Boolean))].sort(),
        [items],
    );
    const wordOptions = useMemo(() => {
        const set = new Set<string>();
        voucherList.forEach((v) => v.word && set.add(v.word));
        items.forEach((it) => {
            const w = parseVoucherWord(it.voucherText);
            if (w) {
                set.add(w);
            }
        });
        return [...set];
    }, [voucherList, items]);

    const kw = keyword.trim().toLowerCase();
    const filtered =
        tab === 1
            ? [...archived].sort(
                  (a, b) => timeOf(b.archivedAt ?? b.uploadedAt) - timeOf(a.archivedAt ?? a.uploadedAt),
              )
            : items.filter((it) => {
                  const f = filters;
                  const upload = fmtDate(it.uploadedAt);
                  const path = normalizeCategoryPath(it.category);
                  const inFolder =
                      !folderPath || path === folderPath || path.startsWith(folderPath + PATH_SEP);
                  if (!inFolder) {
                      return false;
                  }

                  // 上传日期区间
                  if (f.uploadFrom && upload < f.uploadFrom) return false;
                  if (f.uploadTo && upload > f.uploadTo) return false;

                  // 凭证日期 / 字号：优先查凭证索引，未加载到凭证时从 voucherText 兜底解析
                  const linked = voucherIndex.get(it.voucherText ?? "");
                  if (f.voucherFrom || f.voucherTo) {
                      const d = linked?.date ?? "";
                      if (!d || (f.voucherFrom && d < f.voucherFrom) || (f.voucherTo && d > f.voucherTo)) {
                          return false;
                      }
                  }
                  if (f.voucherWord) {
                      const word = linked?.word || parseVoucherWord(it.voucherText);
                      if (word !== f.voucherWord) return false;
                      const no = linked?.number ?? parseVoucherNo(it.voucherText);
                      if (no == null) return false;
                      if (f.voucherNoFrom && no < Number(f.voucherNoFrom)) return false;
                      if (f.voucherNoTo && no > Number(f.voucherNoTo)) return false;
                  }

                  if (!matchText(it.uploader, f.uploader)) return false;
                  if (!matchText(it.matter, f.matter)) return false;
                  if (!matchText(it.source, f.source)) return false;
                  if (f.status !== ALL_OPTION && (it.statusText ?? "") !== f.status) return false;
                  if (!matchText(it.docType, f.docType)) return false;
                  if (f.generated !== "all" && Boolean((it.voucherText ?? "").trim()) !== (f.generated === "yes")) {
                      return false;
                  }
                  if (f.paper !== "all" && Boolean(it.isPaper) !== (f.paper === "yes")) return false;
                  if (!matchText(it.checkStatus, f.checkStatus)) return false;
                  if (!matchText(it.fileName, f.fileName)) return false;
                  if (!matchText(it.category, f.category)) return false;

                  return (
                      !kw ||
                      (it.fileName ?? "").toLowerCase().includes(kw) ||
                      (it.relatedNo ?? "").toLowerCase().includes(kw) ||
                      (it.voucherText ?? "").toLowerCase().includes(kw)
                  );
              });
    const total = filtered.length;
    const pageCount = Math.max(1, Math.ceil(total / pageSize));
    const current = Math.min(page, pageCount);
    const paged = filtered.slice((current - 1) * pageSize, current * pageSize);
    const from = total === 0 ? 0 : (current - 1) * pageSize + 1;
    const to = Math.min(current * pageSize, total);

    const keywordBox = (
        <div className="flex items-center gap-2">
            <TextInput
                className="w-[240px]"
                placeholder="请输入文件名称/发票号码搜索"
                value={keyword}
                onChange={setKeyword}
            />
            <span className="cursor-pointer" onClick={() => void load()} title="刷新">
                <RefreshIcon />
            </span>
        </div>
    );

    /** 工具条筛选入口：日期区间摘要 + 漏斗，点击展开 13 项高级筛选（对照柠檬云）。 */
    const filterPopover = (
        <Popover
            open={filterOpen}
            trigger="click"
            placement="bottomLeft"
            onOpenChange={changeFilterOpen}
            content={
                <div className="w-[520px]">
                    <ERecordFilterPanel
                        value={draft}
                        onChange={setDraft}
                        statusOptions={statusOptions}
                        wordOptions={wordOptions}
                    />
                    <div className="mt-3 flex items-center justify-between gap-3 border-t border-(--border) pt-3">
                        <div className="flex items-center gap-2">
                            <Button type="primary" onClick={applyFilters}>
                                确定
                            </Button>
                            <Button onClick={() => setFilterOpen(false)}>取消</Button>
                            <Button onClick={resetDraft}>重置</Button>
                        </div>
                        <Button type="link" onClick={restoreDefault}>
                            恢复默认排序
                        </Button>
                    </div>
                </div>
            }
        >
            <FilterTriggerButton
                title="高级筛选"
                label={`${filters.uploadFrom ?? "全部"} 至 ${filters.uploadTo ?? "全部"}`}
            />
        </Popover>
    );

    /** 分类树：根节点 + 扁平节点列表 + 上传路径下拉选项（排除根节点与「数据备份」支）。 */
    const categoryTree = useMemo(() => buildCategoryTree(categories), [categories]);
    const categoryFlat = useMemo(() => flattenCategoryTree(categoryTree), [categoryTree]);
    const uploadPathOptions = useMemo(
        () =>
            categoryFlat
                .filter((n) => n.path !== ALL_FILES && !n.path.startsWith(BACKUP_FOLDER))
                .map((n) => ({ value: n.path, label: n.path.split(PATH_SEP).join(" / ") })),
        [categoryFlat],
    );

    /** 递归渲染分类树：缩进体现层级、三角展开折叠、⋮ 挂节点操作菜单。 */
    const renderCategoryNodes = (nodes: CategoryNode[]): React.ReactNode =>
        nodes.map((node) => {
            const hasChildren = node.children.length > 0;
            const isOpen = expanded.has(node.key);
            const active = node.name === ALL_FILES ? folderPath === null : folderPath === node.path;
            return (
                <div key={node.key}>
                    <div
                        className={`flex cursor-pointer items-center rounded-lg py-1 pr-1 text-[13px] text-(--foreground) ${
                            active ? "bg-(--brand-soft)" : "hover:bg-(--muted)"
                        }`}
                        style={{ paddingLeft: 8 + node.depth * 20 }}
                        title={node.path}
                        onClick={() => changeFolder(node)}
                    >
                        <span className="flex min-w-0 flex-1 items-center gap-1.5">
                            {hasChildren ? (
                                <span
                                    className="flex w-3 shrink-0 items-center justify-center text-[10px] text-(--muted-foreground)"
                                    onClick={(e) => {
                                        e.stopPropagation();
                                        toggleExpanded(node.key);
                                    }}
                                >
                                    {isOpen ? <CaretDownOutlined /> : <CaretRightOutlined />}
                                </span>
                            ) : (
                                <span className="w-3 shrink-0" />
                            )}
                            {hasChildren && isOpen ? (
                                <FolderOpenOutlined className="shrink-0 text-[14px]" style={ICON_STYLE.warn} />
                            ) : (
                                <FolderOutlined className="shrink-0 text-[14px]" style={ICON_STYLE.warn} />
                            )}
                            <span className="truncate">{node.name}</span>
                        </span>
                        <span onClick={(e) => e.stopPropagation()}>
                            <Dropdown trigger={["click"]} menu={categoryMenu(node)} placement="bottomRight">
                                <Button
                                    type="text"
                                    size="small"
                                    icon={<MoreOutlined style={{ transform: "rotate(90deg)" }} />}
                                    title="分类操作"
                                />
                            </Dropdown>
                        </span>
                    </div>
                    {hasChildren && isOpen && renderCategoryNodes(node.children)}
                </div>
            );
        });

    const auditMenu = {
        items: [
            { key: "audit", label: "审核", disabled: busy || selected.size === 0 },
            { key: "unaudit", label: "取消审核", disabled: busy || selected.size === 0 },
        ],
        onClick: ({ key }: { key: string }) => void bulk("audit", key === "audit"),
    };

    const linkMenu = {
        items: [
            { key: "link", label: "关联凭证", disabled: busy || selected.size === 0 },
            { key: "unlink", label: "取消关联", disabled: busy || selected.size === 0 },
        ],
        onClick: ({ key }: { key: string }) => {
            if (key === "link") {
                void openLink();
            } else {
                void linkVouchers("");
            }
        },
    };

    const paperMenu = {
        items: [
            { key: "paper", label: "标记为纸质档案", disabled: busy || selected.size === 0 },
            { key: "unpaper", label: "取消标记", disabled: busy || selected.size === 0 },
        ],
        onClick: ({ key }: { key: string }) => void setPaper(key === "paper"),
    };

    const moreMenu = {
        items: [
            { key: "archive", label: "归档所选", disabled: busy || selected.size === 0 },
            { key: "remove", label: "删除所选", disabled: busy || selected.size === 0 },
        ],
        onClick: ({ key }: { key: string }) => {
            if (key === "archive") {
                void bulk("archive");
            } else {
                confirmRemove(items.filter((it) => selected.has(it.id ?? "")));
            }
        },
    };

    const fileInputs = (
        <input ref={addRef} type="file" multiple className="hidden" onChange={(e) => addFiles(e.target.files)} />
    );

    const toolList = (
        <ToolRow
            left={
                <>
                    {filterPopover}
                    <Btn variant="primary" onClick={() => navigate(`/${accountSetId}/vouchers/spb`)}>
                        收票宝上传
                    </Btn>
                    <Btn dropdown disabled={busy} onClick={() => setUploadOpen(true)}>
                        电脑上传
                    </Btn>
                    {fileInputs}
                    <Dropdown trigger={["click"]} menu={auditMenu}>
                        <Btn dropdown disabled={busy}>
                            审核
                        </Btn>
                    </Dropdown>
                    <Dropdown trigger={["click"]} menu={linkMenu}>
                        <Btn dropdown disabled={busy}>
                            关联凭证
                        </Btn>
                    </Dropdown>
                    <Btn dropdown disabled={busy} onClick={() => void openGen()}>生成凭证</Btn>
                    <Dropdown trigger={["click"]} menu={paperMenu}>
                        <Btn dropdown disabled={busy}>
                            标记为纸质档案
                        </Btn>
                    </Dropdown>
                    <Dropdown trigger={["click"]} menu={moreMenu}>
                        <Btn dropdown disabled={busy}>
                            更多
                        </Btn>
                    </Dropdown>
                    <CheckBox label="文件名增加凭证号" checked={withVoucherNo} onChange={setWithVoucherNo} />
                </>
            }
            right={keywordBox}
        />
    );

    const toolArchived = (
        <ToolRow
            left={
                <>
                    <Btn variant="primary" onClick={openPicker}>
                        归档
                    </Btn>
                    <Btn
                        disabled={busy}
                        onClick={() => confirmRemove(archived.filter((it) => selected.has(it.id ?? "")))}
                    >
                        删除
                    </Btn>
                </>
            }
        />
    );

    /** 文件名称单元格：pdf/图片图标 + 可点击下载的蓝字。 */
    const fileNameCell = (it: ArchiveItemDto, maxWidth: string) => {
        const name = withVoucherNo ? nameWithVoucher(it.fileName ?? "", it.voucherText) : (it.fileName ?? "");
        return (
            <span
                className={`inline-flex ${maxWidth} cursor-pointer items-center gap-1.5`}
                onClick={() => void download(it)}
                title={`点击下载 ${name}`}
            >
                {(it.fileName ?? "").toLowerCase().endsWith(".pdf") ? (
                    <FileTextOutlined className="text-[14px] shrink-0" style={ICON_STYLE.danger} />
                ) : (
                    <PictureOutlined className="text-[14px] shrink-0" style={{ color: "var(--tone-green)" }} />
                )}
                <span className="truncate text-(--brand) hover:underline">{name}</span>
            </span>
        );
    };

    const listColumns: ListColumn[] = [
        {
            key: "fileName",
            title: "文件名称",
            width: 250,
            render: (it) => fileNameCell(it, "max-w-[250px]"),
        },
        { key: "category", title: "文件类别", width: 96, render: (it) => categoryLeaf(normalizeCategoryPath(it.category)) },
        { key: "docType", title: "单据类型", width: 96, render: (it) => it.docType },
        { key: "uploadedAt", title: "上传日期", width: 104, render: (it) => fmtDate(it.uploadedAt) },
        { key: "relatedDoc", title: "关联单据", width: 110, render: (it) => it.relatedDoc },
        {
            key: "relatedNo",
            title: "关联单据号",
            width: 150,
            render: (it) => <span className="text-(--brand)">{it.relatedNo}</span>,
        },
        {
            key: "voucherText",
            title: "凭证",
            width: 130,
            render: (it) => <span className="text-(--brand)">{it.voucherText}</span>,
        },
        { key: "matter", title: "事项", width: 100, render: (it) => it.matter },
        {
            key: "checkStatus",
            title: "校验状态",
            width: 140,
            render: (it) => (
                <span className="block max-w-[140px] truncate" title={it.checkStatus ?? undefined}>
                    <StatusCell text={it.checkStatus} />
                </span>
            ),
        },
        { key: "statusText", title: "文件状态", width: 92, render: (it) => <StatusCell text={it.statusText} /> },
        { key: "uploader", title: "上传人", width: 90, render: (it) => it.uploader },
        { key: "source", title: "上传方式", width: 100, render: (it) => it.source },
        { key: "size", title: "大小", width: 96, render: (it) => fmtBytes(it.fileSize ?? 0) },
        { key: "paper", title: "纸质档案", width: 84, render: (it) => (it.isPaper ? "是" : "否") },
        {
            key: "path",
            title: "路径",
            width: 150,
            render: (it) => normalizeCategoryPath(it.category),
        },
    ];

    const archivedColumns: ListColumn[] = [
        {
            key: "code",
            title: "档案编码",
            width: 200,
            render: (it) => <span className="text-(--foreground)">{archiveCode(it)}</span>,
        },
        {
            key: "fileName",
            title: "档案名称",
            width: 380,
            render: (it) => fileNameCell(it, "max-w-[380px]"),
        },
        { key: "size", title: "文件大小", width: 130, render: (it) => fmtBytes(it.fileSize ?? 0) },
        { key: "archivedAt", title: "归档日期", width: 140, render: (it) => fmtDate(it.archivedAt) },
        { key: "archivedBy", title: "归档人", width: 130, render: (it) => it.archivedBy },
        {
            key: "ops",
            title: "操作",
            width: 120,
            render: (it) => (
                <span className="flex gap-2 text-(--brand)">
                    <span className="cursor-pointer hover:underline" onClick={() => void download(it)}>
                        下载
                    </span>
                    <span className="cursor-pointer hover:underline" onClick={() => confirmRemove([it])}>
                        删除
                    </span>
                </span>
            ),
        },
    ];

    const columns = tab === 1 ? archivedColumns : listColumns;

    return (
        <Page>
            <SubTabs tabs={["电子档案列表", "电子档案归档"]} active={tab} onChange={changeTab} />
            <div style={{ marginBottom: 16 }}>{tab === 0 ? toolList : toolArchived}</div>
            <div className="flex min-h-0 flex-1">
                <div className="flex min-w-0 flex-1 flex-col px-4">
                    {/* antd Table 自带横向滚动（scroll.x），外层再包 overflow-x-auto 会让 overflow-y 被计算成 auto，多出一条竖向滚动条 */}
                    <Table<ArchiveItemDto>
                        rowKey={(it) => it.id ?? ""}
                        scroll={{ x: "max-content" }}
                        rowSelection={{
                            selectedRowKeys: [...selected],
                            onChange: (keys) => setSelected(new Set(keys as string[])),
                        }}
                        columns={columns.map((c) => ({
                            key: c.key,
                            title: c.title,
                            width: c.width,
                            render: (_: unknown, it: ArchiveItemDto) => c.render(it),
                        }))}
                        dataSource={paged}
                        pagination={false}
                        locale={{
                            emptyText: (
                                <Empty
                                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                                    description={
                                        tab === 1 ? (
                                            <span>
                                                您还没有归档数据，请点击{" "}
                                                <span
                                                    className="cursor-pointer text-(--brand) hover:underline"
                                                    onClick={openPicker}
                                                >
                                                    “归档”
                                                </span>{" "}
                                                按钮进行归档操作哦~
                                            </span>
                                        ) : (
                                            "暂无档案，点击「电脑上传」把凭证附件、回单、发票等文件存入电子档案。"
                                        )
                                    }
                                />
                            ),
                        }}
                    />
                    {(tab === 0 || total > 0) && (
                        <div className="flex flex-wrap items-center justify-between gap-2 border-t border-(--border) px-1 py-3 text-[12.5px] text-(--muted-foreground)">
                            <span>显示 {from} 到 {to}，共 {total} 条</span>
                            <span className="flex items-center gap-2">
                                <Select
                                    className="w-[76px]"
                                    value={String(pageSize)}
                                    onChange={(v) => setPageSize(Number(v))}
                                    options={["20", "50", "100"]}
                                />
                                <Button
                                    type="text"
                                    icon={<LeftOutlined />}
                                    disabled={current <= 1}
                                    onClick={() => setPage(current - 1)}
                                />
                                <span>
                                    第 <span className="font-medium text-(--brand)">{current}</span> / 共 {pageCount} 页
                                </span>
                                <Button
                                    type="text"
                                    icon={<RightOutlined />}
                                    disabled={current >= pageCount}
                                    onClick={() => setPage(current + 1)}
                                />
                                <span className="cursor-pointer" onClick={() => void load()} title="刷新">
                                    <RefreshIcon />
                                </span>
                            </span>
                        </div>
                    )}
                </div>
                {/* 右侧文件目录面板（仅电子档案列表） */}
                {tab === 0 && (
                    <div className="w-[270px] shrink-0 border-l border-(--border) px-3 py-3">
                        <div className="text-[13px] text-(--foreground)">
                            已用空间( <span className="text-(--brand)">{usage ? fmtBytes(usage.used) : "…"}</span> /{" "}
                            {usage ? fmtBytes(usage.total) : "10.00 MB"} )
                        </div>
                        <HintBox className="mt-2 inline-flex cursor-pointer items-center gap-1">
                            <CrownOutlined className="text-[14px]" /> 升级扩容
                        </HintBox>
                        <div className="mt-4 flex flex-col text-[13px]">
                            {categoryTree.length === 0 ? (
                                <div className="px-2 py-3 text-[12.5px] text-(--muted-foreground)">分类加载中…</div>
                            ) : (
                                renderCategoryNodes(categoryTree)
                            )}
                        </div>
                    </div>
                )}
            </div>
            {pickerOpen && (
                <Dialog
                    title="归档档案"
                    width={720}
                    onClose={() => setPickerOpen(false)}
                    footer={
                        <div className="flex justify-end gap-2">
                            <Btn onClick={() => setPickerOpen(false)}>取消</Btn>
                            <Btn variant="primary" disabled={busy} onClick={() => void archivePicked()}>
                                归档
                            </Btn>
                        </div>
                    }
                >
                <div className="mb-2 text-[13px] text-(--muted-foreground)">勾选要归档的档案，归档后可在「电子档案归档」中查看。</div>
                {items.length === 0 ? (
                    <div className="py-10 text-center text-[13px] text-(--muted-foreground)">
                        暂无可归档的档案，请先在「电子档案列表」上传。
                    </div>
                ) : (
                    <Table<ArchiveItemDto>
                        rowKey={(it) => it.id ?? ""}
                        pagination={false}
                        scroll={{ y: 360 }}
                        rowSelection={{
                            selectedRowKeys: [...pickIds],
                            onChange: (keys) => setPickIds(new Set(keys as string[])),
                        }}
                        columns={[
                            { title: "文件名称", dataIndex: "fileName", width: 280, ellipsis: true },
                            { title: "文件类别", dataIndex: "category", width: 120, ellipsis: true },
                            { title: "上传日期", width: 130, render: (_, it) => fmtDate(it.uploadedAt) },
                            { title: "大小", width: 100, render: (_, it) => fmtBytes(it.fileSize ?? 0) },
                        ]}
                        dataSource={items}
                        locale={{ emptyText: "暂无文件" }}
                    />
                )}
                </Dialog>
            )}

            {/* 电脑上传：文件路径 + 新增文件夹 + 拖拽/粘贴上传 + 标记为纸质档案（对照柠檬云「上传新文件」） */}
            {uploadOpen && (
                <Dialog
                    title="上传新文件"
                    width={680}
                    onClose={() => {
                        setUploadOpen(false);
                        setUploadFiles([]);
                    }}
                    footer={
                        <div className="flex justify-end gap-2">
                            <Btn
                                onClick={() => {
                                    setUploadOpen(false);
                                    setUploadFiles([]);
                                }}
                            >
                                取消
                            </Btn>
                            <Btn variant="primary" disabled={busy} onClick={() => void submitUpload()}>
                                确定
                            </Btn>
                        </div>
                    }
                >
                <div className="mb-3 flex items-center gap-2">
                    <span className="text-[13px] text-(--foreground)">文件路径：</span>
                    <Select
                        className="flex-1"
                        value={uploadFolder}
                        onChange={setUploadFolder}
                        options={
                            uploadPathOptions.length > 0
                                ? uploadPathOptions
                                : [{ value: uploadFolder, label: uploadFolder }]
                        }
                    />
                    <Button
                        type="primary"
                        onClick={() =>
                            openAddCategory(
                                categoryFlat.find((n) => n.path === uploadFolder) ??
                                    categoryTree.find((n) => n.name === ALL_FILES) ??
                                    null,
                                true,
                            )
                        }
                    >
                        新增文件夹
                    </Button>
                </div>

                <div
                    className="flex h-[200px] flex-col items-center justify-center rounded-lg border border-dashed border-(--border) bg-(--muted) text-(--muted-foreground)"
                    onDragOver={(e) => e.preventDefault()}
                    onDrop={(e) => {
                        e.preventDefault();
                        addFiles(e.dataTransfer?.files ?? null);
                    }}
                    onPaste={onPaste}
                    tabIndex={0}
                >
                    <FolderOpenOutlined className="mb-2 text-[26px]" />
                    <div className="text-[13px]">将文件拖拽到此处，或按 Ctrl + V 粘贴上传</div>
                    <Button className="mt-2" onClick={() => addRef.current?.click()}>
                        选择文件
                    </Button>
                </div>

                {uploadFiles.length > 0 && (
                    <div className="mt-2 max-h-[150px] overflow-auto rounded border border-(--border)">
                        {uploadFiles.map((f, i) => (
                            <div key={`${f.name}-${i}`} className="flex items-center justify-between border-b border-(--border) px-2 py-1 text-[13px] last:border-b-0">
                                <span className="truncate">{f.name}</span>
                                <span className="ml-3 shrink-0 text-(--muted-foreground)">{fmtBytes(f.size)}</span>
                                <span
                                    className="ml-3 shrink-0 cursor-pointer text-(--brand) hover:underline"
                                    onClick={() => setUploadFiles((prev) => prev.filter((_, k) => k !== i))}
                                >
                                    移除
                                </span>
                            </div>
                        ))}
                    </div>
                )}

                <div className="mt-3 flex items-center justify-between">
                    <span className="text-[12.5px] text-(--muted-foreground)">可以「拖拽文件到此处」或「Ctrl + V 粘贴」进行上传</span>
                    <CheckBox label="标记为纸质档案" checked={uploadPaper} onChange={setUploadPaper} />
                </div>
                </Dialog>
            )}

            {/* 关联凭证：从当前账套凭证里挑一张，写入档案的「凭证」列 */}
            {linkOpen && (
                <Dialog
                    title="关联凭证"
                    width={480}
                    onClose={() => setLinkOpen(false)}
                    footer={
                        <div className="flex justify-end gap-2">
                            <Btn onClick={() => setLinkOpen(false)}>取消</Btn>
                            <Btn variant="primary" disabled={busy} onClick={() => void linkVouchers(linkVoucher)}>
                                确定
                            </Btn>
                        </div>
                    }
                >
                    <div className="mb-2 text-[13px] text-(--muted-foreground)">选择要关联到所选档案的凭证。</div>
                    <Select
                        className="w-full"
                        value={linkVoucher}
                        onChange={setLinkVoucher}
                        options={[
                            { value: "", label: "请选择凭证" },
                            ...linkChoices.map((c) => ({ value: c.text, label: c.text })),
                        ]}
                    />
                    {linkChoices.length === 0 && <div className="mt-2 text-[12.5px] text-(--muted-foreground)">当前账套还没有凭证。</div>}
                </Dialog>
            )}
            {genOpen && (
                <Dialog
                    title={`由档案生成凭证（已选 ${selected.size} 份，附件数=档案数）`}
                    width={520}
                    onClose={() => setGenOpen(false)}
                    footer={
                        <div className="flex justify-end gap-2">
                            <Btn onClick={() => setGenOpen(false)}>取消</Btn>
                            <Btn variant="primary" disabled={genSaving} onClick={() => void saveGen()}>
                                生成凭证
                            </Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-(--foreground)">
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">凭证日期：</span>
                            <DatePicker
                                className={FORM_CONTROL_W}
                                allowClear={false}
                                value={genForm.date ? dayjs(genForm.date) : null}
                                onChange={(_, text) => setGenForm({ ...genForm, date: (text as string) || genForm.date })}
                            />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">摘要：</span>
                            <TextInput
                                className="flex-1"
                                placeholder="留空取档案事项 / 文件名"
                                value={genForm.summary}
                                onChange={(v) => setGenForm({ ...genForm, summary: v })}
                            />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">金额：</span>
                            <TextInput
                                className={FORM_CONTROL_W}
                                type="number"
                                value={genForm.amount}
                                onChange={(v) => setGenForm({ ...genForm, amount: v })}
                            />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">借方科目：</span>
                            <Select
                                className="flex-1"
                                value={genForm.debitAccountId}
                                onChange={(v) => setGenForm({ ...genForm, debitAccountId: v })}
                                options={[
                                    { value: "", label: "请选择科目" },
                                    ...leafAccounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
                                ]}
                            />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">贷方科目：</span>
                            <Select
                                className="flex-1"
                                value={genForm.creditAccountId}
                                onChange={(v) => setGenForm({ ...genForm, creditAccountId: v })}
                                options={[
                                    { value: "", label: "请选择科目" },
                                    ...leafAccounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
                                ]}
                            />
                        </div>
                        <div className="text-[12px] text-(--muted-foreground)">生成后凭证字号会自动回写到所选档案的「关联凭证」列。</div>
                    </div>
                </Dialog>
            )}

            {/* 分类树节点：在任意节点下新增子分类 / 重命名自建分类 */}
            {catDialog && (
                <Dialog
                    title={catDialog.mode === "add" ? "新增分类" : "重命名分类"}
                    onClose={() => setCatDialog(null)}
                    footer={
                        <div className="flex justify-end gap-2">
                            <Btn onClick={() => setCatDialog(null)}>取消</Btn>
                            <Btn variant="primary" disabled={busy} onClick={() => void saveCategory()}>
                                确定
                            </Btn>
                        </div>
                    }
                >
                    <div className="flex flex-col gap-2 py-1 text-[13px] text-(--foreground)">
                        {catDialog.mode === "add" && (
                            <div>
                                上级目录：<span className="text-(--foreground)">{catDialog.node?.path ?? "根目录"}</span>
                            </div>
                        )}
                        <div>请输入分类名称</div>
                        <TextInput value={catName} onChange={setCatName} placeholder="如：2026年 / 银行回单" />
                        <div className="text-[12px] text-(--muted-foreground)">
                            分类可建多层；名称不能与同级目录重复，也不能包含路径分隔符。
                        </div>
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
