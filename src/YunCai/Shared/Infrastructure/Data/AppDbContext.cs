using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Assist.Domain;
using YunCai.Modules.Closing.Domain;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Archives.Domain;
using YunCai.Modules.FixedAssets.Domain;
using YunCai.Modules.Expenses.Domain;
using YunCai.Modules.Invoices.Domain;
using YunCai.Modules.Auditing.Domain;
using YunCai.Modules.Reports.Domain;
using YunCai.Modules.Reports.Domain.CashFlow;
using YunCai.Modules.Salary.Domain;
using YunCai.Modules.Tax.Domain;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Vouchers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace YunCai.SharedKernel.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;

        public DbSet<AccountSet> AccountSets { get; set; } = null!;

        public DbSet<Account> Accounts { get; set; } = null!;

        /// <summary>各会计准则的建账预置科目清单（参考数据，不挂账套）。</summary>
        public DbSet<PresetAccount> PresetAccounts { get; set; } = null!;

        public DbSet<Journal> Journals { get; set; } = null!;

        public DbSet<EntryCategory> EntryCategories { get; set; } = null!;

        public DbSet<Transfer> Transfers { get; set; } = null!;

        public DbSet<Voucher> Vouchers { get; set; } = null!;

        public DbSet<VoucherEntry> VoucherEntries { get; set; } = null!;

        /// <summary>凭证附件（原始单据影像）。</summary>
        public DbSet<VoucherAttachment> VoucherAttachments { get; set; } = null!;

        public DbSet<VoucherEntryAssist> VoucherEntryAssists { get; set; } = null!;

        public DbSet<AssistItem> AssistItems { get; set; } = null!;

        public DbSet<ClosingTemplate> ClosingTemplates { get; set; } = null!;

        public DbSet<ClosingTemplateLine> ClosingTemplateLines { get; set; } = null!;

        public DbSet<Currency> Currencies { get; set; } = null!;

        public DbSet<VoucherWord> VoucherWords { get; set; } = null!;

        public DbSet<VoucherTemplate> VoucherTemplates { get; set; } = null!;

        public DbSet<VoucherTemplateLine> VoucherTemplateLines { get; set; } = null!;

        public DbSet<Bill> Bills { get; set; } = null!;

        public DbSet<BillAttachment> BillAttachments { get; set; } = null!;

        public DbSet<FixedAssetCategory> FixedAssetCategories { get; set; } = null!;

        public DbSet<FixedAsset> FixedAssets { get; set; } = null!;

        public DbSet<FixedAssetChange> FixedAssetChanges { get; set; } = null!;

        public DbSet<FixedAssetVoucherSetting> FixedAssetVoucherSettings { get; set; } = null!;

        public DbSet<ExpenseBill> ExpenseBills { get; set; } = null!;

        public DbSet<AccountSetMember> AccountSetMembers { get; set; } = null!;

        /// <summary>账套角色（预置 + 自建），权限以「模块.操作」键文本保存。</summary>
        public DbSet<AccountSetRole> AccountSetRoles { get; set; } = null!;

        public DbSet<Invoice> Invoices { get; set; } = null!;

        public DbSet<InvoiceLine> InvoiceLines { get; set; } = null!;

        public DbSet<Employee> Employees { get; set; } = null!;

        public DbSet<InsuranceItem> InsuranceItems { get; set; } = null!;

        public DbSet<EmployeeInsurance> EmployeeInsurances { get; set; } = null!;

        public DbSet<SalaryBill> SalaryBills { get; set; } = null!;

        public DbSet<SalaryVoucherSetting> SalaryVoucherSettings { get; set; } = null!;

        public DbSet<ArchiveItem> ArchiveItems { get; set; } = null!;

        /// <summary>会计电子档案分类树节点。</summary>
        public DbSet<ArchiveCategory> ArchiveCategories { get; set; } = null!;

        public DbSet<OperationLog> OperationLogs { get; set; } = null!;

        /// <summary>年度预算单与预算行（科目 × 12 个月金额）。</summary>
        public DbSet<YunCai.Modules.Budgets.Domain.Budget> Budgets { get; set; } = null!;

        public DbSet<YunCai.Modules.Budgets.Domain.BudgetLine> BudgetLines { get; set; } = null!;

        public DbSet<CustomReport> CustomReports { get; set; } = null!;

        /// <summary>现金流量表行取数公式（按账套 + 行次保存，未保存的行回落默认公式）。</summary>
        public DbSet<CashFlowFormula> CashFlowFormulas { get; set; } = null!;

        /// <summary>税局登录态（加密后的 Cookie / localStorage），按账套 + 用户保存。</summary>
        public DbSet<TaxPortalSessionRecord> TaxPortalSessions { get; set; } = null!;

        /// <summary>任务中心：服务端每向税局办一次事留一行。</summary>
        public DbSet<TaxTaskRecord> TaxTasks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Username).HasColumnName("username").IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100);
                entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
                entity.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
                entity.Property(e => e.IsSystemAdmin).HasColumnName("is_system_admin").IsRequired();
                entity.Property(e => e.TokenVersion).HasColumnName("token_version").IsRequired();
                entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

                entity.HasIndex(e => e.Username).IsUnique();
                // 手机号唯一；PostgreSQL 的唯一索引允许多个 NULL，历史账号不受影响。
                entity.HasIndex(e => e.Phone).IsUnique();
            });

            modelBuilder.Entity<AccountSet>(entity =>
            {
                entity.ToTable("account_sets");
                entity.HasKey(e => e.Id);

                // 主键由领域层生成（Guid.NewGuid），须声明非存储生成，
                // 否则经导航集合新增的实体会被 EF 误判为已存在而生成 UPDATE。
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                entity.Property(e => e.CompanyName).HasColumnName("company_name").IsRequired().HasMaxLength(200);
                entity.Property(e => e.Standard).HasColumnName("standard").HasConversion<int>().IsRequired();
                entity.Property(e => e.TaxpayerType).HasColumnName("taxpayer_type").HasConversion<int>().IsRequired();
                entity.Property(e => e.BaseCurrency).HasColumnName("base_currency").IsRequired().HasMaxLength(50);
                entity.Property(e => e.StartYear).HasColumnName("start_year").IsRequired();
                entity.Property(e => e.StartMonth).HasColumnName("start_month").IsRequired();
                entity.Property(e => e.IsCurrent).HasColumnName("is_current").IsRequired();
                entity.Property(e => e.AccountCodingScheme).HasColumnName("account_coding_scheme").IsRequired().HasMaxLength(50).HasDefaultValue(AccountSet.DefaultCodingScheme);
                entity.Property(e => e.TaxpayerNumber).HasColumnName("taxpayer_number").HasMaxLength(50);
                entity.Property(e => e.TaxRegion).HasColumnName("tax_region").HasMaxLength(50);
                entity.Property(e => e.Industry).HasColumnName("industry").HasMaxLength(50);
                entity.Property(e => e.TaxLoginType).HasColumnName("tax_login_type").HasMaxLength(50);
                entity.Property(e => e.TaxIdentityType).HasColumnName("tax_identity_type").HasMaxLength(50);
                entity.Property(e => e.TaxLoginAccount).HasColumnName("tax_login_account").HasMaxLength(100);
                entity.Property(e => e.TaxLoginPasswordCipher).HasColumnName("tax_login_password_cipher").HasMaxLength(500);
                entity.Property(e => e.IitChannel).HasColumnName("iit_channel").HasMaxLength(50);
                entity.Property(e => e.IitLoginType).HasColumnName("iit_login_type").HasMaxLength(50);
                entity.Property(e => e.IitLoginAccount).HasColumnName("iit_login_account").HasMaxLength(100);
                entity.Property(e => e.IitLoginPasswordCipher).HasColumnName("iit_login_password_cipher").HasMaxLength(500);
                // 这几列带库默认值（存量行 ALTER TABLE 补列要用）。EF 对带默认值的列，
                // 在「值等于哨兵」时按未设置处理、交给库默认，因此哨兵必须显式指向默认值本身，
                // 否则新建账套时把开关关掉会被静默写回默认。
                entity.Property(e => e.AssetsModuleEnabled).HasColumnName("assets_module_enabled").IsRequired().HasDefaultValue(true).HasSentinel(true);
                entity.Property(e => e.FundsModuleEnabled).HasColumnName("funds_module_enabled").IsRequired().HasDefaultValue(true).HasSentinel(true);
                entity.Property(e => e.VoucherAuditRequired).HasColumnName("voucher_audit_required").IsRequired().HasDefaultValue(true).HasSentinel(true);
                // 库默认值供存量行 ALTER TABLE 补列用；哨兵显式指向 0（非法枚举值），
                // 免得 EF 把「未设置」和「用户真的选了 1」混为一谈。
                entity.Property(e => e.BalanceAssistScope).HasColumnName("balance_assist_scope").HasConversion<int>().IsRequired().HasDefaultValue(BalanceAssistScope.All).HasSentinel((BalanceAssistScope)0);
                entity.Property(e => e.UnitDecimalPlaces).HasColumnName("unit_decimal_places").IsRequired().HasDefaultValue(AccountSet.DefaultUnitDecimalPlaces).HasSentinel(-1);
                entity.Property(e => e.IsExportEnterprise).HasColumnName("is_export_enterprise").IsRequired().HasDefaultValue(false);

                entity.Property(e => e.VatRate).HasColumnName("vat_rate").HasColumnType("numeric(5,2)").IsRequired().HasDefaultValue(13m);
                entity.Property(e => e.IncomeTaxRate).HasColumnName("income_tax_rate").HasColumnType("numeric(5,2)").IsRequired().HasDefaultValue(25m);
                // 附加税费率：库默认值给存量账套补法定 7 / 3 / 2，补列后即为可计提状态。
                entity.Property(e => e.CityBuildTaxRate).HasColumnName("city_build_tax_rate").HasColumnType("numeric(5,2)").IsRequired().HasDefaultValue(7m);
                entity.Property(e => e.EducationSurchargeRate).HasColumnName("education_surcharge_rate").HasColumnType("numeric(5,2)").IsRequired().HasDefaultValue(3m);
                entity.Property(e => e.LocalEducationSurchargeRate).HasColumnName("local_education_surcharge_rate").HasColumnType("numeric(5,2)").IsRequired().HasDefaultValue(2m);
                entity.Property(e => e.VoucherWords).HasColumnName("voucher_words").IsRequired().HasMaxLength(100).HasDefaultValue(AccountSet.DefaultVoucherWords);
                entity.Property(e => e.ClosedPeriod).HasColumnName("closed_period").IsRequired().HasDefaultValue(0);
                entity.Property(e => e.CarriedPeriod).HasColumnName("carried_period").IsRequired().HasDefaultValue(0);
                entity.Property(e => e.EnabledAssistTypes).HasColumnName("enabled_assist_types").IsRequired().HasMaxLength(50).HasDefaultValue(string.Empty);

                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.IsCurrent);
            });

            modelBuilder.Entity<TaxPortalSessionRecord>(entity =>
            {
                entity.ToTable("tax_portal_sessions");
                entity.HasKey(e => new { e.AccountSetId, e.UserId, e.PortalKind });

                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(100);
                entity.Property(e => e.PortalKind).HasColumnName("portal_kind");
                entity.Property(e => e.PayloadCipher).HasColumnName("payload_cipher").IsRequired().HasColumnType("text");
                entity.Property(e => e.SavedAt).HasColumnName("saved_at").IsRequired();
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

                // 登录态随账套删除一并清理，避免残留无法再使用的会话。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaxTaskRecord>(entity =>
            {
                entity.ToTable("tax_tasks");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserName).HasColumnName("user_name").IsRequired().HasMaxLength(100);
                entity.Property(e => e.Period).HasColumnName("period").IsRequired();
                entity.Property(e => e.Title).HasColumnName("title").IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(e => e.Suggestion).HasColumnName("suggestion").HasMaxLength(500);
                // 时间列按 UTC DateTime 存取：域模型保留 DateTimeOffset 的偏移语义，查询侧（按东八区自然日过滤、排序）在 PostgreSQL 与测试用的 SQLite 上都能翻译。
                entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at").IsRequired()
                    .HasConversion(v => v.UtcDateTime, v => new DateTimeOffset(v, TimeSpan.Zero));
                entity.Property(e => e.FinishedAt).HasColumnName("finished_at").IsRequired()
                    .HasConversion(v => v.UtcDateTime, v => new DateTimeOffset(v, TimeSpan.Zero));

                // 任务中心只按「账套 + 提交时间区间」查，账套删除时台账一并清掉。
                entity.HasIndex(e => new { e.AccountSetId, e.SubmittedAt });
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("accounts");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(32);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                entity.Property(e => e.Category).HasColumnName("category").HasConversion<int>().IsRequired();
                entity.Property(e => e.Direction).HasColumnName("direction").HasConversion<int>().IsRequired();
                entity.Property(e => e.ParentId).HasColumnName("parent_id");
                entity.Property(e => e.Level).HasColumnName("level").IsRequired();
                entity.Property(e => e.IsLeaf).HasColumnName("is_leaf").IsRequired();
                entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").IsRequired();
                entity.Property(e => e.IsPreset).HasColumnName("is_preset").IsRequired();
                entity.Property(e => e.SubCategory).HasColumnName("sub_category").HasConversion<int>();
                entity.Property(e => e.IsCashAccount).HasColumnName("is_cash_account").IsRequired().HasDefaultValue(false);
                entity.Property(e => e.IsQuantityAccounted).HasColumnName("is_quantity_accounted").IsRequired().HasDefaultValue(false);
                entity.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(Account.MaxUnitLength);
                entity.Property(e => e.IsForeignCurrencyAccounted).HasColumnName("is_foreign_currency_accounted").IsRequired().HasDefaultValue(false);
                entity.Property(e => e.Mnemonic).HasColumnName("mnemonic").HasMaxLength(50);
                entity.Property(e => e.OpeningBalance).HasColumnName("opening_balance").HasColumnType("numeric(18,2)").IsRequired().HasDefaultValue(0m);

                // 科目编码在同一账套内唯一。
                entity.HasIndex(e => new { e.AccountSetId, e.Code }).IsUnique();
                entity.HasIndex(e => e.ParentId);

                // 科目归属于账套；账套非空时不可删除（Restrict）。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);

                // 自引用：上级科目 -> 下级明细科目集合。删除时限制，避免误删带下级的科目。
                entity.HasOne(e => e.Parent)
                      .WithMany(e => e.Children)
                      .HasForeignKey(e => e.ParentId)
                      .OnDelete(DeleteBehavior.Restrict);

                // 科目包含若干辅助核算维度设置；科目删除时一并清除。
                entity.HasMany(e => e.AssistTypes)
                      .WithOne()
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);

                // 外币核算科目可选的币别；科目删除时一并清除。
                entity.HasMany(e => e.Currencies)
                      .WithOne()
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Children 通过背景字段 _children 访问，读写均走字段而非属性。
                entity.Metadata
                      .FindNavigation(nameof(Account.Children))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);

                // AssistTypes 同样通过背景字段访问。
                entity.Metadata
                      .FindNavigation(nameof(Account.AssistTypes))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);

                entity.Metadata
                      .FindNavigation(nameof(Account.Currencies))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<AccountCurrency>(entity =>
            {
                entity.ToTable("account_currencies");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.CurrencyId).HasColumnName("currency_id").IsRequired();

                // 同一科目同一币别只保留一条。
                entity.HasIndex(e => new { e.AccountId, e.CurrencyId }).IsUnique();

                // 币别档案被删除时不级联清除科目上的取值，由写入校验保证可选性（见 AccountsController）。
                entity.HasIndex(e => e.CurrencyId);
            });

            modelBuilder.Entity<PresetAccount>(entity =>
            {
                entity.ToTable("preset_accounts");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.Standard).HasColumnName("standard").HasConversion<int>().IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(32);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                entity.Property(e => e.Category).HasColumnName("category").HasConversion<int>().IsRequired();
                entity.Property(e => e.Direction).HasColumnName("direction").HasConversion<int>().IsRequired();
                entity.Property(e => e.ParentCode).HasColumnName("parent_code").HasMaxLength(32);
                entity.Property(e => e.OrderNo).HasColumnName("order_no").IsRequired();

                // 同一准则内科目编码唯一；建树按 (standard, order_no) 顺序读，保证父级先于子级。
                entity.HasIndex(e => new { e.Standard, e.Code }).IsUnique();
                entity.HasIndex(e => new { e.Standard, e.OrderNo });

                // 清单来源是 PresetChart（两套准则共 467 行），改清单要另生成一个迁移。
                entity.HasData(PresetChart.SeedRows());
            });

            modelBuilder.Entity<Voucher>(entity =>
            {
                entity.ToTable("vouchers");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Word).HasColumnName("word").IsRequired().HasMaxLength(8);
                entity.Property(e => e.Number).HasColumnName("number").IsRequired();
                entity.Property(e => e.Date).HasColumnName("date").IsRequired();
                entity.Property(e => e.AttachmentCount).HasColumnName("attachment_count").IsRequired();
                entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(255);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(e => e.Preparer).HasColumnName("preparer").IsRequired().HasMaxLength(50);
                entity.Property(e => e.Reviewer).HasColumnName("reviewer").HasMaxLength(50);

                // 凭证字/号在同一期间内唯一，但外部平台按月重排号，故不设全局唯一约束；
                // 仅按「账套+字+号+日期」建索引，供导入去重与列表检索使用。
                entity.HasIndex(e => new { e.AccountSetId, e.Word, e.Number, e.Date });

                // 凭证归属于账套；账套非空时不可删除（Restrict）。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);

                // 一张凭证包含多条分录；分录通过背景字段 _entries 访问。
                entity.HasMany(e => e.Entries)
                      .WithOne()
                      .HasForeignKey(e => e.VoucherId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Metadata
                      .FindNavigation(nameof(Voucher.Entries))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<VoucherAttachment>(entity =>
            {
                entity.ToTable("voucher_attachments");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id").IsRequired();
                entity.Property(e => e.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(200);
                entity.Property(e => e.ContentType).HasColumnName("content_type").IsRequired().HasMaxLength(100);
                entity.Property(e => e.SizeBytes).HasColumnName("size_bytes").IsRequired();
                entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at").IsRequired();
                entity.Property(e => e.Content).HasColumnName("content").IsRequired();

                entity.HasIndex(e => e.VoucherId);

                entity.HasOne<Voucher>()
                      .WithMany()
                      .HasForeignKey(e => e.VoucherId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<VoucherEntry>(entity =>
            {
                entity.ToTable("voucher_entries");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id").IsRequired();
                entity.Property(e => e.LineNumber).HasColumnName("line_number").IsRequired();
                entity.Property(e => e.Summary).HasColumnName("summary").IsRequired().HasMaxLength(200);
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.Debit).HasColumnName("debit").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Credit).HasColumnName("credit").HasColumnType("numeric(18,2)").IsRequired();

                entity.HasIndex(e => e.VoucherId);
                entity.HasIndex(e => e.AccountId);

                // 一条分录可挂多个辅助核算取值；分录删除时一并清除。
                entity.HasMany(e => e.Assists)
                      .WithOne()
                      .HasForeignKey(e => e.VoucherEntryId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Metadata
                      .FindNavigation(nameof(VoucherEntry.Assists))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<VoucherEntryAssist>(entity =>
            {
                entity.ToTable("voucher_entry_assists");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.VoucherEntryId).HasColumnName("voucher_entry_id").IsRequired();
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();
                entity.Property(e => e.AssistItemId).HasColumnName("assist_item_id").IsRequired();

                // 同一分录同一档案只登记一次（防御导入或并发产生的重复维度）。
                entity.HasIndex(e => new { e.VoucherEntryId, e.Type, e.AssistItemId }).IsUnique();
                entity.HasIndex(e => e.VoucherEntryId);
                // 账簿按「科目 × 档案」聚合的覆盖索引。
                entity.HasIndex(e => new { e.AccountId, e.AssistItemId });
                entity.HasIndex(e => e.AssistItemId);

                // 档案被分录引用时不允许级联删除档案，改由应用层做删除保护。
                entity.HasOne<AssistItem>()
                      .WithMany()
                      .HasForeignKey(e => e.AssistItemId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AccountAssistType>(entity =>
            {
                entity.ToTable("account_assist_types");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();
                entity.Property(e => e.IsRequired).HasColumnName("is_required").IsRequired().HasDefaultValue(true);

                // 同一科目同一维度只保留一条。
                entity.HasIndex(e => new { e.AccountId, e.Type }).IsUnique();

                // 维度设置引用辅助核算类别为枚举，无需外键；删除科目时级联清除（见 Account 配置）。
            });

            modelBuilder.Entity<AssistItem>(entity =>
            {
                entity.ToTable("assist_items");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(AssistItem.MaxCodeLength);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                entity.Property(e => e.ParentId).HasColumnName("parent_id");
                entity.Property(e => e.Level).HasColumnName("level").IsRequired();
                entity.Property(e => e.IsLeaf).HasColumnName("is_leaf").IsRequired();
                entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").IsRequired();
                entity.Property(e => e.Contact).HasColumnName("contact").HasMaxLength(200);
                entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);

                // 档案编码在同一账套同一类别内唯一。
                entity.HasIndex(e => new { e.AccountSetId, e.Type, e.Code }).IsUnique();
                // 账簿与档案列表均按「账套 + 类别」取数。
                entity.HasIndex(e => new { e.AccountSetId, e.Type });
                entity.HasIndex(e => e.ParentId);

                // 档案归属于账套；账套非空时不可删除（Restrict）。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);

                // 自引用：上级档案 -> 下级档案集合。
                entity.HasOne(e => e.Parent)
                      .WithMany(e => e.Children)
                      .HasForeignKey(e => e.ParentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Metadata
                      .FindNavigation(nameof(AssistItem.Children))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<Journal>(entity =>
            {
                entity.ToTable("journals");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(16);
                entity.Property(e => e.Currency).HasColumnName("currency").IsRequired().HasMaxLength(20).HasDefaultValue("人民币");
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.OpeningBalance).HasColumnName("opening_balance").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").IsRequired().HasDefaultValue(true);
                entity.Property(e => e.BankName).HasColumnName("bank_name").HasMaxLength(50);
                entity.Property(e => e.BankAccountNo).HasColumnName("bank_account_no").HasMaxLength(50);

                entity.HasIndex(e => new { e.AccountSetId, e.AccountId });

                // 日记账归属于账套；账套非空时不可删除（Restrict）。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);

                // 一本日记账包含多条分录；分录通过背景字段 _entries 访问。
                entity.HasMany(e => e.Entries)
                      .WithOne()
                      .HasForeignKey(e => e.JournalId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Metadata
                      .FindNavigation(nameof(Journal.Entries))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<JournalEntry>(entity =>
            {
                entity.ToTable("journal_entries");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.JournalId).HasColumnName("journal_id").IsRequired();
                entity.Property(e => e.Sequence).HasColumnName("sequence").IsRequired();
                entity.Property(e => e.Date).HasColumnName("date").IsRequired();
                entity.Property(e => e.Summary).HasColumnName("summary").IsRequired().HasMaxLength(200);
                entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
                entity.Property(e => e.Counterparty).HasColumnName("counterparty").HasMaxLength(100);
                entity.Property(e => e.Project).HasColumnName("project").HasMaxLength(100);
                entity.Property(e => e.Department).HasColumnName("department").HasMaxLength(100);
                entity.Property(e => e.Income).HasColumnName("income").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Expense).HasColumnName("expense").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Balance).HasColumnName("balance").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id");
                entity.Property(e => e.SettlementMethod).HasColumnName("settlement_method").HasMaxLength(50);
                entity.Property(e => e.BillNo).HasColumnName("bill_no").HasMaxLength(50);
                entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(200);
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);

                entity.HasIndex(e => e.JournalId);
                entity.HasIndex(e => e.VoucherId);
            });

            modelBuilder.Entity<EntryCategory>(entity =>
            {
                entity.ToTable("entry_categories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(EntryCategory.MaxCodeLength);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(EntryCategory.MaxNameLength);
                entity.Property(e => e.ParentId).HasColumnName("parent_id");
                entity.Property(e => e.CashFlowItemId).HasColumnName("cash_flow_item_id");
                entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").IsRequired();
                entity.Property(e => e.SmartKeywords).HasColumnName("smart_keywords").HasMaxLength(EntryCategory.MaxKeywordsLength);
                entity.Property(e => e.VoucherSubjectId).HasColumnName("voucher_subject_id");

                // 同一账套同一类型内类别名称唯一；编码在「账套 + 类型」内唯一（收入与支出各自从 1 编号）。
                entity.HasIndex(e => new { e.AccountSetId, e.Type, e.Name }).IsUnique();
                entity.HasIndex(e => new { e.AccountSetId, e.Type, e.Code }).IsUnique();

                // 收支类别归属于账套；账套非空时不可删除（Restrict）。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ClosingTemplate>(entity =>
            {
                entity.ToTable("closing_templates");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(ClosingTemplate.MaxNameLength);
                entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").IsRequired().HasDefaultValue(true);

                // 同一账套内模板名唯一。
                entity.HasIndex(e => new { e.AccountSetId, e.Name }).IsUnique();

                // 模板归属于账套；账套非空时不可删除（Restrict）。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);

                // 模板包含若干行；整体替换模板行时旧行级联清除。
                entity.HasMany(e => e.Lines)
                      .WithOne()
                      .HasForeignKey(l => l.TemplateId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Metadata
                      .FindNavigation(nameof(ClosingTemplate.Lines))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<ClosingTemplateLine>(entity =>
            {
                entity.ToTable("closing_template_lines");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.TemplateId).HasColumnName("template_id").IsRequired();
                entity.Property(e => e.LineNumber).HasColumnName("line_number").IsRequired();
                entity.Property(e => e.Summary).HasColumnName("summary").IsRequired().HasMaxLength(ClosingTemplateLine.MaxSummaryLength);
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.Direction).HasColumnName("direction").HasConversion<int>().IsRequired();
                entity.Property(e => e.SourceType).HasColumnName("source_type").HasConversion<int>().IsRequired();
                entity.Property(e => e.SourceAccountId).HasColumnName("source_account_id");
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();

                entity.HasIndex(e => e.TemplateId);
            });

            modelBuilder.Entity<Currency>(entity =>
            {
                entity.ToTable("currencies");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(Currency.MaxCodeLength);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(Currency.MaxNameLength);
                entity.Property(e => e.Rate).HasColumnName("rate").HasColumnType("numeric(18,6)").IsRequired();
                entity.Property(e => e.ConvertType).HasColumnName("convert_type").HasConversion<int>().IsRequired().HasDefaultValue(CurrencyConvertType.Multiply).HasSentinel((CurrencyConvertType)0);
                entity.Property(e => e.IsBase).HasColumnName("is_base").IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.Code }).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<VoucherWord>(entity =>
            {
                entity.ToTable("voucher_words");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Word).HasColumnName("word").IsRequired().HasMaxLength(VoucherWord.MaxWordLength);
                entity.Property(e => e.PrintTitle).HasColumnName("print_title").IsRequired().HasMaxLength(VoucherWord.MaxPrintTitleLength);
                entity.Property(e => e.IsDefault).HasColumnName("is_default").IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.Word }).IsUnique();
                entity.HasIndex(e => new { e.AccountSetId, e.IsDefault });

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<VoucherTemplate>(entity =>
            {
                entity.ToTable("voucher_templates");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(VoucherTemplate.MaxNameLength);
                entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.Name }).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Lines)
                      .WithOne()
                      .HasForeignKey(l => l.TemplateId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Metadata
                      .FindNavigation(nameof(VoucherTemplate.Lines))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<VoucherTemplateLine>(entity =>
            {
                entity.ToTable("voucher_template_lines");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.TemplateId).HasColumnName("template_id").IsRequired();
                entity.Property(e => e.LineNumber).HasColumnName("line_number").IsRequired();
                entity.Property(e => e.Summary).HasColumnName("summary").IsRequired().HasMaxLength(VoucherTemplate.MaxSummaryLength);
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.IsDebit).HasColumnName("is_debit").IsRequired();

                entity.HasIndex(e => e.TemplateId);
            });

            modelBuilder.Entity<Bill>(entity =>
            {
                entity.ToTable("bills");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
                entity.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
                entity.Property(e => e.BillNo).HasColumnName("bill_no").IsRequired().HasMaxLength(Bill.MaxBillNoLength);
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Kind).HasColumnName("kind").HasConversion<int>().IsRequired();
                entity.Property(e => e.DueDate).HasColumnName("due_date");
                entity.Property(e => e.Drawer).HasColumnName("drawer").HasMaxLength(100);
                entity.Property(e => e.Payee).HasColumnName("payee").HasMaxLength(100);
                entity.Property(e => e.Acceptor).HasColumnName("acceptor").HasMaxLength(100);
                entity.Property(e => e.DrawerAccountNo).HasColumnName("drawer_account_no").HasMaxLength(50);
                entity.Property(e => e.PayeeAccountNo).HasColumnName("payee_account_no").HasMaxLength(50);
                entity.Property(e => e.AcceptorAccountNo).HasColumnName("acceptor_account_no").HasMaxLength(50);
                entity.Property(e => e.DrawerBank).HasColumnName("drawer_bank").HasMaxLength(100);
                entity.Property(e => e.PayeeBank).HasColumnName("payee_bank").HasMaxLength(100);
                entity.Property(e => e.AcceptorBank).HasColumnName("acceptor_bank").HasMaxLength(100);
                entity.Property(e => e.Transferable).HasColumnName("transferable").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id");

                entity.HasIndex(e => new { e.AccountSetId, e.BillNo }).IsUnique();
                entity.HasIndex(e => new { e.AccountSetId, e.IssueDate });

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BillAttachment>(entity =>
            {
                entity.ToTable("bill_attachments");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.BillId).HasColumnName("bill_id").IsRequired();
                entity.Property(e => e.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(200);
                entity.Property(e => e.ContentType).HasColumnName("content_type").IsRequired().HasMaxLength(120);
                entity.Property(e => e.SizeBytes).HasColumnName("size_bytes").IsRequired();
                entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at").IsRequired();
                entity.Property(e => e.Content).HasColumnName("content").IsRequired();

                entity.HasIndex(e => e.BillId);

                entity.HasOne<Bill>()
                      .WithMany()
                      .HasForeignKey(e => e.BillId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FixedAssetCategory>(entity =>
            {
                entity.ToTable("fixed_asset_categories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(FixedAssetCategory.MaxCodeLength);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(FixedAssetCategory.MaxNameLength);
                entity.Property(e => e.Method).HasColumnName("method").HasConversion<int>().IsRequired();
                entity.Property(e => e.Attr).HasColumnName("attr").HasConversion<int>().IsRequired();
                entity.Property(e => e.SubjectAccountId).HasColumnName("subject_account_id").IsRequired();
                entity.Property(e => e.Months).HasColumnName("months").IsRequired();
                entity.Property(e => e.ResidualRate).HasColumnName("residual_rate").HasColumnType("numeric(5,2)").IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.Code }).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<FixedAsset>(entity =>
            {
                entity.ToTable("fixed_assets");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(FixedAsset.MaxCodeLength);
                entity.Property(e => e.CategoryId).HasColumnName("category_id").IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(FixedAsset.MaxNameLength);
                entity.Property(e => e.Model).HasColumnName("model").HasMaxLength(FixedAsset.MaxTextLength);
                entity.Property(e => e.Dept).HasColumnName("dept").HasMaxLength(FixedAsset.MaxTextLength);
                entity.Property(e => e.StartDate).HasColumnName("start_date").IsRequired();
                entity.Property(e => e.OriginValue).HasColumnName("origin_value").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.AccumulatedDepreciation).HasColumnName("accumulated_depreciation").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(e => e.IsInitial).HasColumnName("is_initial").HasDefaultValue(false).IsRequired();
                entity.Property(e => e.LastDepreciatedPeriod).HasColumnName("last_depreciated_period").IsRequired();
                entity.Property(e => e.ClearedDate).HasColumnName("cleared_date");

                entity.HasIndex(e => new { e.AccountSetId, e.Code }).IsUnique();
                entity.HasIndex(e => e.CategoryId);

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<FixedAssetChange>(entity =>
            {
                entity.ToTable("fixed_asset_changes");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.AssetId).HasColumnName("asset_id").IsRequired();
                entity.Property(e => e.Kind).HasColumnName("kind").HasConversion<int>().IsRequired();
                entity.Property(e => e.Before).HasColumnName("before").IsRequired().HasMaxLength(FixedAssetChange.MaxTextLength);
                entity.Property(e => e.After).HasColumnName("after").IsRequired().HasMaxLength(FixedAssetChange.MaxTextLength);
                entity.Property(e => e.Period).HasColumnName("period").IsRequired();
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id");

                entity.HasIndex(e => new { e.AccountSetId, e.AssetId });
                entity.HasIndex(e => new { e.AccountSetId, e.Period });
            });

            modelBuilder.Entity<FixedAssetVoucherSetting>(entity =>
            {
                entity.ToTable("fixed_asset_voucher_settings");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.DepreciationExpenseAccountId).HasColumnName("depreciation_expense_account_id");
                entity.Property(e => e.DepreciationAccountId).HasColumnName("depreciation_account_id");
                entity.Property(e => e.AmortizationExpenseAccountId).HasColumnName("amortization_expense_account_id");
                entity.Property(e => e.AmortizationAccountId).HasColumnName("amortization_account_id");
                entity.Property(e => e.DisposalAccountId).HasColumnName("disposal_account_id");

                entity.HasIndex(e => e.AccountSetId).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ExpenseBill>(entity =>
            {
                entity.ToTable("expense_bills");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.BillNo).HasColumnName("bill_no").IsRequired().HasMaxLength(ExpenseBill.MaxBillNoLength);
                entity.Property(e => e.BillDate).HasColumnName("bill_date").IsRequired();
                entity.Property(e => e.Reason).HasColumnName("reason").IsRequired().HasMaxLength(ExpenseBill.MaxTextLength);
                entity.Property(e => e.ExpenseType).HasColumnName("expense_type").IsRequired().HasMaxLength(ExpenseBill.MaxTextLength);
                entity.Property(e => e.Payee).HasColumnName("payee").HasMaxLength(ExpenseBill.MaxTextLength);
                entity.Property(e => e.SettleMethod).HasColumnName("settle_method").HasMaxLength(50);
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.DeductibleTax).HasColumnName("deductible_tax").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.BillNo }).IsUnique();
                entity.HasIndex(e => new { e.AccountSetId, e.BillDate });

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AccountSetMember>(entity =>
            {
                entity.ToTable("account_set_members");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(32);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(20);
                entity.Property(e => e.Phone).HasColumnName("phone").IsRequired().HasMaxLength(20);
                entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
                entity.Property(e => e.RoleId).HasColumnName("role_id").IsRequired();
                entity.Property(e => e.Enabled).HasColumnName("enabled").IsRequired();
                entity.Property(e => e.IsOwner).HasColumnName("is_owner").IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.Phone });
                // 访问校验按「登录用户 → 所在账套」查，成员表是唯一的权限来源。
                entity.HasIndex(e => e.UserId);

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<AccountSetRole>()
                      .WithMany()
                      .HasForeignKey(e => e.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AccountSetRole>(entity =>
            {
                entity.ToTable("account_set_roles");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(AccountSetRole.MaxNameLength);
                entity.Property(e => e.IsSystem).HasColumnName("is_system").IsRequired();
                entity.Property(e => e.IsAdmin).HasColumnName("is_admin").IsRequired();
                entity.Property(e => e.Permissions).HasColumnName("permissions").IsRequired().HasMaxLength(2000);
                entity.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.Name }).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.ToTable("invoices");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Direction).HasColumnName("direction").HasConversion<int>().IsRequired();
                entity.Property(e => e.InvoiceType).HasColumnName("invoice_type").IsRequired().HasMaxLength(50);
                entity.Property(e => e.InputDate).HasColumnName("input_date").IsRequired();
                entity.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
                entity.Property(e => e.InvoiceNo).HasColumnName("invoice_no").IsRequired().HasMaxLength(Invoice.MaxInvoiceNoLength);
                entity.Property(e => e.PartnerName).HasColumnName("partner_name").IsRequired().HasMaxLength(Invoice.MaxPartnerLength);
                entity.Property(e => e.BizType).HasColumnName("biz_type").HasMaxLength(50);
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
                entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(e => e.AuthDate).HasColumnName("auth_date");
                entity.Property(e => e.TaxPeriod).HasColumnName("tax_period");
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id");

                entity.HasIndex(e => new { e.AccountSetId, e.Direction, e.InvoiceNo }).IsUnique();
                entity.HasIndex(e => new { e.AccountSetId, e.Direction, e.IssueDate });
                entity.HasIndex(e => new { e.AccountSetId, e.Status });

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Lines)
                      .WithOne()
                      .HasForeignKey(l => l.InvoiceId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Metadata
                      .FindNavigation(nameof(Invoice.Lines))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<InvoiceLine>(entity =>
            {
                entity.ToTable("invoice_lines");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.InvoiceId).HasColumnName("invoice_id").IsRequired();
                entity.Property(e => e.LineNumber).HasColumnName("line_number").IsRequired();
                entity.Property(e => e.GoodsName).HasColumnName("goods_name").IsRequired().HasMaxLength(200);
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.TaxRate).HasColumnName("tax_rate").HasColumnType("numeric(5,2)").IsRequired();
                entity.Property(e => e.TaxAmount).HasColumnName("tax_amount").HasColumnType("numeric(18,2)").IsRequired();

                entity.HasIndex(e => e.InvoiceId);
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("employees");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(Employee.MaxCodeLength);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(Employee.MaxNameLength);
                entity.Property(e => e.Gender).HasColumnName("gender").HasConversion<int>().IsRequired();
                entity.Property(e => e.IdType).HasColumnName("id_type").HasConversion<int>().IsRequired();
                entity.Property(e => e.IdNo).HasColumnName("id_no").HasMaxLength(30);
                entity.Property(e => e.Dept).HasColumnName("dept").HasMaxLength(100);
                entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
                entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
                entity.Property(e => e.BirthDate).HasColumnName("birth_date");
                entity.Property(e => e.HireDate).HasColumnName("hire_date");
                entity.Property(e => e.LeaveDate).HasColumnName("leave_date");
                entity.Property(e => e.BankName).HasColumnName("bank_name").HasMaxLength(100);
                entity.Property(e => e.BankAccountNo).HasColumnName("bank_account_no").HasMaxLength(50);
                entity.Property(e => e.Post).HasColumnName("post").HasMaxLength(50);
                entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(50);
                entity.Property(e => e.Education).HasColumnName("education").HasMaxLength(20);
                entity.Property(e => e.IsDisabled).HasColumnName("is_disabled").IsRequired();
                entity.Property(e => e.IsMartyrFamily).HasColumnName("is_martyr_family").IsRequired();
                entity.Property(e => e.IsSoloElder).HasColumnName("is_solo_elder").IsRequired();
                entity.Property(e => e.IsSeniorExpert).HasColumnName("is_senior_expert");
                entity.Property(e => e.EmploymentType).HasColumnName("employment_type").HasConversion<int>().IsRequired();
                entity.Property(e => e.DeductBasicExpense).HasColumnName("deduct_basic_expense").IsRequired();
                entity.Property(e => e.PayStartDate).HasColumnName("pay_start_date");
                entity.Property(e => e.SubjectAccountId).HasColumnName("subject_account_id");
                entity.Property(e => e.PaysSocialInsurance).HasColumnName("pays_social_insurance").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);

                entity.HasIndex(e => new { e.AccountSetId, e.Code }).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<InsuranceItem>(entity =>
            {
                entity.ToTable("insurance_items");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(InsuranceItem.MaxNameLength);
                entity.Property(e => e.CompanyRate).HasColumnName("company_rate").HasColumnType("numeric(5,2)").IsRequired();
                entity.Property(e => e.PersonalRate).HasColumnName("personal_rate").HasColumnType("numeric(5,2)");
                entity.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
                entity.Property(e => e.BaseAmount).HasColumnName("base_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m).IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.Name }).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EmployeeInsurance>(entity =>
            {
                entity.ToTable("employee_insurances");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.EmployeeId).HasColumnName("employee_id").IsRequired();
                entity.Property(e => e.InsuranceItemId).HasColumnName("insurance_item_id").IsRequired();
                entity.Property(e => e.InsuranceBase).HasColumnName("insurance_base").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.CompanyRate).HasColumnName("company_rate").HasColumnType("numeric(5,2)");
                entity.Property(e => e.PersonalRate).HasColumnName("personal_rate").HasColumnType("numeric(5,2)");

                entity.HasIndex(e => new { e.EmployeeId, e.InsuranceItemId }).IsUnique();

                entity.HasOne<Employee>()
                      .WithMany()
                      .HasForeignKey(e => e.EmployeeId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<InsuranceItem>()
                      .WithMany()
                      .HasForeignKey(e => e.InsuranceItemId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalaryBill>(entity =>
            {
                entity.ToTable("salary_bills");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Period).HasColumnName("period").IsRequired();
                entity.Property(e => e.EmployeeId).HasColumnName("employee_id").IsRequired();
                entity.Property(e => e.Gross).HasColumnName("gross").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Deductible).HasColumnName("deductible").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.PersonalDeduction).HasColumnName("personal_deduction").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.TaxThreshold).HasColumnName("tax_threshold").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Tax).HasColumnName("tax").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Adjust).HasColumnName("adjust").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.CompanyPart).HasColumnName("company_part").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.AccruedVoucherId).HasColumnName("accrued_voucher_id");
                entity.Property(e => e.PaidVoucherId).HasColumnName("paid_voucher_id");

                entity.HasIndex(e => new { e.AccountSetId, e.Period, e.EmployeeId }).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SalaryVoucherSetting>(entity =>
            {
                entity.ToTable("salary_voucher_settings");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.PayrollAccountId).HasColumnName("payroll_account_id");
                entity.Property(e => e.TaxAccountId).HasColumnName("tax_account_id");
                entity.Property(e => e.InsuranceAccountId).HasColumnName("insurance_account_id");
                entity.Property(e => e.BankAccountId).HasColumnName("bank_account_id");

                entity.HasIndex(e => e.AccountSetId).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ArchiveItem>(entity =>
            {
                entity.ToTable("archive_items");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(ArchiveItem.MaxFileNameLength);
                entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(ArchiveItem.MaxCategoryPathLength);
                entity.Property(e => e.DocType).HasColumnName("doc_type").HasMaxLength(50);
                entity.Property(e => e.Content).HasColumnName("content");
                entity.Property(e => e.FileSize).HasColumnName("file_size").IsRequired();
                entity.Property(e => e.RelatedDoc).HasColumnName("related_doc").HasMaxLength(50);
                entity.Property(e => e.RelatedNo).HasColumnName("related_no").HasMaxLength(50);
                entity.Property(e => e.VoucherText).HasColumnName("voucher_text").HasMaxLength(50);
                entity.Property(e => e.Matter).HasColumnName("matter").HasMaxLength(200);
                entity.Property(e => e.CheckStatus).HasColumnName("check_status").HasMaxLength(100);
                entity.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(e => e.Uploader).HasColumnName("uploader").IsRequired().HasMaxLength(50);
                entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
                entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at").IsRequired();
                entity.Property(e => e.IsArchived).HasColumnName("is_archived").IsRequired();
                entity.Property(e => e.IsPaper).HasColumnName("is_paper").IsRequired();
                entity.Property(e => e.ArchivedAt).HasColumnName("archived_at");
                entity.Property(e => e.ArchivedBy).HasColumnName("archived_by").HasMaxLength(50);

                entity.HasIndex(e => new { e.AccountSetId, e.IsArchived });

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ArchiveCategory>(entity =>
            {
                entity.ToTable("archive_categories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.ParentId).HasColumnName("parent_id");
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(ArchiveCategory.MaxNameLength);
                entity.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
                entity.Property(e => e.IsSystem).HasColumnName("is_system").IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

                entity.HasIndex(e => new { e.AccountSetId, e.ParentId });

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<ArchiveCategory>()
                      .WithMany()
                      .HasForeignKey(e => e.ParentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OperationLog>(entity =>
            {
                entity.ToTable("operation_logs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(50);
                entity.Property(e => e.UserName).HasColumnName("user_name").IsRequired().HasMaxLength(50);
                entity.Property(e => e.Type).HasColumnName("type").IsRequired().HasMaxLength(20);
                entity.Property(e => e.Module).HasColumnName("module").IsRequired().HasMaxLength(50);
                entity.Property(e => e.Log).HasColumnName("log").IsRequired().HasMaxLength(OperationLog.MaxLogLength);
                entity.Property(e => e.Time).HasColumnName("time").IsRequired();
                entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(50);
                entity.Property(e => e.EntityId).HasColumnName("entity_id").HasMaxLength(64);
                entity.Property(e => e.BeforeJson).HasColumnName("before_json").HasMaxLength(OperationLog.MaxSnapshotLength);
                entity.Property(e => e.AfterJson).HasColumnName("after_json").HasMaxLength(OperationLog.MaxSnapshotLength);
                entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);

                entity.HasIndex(e => new { e.AccountSetId, e.Time });
            });

            modelBuilder.Entity<YunCai.Modules.Budgets.Domain.Budget>(entity =>
            {
                entity.ToTable("budgets");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Year).HasColumnName("year").IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired()
                    .HasMaxLength(YunCai.Modules.Budgets.Domain.Budget.MaxNameLength);
                entity.Property(e => e.Note).HasColumnName("note")
                    .HasMaxLength(YunCai.Modules.Budgets.Domain.Budget.MaxNoteLength);
                entity.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

                entity.HasMany(e => e.Lines)
                    .WithOne()
                    .HasForeignKey(l => l.BudgetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.AccountSetId, e.Year, e.Name }).IsUnique();
            });

            modelBuilder.Entity<YunCai.Modules.Budgets.Domain.BudgetLine>(entity =>
            {
                entity.ToTable("budget_lines");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.BudgetId).HasColumnName("budget_id").IsRequired();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                for (var month = 1; month <= YunCai.Modules.Budgets.Domain.BudgetLine.MonthCount; month++)
                {
                    entity.Property<decimal>($"M{month:00}")
                        .HasColumnName($"m{month:00}")
                        .HasPrecision(18, 2)
                        .IsRequired();
                }

                entity.HasIndex(e => new { e.BudgetId, e.AccountId }).IsUnique();
                entity.HasIndex(e => e.AccountSetId);
            });

            modelBuilder.Entity<Transfer>(entity =>
            {
                entity.ToTable("transfers");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Date).HasColumnName("date").IsRequired();
                entity.Property(e => e.Summary).HasColumnName("summary").IsRequired().HasMaxLength(Transfer.MaxSummaryLength);
                entity.Property(e => e.FromJournalId).HasColumnName("from_journal_id").IsRequired();
                entity.Property(e => e.ToJournalId).HasColumnName("to_journal_id").IsRequired();
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
                entity.Property(e => e.FromEntryId).HasColumnName("from_entry_id").IsRequired();
                entity.Property(e => e.ToEntryId).HasColumnName("to_entry_id").IsRequired();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id");

                // 列表按账套 + 日期取数。
                entity.HasIndex(e => new { e.AccountSetId, e.Date });

                // 转账归属于账套；账套非空时不可删除（Restrict）。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CustomReport>(entity =>
            {
                entity.ToTable("custom_reports");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Code).HasColumnName("code").IsRequired().HasMaxLength(CustomReport.MaxCodeLength);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(CustomReport.MaxNameLength);
                entity.Property(e => e.ValueType).HasColumnName("value_type").HasConversion<int>().IsRequired();
                entity.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.ModifiedBy).HasColumnName("modified_by").HasMaxLength(50);
                entity.Property(e => e.ModifiedAt).HasColumnName("modified_at");

                // 报表编码在同一账套内唯一。
                entity.HasIndex(e => new { e.AccountSetId, e.Code }).IsUnique();

                // 报表定义归属于账套；账套非空时不可删除（Restrict）。
                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CashFlowFormula>(entity =>
            {
                entity.ToTable("cash_flow_formulas");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Line).HasColumnName("line_no").IsRequired();
                entity.Property(e => e.Expression).HasColumnName("expression").IsRequired().HasMaxLength(CashFlowFormula.MaxExpressionLength);
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

                // 同一账套同一行次只保留一条公式。
                entity.HasIndex(e => new { e.AccountSetId, e.Line }).IsUnique();

                entity.HasOne<AccountSet>()
                      .WithMany()
                      .HasForeignKey(e => e.AccountSetId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
