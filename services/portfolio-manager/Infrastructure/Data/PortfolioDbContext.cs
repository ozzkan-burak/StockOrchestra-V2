namespace PortfolioManager.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using PortfolioManager.Domain.Entities;

using LedgerSide = PortfolioManager.Domain.Entities.LedgerSide;
using LedgerTransactionType = PortfolioManager.Domain.Entities.LedgerTransactionType;
using LedgerStatus = PortfolioManager.Domain.Entities.LedgerStatus;

public class PortfolioDbContext : DbContext
{
    public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : base(options)
    {
    }
    
    public DbSet<User> Users => Set<User>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetLedger> AssetLedgers => Set<AssetLedger>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.HasDefaultSchema("public");
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.Property(e => e.Username)
                .HasColumnName("username")
                .HasColumnType("varchar(50)")
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.Email)
                .HasColumnName("email")
                .HasColumnType("varchar(255)")
                .IsRequired()
                .HasMaxLength(255);
            
            entity.Property(e => e.EmailVerified)
                .HasColumnName("email_verified")
                .HasColumnType("boolean");
            
            entity.Property(e => e.PasswordHash)
                .HasColumnName("password_hash")
                .HasColumnType("varchar(255)")
                .IsRequired();
            
            entity.Property(e => e.FullName)
                .HasColumnName("full_name")
                .HasColumnType("varchar(255)");
            
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("varchar(20)")
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("active");
            
            entity.Property(e => e.LastLoginAt)
                .HasColumnName("last_login_at")
                .HasColumnType("timestamptz");
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });
        
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("assets");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.Property(e => e.Symbol)
                .HasColumnName("symbol")
                .HasColumnType("varchar(20)")
                .IsRequired()
                .HasMaxLength(20);
            
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasColumnType("varchar(255)")
                .IsRequired();
            
            entity.Property(e => e.AssetType)
                .HasColumnName("asset_type")
                .HasColumnType("varchar(20)")
                .IsRequired();
            
            entity.Property(e => e.QuoteCurrency)
                .HasColumnName("quote_currency")
                .HasColumnType("varchar(10)")
                .HasDefaultValue("USD");
            
            entity.Property(e => e.MinQuantity)
                .HasColumnName("min_quantity")
                .HasColumnType("decimal(36,18)")
                .HasPrecision(36, 18);
            
            entity.Property(e => e.MinNotional)
                .HasColumnName("min_notional")
                .HasColumnType("decimal(36,18)")
                .HasPrecision(36, 18);
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasColumnType("boolean");
            
            entity.Property(e => e.DecimalPlaces)
                .HasColumnName("decimal_places")
                .HasColumnType("int");
            
            entity.Property(e => e.CommissionRate)
                .HasColumnName("commission_rate")
                .HasColumnType("decimal(10,8)")
                .HasPrecision(10, 8);
            
            entity.Property(e => e.LastTradedAt)
                .HasColumnName("last_traded_at")
                .HasColumnType("timestamptz");
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.HasIndex(e => e.Symbol).IsUnique();
        });
        
        modelBuilder.Entity<AssetLedger>(entity =>
        {
            entity.ToTable("asset_ledger");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .HasColumnType("uuid")
                .IsRequired();
            
            entity.Property(e => e.AssetId)
                .HasColumnName("asset_id")
                .HasColumnType("uuid");
            
            entity.Property(e => e.TransactionType)
                .HasColumnName("transaction_type")
                .HasColumnType("varchar(20)")
                .IsRequired()
                .HasConversion<string>();
            
            entity.Property(e => e.Side)
                .HasColumnName("side")
                .HasColumnType("varchar(6)")
                .IsRequired()
                .HasConversion<string>();
            
            entity.Property(e => e.Quantity)
                .HasColumnName("quantity")
                .HasColumnType("decimal(36,18)")
                .HasPrecision(36, 18)
                .IsRequired();
            
            entity.Property(e => e.PriceAtTime)
                .HasColumnName("price_at_time")
                .HasColumnType("decimal(36,18)")
                .HasPrecision(36, 18);
            
            entity.Property(e => e.TotalNotional)
                .HasColumnName("total_notional")
                .HasColumnType("decimal(36,18)")
                .HasPrecision(36, 18);
            
            entity.Property(e => e.QuoteBalanceAfter)
                .HasColumnName("quote_balance_after")
                .HasColumnType("decimal(36,18)")
                .HasPrecision(36, 18);
            
            entity.Property(e => e.AssetBalanceAfter)
                .HasColumnName("asset_balance_after")
                .HasColumnType("decimal(36,18)")
                .HasPrecision(36, 18);
            
            entity.Property(e => e.CommissionAmount)
                .HasColumnName("commission_amount")
                .HasColumnType("decimal(36,18)")
                .HasPrecision(36, 18)
                .HasDefaultValue(0);
            
            entity.Property(e => e.CommissionPaid)
                .HasColumnName("commission_paid")
                .HasColumnType("boolean")
                .HasDefaultValue(true);
            
            entity.Property(e => e.ExternalRef)
                .HasColumnName("external_ref")
                .HasColumnType("varchar(255)");
            
            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("varchar(20)")
                .IsRequired()
                .HasConversion<string>()
                .HasDefaultValue("completed");
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at")
                .HasColumnType("timestamptz");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            
            entity.HasIndex(e => new { e.UserId, e.AssetId, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Asset)
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}