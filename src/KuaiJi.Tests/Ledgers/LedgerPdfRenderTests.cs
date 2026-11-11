using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.SharedKernel.Infrastructure.Pdf;
using PaperSharp;
using Xunit;

namespace KuaiJi.Tests.Ledgers;

/// <summary>
/// 账页打印版式的样张输出：用构造好的 DTO 走一遍投影 + 渲染，把 PDF 与同源 PNG 落盘供肉眼核对。
/// 仅在设了 KUAIJI_PDF_ARTIFACTS 时写文件；CI 上只跑「能出 PDF 且体积合理」这条断言。
/// </summary>
public sealed class LedgerPdfRenderTests
{
    private static LedgerMetaDto Meta(string name = "杭州数有限公司") => new()
    {
        AccountSetName = "杭州数",
        CompanyName = name,
        StartDate = "2026-01-01",
        EndDate = "2026-03-31",
        PeriodText = "2026-01-01 至 2026-03-31",
        UnpostedVoucherCount = 1,
    };

    [Theory]
    [InlineData("subsidiary")]
    [InlineData("subsidiary-long")]
    [InlineData("general")]
    [InlineData("multi-column")]
    [InlineData("voucher-summary")]
    [InlineData("assist-balance")]
    [InlineData("assist-subsidiary")]
    public static void ProjectAndRender_ProducesPdf(string kind)
    {
        var sheet = SheetOf(kind);
        var pdf = PrintSheetPdfBuilder.Build(sheet);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 2_000, $"{kind} 的 PDF 体积异常偏小：{pdf.Length}");

        var dir = Environment.GetEnvironmentVariable("KUAIJI_PDF_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(dir)) return;

        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{kind}.pdf"), pdf);

        var (html, css) = PrintSheetPdfBuilder.BuildDocument(sheet);
        // A4 长边约 1123px @96dpi，横版要更宽的视口才不被裁掉。
        var png = Renderer.RenderPng(html, css, width: sheet.Landscape ? 1200 : 820, scale: 2);
        File.WriteAllBytes(Path.Combine(dir, $"{kind}.png"), png);
    }

    private static PrintSheet SheetOf(string kind) => kind switch
    {
        "subsidiary-long" => LedgerPdfProjector.Project(
            LedgerExportKind.Subsidiary,
            new SubsidiaryLedgerDto
            {
                Meta = Meta(),
                Accounts = [SubsidiaryAccount(60)],
            },
            new LedgerQueryOptions { ShowCounterAccount = true }),

        "voucher-summary" => LedgerPdfProjector.Project(
            LedgerExportKind.VoucherSummary,
            new VoucherSummaryDto
            {
                Meta = Meta(),
                VoucherCount = 12,
                AttachmentCount = 30,
                TotalDebit = 1_200m,
                TotalCredit = 1_200m,
                Rows =
                [
                    new VoucherSummaryRowDto { Code = "1001", Name = "库存现金", Debit = 600m, Credit = 0m, VoucherCount = 4 },
                    new VoucherSummaryRowDto { Code = "100101", Name = "人民币", ParentCode = "1001", Debit = 600m, Credit = 0m, VoucherCount = 4 },
                    new VoucherSummaryRowDto { Code = "1122", Name = "应收账款", Debit = 0m, Credit = 300m, VoucherCount = 5 },
                    new VoucherSummaryRowDto { Code = "6602", Name = "管理费用", Debit = 300m, Credit = 0m, VoucherCount = 3 },
                    new VoucherSummaryRowDto { Code = "660202", Name = "差旅费", ParentCode = "6602", Debit = 300m, Credit = 0m, VoucherCount = 3 },
                ],
            }),

        "assist-subsidiary" => LedgerPdfProjector.Project(
            LedgerExportKind.AssistSubsidiary,
            new AssistSubsidiaryDto
            {
                Meta = Meta(),
                AssistTypeText = "客户",
                Accounts =
                [
                    new AssistSubsidiaryAccountDto
                    {
                        Code = "1122",
                        Name = "应收账款",
                        Items =
                        [
                            new AssistSubsidiaryItemDto
                            {
                                Code = "KH001",
                                Name = "甲客户",
                                TotalDebit = 500m,
                                TotalCredit = 0m,
                                Rows =
                                [
                                    new SubsidiaryLedgerRowDto { Summary = "确认收入", Debit = 500m, Date = "2026-01-15", VoucherNo = "记-6", CounterAccount = "6001 主营业务收入", Balance = 1_500m, Direction = "借" },
                                ],
                            },
                            new AssistSubsidiaryItemDto
                            {
                                Code = "KH002",
                                Name = "乙客户",
                                TotalDebit = 0m,
                                TotalCredit = 200m,
                                Rows =
                                [
                                    new SubsidiaryLedgerRowDto { Summary = "收到还款", Credit = 200m, Date = "2026-02-03", VoucherNo = "记-11", CounterAccount = "1001 库存现金", Balance = 200m, Direction = "贷" },
                                ],
                            },
                        ],
                    },
                ],
            }),

        "general" => LedgerPdfProjector.Project(
            LedgerExportKind.General,
            new GeneralLedgerDto
            {
                Meta = Meta(),
                Accounts =
                [
                    new GeneralLedgerAccountDto
                    {
                        Code = "1001",
                        Name = "库存现金",
                        TotalDebit = 300m,
                        TotalCredit = 0m,
                        Rows =
                        [
                            new GeneralLedgerRowDto { PeriodText = "2026-01", OpeningDebit = 0m, Debit = 100m, EndingDebit = 100m, Direction = "借" },
                            new GeneralLedgerRowDto { PeriodText = "2026-02", OpeningDebit = 100m, Debit = 200m, EndingDebit = 300m, Direction = "借" },
                        ],
                    },
                    new GeneralLedgerAccountDto
                    {
                        Code = "6001",
                        Name = "主营业务收入",
                        TotalDebit = 0m,
                        TotalCredit = 300m,
                        Rows =
                        [
                            new GeneralLedgerRowDto { PeriodText = "2026-02", OpeningCredit = 0m, Credit = 300m, EndingCredit = 300m, Direction = "贷" },
                        ],
                    },
                ],
            },
            new LedgerQueryOptions { AccountFrom = "1001", AccountTo = "6602" },
            new LedgerPdfProjector.GeneralYearTotals(new Dictionary<string, (decimal Debit, decimal Credit)>
            {
                ["1001"] = (500m, 0m),
                ["6001"] = (0m, 500m),
            })),

        "multi-column" => LedgerPdfProjector.Project(
            LedgerExportKind.MultiColumn,
            new MultiColumnDto
            {
                Meta = Meta(),
                Code = "6602",
                Name = "管理费用",
                Path = "6602 管理费用",
                TotalDebit = 920m,
                TotalCredit = 0m,
                Columns =
                [
                    new MultiColumnHeaderDto { Code = "660201", Name = "办公费" },
                    new MultiColumnHeaderDto { Code = "660202", Name = "差旅费" },
                ],
                Rows =
                [
                    new MultiColumnRowDto { Date = "2026-01-08", VoucherNo = "记-1", Summary = "购买办公用品", Debits = [120m, 0m], Direction = "借", Balance = 120m },
                    new MultiColumnRowDto { Kind = SubsidiaryRowKind.MonthTotal, Summary = "本月合计", Debits = [120m, 800m], Credits = [0m, 0m], Direction = "借", Balance = 920m },
                ],
            }),

        "assist-balance" => LedgerPdfProjector.Project(
            LedgerExportKind.AssistBalance,
            new AssistBalanceDto
            {
                Meta = Meta(),
                AssistTypeText = "客户",
                Accounts =
                [
                    new AssistBalanceAccountDto
                    {
                        Code = "1122",
                        Name = "应收账款",
                        Rows =
                        [
                            new AssistBalanceRowDto { Code = "KH001", Name = "甲客户", OpeningDebit = 1_000m, PeriodDebit = 500m, EndingDebit = 1_500m, Direction = "借" },
                            new AssistBalanceRowDto { Code = "KH002", Name = "乙客户", OpeningDebit = 0m, PeriodCredit = 200m, EndingCredit = 200m, Direction = "贷" },
                        ],
                    },
                ],
            }),

        _ => LedgerPdfProjector.Project(
            LedgerExportKind.Subsidiary,
            new SubsidiaryLedgerDto
            {
                Meta = Meta(),
                Accounts =
                [
                    SubsidiaryAccount(0),
                    new SubsidiaryLedgerAccountDto
                    {
                        Code = "6001",
                        Name = "主营业务收入",
                        TotalDebit = 0m,
                        TotalCredit = 300m,
                        Rows =
                        [
                            new SubsidiaryLedgerRowDto { Summary = "确认收入", Credit = 300m, Date = "2026-03-02", VoucherNo = "记-31", Balance = 300m, Direction = "贷" },
                        ],
                    },
                ],
            },
            new LedgerQueryOptions { ShowCounterAccount = true })
    };

    /// <summary>造一个 1001 库存现金段：期初 + 若干逐笔（每月末「本月合计」）+ 本年累计。count=0 时用固定三行小样本。</summary>
    private static SubsidiaryLedgerAccountDto SubsidiaryAccount(int count)
    {
        if (count == 0)
        {
            return new SubsidiaryLedgerAccountDto
            {
                Code = "1001",
                Name = "库存现金",
                TotalDebit = 600m,
                TotalCredit = 0m,
                Rows =
                [
                    new SubsidiaryLedgerRowDto { Kind = SubsidiaryRowKind.Opening, Summary = "期初余额", Balance = 0m, Direction = "平" },
                    new SubsidiaryLedgerRowDto { Summary = "报销差旅费", Debit = 300m, CounterAccount = "660202 差旅费", Date = "2026-01-08", VoucherNo = "记-1", Balance = 300m, Direction = "借" },
                    new SubsidiaryLedgerRowDto { Summary = "收客户还款", Debit = 300m, CounterAccount = "1122 应收账款", Date = "2026-02-11", VoucherNo = "记-14", Balance = 600m, Direction = "借" },
                    new SubsidiaryLedgerRowDto { Kind = SubsidiaryRowKind.YearTotal, Summary = "本年累计", Debit = 600m, Balance = 600m, Direction = "借" },
                ],
            };
        }

        var rows = new List<SubsidiaryLedgerRowDto>
        {
            new() { Kind = SubsidiaryRowKind.Opening, Summary = "期初余额", Balance = 0m, Direction = "平" },
        };
        decimal balance = 0m;
        for (var i = 1; i <= count; i++)
        {
            balance += 100m;
            rows.Add(new SubsidiaryLedgerRowDto
            {
                Summary = $"第 {i} 笔现金收讫",
                Debit = 100m,
                CounterAccount = "1122 应收账款",
                Date = $"2026-{((i - 1) / 20) + 1:D2}-{(i % 28) + 1:D2}",
                VoucherNo = $"记-{i}",
                Balance = balance,
                Direction = "借",
            });
            if (i % 20 == 0)
            {
                rows.Add(new SubsidiaryLedgerRowDto { Kind = SubsidiaryRowKind.MonthTotal, Summary = "本月合计", Debit = 2_000m, Balance = balance, Direction = "借" });
            }
        }

        rows.Add(new SubsidiaryLedgerRowDto { Kind = SubsidiaryRowKind.YearTotal, Summary = "本年累计", Debit = balance, Balance = balance, Direction = "借" });

        return new SubsidiaryLedgerAccountDto
        {
            Code = "1001",
            Name = "库存现金",
            TotalDebit = balance,
            TotalCredit = 0m,
            Rows = rows,
        };
    }
}
