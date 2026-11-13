/**
 * 一键取票弹窗（柠檬云 /Invoice/Fetch 的三步向导）。
 *
 * ① 确认公司信息：公司名称 / 统一社会信用代码 / 增值税种类 / 报税地区 / 开票时间区间；
 * ② 填写税局登录信息：与「一键登录税局」弹窗第二步同一套表单与登录状态机；
 * ③ 获取发票：把区间交给服务端的取票任务，轮询到结束后报出「取回 / 新增 / 跳过」。
 *
 * 取票是单向的：服务器在无头浏览器里登录电子税务局查「全量发票查询」，解析后写进发票池，
 * 不会向税局回传任何数据。发票落库由后端 TaxInvoiceSyncJobRunner 完成，
 * 重复同步按「账套 + 方向 + 发票号」跳过，不覆盖已入账的发票。
 * 第一步的资料点「下一步」时校验并写回账套，随后直接发起取票：
 * 服务器能用账套里保存的登录态就一路走到结果，取不回来才退回第二步补税局账号密码。
 */
import { useCallback, useEffect, useRef, useState } from "react";

import { Radio, Spin } from "antd";
import { CheckCircleFilled } from "@ant-design/icons";
import dayjs from "dayjs";
import { useNavigate } from "react-router";

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsApi } from "@/apis-gen";
import { FORM_CONTROL_W } from "@/components/page/styles";
import { apiConfig } from "@/lib/api-client";
import { fmtAmount } from "@/lib/format";
import {
    syncTaxInvoices,
    type TaxInvoiceSyncResult,
    type TaxInvoiceSyncSummary,
    type TaxPortalLoginResponse,
} from "@/lib/tax-portal";
import { TAX_REGIONS, toOptions } from "@/lib/tax-regions";
import { TAXPAYERS } from "@/pages/settings/account-sets/account-set-form-data";

import { Btn, DateInput, Dialog, HintBox, Select, Table, TextInput, Toast } from "./components/ui";
import type { Col } from "./components/ui";
import { AgreementRow, FieldRow, StepBar, TaxLoginStepFields } from "./components/TaxLoginStep";
import {
    DEFAULT_TAX_LOGIN_VALUES,
    useTaxPortalLogin,
    type TaxLoginValues,
} from "./components/useTaxPortalLogin";

const STEPS = ["确认公司信息", "填写税局登录信息", "获取发票"];

/** 开票日期区间上限：与柠檬云一致，最长 24 个月。 */
const MAX_RANGE_MONTHS = 24;

/** 一键取票落库时写入的来源，取票结果只统计这个来源的发票。 */
export const TAX_INVOICE_SOURCE = "一键取票";

const DAY = "YYYY-MM-DD";

/** 取票记录表格：与柠檬云同列，只是不列「发票状态」——税局全量发票查询没有按状态过滤，我们也没解析状态。 */
const SUMMARY_COLS: Col[] = [
    { key: "direction", title: "发票类型", width: "140px" },
    { key: "period", title: "开票日期", width: "230px" },
    { key: "count", title: "份数", align: "right", width: "90px" },
    { key: "amount", title: "不含税金额（元）", align: "right", width: "150px", neg: true },
    { key: "tax", title: "发票税额（元）", align: "right", width: "140px", neg: true },
    { key: "total", title: "价税合计（元）", align: "right", width: "140px", neg: true },
];

/** 金额一律列到两位（0 也写 0.00），与柠檬云的取票记录一致；红冲票为负数。 */
const summaryRow = (row: TaxInvoiceSyncSummary): Record<string, unknown> => ({
    direction: row.direction,
    period: `${row.periodStart} 至 ${row.periodEnd}`,
    count: String(row.count),
    amount: fmtAmount(row.amount, true),
    tax: fmtAmount(row.tax, true),
    total: fmtAmount(row.total, true),
});

const defaultRange = () => ({ start: dayjs().subtract(2, "year").format(DAY), end: dayjs().format(DAY) });

type SyncState = "idle" | "running" | "done" | "failed";

export function InvoiceFetchDialog({ onClose, onFetched }: { onClose: () => void; onFetched?: (result: TaxInvoiceSyncResult) => void }) {
    const { token } = useAuth();
    const accountSetId = useCurrentAccountSetId() ?? "";
    const { accountSets, refresh } = useAccountSets();
    const accountSet = accountSets.find((s) => s.id === accountSetId);
    const navigate = useNavigate();

    const [step, setStep] = useState<1 | 2 | 3>(1);
    const [agreed, setAgreed] = useState(false);
    const [companyName, setCompanyName] = useState("");
    const [taxpayerNumber, setTaxpayerNumber] = useState("");
    const [taxpayerType, setTaxpayerType] = useState<number>(TAXPAYERS[1].value);
    const [region, setRegion] = useState("");
    const [range, setRange] = useState(defaultRange);
    const [login, setLogin] = useState<TaxLoginValues>(DEFAULT_TAX_LOGIN_VALUES);
    const [toast, setToast] = useState<string | null>(null);
    const [helpOpen, setHelpOpen] = useState(false);

    const [syncState, setSyncState] = useState<SyncState>("idle");
    const [result, setResult] = useState<TaxInvoiceSyncResult | null>(null);
    const [syncError, setSyncError] = useState<string | null>(null);

    // 关闭或重新发起后，迟到的取票结果不再回写到界面。
    const syncRunRef = useRef(0);
    const prefilledRef = useRef(false);

    /** 打开时按当前账套预填，用户不必每次从头填。账套列表可能晚于弹窗到达，所以只填一次。 */
    useEffect(() => {
        if (prefilledRef.current || !accountSet) return;
        prefilledRef.current = true;
        setCompanyName(accountSet.companyName ?? "");
        setTaxpayerNumber(accountSet.taxpayerNumber ?? "");
        setTaxpayerType(accountSet.taxpayerType ?? TAXPAYERS[1].value);
        setRegion(accountSet.taxRegion ?? "");
        setLogin({
            loginType: accountSet.taxLoginType ?? DEFAULT_TAX_LOGIN_VALUES.loginType,
            identityType: accountSet.taxIdentityType ?? DEFAULT_TAX_LOGIN_VALUES.identityType,
            account: accountSet.taxLoginAccount ?? "",
            password: "",
            passwordSaved: accountSet.taxLoginPasswordSet ?? false,
        });
    }, [accountSet]);

    /**
     * 把第一步与第二步的税局资料写回账套。后端更新接口里可空字段「不传即保持原值」，
     * 所以只回提交本弹窗负责的这几项，税率与附加税费等留在【设置-账套】里维护。
     */
    const saveTaxInfo = useCallback(async () => {
        if (!accountSetId) return;
        const api = new AccountSetsApi(apiConfig(token));
        const full = await api.accountSetsIdGet({ id: accountSetId });
        await api.accountSetsIdPut({
            id: accountSetId,
            updateAccountSetRequest: {
                name: full.name,
                companyName: companyName.trim(),
                standard: full.standard,
                taxpayerType: taxpayerType,
                baseCurrency: full.baseCurrency,
                taxpayerNumber: taxpayerNumber.trim(),
                taxRegion: region,
                taxLoginType: login.loginType,
                taxIdentityType: login.identityType,
                taxLoginAccount: login.account.trim(),
                // 留空表示沿用已存密码；后端把 null 与空串区分对待。
                taxLoginPassword: login.password.trim() || undefined,
            },
        });
    }, [accountSetId, companyName, login.account, login.identityType, login.loginType, login.password, region, taxpayerNumber, taxpayerType, token]);

    /** 登录成功即自动进入第三步取票，其余状态交给第二步自己展示。 */
    const onLoginEvent = useCallback((response: TaxPortalLoginResponse, changed: boolean) => {
        if (response.state === "success") {
            setStep(3);
            return;
        }
        if (changed && response.state === "sms_required") {
            setToast(response.message || "验证码已发送，请查收短信");
        }
        if (changed && response.state === "captcha_required") {
            setToast("税局要求完成验证码，请按顺序点选图中文字");
        }
    }, []);

    const validateLogin = useCallback((): string | null => {
        if (!accountSetId) return "未识别当前账套，请从侧边栏重新选择账套后再试";
        if (!taxpayerNumber.trim() || !region) return "请先补全统一社会信用代码与报税地区";
        if (!login.account.trim()) return "请填写税局实名账号";
        if (!login.password.trim() && !login.passwordSaved) return "请填写税局实名密码";
        return null;
    }, [accountSetId, login.account, login.password, login.passwordSaved, region, taxpayerNumber]);

    const flow = useTaxPortalLogin({
        accountSetId,
        token,
        active: step === 2,
        validate: validateLogin,
        prepare: async () => {
            await saveTaxInfo();
            refresh();
        },
        onEvent: onLoginEvent,
    });

    /** 第三步：把区间交给服务端取票任务，轮询到任务结束。 */
    const runSync = useCallback(async () => {
        if (!accountSetId) {
            setSyncError("未识别当前账套，请从侧边栏重新选择账套后再试。");
            setSyncState("failed");
            return;
        }
        const run = ++syncRunRef.current;
        setSyncState("running");
        setSyncError(null);
        setResult(null);
        try {
            const job = await syncTaxInvoices(accountSetId, token, range.start, range.end);
            if (syncRunRef.current !== run) return;
            if (job.needLogin) {
                // 登录态在取票途中失效：退回第二步重新登录，登录后会自动再来一次。
                setSyncState("idle");
                setStep(2);
                setToast("税局登录状态已失效，请重新登录后再取票。");
                return;
            }
            const data = job.data ?? null;
            if (job.status !== "success") {
                // 失败时服务器仍带回诊断信息（走到哪一步、抓到什么响应），一并显示出来。
                setResult(data);
                setSyncState("failed");
                setSyncError(job.message || data?.message || "一键取票失败，请稍后重试。");
                return;
            }
            setResult(data);
            setSyncState("done");
            if (data) onFetched?.(data);
        } catch (err) {
            if (syncRunRef.current !== run) return;
            setSyncState("failed");
            setSyncError(err instanceof Error && err.message ? err.message : "一键取票失败，请稍后重试。");
        }
    }, [accountSetId, onFetched, range.end, range.start, token]);

    // 进入第三步自动开始取票；失败后由「重试」按钮再走同一条路径。
    useEffect(() => {
        if (step === 3 && syncState === "idle") void runSync();
    }, [runSync, step, syncState]);

    const runFlow = useCallback((action: () => Promise<string | null>) => {
        void action().then((blocked) => {
            if (blocked) setToast(blocked);
        });
    }, []);

    /** 第一步「下一步」：校验并保存资料后直接发起取票；服务器会用账套里的登录态恢复，取不回来才退回第二步登录。 */
    const handleNext = useCallback(async () => {
        if (!agreed) {
            setToast("请先阅读并同意《税局登录业务隐私政策》");
            return;
        }
        if (!companyName.trim()) {
            setToast("请填写公司名称");
            return;
        }
        if (!taxpayerNumber.trim()) {
            setToast("请填写统一社会信用代码，服务器需要它才能确定要取哪家企业的发票");
            return;
        }
        if (!region) {
            setToast("请选择报税地区，服务器需要它才能打开对应省份的电子税务局");
            return;
        }
        if (!range.start || !range.end) {
            setToast("请选择开票时间区间");
            return;
        }
        if (dayjs(range.end).isBefore(dayjs(range.start))) {
            setToast("开票时间的截止日期不能早于起始日期");
            return;
        }
        if (dayjs(range.start).add(MAX_RANGE_MONTHS, "month").isBefore(dayjs(range.end))) {
            setToast(`一键取票的开票日期最长不能超过${MAX_RANGE_MONTHS}个月，请缩短区间`);
            return;
        }
        try {
            await saveTaxInfo();
            refresh();
        } catch (err) {
            setToast(err instanceof Error && err.message ? err.message : "保存税务信息失败，请重试。");
            return;
        }
        setStep(3);
    }, [agreed, companyName, range.end, range.start, refresh, region, saveTaxInfo, taxpayerNumber]);

    const close = useCallback(() => {
        syncRunRef.current++;
        onClose();
    }, [onClose]);

    return (
        <>
            <Dialog
                title="一键取票"
                width={980}
                onClose={close}
                footer={
                    <div className="flex justify-center gap-3">
                        {step === 1 ? (
                            <>
                                <Btn onClick={close}>取消</Btn>
                                <Btn variant="primary" onClick={() => void handleNext()}>
                                    下一步
                                </Btn>
                            </>
                        ) : step === 2 ? (
                            <>
                                <Btn onClick={() => setStep(1)} disabled={flow.busy}>
                                    上一步
                                </Btn>
                                {flow.captchaRequired ? (
                                    <Btn
                                        variant="primary"
                                        disabled={flow.busy || flow.captchaClicks.length === 0}
                                        onClick={() => runFlow(flow.submitCaptchaClicks)}
                                    >
                                        {flow.busyKind === "verifying" ? "验证中…" : "提交验证"}
                                    </Btn>
                                ) : flow.smsRequired ? (
                                    <Btn variant="primary" disabled={flow.busy} onClick={() => runFlow(flow.submitSmsCode)}>
                                        {flow.busyKind === "verifying" ? "登录中…" : "提交验证码"}
                                    </Btn>
                                ) : (
                                    <Btn variant="primary" disabled={flow.busy} onClick={() => runFlow(flow.startLogin)}>
                                        {flow.busy ? "登录中…" : "登录税局"}
                                    </Btn>
                                )}
                            </>
                        ) : (
                            <>
                                {syncState === "running" ? (
                                    <Btn variant="primary" onClick={close}>
                                        知道了
                                    </Btn>
                                ) : (
                                    <>
                                        <Btn onClick={close}>关闭</Btn>
                                        {syncState === "failed" && (
                                            <Btn variant="primary" onClick={() => void runSync()}>
                                                重试
                                            </Btn>
                                        )}
                                        {syncState === "done" && (
                                            <Btn variant="primary" onClick={() => { close(); navigate(`/${accountSetId}/invoices/fetch`); }}>
                                                去看看
                                            </Btn>
                                        )}
                                    </>
                                )}
                            </>
                        )}
                    </div>
                }
            >
                <div className="rounded-xl bg-(--table-zebra-bg) p-4 text-[13px] leading-6">
                    <div className="flex items-start gap-2">
                        <span className="text-(--warn-text)">⚠</span>
                        <div className="text-(--foreground)">
                            <div>
                                1.一键取票仅仅【单向】从税局获取发票数据（含商品明细及数电发票附件），并不会向税局传送任何数据
                                <span
                                    className="ml-2 cursor-pointer text-(--brand)"
                                    title="数电发票原件（PDF / XML）下载尚未接入取票接口，当前取回的是发票数据"
                                    onClick={() => setToast("数电发票原件（PDF / XML）下载尚未接入取票接口，当前取回的是发票数据与商品明细。")}
                                >
                                    ⚙ 设置原件格式
                                </span>
                            </div>
                            <div>
                                2.
                                <span className="text-(--danger)">首次使用或税务信息填写不准确</span>
                                ，均需要认证税局登录账号后，才能使用一键取票功能；税务信息填写完成后，可前往
                                <span
                                    className="cursor-pointer text-(--brand)"
                                    onClick={() => {
                                        close();
                                        navigate(`/${accountSetId}/settings/account-sets`);
                                    }}
                                >
                                    【设置-账套-税务信息】
                                </span>
                                中再次查看和修改
                            </div>
                            <div>
                                3.
                                <span className="text-(--danger)">系统获取发票过程中请勿登录电子税务局</span>
                                ，同一账号在别处登录会把服务器上的税局会话挤掉、导致取票中断；一键取票的开票日期最长不能超过{MAX_RANGE_MONTHS}个月，如您在使用过程中有任何疑问，请查看{" "}
                                <button
                                    type="button"
                                    onClick={() => setHelpOpen(true)}
                                    className="cursor-pointer text-(--brand) hover:underline"
                                >
                                    帮助文档
                                </button>{" "}
                                或联系
                                <button
                                    type="button"
                                    onClick={() => setHelpOpen(true)}
                                    className="ml-1 inline-flex h-5 items-center rounded bg-(--brand-fill) px-1.5 align-middle text-[12px] text-white"
                                >
                                    在线客服
                                </button>
                            </div>
                        </div>
                    </div>
                </div>

                <StepBar steps={STEPS} current={step} />

                {step === 1 ? (
                    <div className="mt-5 flex flex-col gap-4">
                        {!taxpayerNumber.trim() && (
                            <HintBox>当前未填写统一社会信用代码，服务器无法确定要取哪家企业的发票，请补全后继续。</HintBox>
                        )}
                        <FieldRow label="公司名称" required hint="默认取自当前账套，保存时一并写回账套">
                            <TextInput className={FORM_CONTROL_W} value={companyName} onChange={setCompanyName} placeholder="营业执照上的公司全称" />
                        </FieldRow>
                        <FieldRow label="统一社会信用代码" required>
                            <TextInput className={FORM_CONTROL_W} value={taxpayerNumber} onChange={setTaxpayerNumber} placeholder="请输入统一社会信用代码" />
                        </FieldRow>
                        <FieldRow label="增值税种类" required hint="决定取票后按哪套申报口径统计，与账套的纳税人性质同步">
                            <Radio.Group
                                options={TAXPAYERS.map((t) => ({ value: t.value, label: t.label }))}
                                value={taxpayerType}
                                onChange={(e) => setTaxpayerType(e.target.value as number)}
                            />
                        </FieldRow>
                        <FieldRow label="报税地区" required hint="决定服务器打开哪个省的电子税务局">
                            <Select className={FORM_CONTROL_W} value={region} options={toOptions(TAX_REGIONS)} onChange={setRegion} />
                        </FieldRow>
                        <FieldRow label="开票时间" required>
                            <span className="flex items-center gap-2">
                                <DateInput className={FORM_CONTROL_W} value={range.start} onChange={(start) => setRange((prev) => ({ ...prev, start }))} />
                                <span className="shrink-0 text-[13px] text-(--foreground)">至</span>
                                <DateInput className={FORM_CONTROL_W} value={range.end} onChange={(end) => setRange((prev) => ({ ...prev, end }))} />
                            </span>
                        </FieldRow>
                        <AgreementRow
                            checked={agreed}
                            onChange={setAgreed}
                            label={<>我已阅读并同意 <span className="text-(--brand)">《税局登录业务隐私政策》</span></>}
                        />
                    </div>
                ) : step === 2 ? (
                    <TaxLoginStepFields
                        flow={flow}
                        values={login}
                        onChange={(patch) => setLogin((prev) => ({ ...prev, ...patch }))}
                        onResend={() => runFlow(flow.startLogin)}
                        hint={
                            <HintBox>
                                点「登录税局」后，服务器会打开电子税务局并自动填写、提交登录，登录成功后直接进入取票；
                                只有税局真的发来短信时，这里才会出现验证码输入框。
                                账号密码加密保存在当前账套，登录态只留在服务器，取票不会向税局回传任何数据。
                            </HintBox>
                        }
                    />
                ) : (
                    <div className="mt-5 flex flex-col gap-4">
                        <div className="text-[13px] text-(--foreground)">
                            开票时间：{range.start} 至 {range.end}（按税局「全量发票查询」的开票日期取数）
                        </div>
                        {syncState === "running" ? (
                            <div className="flex flex-col items-center gap-4 py-10 text-center">
                                <div className="flex items-center gap-3 text-[16px] font-semibold text-(--foreground)">
                                    <Spin />
                                    系统正在连接税局获取发票，可能需要几分钟…
                                </div>
                                <div className="max-w-[640px] text-[13px] leading-6 text-(--muted-foreground)">
                                    取票任务在服务器上执行，无需停留在当前页面，关掉本页也不影响结果；
                                    进度与结果可在【发票 - 一键取票】页的「最近取票任务」里查看。
                                </div>
                            </div>
                        ) : syncState === "done" && result ? (
                            <>
                                <div className="flex items-start gap-2 text-[13px] leading-6 text-(--foreground)">
                                    <CheckCircleFilled
                                        className="mt-1 shrink-0 text-[15px]"
                                        style={{ color: "var(--success)" }}
                                    />
                                    <span>
                                        进销项发票数据获取成功：取回
                                        <b className="mx-1 tabular-nums text-(--brand)">{result.fetched}</b>
                                        张，新增
                                        <b className="mx-1 tabular-nums text-(--success)">{result.imported}</b>
                                        张，已存在跳过
                                        <b className="mx-1 tabular-nums">{result.skipped}</b>
                                        张。
                                    </span>
                                </div>
                                <div className="flex flex-col gap-1 text-[13px] leading-6 text-(--foreground)">
                                    <div>
                                        <b>本次新增的发票：</b>
                                        写入【销项发票 / 进项发票】列表，来源标记「{TAX_INVOICE_SOURCE}」，进项发票勾选后可批量认证并生成凭证。
                                    </div>
                                    <div>
                                        <b>税局已返回、发票池里已存在的发票：</b>
                                        只跳过、不覆盖，已有的认证状态、入账结果与凭证关联保持不变。
                                    </div>
                                </div>
                                {result.summary && result.summary.length > 0 && (
                                    <Table cols={SUMMARY_COLS} rows={result.summary.map(summaryRow)} />
                                )}
                                {result.fetched === 0 && (
                                    <HintBox>税局在该区间没有返回发票，发票池保持原样。</HintBox>
                                )}
                                {result.warnings && result.warnings.length > 0 && (
                                    <HintBox>{result.warnings.join("；")}</HintBox>
                                )}
                            </>
                        ) : syncState === "failed" ? (
                            <>
                                <div className="rounded-lg bg-(--table-neg-bg) px-3 py-2 text-[12.5px] leading-5 text-(--danger)">
                                    {syncError || "一键取票失败，请稍后重试。"}
                                </div>
                                {result?.diagnostics && result.diagnostics.length > 0 && (
                                    <div className="rounded-lg bg-(--table-zebra-bg) px-3 py-2 text-[12px] leading-5 text-(--muted-foreground)">
                                        {result.diagnostics.slice(0, 5).map((line) => (
                                            <div key={line}>{line}</div>
                                        ))}
                                    </div>
                                )}
                            </>
                        ) : (
                            <div className="py-8 text-center text-[13px] text-(--muted-foreground)">正在准备取票…</div>
                        )}
                    </div>
                )}
            </Dialog>
            {helpOpen && (
                <Dialog
                    title="一键取票 · 使用说明"
                    width={560}
                    onClose={() => setHelpOpen(false)}
                    footer={
                        <Btn variant="primary" onClick={() => setHelpOpen(false)}>
                            我知道了
                        </Btn>
                    }
                >
                    <ol className="m-0 flex list-decimal flex-col gap-2 pl-5 text-[13px] leading-6 text-(--foreground)">
                        <li>确认公司名称、统一社会信用代码、增值税种类、报税地区与开票时间区间；资料保存后同时写回账套。</li>
                        <li>服务器使用账套里保存的税局登录态取票；登录失效时才退回第二步补账号密码或短信验证码。</li>
                        <li>服务器登录电子税务局查询全量发票，解析后写入发票池，结果按「取回 / 新增 / 跳过」汇总。</li>
                        <li>取回后到「销项发票」「进项发票」勾选发票生成凭证，已入账的发票不会被重复覆盖。</li>
                    </ol>
                    <div className="mt-4 flex flex-col gap-1 rounded-lg bg-(--table-zebra-bg) px-3 py-2 text-[12px] leading-5 text-(--muted-foreground)">
                        <span>• 取票是单向的：只从税局读取发票，不会向税局写入或回传数据。</span>
                        <span>• 取票依赖服务器上的税局登录态；登录失效会退回第二步补账号密码。</span>
                        <span>• 开票日期区间最长 {MAX_RANGE_MONTHS} 个月，重复取票按「账套 + 方向 + 发票号」跳过，不覆盖已入账发票。</span>
                        <span>• 数电发票原件（PDF / XML）下载尚未接入，当前取回的是发票数据与商品明细。</span>
                        <span>• 客服服务时间：周一至周日 8:30-12:00、13:30-20:30。</span>
                    </div>
                </Dialog>
            )}
            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </>
    );
}
