using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Funds.Domain;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>收支类别 DTO（含编码、上级类别、关联现金流、启用状态与智能匹配关键字）。</summary>
    public sealed class EntryCategoryDto
    {
        public Guid Id { get; init; }

        /// <summary>类别类型（1=收入，2=支出）。</summary>
        public int Type { get; init; }

        public string TypeText { get; init; } = null!;

        /// <summary>类别编码（同一账套内唯一，默认按序编号）。</summary>
        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        /// <summary>上级类别标识；顶级类别为 null。</summary>
        public Guid? ParentId { get; init; }

        public string? ParentName { get; init; }

        /// <summary>关联现金流项目（辅助核算「现金流」档案）。</summary>
        public Guid? CashFlowItemId { get; init; }

        public string? CashFlowName { get; init; }

        /// <summary>是否启用。</summary>
        public bool IsEnabled { get; init; }

        /// <summary>智能匹配关键字（逗号分隔）；流水导入时按摘要包含关系自动归类。</summary>
        public string? SmartKeywords { get; init; }

        /// <summary>默认对方科目（由流水生成凭证时使用）。</summary>
        public Guid? VoucherSubjectId { get; init; }

        public string? VoucherSubjectCode { get; init; }

        public string? VoucherSubjectName { get; init; }

        public static EntryCategoryDto FromDomain(
            EntryCategory category,
            Account? subject = null,
            string? parentName = null,
            string? cashFlowName = null) => new()
        {
            Id = category.Id,
            Type = (int)category.Type,
            TypeText = TypeTextOf(category.Type),
            Code = category.Code,
            Name = category.Name,
            ParentId = category.ParentId,
            ParentName = parentName,
            CashFlowItemId = category.CashFlowItemId,
            CashFlowName = cashFlowName,
            IsEnabled = category.IsEnabled,
            SmartKeywords = category.SmartKeywords,
            VoucherSubjectId = category.VoucherSubjectId,
            VoucherSubjectCode = subject?.Code,
            VoucherSubjectName = subject?.Name,
        };

        internal static string TypeTextOf(EntryCategoryType type) => type switch
        {
            EntryCategoryType.Income => "收入",
            EntryCategoryType.Expense => "支出",
            _ => type.ToString(),
        };
    }

    /// <summary>新建收支类别请求。</summary>
    public sealed class CreateEntryCategoryRequest
    {
        /// <summary>类别类型（1=收入，2=支出）。</summary>
        public int Type { get; init; }

        /// <summary>类别编码；为空时自动取当前账套的下一个序号。</summary>
        public string? Code { get; init; }

        public string Name { get; init; } = null!;

        /// <summary>上级类别（选填）。</summary>
        public Guid? ParentId { get; init; }

        /// <summary>关联现金流项目（选填）。</summary>
        public Guid? CashFlowItemId { get; init; }

        /// <summary>智能匹配关键字（逗号分隔，选填）。</summary>
        public string? SmartKeywords { get; init; }

        /// <summary>默认对方科目（选填；须为末级启用科目）。</summary>
        public Guid? VoucherSubjectId { get; init; }
    }

    /// <summary>修改收支类别请求（类型不可变更）。</summary>
    public sealed class UpdateEntryCategoryRequest
    {
        public string Name { get; init; } = null!;

        /// <summary>类别编码；null = 保持不变。</summary>
        public string? Code { get; init; }

        /// <summary>上级类别；null = 保持不变，Guid.Empty = 置为顶级。</summary>
        public Guid? ParentId { get; init; }

        /// <summary>关联现金流项目；null = 保持不变，Guid.Empty = 清除。</summary>
        public Guid? CashFlowItemId { get; init; }

        /// <summary>智能匹配关键字；null = 保持不变，空串 = 清除。</summary>
        public string? SmartKeywords { get; init; }

        /// <summary>默认对方科目；null = 保持不变，Guid.Empty = 清除，其余 = 设置。</summary>
        public Guid? VoucherSubjectId { get; init; }
    }

    /// <summary>收支类别导入结果。</summary>
    public sealed class EntryCategoryImportResult
    {
        /// <summary>总行数（不含表头）。</summary>
        public int TotalRows { get; init; }

        /// <summary>成功导入条数。</summary>
        public int Imported { get; init; }

        /// <summary>跳过条数。</summary>
        public int Skipped { get; init; }

        /// <summary>逐行告警（重复、缺字段等）。</summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }
}
