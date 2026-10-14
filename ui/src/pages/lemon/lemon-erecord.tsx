/**
 * 会计电子档案（对应柠檬云 /ERecord/ERecord，已接真实后端 ArchivesApi）：
 * 档案列表（电脑上传 / 审核 / 归档 / 删除 / 下载 / 搜索 / 按目录类别过滤）+ 归档页签 + 右侧空间面板。
 * 收票宝上传跳转收票宝页；关联凭证/生成凭证/标记纸质档案依赖未建模型，保持原样。
 */
import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { Archive, ChevronDown, Crown, FileText, Folder, FolderOpen, Image } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { StatusPill } from "@/components/page/status-pill";
import type { PillTone } from "@/components/page/styles";


import {
    Btn,
    CheckBox,
    Page,
    PeriodBlock,
    RefreshIcon,
    SubTabs,
    TextInput,
    Toast,
    ToolRow,
    WipBtn,
} from "./components/ui";
import { ArchivesApi, type ArchiveItemDto } from "@/apis-gen";

/** 前端上传大小上限（8 MB）：整文件 Base64 会再膨胀约 1/3，后端单文件/额度均为 10 MB，留出余量。 */
const MAX_UPLOAD_BYTES = 8 * 1024 * 1024;

/** 字节数格式化。 */
function fmtBytes(n: number): string {
    if (n < 1024) {
        return `${n} B`;
    }
    if (n < 1024 * 1024) {
        return `${(n / 1024).toFixed(0)} KB`;
    }
    return `${(n / 1024 / 1024).toFixed(2)} MB`;
}

function th2() {
    return "border-b border-slate-200 bg-muted px-3 py-2.5 text-left text-[13px] font-normal text-slate-500";
}

const td2 = "border-b border-slate-100 px-3 py-2.5 text-[13px] text-slate-600 tabular-nums";

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
    const [toast, setToast] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const fileRef = useRef<HTMLInputElement>(null);

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

    const toggle = (id: string) =>
        setSelected((prev) => {
            const next = new Set([...prev].filter((k) => k !== id));
            if (!prev.has(id)) {
                next.add(id);
            }
            return next;
        });

    /** 上传：本地文件转 base64 交给后端存档。 */
    const upload = async (file: File) => {
        if (!token || !accountSetId || !file) {
            return;
        }
        if (busy) {
            return;
        }
        if (file.size > MAX_UPLOAD_BYTES) {
            setToast(`「${file.name}」超过 8 MB，无法上传，请压缩后重试。`);
            if (fileRef.current) {
                fileRef.current.value = "";
            }
            return;
        }
        setBusy(true);
        try {
            const dataUrl = await new Promise<string>((resolve, reject) => {
                const reader = new FileReader();
                reader.onload = () => resolve(String(reader.result ?? ""));
                reader.onerror = () => reject(new Error("读取文件失败"));
                reader.readAsDataURL(file);
            });
            await new ArchivesApi(apiConfig(token)).accountsetsAccountSetIdArchivesPost({
                accountSetId,
                saveArchiveRequest: {
                    fileName: file.name,
                    category: folder ?? "凭证附件",
                    docType: folder ?? "其他",
                    fileBase64: dataUrl.includes(",") ? dataUrl.split(",")[1] : dataUrl,
                    uploader: user?.displayName || user?.username || "",
                    source: "电脑上传",
                },
            });
            setToast(`已上传「${file.name}」`);
            await load();
        } catch (e) {
            setToast(await apiErrorMessage(e, "上传失败"));
        } finally {
            setBusy(false);
            if (fileRef.current) {
                fileRef.current.value = "";
            }
        }
    };

    /** 批量动作：审核（通过）/ 归档 / 删除。 */
    const bulk = async (action: "audit" | "archive" | "remove") => {
        if (!token || !accountSetId || busy) {
            return;
        }
        if (selected.size === 0) {
            setToast(action === "archive" ? "请先勾选要归档的档案" : "请先勾选档案");
            return;
        }
        const ids = [...selected];
        setBusy(true);
        try {
            const api = new ArchivesApi(apiConfig(token));
            if (action === "audit") {
                const n = await api.accountsetsAccountSetIdArchivesAuditPost({
                    accountSetId,
                    auditArchiveRequest: { ids, audited: true },
                });
                setToast(`已审核 ${n} 份`);
            } else if (action === "archive") {
                const n = await api.accountsetsAccountSetIdArchivesArchivePost({
                    accountSetId,
                    archiveIdsRequest: { ids },
                });
                setToast(`已归档 ${n} 份`);
            } else {
                const results = await Promise.allSettled(
                    ids.map((id) => api.accountsetsAccountSetIdArchivesIdDelete({ accountSetId, id })),
                );
                const ok = results.filter((r) => r.status === "fulfilled").length;
                setToast(`已删除 ${ok} 份`);
            }

            await load();
        } catch (e) {
            setToast(await apiErrorMessage(e, "操作失败"));
        } finally {
            setBusy(false);
        }
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

    const kw = keyword.trim().toLowerCase();
    const filtered = (tab === 1 ? archived : items).filter(
        (it) =>
            (!folder || (it.category ?? "") === folder) &&
            (!kw || (it.fileName ?? "").toLowerCase().includes(kw)),
    );

    const tool = tab === 0 && (
        <ToolRow
            left={
                <>
                    <PeriodBlock range={["", ""]} text="全部期间" />
                    <Btn variant="primary" onClick={() => navigate(`/${accountSetId}/vouchers/spb`)}>
                        收票宝上传
                    </Btn>
                    <Btn variant="primary" disabled={busy} onClick={() => fileRef.current?.click()}>
                        电脑上传
                    </Btn>
                    <input ref={fileRef} type="file" className="hidden" onChange={(e) => void upload(e.target.files?.[0] as File)} />
                    <Btn dropdown disabled={busy} onClick={() => void bulk("audit")}>
                        审核
                    </Btn>
                    <Btn dropdown disabled={busy} onClick={() => void bulk("archive")}>
                        归档
                    </Btn>
                    <WipBtn dropdown title="关联凭证尚未接入后端">关联凭证</WipBtn>
                    <WipBtn dropdown title="由档案生成凭证尚未接入后端">生成凭证</WipBtn>
                    <WipBtn dropdown title="标记为纸质档案尚未接入后端">标记为纸质档案</WipBtn>
                    <WipBtn dropdown title="更多操作尚未接入后端">更多</WipBtn>
                    <CheckBox label="文件名增加凭证号" />
                </>
            }
            right={
                <>
                    <TextInput
                        className="w-[220px]"
                        placeholder="搜索文件名"
                        value={keyword}
                        onChange={setKeyword}
                    />
                    <span className="cursor-pointer" onClick={() => void load()} title="刷新">
                        <RefreshIcon />
                    </span>
                </>
            }
        />
    );

    const toolArchived = tab === 1 && (
        <ToolRow
            left={
                <>
                    <Btn variant="primary" onClick={() => setTab(0)}>
                        去上传档案
                    </Btn>
                    <Btn onClick={() => void bulk("remove")}>删除</Btn>
                </>
            }
            right={
                <span className="cursor-pointer" onClick={() => void load()} title="刷新">
                    <RefreshIcon />
                </span>
            }
        />
    );

    return (
        <Page title="会计电子档案" subtitle="凭证附件、回单、发票等资料的电子化归档管理" icon={Archive}>
            <SubTabs tabs={["电子档案列表", "电子档案归档"]} active={tab} onChange={setTab} />
            {tool}
            {toolArchived}
            <div className="flex min-h-0 flex-1">
                <div className="min-w-0 flex-1 px-4">
                    {tab === 1 ? (
                        <table className="w-full border-collapse text-sm">
                            <thead>
                                <tr className="bg-muted text-[13px] text-slate-500">
                                    <th className={`${th2()} w-[36px]`}> </th>
                                    {["档案名称", "文件类别", "上传人", "上传日期", "操作"].map((t) => (
                                        <th key={t} className={th2()}>
                                            {t}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {filtered.map((it, i) => (
                                    <tr key={it.id} className={`transition-colors hover:bg-[#f6f9ff] ${i % 2 === 1 ? "bg-[#fafbfd]" : "bg-white"}`}>
                                        <td className={td2}>
                                            <span className="inline-flex cursor-pointer" onClick={() => toggle(it.id ?? "")}>
                                                <CheckBox label="" checked={selected.has(it.id ?? "")} />
                                            </span>
                                        </td>
                                        <td className={td2}>{it.fileName}</td>
                                        <td className={td2}>{it.category}</td>
                                        <td className={td2}>{it.uploader}</td>
                                        <td className={td2}>{it.uploadedAt ? new Date(it.uploadedAt).toISOString().substring(0, 10) : ""}</td>
                                        <td className={td2}>
                                            <span className="flex gap-2 text-[#2f6bff]">
                                                <span className="cursor-pointer hover:underline" onClick={() => void download(it)}>
                                                    下载
                                                </span>
                                                <span className="cursor-pointer hover:underline" onClick={() => void bulk("remove")}>
                                                    删除
                                                </span>
                                            </span>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    ) : (
                        <table className="w-full border-collapse text-sm">
                            <thead>
                                <tr>
                                    <th className={`${th2()} w-[36px]`}>
                                        <span
                                            className="inline-flex cursor-pointer"
                                            onClick={() =>
                                                setSelected((prev) =>
                                                    filtered.length > 0 && filtered.every((it) => prev.has(it.id ?? ""))
                                                        ? new Set()
                                                        : new Set(filtered.map((it) => it.id ?? "")),
                                                )
                                            }
                                        >
                                            <CheckBox
                                                label=""
                                                checked={filtered.length > 0 && filtered.every((it) => selected.has(it.id ?? ""))}
                                            />
                                        </span>
                                    </th>
                                    {["文件名称", "文件类别", "单据类型", "上传日期", "关联单据", "关联单号", "凭证", "事项", "校验状态", "文件状态", "上传人", "上传方式"].map((t) => (
                                        <th key={t} className={th2()}>
                                            {t}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {filtered.map((it, i) => (
                                    <tr key={it.id} className={`transition-colors hover:bg-[#f6f9ff] ${i % 2 === 1 ? "bg-[#fafbfd]" : "bg-white"}`}>
                                        <td className={td2}>
                                            <span className="inline-flex cursor-pointer" onClick={() => toggle(it.id ?? "")}>
                                                <CheckBox label="" checked={selected.has(it.id ?? "")} />
                                            </span>
                                        </td>
                                        <td className={td2}>
                                            <span className="inline-flex cursor-pointer items-center gap-1.5" onClick={() => void download(it)} title="点击下载">
                                                {(it.fileName ?? "").endsWith(".pdf") ? (
                                                    <FileText className="h-3.5 w-3.5 text-[#ef4444]" />
                                                ) : (
                                                    <Image className="h-3.5 w-3.5 text-[#10b981]" />
                                                )}
                                                <span className="text-[#2f6bff] hover:underline">{it.fileName}</span>
                                            </span>
                                        </td>
                                        <td className={td2}>{it.category}</td>
                                        <td className={td2}>{it.docType}</td>
                                        <td className={td2}>{it.uploadedAt ? new Date(it.uploadedAt).toISOString().substring(0, 10) : ""}</td>
                                        <td className={td2}>{it.relatedDoc}</td>
                                        <td className={`${td2} text-[#2f6bff]`}>{it.relatedNo}</td>
                                        <td className={`${td2} text-[#2f6bff]`}>{it.voucherText}</td>
                                        <td className={td2}>{it.matter}</td>
                                        <td className={td2}>
                                            <StatusCell text={it.checkStatus} />
                                        </td>
                                        <td className={td2}>
                                            <StatusCell text={it.statusText} />
                                        </td>
                                        <td className={td2}>{it.uploader}</td>
                                        <td className={td2}>{it.source}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                    {filtered.length === 0 && (
                        <div className="py-14 text-center text-[13px] text-slate-400">
                            {tab === 1
                                ? "还没有归档数据，在列表页勾选档案后点「归档」即可移入此处哦~"
                                : "暂无档案，点击「电脑上传」把凭证附件、回单、发票等文件存入电子档案。"}
                        </div>
                    )}
                    {filtered.length === 0 ? null : (
                        <div className="flex items-center px-1 py-3 text-[13px] text-slate-500">共 {filtered.length} 条记录</div>
                    )}
                </div>
                {/* 右侧文件目录面板 */}
                <div className="w-[270px] shrink-0 border-l border-slate-200 px-3 py-3">
                    <div className="text-[13px] text-slate-700">
                        已用空间( <span className="text-[#2f6bff]">{usage ? fmtBytes(usage.used) : "…"}</span> /{" "}
                        {usage ? fmtBytes(usage.total) : "10.00 MB"} )
                    </div>
                    <div className="mt-2 inline-flex h-8 cursor-pointer items-center gap-1 rounded-lg border border-[#fde68a] bg-[#fffbeb] px-3 text-[13px] text-[#92640b]">
                        <Crown className="h-3.5 w-3.5" /> 升级扩容
                    </div>
                    <div className="mt-4 flex flex-col text-[13px]">
                        <div
                            className={`flex cursor-pointer items-center justify-between rounded-lg px-2 py-1.5 text-slate-700 ${
                                folder === null ? "bg-[#eef4ff]" : "hover:bg-slate-100"
                            }`}
                            onClick={() => setFolder(null)}
                        >
                            <span className="flex items-center gap-1.5">
                                <ChevronDown className="h-3 w-3 text-[#2f6bff]" />
                                <Folder className="h-3.5 w-3.5 text-[#f59e0b]" /> 所有文件
                            </span>
                            <span className="text-slate-400">⋮</span>
                        </div>
                        {FOLDERS.map((f) => (
                            <div
                                key={f}
                                className={`flex cursor-pointer items-center gap-1.5 rounded-lg px-2 py-1.5 pl-7 text-slate-700 ${
                                    folder === f ? "bg-[#eef4ff]" : "hover:bg-slate-100"
                                }`}
                                onClick={() => setFolder(f)}
                            >
                                <FolderOpen className="h-3.5 w-3.5 text-[#f59e0b]" /> {f}
                            </div>
                        ))}
                        <div className="mt-2 flex cursor-pointer items-center gap-1.5 rounded-lg px-2 py-1.5 text-slate-700 hover:bg-slate-100">
                            <Folder className="h-3.5 w-3.5 text-[#f59e0b]" /> 数据备份
                        </div>
                    </div>
                </div>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
