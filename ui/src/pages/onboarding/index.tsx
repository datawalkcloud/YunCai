import { useState } from "react"
import { Alert, Button } from "antd"
import dayjs from "dayjs"
import { useNavigate } from "react-router"

import { useAuth } from "@/auth/AuthContext"
import { useAccountSets } from "@/accountSets/AccountSetsContext"
import { AccountSetsApi } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { SectionCard } from "@/components/page/section"
import {
    BasicInfoFields,
} from "@/pages/settings/account-sets/account-set-form"
import {
    emptyForm,
    toCreateRequest,
    validateAccountSet,
    type AccountSetErrors,
    type AccountSetForm,
} from "@/pages/settings/account-sets/account-set-form-data"

/**
 * 登录后还没有任何账套时的引导页：直接在页内建套（字段与「设置 → 账套」的新增抽屉一致），
 * 创建成功后进入新账套。后端会把首个账套置为当前，并把创建者登记为账套管理员。
 */
export default function NoAccountSetGuide() {
    const { token } = useAuth()
    const { refresh } = useAccountSets()
    const navigate = useNavigate()

    const [values, setValues] = useState<AccountSetForm>(() => ({ ...emptyForm(), startPeriod: dayjs().toDate() }))
    const [errors, setErrors] = useState<AccountSetErrors>({})
    const [submitting, setSubmitting] = useState(false)
    const [error, setError] = useState<string | null>(null)

    function setValue<K extends keyof AccountSetForm>(key: K, value: AccountSetForm[K]) {
        const next: AccountSetForm = { ...values, [key]: value }
        setValues(next)
        if (Object.keys(errors).length > 0) setErrors(validateAccountSet(next, false))
    }

    async function submit() {
        const next = validateAccountSet(values, false)
        setErrors(next)
        if (Object.keys(next).length > 0) return
        setSubmitting(true)
        setError(null)
        try {
            const created = await new AccountSetsApi(apiConfig(token)).accountSetsPost({
                createAccountSetRequest: toCreateRequest(values),
            })
            refresh()
            if (created.id) navigate(`/${created.id}`, { replace: true })
        } catch (err) {
            setError(await apiErrorMessage(err, "创建账套失败，请重试。"))
        } finally {
            setSubmitting(false)
        }
    }

    return (
        <div className="flex min-h-svh items-center justify-center p-6">
            <SectionCard className="w-full max-w-[640px]">
                <div className="flex flex-col gap-6">
                    <div className="flex flex-col gap-1">
                        <h3 className="text-[15px] font-semibold text-(--foreground)">创建第一个账套</h3>
                        <p className="text-[12.5px] text-(--muted-foreground)">
                            账套是一套完整的账务数据，包含科目、凭证、账簿与报表；建好即可开始记账。
                            账套名称按公司名称与启用年份自动生成，创建后可在「设置 → 账套」里改。
                        </p>
                    </div>

                    {error && <Alert type="error" showIcon title={error} />}

                    <BasicInfoFields values={values} errors={errors} editing={false} onChange={setValue} />

                    <div className="flex justify-end">
                        <Button type="primary" loading={submitting} onClick={() => void submit()}>
                            创建账套
                        </Button>
                    </div>
                </div>
            </SectionCard>
        </div>
    )
}
