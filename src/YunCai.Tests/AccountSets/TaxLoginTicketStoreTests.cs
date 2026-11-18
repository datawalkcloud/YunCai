using YunCai.Modules.AccountSets.Application;
using Xunit;

namespace YunCai.Tests.AccountSets;

/// <summary>
/// 税局密码的一次性领取票据（<see cref="TaxLoginTicketStore"/>）。
/// 领域依据（一键登录税局 + 本机助手兑换）：
/// - 票据只能兑换一次，兑换后立即作废——防止明文密码在链路里被重放；
/// - 2 分钟过期，过期兑换返回 null 而非密码；
/// - 空/不存在的票据兑换返回 null（助手拿到的票据可能损坏或已用过）。
/// </summary>
public static class TaxLoginTicketStoreTests
{
    [Fact]
    public static void Issue_then_redeem_once_returns_the_password()
    {
        var store = new TaxLoginTicketStore();
        var ticket = store.Issue("Secret#2026");

        Assert.False(string.IsNullOrWhiteSpace(ticket));
        Assert.Equal("Secret#2026", store.Redeem(ticket));
    }

    [Fact]
    public static void Ticket_cannot_be_redeemed_twice()
    {
        var store = new TaxLoginTicketStore();
        var ticket = store.Issue("Secret#2026");

        Assert.Equal("Secret#2026", store.Redeem(ticket));
        // 第二次兑换：票据已作废。
        Assert.Null(store.Redeem(ticket));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage-ticket")]
    public static void Redeem_of_missing_or_blank_ticket_returns_null(string? ticket)
    {
        var store = new TaxLoginTicketStore();
        Assert.Null(store.Redeem(ticket));
    }

    [Fact]
    public static void Redeem_of_already_expired_ticket_returns_null()
    {
        var store = new TaxLoginTicketStore();
        // 直接塞一条已过期（1 小时前失效）的条目，验证 Redeem 走「过期即 null」分支而不是泄露密码。
        var expired = DateTimeOffset.UtcNow.AddHours(-1);
        var ticket = "expired-ticket";
        Seed(store, ticket, "Secret#2026", expired);

        Assert.Null(store.Redeem(ticket));
    }

    [Fact]
    public static void Valid_ticket_within_ttl_redeems_even_after_purge_pass()
    {
        var store = new TaxLoginTicketStore();
        var ticket = store.Issue("Secret#2026");
        // Issue 内部会触发 PurgeExpired；确认正常签发的票据仍可兑换。
        Assert.Equal("Secret#2026", store.Redeem(ticket));
    }

    /// <summary>通过反射往私有字典里塞一条指定过期时间的条目，避免为测试暴露内部类型。</summary>
    private static void Seed(TaxLoginTicketStore store, string ticket, string password, DateTimeOffset expiresAt)
    {
        var field = typeof(TaxLoginTicketStore)
            .GetField("_tickets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("找不到 _tickets 字段");
        var entryType = typeof(TaxLoginTicketStore).Assembly
            .GetType("YunCai.Modules.AccountSets.Application.TaxLoginTicketStore+Entry")
            ?? throw new InvalidOperationException("找不到 Entry 类型");
        var entry = System.Activator.CreateInstance(entryType, password, expiresAt)
            ?? throw new InvalidOperationException("无法构造 Entry");
        var dict = (System.Collections.IDictionary)field.GetValue(store)!;
        dict[ticket] = entry;
    }
}
