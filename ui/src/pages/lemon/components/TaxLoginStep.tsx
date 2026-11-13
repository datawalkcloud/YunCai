/**
 * 税局登录步骤的共享界面：表单行、步骤条，以及「填写税局登录信息」整页。
 *
 * 「一键登录税局」页与「一键取票」弹窗第二步画的是同一套东西（登录方式 / 身份类型 /
 * 实名账号密码 + 点选验证码 + 短信验证码 + 进展提示），状态由 useTaxPortalLogin 管，
 * 这里只负责渲染，两处共用一份版式。
 */
import type { ReactNode } from "react";

import { Input } from "antd";

import { FORM_CONTROL_W } from "@/components/page/styles";
import { TAX_IDENTITY_TYPES, TAX_LOGIN_TYPES, toOptions } from "@/lib/tax-regions";

import type { TaxLoginFlow, TaxLoginValues } from "./useTaxPortalLogin";
import { Btn, CheckBox, HintBox, Select, TextInput } from "./ui";

/** 表单行：左标签右控件，与柠檬云弹窗的对齐方式一致。 */
export function FieldRow({
    label,
    required,
    children,
    hint,
    labelWidth = "w-[170px]",
}: {
    label: string;
    required?: boolean;
    children: ReactNode;
    hint?: string;
    labelWidth?: string;
}) {
    return (
        <div className="flex items-center">
            <span className={`${labelWidth} shrink-0 text-right text-[13px] text-(--foreground)`}>
                {required && <span className="text-(--danger)">* </span>}
                {label}：
            </span>
            <div className="min-w-0 flex-1">{children}</div>
            {hint && <span className="ml-2 shrink-0 text-[12px] text-(--muted-foreground)">{hint}</span>}
        </div>
    );
}

/** 步骤条：已走过与当前这一步实心主色，未到的步骤灰圈。current 从 1 开始。 */
export function StepBar({ steps, current }: { steps: string[]; current: number }) {
    return (
        <div className="mt-5 flex items-center gap-3 text-[13px]">
            {steps.map((text, i) => {
                const no = i + 1;
                const done = no < current;
                const active = no === current;
                const tone = done || active ? "bg-(--brand-fill) text-white" : "border border-(--border) text-(--muted-foreground)";
                return (
                    <div key={text} className="flex items-center gap-3">
                        {i > 0 && <span className="h-px w-16 bg-(--muted)" />}
                        <span className="flex items-center gap-1.5">
                            <span className={`flex h-6 w-6 items-center justify-center rounded-full text-[12px] ${tone}`}>{no}</span>
                            <span className={active || done ? "text-(--brand)" : "text-(--muted-foreground)"}>{text}</span>
                        </span>
                    </div>
                );
            })}
        </div>
    );
}

/** 第二步「填写税局登录信息」的整页内容：资料字段 + 验证码 + 短信 + 进展说明。 */
export function TaxLoginStepFields({
    flow,
    values,
    onChange,
    onResend,
    hint,
}: {
    flow: TaxLoginFlow;
    values: TaxLoginValues;
    onChange: (patch: Partial<TaxLoginValues>) => void;
    /** 短信倒计时结束后重发验证码。 */
    onResend: () => void;
    hint?: ReactNode;
}) {
    return (
        <div className="mt-5 flex flex-col gap-4">
            <div className="flex flex-wrap items-center gap-x-8 gap-y-4">
                <FieldRow label="登录方式" required>
                    <Select
                        className={FORM_CONTROL_W}
                        value={values.loginType}
                        options={toOptions(TAX_LOGIN_TYPES)}
                        onChange={(loginType) => onChange({ loginType })}
                    />
                </FieldRow>
                <FieldRow label="身份类型" required>
                    <Select
                        className={FORM_CONTROL_W}
                        value={values.identityType}
                        options={toOptions(TAX_IDENTITY_TYPES)}
                        onChange={(identityType) => onChange({ identityType })}
                    />
                </FieldRow>
            </div>
            <div className="flex flex-wrap items-center gap-x-8 gap-y-4">
                <FieldRow label="税局实名账号" required>
                    <TextInput
                        className={FORM_CONTROL_W}
                        value={values.account}
                        onChange={(account) => onChange({ account })}
                        placeholder="手机号或身份证号"
                    />
                </FieldRow>
                <FieldRow label="税局实名密码" required>
                    <Input.Password
                        className={FORM_CONTROL_W}
                        value={values.password}
                        autoComplete="new-password"
                        placeholder={values.passwordSaved ? "已保存，留空表示不修改" : "税局登录密码"}
                        onChange={(e) => onChange({ password: e.target.value })}
                    />
                </FieldRow>
            </div>

            {flow.captchaRequired && flow.captcha && (
                <div className="rounded-xl border border-(--border) bg-(--card) p-3">
                    <div className="flex flex-wrap items-center gap-2 text-[13px] text-(--foreground)">
                        <span>请依次点击：</span>
                        {flow.captcha.promptImage && (
                            <img src={flow.captcha.promptImage} alt="验证码提示字" className="h-14 select-none" draggable={false} />
                        )}
                        <span className="ml-auto text-[12px] text-(--muted-foreground)">按提示顺序点击下方图中文字</span>
                    </div>
                    <div
                        className="relative mx-auto mt-2 cursor-crosshair select-none"
                        style={{
                            width: `min(100%, ${Math.round((flow.captcha.width || 300) * 1.6)}px)`,
                            aspectRatio: `${flow.captcha.width || 300} / ${flow.captcha.height || 180}`,
                        }}
                    >
                        <img
                            src={flow.captcha.image}
                            alt="验证码底图"
                            className="h-full w-full cursor-crosshair rounded"
                            draggable={false}
                            onClick={flow.onCaptchaImageClick}
                        />
                        {flow.captchaClicks.map((point, index) => (
                            <span
                                key={`${index}-${point.x.toFixed(4)}-${point.y.toFixed(4)}`}
                                className="pointer-events-none absolute flex h-5 w-5 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full bg-(--brand-fill) text-[11px] text-white shadow"
                                style={{ left: `${point.x * 100}%`, top: `${point.y * 100}%` }}
                            >
                                {index + 1}
                            </span>
                        ))}
                    </div>
                    <div className="mt-2 flex items-center gap-2 text-[12px] text-(--muted-foreground)">
                        <span>已点选 {flow.captchaClicks.length} 个，点错了可以清空重选。</span>
                        <Btn disabled={flow.captchaClicks.length === 0} onClick={flow.clearCaptchaClicks}>
                            清空
                        </Btn>
                    </div>
                </div>
            )}

            {flow.smsRequired && (
                <FieldRow label="短信验证码" required>
                    <span className="flex items-center gap-2">
                        <TextInput className="w-[170px]" value={flow.smsCode} onChange={flow.setSmsCode} placeholder="税局短信验证码" />
                        <Btn disabled={flow.busy || flow.countdown > 0} onClick={onResend}>
                            {flow.countdown > 0 ? `${flow.countdown}s 后重发` : "重新获取"}
                        </Btn>
                    </span>
                </FieldRow>
            )}

            {flow.flowMessage && (
                <div
                    className={`rounded-lg px-3 py-2 text-[12.5px] leading-5 ${
                        flow.flowState === "failed" ? "bg-(--table-neg-bg) text-(--danger)" : "bg-(--brand-soft) text-(--brand)"
                    }`}
                >
                    {flow.flowMessage}
                </div>
            )}

            {hint ?? (
                <HintBox>
                    点「登录税局」后，服务器会打开电子税务局并自动填写、提交登录；只有税局真的发来短信时，这里才会出现验证码输入框。
                    账号密码加密保存在当前账套，登录态只留在服务器。
                </HintBox>
            )}
        </div>
    );
}

/** 复选框行：与柠檬云弹窗里「我已阅读并同意」一行的缩进一致。 */
export function AgreementRow({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: ReactNode }) {
    return (
        <div className="flex items-center">
            <span className="w-[170px] shrink-0" />
            <CheckBox checked={checked} onChange={onChange} label={label} />
        </div>
    );
}
