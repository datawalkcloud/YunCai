namespace YunCai.Modules.AccountSets.Domain
{
    /// <summary>重新初始化仓储：批量清空账套业务数据（绕过聚合逐个删除，直接批量执行）。</summary>
    public interface IReinitRepository
    {
        /// <summary>清空账套全部业务数据（凭证/日记账/转账/票据/发票/工资/资产/电子档案/模板/收支类别/辅助核算档案），
        /// 并将非预置科目删除、全部科目期初余额清零。返回各表删除行数。</summary>
        Task<IReadOnlyDictionary<string, int>> WipeBusinessDataAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>将账套结转 / 结账期间退回启用期间（0）。</summary>
        Task ResetPeriodsAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>级联删除账套及其全部关联数据（科目、凭证、账簿、资金、发票、工资、资产、档案、模板、字典与成员），不可撤销。</summary>
        Task DeleteAccountSetAsync(Guid accountSetId, CancellationToken cancellationToken = default);
    }
}
