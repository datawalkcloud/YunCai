using System.Globalization;
using YunCai.SharedKernel.Infrastructure.Pdf;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>打印稿页头需要的账套信息：资金各 DTO 不带账套名，由控制器注入。</summary>
    public sealed record FundPrintContext(string AccountSetName, string? CompanyName = null);

    /// <summary>
    /// 日记账「打印列表」的查询条件（从 query string 绑定）。
    /// <see cref="Columns"/> 是页面表头齿轮里当前可见的列键，其余条件按页面上的两套筛选项命名：
    /// 不带前缀的是列头漏斗（<c>filters</c>），<c>adv</c> 前缀的是区间控件里的高级筛选（<c>advanced</c>）。
    /// </summary>
    public sealed class JournalPrintQuery
    {
        /// <summary>业务日期区间起；与 <see cref="EndDate"/> 都为空时列示全部流水。</summary>
        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        /// <summary>可见列键（逗号分隔），省略时取页面默认显示的九列。</summary>
        public string? Columns { get; set; }

        /// <summary>序号列前缀（现金「1-001」/ 银行「2-001」），与页面序号列同口径。</summary>
        public string? SeqPrefix { get; set; }

        public string? Date { get; set; }
        public string? Summary { get; set; }
        public string? Category { get; set; }
        public string? Counterparty { get; set; }

        /// <summary>金额类漏斗按页面显示文本（千分位）做包含匹配，与列头输入框的行为一致。</summary>
        public string? Income { get; set; }

        public string? Expense { get; set; }
        public string? Balance { get; set; }
        public string? Voucher { get; set; }
        public string? Seq { get; set; }

        public DateOnly? AdvVoucherDateFrom { get; set; }
        public DateOnly? AdvVoucherDateTo { get; set; }
        public string? AdvVoucherWord { get; set; }
        public int? AdvVoucherNoFrom { get; set; }
        public int? AdvVoucherNoTo { get; set; }
        public string? AdvCategory { get; set; }
        public string? AdvSettlementMethod { get; set; }
        public string? AdvCounterparty { get; set; }
        public string? AdvBillNo { get; set; }
        public string? AdvProject { get; set; }
        public string? AdvDepartment { get; set; }
        public string? AdvSummary { get; set; }
        public string? AdvRemark { get; set; }
        public string? AdvSource { get; set; }
        public decimal? AdvAmountFrom { get; set; }
        public decimal? AdvAmountTo { get; set; }

        /// <summary>凭证状态：yes=已生成凭证，no=未生成凭证，其余值不过滤。</summary>
        public string? AdvHasVoucher { get; set; }
    }

    /// <summary>内部转账「打印列表」的查询条件，与页面上的期间开关和列头漏斗同名。</summary>
    public sealed class TransferPrintQuery
    {
        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        /// <summary>显示全部：为 true 时忽略日期区间，与页面勾选框一致。</summary>
        public bool? ShowAll { get; set; }

        public string? Date { get; set; }
        public string? Summary { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Amount { get; set; }
        public string? Voucher { get; set; }
    }

    /// <summary>
    /// 资金出纳打印 PDF 的表格投影：列名、分组表头、期初行 / 小计行 / 合计行按各页屏幕表格 1:1 描述，
    /// 版式（纸张、边框、跨页续表、页脚页码与打印时间）由 <see cref="PrintSheetPdfBuilder"/> 一处负责。
    /// 与 xlsx 导出（<c>JournalExportBuilder</c> / <c>FundReportExportBuilder</c> 等压平成单行表头）不共用列定义，
    /// 打印稿保留页面上的列层次与筛选结果。
    /// </summary>
    public static class FundsPdfProjector
    {
        /// <summary>日记账账页的全部列（顺序与页面 allColumns 一致，「操作」「设置」两列不参与打印）。</summary>
        private static readonly (string Key, PrintColumn Column)[] JournalColumns =
        [
            ("date", new PrintColumn("日期", Width: "7%", Align: PdfAlign.Center)),
            ("summary", new PrintColumn("摘要", Width: "16%")),
            ("category", new PrintColumn("收支类别", Width: "9%")),
            ("counterparty", new PrintColumn("往来单位", Width: "11%")),
            ("income", new PrintColumn("收入（借方）", Width: "9%", Align: PdfAlign.Right)),
            ("expense", new PrintColumn("支出（贷方）", Width: "9%", Align: PdfAlign.Right)),
            ("balance", new PrintColumn("余额", Width: "9%", Align: PdfAlign.Right)),
            ("voucher", new PrintColumn("关联凭证", Width: "8%", Align: PdfAlign.Center)),
            ("seq", new PrintColumn("日记账序号", Width: "13%", Align: PdfAlign.Center)),
            ("settlementMethod", new PrintColumn("结算方式", Width: "8%")),
            ("billNo", new PrintColumn("票据号", Width: "8%")),
            ("project", new PrintColumn("项目", Width: "8%")),
            ("department", new PrintColumn("部门", Width: "8%")),
            ("remark", new PrintColumn("备注", Width: "10%")),
            ("source", new PrintColumn("流水来源", Width: "8%")),
            ("voucherDate", new PrintColumn("凭证日期", Width: "8%", Align: PdfAlign.Center)),
            ("voucherWord", new PrintColumn("凭证字", Width: "5%", Align: PdfAlign.Center)),
        ];

        /// <summary>页面默认收起的可选列之外的那几列：未显式传 columns 时按这组打印。</summary>
        private static readonly string[] DefaultJournalKeys =
        [
            "date", "summary", "category", "counterparty", "income", "expense", "balance", "voucher", "seq",
        ];

        /// <summary>资金报表的维度页签名（与页面六个页签、xlsx 工作表名同名）。</summary>
        private static string DimensionText(string dimension) => dimension switch
        {
            FundReportDimensions.Account => "按账户汇总",
            FundReportDimensions.Category => "按收支类别汇总",
            FundReportDimensions.Counterparty => "按往来单位汇总",
            FundReportDimensions.Project => "按项目汇总",
            FundReportDimensions.Department => "按部门汇总",
            FundReportDimensions.Combo => "按组合汇总",
            _ => "资金报表",
        };

        /* ---------------- 资金账户日记账：打印列表 ---------------- */

        /// <summary>
        /// 日记账账页：首行「上笔余额」、逐笔流水、末行「合计」，列与筛选结果同页面。
        /// </summary>
        /// <param name="categories">收支类别档案，用于还原页面上「收- / 支-」前缀。</param>
        public static PrintSheet JournalList(
            JournalDetailDto journal,
            IReadOnlyList<EntryCategoryDto> categories,
            JournalPrintQuery? query,
            FundPrintContext context)
        {
            var q = query ?? new JournalPrintQuery();
            var seqPrefix = string.IsNullOrWhiteSpace(q.SeqPrefix) ? "1-001" : q.SeqPrefix.Trim();
            var categoryTypes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var category in categories)
            {
                categoryTypes[category.Name ?? string.Empty] = category.Type;
            }

            var entries = journal.Entries.Where(e => MatchesJournal(e, q, seqPrefix, categoryTypes)).ToList();

            // 期初取筛选区间前一笔的余额（区间为空时回落账簿期初），与页面 openingBalance 同口径。
            var opening = journal.OpeningBalance;
            if (entries.Count > 0)
            {
                for (var i = 0; i < journal.Entries.Count; i++)
                {
                    if (journal.Entries[i].Id == entries[0].Id)
                    {
                        if (i > 0) opening = journal.Entries[i - 1].Balance;
                        break;
                    }
                }
            }

            var columns = JournalColumnsOf(q.Columns);
            var keys = columns.Select(c => c.Key).ToList();
            var rows = new List<PrintRow>
            {
                // 期初行：页面只在摘要列写「上笔余额」，余额列落在有值时显示。
                new(keys.Select(key => new PrintCell(JournalOpeningValue(key, opening))).ToList()),
            };

            rows.AddRange(entries.Select(e => new PrintRow(
                keys.Select(key => new PrintCell(JournalCellValue(key, e, seqPrefix, categoryTypes))).ToList())));

            var totalIncome = entries.Sum(e => e.Income);
            var totalExpense = entries.Sum(e => e.Expense);
            var closing = opening + totalIncome - totalExpense;
            rows.Add(new PrintRow(
                keys.Select(key => new PrintCell(JournalTotalValue(key, totalIncome, totalExpense, closing))).ToList(),
                PdfCss.Strong));

            var accountLabel = AccountLabel(journal.AccountCode, journal.AccountName);
            var head = new List<PrintMeta>();
            if (!string.IsNullOrWhiteSpace(context.CompanyName)) head.Add(new PrintMeta($"编制单位：{context.CompanyName}"));
            head.Add(new PrintMeta($"账套：{context.AccountSetName}", PdfAlign.Center));
            head.Add(new PrintMeta($"账户：{accountLabel}", PdfAlign.Center));
            head.Add(new PrintMeta($"期间：{PeriodText(q.StartDate, q.EndDate)}", PdfAlign.Right));

            return new PrintSheet
            {
                Title = string.IsNullOrWhiteSpace(journal.TypeText) ? journal.Name : journal.TypeText,
                Meta = head,
                Columns = columns.Select(c => c.Column).ToList(),
                Rows = rows,
                Notes = ["单位：元", $"币别：{journal.Currency}"],
                // 默认九列已接近 A4 横向的容量，勾选了可选列更要横向，与页面账页的观感一致。
                Landscape = keys.Count > 6,
                PrintedAt = DateTimeOffset.Now,
                EmptyText = "该区间暂无流水，请调整日期或筛选条件。",
            };
        }

        /// <summary>收/付款凭据要打印的分录（按勾选顺序原样列示）；未指定标识时打印整本账簿。</summary>
        public static IReadOnlyList<JournalEntryDto> ReceiptEntries(JournalDetailDto journal, IReadOnlyList<Guid> entryIds)
        {
            if (entryIds.Count == 0) return journal.Entries;

            // 保持前端传来的顺序：用户勾选的先后即是凭据的打印顺序。
            var position = new Dictionary<Guid, int>();
            for (var i = 0; i < entryIds.Count; i++) position.TryAdd(entryIds[i], i);
            return journal.Entries
                .Where(e => position.ContainsKey(e.Id))
                .OrderBy(e => position[e.Id])
                .ToList();
        }

        /* ---------------- 资金报表打印 ---------------- */

        /// <summary>
        /// 资金报表：一张表列出全部分段，每段一段小标题（页面是分段的若干张小表）+ 段末小计行，
        /// 多段时末尾补总计行，与页面上的表格层级一致。
        /// </summary>
        /// <param name="showPrimarySubtotal">组合维度页面上的「显示主类型合计」。</param>
        public static PrintSheet Report(FundReportDto report, bool showPrimarySubtotal, FundPrintContext context)
        {
            var dim = report.Dimension;
            var withCode = dim == FundReportDimensions.Account || dim == FundReportDimensions.Category;
            var withOpening = dim == FundReportDimensions.Account;
            var isCombo = dim == FundReportDimensions.Combo;

            var columns = new List<PrintColumn>();
            if (withCode) columns.Add(new PrintColumn("编码", Width: "10%", Align: PdfAlign.Center));
            columns.Add(new PrintColumn(ReportNameLabel(report), Width: withCode ? "20%" : "26%"));
            if (isCombo) columns.Add(new PrintColumn(report.SecondaryText ?? "项目", Width: "18%"));
            if (withOpening) columns.Add(new PrintColumn("期初余额", Width: "12%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("收入总额", Width: "12%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("支出总额", Width: "12%", Align: PdfAlign.Right));
            if (withOpening) columns.Add(new PrintColumn("余额", Width: "12%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("收入笔数", Width: "8%"));
            columns.Add(new PrintColumn("支出笔数", Width: "8%"));

            var rows = new List<PrintRow>();
            foreach (var section in report.Sections)
            {
                rows.Add(new PrintRow([new PrintCell(section.Name, ColSpan: columns.Count, Css: PdfCss.Strong)]));
                if (section.Rows.Count == 0)
                {
                    continue;
                }

                rows.AddRange(section.Rows.Select(row => new PrintRow(ReportCells(row, columns.Count, dim, withCode, withOpening, isCombo))));
                if (showPrimarySubtotal && isCombo) rows.AddRange(PrimarySubtotals(section.Rows, columns.Count));

                rows.Add(new PrintRow(
                    ReportCells(section.Subtotal, columns.Count, dim, withCode, withOpening, isCombo),
                    PdfCss.Strong));
            }

            if (report.Sections.Count > 1)
            {
                rows.Add(new PrintRow(
                    ReportCells(report.Total, columns.Count, dim, withCode, withOpening, isCombo),
                    PdfCss.Strong));
            }

            var head = new List<PrintMeta>();
            if (!string.IsNullOrWhiteSpace(context.CompanyName)) head.Add(new PrintMeta($"编制单位：{context.CompanyName}"));
            head.Add(new PrintMeta($"账套：{context.AccountSetName}", PdfAlign.Center));
            head.Add(new PrintMeta($"期间：{report.StartDate:yyyy-MM-dd} 至 {report.EndDate:yyyy-MM-dd}", PdfAlign.Right));

            var notes = new List<string>();
            if (!string.IsNullOrWhiteSpace(report.CurrencyNote)) notes.Add(report.CurrencyNote);
            if (!string.IsNullOrWhiteSpace(report.Note)) notes.Add($"提示：{report.Note}");

            return new PrintSheet
            {
                Title = DimensionText(dim),
                Meta = head,
                Columns = columns,
                Rows = rows,
                Notes = notes,
                Landscape = true,
                PrintedAt = DateTimeOffset.Now,
                EmptyText = "该区间暂无收支数据，请调整日期或账户筛选。",
            };
        }

        /* ---------------- 出纳核对表打印 ---------------- */

        /// <summary>核对总账：不平的账户在名称后标注并在余额后附差异，末尾一行「合计（N 个账户）」。</summary>
        public static PrintSheet Reconciliation(FundReconciliationDto data, bool onlyUnbalanced, FundPrintContext context)
        {
            var rows = onlyUnbalanced ? data.Rows.Where(r => !r.Balanced).ToList() : data.Rows.ToList();

            var columns = new List<PrintColumn>
            {
                new("项目", Width: "10%"),
                new("名称", Width: "26%"),
                new("币别", Width: "8%", Align: PdfAlign.Center),
                new("期初余额", Width: "12%", Align: PdfAlign.Right),
                new("借方(收入)", Width: "12%", Align: PdfAlign.Right),
                new("贷方(支出)", Width: "12%", Align: PdfAlign.Right),
                new("余额", Width: "18%", Align: PdfAlign.Right),
            };

            var body = rows
                .Select(r => new PrintRow(
                [
                    new PrintCell(r.ProjectText),
                    new PrintCell(r.Balanced ? r.JournalName : $"{r.JournalName}（不平）"),
                    new PrintCell(string.IsNullOrWhiteSpace(r.Currency) ? "人民币" : r.Currency, Align: PdfAlign.Center),
                    new PrintCell(r.OpeningBalance),
                    new PrintCell(r.Debit),
                    new PrintCell(r.Credit),
                    // 平账时只给余额，不平时的余额列同页面附上差异。
                    new PrintCell(r.Balanced
                        ? PdfText.AmountAlways(r.Balance)
                        : $"{PdfText.AmountAlways(r.Balance)}(差异 {PdfText.AmountAlways(r.Difference)})"),
                ]))
                .ToList();

            body.Add(new PrintRow(
            [
                new PrintCell($"合计（{rows.Count} 个账户）", ColSpan: 3, Css: PdfCss.Strong),
                new PrintCell(PdfText.AmountAlways(rows.Sum(r => r.OpeningBalance)), Css: PdfCss.Strong),
                new PrintCell(PdfText.AmountAlways(rows.Sum(r => r.Debit)), Css: PdfCss.Strong),
                new PrintCell(PdfText.AmountAlways(rows.Sum(r => r.Credit)), Css: PdfCss.Strong),
                new PrintCell(PdfText.AmountAlways(rows.Sum(r => r.Balance)), Css: PdfCss.Strong),
            ]));

            var head = new List<PrintMeta>();
            if (!string.IsNullOrWhiteSpace(context.CompanyName)) head.Add(new PrintMeta($"编制单位：{context.CompanyName}"));
            head.Add(new PrintMeta($"账套：{context.AccountSetName}", PdfAlign.Center));
            head.Add(new PrintMeta($"会计期间：{data.StartDate:yyyy年MM月}", PdfAlign.Center));
            head.Add(new PrintMeta($"核对日：{data.Date:yyyy-MM-dd}", PdfAlign.Right));

            var diffCount = rows.Count(r => !r.Balanced);
            var notes = new List<string>
            {
                data.HasUnlinkedAccounts
                    ? "现金和银行账户没有关联科目，无法展示核对总账数据。"
                    : rows.Count == 0
                        ? "暂无资金账户，请先在「账户设置」中建账。"
                        : diffCount > 0
                            ? $"共 {rows.Count} 个资金账户，其中 {diffCount} 个与总账不一致。"
                            : $"共 {rows.Count} 个资金账户，出纳账与总账全部一致。",
                "账面余额取总账正常凭证，差异 = 出纳余额 − 账面余额。",
            };

            return new PrintSheet
            {
                Title = "核对总账",
                Meta = head,
                Columns = columns,
                Rows = body,
                Notes = notes,
                Landscape = true,
                PrintedAt = DateTimeOffset.Now,
                EmptyText = "暂无核对数据。",
            };
        }

        /* ---------------- 内部转账打印 ---------------- */

        /// <summary>内部转账台账：日期 / 摘要 / 两端账户 / 金额 / 备注 / 关联凭证，末行合计。</summary>
        public static PrintSheet Transfers(IReadOnlyList<TransferDto> transfers, TransferPrintQuery? query, FundPrintContext context)
        {
            var q = query ?? new TransferPrintQuery();
            var rows = transfers.Where(t => MatchesTransfer(t, q)).ToList();

            var columns = new List<PrintColumn>
            {
                new("日期", Width: "11%", Align: PdfAlign.Center),
                new("摘要", Width: "22%"),
                new("转出账户", Width: "14%"),
                new("转入账户", Width: "14%"),
                new("金额", Width: "13%", Align: PdfAlign.Right),
                new("备注", Width: "14%"),
                new("关联凭证", Width: "11%", Align: PdfAlign.Center),
            };

            var body = rows
                .Select(t => new PrintRow(
                [
                    new PrintCell(t.Date, Align: PdfAlign.Center),
                    new PrintCell(t.Summary),
                    new PrintCell(t.FromJournalName),
                    new PrintCell(t.ToJournalName),
                    new PrintCell($"¥{PdfText.AmountAlways(t.Amount)}", Align: PdfAlign.Right),
                    new PrintCell(t.Remark),
                    // 页面上未生成凭证的行显示「—」，打印稿照搬。
                    new PrintCell(string.IsNullOrWhiteSpace(t.VoucherNo) ? "—" : t.VoucherNo, Align: PdfAlign.Center),
                ]))
                .ToList();

            var amount = rows.Sum(t => t.Amount);
            body.Add(new PrintRow(
            [
                new PrintCell("合计", ColSpan: 4, Css: PdfCss.Strong),
                new PrintCell($"¥{PdfText.AmountAlways(amount)}", Css: PdfCss.Strong),
                PrintCell.Blank,
                PrintCell.Blank,
            ]));

            var head = new List<PrintMeta>();
            if (!string.IsNullOrWhiteSpace(context.CompanyName)) head.Add(new PrintMeta($"编制单位：{context.CompanyName}"));
            head.Add(new PrintMeta($"账套：{context.AccountSetName}", PdfAlign.Center));
            head.Add(new PrintMeta($"期间：{TransferPeriodText(q)}", PdfAlign.Right));

            return new PrintSheet
            {
                Title = "内部转账",
                Meta = head,
                Columns = columns,
                Rows = body,
                Notes = [$"转账笔数：{rows.Count}　金额合计：¥{PdfText.AmountAlways(amount)}　未生成凭证：{rows.Count(t => t.VoucherId is null)}"],
                Landscape = true,
                PrintedAt = DateTimeOffset.Now,
                EmptyText = "该区间暂无内部转账。",
            };
        }

        /* ---------------- 取数与投影细节 ---------------- */

        private static IReadOnlyList<(string Key, PrintColumn Column)> JournalColumnsOf(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return JournalColumns.Where(c => DefaultJournalKeys.Contains(c.Key)).ToList();
            }

            var keys = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var picked = keys
                .Select(key => JournalColumns.FirstOrDefault(c => c.Key == key))
                .Where(c => c.Key is not null)
                .ToList();

            // 传了未知列名（如页面遗留的 ops / 设置列）时回落默认列，避免出现空白表。
            return picked.Count > 0 ? picked : JournalColumns.Where(c => DefaultJournalKeys.Contains(c.Key)).ToList();
        }

        /// <summary>列头漏斗 + 高级筛选，逐条复刻页面 filtered 的判定口径。</summary>
        private static bool MatchesJournal(JournalEntryDto e, JournalPrintQuery q, string seqPrefix, IReadOnlyDictionary<string, int> categoryTypes)
        {
            var date = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (q.StartDate is { } from && e.Date < from) return false;
            if (q.EndDate is { } to && e.Date > to) return false;
            if (!Contains(date, q.Date)) return false;
            if (!Contains(e.Summary, q.Summary)) return false;
            if (!Contains(e.Category, q.Category)) return false;
            if (!Contains(e.Counterparty, q.Counterparty)) return false;
            if (!Contains(Money(e.Income), q.Income)) return false;
            if (!Contains(Money(e.Expense), q.Expense)) return false;
            if (!Contains(Money(e.Balance), q.Balance)) return false;
            if (!Contains(e.VoucherNo, q.Voucher)) return false;
            if (!Contains(SeqOf(e, seqPrefix), q.Seq)) return false;

            var voucherDate = e.VoucherDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (q.AdvVoucherDateFrom is not null || q.AdvVoucherDateTo is not null)
            {
                if (voucherDate is null) return false;
                if (q.AdvVoucherDateFrom is { } vdFrom && e.VoucherDate < vdFrom) return false;
                if (q.AdvVoucherDateTo is { } vdTo && e.VoucherDate > vdTo) return false;
            }

            if (!string.IsNullOrWhiteSpace(q.AdvVoucherWord) && e.VoucherWord != q.AdvVoucherWord) return false;
            // 页面把「无凭证号」按 0 参与区间比较，这里同样先补 0 再比。
            if (q.AdvVoucherNoFrom is { } noFrom && (e.VoucherNumber ?? 0) < noFrom) return false;
            if (q.AdvVoucherNoTo is { } noTo && (e.VoucherNumber ?? 0) > noTo) return false;
            if (!Contains(e.Category, q.AdvCategory)) return false;
            if (!Contains(e.SettlementMethod, q.AdvSettlementMethod)) return false;
            if (!Contains(e.Counterparty, q.AdvCounterparty)) return false;
            if (!Contains(e.BillNo, q.AdvBillNo)) return false;
            if (!Contains(e.Project, q.AdvProject)) return false;
            if (!Contains(e.Department, q.AdvDepartment)) return false;
            if (!Contains(e.Summary, q.AdvSummary)) return false;
            if (!Contains(e.Remark, q.AdvRemark)) return false;

            var source = e.Source ?? "手工登记";
            if (!string.IsNullOrWhiteSpace(q.AdvSource) && source != q.AdvSource) return false;

            // 金额区间取「有收入用收入、否则用支出」，与页面高级筛选一致。
            var amount = e.Income > 0m ? e.Income : e.Expense;
            if (q.AdvAmountFrom is { } amountFrom && amount < amountFrom) return false;
            if (q.AdvAmountTo is { } amountTo && amount > amountTo) return false;

            return q.AdvHasVoucher switch
            {
                "yes" => e.VoucherId is not null,
                "no" => e.VoucherId is null,
                _ => true,
            };
        }

        private static bool MatchesTransfer(TransferDto t, TransferPrintQuery q)
        {
            var date = t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (q.ShowAll != true && q.StartDate is { } from && t.Date < from) return false;
            if (q.ShowAll != true && q.EndDate is { } to && t.Date > to) return false;
            if (!Contains(date, q.Date)) return false;
            if (!Contains(t.Summary, q.Summary)) return false;
            if (!Contains(t.FromJournalName, q.From)) return false;
            if (!Contains(t.ToJournalName, q.To)) return false;
            if (!Contains(t.Amount.ToString(CultureInfo.InvariantCulture), q.Amount)) return false;
            return Contains(t.VoucherNo, q.Voucher);
        }

        private static object? JournalOpeningValue(string key, decimal opening) => key switch
        {
            "summary" => "上笔余额",
            "balance" => opening,
            _ => null,
        };

        private static object? JournalTotalValue(string key, decimal income, decimal expense, decimal closing) => key switch
        {
            // 合计行的三个金额页面强制列示（0 也显示 0.00），故传已格式化的文本。
            "summary" => "合计",
            "income" => PdfText.AmountAlways(income),
            "expense" => PdfText.AmountAlways(expense),
            "balance" => PdfText.AmountAlways(closing),
            _ => null,
        };

        private static object? JournalCellValue(
            string key,
            JournalEntryDto e,
            string seqPrefix,
            IReadOnlyDictionary<string, int> categoryTypes) => key switch
        {
            "date" => e.Date,
            "summary" => e.Summary,
            "category" => CategoryText(e.Category, categoryTypes),
            "counterparty" => e.Counterparty,
            "income" => e.Income,
            "expense" => e.Expense,
            "balance" => e.Balance,
            "voucher" => e.VoucherNo,
            "seq" => SeqOf(e, seqPrefix),
            "settlementMethod" => e.SettlementMethod,
            "billNo" => e.BillNo,
            "project" => e.Project,
            "department" => e.Department,
            "remark" => e.Remark,
            "source" => e.Source ?? "手工登记",
            "voucherDate" => e.VoucherDate,
            "voucherWord" => e.VoucherWord,
            _ => null,
        };

        /// <summary>收 / 支类别在页面上的显示名带方向前缀，档案里查不到的历史数据按原名显示。</summary>
        private static string? CategoryText(string? name, IReadOnlyDictionary<string, int> categoryTypes)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            return categoryTypes.TryGetValue(name, out var type)
                ? $"{(type == 2 ? "支" : "收")}-{name}"
                : name;
        }

        private static string SeqOf(JournalEntryDto e, string seqPrefix)
            => $"{seqPrefix}-{e.Date:yyyyMMdd}-{e.Sequence}";

        private static List<PrintCell> ReportCells(
            FundReportRowDto row,
            int columnCount,
            string dimension,
            bool withCode,
            bool withOpening,
            bool isCombo)
        {
            var cells = new List<PrintCell>();
            if (withCode) cells.Add(new PrintCell(row.Code, Align: PdfAlign.Center));
            cells.Add(new PrintCell(row.Name));
            if (isCombo) cells.Add(new PrintCell(row.SecondaryName));
            if (withOpening) cells.Add(new PrintCell(row.OpeningBalance));
            cells.Add(new PrintCell(row.TotalIncome));
            cells.Add(new PrintCell(row.TotalExpense));
            if (withOpening) cells.Add(new PrintCell(PdfText.AmountAlways(row.Balance)));
            cells.Add(new PrintCell(row.IncomeCount));
            cells.Add(new PrintCell(row.ExpenseCount));

            // 小计行不带编码，其余维度也只有名称列；这里按列数补齐，保证边框完整。
            while (cells.Count < columnCount) cells.Add(PrintCell.Blank);
            return cells;
        }

        /// <summary>组合维度「显示主类型合计」：同主类别的连续行末尾补一行小计（次类别列为空）。</summary>
        private static IEnumerable<PrintRow> PrimarySubtotals(IReadOnlyList<FundReportRowDto> rows, int columnCount)
        {
            var index = 0;
            while (index < rows.Count)
            {
                var primary = rows[index].Name;
                var next = index;
                while (next < rows.Count && rows[next].Name == primary) next++;
                var group = rows.Skip(index).Take(next - index).ToList();
                yield return new PrintRow(ReportCells(
                    new FundReportRowDto
                    {
                        Name = $"{primary}合计",
                        TotalIncome = group.Sum(r => r.TotalIncome),
                        TotalExpense = group.Sum(r => r.TotalExpense),
                        IncomeCount = group.Sum(r => r.IncomeCount),
                        ExpenseCount = group.Sum(r => r.ExpenseCount),
                    },
                    columnCount,
                    FundReportDimensions.Combo,
                    withCode: false,
                    withOpening: false,
                    isCombo: true),
                    PdfCss.Strong);
                index = next;
            }
        }

        private static string ReportNameLabel(FundReportDto report) => report.Dimension switch
        {
            FundReportDimensions.Account => "账户",
            FundReportDimensions.Category => "收支类别",
            FundReportDimensions.Counterparty => "往来单位",
            FundReportDimensions.Project => "项目",
            FundReportDimensions.Department => "部门",
            FundReportDimensions.Combo => report.PrimaryText ?? "收支类别",
            _ => report.Sections.FirstOrDefault()?.Name ?? "名称",
        };

        private static string TransferPeriodText(TransferPrintQuery q)
            => q.ShowAll == true || (q.StartDate is null && q.EndDate is null)
                ? "全部期间"
                : PeriodText(q.StartDate, q.EndDate);

        private static string PeriodText(DateOnly? start, DateOnly? end)
            => start is null && end is null
                ? "全部期间"
                : $"{DateText(start) ?? "不限"} 至 {DateText(end) ?? "不限"}";

        private static string? DateText(DateOnly? date)
            => date is { } value ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

        private static string AccountLabel(string code, string name)
            => $"{code} - {name}".TrimStart(' ');

        /// <summary>与页面列头漏斗一致的包含匹配：条件为空即不过滤，区分大小写（同 JS includes）。</summary>
        private static bool Contains(string? haystack, string? needle)
            => string.IsNullOrEmpty(needle) || (haystack ?? string.Empty).Contains(needle, StringComparison.Ordinal);

        /// <summary>金额显示文本：0 留白（账页惯例），其余千分位两位小数，与页面 fmtAmount 同口径。</summary>
        private static string Money(decimal? value)
            => value is { } amount && amount != 0m
                ? amount.ToString("#,##0.00", CultureInfo.InvariantCulture)
                : string.Empty;

        /// <summary>凭据上的金额：收入优先，否则支出（与页面「打印」链接的判定一致）。</summary>
        public static decimal ReceiptAmount(JournalEntryDto entry) => entry.Income > 0m ? entry.Income : entry.Expense;
    }
}
