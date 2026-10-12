import { useEffect, useState } from "react"

import { AccountsApi, type AccountDto, type AssistCategoryStatDto, type AssistItemDto } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import {
    useAssistCategories as useCachedAssistCategories,
    useAssistItems as useCachedAssistItems,
} from "@/components/assist/assist-store"

/** 拉取当前账套的科目列表（多栏账选科目、级次上限都用它）。 */
export function useAccounts(accountSetId: string, token?: string | null): AccountDto[] {
    const [accounts, setAccounts] = useState<AccountDto[]>([])

    useEffect(() => {
        if (!accountSetId) {
            setAccounts([])
            return
        }
        let cancelled = false
        new AccountsApi(apiConfig(token))
            .accountsetsAccountSetIdAccountsGet({ accountSetId })
            .then((res) => {
                if (!cancelled) setAccounts(res ?? [])
            })
            .catch(() => {
                // 下拉数据取不到不影响账页主体，保持为空即可。
                if (!cancelled) setAccounts([])
            })
        return () => {
            cancelled = true
        }
    }, [accountSetId, token])

    return accounts
}

/** 拉取账套的辅助核算类别统计，用于只展示已启用的类别。 */
export function useAssistCategories(
    accountSetId: string,
    token?: string | null,
): AssistCategoryStatDto[] {
    return useCachedAssistCategories(accountSetId, token).categories
}

/** 按类别拉取辅助核算档案，供「核算项目」下拉筛选。 */
export function useAssistItems(
    accountSetId: string,
    token: string | null | undefined,
    type?: number,
): AssistItemDto[] {
    return useCachedAssistItems(accountSetId, type ?? null, token).items
}
