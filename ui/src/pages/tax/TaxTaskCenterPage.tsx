/**
 * 智能报税 · 任务中心。
 *
 * 台账来自后端 tax_tasks：服务器每向税局办一次事（查税费种、取申报记录、同步个税人员与专项附加扣除）
 * 就落一行，按提交时间区间倒序查询。个税取数跑在请求之外，先落「进行中」再回填结果，
 * 所以这里遇到进行中的任务会自动刷新。失败行的「解决建议」就是服务器当时从税局拿到的答复，
 * 排查取数问题先看这一列。
 */
import { useCallback, useEffect, useState } from "react";
import { Card, Button, DatePicker, Table } from "antd";
import {
    CheckCircleFilled,
    CloseCircleFilled,
    ExclamationCircleFilled,
    LoadingOutlined,
    ReloadOutlined,
} from "@ant-design/icons";
import type { TableColumnsType } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import { useParams } from "react-router";

import { useAuth } from "@/auth/AuthContext";
import { EMPTY_CELL } from "@/lib/tax-format";
import { fetchTaxTasks, type TaxTaskRow } from "@/lib/tax-portal";

/** 默认看最近一个月：台账只增不删，区间拉太长会把当期问题淹在历史记录里。 */
const DEFAULT_DAYS = 30;

const DATE_FORMAT = "YYYY-MM-DD";
const TIME_FORMAT = "YYYY-MM-DD HH:mm:ss";

/** 税款所属期 yyyyMM → 2026-10。 */
function formatPeriod(period: number): string {
    if (!period) return EMPTY_CELL;
    const text = String(period);
    return text.length === 6 ? `${text.slice(0, 4)}-${text.slice(4)}` : text;
}

function formatTime(value: string): string {
    const time = dayjs(value);
    return time.isValid() ? time.format(TIME_FORMAT) : EMPTY_CELL;
}

const TEXT_DASH = <span className="text-(--muted-foreground)">{EMPTY_CELL}</span>;

const columns: TableColumnsType<TaxTaskRow> = [
    { title: "税款所属期", key: "period", width: 120, render: (_: unknown, row) => formatPeriod(row.period) },
    { title: "任务说明", dataIndex: "title", key: "title", width: 280 },
    {
        title: "任务状态",
        key: "status",
        width: 120,
        render: (_: unknown, row) =>
            row.status === "running" ? (
                <span className="inline-flex items-center gap-1.5 text-(--brand)">
                    <LoadingOutlined spin />进行中
                </span>
            ) : row.status === "success" ? (
                <span className="inline-flex items-center gap-1.5 text-(--success)">
                    <CheckCircleFilled />成功
                </span>
            ) : (
                <span className="inline-flex items-center gap-1.5 text-(--danger)">
                    <CloseCircleFilled />失败
                </span>
            ),
    },
    {
        title: "解决建议",
        key: "suggestion",
        render: (_: unknown, row) => (row.suggestion ? <span className="text-(--warn-text)">{row.suggestion}</span> : TEXT_DASH),
    },
    { title: "任务提交时间", key: "submittedAt", width: 180, render: (_: unknown, row) => formatTime(row.submittedAt) },
    {
        title: "任务结束时间",
        key: "finishedAt",
        width: 180,
        // 进行中的任务还没结束，后端回的是占位时间，这里显示「—」。
        render: (_: unknown, row) => (row.status === "running" ? TEXT_DASH : formatTime(row.finishedAt)),
    },
    { title: "操作人", dataIndex: "operator", key: "operator", width: 120 },
];

export function TaxTaskCenterPage() {
    const { accountSetId = "" } = useParams();
    const { token } = useAuth();
    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [dayjs().subtract(DEFAULT_DAYS - 1, "day"), dayjs()]);
    const [rows, setRows] = useState<TaxTaskRow[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const reload = useCallback(async (silent = false) => {
        if (!accountSetId) return;
        if (!silent) setLoading(true);
        try {
            const result = await fetchTaxTasks(
                accountSetId,
                token,
                range[0].format(DATE_FORMAT),
                range[1].format(DATE_FORMAT),
            );
            setRows(result.rows ?? []);
            setError(null);
        } catch (err) {
            setError(err instanceof Error ? err.message : "读取任务列表失败，请稍后重试。");
        } finally {
            if (!silent) setLoading(false);
        }
    }, [accountSetId, range, token]);

    useEffect(() => {
        void reload();
    }, [reload]);

    /** 个税取数跑在后台、结束时回填状态，所以区间里只要还有进行中的任务就每 5 秒静默刷一次。 */
    const hasRunning = rows.some((row) => row.status === "running");
    useEffect(() => {
        if (!hasRunning) return;
        const timer = setInterval(() => void reload(true), 5000);
        return () => clearInterval(timer);
    }, [hasRunning, reload]);

    return (
        <Card>
            <div className="flex shrink-0 flex-wrap items-center gap-3 text-[13px]">
                <span className="text-(--muted-foreground)">提交时间：</span>
                <DatePicker.RangePicker
                    allowClear={false}
                    className="w-[252px]"
                    value={range}
                    format={DATE_FORMAT}
                    onChange={(value) => value && value[0] && value[1] && setRange([value[0], value[1]])}
                />
                <Button icon={<ReloadOutlined />} loading={loading} onClick={() => void reload()}>
                    刷新
                </Button>
                <div className="ml-auto flex items-center gap-2 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-1.5 text-[12.5px] text-(--warn-text)">
                    <ExclamationCircleFilled />
                    <span>如对任务结果有疑问，请先按「解决建议」处理，仍解决不了请联系技术支持。</span>
                </div>
            </div>

            <div className="min-h-0 flex-1 pb-3 pt-3">
                {error && (
                    <div className="mb-3 rounded-md border border-(--warn-border) bg-(--warn-bg) px-3 py-1.5 text-[12.5px] text-(--warn-text)">
                        {error}
                    </div>
                )}
                <Table<TaxTaskRow>
                    columns={columns}
                    dataSource={rows}
                    rowKey="key"
                    loading={loading}
                    scroll={{ x: 1120 }}
                    pagination={{
                        pageSize: 20,
                        showSizeChanger: true,
                        showTotal: (total) => `共 ${total} 条记录`,
                    }}
                    locale={{
                        emptyText: (
                            <span className="block py-16 text-[13px] text-(--muted-foreground)">
                                {loading ? "正在读取任务列表…" : "该区间还没有税局任务记录"}
                            </span>
                        ),
                    }}
                />
                <div className="mt-2 text-[12px] text-(--muted-foreground)">
                    任务由服务器在税局侧执行，只登记实际办过的操作，不会预生成。
                </div>
            </div>
        </Card>
    );
}
