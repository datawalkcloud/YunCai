using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Users;
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

        public DbSet<Account> Accounts { get; set; } = null!;

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

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("accounts");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
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

                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.ParentId);

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
        }
    }
}
