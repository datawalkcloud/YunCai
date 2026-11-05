using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Tests.Support;

/// <summary>
/// 基于内存 SQLite 的测试上下文：复用生产 <see cref="AppDbContext"/> 的实体映射，
/// 每个测试用例独立一个连接，用完即销毁，不依赖本机 PostgreSQL。
/// </summary>
internal sealed class SqliteTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        Context.Database.EnsureCreated();
    }

    public AppDbContext Context { get; }

    /// <summary>
    /// 清空变更跟踪，模拟「一次 HTTP 请求一个新的 DbContext」。
    /// 生产仓储的读一律 AsNoTracking，同一个测试里连续调用多个服务时若不清跟踪，
    /// 前一次服务留在跟踪器里的实例会和后一次的 Remove/Update 撞主键。
    /// </summary>
    public void DetachAll() => Context.ChangeTracker.Clear();

    public AccountSet AddAccountSet(string name, string? codingScheme = null, AccountingStandard standard = AccountingStandard.Enterprise)
    {
        var accountSet = AccountSet.Create(
            name, name, standard, TaxpayerType.General, "人民币", 2026, 1, codingScheme ?? AccountSet.DefaultCodingScheme);

        Context.AccountSets.Add(accountSet);
        Context.SaveChanges();
        return accountSet;
    }

    public Account AddAccount(AccountSet accountSet, string code, string name)
    {
        var account = Account.CreateRoot(
            accountSet.Id, code, name, AccountCategory.Asset, BalanceDirection.Debit);
        Context.Accounts.Add(account);
        Context.SaveChanges();
        return account;
    }

    public Voucher AddVoucher(Guid accountSetId, string word, int number, DateOnly date, Guid cashAccountId, Guid counterAccountId)
    {
        var voucher = Voucher.Create(accountSetId, word, number, date, "测试制单人");
        voucher.AddEntry("测试分录", cashAccountId, 100m, 0m);
        voucher.AddEntry("测试分录", counterAccountId, 0m, 100m);
        Context.Vouchers.Add(voucher);
        Context.SaveChanges();
        return voucher;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
