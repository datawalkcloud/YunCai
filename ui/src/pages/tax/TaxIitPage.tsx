/**
 * 智能报税 · 个税申报（人员管理）。
 *
 * 人员信息直接取工资模块的员工档案（同一账套同一份数据），在本页不做重复维护：
 * 新增 / 导入 / 编辑统一回到工资模块，删除沿用员工接口（已有工资单的员工由后端拒绝）。
 * 「同步税局人员信息」和专项附加扣除取数走的是自然人电子税务局（个税），与企业电子税务局
 * 是两套站点、两套账号：未登录时弹出「个税申报登录」，登录后由服务器无头浏览器取回数据，
 * 再写进工资模块的员工档案——按证件号码（税局只回脱敏号，取不到时退到姓名）匹配，
 * 命中只覆盖税局给得到的列，没命中的新增，税局没有的字段一律用员工档案的默认值。
 * 同步后税局的报送状态与身份验证状态覆盖到本地名单上；仍未落到档案的人排在列表末尾并标「未登记」，
 * 行内给「登记到工资模块」入口。
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Card, Alert, App, Button, Checkbox, Dropdown, Input, Table, Tooltip } from "antd";
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
import {
    fetchIitDeductions,
    syncIitPersonnel,
    type TaxIitDeductionRow,
    type TaxIitPersonnelRow,
} from "@/lib/tax-iit";
import { cn } from "@/lib/utils";

import { IitLoginModal } from "./IitLoginModal";

type TabKey = "people" | "deduction";

/** 税局取数的页签状态；failed 是终态，否则「取不到就回 idle」会被自动加载的 effect 反复重发请求。 */
type FetchState = "idle" | "loading" | "loaded" | "failed";

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
    /** 税局名单里有、本账套工资模块还没登记的行：只读，不能改删。 */
    unregistered: boolean;
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

/** 税局与本地档案的证件号码大小写、空格不一致，比对前统一。 */
const idKey = (no: string) => no.trim().toUpperCase();

/** 税局回「报送成功」，本地列的筛选值用「已报送成功」，这里对齐口径。 */
function reportStateOf(remote: string): string {
    if (!remote.trim()) return "待报送";
    return remote.includes("成功") ? "已报送成功" : remote;
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
        unregistered: false,
    };
}

/** 税局名单里、工资模块还没有的人：税局只回脱敏证件号，姓名与编号照抄，状态取税路口径。 */
function toBureauRow(remote: TaxIitPersonnelRow): IitRow {
    return {
        key: `bureau:${remote.key}`,
        id: "",
        code: remote.code ?? "",
        name: remote.name ?? "",
        dept: remote.dept ?? "",
        idType: remote.idTypeText ?? "",
        idNo: remote.idNo ?? "",
        phone: remote.phone ?? "",
        employType: remote.employTypeText || "雇员",
        status: "",
        hireDate: remote.hireDate ?? "",
        reportStatus: reportStateOf(remote.reportStateText),
        verifyStatus: remote.verifyStateText || "未验证",
        gender: remote.genderText ?? "",
        email: "",
        remark: "",
        unregistered: true,
    };
}

/** 两个页签的表格共用一套分页文案。 */
const TABLE_PAGINATION = {
    pageSize: 20,
    pageSizeOptions: [20, 50, 100],
    showSizeChanger: true,
    showQuickJumper: true,
    showTotal: (total: number, [from, to]: [number, number]) =>
        total === 0 ? "显示 0 到 0，共 0 条记录" : `显示 ${from} 到 ${to}，共 ${total} 条记录`,
};

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
    /** 「个税申报登录」弹窗：自然人电子税务局与企业电子税务局是两套登录态。 */
    const [loginOpen, setLoginOpen] = useState(false);
    const [syncing, setSyncing] = useState(false);
    /** 税局侧已登记的人员，按证件号码覆盖到本地名单上。 */
    const [bureau, setBureau] = useState<TaxIitPersonnelRow[]>([]);
    const [deductions, setDeductions] = useState<TaxIitDeductionRow[]>([]);
    /** 扣除明细只在第一次切到该页签时取，之后靠查询按钮重取。 */
    const [deductionState, setDeductionState] = useState<FetchState>("idle");
    const [deductionError, setDeductionError] = useState<string | null>(null);
    const deductionsInFlight = useRef(false);

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

    const rows = useMemo(() => {
        // 税局名单回的是脱敏证件号（3412************97），和本地完整号对不上，
        // 所以先按证件号匹配、取不到再按姓名兜底；每条税局记录只认给一个人。
        const used = new Set<string>();
        const takeById = (no: string) =>
            no ? bureau.find((remote) => !used.has(remote.key) && idKey(remote.idNo) === no) : undefined;
        const takeByName = (name: string) =>
            name ? bureau.find((remote) => !used.has(remote.key) && remote.name === name) : undefined;

        const localRows = employees.map((employee) => {
            const local = toRow(employee);
            const matched = takeById(idKey(local.idNo)) ?? takeByName(local.name);
            if (!matched) return local;
            used.add(matched.key);
            // 只覆盖税局口径的两列：报送状态与身份验证状态。人员状态词表两边不同，不覆盖。
            return {
                ...local,
                reportStatus: reportStateOf(matched.reportStateText),
                verifyStatus: matched.verifyStateText || local.verifyStatus,
            };
        });

        return [...localRows, ...bureau.filter((remote) => !used.has(remote.key)).map((remote) => toBureauRow(remote))];
    }, [bureau, employees]);

    /** 取回个税「人员信息采集」名单并写进员工档案；未登录时弹出个税登录。 */
    const syncPersonnel = useCallback(async () => {
        if (!accountSetId || syncing) return;
        setSyncing(true);
        try {
            const outcome = await syncIitPersonnel(accountSetId, token);
            if (outcome.needLogin) {
                setLoginOpen(true);
                return;
            }
            if (outcome.rows.length === 0) {
                message.warning(outcome.message ?? "未从个税系统取回人员信息。");
                return;
            }
            setBureau(outcome.rows);
            // 名单已落到 employees 表，重新读一次本地档案，新增的人直接出现在原位而不是行尾。
            await reload();
            const sync = outcome.sync;
            message.success(
                sync
                    ? `已同步个税人员 ${outcome.rows.length} 人：新增 ${sync.imported} 人、更新 ${sync.updated} 人` +
                          (sync.skipped ? `，${sync.skipped} 条缺姓名未登记。` : "。")
                    : `已同步个税人员 ${outcome.rows.length} 人。`,
            );
        } catch (err) {
            message.error(await apiErrorMessage(err, "同步个税人员信息失败，请稍后重试。"));
        } finally {
            setSyncing(false);
        }
    }, [accountSetId, message, reload, syncing, token]);

    /** 专项附加扣除明细：同样按个税登录态取数。 */
    const loadDeductions = useCallback(async () => {
        if (!accountSetId || deductionsInFlight.current) return;
        // StrictMode 会把挂载 effect 跑两遍，挡住第二次，免得对税局重复发一次取数。
        deductionsInFlight.current = true;
        setDeductionState("loading");
        setDeductionError(null);
        try {
            const result = await fetchIitDeductions(accountSetId, token);
            if (result.needLogin) {
                setDeductionState("failed");
                setLoginOpen(true);
                return;
            }
            setDeductions(result.rows);
            setDeductionState("loaded");
            if (result.rows.length === 0) {
                setDeductionError(result.message ?? "未从个税系统取回专项附加扣除信息。");
            }
        } catch (err) {
            setDeductionState("failed");
            setDeductionError(await apiErrorMessage(err, "读取专项附加扣除信息失败。"));
        } finally {
            deductionsInFlight.current = false;
        }
    }, [accountSetId, token]);

    /** 进页签自动取一次；状态停在 idle 之外就不会重复触发（未登录时由取数函数弹登录框）。 */
    useEffect(() => {
        if (tab === "deduction" && deductionState === "idle") void loadDeductions();
    }, [deductionState, loadDeductions, tab]);

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

    /** 报送由个税系统侧完成，这里只能先把人引导去登录自然人电子税务局。 */
    function requireIitLogin(title: string, content: string) {
        modal.confirm({
            title,
            content,
            okText: "前往登录个税",
            cancelText: "取消",
            onOk: () => setLoginOpen(true),
        });
    }

    function openReport() {
        const count = selected.length > 0 ? selected.length : rows.length;
        requireIitLogin(
            "报送人员信息",
            `将向自然人电子税务局报送 ${count} 名人员信息。报送在个税系统侧完成，需先登录个税后在税局页面办理。`,
        );
    }

    const nameColumnFilter = {
        filterIcon: <FilterFilled style={{ color: nameKeyword ? "var(--brand)" : "var(--muted-foreground)" }} />,
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
        filterIcon: <FilterFilled style={{ color: phoneKeyword ? "var(--brand)" : "var(--muted-foreground)" }} />,
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
            {
                title: "姓名",
                dataIndex: "name",
                key: "name",
                width: 140,
                ...nameColumnFilter,
                render: (value: string, row) =>
                    row.unregistered ? (
                        <span className="inline-flex items-center gap-1.5">
                            {value}
                            <span className="rounded bg-(--warn-bg) px-1 text-[11px] leading-[18px] text-(--warn-icon)">未登记</span>
                        </span>
                    ) : (
                        value
                    ),
            },
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
                render: (value: string) => value || "—",
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
                        <Tooltip title="点「同步税局人员信息」后取个税系统的报送状态；未同步时按待报送显示。">
                            <QuestionCircleOutlined className="text-[11px] text-(--muted-foreground)" />
                        </Tooltip>
                    </span>
                ),
                dataIndex: "reportStatus",
                key: "reportStatus",
                width: 120,
                filters: [
                    { text: "待报送", value: "待报送" },
                    { text: "已报送成功", value: "已报送成功" },
                    { text: "报送失败", value: "报送失败" },
                    { text: "报送中", value: "报送中" },
                ],
                onFilter: (value, row) => row.reportStatus === value,
            },
            {
                title: (
                    <span className="inline-flex items-center gap-1">
                        验证状态
                        <Tooltip title="未同步时按居民身份证校验位本地校验；同步后取个税系统的身份验证状态。">
                            <QuestionCircleOutlined className="text-[11px] text-(--muted-foreground)" />
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
                    { text: "待验证", value: "待验证" },
                ],
                onFilter: (value, row) => row.verifyStatus === value,
                render: (value: string) => (
                    <span className="inline-flex items-center gap-1.5">
                        <span
                            className={cn(
                                "h-1.5 w-1.5 rounded-full",
                                value === "验证通过"
                                    ? "bg-(--success)"
                                    : value === "验证不通过"
                                        ? "bg-(--danger)"
                                        : "bg-(--muted)",
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
            render: (_, row) =>
                row.unregistered ? (
                    <Button
                        type="link"
                        title="税局名单里有这个人，但本账套工资模块还没有档案；去工资模块补一条即可（税局只回脱敏证件号，需手工填写）"
                        onClick={toSalaryEmployees}
                    >
                        登记到工资模块
                    </Button>
                ) : (
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

    /** 扣除项目 / 申报方式的筛选项从已取回的数据里生成，税局码表外的值也不会漏。 */
    const textFilters = (pick: (row: TaxIitDeductionRow) => string) => {
        const values = [...new Set(deductions.map(pick).filter(Boolean))].sort((a, b) => a.localeCompare(b, "zh-CN"));
        return {
            filters: values.map((value) => ({ text: value, value })),
            onFilter: (value: unknown, row: TaxIitDeductionRow) => pick(row) === value,
        };
    };

    const deductionColumns: TableColumnsType<TaxIitDeductionRow> = [
        { title: "姓名", dataIndex: "name", key: "name", width: 100 },
        { title: "证件类型", dataIndex: "idTypeText", key: "idTypeText", width: 110, render: (v: string) => v || "—" },
        { title: "证件号码", dataIndex: "idNo", key: "idNo", width: 190, render: (v: string) => v || "—" },
        {
            title: "扣除项目",
            dataIndex: "projectText",
            key: "projectText",
            width: 150,
            ...textFilters((row) => row.projectText),
        },
        { title: "扣除对象", dataIndex: "relativeText", key: "relativeText", width: 110, render: (v: string) => v || "—" },
        { title: "扣除年度", dataIndex: "yearText", key: "yearText", width: 90, render: (v: string) => v || "—" },
        { title: "有效期起", dataIndex: "periodStart", key: "periodStart", width: 120, render: (v: string) => v || "—" },
        { title: "有效期止", dataIndex: "periodEnd", key: "periodEnd", width: 120, render: (v: string) => v || "长期" },
        {
            title: "每月扣除金额",
            dataIndex: "monthlyAmount",
            key: "monthlyAmount",
            width: 120,
            align: "right",
            sorter: (a, b) => a.monthlyAmount - b.monthlyAmount,
            render: (value: number) => value.toLocaleString("zh-CN"),
        },
        {
            title: "申报方式",
            dataIndex: "declareWayText",
            key: "declareWayText",
            width: 180,
            ...textFilters((row) => row.declareWayText),
            render: (v: string) => v || "—",
        },
        { title: "验证状态", dataIndex: "verifyStateText", key: "verifyStateText", width: 110, render: (v: string) => v || "—" },
        { title: "报送状态", dataIndex: "reportStateText", key: "reportStateText", width: 110, render: (v: string) => v || "—" },
    ];

    return (
        <Card>
            <div className="flex shrink-0 flex-wrap items-center gap-4 border-b border-(--border)">
                <div className="flex items-center gap-6">
                    {TABS.map((item) => (
                        <button
                            key={item.key}
                            type="button"
                            onClick={() => {
                                // 上次没取到（多半是个税没登录）时，再进这个页签要再试一次、再弹一次登录框。
                                if (item.key !== "deduction" && deductionState === "failed") setDeductionState("idle");
                                setTab(item.key);
                            }}
                            className={cn(
                                "relative h-[46px] text-[13px] transition-colors",
                                tab === item.key
                                    ? "font-medium text-(--foreground)"
                                    : "text-(--muted-foreground) hover:text-(--foreground)",
                            )}
                        >
                            {item.label}
                            {tab === item.key && <span className="absolute inset-x-0 bottom-0 h-[2px] bg-(--brand-fill)" />}
                        </button>
                    ))}
                </div>
                {tab === "people" && (
                    <div className="ml-6 flex min-w-0 flex-1 items-center gap-2 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-1.5 text-[12.5px] leading-5 text-(--warn-text)">
                        <InfoCircleOutlined className="shrink-0 text-(--warn-icon)" />
                        <span>
                            人员信息列表数据与财务软件账套工资模块的员工信息数据为互通数据，新增或修改人员信息会同步更新工资模块的员工信息。
                        </span>
                    </div>
                )}
            </div>

            {tab === "deduction" ? (
                <div className="flex min-h-0 flex-1 flex-col">
                    <div className="flex shrink-0 flex-wrap items-center gap-3 pt-3" style={{ marginBottom: 16 }}>
                        <span className="text-[13px] text-(--muted-foreground)">
                            共 <span className="font-medium text-(--warn-icon)">{deductions.length}</span> 条扣除记录
                            {deductions.length > 0 && (
                                <span className="ml-2 text-(--muted-foreground)">
                                    每月合计 {deductions.reduce((sum, row) => sum + row.monthlyAmount, 0).toLocaleString("zh-CN")} 元
                                </span>
                            )}
                        </span>
                        <div className="ml-auto flex items-center gap-2">
                            <Button
                                type="primary"
                                loading={deductionState === "loading"}
                                onClick={() => void loadDeductions()}
                            >
                                同步专项附加扣除
                            </Button>
                            <Tooltip title="刷新">
                                <Button
                                    type="text"
                                    icon={<ReloadOutlined />}
                                    aria-label="刷新"
                                    onClick={() => void loadDeductions()}
                                />
                            </Tooltip>
                        </div>
                    </div>
                    <div className="min-h-0 flex-1 pb-3">
                        {deductionError && <Alert type="warning" showIcon title={deductionError} className="mb-3" />}
                        <Table<TaxIitDeductionRow>
                            columns={deductionColumns}
                            dataSource={deductions}
                            rowKey="key"
                            loading={deductionState === "loading"}
                            tableLayout="fixed"
                            scroll={{ x: 1620, y: "calc(100vh - 320px)" }}
                            pagination={TABLE_PAGINATION}
                            locale={{
                                emptyText: (
                                    <span className="block py-16 text-[13px] text-(--muted-foreground)">
                                        暂无扣除记录，点击「同步专项附加扣除」从个税系统取回
                                    </span>
                                ),
                            }}
                        />
                    </div>
                </div>
            ) : (
                <>
                    <div className="flex shrink-0 flex-wrap items-center gap-3 pt-3" style={{ marginBottom: 16 }}>
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
                            <Button>
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
                                    ? "font-medium"
                                    : undefined,
                            )}
                        >
                            在职：
                            <span className="font-medium text-(--warn-icon)">{activeCount}</span>人
                        </Button>
                        <Button
                            type="link"
                            onClick={() => setOnlyUnreported((prev) => !prev)}
                            className={cn(
                                onlyUnreported
                                    ? "font-medium"
                                    : undefined,
                            )}
                        >
                            未报送成功：
                            <span className="font-medium text-(--warn-icon)">{unreportedCount}</span>人
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
                                <Button loading={syncing} onClick={() => void syncPersonnel()}>
                                    同步税局人员信息
                                </Button>
                                <Tooltip title="从自然人电子税务局取回人员信息；需先登录个税。">
                                    <QuestionCircleOutlined className="text-[12px] text-(--muted-foreground)" />
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

                    <div className="min-h-0 flex-1 pb-3 [&_.ant-table-body]:min-h-[calc(100vh-320px)] [&_.ant-table-body]:!overflow-y-auto">
                        {error && (
                            <Alert type="error" showIcon title={error} className="mb-3" />
                        )}
                        <Table<IitRow>
                            columns={columns}
                            dataSource={visibleRows}
                            rowKey="key"
                            loading={loading}
                            tableLayout="fixed"
                            scroll={{ x: showAll ? 2080 : 1640, y: "calc(100vh - 320px)" }}
                            rowSelection={{
                                selectedRowKeys: selected,
                                onChange: (keys) => setSelected(keys.map(String)),
                                columnWidth: 40,
                                // 未登记行在本地没有员工档案，勾选后批量删除 / 报送都无从下手。
                                getCheckboxProps: (row) => ({ disabled: row.unregistered }),
                            }}
                            pagination={TABLE_PAGINATION}
                            locale={{
                                emptyText: (
                                    <span className="block py-16 text-[13px] text-(--muted-foreground)">
                                        暂无人员信息，请到工资模块新增员工
                                    </span>
                                ),
                            }}
                        />
                    </div>
                </>
            )}

            <IitLoginModal
                open={loginOpen}
                accountSetId={accountSetId}
                onClose={() => setLoginOpen(false)}
                onSuccess={() => {
                    setLoginOpen(false);
                    // 登录成功后立刻把当前页签缺的数据补上，省得用户再点一次同步。
                    void syncPersonnel();
                    if (tab === "deduction") void loadDeductions();
                }}
            />
        </Card>
    );
}
