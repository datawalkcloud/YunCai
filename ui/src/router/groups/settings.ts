/**
 * 设置域 chunk：科目、账套、期初、辅助核算档案（既有实页）与柠檬云的
 * 币别、凭证字、凭证模板、权限、老板报表、备份、操作日志、关联与导入/重新初始化。
 * 仅供 router 动态 import，禁止静态引入。
 */
export { default as Accounts } from "@/pages/settings/accounts";
export { default as AccountSets } from "@/pages/settings/account-sets";
export { default as OpeningBalances } from "@/pages/settings/opening-balances";
export { default as AssistItems } from "@/pages/settings/assist-items";
export {
    LemonCurrency,
    LemonVoucherWord,
    LemonVoucherTemplate,
    LemonPermission,
    LemonBoss,
    LemonBackup,
    LemonLog,
    LemonEisRelation,
    LemonScmRelation,
    LemonImportOld,
    LemonReinit,
} from "@/pages/lemon/lemon-settings";
