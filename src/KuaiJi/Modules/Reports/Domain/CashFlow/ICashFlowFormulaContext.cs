namespace KuaiJi.Modules.Reports.Domain.CashFlow
{
    /// <summary>
    /// 取数公式的取数上下文：一个上下文对应报表的一个金额列（本期 / 本年累计 / 上年同期），
    /// 所有函数都在该列的时间窗口内取值。
    /// </summary>
    public interface ICashFlowFormulaContext
    {
        /// <summary>该窗口内指定科目（含下级）的借方发生额。</summary>
        decimal Debit(string accountCode);

        /// <summary>该窗口内指定科目（含下级）的贷方发生额。</summary>
        decimal Credit(string accountCode);

        /// <summary>窗口起点前的借-贷净额（含科目启用期初）。</summary>
        decimal Opening(string accountCode);

        /// <summary>窗口截止日的借-贷净额（含科目启用期初）。</summary>
        decimal Closing(string accountCode);

        /// <summary>该窗口内指定现金流项目（含下级）的流入金额。</summary>
        decimal CashInflow(string itemCode);

        /// <summary>该窗口内指定现金流项目（含下级）的流出金额。</summary>
        decimal CashOutflow(string itemCode);

        /// <summary>该窗口内指定现金流项目（含下级）的净额（流入-流出）。</summary>
        decimal CashNet(string itemCode);

        /// <summary>该窗口内系统归类到指定行次的金额（与直接法主表同源）。</summary>
        decimal Auto(int line);
    }
}
