/**
 * 柠檬云凭证模块（已接真实后端 VouchersApi）：
 * - VoucherNew  新增凭证（表格化借贷金额栏 + 凭证头 + 制单人，可保存落库）
 *   凭证字取自账套 voucherWords；下一凭证号按当期已有最大号 +1（后端无 next-number 端点）。
 * 查看凭证与既有实页重叠，复刻版已下线（见 router）。
 */
import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router";
import {
    CloseOutlined,
    ControlOutlined,
    FileTextOutlined,
    FullscreenOutlined,
    PlusOutlined,
    SearchOutlined,
} from "@ant-design/icons";
import { Input, Select as GridSelect } from "antd";

import { Btn, CheckBox, DateInput, Page, RefreshIcon, Select, TextInput, Toast, WipBtn } from "./components/ui";
import { ICON_STYLE } from "@/components/page/styles";
import { useAuth } from "@/auth/AuthContext";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { useCurrentAccountSet, useDefaultAccountingPeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { AccountsApi, VouchersApi, type AccountDto } from "@/apis-gen";

/* ================= 新增凭证 ================= */

/** 金额栏位数（亿千百十万千百十元角分）。 */
const DIGITS = ["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"];

const EDIT_CLS = "w-full";
/** 单元格输入框尺寸：antd 的 .ant-input 是无层级声明，Tailwind 的 h-/text- 压不过，只能内联。 */
const EDIT_STYLE = { height: 44, fontSize: 13 } as const;

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

/** 凭证默认记账日期：今天，若不在当前会计期间内则归到期间边界（不再硬编码日期）。 */
function defaultVoucherDate(period: AccountingPeriod): string {
    const today = todayText();
    if (today < period.startDate) return period.startDate;
    if (today > period.endDate) return period.endDate;
    return today;
}

/** 账套 voucherWords（逗号分隔字符串）→ 去空去重后的凭证字列表。 */
function parseVoucherWords(raw?: string): string[] {
    const list = (raw ?? "")
        .split(",")
        .map((s) => s.trim())
        .filter(Boolean);
    return list.length > 0 ? [...new Set(list)] : ["记"];
}

export function VoucherNew() {
    const { token, user } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const navigate = useNavigate();

    const accountSet = useCurrentAccountSet();
    const period = useDefaultAccountingPeriod();
    const words = useMemo(() => parseVoucherWords(accountSet?.voucherWords), [accountSet]);

    const [accounts, setAccounts] = useState<AccountDto[]>([]);
    const [nextNumber, setNextNumber] = useState<number | null>(null);
    const [word, setWord] = useState("");
    const [date, setDate] = useState("");
    const [attachmentCount, setAttachmentCount] = useState("");
    const [rows, setRows] = useState<EntryRow[]>(emptyRows);
    const [loading, setLoading] = useState(false);
    const [metaError, setMetaError] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    // 凭证字：账套列表加载后默认取第一个（或保持当前仍有效的选择）。
    useEffect(() => {
        setWord((prev) => (prev && words.includes(prev) ? prev : words[0]));
    }, [words]);

    // 记账日期：期间确定后归入当期（用户仍可手动改）。
    useEffect(() => {
        setDate(defaultVoucherDate(period));
    }, [period]);

    // 加载明细科目与下一个凭证号。
    const reloadMeta = useCallback(async () => {
        if (!token || !accountSetId || !word) {
            return;
        }
        setLoading(true);
        setMetaError(null);
        try {
            const api = new VouchersApi(apiConfig(token));
            const [leafAccounts, vouchers] = await Promise.all([
                new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true }),
                // 后端无「下一个凭证号」端点：把取数限定到当前会计期间，按当期同凭证字已有最大号 +1 推算。
                api.accountsetsAccountSetIdVouchersGet({
                    accountSetId,
                    startDate: new Date(period.startDate),
                    endDate: new Date(period.endDate),
                }),
            ]);
            setAccounts(leafAccounts ?? []);
            const maxNo = (vouchers ?? [])
                .filter((v) => (v.word ?? "") === word)
                .reduce((m, v) => Math.max(m, v.number ?? 0), 0);
            setNextNumber(maxNo + 1);
        } catch (e) {
            const msg = await apiErrorMessage(e, "加载科目或凭证失败");
            setMetaError(msg);
            setToast(msg);
        } finally {
            setLoading(false);
        }
    }, [token, accountSetId, word, period]);
    useEffect(() => {
        void reloadMeta();
    }, [reloadMeta]);

    const accountOptions = useMemo(
        () => (accounts ?? []).map((a) => ({ value: a.id ?? "", label: `${a.code ?? ""} ${a.name ?? ""}` })),
        [accounts],
    );

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
        if (!word || !date) {
            setToast("凭证字或记账日期尚未就绪，请稍候再试");
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

    const canSave = !saving && !loading && !!word && !!date && totals.balanced;
    return (
        <Page title="新增凭证" subtitle="录入借贷分录并保存过账" icon={FileTextOutlined}>
            <div className="relative flex min-h-[56px] items-center border-b border-slate-100 px-4 py-2">
                <div className="flex flex-1 items-center justify-center gap-3">
                    <Btn variant={canSave ? "primary" : "disabled"} disabled={!canSave} onClick={() => void save(true)}>
                        保存并新增
                    </Btn>
                    <Btn variant={canSave ? "primary" : "disabled"} disabled={!canSave} onClick={() => void save(false)}>
                        保存
                    </Btn>
                    <WipBtn dropdown title="凭证打印尚未接入，建设中">
                        打印
                    </WipBtn>
                    <WipBtn dropdown title="更多操作尚未接入，建设中">
                        更多
                    </WipBtn>
                </div>
                <div className="absolute right-4 flex items-center gap-4 text-sm text-slate-700">
                    <span className="flex items-center gap-1">
                        <ControlOutlined className="text-[16px]" style={ICON_STYLE.muted} /> 快捷键
                    </span>
                    <span className="flex items-center gap-1 text-slate-400">
                        上一页 <CloseOutlined className="text-[12px]" /> <PlusOutlined className="text-[12px]" /> 下一页
                    </span>
                </div>
            </div>
            {/* 加载 / 错误提示（拉科目与凭证字时可见） */}
            {loading && <div className="mx-auto w-[1080px] py-1 text-[13px] text-slate-400">正在加载科目与凭证字…</div>}
            {metaError && <div className="mx-auto w-[1080px] py-1 text-[13px] text-[#ef4444]">{metaError}</div>}
            {/* 凭证头 */}
            <div className="mx-auto flex w-[1080px] flex-wrap items-center gap-2 py-3">
                <Select value={word} onChange={setWord} options={words} className="w-[88px]" />
                <span className="inline-flex h-8 w-[64px] items-center justify-center rounded-lg border border-slate-200 text-sm">
                    {loading ? "…" : (nextNumber ?? "-")}
                </span>
                <span className="text-sm text-slate-700">号</span>
                <span className="text-sm text-slate-700">日期：</span>
                <DateInput value={date} onChange={setDate} className="w-[150px]" />
                <span className="ml-3 text-[13px] text-[#92640b]">亲，按回车键，能快速选择单元格哟！</span>
                <span className="ml-auto flex items-center gap-2 text-sm text-slate-700">
                    附单据
                    <TextInput
                        className="w-[64px]"
                        value={attachmentCount}
                        onChange={(v) => setAttachmentCount(v.replace(/\D/g, ""))}
                    />
                    张
                    <span className="cursor-pointer text-[#2f6bff]">附件</span>
                    <span className="cursor-pointer text-[#2f6bff]">备注 ⓘ</span>
                    <span className="flex items-center gap-1">
                        <SearchOutlined className="text-[16px]" style={ICON_STYLE.muted} /> 放大
                    </span>
                    <span className="flex items-center gap-1">
                        <FullscreenOutlined className="text-[16px]" style={ICON_STYLE.muted} /> 全屏
                    </span>
                </span>
            </div>
            {/* 分录网格 */}
            <div className="mx-auto w-[1080px]">
                <table className="w-full border-collapse">
                    <thead>
                        <tr className="bg-muted text-[13px] text-slate-500">
                            <th rowSpan={2} className="w-[46px] border border-slate-200 px-1 py-1.5 font-normal">
                                序号
                            </th>
                            <th rowSpan={2} className="w-[240px] border border-slate-200 px-2 py-1.5 font-normal">
                                摘要
                            </th>
                            <th rowSpan={2} className="border border-slate-200 px-2 py-1.5 font-normal">
                                会计科目
                            </th>
                            <th colSpan={11} className="border border-slate-200 px-2 py-1 font-normal">
                                借方金额
                            </th>
                            <th colSpan={11} className="border border-slate-200 px-2 py-1 font-normal">
                                贷方金额
                            </th>
                        </tr>
                        <tr className="bg-muted text-[10px] text-slate-500">
                            {DIGITS.map((d, i) => (
                                <th
                                    key={`j${i}`}
                                    className="w-[20px] border border-slate-200 py-0.5 font-normal"
                                    style={{ borderLeftColor: i === 8 ? "#fecaca" : undefined }}
                                >
                                    {d}
                                </th>
                            ))}
                            {DIGITS.map((d, i) => (
                                <th
                                    key={`d${i}`}
                                    className="w-[20px] border border-slate-200 py-0.5 font-normal"
                                    style={{ borderLeftColor: i === 8 ? "#fecaca" : undefined }}
                                >
                                    {d}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map((r, idx) => (
                            <tr key={idx} className="h-[52px] transition-colors hover:bg-[#f6f9ff]">
                                <td className="border border-slate-200 text-center text-[13px] text-slate-600 tabular-nums">{idx + 1}</td>
                                <td className="border border-slate-200 p-0">
                                    <Input
                                        variant="borderless"
                                        className={EDIT_CLS}
                                        style={EDIT_STYLE}
                                        value={r.summary}
                                        onChange={(e) => patchRow(idx, { summary: e.target.value })}
                                        placeholder="摘要"
                                    />
                                </td>
                                <td className={`border border-slate-200 p-0 ${idx === 0 ? "bg-[#eef4ff]" : ""}`}>
                                    <GridSelect
                                        variant="borderless"
                                        className="w-full"
                                        style={EDIT_STYLE}
                                        placeholder="选择科目"
                                        value={r.accountId || undefined}
                                        options={accountOptions}
                                        onChange={(v) => patchRow(idx, { accountId: v ?? "" })}
                                    />
                                </td>
                                <td colSpan={11} className="border border-slate-200 p-0">
                                    <Input
                                        variant="borderless"
                                        className={`${EDIT_CLS} tabular-nums`}
                                        style={{ ...EDIT_STYLE, textAlign: "right" }}
                                        value={r.debit}
                                        onChange={(e) => patchRow(idx, { debit: e.target.value.replace(/[^\d.]/g, ""), credit: "" })}
                                        placeholder="0.00"
                                    />
                                </td>
                                <td colSpan={11} className="border border-slate-200 p-0">
                                    <Input
                                        variant="borderless"
                                        className={`${EDIT_CLS} tabular-nums`}
                                        style={{ ...EDIT_STYLE, textAlign: "right" }}
                                        value={r.credit}
                                        onChange={(e) => patchRow(idx, { credit: e.target.value.replace(/[^\d.]/g, ""), debit: "" })}
                                        placeholder="0.00"
                                    />
                                </td>
                            </tr>
                        ))}
                        {/* 合计行 */}
                        <tr className="h-[44px]">
                            <td colSpan={3} className="border border-slate-200 px-3 text-sm text-slate-700">
                                合计：{toChineseAmount(totals.debit)}
                            </td>
                            <td colSpan={11} className="border border-slate-200 bg-[#fffbeb] px-2 text-right text-sm text-slate-700 tabular-nums">
                                {totals.debit > 0 ? totals.debit.toFixed(2) : ""}
                            </td>
                            <td colSpan={11} className="border border-slate-200 bg-[#fffbeb] px-2 text-right text-sm text-slate-700 tabular-nums">
                                {totals.credit > 0 ? totals.credit.toFixed(2) : ""}
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>
            <div className="mx-auto flex w-[1080px] items-center justify-between border-t border-slate-100 py-4 text-sm text-slate-700">
                <CheckBox label={`制单人：${user?.displayName || user?.username || "-"}`} checked />
                <span className="flex items-center gap-1 text-slate-400">
                    <RefreshIcon />
                </span>
            </div>
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
