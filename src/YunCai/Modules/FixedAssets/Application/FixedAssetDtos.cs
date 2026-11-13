using YunCai.Modules.FixedAssets.Domain;

namespace YunCai.Modules.FixedAssets.Application
{
    /// <summary>资产类别 DTO。</summary>
    public sealed class FixedAssetCategoryDto
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        public string MethodText => "平均年限法";

        public string AttrText { get; init; } = null!;

        public Guid SubjectAccountId { get; init; }

        public string SubjectCode { get; init; } = null!;

        public string SubjectName { get; init; } = null!;

        public int Months { get; init; }

        public decimal ResidualRate { get; init; }

        public string? Remark { get; init; }

        public static string AttrTextOf(FixedAssetAttr attr) => attr switch
        {
            FixedAssetAttr.Fixed => "固定资产",
            FixedAssetAttr.Intangible => "无形资产",
            _ => "长期待摊费用",
        };
    }

    /// <summary>新建 / 修改资产类别请求。</summary>
    public sealed class SaveFixedAssetCategoryRequest
    {
        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        /// <summary>折旧方法（当前仅 1 = 平均年限法）。</summary>
        public int Method { get; init; } = 1;

        /// <summary>资产属性（1 固定资产 / 2 无形资产 / 3 长期待摊费用）。</summary>
        public int Attr { get; init; }

        public Guid SubjectAccountId { get; init; }

        public int Months { get; init; }

        public decimal ResidualRate { get; init; }
    }

    /// <summary>资产卡片 DTO。</summary>
    public sealed class FixedAssetDto
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = null!;

        public Guid CategoryId { get; init; }

        public string CategoryName { get; init; } = null!;

        public string AttrText { get; init; } = null!;

        public string Name { get; init; } = null!;

        public string? Model { get; init; }

        public string? Dept { get; init; }

        public DateOnly StartDate { get; init; }

        public decimal OriginValue { get; init; }

        /// <summary>累计折旧（含初始化已提）。</summary>
        public decimal AccumulatedDepreciation { get; init; }

        /// <summary>本期折旧（最近一次计提期间的折旧额，与柠檬云「本期折旧」列一致）。</summary>
        public decimal CurrentPeriodDepreciation { get; init; }

        /// <summary>资产净值 = 原值 − 累计折旧。</summary>
        public decimal NetValue => OriginValue - AccumulatedDepreciation;

        public string StatusText { get; init; } = null!;

        public DateOnly? ClearedDate { get; init; }

        /// <summary>是否为期初（资产初始化）卡片。</summary>
        public bool IsInitial { get; init; }
    }

    /// <summary>新建 / 修改资产卡片请求。</summary>
    public sealed class SaveFixedAssetRequest
    {
        public string Code { get; init; } = null!;

        public Guid CategoryId { get; init; }

        public string Name { get; init; } = null!;

        public string? Model { get; init; }

        public string? Dept { get; init; }

        public DateOnly StartDate { get; init; }

        public decimal OriginValue { get; init; }

        /// <summary>初始化累计折旧（仅新建时生效）。</summary>
        public decimal? InitialAccumulatedDepreciation { get; init; }
    }

    /// <summary>计提折旧行 DTO（折旧摊销明细表）。</summary>
    public sealed class DepreciationLedgerRowDto
    {
        public Guid AssetId { get; init; }

        public string AssetCode { get; init; } = null!;

        public string CategoryName { get; init; } = null!;

        public string AttrText { get; init; } = null!;

        public string AssetName { get; init; } = null!;

        public string? Model { get; init; }

        public string? Dept { get; init; }

        public decimal OriginValue { get; init; }

        /// <summary>本期折旧。</summary>
        public decimal PeriodDepreciation { get; init; }

        /// <summary>本年累计折旧。</summary>
        public decimal YearAccumulated { get; init; }

        /// <summary>累计折旧。</summary>
        public decimal TotalAccumulated { get; init; }

        /// <summary>减值准备（模型暂未启用，恒为 0）。</summary>
        public decimal Impairment => 0m;

        public decimal NetValue => OriginValue - TotalAccumulated - Impairment;
    }

    /// <summary>资产变动记录 DTO。</summary>
    public sealed class FixedAssetChangeDto
    {
        public Guid Id { get; init; }

        public Guid AssetId { get; init; }

        public string AssetCode { get; init; } = null!;

        public string AssetName { get; init; } = null!;

        public string KindText { get; init; } = null!;

        public string Before { get; init; } = null!;

        public string After { get; init; } = null!;

        public string Period { get; init; } = null!;

        public decimal Amount { get; init; }

        /// <summary>已关联的凭证标识（未生成为 null）。</summary>
        public Guid? VoucherId { get; init; }

        /// <summary>关联凭证字号（形如「记-12」）。</summary>
        public string? VoucherText { get; init; }

        public static string KindTextOf(FixedAssetChangeKind kind) => kind switch
        {
            FixedAssetChangeKind.Add => "新增",
            FixedAssetChangeKind.OriginValueAdjust => "原值调整",
            FixedAssetChangeKind.AccumulatedDepreciationAdjust => "累计折旧调整",
            FixedAssetChangeKind.Clearing => "资产清理",
            _ => "每月计提",
        };
    }

    /// <summary>资产汇总行 DTO（按部门 / 类别分组）。</summary>
    public sealed class FixedAssetSummaryRowDto
    {
        public string GroupName { get; init; } = null!;

        /// <summary>资产原值：期初数 / 本期增加 / 本期减少 / 期末数。</summary>
        public decimal OriginOpening { get; init; }

        public decimal OriginIncrease { get; init; }

        public decimal OriginDecrease { get; init; }

        public decimal OriginClosing { get; init; }

        /// <summary>累计折旧四列。</summary>
        public decimal DepOpening { get; init; }

        public decimal DepIncrease { get; init; }

        public decimal DepDecrease { get; init; }

        public decimal DepClosing { get; init; }

        /// <summary>减值准备四列（模型暂未启用，恒为 0）。</summary>
        public decimal ImpairOpening => 0m;

        public decimal ImpairIncrease => 0m;

        public decimal ImpairDecrease => 0m;

        public decimal ImpairClosing => 0m;

        /// <summary>资产净值四列。</summary>
        public decimal NetOpening => OriginOpening - DepOpening - ImpairOpening;

        public decimal NetIncrease => OriginIncrease - DepIncrease - ImpairIncrease;

        public decimal NetDecrease => OriginDecrease - DepDecrease - ImpairDecrease;

        public decimal NetClosing => OriginClosing - DepClosing - ImpairClosing;
    }

    /// <summary>资产核对总账行 DTO。</summary>
    public sealed class FixedAssetCheckRowDto
    {
        /// <summary>项目（会计科目 / 资产卡片 / 差异）。</summary>
        public string Item { get; init; } = null!;

        public string Name { get; init; } = null!;

        public decimal Opening { get; init; }

        public decimal Debit { get; init; }

        public decimal Credit { get; init; }

        public decimal Balance { get; init; }

        /// <summary>差异行以蓝色链接呈现。</summary>
        public bool IsDiff { get; init; }
    }

    /// <summary>期间计提结果。</summary>
    public sealed class DepreciatePeriodResultDto
    {
        public int Period { get; init; }

        public int DepreciatedCount { get; init; }

        public IReadOnlyList<string> Skipped { get; init; } = [];
    }

    /// <summary>资产凭证科目设置 DTO。</summary>
    public sealed class FixedAssetVoucherSettingDto
    {
        public Guid? DepreciationExpenseAccountId { get; init; }

        public Guid? DepreciationAccountId { get; init; }

        public Guid? AmortizationExpenseAccountId { get; init; }

        public Guid? AmortizationAccountId { get; init; }

        public Guid? DisposalAccountId { get; init; }

        /// <summary>折旧费用科目（配置值或自动推断值）。</summary>
        public string? DepreciationExpenseAccountText { get; init; }

        public string? DepreciationAccountText { get; init; }

        public string? AmortizationExpenseAccountText { get; init; }

        public string? AmortizationAccountText { get; init; }

        public string? DisposalAccountText { get; init; }

        /// <summary>折旧科目是否已解析到（false 表示账套缺科目，需先补科目）。</summary>
        public bool DepreciationResolved { get; init; }

        public bool AmortizationResolved { get; init; }

        public bool DisposalResolved { get; init; }
    }

    /// <summary>保存资产凭证科目设置请求（留空回落到自动推断）。</summary>
    public sealed class SaveFixedAssetVoucherSettingRequest
    {
        public Guid? DepreciationExpenseAccountId { get; init; }

        public Guid? DepreciationAccountId { get; init; }

        public Guid? AmortizationExpenseAccountId { get; init; }

        public Guid? AmortizationAccountId { get; init; }

        public Guid? DisposalAccountId { get; init; }
    }

    /// <summary>折旧凭证生成请求。</summary>
    public sealed class GenerateDepreciationVoucherRequest
    {
        /// <summary>计提期间（yyyyMM）。</summary>
        public int Period { get; init; }

        /// <summary>凭证字，默认「记」。</summary>
        public string? Word { get; init; }

        public string Preparer { get; init; } = "系统";
    }

    /// <summary>某期间的折旧凭证信息（用于界面回显「折旧凭证」列）。</summary>
    public sealed class DepreciationVoucherInfoDto
    {
        public int Period { get; init; }

        /// <summary>本期已计提的变动条数。</summary>
        public int ChangeCount { get; init; }

        /// <summary>本期计提金额合计。</summary>
        public decimal TotalAmount { get; init; }

        /// <summary>本期折旧凭证是否已生成。</summary>
        public bool Generated { get; init; }

        public Guid? VoucherId { get; init; }

        public string? VoucherText { get; init; }
    }

    /// <summary>变更凭证分录（预览 / 提交共用）。</summary>
    public sealed class ChangeVoucherEntryDto
    {
        public string Summary { get; init; } = string.Empty;

        public Guid AccountId { get; init; }

        public string AccountText { get; init; } = string.Empty;

        public decimal Debit { get; init; }

        public decimal Credit { get; init; }
    }

    /// <summary>变更记录生成凭证的预览（含建议分录，界面可改科目与金额）。</summary>
    public sealed class ChangeVoucherPreviewDto
    {
        public Guid ChangeId { get; init; }

        public string KindText { get; init; } = null!;

        public string AssetCode { get; init; } = null!;

        public string AssetName { get; init; } = null!;

        public DateOnly Date { get; init; }

        public IReadOnlyList<ChangeVoucherEntryDto> Entries { get; init; } = [];

        /// <summary>无法自动给出分录时的说明（界面仅允许手工录入）。</summary>
        public string? Hint { get; init; }
    }

    /// <summary>变更记录生成凭证请求。</summary>
    public sealed class GenerateChangeVoucherRequest
    {
        public IReadOnlyList<ChangeVoucherEntryInput> Entries { get; init; } = [];

        public DateOnly? Date { get; init; }

        public string? Word { get; init; }

        public string Preparer { get; init; } = "系统";
    }

    /// <summary>变更凭证分录输入。</summary>
    public sealed class ChangeVoucherEntryInput
    {
        public string? Summary { get; init; }

        public Guid AccountId { get; init; }

        public decimal Debit { get; init; }

        public decimal Credit { get; init; }
    }

    /// <summary>资产批量操作结果。</summary>
    public sealed class FixedAssetBulkResultDto
    {
        public int Affected { get; init; }

        public int Skipped { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = [];
    }

    /// <summary>资产导入结果。</summary>
    public sealed class FixedAssetImportResultDto
    {
        public int Imported { get; init; }

        public int Updated { get; init; }

        public IReadOnlyList<FixedAssetImportFailureDto> Failures { get; init; } = [];
    }

    /// <summary>资产导入失败明细。</summary>
    public sealed class FixedAssetImportFailureDto
    {
        public int Row { get; init; }

        public string Reason { get; init; } = null!;
    }

    /// <summary>资产导入行（由 Excel 解析而来）。</summary>
    public sealed class FixedAssetImportRow
    {
        public int Row { get; init; }

        public string? Code { get; init; }

        public string? CategoryCode { get; init; }

        public string? CategoryName { get; init; }

        public string? Name { get; init; }

        public string? Model { get; init; }

        public string? Dept { get; init; }

        public string? StartDate { get; init; }

        public string? OriginValue { get; init; }

        public string? AccumulatedDepreciation { get; init; }
    }

    /// <summary>批量操作请求（资产 / 类别通用）。</summary>
    public sealed class FixedAssetBulkIdsRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];
    }

    /// <summary>复制资产请求。</summary>
    public sealed class CopyFixedAssetRequest
    {
        /// <summary>新资产编号；留空时自动在原名后追加序号。</summary>
        public string? Code { get; init; }

        /// <summary>新资产名称；留空时沿用原名称。</summary>
        public string? Name { get; init; }
    }
}
