/**
 * 发票域 chunk：发票相关页面（取票、销项、进项、费用单、汇总、风险），
 * 以及同源于发票模块 的税务凭证、银企互联两个页面——
 * 同一个源模块无法拆到不同 chunk，故一并归入发票域。
 * 税局登录已拆分为独立页面，仍从本 chunk 导出以免多吐一个碎片文件。
 * 仅供 router 动态 import，禁止静态引入。
 */
export {
    InvoiceFetch,
    SalesInvoice,
    PurchaseInvoice,
    ExpenseBill,
    InvoiceSummary,
    InvoiceRisk,
    Spb,
    BankLink,
} from "@/pages/business/invoice";

export { TaxLogin } from "@/pages/business/tax";

export { VatCalc } from "@/pages/business/vat-calc";
