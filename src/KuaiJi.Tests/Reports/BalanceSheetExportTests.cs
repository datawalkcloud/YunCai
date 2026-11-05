using System.Text;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Reports.Infrastructure;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 资产负债表导出：版式选择、两栏行数与合计、以及 NPOI 生成的 .xls（BIFF8/OLE2）字节。
/// </summary>
public sealed class BalanceSheetExportTests
{
    private static BalanceSheetLineDto Line(int line, decimal ending, decimal opening = 0m)
        => new() { Line = line, Ending = ending, Opening = opening };

    [Fact]
    public void Templates_ResolveByAccountingStandard()
    {
        Assert.Equal(BalanceSheetTemplate.Enterprise, BalanceSheetTemplates.Resolve(AccountingStandard.Enterprise));
        Assert.Equal(BalanceSheetTemplate.SmallBusiness, BalanceSheetTemplates.Resolve(AccountingStandard.SmallBusiness));
        Assert.Equal("会企01表", BalanceSheetTemplates.Title(BalanceSheetTemplate.Enterprise));
        Assert.Equal("会小企01表", BalanceSheetTemplates.Title(BalanceSheetTemplate.SmallBusiness));
    }

    [Fact]
    public void SmallBusinessLayout_MatchesLemonTwoColumnShapeAndTotals()
    {
        var lines = new[]
        {
            Line(1, 20_010.18m, 20_101.82m),
            Line(33, 0m, 100m),          // 应付账款
            Line(48, 20_001m, 20_001m),  // 实收资本
            Line(51, 9.18m, 0.82m),      // 未分配利润
        };

        var (left, right) = BalanceSheetExportLayout.Build(BalanceSheetTemplate.SmallBusiness, lines);

        Assert.Equal(32, left.Count);
        Assert.Equal(32, right.Count);
        Assert.Equal(20_010.18m, left.Single(r => r.Line == 15).Ending); // 流动资产合计
        Assert.Equal(20_010.18m, left.Single(r => r.Line == 30).Ending); // 资产合计
        Assert.Equal(100m, right.Single(r => r.Line == 41).Opening);     // 流动负债合计
        Assert.Equal(20_010.18m, right.Single(r => r.Line == 52).Ending); // 所有者权益合计（实收资本 + 未分配利润）
        Assert.Equal(20_010.18m, right.Single(r => r.Line == 53).Ending);// 负债和所有者权益
    }

    [Fact]
    public void EnterpriseLayout_HasBalancedTotals()
    {
        var lines = new[]
        {
            Line(1, 100m),
            Line(35, 50m),  // 应付账款
            Line(55, 50m),  // 实收资本
        };

        var (left, right) = BalanceSheetExportLayout.Build(BalanceSheetTemplate.Enterprise, lines);

        Assert.Equal(34, left.Count);
        Assert.Equal(34, right.Count);
        Assert.Equal(100m, left.Single(r => r.Line == 31).Ending);  // 资产总计
        Assert.Equal(100m, right.Single(r => r.Line == 62).Ending); // 负债和所有者权益总计
    }

    [Fact]
    public void XlsBuilder_WritesOle2CompoundFile()
    {
        var (left, right) = BalanceSheetExportLayout.Build(
            BalanceSheetTemplate.SmallBusiness,
            [Line(1, 20_010.18m), Line(48, 20_001m), Line(51, 9.18m)]);

        var bytes = BalanceSheetXlsBuilder.Build(new BalanceSheetXlsModel(
            "测试1有限公司", "2026年10月31日", "会小企01表", left, right));

        Assert.True(bytes.Length > 0);
        // .xls 是 OLE2 复合文档，文件头固定为 D0 CF 11 E0
        Assert.Equal(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }, bytes[..4]);
    }

    [Fact]
    public void PdfBuilder_RendersA4Pdf()
    {
        var (left, right) = BalanceSheetExportLayout.Build(
            BalanceSheetTemplate.SmallBusiness,
            [Line(1, 20_010.18m), Line(48, 20_001m), Line(51, 9.18m)]);

        var bytes = BalanceSheetPdfBuilder.Build(new BalanceSheetPdfModel(
            "测试1有限公司", "2026-10-31", "会小企01表", left, right));

        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }
}
