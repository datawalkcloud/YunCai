/**
 * 账簿域 chunk：科目余额表、明细账、总账、凭证汇总表、序时账、多栏账、
 * 辅助核算三类账页与账龄分析。
 * 仅供 router 动态 import，禁止静态引入。
 */
export { default as TrialBalance } from "@/pages/ledger/trial-balance";
export { default as SubsidiaryLedger } from "@/pages/ledger/subsidiary";
export { default as GeneralLedger } from "@/pages/ledger/general";
export { default as VoucherJournal } from "@/pages/ledger/journal";
export { default as VoucherSummary } from "@/pages/ledger/voucher-summary";
export { default as MultiColumnLedger } from "@/pages/ledger/multi-column";
export { default as AssistBalance } from "@/pages/ledger/assist-balance";
export { default as AssistSubsidiary } from "@/pages/ledger/assist-subsidiary";
export { default as AssistCombine } from "@/pages/ledger/assist-combine";
export { default as AgeingAnalysis } from "@/pages/ledger/ageing";
