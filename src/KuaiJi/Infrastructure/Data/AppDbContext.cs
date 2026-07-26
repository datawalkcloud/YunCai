using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Journals;
using KuaiJi.Domain.Users;
using KuaiJi.Domain.Vouchers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KuaiJi.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;

        public DbSet<AccountSet> AccountSets { get; set; } = null!;

        public DbSet<Account> Accounts { get; set; } = null!;

        public DbSet<Journal> Journals { get; set; } = null!;

        public DbSet<Voucher> Vouchers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Username).HasColumnName("username").IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
                entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100);

                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<AccountSet>(entity =>
            {
                entity.ToTable("account_sets");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
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
                entity.Property(e => e.VatRate).HasColumnName("vat_rate").HasColumnType("numeric(5,2)").IsRequired().HasDefaultValue(13m);
                entity.Property(e => e.IncomeTaxRate).HasColumnName("income_tax_rate").HasColumnType("numeric(5,2)").IsRequired().HasDefaultValue(25m);
                entity.Property(e => e.VoucherWords).HasColumnName("voucher_words").IsRequired().HasMaxLength(100).HasDefaultValue(AccountSet.DefaultVoucherWords);
                entity.Property(e => e.ClosedPeriod).HasColumnName("closed_period").IsRequired().HasDefaultValue(0);

                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.IsCurrent);
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("accounts");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
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

                // Children 通过背景字段 _children 访问，读写均走字段而非属性。
                entity.Metadata
                      .FindNavigation(nameof(Account.Children))!
                      .SetPropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<Voucher>(entity =>
            {
                entity.ToTable("vouchers");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Word).HasColumnName("word").IsRequired().HasMaxLength(8);
                entity.Property(e => e.Number).HasColumnName("number").IsRequired();
                entity.Property(e => e.Date).HasColumnName("date").IsRequired();
                entity.Property(e => e.AttachmentCount).HasColumnName("attachment_count").IsRequired();
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

            modelBuilder.Entity<VoucherEntry>(entity =>
            {
                entity.ToTable("voucher_entries");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id").IsRequired();
                entity.Property(e => e.LineNumber).HasColumnName("line_number").IsRequired();
                entity.Property(e => e.Summary).HasColumnName("summary").IsRequired().HasMaxLength(200);
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.Debit).HasColumnName("debit").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Credit).HasColumnName("credit").HasColumnType("numeric(18,2)").IsRequired();

                entity.HasIndex(e => e.VoucherId);
                entity.HasIndex(e => e.AccountId);
            });

            modelBuilder.Entity<Journal>(entity =>
            {
                entity.ToTable("journals");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.AccountSetId).HasColumnName("account_set_id").IsRequired();
                entity.Property(e => e.Type).HasColumnName("type").HasConversion<int>().IsRequired();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
                entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(e => e.OpeningBalance).HasColumnName("opening_balance").HasColumnType("numeric(18,2)").IsRequired();

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

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.JournalId).HasColumnName("journal_id").IsRequired();
                entity.Property(e => e.Sequence).HasColumnName("sequence").IsRequired();
                entity.Property(e => e.Date).HasColumnName("date").IsRequired();
                entity.Property(e => e.Summary).HasColumnName("summary").IsRequired().HasMaxLength(200);
                entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
                entity.Property(e => e.Counterparty).HasColumnName("counterparty").HasMaxLength(100);
                entity.Property(e => e.Income).HasColumnName("income").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Expense).HasColumnName("expense").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.Balance).HasColumnName("balance").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id");

                entity.HasIndex(e => e.JournalId);
                entity.HasIndex(e => e.VoucherId);
            });
        }
    }
}
