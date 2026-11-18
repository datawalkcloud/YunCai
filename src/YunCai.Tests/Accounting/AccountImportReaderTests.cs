using MiniExcelLibs;
using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Accounting;

/// <summary>
/// 科目导入读取器测试：本系统模板与外部软件（金蝶）导出的表头用词不同，
/// 按别名对上即可；类别/启用状态的口径由应用服务负责，这里连同真实 xlsx 一起验。
/// xlsx 用 MiniExcel 现造，不依赖磁盘上的导出文件。
/// </summary>
public sealed class AccountImportReaderTests : IDisposable
{
    private readonly SqliteTestContext _db = new();

    public void Dispose() => _db.Dispose();

    /// <summary>金蝶「科目」导出的列名与取值口径。</summary>
    private static byte[] KingdeeWorkbook()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            KingdeeRow("1122", "应收账款", "流动资产", "借", "启用"),
            KingdeeRow("2202", "应付账款", "流动负债", "贷", "启用"),
            KingdeeRow("3001", "实收资本", "所有者权益", "贷", "启用"),
            KingdeeRow("4001", "生产成本", "成本", "借", "启用"),
            KingdeeRow("5001", "主营业务收入", "营业收入", "贷", "启用"),
            KingdeeRow("5602", "管理费用", "期间费用", "借", "启用"),
            KingdeeRow("560201", "办公费", "期间费用", "借", "启用"),
            KingdeeRow("6000", "以前年度损益调整", "以前年度损益调整", "借", "禁用"),
        };

        using var stream = new MemoryStream();
        MiniExcel.SaveAs(stream, new Dictionary<string, object> { ["科目"] = rows }, excelType: ExcelType.XLSX);
        return stream.ToArray();
    }

    private static Dictionary<string, object?> KingdeeRow(
        string code, string name, string category, string direction, string status) => new()
    {
        ["编码"] = code,
        ["名称"] = name,
        ["类别"] = category,
        ["余额方向"] = direction,
        ["辅助核算类别"] = "",
        ["是否现金科目"] = "否",
        ["状态"] = status,
        ["是否平行科目"] = "否",
    };

    private static List<ImportedAccountRow> Read(byte[] xlsx)
    {
        using var stream = new MemoryStream(xlsx);
        return [.. new XlsxAccountImportReader().Read(stream)];
    }

    [Fact]
    public void Read_KingdeeExport_ResolvesAliasedHeaders()
    {
        var rows = Read(KingdeeWorkbook());

        Assert.Equal(8, rows.Count);
        Assert.Equal("1122", rows[0].Code);
        Assert.Equal("应收账款", rows[0].Name);
        Assert.Equal("流动资产", rows[0].Category);
        Assert.Equal("借", rows[0].Direction);
        Assert.True(rows[0].IsEnabled);
        // 「状态」列的禁用要落成停用，而不是当作未填默认启用
        Assert.False(rows[^1].IsEnabled);
    }

    [Fact]
    public void Read_OwnTemplate_RoundsTrip()
    {
        var rows = Read(AccountImportTemplateBuilder.Build());

        Assert.Equal(2, rows.Count);
        Assert.Equal("22020", rows[0].Code);
        Assert.Equal("应付账款", rows[0].Name);
    }

    [Fact]
    public void Read_UnknownHeaderCode_ThrowsWithAliasHint()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["代码"] = "1001", ["科目名称"] = "库存现金" },
        };
        using var stream = new MemoryStream();
        MiniExcel.SaveAs(stream, new Dictionary<string, object> { ["科目"] = rows }, excelType: ExcelType.XLSX);

        var ex = Assert.Throws<AccountImportException>(
            () => Read(stream.ToArray()));

        Assert.Contains("表头缺少「科目编码」", ex.Message);
        Assert.Contains("编码", ex.Message);
    }

    [Fact]
    public async Task Import_KingdeeExport_UsesItsOwnCategoryVocabulary()
    {
        // 金蝶把利润表科目按营业收入/期间费用等细分口径导出，编码首位推断会把 5xxx 误判成成本类
        // （小企业准则的成本在 4xxx），必须优先采信文件里的类别。
        var accountSet = _db.AddAccountSet("金蝶导入账套", "4-2-2-2");
        var accounts = new AccountRepository(_db.Context);
        var service = new AccountImportService(
            new XlsxAccountImportReader(),
            new AccountSetRepository(_db.Context),
            accounts,
            new AccountAutoCreateService(accounts));

        using var stream = new MemoryStream(KingdeeWorkbook());
        var result = await service.ImportAsync(accountSet.Id, stream);

        Assert.Equal(8, result.TotalRows);
        Assert.Equal(8, result.ImportedAccounts);
        Assert.Empty(result.Warnings);

        _db.DetachAll();
        Assert.Equal(AccountCategory.Asset, (await accounts.GetByCodeAsync(accountSet.Id, "1122"))!.Category);
        Assert.Equal(AccountCategory.Liability, (await accounts.GetByCodeAsync(accountSet.Id, "2202"))!.Category);
        Assert.Equal(AccountCategory.Equity, (await accounts.GetByCodeAsync(accountSet.Id, "3001"))!.Category);
        Assert.Equal(AccountCategory.Cost, (await accounts.GetByCodeAsync(accountSet.Id, "4001"))!.Category);
        var income = await accounts.GetByCodeAsync(accountSet.Id, "5001");
        Assert.Equal(AccountCategory.ProfitAndLoss, income!.Category);
        Assert.Equal(BalanceDirection.Credit, income.Direction);
        Assert.Equal(AccountCategory.ProfitAndLoss, (await accounts.GetByCodeAsync(accountSet.Id, "6000"))!.Category);

        // 明细科目按编码方案挂到 5602 下，类别继承上级
        var child = await accounts.GetByCodeAsync(accountSet.Id, "560201");
        Assert.Equal(2, child!.Level);
        Assert.Equal(AccountCategory.ProfitAndLoss, child.Category);
        Assert.Equal((await accounts.GetByCodeAsync(accountSet.Id, "5602"))!.Id, child.ParentId);
        Assert.False((await accounts.GetByCodeAsync(accountSet.Id, "6000"))!.IsEnabled);
    }
}
