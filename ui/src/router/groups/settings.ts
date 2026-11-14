/**
 * 设置域 chunk：科目、账套、期初、辅助核算档案、凭证字、操作日志（既有实页）与柠檬云的
 * 币别、凭证模板、权限、老板报表、备份、关联与导入/重新初始化。
 * 仅供 router 动态 import，禁止静态引入。
 */
export { default as Accounts } from "@/pages/settings/accounts";
export { default as AccountSets } from "@/pages/settings/account-sets";
export { default as OpeningBalances } from "@/pages/settings/opening-balances";
export { default as AssistItems } from "@/pages/settings/assist-items";
export { default as VoucherWord } from "@/pages/settings/voucher-word";
export { default as OperationLog } from "@/pages/settings/operation-log";
export { default as Users } from "@/pages/settings/users";
export {
    LemonCurrency,
    LemonVoucherTemplate,
    LemonPermission,
    LemonBoss,
    LemonBackup,
    LemonEisRelation,
    LemonScmRelation,
    LemonImportOld,
    LemonReinit,
} from "@/pages/lemon/lemon-settings";
