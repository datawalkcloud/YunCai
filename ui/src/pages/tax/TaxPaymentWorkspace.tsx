/**
 * 智能报税 · 税费缴纳。
 *
 * 税局侧「缴税」「缴款信息查询」「开具完税证明」「下载完税证明」是四个独立功能，
 * 工作台外壳只有一个板块页签位，因此在板块内再分四个页签；数据接入前除缴款信息
 * 查询用本地演示数据外，其余页面保持空态。
 */
import { useState } from "react";

import type { AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { cn } from "@/lib/utils";

import { TaxCertificateDownloadPage } from "./TaxCertificateDownloadPage";
import { TaxCertificatePage } from "./TaxCertificatePage";
import { TaxPaymentPage } from "./TaxPaymentPage";
import { TaxPaymentPayPage } from "./TaxPaymentPayPage";

type TabKey = "pay" | "query" | "certificate" | "download";

const TABS: Array<{ key: TabKey; label: string }> = [
    { key: "pay", label: "缴税" },
    { key: "query", label: "缴款信息查询" },
    { key: "certificate", label: "开具完税证明" },
    { key: "download", label: "下载完税证明" },
];

export function TaxPaymentWorkspace({ period }: { period: AccountingPeriod }) {
    const [tab, setTab] = useState<TabKey>("download");

    return (
        <div className="flex h-full flex-col">
            <div className="flex h-[46px] shrink-0 items-center gap-6 border-b border-slate-200 px-6">
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

            <div className="min-h-0 flex-1 overflow-auto">
                {tab === "pay" ? (
                    <TaxPaymentPayPage period={period} />
                ) : tab === "query" ? (
                    <TaxPaymentPage />
                ) : tab === "certificate" ? (
                    <TaxCertificatePage />
                ) : (
                    <TaxCertificateDownloadPage onOpenCertificate={() => setTab("certificate")} />
                )}
            </div>
        </div>
    );
}
