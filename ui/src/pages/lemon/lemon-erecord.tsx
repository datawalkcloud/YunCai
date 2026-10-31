/**
 * 会计电子档案（对应柠檬云 /ERecord/ERecord，已接真实后端 ArchivesApi）：
 * 档案列表（电脑上传 / 审核 / 归档 / 删除 / 下载 / 搜索 / 按目录类别过滤）+ 归档页签 + 右侧空间面板。
 * 收票宝上传跳转收票宝页；关联凭证/生成凭证/标记纸质档案依赖未建模型，保持原样。
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router";
import dayjs from "dayjs";
import { Button, DatePicker, Dropdown, Modal } from "antd";
import {
    CrownOutlined,
    FileTextOutlined,
    FolderOpenOutlined,
    FolderOutlined,
    HddOutlined,
    LeftOutlined,
    PictureOutlined,
    RightOutlined,
} from "@ant-design/icons";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { StatusPill } from "@/components/page/status-pill";
import { ICON_STYLE, type PillTone } from "@/components/page/styles";

import {
    Btn,
    CheckBox,
    Dialog,
    EmptyTip,
    HintBox,
    Page,
    RefreshIcon,
    Select,
    SubTabs,
    TextInput,
    Toast,
    ToolRow,
} from "./components/ui";
import { AccountsApi, ArchivesApi, VouchersApi, type AccountDto, type ArchiveItemDto } from "@/apis-gen";

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

function th2() {
    return "border-b border-slate-200 bg-[#f8fafc] px-3 py-2.5 text-left text-[13px] font-normal text-slate-500 whitespace-nowrap";
}

const td2 = "border-b border-slate-100 px-3 py-2.5 text-[13px] text-slate-600 tabular-nums whitespace-nowrap";

const FOLDERS = ["凭证附件", "回单", "发票", "现金流水", "票据", "工资", "资产"];

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

type ListColumn = {
    key: string;
    title: string;
    width?: number;
    render: (it: ArchiveItemDto) => React.ReactNode;
};

export default function LemonERecord() {
    const navigate = useNavigate();
    const { token, user } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const [tab, setTab] = useState(0);
    const [items, setItems] = useState<ArchiveItemDto[]>([]);
    const [archived, setArchived] = useState<ArchiveItemDto[]>([]);
    const [usage, setUsage] = useState<{ used: number; total: number } | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [keyword, setKeyword] = useState("");
    const [folder, setFolder] = useState<string | null>(null);
    const [range, setRange] = useState<[string, string] | null>(defaultRange);
    const [withVoucherNo, setWithVoucherNo] = useState(false);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [pickerOpen, setPickerOpen] = useState(false);
    const [pickIds, setPickIds] = useState<Set<string>>(new Set());
    /** 电脑上传弹窗：文件路径（归档目录）、待上传文件、是否标记为纸质档案。 */
    const [uploadOpen, setUploadOpen] = useState(false);
    const [uploadFolder, setUploadFolder] = useState("凭证附件");
    const [uploadFiles, setUploadFiles] = useState<File[]>([]);
    const [uploadPaper, setUploadPaper] = useState(false);
    /** 用户在「新增文件」里自建的目录（按账套存本机）。 */
    const [extraFolders, setExtraFolders] = useState<string[]>([]);
    /** 「新增文件夹」弹窗（替代 window.prompt）：目录名受控输入。 */
    const [folderOpen, setFolderOpen] = useState(false);
    const [folderName, setFolderName] = useState("");
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

    useEffect(() => {
        void load();
    }, [load]);

    /** 自建目录按账套存放在本机浏览器。 */
    useEffect(() => {
        if (!accountSetId) {
            setExtraFolders([]);
            return;
        }
        try {
            const raw = window.localStorage.getItem("kuaiji.erecord.folders." + accountSetId);
            setExtraFolders(raw ? (JSON.parse(raw) as string[]) : []);
        } catch {
            setExtraFolders([]);
        }
    }, [accountSetId]);

    useEffect(() => {
        setPage(1);
    }, [tab, folder, keyword, range, pageSize]);

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

    const toggle = (id: string) =>
        setSelected((prev) => {
            const next = new Set([...prev].filter((k) => k !== id));
            if (!prev.has(id)) {
                next.add(id);
            }
            return next;
        });

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
                        docType: category,
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

    /** 打开「新增文件夹」弹窗（受控输入，替代 window.prompt）。 */
    const openFolderDialog = () => {
        setFolderName("");
        setFolderOpen(true);
    };

    /** 新增文件：自建一个归档目录（确定后创建，逻辑与原 window.prompt 版一致）。 */
    const addFolder = () => {
        const trimmed = folderName.trim();
        if (!trimmed) {
            return;
        }
        if (folderOptions.includes(trimmed)) {
            setToast("该文件夹已存在");
            return;
        }
        setExtraFolders((prev) => {
            const next = [...prev, trimmed];
            try {
                window.localStorage.setItem("kuaiji.erecord.folders." + accountSetId, JSON.stringify(next));
            } catch {
                /* 本机存储不可用时仅保留内存态 */
            }
            return next;
        });
        setUploadFolder(trimmed);
        setFolderOpen(false);
    };

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
        Modal.confirm({
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

    const togglePick = (id: string) =>
        setPickIds((prev) => {
            const next = new Set(prev);
            if (next.has(id)) {
                next.delete(id);
            } else {
                next.add(id);
            }
            return next;
        });

    const allPicked = items.length > 0 && items.every((it) => pickIds.has(it.id ?? ""));

    const togglePickAll = () =>
        setPickIds(allPicked ? new Set() : new Set(items.map((it) => it.id ?? "")));

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

    const changeFolder = (f: string | null) => {
        setFolder(f);
        setSelected(new Set());
    };

    const kw = keyword.trim().toLowerCase();
    const filtered =
        tab === 1
            ? [...archived].sort(
                  (a, b) => timeOf(b.archivedAt ?? b.uploadedAt) - timeOf(a.archivedAt ?? a.uploadedAt),
              )
            : items.filter((it) => {
                  const d = fmtDate(it.uploadedAt);
                  return (
                      (!folder || (it.category ?? "") === folder) &&
                      (!range || (d >= range[0] && d <= range[1])) &&
                      (!kw ||
                          (it.fileName ?? "").toLowerCase().includes(kw) ||
                          (it.relatedNo ?? "").toLowerCase().includes(kw) ||
                          (it.voucherText ?? "").toLowerCase().includes(kw))
                  );
              });
    const total = filtered.length;
    const pageCount = Math.max(1, Math.ceil(total / pageSize));
    const current = Math.min(page, pageCount);
    const paged = filtered.slice((current - 1) * pageSize, current * pageSize);
    const from = total === 0 ? 0 : (current - 1) * pageSize + 1;
    const to = Math.min(current * pageSize, total);
    const allChecked = paged.length > 0 && paged.every((it) => selected.has(it.id ?? ""));

    const toggleAll = () =>
        setSelected((prev) => {
            if (allChecked) {
                return new Set([...prev].filter((id) => !paged.some((it) => it.id === id)));
            }
            return new Set([...prev, ...paged.map((it) => it.id ?? "")]);
        });

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

    const rangePicker = (
        <DatePicker.RangePicker
            style={{ width: 250 }}
            allowClear
            value={range ? [dayjs(range[0]), dayjs(range[1])] : null}
            onChange={(_, text) => setRange(text[0] && text[1] ? [text[0], text[1]] : null)}
        />
    );

    /** 归档目录：内置分类 + 用户自建文件夹。 */
    const folderOptions = useMemo(() => [...FOLDERS, ...extraFolders], [extraFolders]);

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
                    {rangePicker}
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
                    <PictureOutlined className="text-[14px] shrink-0" style={{ color: "#10b981" }} />
                )}
                <span className="truncate text-[#2f6bff] hover:underline">{name}</span>
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
        { key: "category", title: "文件类别", width: 96, render: (it) => it.category },
        { key: "docType", title: "单据类型", width: 96, render: (it) => it.docType },
        { key: "uploadedAt", title: "上传日期", width: 104, render: (it) => fmtDate(it.uploadedAt) },
        { key: "relatedDoc", title: "关联单据", width: 110, render: (it) => it.relatedDoc },
        {
            key: "relatedNo",
            title: "关联单据号",
            width: 150,
            render: (it) => <span className="text-[#2f6bff]">{it.relatedNo}</span>,
        },
        {
            key: "voucherText",
            title: "凭证",
            width: 130,
            render: (it) => <span className="text-[#2f6bff]">{it.voucherText}</span>,
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
            render: (it) => (it.category ? `所有文件>${it.category}` : "所有文件"),
        },
    ];

    const archivedColumns: ListColumn[] = [
        {
            key: "code",
            title: "档案编码",
            width: 200,
            render: (it) => <span className="text-slate-700">{archiveCode(it)}</span>,
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
                <span className="flex gap-2 text-[#2f6bff]">
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
        <Page title="会计电子档案" subtitle="凭证附件、回单、发票等资料的电子化归档管理" icon={HddOutlined}>
            <SubTabs tabs={["电子档案列表", "电子档案归档"]} active={tab} onChange={changeTab} />
            {tab === 0 ? toolList : toolArchived}
            <div className="flex min-h-0 flex-1">
                <div className="flex min-w-0 flex-1 flex-col px-4">
                    <div className="overflow-x-auto">
                        <table
                            className={`w-full border-collapse text-[13px] ${tab === 1 ? "min-w-[1000px]" : "min-w-[1560px]"}`}
                        >
                            <thead>
                                <tr>
                                    <th className={`${th2()} w-[36px]`}>
                                        <span className="inline-flex cursor-pointer" onClick={toggleAll}>
                                            <CheckBox label="" checked={allChecked} />
                                        </span>
                                    </th>
                                    {columns.map((c) => (
                                        <th key={c.key} className={th2()} style={{ width: c.width }}>
                                            {c.title}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {paged.map((it, i) => (
                                    <tr
                                        key={it.id}
                                        className={`transition-colors hover:bg-[#f6f9ff] ${i % 2 === 1 ? "bg-[#fafbfd]" : "bg-white"}`}
                                    >
                                        <td className={td2}>
                                            <span className="inline-flex cursor-pointer" onClick={() => toggle(it.id ?? "")}>
                                                <CheckBox label="" checked={selected.has(it.id ?? "")} />
                                            </span>
                                        </td>
                                        {columns.map((c) => (
                                            <td key={c.key} className={td2}>
                                                {c.render(it)}
                                            </td>
                                        ))}
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                    {total === 0 && (
                        <div className="py-10 text-center">
                            {tab === 1 ? (
                                <span className="text-[13px] text-slate-400">
                                    您还没有归档数据，请点击{" "}
                                    <span className="cursor-pointer text-[#2f6bff] hover:underline" onClick={openPicker}>
                                        “归档”
                                    </span>{" "}
                                    按钮进行归档操作哦~
                                </span>
                            ) : (
                                <EmptyTip text="暂无档案，点击「电脑上传」把凭证附件、回单、发票等文件存入电子档案。" />
                            )}
                        </div>
                    )}
                    {(tab === 0 || total > 0) && (
                        <div className="mt-auto flex flex-wrap items-center justify-between gap-2 border-t border-slate-100 px-1 py-3 text-[12.5px] text-slate-500">
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
                                    size="small"
                                    icon={<LeftOutlined />}
                                    disabled={current <= 1}
                                    onClick={() => setPage(current - 1)}
                                />
                                <span>
                                    第 <span className="font-medium text-[#2f6bff]">{current}</span> / 共 {pageCount} 页
                                </span>
                                <Button
                                    type="text"
                                    size="small"
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
                    <div className="w-[270px] shrink-0 border-l border-slate-200 px-3 py-3">
                        <div className="text-[13px] text-slate-700">
                            已用空间( <span className="text-[#2f6bff]">{usage ? fmtBytes(usage.used) : "…"}</span> /{" "}
                            {usage ? fmtBytes(usage.total) : "10.00 MB"} )
                        </div>
                        <HintBox className="mt-2 inline-flex cursor-pointer items-center gap-1">
                            <CrownOutlined className="text-[14px]" /> 升级扩容
                        </HintBox>
                        <div className="mt-4 flex flex-col text-[13px]">
                            <div
                                className={`flex cursor-pointer items-center justify-between rounded-lg px-2 py-1.5 text-slate-700 ${
                                    folder === null ? "bg-[#eef4ff]" : "hover:bg-slate-100"
                                }`}
                                onClick={() => changeFolder(null)}
                            >
                                <span className="flex items-center gap-1.5">
                                    <FolderOutlined className="text-[14px]" style={ICON_STYLE.warn} /> 所有文件
                                </span>
                                <span className="text-slate-400">⋮</span>
                            </div>
                            {FOLDERS.map((f) => (
                                <div
                                    key={f}
                                    className={`flex cursor-pointer items-center gap-1.5 rounded-lg px-2 py-1.5 pl-7 text-slate-700 ${
                                        folder === f ? "bg-[#eef4ff]" : "hover:bg-slate-100"
                                    }`}
                                    onClick={() => changeFolder(f)}
                                >
                                    <FolderOpenOutlined className="text-[14px]" style={ICON_STYLE.warn} /> {f}
                                </div>
                            ))}
                            <div className="mt-2 flex cursor-pointer items-center gap-1.5 rounded-lg px-2 py-1.5 text-slate-700 hover:bg-slate-100">
                                <FolderOutlined className="text-[14px]" style={ICON_STYLE.warn} /> 数据备份
                            </div>
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
                <div className="mb-2 text-[13px] text-slate-500">勾选要归档的档案，归档后可在「电子档案归档」中查看。</div>
                {items.length === 0 ? (
                    <div className="py-10 text-center text-[13px] text-slate-400">
                        暂无可归档的档案，请先在「电子档案列表」上传。
                    </div>
                ) : (
                    <div className="max-h-[380px] overflow-auto">
                        <table className="w-full border-collapse text-[13px]">
                            <thead>
                                <tr>
                                    <th className={`${th2()} w-[36px]`}>
                                        <span className="inline-flex cursor-pointer" onClick={togglePickAll}>
                                            <CheckBox label="" checked={allPicked} />
                                        </span>
                                    </th>
                                    {["文件名称", "文件类别", "上传日期", "大小"].map((t) => (
                                        <th key={t} className={th2()}>
                                            {t}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {items.map((it) => (
                                    <tr key={it.id} className="hover:bg-[#f6f9ff]">
                                        <td className={td2}>
                                            <span
                                                className="inline-flex cursor-pointer"
                                                onClick={() => togglePick(it.id ?? "")}
                                            >
                                                <CheckBox label="" checked={pickIds.has(it.id ?? "")} />
                                            </span>
                                        </td>
                                        <td className={td2}>{it.fileName}</td>
                                        <td className={td2}>{it.category}</td>
                                        <td className={td2}>{fmtDate(it.uploadedAt)}</td>
                                        <td className={td2}>{fmtBytes(it.fileSize ?? 0)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
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
                    <span className="text-[13px] text-slate-600">文件路径：</span>
                    <Select
                        className="flex-1"
                        value={uploadFolder}
                        onChange={setUploadFolder}
                        options={folderOptions}
                    />
                    <Button size="small" type="primary" onClick={openFolderDialog}>
                        新增文件
                    </Button>
                </div>

                <div
                    className="flex h-[200px] flex-col items-center justify-center rounded-lg border border-dashed border-slate-300 bg-slate-50 text-slate-400"
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
                    <Button size="small" className="mt-2" onClick={() => addRef.current?.click()}>
                        选择文件
                    </Button>
                </div>

                {uploadFiles.length > 0 && (
                    <div className="mt-2 max-h-[150px] overflow-auto rounded border border-slate-200">
                        {uploadFiles.map((f, i) => (
                            <div key={`${f.name}-${i}`} className="flex items-center justify-between border-b border-slate-100 px-2 py-1 text-[13px] last:border-b-0">
                                <span className="truncate">{f.name}</span>
                                <span className="ml-3 shrink-0 text-slate-400">{fmtBytes(f.size)}</span>
                                <span
                                    className="ml-3 shrink-0 cursor-pointer text-[#2f6bff] hover:underline"
                                    onClick={() => setUploadFiles((prev) => prev.filter((_, k) => k !== i))}
                                >
                                    移除
                                </span>
                            </div>
                        ))}
                    </div>
                )}

                <div className="mt-3 flex items-center justify-between">
                    <span className="text-[12.5px] text-slate-400">可以「拖拽文件到此处」或「Ctrl + V 粘贴」进行上传</span>
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
                    <div className="mb-2 text-[13px] text-slate-500">选择要关联到所选档案的凭证。</div>
                    <Select
                        className="w-full"
                        value={linkVoucher}
                        onChange={setLinkVoucher}
                        options={[
                            { value: "", label: "请选择凭证" },
                            ...linkChoices.map((c) => ({ value: c.text, label: c.text })),
                        ]}
                    />
                    {linkChoices.length === 0 && <div className="mt-2 text-[12.5px] text-slate-400">当前账套还没有凭证。</div>}
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
                    <div className="flex flex-col gap-3 px-1 text-[13px] text-slate-700">
                        <div className="flex items-center gap-3">
                            <span className="w-[80px] text-right">凭证日期：</span>
                            <DatePicker
                                className="w-[180px]"
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
                                className="w-[180px]"
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
                        <div className="text-[12px] text-slate-400">生成后凭证字号会自动回写到所选档案的「关联凭证」列。</div>
                    </div>
                </Dialog>
            )}

            {/* 新增文件夹（替代 window.prompt）：受控输入，确定后创建，逻辑与原实现一致 */}
            {folderOpen && (
                <Dialog
                    title="新增文件夹"
                    onClose={() => setFolderOpen(false)}
                    footer={
                        <div className="flex justify-end gap-2">
                            <Btn onClick={() => setFolderOpen(false)}>取消</Btn>
                            <Btn variant="primary" onClick={addFolder}>
                                确定
                            </Btn>
                        </div>
                    }
                >
                    <div className="py-1 text-[13px] text-slate-600">
                        <div>请输入文件夹名称</div>
                        <div className="mt-2">
                            <TextInput value={folderName} onChange={setFolderName} placeholder="请输入文件夹名称" />
                        </div>
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
