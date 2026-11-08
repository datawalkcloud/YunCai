/**
 * 智能报税 · 公司信息弹窗（工作台顶栏「详情」）。
 *
 * 版式对齐电子税务局的「公司信息」对话框：工商/税务登记资料只读展示，
 * 「是否出口企业」「总分支机构类型」可改。税局取数尚未接入，登记注册类型与
 * 税务登记日期先留空占位；两个可选项与「报税设置」一样按账套保存在本地。
 */
import { useEffect, useState, type ReactNode } from "react";

import { App, Button, Input, Modal, Select, Tooltip } from "antd";
import { QuestionCircleOutlined } from "@ant-design/icons";

import type { AccountSetDto } from "@/api/accountSets";

/** 尚未接入税局取数的字段占位。 */
const NOT_SYNCED = "—";

const EXPORT_OPTIONS = [
    { value: "否", label: "否" },
    { value: "是", label: "是" },
];

const BRANCH_OPTIONS = [
    { value: "非总分机构", label: "非总分机构" },
    { value: "总机构", label: "总机构" },
    { value: "分支机构", label: "分支机构" },
];

interface CompanyInfoSnapshot {
    exportEnterprise: string;
    branchType: string;
}

const DEFAULT_SNAPSHOT: CompanyInfoSnapshot = {
    exportEnterprise: "否",
    branchType: "非总分机构",
};

const storageKey = (accountSetId: string) => `kuaiji.tax.company-info.${accountSetId}`;

function loadSnapshot(accountSetId: string): CompanyInfoSnapshot {
    try {
        const raw = window.localStorage.getItem(storageKey(accountSetId));
        if (!raw) return DEFAULT_SNAPSHOT;
        const saved = JSON.parse(raw) as Partial<CompanyInfoSnapshot>;
        return {
            exportEnterprise: EXPORT_OPTIONS.some((o) => o.value === saved.exportEnterprise)
                ? saved.exportEnterprise!
                : DEFAULT_SNAPSHOT.exportEnterprise,
            branchType: BRANCH_OPTIONS.some((o) => o.value === saved.branchType)
                ? saved.branchType!
                : DEFAULT_SNAPSHOT.branchType,
        };
    } catch {
        return DEFAULT_SNAPSHOT;
    }
}

/** 纳税人资格类型：账套里存的是枚举值（1 一般纳税人 / 2 小规模纳税人）。 */
function taxpayerLabel(value?: number): string {
    if (value === 1) return "一般纳税人";
    if (value === 2) return "小规模纳税人";
    return NOT_SYNCED;
}

/** 主管税务机关：账套只保存到省级/计划单列市，展示到对应税务局。 */
function taxAuthority(region?: string | null): string {
    return region ? `国家税务总局${region}税务局` : NOT_SYNCED;
}

/** 表单行：左标签右控件，与税局公司信息对话框的对齐方式一致。 */
function FieldRow({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
    return (
        <div className="flex items-center gap-4">
            <span className="flex w-[150px] shrink-0 items-center justify-end gap-1 text-[13px] text-(--foreground)">
                {hint && (
                    <Tooltip title={hint}>
                        <QuestionCircleOutlined className="cursor-help text-[13px] text-(--muted-foreground)" />
                    </Tooltip>
                )}
                {label}：
            </span>
            <div className="min-w-0 flex-1">{children}</div>
        </div>
    );
}

export function CompanyInfoModal({
    open,
    accountSet,
    onClose,
}: {
    open: boolean;
    accountSet?: AccountSetDto;
    onClose: () => void;
}) {
    const { message } = App.useApp();
    const accountSetId = accountSet?.id ?? "";
    const [snapshot, setSnapshot] = useState<CompanyInfoSnapshot>(DEFAULT_SNAPSHOT);

    // 每次打开都按当前账套重新读取本地保存的两个选项。
    useEffect(() => {
        if (open && accountSetId) setSnapshot(loadSnapshot(accountSetId));
    }, [open, accountSetId]);

    const patch = (next: Partial<CompanyInfoSnapshot>) => setSnapshot((prev) => ({ ...prev, ...next }));

    const save = () => {
        if (accountSetId) {
            window.localStorage.setItem(storageKey(accountSetId), JSON.stringify(snapshot));
        }
        message.success("公司信息已保存。");
    };

    /** 公司登记资料由税局下发，需先完成服务端登录才能同步。 */
    const syncCompanyInfo = () => {
        message.info("公司登记信息由电子税务局返回，完成「一键登录税局」后自动同步；当前展示账套中已保存的资料。");
    };

    return (
        <Modal
            open={open}
            onCancel={onClose}
            width={900}
            centered
            title="公司信息"
            footer={
                <div className="flex items-center justify-center gap-3">
                    <Button  onClick={save}>
                        保存
                    </Button>
                    <Button type="primary"  onClick={syncCompanyInfo}>
                        更新公司信息
                    </Button>
                </div>
            }
        >
            <div className="flex flex-col gap-4 pb-1 pt-2">
                <FieldRow label="公司名称">
                    <Input size="large" disabled value={accountSet?.companyName ?? ""} />
                </FieldRow>
                <FieldRow label="统一信用代码">
                    <Input size="large" disabled value={accountSet?.taxpayerNumber ?? ""} />
                </FieldRow>
                <FieldRow label="纳税人资格类型">
                    <Input size="large" disabled value={taxpayerLabel(accountSet?.taxpayerType)} />
                </FieldRow>
                <FieldRow label="登记注册类型">
                    <Input size="large" disabled value={NOT_SYNCED} />
                </FieldRow>
                <FieldRow label="税务登记日期">
                    <Input size="large" disabled value={NOT_SYNCED} />
                </FieldRow>
                <FieldRow label="主管税务机关">
                    <Input size="large" disabled value={taxAuthority(accountSet?.taxRegion)} />
                </FieldRow>
                <FieldRow label="是否出口企业" hint="出口企业需先办理出口退（免）税备案，是否出口以电子税务局登记为准。">
                    <Select
                        size="large"
                        className="w-full"
                        options={EXPORT_OPTIONS}
                        value={snapshot.exportEnterprise}
                        onChange={(value) => patch({ exportEnterprise: value })}
                    />
                </FieldRow>
                <FieldRow label="总分支机构类型">
                    <Select
                        size="large"
                        className="w-full"
                        options={BRANCH_OPTIONS}
                        value={snapshot.branchType}
                        onChange={(value) => patch({ branchType: value })}
                    />
                </FieldRow>
            </div>
            <p className="mt-2 text-[12px] leading-5 text-(--muted-foreground)">
                登记注册类型与税务登记日期由电子税务局返回，完成「一键登录税局」后自动同步。
            </p>
        </Modal>
    );
}
