import { Checkbox, Input, Radio, Select, Switch, Tooltip } from "antd"
import { QuestionCircleOutlined } from "@ant-design/icons"

import type { CurrencyDto } from "@/apis-gen"
import { ASSIST_TYPES } from "@/components/assist/constants"
import { FormField } from "@/components/page/form-field"
import { SOFT_PANEL } from "@/components/page/styles"
import {
    DIRECTION_OPTIONS,
    SUB_CATEGORY_OPTIONS_BY_CATEGORY,
    UNIT_MAX_LENGTH,
    foreignCurrenciesOf,
    type AccountAttributeErrors,
    type AccountAttributes,
} from "./account-attributes"

/**
 * 科目核算属性字段组：科目类别（二级）、余额方向 + 是否为现金科目、辅助核算、数量核算、外币核算。
 * 新增与编辑两个弹窗共用，字段顺序与文案的「编辑科目」面板。
 */
interface FieldsProps {
    /** 科目大类，决定二级类别候选。 */
    category: number
    /** 余额方向（借/贷），与「是否为现金科目」同排展示。 */
    direction: number
    onDirectionChange: (direction: number) => void
    /** 末级科目才能改方向与编码。 */
    directionDisabled?: boolean
    /** 新增明细科目：核算属性由后端从上级继承，控件只读并给出说明。 */
    inherited?: boolean
    value: AccountAttributes
    onChange: (patch: Partial<AccountAttributes>) => void
    errors?: AccountAttributeErrors
    /** 账套已启用的辅助核算类别（type → 是否启用）；未启用且未勾选的类别不可点。 */
    assistEnabledByType: Record<number, boolean>
    /** 账套币别档案。 */
    currencies: CurrencyDto[]
    /** 币别档案加载失败：给出可辨识的提示，而不是显示成「没有币别」。 */
    currenciesUnavailable?: boolean
}

export function AccountAttributeFields({
    category,
    direction,
    onDirectionChange,
    directionDisabled = false,
    inherited = false,
    value,
    onChange,
    errors,
    assistEnabledByType,
    currencies,
    currenciesUnavailable = false,
}: FieldsProps) {
    const subOptions = SUB_CATEGORY_OPTIONS_BY_CATEGORY[category] ?? []
    const foreignCurrencies = foreignCurrenciesOf(currencies)
    const checkedAssistCount = value.assistRows.filter((r) => r.checked).length

    return (
        <>
            <FormField label="科目类别" htmlFor="account-sub-category" error={errors?.subCategory}>
                <Select
                    id="account-sub-category"
                    className="w-full"
                    allowClear
                    disabled={inherited}
                    placeholder={inherited ? "继承上级科目" : "（未指定）"}
                    value={value.subCategory}
                    options={subOptions}
                    onChange={(v) => onChange({ subCategory: v == null ? undefined : Number(v) })}
                />
            </FormField>

            <div className="flex flex-wrap items-center gap-x-8 gap-y-3">
                <FormField
                    label="余额方向"
                    className="w-fit"
                    description={directionDisabled ? "非末级科目不允许修改余额方向。" : undefined}
                >
                    <Radio.Group
                        disabled={directionDisabled}
                        value={direction}
                        options={DIRECTION_OPTIONS}
                        onChange={(e) => onDirectionChange(e.target.value as number)}
                    />
                </FormField>
                <Checkbox
                    disabled={inherited}
                    checked={value.isCashAccount}
                    onChange={(e) => onChange({ isCashAccount: e.target.checked })}
                >
                    <span className="flex items-center gap-1">
                        是否为现金科目
                        <Tooltip title="勾选后该科目计入现金流量表的现金及现金等价物口径">
                            <QuestionCircleOutlined className="text-[13px]" />
                        </Tooltip>
                    </span>
                </Checkbox>
            </div>

            <FormField
                label="辅助核算"
                description={inherited ? "辅助核算按科目单独维护：明细科目建好后在其「编辑科目」里勾选。" : undefined}
            >
                <div className="flex items-center gap-3">
                    <Switch
                        disabled={inherited}
                        checked={checkedAssistCount > 0}
                        onChange={(checked) =>
                            onChange({
                                assistRows: value.assistRows.map((r) =>
                                    // 关闭只清掉可勾选的维度；已挂上但账套停用的维度保持原样，避免误删历史配置。
                                    checked || !assistEnabledByType[r.type] ? r : { ...r, checked: false },
                                ),
                            })
                        }
                    />
                    <span className="text-[12.5px] text-(--muted-foreground)">
                        {checkedAssistCount > 0 ? `已选 ${checkedAssistCount} 项` : "未设置"}
                    </span>
                </div>
            </FormField>

            {checkedAssistCount > 0 && !inherited && (
                <div className={`${SOFT_PANEL} flex flex-col gap-2 px-4 py-3`}>
                    <div className="flex flex-wrap gap-x-6 gap-y-2">
                        {value.assistRows.map((row) => {
                            const meta = ASSIST_TYPES.find((t) => t.value === row.type)
                            const enabledInSet = assistEnabledByType[row.type] === true
                            return (
                                <span key={row.type} className="flex items-center gap-3">
                                    <Checkbox
                                        checked={row.checked}
                                        disabled={!enabledInSet && !row.checked}
                                        onChange={(e) =>
                                            onChange({
                                                assistRows: value.assistRows.map((r) =>
                                                    r.type === row.type ? { ...r, checked: e.target.checked } : r,
                                                ),
                                            })
                                        }
                                    >
                                        {meta?.label ?? row.type}
                                        {!enabledInSet && !row.checked && (
                                            <span className="ml-1 text-[11px] text-(--muted-foreground)">账套未启用</span>
                                        )}
                                    </Checkbox>
                                    {row.checked && (
                                        <Checkbox
                                            checked={row.isRequired}
                                            onChange={(e) =>
                                                onChange({
                                                    assistRows: value.assistRows.map((r) =>
                                                        r.type === row.type ? { ...r, isRequired: e.target.checked } : r,
                                                    ),
                                                })
                                            }
                                        >
                                            必录
                                        </Checkbox>
                                    )}
                                </span>
                            )
                        })}
                    </div>
                    <p className="text-[12.5px] text-(--muted-foreground)">
                        挂上辅助核算后，凭证里该科目分录必须录入对应类别的核算项目档案（标「必录」）。改动只影响后续制单，历史凭证取值不变。
                    </p>
                </div>
            )}

            <FormField label="数量核算">
                <div className="flex items-center gap-3">
                    <Switch
                        disabled={inherited}
                        checked={value.isQuantityAccounted}
                        onChange={(checked) => onChange({ isQuantityAccounted: checked })}
                    />
                    <span className="text-[12.5px] text-(--muted-foreground)">
                        {value.isQuantityAccounted ? "按数量 + 金额记账" : "未开启"}
                    </span>
                </div>
            </FormField>

            {value.isQuantityAccounted && !inherited && (
                <div className={`${SOFT_PANEL} flex items-center gap-3 px-4 py-3`}>
                    <span className="text-sm">计算单位：</span>
                    <Input
                        className="max-w-[240px]"
                        maxLength={UNIT_MAX_LENGTH}
                        placeholder="请录入计量单位"
                        value={value.unit}
                        status={errors?.unit ? "error" : undefined}
                        onChange={(e) => onChange({ unit: e.target.value })}
                    />
                </div>
            )}

            <FormField label="外币核算" error={errors?.currencyIds}>
                <div className="flex items-center gap-3">
                    <Switch
                        disabled={inherited}
                        checked={value.isForeignCurrencyAccounted}
                        onChange={(checked) => onChange({ isForeignCurrencyAccounted: checked })}
                    />
                    <span className="text-[12.5px] text-(--muted-foreground)">
                        {value.isForeignCurrencyAccounted
                            ? `已选 ${value.currencyIds.length} 种币别`
                            : "未开启"}
                    </span>
                </div>
            </FormField>

            {value.isForeignCurrencyAccounted && !inherited && (
                <div className={`${SOFT_PANEL} flex flex-col gap-2 px-4 py-3`}>
                    {foreignCurrencies.length === 0 ? (
                        <span className="text-[12.5px] text-(--muted-foreground)">
                            {currenciesUnavailable
                                ? "币别档案加载失败，请刷新页面后重试。"
                                : "账套还没有非本位币币别，请先在账套设置的「币别」中新增。"}
                        </span>
                    ) : (
                        <Checkbox.Group
                            className="flex flex-wrap gap-x-6 gap-y-2"
                            value={value.currencyIds}
                            options={foreignCurrencies.map((c) => ({ value: String(c.id), label: c.name ?? "" }))}
                            onChange={(ids) => onChange({ currencyIds: ids.map(String) })}
                        />
                    )}
                    <p className="text-[12.5px] text-(--muted-foreground)">
                        制单时该科目只能选这里勾了的币别，本位币始终可选。
                    </p>
                </div>
            )}
        </>
    )
}
