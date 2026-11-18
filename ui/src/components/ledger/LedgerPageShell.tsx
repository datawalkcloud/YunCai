import type { ReactNode } from "react"
import { Alert } from "antd"
import { Link } from "react-router"

import type { LedgerMetaDto } from "@/apis-gen"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { SectionCard } from "@/components/page/section"
import { setPath } from "@/accountSets/url-id"

export interface LedgerPageShellProps {
    /** 查询面板。 */
    panel?: ReactNode
    meta?: LedgerMetaDto | null
    /** 接口返回的中文错误，直接展示。 */
    error?: string | null
    /** 右侧固定栏（如明细账的「快速切换」科目树）。 */
    side?: ReactNode
    children: ReactNode
}

/**
 * 账页统一骨架：查询面板 + 正文，可选右侧栏。
 * 金额单位固定为元，故在正文下方统一标注。
 */
export function LedgerPageShell({ panel, meta, error, side, children }: LedgerPageShellProps) {
    const accountSetId = useCurrentAccountSetId()
    const unposted = meta?.unpostedVoucherCount ?? 0
    return (
        <SectionCard bodyClassName="flex flex-col gap-4">
            {panel}
            <div className="flex items-start gap-4">
                <div className="flex min-w-0 flex-1 flex-col gap-4">
                    {unposted > 0 && (
                        <Alert
                            type="warning"
                            showIcon
                            title={`${meta?.periodText ?? ""} 内有 ${unposted} 张暂存凭证未纳入账簿，请先在凭证页打开并保存入账。`}
                            action={
                                accountSetId ? (
                                    <Link
                                        to={setPath(
                                            accountSetId,
                                            `/vouchers/manage?status=draft${
                                                meta?.startDate && meta.endDate
                                                    ? `&periodFrom=${meta.startDate.slice(0, 7)}&periodTo=${meta.endDate.slice(0, 7)}`
                                                    : ""
                                            }`,
                                        )}
                                        className="whitespace-nowrap text-[12.5px] font-medium text-(--brand) hover:underline"
                                    >
                                        去凭证页处理
                                    </Link>
                                ) : undefined
                            }
                        />
                    )}
                    {error && <Alert type="error" showIcon title={error} />}
                    {children}
                    <span className="text-[12px] text-(--muted-foreground)">单位：元</span>
                </div>
                {side && (
                    <aside className="sticky top-3 hidden max-h-[calc(100vh-120px)] w-[228px] shrink-0 self-stretch overflow-hidden xl:block">
                        {side}
                    </aside>
                )}
            </div>
        </SectionCard>
    )
}
