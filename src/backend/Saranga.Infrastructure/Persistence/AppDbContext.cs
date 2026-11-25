using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Interfaces;
using Saranga.Domain.Common;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUserService _currentUserService;

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "TokenHash",
        "Token",
        "Secret",
        "SecurityStamp",
        "ConnectionString"
    };

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Module 3: Attributes Master
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Purity> Purities => Set<Purity>();
    public DbSet<Specification> Specifications => Set<Specification>();
    public DbSet<SpecificationOption> SpecificationOptions => Set<SpecificationOption>();
    public DbSet<StockLocation> StockLocations => Set<StockLocation>();

    // Module 4: Inventory Core
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryItemSpecification> InventoryItemSpecifications => Set<InventoryItemSpecification>();
    public DbSet<InventoryItemMedia> InventoryItemMedia => Set<InventoryItemMedia>();

    // Module 5 Hardening: Idempotency
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    // Module 6: Assembly & Group Compositions
    public DbSet<InventoryComposition> InventoryCompositions => Set<InventoryComposition>();
    public DbSet<InventoryCompositionMember> InventoryCompositionMembers => Set<InventoryCompositionMember>();

    // Module 7: Excel / CSV Bulk Inventory Import
    public DbSet<InventoryImportBatch> InventoryImportBatches => Set<InventoryImportBatch>();

    // Module 8: Inventory Count & Physical Stock Verification
    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();
    public DbSet<InventoryCountLine> InventoryCountLines => Set<InventoryCountLine>();
    public DbSet<InventoryCountUnexpectedItem> InventoryCountUnexpectedItems => Set<InventoryCountUnexpectedItem>();

    // Module 9: Gold Rate Management & VAT Configuration
    public DbSet<MetalRate> MetalRates => Set<MetalRate>();
    public DbSet<VatConfiguration> VatConfigurations => Set<VatConfiguration>();

    // Module 10: Customer Management
    public DbSet<Customer> Customers => Set<Customer>();

    // Module 11: Shift Opening & Closing
    public DbSet<CashDrawer> CashDrawers => Set<CashDrawer>();
    public DbSet<Shift> Shifts => Set<Shift>();

    // Module 12: Cash Ledger & Cash Drawer Movements
    public DbSet<CashMovementCategory> CashMovementCategories => Set<CashMovementCategory>();
    public DbSet<CashLedgerTransaction> CashLedgerTransactions => Set<CashLedgerTransaction>();
    public DbSet<CashLedgerEntry> CashLedgerEntries => Set<CashLedgerEntry>();

    // Module 13: POS Normal Jewellery Sales
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global soft-delete query filter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(
                    ExpressionHelpers.CreateSoftDeleteFilter(entityType.ClrType)
                );
            }
        }

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();

            entity.Property(u => u.Username).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(255).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();

            entity.HasOne(u => u.DefaultBranch)
                  .WithMany(b => b.DefaultUsers)
                  .HasForeignKey(u => u.DefaultBranchId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(u => u.RefreshTokens)
                  .WithOne(r => r.User)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Branch Configuration
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("Branches");
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => b.Code).IsUnique();

            entity.Property(b => b.Name).HasMaxLength(150).IsRequired();
            entity.Property(b => b.Code).HasMaxLength(20).IsRequired();
            entity.Property(b => b.Country).HasMaxLength(100).IsRequired();
            entity.Property(b => b.CurrencyCode).HasMaxLength(10).IsRequired();
            entity.Property(b => b.TimeZone).HasMaxLength(100).IsRequired();
            entity.Property(b => b.IsActive).HasDefaultValue(true);
        });

        // Role Configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.Name).HasMaxLength(100).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(500);
        });

        // Permission Configuration
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Name).IsUnique();
            entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Module).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(500);
        });

        // UserRole (Junction)
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(ur => ur.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // RolePermission (Junction)
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            entity.HasOne(rp => rp.Role)
                  .WithMany(r => r.RolePermissions)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                  .WithMany(p => p.RolePermissions)
                  .HasForeignKey(rp => rp.PermissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // UserBranch (Junction for multi-branch access)
        modelBuilder.Entity<UserBranch>(entity =>
        {
            entity.ToTable("UserBranches");
            entity.HasKey(ub => new { ub.UserId, ub.BranchId });

            entity.HasOne(ub => ub.User)
                  .WithMany(u => u.UserBranches)
                  .HasForeignKey(ub => ub.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ub => ub.Branch)
                  .WithMany(b => b.UserBranches)
                  .HasForeignKey(ub => ub.BranchId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // RefreshToken Configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.TokenHash).IsUnique();
            entity.Property(r => r.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(r => r.ReplacedByTokenHash).HasMaxLength(128);
        });

        // AuditLog Configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.EntityName, a.EntityId });
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.BranchId);

            entity.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Action).HasMaxLength(50).IsRequired();
            entity.Property(a => a.Username).HasMaxLength(100);
            entity.Property(a => a.OldValues).HasColumnType("jsonb");
            entity.Property(a => a.NewValues).HasColumnType("jsonb");
            entity.Property(a => a.IpAddress).HasMaxLength(50);
            entity.Property(a => a.UserAgent).HasMaxLength(500);
        });

        // Module 3: Material Configuration
        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("Materials");
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.Code).IsUnique();
            entity.Property(m => m.Name).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Code).HasMaxLength(50).IsRequired();
            entity.Property(m => m.Description).HasMaxLength(500);
            entity.Property(m => m.IsActive).HasDefaultValue(true);
            entity.Property(m => m.SortOrder).HasDefaultValue(0);
        });

        // Module 3: Category Configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Code).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Code).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.Property(c => c.IsActive).HasDefaultValue(true);
            entity.Property(c => c.SortOrder).HasDefaultValue(0);
        });

        // Module 3: Purity Configuration
        modelBuilder.Entity<Purity>(entity =>
        {
            entity.ToTable("Purities");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.Name).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Code).HasMaxLength(20).IsRequired();
            entity.Property(p => p.KaratValue).HasPrecision(5, 2).IsRequired();
            entity.Property(p => p.PurityPercentage).HasPrecision(5, 2).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(500);
            entity.Property(p => p.IsActive).HasDefaultValue(true);
            entity.Property(p => p.SortOrder).HasDefaultValue(0);
        });

        // Module 3: Specification Configuration
        modelBuilder.Entity<Specification>(entity =>
        {
            entity.ToTable("Specifications");
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.Code).IsUnique();
            entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
            entity.Property(s => s.Code).HasMaxLength(50).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(500);
            entity.Property(s => s.InputType).HasMaxLength(30).IsRequired().HasDefaultValue("PREDEFINED");
            entity.Property(s => s.IsActive).HasDefaultValue(true);
            entity.Property(s => s.SortOrder).HasDefaultValue(0);

            entity.HasMany(s => s.Options)
                  .WithOne(o => o.Specification)
                  .HasForeignKey(o => o.SpecificationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Module 3: SpecificationOption Configuration
        modelBuilder.Entity<SpecificationOption>(entity =>
        {
            entity.ToTable("SpecificationOptions");
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => new { o.SpecificationId, o.Code }).IsUnique();
            entity.Property(o => o.Value).HasMaxLength(100).IsRequired();
            entity.Property(o => o.Code).HasMaxLength(50).IsRequired();
            entity.Property(o => o.Description).HasMaxLength(500);
            entity.Property(o => o.IsActive).HasDefaultValue(true);
            entity.Property(o => o.SortOrder).HasDefaultValue(0);
        });

        // Module 3: StockLocation Configuration (Branch-scoped)
        modelBuilder.Entity<StockLocation>(entity =>
        {
            entity.ToTable("StockLocations");
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.BranchId, l.Code }).IsUnique();
            entity.Property(l => l.Name).HasMaxLength(100).IsRequired();
            entity.Property(l => l.Code).HasMaxLength(50).IsRequired();
            entity.Property(l => l.LocationType).HasMaxLength(50).HasDefaultValue("Display").IsRequired();
            entity.Property(l => l.Description).HasMaxLength(500);
            entity.Property(l => l.IsActive).HasDefaultValue(true);
            entity.Property(l => l.SortOrder).HasDefaultValue(0);

            entity.HasOne(l => l.Branch)
                  .WithMany(b => b.StockLocations)
                  .HasForeignKey(l => l.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 4: Inventory Tag Sequence (Collision-safe atomic counter)
        modelBuilder.HasSequence<long>("InventoryTagSequence").StartsAt(100001).IncrementsBy(1);

        // Module 4: InventoryItem Configuration
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("InventoryItems");
            entity.HasKey(i => i.Id);

            // Indexes
            entity.HasIndex(i => i.TagNumber).IsUnique();
            entity.HasIndex(i => i.Barcode).IsUnique();
            entity.HasIndex(i => new { i.BranchId, i.Status });
            entity.HasIndex(i => new { i.BranchId, i.StockLocationId });
            entity.HasIndex(i => new { i.CategoryId, i.MaterialId, i.PurityId });

            // Optimistic concurrency token (prevents concurrent double-sale in POS)
            entity.Property(i => i.Version).IsRowVersion();

            entity.Property(i => i.Name).HasMaxLength(200).IsRequired();
            entity.Property(i => i.ItemCode).HasMaxLength(50).IsRequired();
            entity.Property(i => i.TagNumber).HasMaxLength(50).IsRequired();
            entity.Property(i => i.Barcode).HasMaxLength(50).IsRequired();

            entity.Property(i => i.GrossWeight).HasPrecision(18, 4).IsRequired();
            entity.Property(i => i.StoneWeight).HasPrecision(18, 4).IsRequired();
            entity.Property(i => i.NetWeight).HasPrecision(18, 4).IsRequired();

            entity.Property(i => i.PieceCount).HasDefaultValue(1).IsRequired();
            entity.Property(i => i.MakingChargeMethod).HasConversion<int>().IsRequired();
            entity.Property(i => i.MakingChargeValue).HasPrecision(18, 2).HasDefaultValue(0.00m).IsRequired();
            entity.Property(i => i.Status).HasConversion<int>().HasDefaultValue(InventoryStatus.Available).IsRequired();
            entity.Property(i => i.Description).HasMaxLength(1000);
            entity.Property(i => i.IsActive).HasDefaultValue(true);

            // Foreign Keys with Restrict to protect historical integrity
            entity.HasOne(i => i.Branch)
                  .WithMany()
                  .HasForeignKey(i => i.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.StockLocation)
                  .WithMany()
                  .HasForeignKey(i => i.StockLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Material)
                  .WithMany()
                  .HasForeignKey(i => i.MaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Category)
                  .WithMany()
                  .HasForeignKey(i => i.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Purity)
                  .WithMany()
                  .HasForeignKey(i => i.PurityId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(i => i.Specifications)
                  .WithOne(s => s.InventoryItem)
                  .HasForeignKey(s => s.InventoryItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(i => i.Media)
                  .WithOne(m => m.InventoryItem)
                  .HasForeignKey(m => m.InventoryItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Module 4: InventoryItemSpecification Configuration
        modelBuilder.Entity<InventoryItemSpecification>(entity =>
        {
            entity.ToTable("InventoryItemSpecifications");
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.InventoryItemId, s.SpecificationId }).IsUnique();

            entity.Property(s => s.CustomValue).HasMaxLength(250);

            entity.HasOne(s => s.Specification)
                  .WithMany()
                  .HasForeignKey(s => s.SpecificationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.SpecificationOption)
                  .WithMany()
                  .HasForeignKey(s => s.SpecificationOptionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 4: InventoryItemMedia Configuration
        modelBuilder.Entity<InventoryItemMedia>(entity =>
        {
            entity.ToTable("InventoryItemMedia");
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.InventoryItemId, m.SortOrder });

            entity.Property(m => m.FileName).HasMaxLength(255).IsRequired();
            entity.Property(m => m.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(m => m.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(m => m.MediaType).HasConversion<int>().IsRequired();
            entity.Property(m => m.IsPrimary).HasDefaultValue(false);
            entity.Property(m => m.SortOrder).HasDefaultValue(0);
        });

        // Module 5 Hardening: IdempotencyRecord Configuration
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => new { i.UserId, i.Key }).IsUnique();
            entity.HasIndex(i => i.ExpiresAt);

            entity.Property(i => i.Key).HasMaxLength(100).IsRequired();
            entity.Property(i => i.Endpoint).HasMaxLength(150).IsRequired();
            entity.Property(i => i.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(i => i.ResponseBody).IsRequired();
        });

        // Module 6: InventoryComposition Configuration
        modelBuilder.Entity<InventoryComposition>(entity =>
        {
            entity.ToTable("InventoryCompositions");
            entity.HasKey(c => c.Id);

            entity.HasIndex(c => c.TagNumber).IsUnique();
            entity.HasIndex(c => c.Barcode).IsUnique();
            entity.HasIndex(c => new { c.BranchId, c.Status });
            entity.HasIndex(c => new { c.BranchId, c.StockLocationId });

            entity.Property(c => c.Version).IsRowVersion();

            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.TagNumber).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Barcode).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(1000);
            entity.Property(c => c.Type).HasConversion<int>().IsRequired();
            entity.Property(c => c.Status).HasConversion<int>().HasDefaultValue(CompositionStatus.Active).IsRequired();
            entity.Property(c => c.IsActive).HasDefaultValue(true);

            entity.HasOne(c => c.Branch)
                  .WithMany()
                  .HasForeignKey(c => c.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.StockLocation)
                  .WithMany()
                  .HasForeignKey(c => c.StockLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(c => c.Members)
                  .WithOne(m => m.Composition)
                  .HasForeignKey(m => m.CompositionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Module 6: InventoryCompositionMember Configuration
        modelBuilder.Entity<InventoryCompositionMember>(entity =>
        {
            entity.ToTable("InventoryCompositionMembers");
            entity.HasKey(m => m.Id);

            entity.HasIndex(m => new { m.CompositionId, m.IsActive });

            // CRITICAL HARDENING: Database-enforced One Child -> Maximum One Active Composition rule
            entity.HasIndex(m => m.InventoryItemId)
                  .IsUnique()
                  .HasFilter(@"""IsActive"" = true");

            entity.Property(m => m.IsActive).HasDefaultValue(true);
            entity.Property(m => m.SortOrder).HasDefaultValue(0);

            entity.HasOne(m => m.InventoryItem)
                  .WithMany()
                  .HasForeignKey(m => m.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 7: InventoryImportBatch Configuration
        modelBuilder.Entity<InventoryImportBatch>(entity =>
        {
            entity.ToTable("InventoryImportBatches");
            entity.HasKey(b => b.Id);

            entity.HasIndex(b => new { b.BranchId, b.Status });
            entity.HasIndex(b => b.IdempotencyKey);
            entity.HasIndex(b => b.CreatedAt);

            entity.Property(b => b.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(b => b.FileType).HasMaxLength(10).IsRequired();
            entity.Property(b => b.Status).HasConversion<int>().IsRequired();
            entity.Property(b => b.IdempotencyKey).HasMaxLength(100);

            entity.Property(b => b.PayloadJson).HasColumnType("jsonb");
            entity.Property(b => b.ValidationErrorsJson).HasColumnType("jsonb");
            entity.Property(b => b.ResultSummaryJson).HasColumnType("jsonb");

            entity.HasOne(b => b.Branch)
                  .WithMany()
                  .HasForeignKey(b => b.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 8: InventoryCount Configuration
        modelBuilder.Entity<InventoryCount>(entity =>
        {
            entity.ToTable("InventoryCounts");
            entity.HasKey(c => c.Id);

            // Filtered partial unique index: Exactly ONE active count per BranchId + StockLocationId (Status 1=InProgress, 2=RequiresReview)
            entity.HasIndex(c => new { c.BranchId, c.StockLocationId })
                  .HasFilter("\"Status\" IN (1, 2)")
                  .IsUnique();

            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.StartedAt);

            entity.Property(c => c.Status).HasConversion<int>().IsRequired();
            entity.Property(c => c.Notes).HasMaxLength(1000);
            entity.Property(c => c.CancellationReason).HasMaxLength(500);

            // PostgreSQL MVCC row-versioning for optimistic concurrency
            entity.Property(c => c.Version).IsRowVersion();

            entity.HasOne(c => c.Branch)
                  .WithMany()
                  .HasForeignKey(c => c.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.StockLocation)
                  .WithMany()
                  .HasForeignKey(c => c.StockLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(c => c.Lines)
                  .WithOne(l => l.InventoryCount)
                  .HasForeignKey(l => l.InventoryCountId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.UnexpectedItems)
                  .WithOne(u => u.InventoryCount)
                  .HasForeignKey(u => u.InventoryCountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Module 8: InventoryCountLine Configuration
        modelBuilder.Entity<InventoryCountLine>(entity =>
        {
            entity.ToTable("InventoryCountLines");
            entity.HasKey(l => l.Id);

            // Unique index: An item cannot appear multiple times in the same count's baseline
            entity.HasIndex(l => new { l.InventoryCountId, l.InventoryItemId })
                  .IsUnique();

            entity.HasIndex(l => new { l.InventoryCountId, l.IsCounted });
            entity.HasIndex(l => new { l.InventoryCountId, l.TagNumberSnapshot });
            entity.HasIndex(l => new { l.InventoryCountId, l.BarcodeSnapshot });

            entity.Property(l => l.TagNumberSnapshot).HasMaxLength(50).IsRequired();
            entity.Property(l => l.BarcodeSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(l => l.ItemNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(l => l.MaterialNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(l => l.CategoryNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(l => l.PurityNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(l => l.ExpectedStatusSnapshot).HasMaxLength(50).IsRequired();

            entity.Property(l => l.GrossWeightSnapshot).HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(l => l.StoneWeightSnapshot).HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(l => l.NetWeightSnapshot).HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(l => l.PieceCountSnapshot).IsRequired();

            entity.Property(l => l.Result).HasConversion<int>().IsRequired();
            entity.Property(l => l.ResolutionStatus).HasConversion<int>().IsRequired();
            entity.Property(l => l.ResolutionReasonCode).HasMaxLength(50);
            entity.Property(l => l.ResolutionNotes).HasMaxLength(1000);

            entity.HasOne(l => l.InventoryItem)
                  .WithMany()
                  .HasForeignKey(l => l.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.ExpectedBranch)
                  .WithMany()
                  .HasForeignKey(l => l.ExpectedBranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.ExpectedStockLocation)
                  .WithMany()
                  .HasForeignKey(l => l.ExpectedStockLocationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 8: InventoryCountUnexpectedItem Configuration
        modelBuilder.Entity<InventoryCountUnexpectedItem>(entity =>
        {
            entity.ToTable("InventoryCountUnexpectedItems");
            entity.HasKey(u => u.Id);

            // Unique index: An item cannot be recorded multiple times as unexpected in the same count
            entity.HasIndex(u => new { u.InventoryCountId, u.InventoryItemId })
                  .IsUnique();

            entity.HasIndex(u => new { u.InventoryCountId, u.TagNumberSnapshot });
            entity.HasIndex(u => new { u.InventoryCountId, u.BarcodeSnapshot });

            entity.Property(u => u.TagNumberSnapshot).HasMaxLength(50).IsRequired();
            entity.Property(u => u.BarcodeSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(u => u.ItemNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(u => u.MaterialNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(u => u.CategoryNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(u => u.PurityNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(u => u.CurrentSystemLocationNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(u => u.CurrentSystemStatusSnapshot).HasMaxLength(50).IsRequired();

            entity.Property(u => u.GrossWeightSnapshot).HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(u => u.StoneWeightSnapshot).HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(u => u.NetWeightSnapshot).HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(u => u.PieceCountSnapshot).IsRequired();

            entity.Property(u => u.ResolutionStatus).HasConversion<int>().IsRequired();
            entity.Property(u => u.ResolutionReasonCode).HasMaxLength(50);
            entity.Property(u => u.ResolutionNotes).HasMaxLength(1000);

            entity.HasOne(u => u.InventoryItem)
                  .WithMany()
                  .HasForeignKey(u => u.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.ObservedBranch)
                  .WithMany()
                  .HasForeignKey(u => u.ObservedBranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.ObservedStockLocation)
                  .WithMany()
                  .HasForeignKey(u => u.ObservedStockLocationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.CurrentSystemStockLocation)
                  .WithMany()
                  .HasForeignKey(u => u.CurrentSystemStockLocationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 9: MetalRate Configuration
        modelBuilder.Entity<MetalRate>(entity =>
        {
            entity.ToTable("MetalRates");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.CurrencyCode).HasMaxLength(10).IsRequired();
            entity.Property(r => r.RatePerGram).HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(r => r.Source).HasMaxLength(50).IsRequired();
            entity.Property(r => r.ExternalReferenceRate).HasColumnType("numeric(18,4)");
            entity.Property(r => r.OverrideReason).HasMaxLength(500);

            entity.HasIndex(r => new { r.BranchId, r.MaterialId, r.PurityId, r.CurrencyCode })
                  .HasFilter("\"IsActive\" = true")
                  .IsUnique();

            entity.HasIndex(r => r.EffectiveFrom);
            entity.HasIndex(r => r.CreatedAt);

            entity.HasOne(r => r.Branch)
                  .WithMany()
                  .HasForeignKey(r => r.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Material)
                  .WithMany()
                  .HasForeignKey(r => r.MaterialId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Purity)
                  .WithMany()
                  .HasForeignKey(r => r.PurityId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Creator)
                  .WithMany()
                  .HasForeignKey(r => r.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 9: VatConfiguration Configuration
        modelBuilder.Entity<VatConfiguration>(entity =>
        {
            entity.ToTable("VatConfigurations");
            entity.HasKey(v => v.Id);

            entity.Property(v => v.Name).HasMaxLength(100).IsRequired();
            entity.Property(v => v.RatePercentage).HasColumnType("numeric(5,2)").IsRequired();
            entity.Property(v => v.CalculationMode).HasMaxLength(50).IsRequired();

            entity.HasIndex(v => v.BranchId)
                  .HasFilter("\"IsActive\" = true")
                  .IsUnique();

            entity.HasIndex(v => v.EffectiveFrom);

            entity.HasOne(v => v.Branch)
                  .WithMany()
                  .HasForeignKey(v => v.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Creator)
                  .WithMany()
                  .HasForeignKey(v => v.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Updater)
                  .WithMany()
                  .HasForeignKey(v => v.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 10: Customer Management Configuration
        modelBuilder.HasSequence<long>("CustomerCodeSequence").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.CustomerCode).HasMaxLength(50).IsRequired();
            entity.HasIndex(c => c.CustomerCode).IsUnique();

            entity.Property(c => c.FullName).HasMaxLength(200).IsRequired();
            entity.HasIndex(c => c.FullName);

            entity.Property(c => c.PrimaryPhone).HasMaxLength(50).IsRequired();
            entity.Property(c => c.NormalizedPrimaryPhone).HasMaxLength(50).IsRequired();
            entity.HasIndex(c => c.NormalizedPrimaryPhone).IsUnique();

            entity.Property(c => c.SecondaryPhone).HasMaxLength(50);
            entity.Property(c => c.NormalizedSecondaryPhone).HasMaxLength(50);
            entity.HasIndex(c => c.NormalizedSecondaryPhone);

            entity.Property(c => c.Email).HasMaxLength(200);
            entity.Property(c => c.NormalizedEmail).HasMaxLength(200);
            entity.HasIndex(c => c.NormalizedEmail).IsUnique().HasFilter("\"NormalizedEmail\" IS NOT NULL");

            entity.Property(c => c.AddressLine1).HasMaxLength(200);
            entity.Property(c => c.AddressLine2).HasMaxLength(200);
            entity.Property(c => c.City).HasMaxLength(100);
            entity.Property(c => c.DistrictProvince).HasMaxLength(100);
            entity.Property(c => c.PostalCode).HasMaxLength(20);
            entity.Property(c => c.Country).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Notes).HasMaxLength(2000);

            entity.Property(c => c.Version).IsRowVersion();

            entity.HasIndex(c => c.IsActive);
            entity.HasIndex(c => c.CreatedAt);

            entity.HasOne(c => c.Creator)
                  .WithMany()
                  .HasForeignKey(c => c.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Updater)
                  .WithMany()
                  .HasForeignKey(c => c.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 11: CashDrawer Configuration
        modelBuilder.Entity<CashDrawer>(entity =>
        {
            entity.ToTable("CashDrawers");
            entity.HasKey(d => d.Id);

            entity.Property(d => d.DrawerCode).HasMaxLength(50).IsRequired();
            entity.Property(d => d.Name).HasMaxLength(150).IsRequired();
            entity.Property(d => d.Description).HasMaxLength(500);

            entity.HasIndex(d => new { d.BranchId, d.DrawerCode }).IsUnique();
            entity.HasIndex(d => d.IsActive);

            entity.HasOne(d => d.Branch)
                  .WithMany()
                  .HasForeignKey(d => d.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 11: Shift Configuration
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.ToTable("Shifts");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.ShiftNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(s => s.ShiftNumber).IsUnique();

            entity.Property(s => s.CurrencyCode).HasMaxLength(10).IsRequired().HasDefaultValue("LKR");

            entity.Property(s => s.Status).HasConversion<int>().IsRequired();

            entity.Property(s => s.OpeningBalance).HasPrecision(18, 2);
            entity.Property(s => s.SystemExpectedOpeningBalance).HasPrecision(18, 2);
            entity.Property(s => s.OpeningBalanceDifference).HasPrecision(18, 2);
            entity.Property(s => s.OpeningDifferenceReason).HasMaxLength(500);
            entity.Property(s => s.OpeningNotes).HasMaxLength(1000);

            entity.Property(s => s.ActualClosingBalance).HasPrecision(18, 2);
            entity.Property(s => s.ExpectedClosingBalance).HasPrecision(18, 2);
            entity.Property(s => s.ClosingVariance).HasPrecision(18, 2);
            entity.Property(s => s.ClosingVarianceReason).HasMaxLength(500);
            entity.Property(s => s.ClosingNotes).HasMaxLength(1000);

            entity.Property(s => s.IsOverrideClose).HasDefaultValue(false);
            entity.Property(s => s.OverrideReason).HasMaxLength(500);

            entity.Property(s => s.Version).IsRowVersion();

            // PostgreSQL level uniqueness: One active shift per cash drawer
            entity.HasIndex(s => s.CashDrawerId)
                  .IsUnique()
                  .HasFilter("\"Status\" = 0 AND \"IsDeleted\" = false");

            // PostgreSQL level uniqueness: One active shift per cashier for MVP
            entity.HasIndex(s => s.CashierId)
                  .IsUnique()
                  .HasFilter("\"Status\" = 0 AND \"IsDeleted\" = false");

            entity.HasIndex(s => s.BranchId);
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => s.OpenedAt);

            entity.HasOne(s => s.Branch)
                  .WithMany()
                  .HasForeignKey(s => s.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.CashDrawer)
                  .WithMany()
                  .HasForeignKey(s => s.CashDrawerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Cashier)
                  .WithMany()
                  .HasForeignKey(s => s.CashierId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Opener)
                  .WithMany()
                  .HasForeignKey(s => s.OpenedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Closer)
                  .WithMany()
                  .HasForeignKey(s => s.ClosedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 12: Cash Ledger Configuration
        modelBuilder.HasSequence<long>("CashLedgerSequence").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<CashMovementCategory>(entity =>
        {
            entity.ToTable("CashMovementCategories");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(c => c.Code).IsUnique();

            entity.Property(c => c.AllowedDirection).HasConversion<int>().IsRequired();
            entity.Property(c => c.Description).HasMaxLength(250);
            entity.Property(c => c.IsActive).HasDefaultValue(true);
            entity.Property(c => c.IsSystem).HasDefaultValue(false);

            entity.HasIndex(c => c.IsActive);

            entity.HasOne(c => c.Creator)
                  .WithMany()
                  .HasForeignKey(c => c.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Updater)
                  .WithMany()
                  .HasForeignKey(c => c.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashLedgerTransaction>(entity =>
        {
            entity.ToTable("CashLedgerTransactions", t =>
            {
                t.HasCheckConstraint("CK_CashLedgerTransactions_Amount", "\"Amount\" > 0");
                t.HasCheckConstraint("CK_CashLedgerTransactions_Type", "\"TransactionType\" IN (1, 2, 3, 4)");
                t.HasCheckConstraint("CK_CashLedgerTransactions_Status", "\"Status\" IN (1, 2)");
            });

            entity.HasKey(t => t.Id);

            entity.Property(t => t.TransactionReference).HasMaxLength(50).IsRequired();
            entity.HasIndex(t => t.TransactionReference).IsUnique();

            entity.Property(t => t.TransactionType).HasConversion<int>().IsRequired();
            entity.Property(t => t.SourceType).HasConversion<int>().IsRequired();
            entity.Property(t => t.SourceReferenceId).HasMaxLength(100);

            entity.Property(t => t.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(t => t.CurrencyCode).HasMaxLength(10).IsRequired().HasDefaultValue("LKR");
            entity.Property(t => t.Description).HasMaxLength(500);
            entity.Property(t => t.Reason).HasMaxLength(500);
            entity.Property(t => t.Status).HasConversion<int>().IsRequired();
            entity.Property(t => t.IdempotencyKey).HasMaxLength(100);

            // Reversal uniqueness: A transaction can be reversed at most once
            entity.HasIndex(t => t.ReversalOfTransactionId)
                  .IsUnique()
                  .HasFilter("\"ReversalOfTransactionId\" IS NOT NULL");

            // Source reference uniqueness for future POS / External modules
            entity.HasIndex(t => new { t.SourceType, t.SourceReferenceId })
                  .IsUnique()
                  .HasFilter("\"SourceReferenceId\" IS NOT NULL");

            entity.HasIndex(t => new { t.BranchId, t.TransactionAt });
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.CreatedAt);

            entity.HasOne(t => t.Branch)
                  .WithMany()
                  .HasForeignKey(t => t.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Category)
                  .WithMany(c => c.Transactions)
                  .HasForeignKey(t => t.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ReversalOfTransaction)
                  .WithMany(t => t.ReversedByTransactions)
                  .HasForeignKey(t => t.ReversalOfTransactionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Creator)
                  .WithMany()
                  .HasForeignKey(t => t.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Updater)
                  .WithMany()
                  .HasForeignKey(t => t.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashLedgerEntry>(entity =>
        {
            entity.ToTable("CashLedgerEntries", e =>
            {
                e.HasCheckConstraint("CK_CashLedgerEntries_Amount", "\"Amount\" > 0");
                e.HasCheckConstraint("CK_CashLedgerEntries_Direction", "\"Direction\" IN (1, 2)");
            });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Direction).HasConversion<int>().IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.CurrencyCode).HasMaxLength(10).IsRequired().HasDefaultValue("LKR");

            entity.HasIndex(e => e.ShiftId);
            entity.HasIndex(e => e.CashDrawerId);
            entity.HasIndex(e => new { e.BranchId, e.CreatedAt });

            entity.HasOne(e => e.CashLedgerTransaction)
                  .WithMany(t => t.Entries)
                  .HasForeignKey(e => e.CashLedgerTransactionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Branch)
                  .WithMany()
                  .HasForeignKey(e => e.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CashDrawer)
                  .WithMany()
                  .HasForeignKey(e => e.CashDrawerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Shift)
                  .WithMany()
                  .HasForeignKey(e => e.ShiftId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Module 13: POS Normal Jewellery Sales Configuration
        modelBuilder.HasSequence<long>("InvoiceSequence").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices");
            entity.HasKey(i => i.Id);

            entity.Property(i => i.InvoiceNumber).HasMaxLength(60).IsRequired();
            entity.HasIndex(i => i.InvoiceNumber).IsUnique();

            entity.Property(i => i.CustomerNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(i => i.CustomerPhoneSnapshot).HasMaxLength(50).IsRequired();
            entity.Property(i => i.CurrencyCode).HasMaxLength(10).IsRequired().HasDefaultValue("LKR");

            entity.Property(i => i.SubTotal).HasPrecision(18, 2);
            entity.Property(i => i.DiscountAmount).HasPrecision(18, 2);
            entity.Property(i => i.VatRate).HasPrecision(18, 2);
            entity.Property(i => i.VatAmount).HasPrecision(18, 2);
            entity.Property(i => i.TotalAmount).HasPrecision(18, 2);
            entity.Property(i => i.TotalPaidAmount).HasPrecision(18, 2);
            entity.Property(i => i.TotalCashTendered).HasPrecision(18, 2);
            entity.Property(i => i.TotalCashChange).HasPrecision(18, 2);
            entity.Property(i => i.NetCashPaid).HasPrecision(18, 2);

            entity.Property(i => i.Status).HasConversion<int>().IsRequired();
            entity.Property(i => i.IdempotencyKey).HasMaxLength(100);

            entity.HasIndex(i => i.BranchId);
            entity.HasIndex(i => i.ShiftId);
            entity.HasIndex(i => i.CustomerId);
            entity.HasIndex(i => i.Status);
            entity.HasIndex(i => i.CreatedAt);
            entity.HasIndex(i => new { i.BranchId, i.CreatedAt });

            entity.HasOne(i => i.Branch)
                  .WithMany()
                  .HasForeignKey(i => i.BranchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Shift)
                  .WithMany()
                  .HasForeignKey(i => i.ShiftId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Cashier)
                  .WithMany()
                  .HasForeignKey(i => i.CashierId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Customer)
                  .WithMany()
                  .HasForeignKey(i => i.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(i => i.Lines)
                  .WithOne(l => l.Invoice)
                  .HasForeignKey(l => l.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(i => i.Payments)
                  .WithOne(p => p.Invoice)
                  .HasForeignKey(p => p.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("InvoiceLines");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.LineType).HasConversion<int>().IsRequired();
            entity.Property(l => l.ItemCode).HasMaxLength(60).IsRequired();
            entity.Property(l => l.TagNumber).HasMaxLength(60).IsRequired();
            entity.Property(l => l.Barcode).HasMaxLength(100);
            entity.Property(l => l.ItemName).HasMaxLength(200).IsRequired();

            entity.Property(l => l.GrossWeight).HasPrecision(18, 4);
            entity.Property(l => l.StoneWeight).HasPrecision(18, 4);
            entity.Property(l => l.NetWeight).HasPrecision(18, 4);

            entity.Property(l => l.ReferenceRatePerGram).HasPrecision(18, 2);
            entity.Property(l => l.ReferenceMaterialValue).HasPrecision(18, 2);
            entity.Property(l => l.ReferenceMakingCharge).HasPrecision(18, 2);
            entity.Property(l => l.ReferencePrice).HasPrecision(18, 2);
            entity.Property(l => l.UnitPrice).HasPrecision(18, 2);
            entity.Property(l => l.LineTotal).HasPrecision(18, 2);

            entity.HasOne(l => l.InventoryItem)
                  .WithMany()
                  .HasForeignKey(l => l.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Composition)
                  .WithMany()
                  .HasForeignKey(l => l.CompositionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoicePayment>(entity =>
        {
            entity.ToTable("InvoicePayments");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.PaymentMethod).HasConversion<int>().IsRequired();
            entity.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(p => p.CashTendered).HasPrecision(18, 2);
            entity.Property(p => p.ChangeGiven).HasPrecision(18, 2);

            entity.Property(p => p.ReferenceNumber).HasMaxLength(100);
            entity.Property(p => p.CardType).HasMaxLength(50);
            entity.Property(p => p.CardLast4).HasMaxLength(10);
            entity.Property(p => p.BankName).HasMaxLength(100);

            entity.HasOne(p => p.CashLedgerTransaction)
                  .WithMany()
                  .HasForeignKey(p => p.CashLedgerTransactionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Audit safety check: ensure historical audit logs can NEVER be updated or deleted
        foreach (var entry in ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit logs are append-only immutable records and cannot be modified or deleted.");
            }
        }

        // Financial immutability check: Cash ledger entries can NEVER be modified or deleted
        foreach (var entry in ChangeTracker.Entries<CashLedgerEntry>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Cash ledger entries are immutable financial records and cannot be modified or deleted. Use reversals instead.");
            }
        }

        // Financial immutability check: Cash ledger transactions can NEVER be deleted
        foreach (var entry in ChangeTracker.Entries<CashLedgerTransaction>())
        {
            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Cash ledger transactions are immutable financial records and cannot be deleted. Use reversals instead.");
            }
            if (entry.State == EntityState.Modified)
            {
                var propAmount = entry.Property(t => t.Amount);
                var propType = entry.Property(t => t.TransactionType);
                var propRef = entry.Property(t => t.TransactionReference);
                var propBranch = entry.Property(t => t.BranchId);
                var propReversal = entry.Property(t => t.ReversalOfTransactionId);

                if (propAmount.IsModified || propType.IsModified || propRef.IsModified || propBranch.IsModified || (propReversal.IsModified && propReversal.OriginalValue != null))
                {
                    throw new InvalidOperationException("Core financial attributes of a posted cash ledger transaction cannot be modified. Use reversals instead.");
                }
            }
        }

        var currentUserId = _currentUserService.UserId;
        var now = DateTimeOffset.UtcNow;

        var auditEntries = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries())
        {
            // Do not audit AuditLog itself
            if (entry.Entity is AuditLog || entry.Entity is RefreshToken)
            {
                continue;
            }

            if (entry.Entity is BaseEntity baseEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        baseEntity.CreatedAt = now;
                        baseEntity.CreatedBy ??= currentUserId;
                        baseEntity.IsDeleted = false;
                        auditEntries.Add(CreateAuditRecord(entry, "CREATE", currentUserId, now));
                        break;

                    case EntityState.Modified:
                        baseEntity.UpdatedAt = now;
                        baseEntity.UpdatedBy = currentUserId;
                        auditEntries.Add(CreateAuditRecord(entry, "UPDATE", currentUserId, now));
                        break;

                    case EntityState.Deleted:
                        // Convert physical delete to soft delete
                        entry.State = EntityState.Modified;
                        baseEntity.IsDeleted = true;
                        baseEntity.UpdatedAt = now;
                        baseEntity.UpdatedBy = currentUserId;
                        auditEntries.Add(CreateAuditRecord(entry, "DELETE", currentUserId, now));
                        break;
                }
            }
        }

        if (auditEntries.Count > 0)
        {
            AuditLogs.AddRange(auditEntries);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private AuditLog CreateAuditRecord(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string action, Guid? currentUserId, DateTimeOffset now)
    {
        var entityName = entry.Entity.GetType().Name;
        var entityId = entry.Property("Id").CurrentValue?.ToString() ?? string.Empty;

        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            // Omit sensitive properties from audit log
            if (SensitivePropertyNames.Contains(prop.Metadata.Name))
            {
                continue;
            }

            if (action == "CREATE")
            {
                newValues[prop.Metadata.Name] = prop.CurrentValue;
            }
            else if (action == "DELETE")
            {
                oldValues[prop.Metadata.Name] = prop.OriginalValue;
            }
            else if (action == "UPDATE" && prop.IsModified)
            {
                oldValues[prop.Metadata.Name] = prop.OriginalValue;
                newValues[prop.Metadata.Name] = prop.CurrentValue;
            }
        }

        return new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            UserId = currentUserId,
            Username = _currentUserService.Username,
            BranchId = _currentUserService.BranchId,
            Timestamp = now,
            OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent
        };
    }
}

internal static class ExpressionHelpers
{
    public static System.Linq.Expressions.LambdaExpression CreateSoftDeleteFilter(Type entityType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
        var comparison = System.Linq.Expressions.Expression.Equal(property, System.Linq.Expressions.Expression.Constant(false));
        return System.Linq.Expressions.Expression.Lambda(comparison, parameter);
    }
}
