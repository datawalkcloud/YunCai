using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Closing.Domain;

namespace KuaiJi.Modules.Closing.Application
{
    /// <summary>期末结转与结账的总体状态：启用期间、已结转/已结账进度与逐月状态卡片。</summary>
    public sealed class ClosingStatusDto
    {
        /// <summary>账套启用期间（如 "2026-01"）。</summary>
        public string StartPeriod { get; init; } = "";

        /// <summary>已结转损益至期间（如 "2026-01"）；尚未结转时为 null。</summary>
        public string? CarriedPeriod { get; init; }

        /// <summary>已结账至期间（如 "2026-01"）；尚未结账时为 null。</summary>
        public string? ClosedPeriod { get; init; }

        /// <summary>下一个待结转损益期间——年份。</summary>
        public int NextCarryYear { get; init; }

        /// <summary>下一个待结转损益期间——月份。</summary>
        public int NextCarryMonth { get; init; }

        /// <summary>下一个待结账期间——年份。</summary>
        public int NextCloseYear { get; init; }

        /// <summary>下一个待结账期间——月份。</summary>
        public int NextCloseMonth { get; init; }

        /// <summary>是否可以反结账（存在已结期间）。</summary>
        public bool CanReopen { get; init; }

        /// <summary>是否可以撤销结转（最近一次结转尚未被结账覆盖）。</summary>
        public bool CanCancelCarry { get; init; }

        /// <summary>逐月状态列表（按时间顺序）。</summary>
        public IReadOnlyList<ClosingPeriodDto> Periods { get; init; } = [];
    }

    /// <summary>单个会计期间的结转/结账状态卡片。</summary>
    public sealed class ClosingPeriodDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间文本（如 "2026-01"）。</summary>
        public string PeriodText { get; init; } = "";

        /// <summary>状态：NotCarried（未结转损益、未结账）/ Carried（已结转损益、未结账）/ Closed（已结转损益、已结账）。</summary>
        public string State { get; init; } = "NotCarried";

        /// <summary>本期凭证张数（含未过账）。</summary>
        public int VoucherCount { get; init; }

        /// <summary>本期未过账凭证张数。</summary>
        public int UnpostedCount { get; init; }
    }

    /// <summary>期末检查结果：检查项清单与「可否结转 / 可否结账」结论。</summary>
    public sealed class ClosingCheckResultDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间文本（如 "2026-01"）。</summary>
        public string PeriodText { get; init; } = "";

        /// <summary>当前是否可执行结转损益。</summary>
        public bool CanCarry { get; init; }

        /// <summary>当前是否可执行结账。</summary>
        public bool CanClose { get; init; }

        /// <summary>检查项清单。</summary>
        public IReadOnlyList<ClosingCheckItemDto> Items { get; init; } = [];
    }

    /// <summary>单项期末检查结果。</summary>
    public sealed class ClosingCheckItemDto
    {
        /// <summary>检查项标识：preclose / carry / audit / numbering / report / funds / asset / depreciation。</summary>
        public string Key { get; init; } = "";

        /// <summary>检查项名称。</summary>
        public string Title { get; init; } = "";

        /// <summary>状态：ok（通过）/ warn（提醒）/ error（阻断）。</summary>
        public string Status { get; init; } = "ok";

        /// <summary>结果说明（含定位提示）。</summary>
        public string Message { get; init; } = "";

        /// <summary>分组：blocking（影响结账）/ info（仅提示）。</summary>
        public string Group { get; init; } = "blocking";

        /// <summary>可跳转查看的动作标识（如 depreciation / funds），无跳转时为 null。</summary>
        public string? ActionKey { get; init; }

        /// <summary>跳转动作文案（如「点击查看」）。</summary>
        public string? ActionText { get; init; }
    }

    /// <summary>期末检查（第 1 步）的结转项清单：预置结转项 + 工资模块结转项。</summary>
    public sealed class PreCloseListDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        public string PeriodText { get; init; } = "";

        public IReadOnlyList<PreCloseItemDto> Items { get; init; } = [];
    }

    /// <summary>单个预置结转项：显示金额并按需生成凭证。</summary>
    public sealed class PreCloseItemDto
    {
        /// <summary>结转项标识（sales-cost / payroll-accrual / salary-accrue / salary-pay / ...）。</summary>
        public string Key { get; init; } = "";

        /// <summary>结转项名称（与界面上的一致）。</summary>
        public string Title { get; init; } = "";

        /// <summary>按本期数据计算的应结转金额；不可用时为 0。</summary>
        public decimal Amount { get; init; }

        /// <summary>是否可按当前账套数据生成凭证。</summary>
        public bool Available { get; init; }

        /// <summary>生成方式：ledger（本模块直接生成）/ salary（工资模块生成）/ unsupported（暂不支持）。</summary>
        public string Source { get; init; } = "ledger";

        /// <summary>说明：不可用原因或计算口径。</summary>
        public string Hint { get; init; } = "";

        /// <summary>本期是否已生成过该结转项（仅 ledger 项可识别）。</summary>
        public bool Generated { get; init; }

        /// <summary>已生成凭证的字号（如「记-3」）。</summary>
        public string? VoucherLabel { get; init; }

        /// <summary>已生成凭证的标识（用于跳转打印）。</summary>
        public Guid? VoucherId { get; init; }
    }

    /// <summary>生成单个预置结转项凭证的请求。</summary>
    public sealed class GeneratePreCloseRequest
    {
        public int Year { get; init; }

        public int Month { get; init; }

        public string Key { get; init; } = "";
    }

    /// <summary>预置结转项的凭证生成结果。</summary>
    public sealed class GeneratePreCloseResultDto
    {
        public string Message { get; init; } = "";

        public CarryVoucherRefDto? Voucher { get; init; }
    }

    /// <summary>结转损益预览：展示将要生成的结转凭证，不落库。</summary>
    public sealed class CarryForwardPreviewDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        public string PeriodText { get; init; } = "";

        /// <summary>本期是否已完成结转（为 true 时 Vouchers 是已生成的结转凭证）。</summary>
        public bool AlreadyCarried { get; init; }

        /// <summary>是否存在需要生成的结转凭证；为 false 时界面提示「本期不需要结转损益」。</summary>
        public bool NeedsCarry { get; init; }

        /// <summary>本次结转的损益净额（正数为净利润，负数为净亏损）。</summary>
        public decimal ProfitAmount { get; init; }

        /// <summary>
        /// 年末「本年利润」余额（正数为贷方余额即盈利，负数为借方余额即亏损）。
        /// 仅在已结转的 12 月返回，用于年末补做结转未分配利润。
        /// </summary>
        public decimal RetainedProfitAmount { get; init; }

        /// <summary>本期是否已存在「结转未分配利润」凭证。</summary>
        public bool HasRetainedVoucher { get; init; }

        /// <summary>年末待生成的「本年利润 → 未分配利润」凭证预览；已生成或无需结转时为空。</summary>
        public CarryPreviewVoucherDto? RetainedVoucher { get; init; }

        public IReadOnlyList<CarryPreviewVoucherDto> Vouchers { get; init; } = [];

        /// <summary>预览阶段的提示（如存在未过账凭证）。</summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    /// <summary>预览中的一张结转凭证。</summary>
    public sealed class CarryPreviewVoucherDto
    {
        /// <summary>凭证来源：Template / Carry / Retained。</summary>
        public string Kind { get; init; } = "Carry";

        public string Word { get; init; } = "";

        public int Number { get; init; }

        public string Label { get; init; } = "";

        public DateOnly Date { get; init; }

        public string Summary { get; init; } = "";

        public decimal TotalDebit { get; init; }

        public decimal TotalCredit { get; init; }

        public IReadOnlyList<CarryPreviewLineDto> Lines { get; init; } = [];
    }

    /// <summary>预览中的一条结转分录。</summary>
    public sealed class CarryPreviewLineDto
    {
        public string Summary { get; init; } = "";

        public Guid AccountId { get; init; }

        public string AccountCode { get; init; } = "";

        public string AccountName { get; init; } = "";

        public decimal Debit { get; init; }

        public decimal Credit { get; init; }

        /// <summary>该行过账后的科目余额（按科目余额方向取正，贷方向为「贷 - 借」）。</summary>
        public decimal Balance { get; init; }
    }

    /// <summary>结转损益请求。</summary>
    public sealed class CarryForwardRequest
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>收入与成本费用分开生成两张结转凭证（默认合并为一张）。</summary>
        public bool SplitIncomeCost { get; init; }

        /// <summary>同时将「本年利润」余额结转至「未分配利润」（通常用于年末）。</summary>
        public bool TransferToRetainedEarnings { get; init; }

        /// <summary>本次结转同时执行的结转模板标识集合（空表示不执行模板）。</summary>
        public IReadOnlyList<Guid>? TemplateIds { get; init; }
    }

    /// <summary>结转损益结果。</summary>
    public sealed class CarryForwardResultDto
    {
        /// <summary>期间文本（如 "2026-01"）。</summary>
        public string PeriodText { get; init; } = "";

        /// <summary>生成的结转凭证（按生成顺序）。</summary>
        public IReadOnlyList<CarryVoucherRefDto> Vouchers { get; init; } = [];

        /// <summary>结转的损益净额（正数为净利润，负数为净亏损）。</summary>
        public decimal ProfitAmount { get; init; }

        /// <summary>结果说明。</summary>
        public string Message { get; init; } = "";
    }

    /// <summary>结转凭证引用（字、号与摘要）。</summary>
    public sealed class CarryVoucherRefDto
    {
        public Guid Id { get; init; }

        public string Word { get; init; } = "";

        public int Number { get; init; }

        /// <summary>显示文本（如 "转-3"）。</summary>
        public string Label { get; init; } = "";

        public string Summary { get; init; } = "";

        /// <summary>凭证来源：Template（结转模板）/ Carry（结转损益）/ Retained（结转未分配利润）。</summary>
        public string Kind { get; init; } = "Carry";
    }

    /// <summary>年末补做结转未分配利润的结果。</summary>
    public sealed class TransferRetainedResultDto
    {
        public string Message { get; init; } = "";

        public CarryVoucherRefDto? Voucher { get; init; }
    }

    /// <summary>结账 / 撤销结转等按期间的请求体。</summary>
    public sealed class ClosingPeriodRequest
    {
        public int Year { get; init; }

        public int Month { get; init; }
    }

    /// <summary>撤销结转结果。</summary>
    public sealed class CancelCarryResultDto
    {
        /// <summary>结果说明。</summary>
        public string Message { get; init; } = "";
    }

    /// <summary>结转模板（含行明细，科目按编码/名称解析）。</summary>
    public sealed class ClosingTemplateDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = "";

        public bool IsEnabled { get; init; }

        public IReadOnlyList<ClosingTemplateLineDto> Lines { get; init; } = [];
    }

    /// <summary>结转模板行。</summary>
    public sealed class ClosingTemplateLineDto
    {
        public Guid Id { get; init; }

        public int LineNumber { get; init; }

        public string Summary { get; init; } = "";

        public Guid AccountId { get; init; }

        public string AccountCode { get; init; } = "";

        public string AccountName { get; init; } = "";

        /// <summary>借贷方向（1 借 / 2 贷，与 BalanceDirection 一致）。</summary>
        public int Direction { get; init; }

        public string DirectionText { get; init; } = "";

        /// <summary>取数方式（1 固定金额 / 2 本期借方 / 3 本期贷方 / 4 期末余额）。</summary>
        public int SourceType { get; init; }

        public string SourceTypeText { get; init; } = "";

        public Guid? SourceAccountId { get; init; }

        public string? SourceAccountCode { get; init; }

        public string? SourceAccountName { get; init; }

        /// <summary>固定金额（仅 SourceType=1 时使用）。</summary>
        public decimal Amount { get; init; }
    }

    /// <summary>保存结转模板请求（新增与编辑共用；编辑时整体替换行）。</summary>
    public sealed class SaveClosingTemplateRequest
    {
        public string Name { get; init; } = "";

        public bool IsEnabled { get; init; } = true;

        public IReadOnlyList<SaveClosingTemplateLineRequest> Lines { get; init; } = [];
    }

    /// <summary>保存结转模板行。</summary>
    public sealed class SaveClosingTemplateLineRequest
    {
        public string Summary { get; init; } = "";

        public Guid AccountId { get; init; }

        public BalanceDirection Direction { get; init; }

        public ClosingTemplateSourceType SourceType { get; init; }

        public Guid? SourceAccountId { get; init; }

        /// <summary>固定金额（仅 SourceType=Fixed 时使用）。</summary>
        public decimal Amount { get; init; }
    }

    /// <summary>结转模板预览：按指定期间计算各模板将生成的金额与平衡情况。</summary>
    public sealed class ClosingTemplatePreviewDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        public string PeriodText { get; init; } = "";

        public IReadOnlyList<ClosingTemplatePreviewItemDto> Templates { get; init; } = [];
    }

    /// <summary>单个模板的预览结果。</summary>
    public sealed class ClosingTemplatePreviewItemDto
    {
        public Guid TemplateId { get; init; }

        public string Name { get; init; } = "";

        public bool IsEnabled { get; init; }

        public IReadOnlyList<ClosingTemplatePreviewLineDto> Lines { get; init; } = [];

        public decimal TotalDebit { get; init; }

        public decimal TotalCredit { get; init; }

        public bool Balanced { get; init; }

        /// <summary>计算/校验问题说明（不平衡或科目缺失时给出）。</summary>
        public string? Error { get; init; }
    }

    /// <summary>模板预览行（金额已按预览期间计算）。</summary>
    public sealed class ClosingTemplatePreviewLineDto
    {
        public string Summary { get; init; } = "";

        public Guid AccountId { get; init; }

        public string AccountCode { get; init; } = "";

        public string AccountName { get; init; } = "";

        public BalanceDirection Direction { get; init; }

        public decimal Amount { get; init; }
    }

    /// <summary>批量结账结果。</summary>
    public sealed class CloseBatchResultDto
    {
        public string Message { get; init; } = "";

        /// <summary>本次结账的期间列表（如 ["2026-02","2026-03"]）。</summary>
        public IReadOnlyList<string> ClosedPeriods { get; init; } = [];

        public ClosingStatusDto Status { get; init; } = null!;
    }

    /// <summary>批量标记结转的结果。</summary>
    public sealed class CarryBatchResultDto
    {
        public string Message { get; init; } = "";

        /// <summary>本次标记为已结转的期间列表（如 ["2026-01","2026-02"]）。</summary>
        public IReadOnlyList<string> MarkedPeriods { get; init; } = [];

        public ClosingStatusDto Status { get; init; } = null!;
    }
}
