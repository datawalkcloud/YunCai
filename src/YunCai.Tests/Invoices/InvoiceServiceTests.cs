using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Funds.Infrastructure;
using YunCai.Modules.Invoices.Application;
using YunCai.Modules.Invoices.Domain;
using YunCai.Modules.Invoices.Infrastructure;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Vouchers.Infrastructure;
using YunCai.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace YunCai.Tests.Invoices;

/// <summary>
/// 发票服务测试：进销项发票的 CRUD 与明细行金额 / 税额校验、发票号按方向判重、
/// 入账状态流转与进项认证、由发票生成凭证的分录方向（销项借对方 / 贷收入+销项税额，
/// 进项借商品+进项税额 / 贷对方）与默认科目按编码解析、凭证日期优先级（请求传入 > 发票开票日）、
/// 批量出票的幂等跳过与单条业务失败明细、
/// 以及凭证被删除后列表查询的自愈（清残留引用并回退未入账）。
/// </summary>
public sealed class InvoiceServiceTests : IDisposable
{
    private readonly SqliteTestContext _db = new();
    private readonly AccountSet _accountSet;
    private readonly InvoiceRepository _invoices;
    private readonly VoucherRepository _vouchers;
    private readonly InvoiceService _service;

    public InvoiceServiceTests()
    {
        _accountSet = _db.AddAccountSet("测试账套");
        _invoices = new InvoiceRepository(_db.Context);
        _vouchers = new VoucherRepository(_db.Context);
        _service = new InvoiceService(
            new AccountSetRepository(_db.Context),
            _invoices,
            new JournalRepository(_db.Context),
            new AccountRepository(_db.Context),
            _vouchers);
    }

    public void Dispose() => _db.Dispose();

    // —— 种子助手 ——

    private async Task<Invoice> SeedInvoice(
        InvoiceDirection direction = InvoiceDirection.Sales,
        string invoiceNo = "25317000",
        string partner = "客户甲",
        InvoiceStatus status = InvoiceStatus.Pending,
        Guid? voucherId = null,
        decimal amount = 100m,
        decimal taxRate = 13m,
        decimal taxAmount = 13m)
    {
        var invoice = Invoice.Create(
            _accountSet.Id, direction, "增值税专用发票",
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1),
            invoiceNo, partner, null, "手工新增", null);
        invoice.ReplaceLines([("商品", amount, taxRate, taxAmount)]);
        if (voucherId is { } id)
        {
            invoice.AttachVoucher(id);
        }

        if (status != InvoiceStatus.Pending)
        {
            invoice.ChangeStatus(status);
        }

        await _invoices.AddAsync(invoice);
        await _invoices.SaveChangesAsync();
        return invoice;
    }

    /// <summary>直接种一张凭证（不经过凭证服务，供回填关联 / 自愈测试使用）。</summary>
    private Voucher SeedVoucher(int number)
    {
        var voucher = Voucher.Create(_accountSet.Id, "记", number, new DateOnly(2026, 2, 1), "测试制单人");
        _db.Context.Vouchers.Add(voucher);
        _db.Context.SaveChanges();
        return voucher;
    }

    private static SaveInvoiceRequest NewRequest(
        string invoiceNo = "8801",
        string partner = "客户甲",
        IReadOnlyList<SaveInvoiceLineRequest>? lines = null) => new()
    {
        InvoiceType = "增值税专用发票",
        InputDate = new DateOnly(2026, 3, 1),
        IssueDate = new DateOnly(2026, 3, 1),
        InvoiceNo = invoiceNo,
        PartnerName = partner,
        Lines = lines ??
        [
            new SaveInvoiceLineRequest { GoodsName = "键盘", Amount = 100m, TaxRate = 13m, TaxAmount = 13m },
            new SaveInvoiceLineRequest { GoodsName = "鼠标", Amount = 200m, TaxRate = 13m, TaxAmount = 26m },
        ],
    };

    private static GenerateInvoiceVoucherRequest NewVoucherRequest(
        Guid? counterAccountId = null,
        Guid? subjectAccountId = null,
        Guid? taxAccountId = null,
        DateOnly? date = null) => new()
    {
        CounterAccountId = counterAccountId,
        SubjectAccountId = subjectAccountId,
        TaxAccountId = taxAccountId,
        Date = date,
    };

    private async Task<Voucher> LoadVoucherAsync(Guid voucherId)
        => await _db.Context.Vouchers.Include(v => v.Entries).SingleAsync(v => v.Id == voucherId);

    // —— 新建发票 ——

    [Fact]
    public async Task Create_SalesInvoice_PersistsLinesAndTotals()
    {
        var dto = await _service.CreateAsync(_accountSet.Id, InvoiceDirection.Sales, NewRequest());

        Assert.Equal(300m, dto.TotalAmount);
        Assert.Equal(39m, dto.TotalTax);
        Assert.Equal(339m, dto.TotalWithTax);
        Assert.Equal(2, dto.Lines.Count);
        Assert.Equal("手工新增", dto.Source);
        Assert.Equal("未入账", dto.StatusText);
    }

    [Fact]
    public async Task Create_DuplicateInvoiceNoSameDirection_ThrowsInvoiceException()
    {
        await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");

        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.CreateAsync(_accountSet.Id, InvoiceDirection.Sales, NewRequest(invoiceNo: "8801")));

        Assert.Contains("8801", ex.Message);
        Assert.Contains("已存在", ex.Message);
    }

    [Fact]
    public async Task Create_SameInvoiceNoDifferentDirection_Succeeds()
    {
        // 发票号唯一范围是「账套 + 方向」，销项用过的号进项可以再用。
        await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");

        var dto = await _service.CreateAsync(_accountSet.Id, InvoiceDirection.Purchase, NewRequest(invoiceNo: "8801"));

        Assert.Equal("8801", dto.InvoiceNo);
        var reloaded = await _invoices.GetByIdAsync(_accountSet.Id, dto.Id);
        Assert.NotNull(reloaded);
    }

    [Fact]
    public async Task Create_WithoutLines_ThrowsInvoiceException()
    {
        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.CreateAsync(_accountSet.Id, InvoiceDirection.Sales, NewRequest(lines: [])));

        Assert.Contains("至少要有一行商品明细", ex.Message);
    }

    [Fact]
    public async Task Create_NonPositiveAmount_ThrowsInvoiceException()
    {
        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.CreateAsync(_accountSet.Id, InvoiceDirection.Sales, NewRequest(lines:
            [
                new SaveInvoiceLineRequest { GoodsName = "商品", Amount = 0m, TaxRate = 13m, TaxAmount = 0m },
            ])));

        Assert.Contains("金额必须大于零", ex.Message);
    }

    [Fact]
    public async Task Create_TaxRateOutOfRange_ThrowsInvoiceException()
    {
        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.CreateAsync(_accountSet.Id, InvoiceDirection.Sales, NewRequest(lines:
            [
                new SaveInvoiceLineRequest { GoodsName = "商品", Amount = 100m, TaxRate = 101m, TaxAmount = 13m },
            ])));

        Assert.Contains("税率必须在 0-100 之间", ex.Message);
    }

    [Fact]
    public async Task Create_NegativeTaxAmount_ThrowsInvoiceException()
    {
        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.CreateAsync(_accountSet.Id, InvoiceDirection.Sales, NewRequest(lines:
            [
                new SaveInvoiceLineRequest { GoodsName = "商品", Amount = 100m, TaxRate = 13m, TaxAmount = -1m },
            ])));

        Assert.Contains("税额不能为负数", ex.Message);
    }

    [Fact]
    public async Task Create_UnknownAccountSet_ThrowsInvoiceException()
    {
        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.CreateAsync(Guid.NewGuid(), InvoiceDirection.Sales, NewRequest()));

        Assert.Contains("账套不存在", ex.Message);
    }

    // —— 修改发票 ——

    [Fact]
    public async Task Update_ExistingInvoice_AppliesHeaderAndLines()
    {
        var invoice = await SeedInvoice();

        var dto = await _service.UpdateAsync(_accountSet.Id, invoice.Id, NewRequest(
            invoiceNo: "8802",
            partner: "客户乙",
            lines:
            [
                new SaveInvoiceLineRequest { GoodsName = "显示器", Amount = 1000m, TaxRate = 13m, TaxAmount = 130m },
            ]));

        Assert.NotNull(dto);
        Assert.Equal("8802", dto!.InvoiceNo);
        Assert.Equal("客户乙", dto.PartnerName);
        Assert.Equal(1000m, dto.TotalAmount);
        Assert.Equal(130m, dto.TotalTax);
        Assert.Single(dto.Lines);
        Assert.Equal("显示器", dto.Lines[0].GoodsName);
    }

    [Fact]
    public async Task Update_DuplicateInvoiceNo_ThrowsInvoiceException()
    {
        var invoice = await SeedInvoice(invoiceNo: "8801");
        await SeedInvoice(invoiceNo: "8802");

        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.UpdateAsync(_accountSet.Id, invoice.Id, NewRequest(invoiceNo: "8802")));

        Assert.Contains("已存在", ex.Message);
    }

    [Fact]
    public async Task Update_KeepingOwnInvoiceNo_Succeeds()
    {
        // 判重排除自身：不改号仅改其他字段的修改不应被拦截。
        var invoice = await SeedInvoice(invoiceNo: "8801");

        var dto = await _service.UpdateAsync(_accountSet.Id, invoice.Id, NewRequest(invoiceNo: "8801", partner: "客户乙"));

        Assert.NotNull(dto);
        Assert.Equal("客户乙", dto!.PartnerName);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNull()
    {
        var dto = await _service.UpdateAsync(_accountSet.Id, Guid.NewGuid(), NewRequest());

        Assert.Null(dto);
    }

    // —— 删除发票 ——

    [Fact]
    public async Task Delete_ExistingInvoice_ReturnsTrueAndRemoves()
    {
        var invoice = await SeedInvoice();

        Assert.True(await _service.DeleteAsync(_accountSet.Id, invoice.Id));
        Assert.Null(await _invoices.GetByIdAsync(_accountSet.Id, invoice.Id));
    }

    [Fact]
    public async Task Delete_NonExistentInvoice_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAsync(_accountSet.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteMany_DeduplicatesIdsAndCountsSkipped()
    {
        var invoice = await SeedInvoice();
        var missingId = Guid.NewGuid();

        var result = await _service.DeleteManyAsync(_accountSet.Id, [invoice.Id, invoice.Id, missingId]);

        Assert.Equal(1, result.Affected);
        Assert.Equal(1, result.Skipped);
    }

    // —— 状态流转与认证 ——

    [Fact]
    public async Task ChangeStatus_SetsStatus()
    {
        var invoice = await SeedInvoice();

        var dto = await _service.ChangeStatusAsync(_accountSet.Id, invoice.Id, new ChangeInvoiceStatusRequest { Status = (int)InvoiceStatus.Suspended });

        Assert.NotNull(dto);
        Assert.Equal("暂不入账", dto!.StatusText);
    }

    [Fact]
    public async Task ChangeStatus_InvalidStatus_ThrowsInvoiceException()
    {
        var invoice = await SeedInvoice();

        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.ChangeStatusAsync(_accountSet.Id, invoice.Id, new ChangeInvoiceStatusRequest { Status = 99 }));

        Assert.Contains("状态无效", ex.Message);
    }

    [Fact]
    public async Task ChangeStatus_NonExistent_ReturnsNull()
    {
        Assert.Null(await _service.ChangeStatusAsync(_accountSet.Id, Guid.NewGuid(), new ChangeInvoiceStatusRequest { Status = 2 }));
    }

    [Fact]
    public async Task Authenticate_PurchaseInvoice_SetsAuthDateAndTaxPeriod()
    {
        var invoice = await SeedInvoice(InvoiceDirection.Purchase);

        var dto = await _service.AuthenticateAsync(
            _accountSet.Id, invoice.Id, new AuthenticateInvoiceRequest { AuthDate = new DateOnly(2026, 3, 5), TaxPeriod = 202603 });

        Assert.NotNull(dto);
        Assert.Equal(new DateOnly(2026, 3, 5), dto!.AuthDate);
        Assert.Equal(202603, dto.TaxPeriod);
    }

    [Fact]
    public async Task Authenticate_SalesInvoice_ThrowsInvoiceException()
    {
        var invoice = await SeedInvoice(InvoiceDirection.Sales);

        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.AuthenticateAsync(
                _accountSet.Id, invoice.Id, new AuthenticateInvoiceRequest { AuthDate = new DateOnly(2026, 3, 5) }));

        Assert.Contains("仅进项发票可以认证", ex.Message);
    }

    [Fact]
    public async Task AuthenticateMany_SkipsSalesAndMissing_WithoutFailingBatch()
    {
        var purchase = await SeedInvoice(InvoiceDirection.Purchase, invoiceNo: "8801");
        var sales = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8802");
        var missingId = Guid.NewGuid();

        var result = await _service.AuthenticateManyAsync(
            _accountSet.Id, [purchase.Id, sales.Id, missingId], new DateOnly(2026, 3, 5), 202603);

        Assert.Equal(1, result.Affected);
        Assert.Equal(2, result.Skipped);
        var reloaded = await _invoices.GetByIdAsync(_accountSet.Id, purchase.Id);
        Assert.Equal(new DateOnly(2026, 3, 5), reloaded!.AuthDate);
    }

    // —— 由发票生成凭证 ——

    [Fact]
    public async Task GenerateVoucher_SalesWithExplicitAccounts_BooksCounterDebitAndIncomeTaxCredit()
    {
        // 销项：借 对方科目 价税合计 / 贷 收入科目 金额、贷 销项税额科目 税额。
        var counter = _db.AddAccount(_accountSet, "1122", "应收账款");
        var income = _db.AddAccount(_accountSet, "5001", "主营业务收入");
        var tax = _db.AddAccount(_accountSet, "222100105", "销项税额");
        var invoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");

        var result = await _service.GenerateVoucherAsync(
            _accountSet.Id, invoice.Id, "制单人", NewVoucherRequest(counter.Id, income.Id, tax.Id));

        Assert.Equal("记-1", result.VoucherNo);
        var voucher = await LoadVoucherAsync(result.VoucherId);
        Assert.Equal(3, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == counter.Id && e.Debit == 113m && e.Credit == 0m);
        Assert.Contains(voucher.Entries, e => e.AccountId == income.Id && e.Debit == 0m && e.Credit == 100m);
        Assert.Contains(voucher.Entries, e => e.AccountId == tax.Id && e.Debit == 0m && e.Credit == 13m);

        var reloaded = await _invoices.GetByIdAsync(_accountSet.Id, invoice.Id);
        Assert.Equal(InvoiceStatus.Posted, reloaded!.Status);
        Assert.Equal(voucher.Id, reloaded.VoucherId);
    }

    [Fact]
    public async Task GenerateVoucher_PurchaseWithDefaultAccounts_BooksGoodsAndTaxDebit()
    {
        // 进项：借 商品科目 金额、借 进项税额科目 税额 / 贷 对方科目 价税合计；
        // 三个科目都省略时按编码取默认（2202 / 1405 / 222100101）。
        var counter = _db.AddAccount(_accountSet, "2202", "应付账款");
        var goods = _db.AddAccount(_accountSet, "1405", "库存商品");
        var tax = _db.AddAccount(_accountSet, "222100101", "进项税额");
        var invoice = await SeedInvoice(InvoiceDirection.Purchase, invoiceNo: "8801");

        var result = await _service.GenerateVoucherAsync(_accountSet.Id, invoice.Id, "制单人", NewVoucherRequest());

        var voucher = await LoadVoucherAsync(result.VoucherId);
        Assert.Contains(voucher.Entries, e => e.AccountId == goods.Id && e.Debit == 100m);
        Assert.Contains(voucher.Entries, e => e.AccountId == tax.Id && e.Debit == 13m);
        Assert.Contains(voucher.Entries, e => e.AccountId == counter.Id && e.Credit == 113m);
    }

    [Fact]
    public async Task GenerateVoucher_SalesWithDefaultAccounts_ResolvesByCode()
    {
        var counter = _db.AddAccount(_accountSet, "1122", "应收账款");
        var income = _db.AddAccount(_accountSet, "5001", "主营业务收入");
        var tax = _db.AddAccount(_accountSet, "222100105", "销项税额");
        var invoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");

        var result = await _service.GenerateVoucherAsync(_accountSet.Id, invoice.Id, "制单人", NewVoucherRequest());

        var voucher = await LoadVoucherAsync(result.VoucherId);
        Assert.Contains(voucher.Entries, e => e.AccountId == counter.Id && e.Debit == 113m);
        Assert.Contains(voucher.Entries, e => e.AccountId == income.Id && e.Credit == 100m);
        Assert.Contains(voucher.Entries, e => e.AccountId == tax.Id && e.Credit == 13m);
    }

    [Fact]
    public async Task GenerateVoucher_ZeroTax_OmitsTaxEntry()
    {
        var counter = _db.AddAccount(_accountSet, "1122", "应收账款");
        var income = _db.AddAccount(_accountSet, "5001", "主营业务收入");
        var invoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801", amount: 100m, taxRate: 0m, taxAmount: 0m);

        // 税额为 0 时不产生税额行，分录只有借对方 / 贷收入两行。
        var result = await _service.GenerateVoucherAsync(_accountSet.Id, invoice.Id, "制单人", NewVoucherRequest(counter.Id, income.Id));

        var voucher = await LoadVoucherAsync(result.VoucherId);
        Assert.Equal(2, voucher.Entries.Count);
        Assert.Contains(voucher.Entries, e => e.AccountId == counter.Id && e.Debit == 100m);
        Assert.Contains(voucher.Entries, e => e.AccountId == income.Id && e.Credit == 100m);
    }

    [Fact]
    public async Task GenerateVoucher_DateRule_RequestDateWinsOtherwiseInvoiceIssueDate()
    {
        // 凭证日期口径（InvoiceService.GenerateVoucherAsync 的 request.Date ?? invoice.IssueDate）：
        // 显式传入优先，为空才回落到发票开票日期。
        // 注意发票带税额，必须备齐 222100105 销项税额科目，否则整单在科目解析阶段就被拒。
        var counter = _db.AddAccount(_accountSet, "1122", "应收账款");
        var income = _db.AddAccount(_accountSet, "5001", "主营业务收入");
        var tax = _db.AddAccount(_accountSet, "222100105", "销项税额");

        // 分支一：不传日期 → 取发票开票日期 2026-02-01。
        var fallbackInvoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");
        _db.DetachAll();
        var fallbackResult = await _service.GenerateVoucherAsync(
            _accountSet.Id, fallbackInvoice.Id, "制单人", NewVoucherRequest(counter.Id, income.Id, tax.Id));
        var fallbackVoucher = await LoadVoucherAsync(fallbackResult.VoucherId);
        Assert.Equal("记-1", fallbackResult.VoucherNo);
        Assert.Equal(new DateOnly(2026, 2, 1), fallbackVoucher.Date);

        // 分支二：显式传日期 → 覆盖开票日期（跨期入账场景）。
        // 一次请求一个新 DbContext：上一条出票留下的跟踪实例不该影响这一条。
        var explicitInvoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8802");
        _db.DetachAll();
        var explicitResult = await _service.GenerateVoucherAsync(
            _accountSet.Id, explicitInvoice.Id, "制单人",
            NewVoucherRequest(counter.Id, income.Id, tax.Id, new DateOnly(2026, 3, 20)));
        var explicitVoucher = await LoadVoucherAsync(explicitResult.VoucherId);
        Assert.Equal("记-2", explicitResult.VoucherNo);
        Assert.Equal(new DateOnly(2026, 3, 20), explicitVoucher.Date);

        // 日期规则不影响分录口径：两张都是借对方 113 / 贷收入 100 + 贷销项税额 13。
        Assert.Equal(3, fallbackVoucher.Entries.Count);
        Assert.Equal(3, explicitVoucher.Entries.Count);
        Assert.Contains(explicitVoucher.Entries, e => e.AccountId == counter.Id && e.Debit == 113m);
        Assert.Contains(explicitVoucher.Entries, e => e.AccountId == tax.Id && e.Credit == 13m);
    }

    [Fact]
    public async Task GenerateVoucher_PostedInvoice_ThrowsInvoiceException()
    {
        var counter = _db.AddAccount(_accountSet, "1122", "应收账款");
        var income = _db.AddAccount(_accountSet, "5001", "主营业务收入");
        var invoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801", status: InvoiceStatus.Posted);

        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, invoice.Id, "制单人", NewVoucherRequest(counter.Id, income.Id)));

        Assert.Contains("已入账", ex.Message);
        Assert.Contains("请勿重复生成凭证", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_SuspendedInvoice_ThrowsInvoiceException()
    {
        var invoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801", status: InvoiceStatus.Suspended);

        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, invoice.Id, "制单人", NewVoucherRequest()));

        Assert.Contains("暂不入账", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_NonExistentInvoice_ThrowsInvoiceException()
    {
        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, Guid.NewGuid(), "制单人", NewVoucherRequest()));

        Assert.Contains("发票不存在", ex.Message);
    }

    [Fact]
    public async Task GenerateVoucher_MissingDefaultAccount_ThrowsOperativeHint()
    {
        // 账套里没有 1122 应收账款：默认科目解析失败，提示须手动指定科目。
        var invoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");

        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.GenerateVoucherAsync(_accountSet.Id, invoice.Id, "制单人", NewVoucherRequest()));

        Assert.Contains("应收账款", ex.Message);
        Assert.Contains("1122", ex.Message);
    }

    [Fact]
    public async Task GenerateVouchersAsync_PostedAndMissing_BothCountedAsSkippedWithoutFailures()
    {
        // 批量出票的跳过口径（InvoiceService.GenerateVouchersAsync）：
        // 不存在 / 已入账 / 暂不入账一律只计入 Skipped，不进失败明细——重复勾选幂等不报错，
        // 失败明细只留给「状态可出票但业务校验不过」的发票。
        var counter = _db.AddAccount(_accountSet, "1122", "应收账款");
        var income = _db.AddAccount(_accountSet, "5001", "主营业务收入");
        var tax = _db.AddAccount(_accountSet, "222100105", "销项税额");
        var pendingA = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");
        var pendingB = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8802");
        var posted = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8803", status: InvoiceStatus.Posted);
        var missingId = Guid.NewGuid();
        var request = NewVoucherRequest(counter.Id, income.Id, tax.Id);

        _db.DetachAll();
        var result = await _service.GenerateVouchersAsync(
            _accountSet.Id, [pendingA.Id, pendingB.Id, posted.Id, missingId], "制单人", request);

        Assert.Equal(2, result.Created);
        Assert.Equal(2, result.Skipped);
        Assert.Empty(result.Failures);

        // 业务结果：两张未入账各自独立出票并回写「已入账」，已入账那张没被重复出票。
        _db.DetachAll();
        var reloadedA = await _invoices.GetByIdAsync(_accountSet.Id, pendingA.Id);
        Assert.Equal(InvoiceStatus.Posted, reloadedA!.Status);
        Assert.NotNull(reloadedA.VoucherId);
        var reloadedB = await _invoices.GetByIdAsync(_accountSet.Id, pendingB.Id);
        Assert.Equal(InvoiceStatus.Posted, reloadedB!.Status);
        Assert.NotNull(reloadedB.VoucherId);
        Assert.NotEqual(reloadedA.VoucherId, reloadedB.VoucherId);
        var reloadedPosted = await _invoices.GetByIdAsync(_accountSet.Id, posted.Id);
        Assert.Null(reloadedPosted!.VoucherId);
        Assert.Equal(2, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == _accountSet.Id));
    }

    [Fact]
    public async Task GenerateVouchersAsync_PendingInvoiceFails_RecordsReasonAndKeepsBatchGoing()
    {
        // 失败明细的真实来源：未入账发票出票时业务校验不过（这里让税额科目指向一张不存在的科目），
        // 单条失败计入 Failures（带发票号码与中文原因），其余照常出票，失败的保持未入账。
        var counter = _db.AddAccount(_accountSet, "1122", "应收账款");
        var income = _db.AddAccount(_accountSet, "5001", "主营业务收入");
        var zeroTax = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801", taxRate: 0m, taxAmount: 0m);
        var withTax = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8802");

        // 税额科目故意指向一张不存在的科目：无税发票走不到税额解析，正常出票；有税发票失败。
        var request = new GenerateInvoiceVoucherRequest
        {
            CounterAccountId = counter.Id,
            SubjectAccountId = income.Id,
            TaxAccountId = Guid.NewGuid(),
        };

        _db.DetachAll();
        var result = await _service.GenerateVouchersAsync(
            _accountSet.Id, [zeroTax.Id, withTax.Id], "制单人", request);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Skipped);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(withTax.Id, failure.InvoiceId);
        Assert.Equal("8802", failure.InvoiceNo);
        Assert.Contains("指定的销项税额科目不存在", failure.Reason);

        _db.DetachAll();
        Assert.Equal(InvoiceStatus.Posted, (await _invoices.GetByIdAsync(_accountSet.Id, zeroTax.Id))!.Status);
        var failed = await _invoices.GetByIdAsync(_accountSet.Id, withTax.Id);
        Assert.Equal(InvoiceStatus.Pending, failed!.Status);
        Assert.Null(failed.VoucherId);
        // 失败的那条不留半成品凭证。
        Assert.Equal(1, await _db.Context.Vouchers.CountAsync(v => v.AccountSetId == _accountSet.Id));
    }

    // —— 列表查询与自愈 ——

    [Fact]
    public async Task GetRangeAsync_IsolatesByDirectionAndStatus()
    {
        await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");
        await SeedInvoice(InvoiceDirection.Purchase, invoiceNo: "8802");
        await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8803", status: InvoiceStatus.Posted);

        var sales = await _service.GetRangeAsync(_accountSet.Id, InvoiceDirection.Sales);
        var purchasePending = await _service.GetRangeAsync(
            _accountSet.Id, InvoiceDirection.Purchase, status: InvoiceStatus.Pending);

        Assert.Equal(2, sales.Count);
        var pending = Assert.Single(purchasePending);
        Assert.Equal("8802", pending.InvoiceNo);
    }

    [Fact]
    public async Task GetRangeAsync_UnknownAccountSet_ThrowsInvoiceException()
    {
        var ex = await Assert.ThrowsAsync<InvoiceException>(
            () => _service.GetRangeAsync(Guid.NewGuid(), InvoiceDirection.Sales));

        Assert.Contains("账套不存在", ex.Message);
    }

    [Fact]
    public async Task GetRangeAsync_HealsStaleVoucherLink_AndRollsBackToPending()
    {
        // 凭证被删除后发票仍持有 VoucherId：列表查询应清引用并把已入账回退为未入账。
        var voucher = SeedVoucher(1);
        var invoice = await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801", status: InvoiceStatus.Posted, voucherId: voucher.Id);
        _db.Context.Vouchers.Remove(voucher);
        await _db.Context.SaveChangesAsync();

        // 列表查询按「一次请求一个新 DbContext」跑：播种期留下的被跟踪发票必须清掉，
        // 否则自愈里的 InvoiceRepository.Update → Attach 会和它撞主键。
        _db.DetachAll();
        var rows = await _service.GetRangeAsync(_accountSet.Id, InvoiceDirection.Sales);

        var dto = Assert.Single(rows);
        Assert.Null(dto.VoucherText);
        Assert.Equal("未入账", dto.StatusText);

        // 自愈要真的落库，不能只改内存实例：否则下次刷新时该发票仍因「已入账」被永久排除、无法重新出票。
        _db.DetachAll();
        var reloaded = await _invoices.GetByIdAsync(_accountSet.Id, invoice.Id);
        Assert.Null(reloaded!.VoucherId);
        Assert.Equal(InvoiceStatus.Pending, reloaded.Status);
    }

    [Fact]
    public async Task GetRangeAsync_KeepsVoucherTextForLiveVoucher()
    {
        var voucher = SeedVoucher(7);
        await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801", status: InvoiceStatus.Posted, voucherId: voucher.Id);

        var rows = await _service.GetRangeAsync(_accountSet.Id, InvoiceDirection.Sales);

        Assert.Equal("记-7", Assert.Single(rows).VoucherText);
    }

    [Fact]
    public async Task GetSummaryAsync_SumsTotalsAcrossInvoices()
    {
        await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8801");
        await SeedInvoice(InvoiceDirection.Sales, invoiceNo: "8802");

        var summary = await _service.GetSummaryAsync(_accountSet.Id, InvoiceDirection.Sales);

        Assert.Equal(2, summary.Count);
        Assert.Equal(200m, summary.TotalAmount);
        Assert.Equal(26m, summary.TotalTax);
        Assert.Equal(226m, summary.TotalWithTax);
    }
}
