/**
 * 柠檬云凭证模块（已接真实后端 VouchersApi）：
 * - VoucherNew  新增凭证（表格化借贷金额栏 + 凭证头 + 制单人，可保存落库）
 * 查看凭证与既有实页重叠，复刻版已下线（见 router）。
 */
import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router";
import { CalendarDays, Keyboard, Maximize2, Plus, Search, X } from "lucide-react";

import { Btn, CheckBox, FakeSelect, Page, RefreshIcon, Toast } from "./components/ui";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { AccountsApi, VouchersApi, type AccountDto } from "@/apis-gen";

/* ================= 新增凭证 ================= */

/** 金额栏位数（亿千百十万千百十元角分）。 */
const DIGITS = ["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"];

const EDIT_CLS = "h-[44px] w-full bg-transparent px-2 text-[13px] text-[#333] outline-none placeholder:text-[#bbb]";

/** 凭证分录行。 */
type EntryRow = {
    summary: string;
    accountId: string;
    debit: string;
    credit: string;
};

const emptyRows = (): EntryRow[] => [0, 1, 2, 3, 4].map(() => ({ summary: "", accountId: "", debit: "", credit: "" }));

/** 金额文本 → 数值（空串为 0；非法输入按 0 处理）。 */
function toAmount(text: string): number {
    const n = Number(text);
    return Number.isFinite(n) && text.trim() !== "" ? n : 0;
}

/** 小写金额 → 人民币大写（支持到亿，两位小数）。 */
function toChineseAmount(value: number): string {
    const digits = "零壹贰叁肆伍陆柒捌玖";
    const units = ["", "拾", "佰", "仟"];
    const bigUnits = ["", "万", "亿"];
    if (value === 0) {
        return "零元整";
    }
    const negative = value < 0;
    let v = Math.round(Math.abs(value) * 100);
    const jiao = Math.floor(v / 10) % 10;
    const fen = v % 10;
    v = Math.floor(v / 100);
    let yuan = "";
    let section = 0;
    while (v > 0) {
        let part = v % 10000;
        let partText = "";
        let zero = false;
        for (let i = 0; i < 4; i++) {
            const d = part % 10;
            if (d === 0) {
                zero = partText !== "";
            } else {
                partText = digits[d] + units[i] + (zero ? "零" : "") + partText;
                zero = false;
            }
            part = Math.floor(part / 10);
        }
        yuan = partText + (part > 0 || section > 0 ? bigUnits[section] : "") + yuan;
        v = Math.floor(v / 10000);
        section += 1;
    }
    let result = `${negative ? "负" : ""}${yuan || "零"}元`;
    if (jiao === 0 && fen === 0) {
        result += "整";
    } else {
        if (jiao > 0) {
            result += digits[jiao] + "角";
        } else if (fen > 0) {
            result += "零";
        }
        if (fen > 0) {
            result += digits[fen] + "分";
        }
    }
    return result;
}

function todayText(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

export function VoucherNew() {
    const { token, user } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const navigate = useNavigate();

    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    const [nextNumber, setNextNumber] = useState<number | null>(null);
    const [word] = useState("记");
    const [date, setDate] = useState(todayText());
    const [attachmentCount, setAttachmentCount] = useState("");
    const [rows, setRows] = useState<EntryRow[]>(emptyRows);
    const [saving, setSaving] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    // 加载明细科目与下一个凭证号。
    const reloadMeta = useCallback(async () => {
        if (!token || !accountSetId) {
            return;
        }
        try {
            const api = new VouchersApi(apiConfig(token));
            const [leafAccounts, vouchers] = await Promise.all([
                new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true }),
                api.accountsetsAccountSetIdVouchersGet({ accountSetId }),
            ]);
            setAccounts(leafAccounts ?? []);
            const maxNo = (vouchers ?? [])
                .filter((v) => (v.word ?? "") === word)
                .reduce((m, v) => Math.max(m, v.number ?? 0), 0);
            setNextNumber(maxNo + 1);
        } catch (e) {
            setToast(await apiErrorMessage(e, "加载科目失败"));
        }
    }, [token, accountSetId, word]);
    useEffect(() => {
        void reloadMeta();
    }, [reloadMeta]);

    const totals = useMemo(() => {
        const debit = rows.reduce((s, r) => s + toAmount(r.debit), 0);
        const credit = rows.reduce((s, r) => s + toAmount(r.credit), 0);
        return { debit, credit, balanced: debit > 0 && Math.abs(debit - credit) < 0.005 };
    }, [rows]);

    const patchRow = (index: number, patch: Partial<EntryRow>) =>
        setRows((prev) => prev.map((r, i) => (i === index ? { ...r, ...patch } : r)));

    const save = async (keepEditing: boolean) => {
        if (!token || !accountSetId) {
            return;
        }
        const entries = rows
            .filter((r) => r.accountId && (toAmount(r.debit) !== 0 || toAmount(r.credit) !== 0))
            .map((r) => ({
                summary: r.summary || "（无摘要）",
                accountId: r.accountId,
                debit: toAmount(r.debit),
                credit: toAmount(r.credit),
            }));
        if (entries.length === 0) {
            setToast("请先录入分录（选择科目并填写金额）");
            return;
        }
        if (!totals.balanced) {
            setToast("借贷不平衡，不能保存");
            return;
        }
        setSaving(true);
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersPost({
                accountSetId,
                createVoucherRequest: {
                    word,
                    date: new Date(date),
                    attachmentCount: Math.max(0, Number(attachmentCount) || 0),
                    entries,
                },
            });
            setToast("保存成功");
            if (keepEditing) {
                setRows(emptyRows());
                await reloadMeta();
            } else {
                navigate(`/${accountSetId}/vouchers/manage`);
            }
        } catch (e) {
            setToast(await apiErrorMessage(e, "保存失败"));
        } finally {
            setSaving(false);
        }
    };

    const canSave = !saving && totals.balanced;
    return (
        <Page>
            <div className="relative flex min-h-[56px] items-center px-4 py-2">
                <div className="flex flex-1 items-center justify-center gap-3">
                    <Btn variant={canSave ? "primary" : "disabled"} onClick={() => void save(true)}>
                        保存并新增
                    </Btn>
                    <Btn variant={canSave ? "primary" : "disabled"} onClick={() => void save(false)}>
                        保存
                    </Btn>
                    <Btn dropdown onClick={() => window.print()}>
                        打印
                    </Btn>
                    <Btn dropdown>更多</Btn>
                </div>
                <div className="absolute right-4 flex items-center gap-4 text-sm text-[#333]">
                    <span className="flex items-center gap-1">
                        <Keyboard className="h-4 w-4 text-[#666]" /> 快捷键
                    </span>
                    <span className="flex items-center gap-1 text-[#999]">
                        上一页 <X className="h-3 w-3" /> <Plus className="h-3 w-3" /> 下一页
                    </span>
                </div>
            </div>
            {/* 凭证头 */}
            <div className="mx-auto flex w-[1080px] flex-wrap items-center gap-2 py-3">
                <FakeSelect text={word} className="w-[88px]" />
                <span className="inline-flex h-8 w-[64px] items-center justify-center rounded-[2px] border border-[#d9d9d9] text-sm">
                    {nextNumber ?? "-"}
                </span>
                <span className="text-sm text-[#333]">号</span>
                <span className="text-sm text-[#333]">日期：</span>
                <span className="inline-flex h-8 items-center gap-2 rounded-[2px] border border-[#d9d9d9] px-2.5 text-sm">
                    <input
                        type="date"
                        value={date}
                        onChange={(e) => setDate(e.target.value)}
                        className="w-[120px] bg-transparent text-sm text-[#333] outline-none"
                    />
                    <CalendarDays className="h-3.5 w-3.5 text-[#999]" />
                </span>
                <span className="ml-3 text-[13px] text-[#ff9f2e]">亲，按回车键，能快速选择单元格哟！</span>
                <span className="ml-auto flex items-center gap-2 text-sm text-[#333]">
                    附单据
                    <input
                        className="inline-flex h-8 w-[64px] items-center rounded-[2px] border border-[#d9d9d9] px-2.5 text-sm outline-none"
                        value={attachmentCount}
                        onChange={(e) => setAttachmentCount(e.target.value.replace(/\D/g, ""))}
                    />
                    张
                    <span className="cursor-pointer text-[#3385ff]">附件</span>
                    <span className="cursor-pointer text-[#3385ff]">备注 ⓘ</span>
                    <span className="flex items-center gap-1">
                        <Search className="h-4 w-4 text-[#666]" /> 放大
                    </span>
                    <span className="flex items-center gap-1">
                        <Maximize2 className="h-4 w-4 text-[#666]" /> 全屏
                    </span>
                </span>
            </div>
            {/* 分录网格 */}
            <div className="mx-auto w-[1080px]">
                <table className="w-full border-collapse">
                    <thead>
                        <tr className="bg-white text-[13px] text-[#333]">
                            <th rowSpan={2} className="w-[46px] border border-[#8c8c8c] px-1 py-1.5 font-normal">
                                序号
                            </th>
                            <th rowSpan={2} className="w-[240px] border border-[#8c8c8c] px-2 py-1.5 font-normal">
                                摘要
                            </th>
                            <th rowSpan={2} className="border border-[#8c8c8c] px-2 py-1.5 font-normal">
                                会计科目
                            </th>
                            <th colSpan={11} className="border border-[#8c8c8c] px-2 py-1 font-normal">
                                借方金额
                            </th>
                            <th colSpan={11} className="border border-[#8c8c8c] px-2 py-1 font-normal">
                                贷方金额
                            </th>
                        </tr>
                        <tr className="bg-white text-[10px] text-[#666]">
                            {DIGITS.map((d, i) => (
                                <th
                                    key={`j${i}`}
                                    className="w-[20px] border border-[#8c8c8c] py-0.5 font-normal"
                                    style={{ borderLeftColor: i === 8 ? "#f0b8b8" : undefined }}
                                >
                                    {d}
                                </th>
                            ))}
                            {DIGITS.map((d, i) => (
                                <th
                                    key={`d${i}`}
                                    className="w-[20px] border border-[#8c8c8c] py-0.5 font-normal"
                                    style={{ borderLeftColor: i === 8 ? "#f0b8b8" : undefined }}
                                >
                                    {d}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map((r, idx) => (
                            <tr key={idx} className="h-[52px]">
                                <td className="border border-[#8c8c8c] text-center text-[13px] text-[#333]">{idx + 1}</td>
                                <td className="border border-[#8c8c8c] p-0">
                                    <input
                                        className={EDIT_CLS}
                                        value={r.summary}
                                        onChange={(e) => patchRow(idx, { summary: e.target.value })}
                                        placeholder="摘要"
                                    />
                                </td>
                                <td className={`border border-[#8c8c8c] p-0 ${idx === 0 ? "bg-[#e8f7e8]" : ""}`}>
                                    <select className={EDIT_CLS} value={r.accountId} onChange={(e) => patchRow(idx, { accountId: e.target.value })}>
                                        <option value="">选择科目</option>
                                        {(accounts ?? []).map((a) => (
                                            <option key={a.id} value={a.id}>
                                                {`${a.code ?? ""} ${a.name ?? ""}`}
                                            </option>
                                        ))}
                                    </select>
                                </td>
                                <td colSpan={11} className="border border-[#8c8c8c] p-0">
                                    <input
                                        className={`${EDIT_CLS} text-right`}
                                        value={r.debit}
                                        onChange={(e) => patchRow(idx, { debit: e.target.value.replace(/[^\d.]/g, ""), credit: "" })}
                                        placeholder="0.00"
                                    />
                                </td>
                                <td colSpan={11} className="border border-[#8c8c8c] p-0">
                                    <input
                                        className={`${EDIT_CLS} text-right`}
                                        value={r.credit}
                                        onChange={(e) => patchRow(idx, { credit: e.target.value.replace(/[^\d.]/g, ""), debit: "" })}
                                        placeholder="0.00"
                                    />
                                </td>
                            </tr>
                        ))}
                        {/* 合计行 */}
                        <tr className="h-[44px]">
                            <td colSpan={3} className="border border-[#8c8c8c] px-3 text-sm text-[#333]">
                                合计：{toChineseAmount(totals.debit)}
                            </td>
                            <td colSpan={11} className="border border-[#8c8c8c] bg-[#fdfdec] px-2 text-right text-sm text-[#333]">
                                {totals.debit > 0 ? totals.debit.toFixed(2) : ""}
                            </td>
                            <td colSpan={11} className="border border-[#8c8c8c] bg-[#fdfdec] px-2 text-right text-sm text-[#333]">
                                {totals.credit > 0 ? totals.credit.toFixed(2) : ""}
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>
            <div className="mx-auto flex w-[1080px] items-center justify-between py-4 text-sm text-[#333]">
                <CheckBox label={`制单人：${user?.displayName || user?.username || "-"}`} checked />
                <span className="flex items-center gap-1 text-[#999]">
                    <RefreshIcon />
                </span>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
