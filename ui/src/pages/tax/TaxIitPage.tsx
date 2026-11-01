/**
 * 智能报税 · 个税申报（人员管理）。
 *
 * 人员信息直接取工资模块的员工档案（同一账套同一份数据），在本页不做重复维护：
 * 新增 / 导入 / 编辑统一回到工资模块，删除沿用员工接口（已有工资单的员工由后端拒绝）。
 * 报送状态与验证状态只做本地标记：验证状态按居民身份证校验位本地校验，
 * 正式报送与税局实名验证需要登录电子税务局，按钮统一引导到「一键登录税局」。
 */
import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, App, Button, Checkbox, ConfigProvider, Dropdown, Input, Table, Tooltip } from "antd";
import type { TableColumnsType } from "antd";
import {
    DownOutlined,
    FilterFilled,
    InfoCircleOutlined,
    QuestionCircleOutlined,
    ReloadOutlined,
} from "@ant-design/icons";
import { useNavigate, useParams } from "react-router";

import { SalariesApi, type EmployeeDto } from "@/apis-gen";
import { useAuth } from "@/auth/AuthContext";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { cn } from "@/lib/utils";

type TabKey = "people" | "deduction";

const TABS: Array<{ key: TabKey; label: string }> = [
    { key: "people", label: "人员信息" },
    { key: "deduction", label: "专项附加扣除信息" },
];

interface IitRow {
    key: string;
    id: string;
    code: string;
    name: string;
    dept: string;
    idType: string;
    idNo: string;
    phone: string;
    /** 任职受雇从业类型：工资模块的员工档案按「雇员」报送。 */
    employType: string;
    status: string;
    hireDate: string;
    reportStatus: string;
    verifyStatus: string;
    gender: string;
    email: string;
    remark: string;
}

const ID_WEIGHTS = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2];
const ID_CHECK_CODES = "10X98765432";

/** 居民身份证 18 位校验位，只用于本地格式校验。 */
function isValidIdCard(no: string): boolean {
    if (!/^\d{17}[\dXx]$/.test(no)) return false;
    const sum = ID_WEIGHTS.reduce((acc, weight, index) => acc + Number(no[index]) * weight, 0);
    return ID_CHECK_CODES[sum % 11] === no[17].toUpperCase();
}

/** 验证状态：身份证按校验位本地校验，其余证件类型暂不校验。 */
function verifyStatusOf(idType: string, idNo: string): string {
    if (!idNo.trim() || idNo.includes("*") || !idType.includes("身份证")) return "未验证";
    return isValidIdCard(idNo.trim()) ? "验证通过" : "验证不通过";
}

const dayOf = (value: Date | string | null | undefined) =>
    value ? new Date(value).toISOString().substring(0, 10) : "";

function toRow(employee: EmployeeDto): IitRow {
    const idType = employee.idTypeText ?? "";
    const idNo = employee.idNo ?? "";
    return {
        key: employee.id ?? employee.code ?? "",
        id: employee.id ?? "",
        code: employee.code ?? "",
        name: employee.name ?? "",
        dept: employee.dept ?? "",
        idType,
        idNo,
        phone: employee.phone ?? "",
        employType: "雇员",
        status: employee.statusText ?? "",
        hireDate: dayOf(employee.hireDate),
        reportStatus: "待报送",
        verifyStatus: verifyStatusOf(idType, idNo),
        gender: employee.genderText ?? "",
        email: employee.email ?? "",
        remark: employee.remark ?? "",
    };
}

export function TaxIitPage() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { token } = useAuth();
    const { message, modal } = App.useApp();

    const [tab, setTab] = useState<TabKey>("people");
    const [employees, setEmployees] = useState<EmployeeDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [showAll, setShowAll] = useState(false);
    const [statusFilter, setStatusFilter] = useState<string | null>(null);
    const [deptFilter, setDeptFilter] = useState<string | null>(null);
    const [onlyUnreported, setOnlyUnreported] = useState(false);
    const [selected, setSelected] = useState<string[]>([]);
    const [nameKeyword, setNameKeyword] = useState("");
    const [phoneKeyword, setPhoneKeyword] = useState("");
    const [busy, setBusy] = useState(false);

    const toTaxLogin = () => navigate(`/${accountSetId}/tax/login`);
    const toSalaryEmployees = () => navigate(`/${accountSetId}/salary/employee`);

    const reload = useCallback(async () => {
        if (!accountSetId) {
            setLoading(false);
            return;
        }
        setLoading(true);
        try {
            const rows = await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryEmployeesGet({ accountSetId });
            setEmployees(rows);
            setError(null);
        } catch (err) {
            setError(await apiErrorMessage(err, "加载员工信息失败。"));
        } finally {
            setLoading(false);
        }
    }, [accountSetId, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    const rows = useMemo(() => employees.map(toRow), [employees]);

    const departments = useMemo(
        () => [...new Set(rows.map((row) => row.dept).filter(Boolean))].sort((a, b) => a.localeCompare(b, "zh-CN")),
        [rows],
    );

    const activeCount = useMemo(() => rows.filter((row) => row.status === "在职").length, [rows]);
    const unreportedCount = useMemo(() => rows.filter((row) => row.reportStatus !== "已报送成功").length, [rows]);

    /** 工具栏（部门 / 在职 / 未报送）先过滤，列头上的筛选再交给 antd。 */
    const visibleRows = useMemo(
        () =>
            rows.filter(
                (row) =>
                    (!statusFilter || row.status === statusFilter) &&
                    (!deptFilter || row.dept === deptFilter) &&
                    (!onlyUnreported || row.reportStatus !== "已报送成功") &&
                    (!nameKeyword || row.name.includes(nameKeyword)) &&
                    (!phoneKeyword || row.phone.includes(phoneKeyword)),
            ),
        [rows, statusFilter, deptFilter, onlyUnreported, nameKeyword, phoneKeyword],
    );

    function deleteEmployee(row: IitRow) {
        modal.confirm({
            title: "删除人员",
            content: `确定删除「${row.name || row.code}」吗？已生成工资单的员工不会被删除。`,
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: async () => {
                try {
                    await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryEmployeesIdDelete({
                        accountSetId,
                        id: row.id,
                    });
                    message.success("人员已删除。");
                    setSelected((prev) => prev.filter((id) => id !== row.id));
                    await reload();
                } catch (err) {
                    message.error(await apiErrorMessage(err, "删除失败。"));
                }
            },
        });
    }

    async function bulkDelete() {
        if (selected.length === 0 || busy) return;
        setBusy(true);
        try {
            const result = await new SalariesApi(apiConfig(token)).accountsetsAccountSetIdSalaryEmployeesBulkDeletePost({
                accountSetId,
                salaryBulkIdsRequest: { ids: selected },
            });
            const extra = result.messages?.length ? `；${result.messages.slice(0, 3).join("，")}` : "";
            message.success(`已删除 ${result.affected} 人${(result.skipped ?? 0) > 0 ? `，跳过 ${result.skipped} 人` : ""}${extra}`);
            setSelected([]);
            await reload();
        } catch (err) {
            message.error(await apiErrorMessage(err, "批量删除失败。"));
        } finally {
            setBusy(false);
        }
    }

    /** 导出员工名册 / 下载导入模板：二进制下载，文件名以响应头为准。 */
    async function download(path: string, fallbackName: string) {
        if (!token || !accountSetId) return;
        try {
            const res = await fetch(`/api/accountsets/${accountSetId}/salary/${path}`, {
                headers: { Authorization: `Bearer ${token}` },
            });
            if (!res.ok) throw new Error(`下载失败（HTTP ${res.status}）`);
            const disposition = res.headers.get("Content-Disposition") ?? "";
            const star = /filename\*=UTF-8''([^;\n]+)/i.exec(disposition);
            const plain = /filename="?([^";\n]+)"?/i.exec(disposition);
            const name = star ? decodeURIComponent(star[1]) : plain?.[1] ?? fallbackName;
            const href = URL.createObjectURL(await res.blob());
            const link = document.createElement("a");
            link.href = href;
            link.download = name;
            link.click();
            URL.revokeObjectURL(href);
        } catch (err) {
            message.error(err instanceof Error ? err.message : "下载失败。");
        }
    }

    /** 报送 / 同步税局人员信息都依赖电子税务局，当前只能引导去登录。 */
    function requireEtax(title: string, content: string) {
        modal.confirm({
            title,
            content,
            okText: "前往登录税局",
            cancelText: "取消",
            onOk: toTaxLogin,
        });
    }

    function openReport() {
        const count = selected.length > 0 ? selected.length : rows.length;
        requireEtax(
            "报送人员信息",
            `将向电子税务局报送 ${count} 名人员信息。报送由税局侧完成，当前需先登录电子税务局后在税局页面办理。`,
        );
    }

    const nameColumnFilter = {
        filterIcon: <FilterFilled style={{ color: nameKeyword ? "#2f6bff" : "#c0c4cc", fontSize: 11 }} />,
        filterDropdown: ({ confirm }: { confirm: () => void }) => (
            <div className="w-[200px] p-2" onKeyDown={(event) => event.stopPropagation()}>
                <Input
                    autoFocus
                    allowClear
                    placeholder="输入姓名筛选"
                    value={nameKeyword}
                    onChange={(event) => setNameKeyword(event.target.value)}
                    onPressEnter={() => confirm()}
                />
                <div className="mt-2 flex justify-end gap-2">
                    <Button
                        onClick={() => {
                            setNameKeyword("");
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
    };

    const phoneColumnFilter = {
        filterIcon: <FilterFilled style={{ color: phoneKeyword ? "#2f6bff" : "#c0c4cc", fontSize: 11 }} />,
        filterDropdown: ({ confirm }: { confirm: () => void }) => (
            <div className="w-[200px] p-2" onKeyDown={(event) => event.stopPropagation()}>
                <Input
                    autoFocus
                    allowClear
                    placeholder="输入手机号筛选"
                    value={phoneKeyword}
                    onChange={(event) => setPhoneKeyword(event.target.value)}
                    onPressEnter={() => confirm()}
                />
                <div className="mt-2 flex justify-end gap-2">
                    <Button
                        onClick={() => {
                            setPhoneKeyword("");
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
    };

    const columns: TableColumnsType<IitRow> = (() => {
        const base: TableColumnsType<IitRow> = [
            {
                title: "编号",
                dataIndex: "code",
                key: "code",
                width: 100,
                sorter: (a, b) => a.code.localeCompare(b.code),
            },
            { title: "姓名", dataIndex: "name", key: "name", width: 100, ...nameColumnFilter },
            {
                title: "部门",
                dataIndex: "dept",
                key: "dept",
                width: 110,
                filters: departments.map((dept) => ({ text: dept, value: dept })),
                onFilter: (value, row) => row.dept === value,
                render: (value: string) => value || "—",
            },
            { title: "证件类型", dataIndex: "idType", key: "idType", width: 120 },
            {
                title: "证件号码",
                dataIndex: "idNo",
                key: "idNo",
                width: 200,
                render: (value: string) => value || "—",
            },
            { title: "手机", dataIndex: "phone", key: "phone", width: 150, ...phoneColumnFilter },
            {
                title: "任职受雇从业类型",
                dataIndex: "employType",
                key: "employType",
                width: 160,
                filters: [{ text: "雇员", value: "雇员" }],
                onFilter: (value, row) => row.employType === value,
            },
            {
                title: "员工状态",
                dataIndex: "status",
                key: "status",
                width: 110,
                filters: [
                    { text: "在职", value: "在职" },
                    { text: "离职", value: "离职" },
                ],
                onFilter: (value, row) => row.status === value,
            },
            {
                title: "入职日期",
                dataIndex: "hireDate",
                key: "hireDate",
                width: 130,
                sorter: (a, b) => a.hireDate.localeCompare(b.hireDate),
                render: (value: string) => value || "—",
            },
            {
                title: (
                    <span className="inline-flex items-center gap-1">
                        报送状态
                        <Tooltip title="尚未接入税局报送，人员默认显示待报送。">
                            <QuestionCircleOutlined className="text-[11px] text-slate-400" />
                        </Tooltip>
                    </span>
                ),
                dataIndex: "reportStatus",
                key: "reportStatus",
                width: 120,
                filters: [
                    { text: "待报送", value: "待报送" },
                    { text: "已报送成功", value: "已报送成功" },
                ],
                onFilter: (value, row) => row.reportStatus === value,
            },
            {
                title: (
                    <span className="inline-flex items-center gap-1">
                        验证状态
                        <Tooltip title="身份证按证件号码校验位本地校验；税局实名验证需登录电子税务局完成。">
                            <QuestionCircleOutlined className="text-[11px] text-slate-400" />
                        </Tooltip>
                    </span>
                ),
                dataIndex: "verifyStatus",
                key: "verifyStatus",
                width: 130,
                filters: [
                    { text: "验证通过", value: "验证通过" },
                    { text: "验证不通过", value: "验证不通过" },
                    { text: "未验证", value: "未验证" },
                ],
                onFilter: (value, row) => row.verifyStatus === value,
                render: (value: string) => (
                    <span className="inline-flex items-center gap-1.5">
                        <span
                            className={cn(
                                "h-1.5 w-1.5 rounded-full",
                                value === "验证通过"
                                    ? "bg-[#10b981]"
                                    : value === "验证不通过"
                                        ? "bg-[#ef4444]"
                                        : "bg-slate-300",
                            )}
                        />
                        {value}
                    </span>
                ),
            },
        ];

        if (showAll) {
            base.push(
                { title: "性别", dataIndex: "gender", key: "gender", width: 80 },
                { title: "邮箱", dataIndex: "email", key: "email", width: 180 },
                { title: "备注", dataIndex: "remark", key: "remark", width: 180 },
            );
        }

        base.push({
            title: "操作",
            key: "op",
            width: 130,
            render: (_, row) => (
                <span className="flex gap-2">
                    <Button
                        type="link"
                        title="人员信息统一在工资模块维护"
                        onClick={toSalaryEmployees}
                    >
                        编辑
                    </Button>
                    <Button
                        type="link"
                        danger
                        onClick={() => deleteEmployee(row)}
                    >
                        删除
                    </Button>
                </span>
            ),
        });

        return base;
    })();

    const filterMenuItems = [
        { key: "all", label: "所有员工" },
        { key: "在职", label: `在职（${activeCount}人）` },
        { key: "离职", label: "离职" },
        ...(departments.length > 0
            ? [
                  { type: "divider" as const },
                  {
                      type: "group" as const,
                      label: "按部门",
                      children: departments.map((dept) => ({ key: `dept:${dept}`, label: dept })),
                  },
              ]
            : []),
    ];

    return (
        <div className="flex h-full flex-col">
            <div className="flex shrink-0 flex-wrap items-center gap-4 border-b border-slate-200 px-6">
                <div className="flex items-center gap-6">
                    {TABS.map((item) => (
                        <button
                            key={item.key}
                            type="button"
                            onClick={() => setTab(item.key)}
                            className={cn(
                                "relative h-[46px] text-[13px] transition-colors",
                                tab === item.key
                                    ? "font-medium text-slate-800"
                                    : "text-slate-500 hover:text-slate-700",
                            )}
                        >
                            {item.label}
                            {tab === item.key && <span className="absolute inset-x-0 bottom-0 h-[2px] bg-[#2f6bff]" />}
                        </button>
                    ))}
                </div>
                {tab === "people" && (
                    <div className="ml-6 flex min-w-0 flex-1 items-center gap-2 rounded-[2px] border border-[#f0d7a8] bg-[#fff9ef] px-3 py-1.5 text-[12.5px] leading-5 text-[#8a6a35]">
                        <InfoCircleOutlined className="shrink-0 text-[#e6a23c]" />
                        <span>
                            人员信息列表数据与财务软件账套工资模块的员工信息数据为互通数据，新增或修改人员信息会同步更新工资模块的员工信息。
                        </span>
                    </div>
                )}
            </div>

            {tab === "deduction" ? (
                <div className="flex min-h-0 flex-1 flex-col items-center justify-center gap-3 px-6 text-center">
                    <span className="flex h-14 w-14 items-center justify-center rounded-full bg-[#eef4ff] text-[22px] text-[#2f6bff]">
                        <InfoCircleOutlined />
                    </span>
                    <div className="text-[15px] font-medium text-slate-700">专项附加扣除信息</div>
                    <div className="max-w-[520px] text-[13px] leading-6 text-slate-500">
                        专项附加扣除由员工在个人所得税 App 采集，本系统需要登录电子税务局后取回。
                        当前尚未接入税局取数，请先前往一键登录税局，在税局页面查看与维护。
                    </div>
                    <Button type="primary" onClick={toTaxLogin}>
                        前往一键登录税局
                    </Button>
                </div>
            ) : (
                <>
                    <div className="flex shrink-0 flex-wrap items-center gap-3 px-6 pt-3" style={{ marginBottom: 16 }}>
                        <Dropdown
                            trigger={["click"]}
                            menu={{
                                items: filterMenuItems,
                                selectable: true,
                                selectedKeys: [deptFilter ? `dept:${deptFilter}` : statusFilter ?? "all"],
                                onClick: ({ key }) => {
                                    if (key === "all") {
                                        setStatusFilter(null);
                                        setDeptFilter(null);
                                    } else if (key.startsWith("dept:")) {
                                        setDeptFilter(key.slice(5));
                                        setStatusFilter(null);
                                    } else {
                                        setStatusFilter(key);
                                    }
                                },
                            }}
                        >
                            <Button className="!text-[#2f6bff]">
                                筛选 <DownOutlined className="text-[9px]" />
                            </Button>
                        </Dropdown>
                        <Button
                            type="link"
                            onClick={() => {
                                setDeptFilter(null);
                                setStatusFilter(statusFilter === "在职" ? null : "在职");
                            }}
                            className={cn(
                                statusFilter === "在职"
                                    ? "font-medium !text-[#2f6bff]"
                                    : "!text-slate-600 hover:!text-[#2f6bff]",
                            )}
                        >
                            在职：
                            <span className="font-medium text-[#e6a23c]">{activeCount}</span>人
                        </Button>
                        <Button
                            type="link"
                            onClick={() => setOnlyUnreported((prev) => !prev)}
                            className={cn(
                                onlyUnreported
                                    ? "font-medium !text-[#2f6bff]"
                                    : "!text-slate-600 hover:!text-[#2f6bff]",
                            )}
                        >
                            未报送成功：
                            <span className="font-medium text-[#e6a23c]">{unreportedCount}</span>人
                        </Button>

                        <div className="ml-auto flex flex-wrap items-center gap-2">
                            <Tooltip title="勾选后显示性别、邮箱、备注等工资模块字段">
                                <Checkbox checked={showAll} onChange={(event) => setShowAll(event.target.checked)}>
                                    <span className="text-[13px]">显示所有信息</span>
                                </Checkbox>
                            </Tooltip>
                            <Button type="primary" onClick={toSalaryEmployees}>
                                增加员工
                            </Button>
                            <Button onClick={toSalaryEmployees}>导入</Button>
                            <span className="inline-flex items-center gap-1">
                                <Button
                                    onClick={() =>
                                        requireEtax(
                                            "同步税局人员信息",
                                            "同步会从电子税务局取回已登记的人员信息，需要先登录税局。",
                                        )
                                    }
                                >
                                    同步税局人员信息
                                </Button>
                                <Tooltip title="从电子税务局取回人员信息；需先登录税局。">
                                    <QuestionCircleOutlined className="text-[12px] text-slate-400" />
                                </Tooltip>
                            </span>
                            <Button type="primary" onClick={openReport}>
                                报送
                            </Button>
                            <Dropdown
                                trigger={["click"]}
                                menu={{
                                    items: [
                                        { key: "export", label: "导出员工名册" },
                                        { key: "template", label: "下载导入模板" },
                                        { type: "divider" },
                                        {
                                            key: "delete",
                                            label: `批量删除（已选 ${selected.length} 人）`,
                                            disabled: selected.length === 0 || busy,
                                            danger: true,
                                        },
                                    ],
                                    onClick: ({ key }) => {
                                        if (key === "export") void download("employees/export", "员工信息.xlsx");
                                        if (key === "template") void download("employees/import-template", "员工导入模板.xlsx");
                                        if (key === "delete") void bulkDelete();
                                    },
                                }}
                            >
                                <Button>
                                    更多操作 <DownOutlined className="text-[9px]" />
                                </Button>
                            </Dropdown>
                            <Tooltip title="刷新">
                                <Button
                                    type="text"
                                    icon={<ReloadOutlined />}
                                    aria-label="刷新"
                                    onClick={() => void reload()}
                                />
                            </Tooltip>
                        </div>
                    </div>

                    <div className="min-h-0 flex-1 px-6 pb-3 [&_.ant-table-body]:min-h-[calc(100vh-320px)] [&_.ant-table-body]:!overflow-y-auto">
                        {error && (
                            <Alert type="error" showIcon title={error} className="mb-3" />
                        )}
                        <ConfigProvider
                            theme={{
                                components: {
                                    Table: {
                                        headerBg: "#f4f7ff",
                                        headerColor: "#475569",
                                        borderColor: "#eef2f7",
                                        rowHoverBg: "#f6f9ff",
                                    },
                                },
                            }}
                        >
                            <Table<IitRow>
                                columns={columns}
                                dataSource={visibleRows}
                                rowKey="key"
                                loading={loading}
                                tableLayout="fixed"
                                scroll={{ x: showAll ? 2040 : 1600, y: "calc(100vh - 320px)" }}
                                rowSelection={{
                                    selectedRowKeys: selected,
                                    onChange: (keys) => setSelected(keys.map(String)),
                                    columnWidth: 40,
                                }}
                                pagination={{
                                    pageSize: 20,
                                    pageSizeOptions: [20, 50, 100],
                                    showSizeChanger: true,
                                    showQuickJumper: true,
                                    showTotal: (total, [from, to]) =>
                                        total === 0
                                            ? "显示 0 到 0，共 0 条记录"
                                            : `显示 ${from} 到 ${to}，共 ${total} 条记录`,
                                }}
                                locale={{
                                    emptyText: (
                                        <span className="block py-16 text-[13px] text-slate-400">
                                            暂无人员信息，请到工资模块新增员工
                                        </span>
                                    ),
                                }}
                            />
                        </ConfigProvider>
                    </div>
                </>
            )}
        </div>
    );
}
