/**
 * 智能报税 · 个税申报 · 报表填写（综合工资薪金所得）。
 *
 * 申报表由本系统工资表按累计预扣法算出：本期收入取应发工资，累计列取本年 1 月至所属期，
 * 已缴税额取 1 月至上月已登记的个税，应补（退）税额 = 累计应纳税额 − 已缴税额。
 * 工资表只有「代扣个人款项」合计，险种明细、专项附加扣除与减免捐赠需登录电子税务局取回，
 * 因此这些列保留税局表头、内容留空；正式申报同样引导到「一键登录税局」。
 */
import { useCallback, useEffect, useMemo, useState } from "react";
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { Card, App, Button, Dropdown, Input, Modal, Radio, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import { DownOutlined, FilterFilled, QuestionCircleOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router";

import { useTaxPeriod } from "./tax-period";
import { useAuth } from "@/auth/AuthContext";
import { downloadCsv } from "@/lib/tax-csv";
import { formatAmount } from "@/lib/tax-format";
import { fetchIitReport, type TaxIitReportRow } from "@/lib/tax-portal";
import { setPath } from "@/accountSets/url-id";

/** 金额列宽度；未接入的列只占位，等税局取数后填充。 */
const LEAF_WIDTH = 112;

type CellKind = "money" | "rate" | "text";

interface Leaf {
    title: string;
    /** 接口能算出的列；留空表示该列尚未接入，渲染成「—」。 */
    field?: keyof TaxIitReportRow;
    width?: number;
    kind?: CellKind;
}

interface Group {
    title: string;
    /** 分组列的取数说明，挂在表头的问号上。 */
    tip?: string;
    children: Leaf[];
}

type ColSpec = Leaf | Group;

const isGroup = (spec: ColSpec): spec is Group => "children" in spec;

const AMOUNT_COLUMNS: ColSpec[] = [
    {
        title: "本期收入额",
        children: [
            { title: "本期收入", field: "currentIncome" },
            { title: "本期免税收入" },
        ],
    },
    { title: "减除费用标准", field: "standardDeduction" },
    {
        title: "本期专项扣除",
        tip: "工资表按「代扣个人款项」合计登记，不拆分险种；扣除明细需登录电子税务局取回。",
        children: [
            { title: "基本养老保险费" },
            { title: "基本医疗保险费" },
            { title: "失业保险费" },
            { title: "住房公积金" },
            { title: "企业（职业）年金" },
        ],
    },
    {
        title: "本期其他扣除",
        tip: "商业健康保险、税延养老保险等其他扣除项目需在电子税务局采集后取回。",
        children: [
            { title: "商业健康保险" },
            { title: "税延养老保险" },
            { title: "公务交通费用" },
            { title: "通讯费用" },
            { title: "律师办案费用" },
            { title: "住房公积金调整" },
            { title: "其他" },
        ],
    },
    { title: "累计收入额", field: "cumulativeIncome" },
    { title: "累计减除费用", field: "cumulativeStandardDeduction" },
    { title: "累计专项扣除", field: "cumulativeSpecialDeduction" },
    {
        title: "累计专项附加扣除",
        tip: "专项附加扣除由员工在个人所得税 App 采集，需登录电子税务局取回。",
        children: [
            { title: "累计子女教育" },
            { title: "累计继续教育" },
            { title: "累计住房贷款利息" },
            { title: "累计住房租金" },
            { title: "累计赡养老人" },
            { title: "累计3岁以下婴幼儿照护" },
        ],
    },
    { title: "累计个人养老金" },
    { title: "累计其他扣除" },
    { title: "准予扣除的捐赠额" },
    { title: "累计准予扣除的捐赠额", width: 148 },
    { title: "备注", field: "remark", kind: "text", width: 120 },
    { title: "累计应纳税所得额", field: "taxableIncome", width: 136 },
    { title: "税率/扣除率", field: "rate", kind: "rate", width: 96 },
    { title: "速算扣除数", field: "quickDeduction" },
    { title: "累计应纳税额", field: "cumulativeTax" },
    { title: "减免税额" },
    { title: "累计减免税额" },
    { title: "协定减免" },
    { title: "累计协定减免" },
    { title: "已缴税额", field: "paidTax" },
    { title: "应补（退）税额", field: "refundable", width: 124 },
];

const LEAVES: Leaf[] = AMOUNT_COLUMNS.flatMap((spec) => (isGroup(spec) ? spec.children : [spec]));

/** 横向滚动宽度：左侧固定三列 + 全部金额列。 */
const TABLE_WIDTH = 372 + LEAVES.reduce((sum, leaf) => sum + (leaf.width ?? LEAF_WIDTH), 0);

const cellText = (row: TaxIitReportRow, leaf: Leaf): string => {
    if (!leaf.field) return "";
    const value = row[leaf.field];
    if (leaf.kind === "text") return String(value ?? "");
    if (leaf.kind === "rate") return `${Number(value) * 100}%`;
    return formatAmount(Number(value));
};

const SUMMARY_NOTES = [
    "本期收入取工资表的应发工资（工资总额 − 应扣工资）。",
    "累计收入额、累计专项扣除为本年 1 月至所属期的合计；累计专项扣除即「代扣个人款项」的累计。",
    "累计减除费用 = 减除费用标准 × 本年截至本月的任职受雇月份数，当年入职的从入职月起算。",
    "累计应纳税所得额 = 累计收入额 − 累计减除费用 − 累计专项扣除 − 累计专项附加扣除 − 其他扣除。",
    "累计应纳税额按综合所得预扣率表计算，应补（退）税额 = 累计应纳税额 − 已缴税额。",
    "本期免税收入、险种明细、专项附加扣除、个人养老金、捐赠与减免需登录电子税务局取回，当前留空。",
];

export function TaxIitReportPage() {
    const period = useTaxPeriod();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const navigate = useNavigate();
    const { token } = useAuth();
    const { message, modal } = App.useApp();

    const [rows, setRows] = useState<TaxIitReportRow[]>([]);
    const [summary, setSummary] = useState<{ personCount: number; totalIncome: number; totalRefundable: number } | null>(
        null,
    );
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [hint, setHint] = useState<string | null>(null);
    const [selected, setSelected] = useState<string[]>([]);
    const [nameKeyword, setNameKeyword] = useState("");
    const [idKeyword, setIdKeyword] = useState("");
    const [generateOpen, setGenerateOpen] = useState(false);
    const [source, setSource] = useState("salary");
    const [notesOpen, setNotesOpen] = useState(false);

    const toTaxLogin = () => navigate(setPath(accountSetId, "/tax/login"));
    const toSalaryBills = () => navigate(setPath(accountSetId, "/salary/manage"));

    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        try {
            const result = await fetchIitReport(accountSetId, token, String(period.period));
            setRows(result.rows ?? []);
            setSummary({
                personCount: result.personCount ?? 0,
                totalIncome: result.totalIncome ?? 0,
                totalRefundable: result.totalRefundable ?? 0,
            });
            setHint(result.message ?? null);
            setError(null);
        } catch (err) {
            setError(err instanceof Error ? err.message : "加载个税申报表失败。");
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token, period.period]);

    useEffect(() => {
        void reload();
    }, [reload]);

    const visibleRows = useMemo(
        () =>
            rows.filter(
                (row) =>
                    (!nameKeyword || row.name.includes(nameKeyword)) &&
                    (!idKeyword || row.idNo.includes(idKeyword)),
            ),
        [rows, nameKeyword, idKeyword],
    );

    /** 没有申报表时汇总按税局口径显示「--」，不显示 0。 */
    const headcountText = summary && rows.length ? String(summary.personCount) : "--";
    const totalIncomeText = summary && rows.length ? formatAmount(summary.totalIncome) : "--";
    const totalRefundableText = summary && rows.length ? formatAmount(summary.totalRefundable) : "--";

    function exportCsv(target: TaxIitReportRow[], label: string) {
        if (target.length === 0) {
            message.warning("没有可导出的申报表数据，请先到工资模块登记当期工资。");
            return;
        }
        const header = ["编号", "姓名", "证件号码", ...LEAVES.map((leaf) => leaf.title)];
        const body = target.map((row) => [
            row.code,
            row.name,
            row.idNo,
            ...LEAVES.map((leaf) => cellText(row, leaf)),
        ]);
        downloadCsv(`个税申报表_${period.period}.csv`, [header, ...body]);
        message.success(`已导出 ${target.length} 条${label}。`);
    }

    /** 正式申报与税款提交都在电子税务局完成，本页只负责按工资表把表算出来。 */
    function openDeclare() {
        modal.confirm({
            title: "发送申报",
            content: `将向电子税务局申报 ${period.year}年${period.month}月 综合所得（工资薪金）${rows.length} 人。申报由税局侧完成，当前需先登录电子税务局后在税局页面办理。`,
            okText: "前往登录税局",
            cancelText: "取消",
            onOk: toTaxLogin,
        });
    }

    const keywordFilter = (
        placeholder: string,
        keyword: string,
        setKeyword: (value: string) => void,
    ) => ({
        filterIcon: <FilterFilled style={{ color: keyword ? "var(--brand)" : "var(--muted-foreground)" }} />,
        filterDropdown: ({ confirm }: { confirm: () => void }) => (
            <div className="w-[200px] p-2" onKeyDown={(event) => event.stopPropagation()}>
                <Input
                    autoFocus
                    allowClear
                    placeholder={placeholder}
                    value={keyword}
                    onChange={(event) => setKeyword(event.target.value)}
                    onPressEnter={() => confirm()}
                />
                <div className="mt-2 flex justify-end gap-2">
                    <Button
                        onClick={() => {
                            setKeyword("");
                            confirm();
                        }}
                    >
                        重置
                    </Button>
                    <Button type="primary" onClick={() => confirm()}>
                        筛选
                    </Button>
                </div>
            </div>
        ),
    });

    const columns: TableColumnsType<TaxIitReportRow> = [
        {
            title: "编号",
            dataIndex: "code",
            key: "code",
            width: 92,
            fixed: "left",
            sorter: (a, b) => a.code.localeCompare(b.code),
        },
        {
            title: "姓名",
            dataIndex: "name",
            key: "name",
            width: 92,
            fixed: "left",
            ...keywordFilter("输入姓名筛选", nameKeyword, setNameKeyword),
        },
        {
            title: "证件号码",
            dataIndex: "idNo",
            key: "idNo",
            width: 188,
            ...keywordFilter("输入证件号码筛选", idKeyword, setIdKeyword),
            render: (value: string) => value || "—",
        },
        ...AMOUNT_COLUMNS.map((spec) => {
            if (isGroup(spec)) {
                return {
                    title: spec.tip ? (
                        <span className="inline-flex items-center gap-1">
                            {spec.title}
                            <Tooltip title={spec.tip}>
                                <QuestionCircleOutlined className="text-[11px] text-(--muted-foreground)" />
                            </Tooltip>
                        </span>
                    ) : (
                        spec.title
                    ),
                    key: spec.title,
                    children: spec.children.map(toLeafColumn),
                };
            }
            return toLeafColumn(spec);
        }),
    ];

    function toLeafColumn(leaf: Leaf) {
        return {
            title: leaf.title,
            dataIndex: leaf.field ?? leaf.title,
            key: leaf.title,
            width: leaf.width ?? LEAF_WIDTH,
            align: leaf.kind === "text" ? ("left" as const) : ("right" as const),
            render: (_: unknown, row: TaxIitReportRow) => {
                const text = cellText(row, leaf);
                return text ? (
                    <span className={leaf.field === "refundable" ? "font-medium text-(--foreground)" : undefined}>
                        {text}
                    </span>
                ) : (
                    <span className="text-(--muted-foreground)">—</span>
                );
            },
        };
    }

    return (
        <Card>
            <div className="flex shrink-0 items-center border-b border-(--border)">
                <button
                    type="button"
                    className="relative h-[46px] text-[13px] font-medium text-(--foreground)"
                >
                    综合工资薪金所得
                    <span className="absolute inset-x-0 bottom-0 h-[2px] bg-(--brand-fill)" />
                </button>
            </div>

            <div className="flex shrink-0 flex-wrap items-center gap-6 pt-3 text-[13px]">
                <span className="text-(--muted-foreground)">
                    税款所属期：
                    <span className="font-medium text-(--foreground)">
                        {period.year}年{period.month}月
                    </span>
                </span>
                <span className="text-(--muted-foreground)">
                    申报总人数：
                    <span className="font-medium text-(--foreground)">{headcountText}</span> 人
                </span>
                <span className="text-(--muted-foreground)">
                    收入总额：
                    <span className="font-medium text-(--foreground)">{totalIncomeText}</span> 元
                </span>
                <span className="text-(--muted-foreground)">
                    应补（退）税额：
                    <span className="font-medium text-(--foreground)">{totalRefundableText}</span> 元
                </span>

                <div className="ml-auto flex flex-wrap items-center gap-2">
                    <Button onClick={() => setGenerateOpen(true)}>生成申报表</Button>
                    <Tooltip title="按累计预扣法重算本期申报表">
                        <Button disabled={rows.length === 0} onClick={() => void reload()}>
                            税款计算
                        </Button>
                    </Tooltip>
                    <Button
                        onClick={() =>
                            message.warning("手动导入个税申报表尚未接入，申报表数据来自本系统工资表。")
                        }
                    >
                        导入
                    </Button>
                    <Button onClick={() => exportCsv(visibleRows, "申报表记录")}>导出</Button>
                    <Dropdown
                        trigger={["click"]}
                        menu={{
                            items: [
                                {
                                    key: "selected",
                                    label: `导出勾选记录（已选 ${selected.length} 人）`,
                                    disabled: selected.length === 0,
                                },
                                {
                                    key: "clear",
                                    label: "清空报表",
                                    disabled: rows.length === 0,
                                },
                                { type: "divider" },
                                { key: "notes", label: "查看填报说明" },
                            ],
                            onClick: ({ key }) => {
                                if (key === "selected")
                                    exportCsv(
                                        rows.filter((row) => selected.includes(row.key)),
                                        "勾选的申报表记录",
                                    );
                                if (key === "clear") {
                                    setRows([]);
                                    setSummary(null);
                                    setSelected([]);
                                    setHint("报表已清空，点「生成申报表」可从工资表重新取数。");
                                }
                                if (key === "notes") setNotesOpen(true);
                            },
                        }}
                    >
                        <Button>
                            更多操作 <DownOutlined className="text-[9px]" />
                        </Button>
                    </Dropdown>
                    <Button type="primary" disabled={rows.length === 0} onClick={openDeclare}>
                        申报
                    </Button>
                </div>
            </div>

            {hint && !error && (
                <div className="shrink-0 pt-3">
                    <div className="flex items-center gap-3 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-1.5 text-[12.5px] text-(--warn-text)">
                        <span>{hint}</span>
                        <Button size="small" onClick={toSalaryBills}>
                            去工资表
                        </Button>
                    </div>
                </div>
            )}

            <div className="min-h-0 flex-1 pb-3 pt-3 [&_.ant-table-body]:min-h-[calc(100vh-360px)] [&_.ant-table-body]:!overflow-y-auto">
                {error && (
                    <div className="mb-3 rounded-md border border-(--warn-border) bg-(--table-neg-bg) px-3 py-1.5 text-[12.5px] text-(--danger)">
                        {error}
                    </div>
                )}
                <Table<TaxIitReportRow>
                    columns={columns}
                    dataSource={visibleRows}
                    rowKey="key"
                    loading={loading}
                    tableLayout="fixed"
                    scroll={{ x: TABLE_WIDTH, y: "calc(100vh - 360px)" }}
                    rowSelection={{
                        selectedRowKeys: selected,
                        onChange: (keys) => setSelected(keys.map(String)),
                        columnWidth: 40,
                        fixed: true,
                    }}
                    pagination={{
                        pageSize: 20,
                        pageSizeOptions: [20, 50, 100],
                        showSizeChanger: true,
                        showTotal: (total, [from, to]) =>
                            total === 0
                                ? "显示 0 到 0，共 0 条记录"
                                : `显示 ${from} 到 ${to}，共 ${total} 条记录`,
                    }}
                    locale={{
                        emptyText: (
                            <span className="block py-16 text-[13px] text-(--muted-foreground)">
                                暂无申报表数据，点「生成申报表」从工资表取数
                            </span>
                        ),
                    }}
                />
            </div>

            <Modal
                open={generateOpen}
                title="生成申报表"
                width={460}
                okText="确定"
                cancelText="取消"
                onCancel={() => setGenerateOpen(false)}
                onOk={() => {
                    setGenerateOpen(false);
                    void reload();
                }}
            >
                <div className="flex gap-4 py-3">
                    <span className="shrink-0 pt-0.5 text-[13px] text-(--muted-foreground)">选择：</span>
                    <Radio.Group
                        value={source}
                        onChange={(event) => setSource(String(event.target.value))}
                        style={{ display: "flex", flexDirection: "column", alignItems: "flex-start", gap: 8 }}
                    >
                        <Radio value="salary">选择财务账套工资表数据</Radio>
                        <Radio value="last" disabled>
                            复制上期个税申报数据
                        </Radio>
                        <Radio value="zero" disabled>
                            生成零工资申报表
                        </Radio>
                        <Radio value="manual" disabled>
                            手动导入个税申报表
                        </Radio>
                    </Radio.Group>
                </div>
                <div className="text-[12px] leading-5 text-(--muted-foreground)">
                    当前仅支持从本系统工资表生成，其余方式依赖电子税务局取数，接入后开放。
                </div>
            </Modal>

            <Modal open={notesOpen} title="填报说明" width={560} footer={null} onCancel={() => setNotesOpen(false)}>
                <ul className="m-0 flex list-disc flex-col gap-2 pl-5 text-[13px] leading-6 text-(--foreground)">
                    {SUMMARY_NOTES.map((note) => (
                        <li key={note}>{note}</li>
                    ))}
                </ul>
            </Modal>
        </Card>
    );
}
