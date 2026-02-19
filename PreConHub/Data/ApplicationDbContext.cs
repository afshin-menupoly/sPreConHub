using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PreConHub.Models.Entities;
using System.Reflection.Emit;

namespace PreConHub.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Main entities
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectFee> ProjectFees { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<UnitFee> UnitFees { get; set; }
        public DbSet<UnitPurchaser> UnitPurchasers { get; set; }
        public DbSet<MortgageInfo> MortgageInfos { get; set; }
        public DbSet<PurchaserFinancials> PurchaserFinancials { get; set; }
        public DbSet<Deposit> Deposits { get; set; }
        public DbSet<OccupancyFee> OccupancyFees { get; set; }
        public DbSet<StatementOfAdjustments> StatementsOfAdjustments { get; set; }
        public DbSet<ShortfallAnalysis> ShortfallAnalyses { get; set; }
        public DbSet<LawyerAssignment> LawyerAssignments { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ProjectSummary> ProjectSummaries { get; set; }
        public DbSet<LawyerNote> LawyerNotes { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ProjectFinancials> ProjectFinancials { get; set; }
        public DbSet<ClosingExtensionRequest> ClosingExtensionRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ============================================
            // ApplicationUser Configuration
            // ============================================
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CompanyName).HasMaxLength(100);
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.UserType);
            });

            // ============================================
            // Project Configuration
            // ============================================
            builder.Entity<Project>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Address).HasMaxLength(500).IsRequired();
                
                entity.HasOne(e => e.Builder)
                    .WithMany(u => u.BuilderProjects)
                    .HasForeignKey(e => e.BuilderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.BuilderId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => new { e.BuilderId, e.Status });
            });

            // ============================================
            // ProjectFee Configuration
            // ============================================
            builder.Entity<ProjectFee>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Fees)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProjectId);
            });

            // ============================================
            // Unit Configuration
            // ============================================
            builder.Entity<Unit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitNumber).HasMaxLength(50).IsRequired();
                entity.Property(e => e.PurchasePrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CurrentAppraisalValue).HasColumnType("decimal(18,2)");
                
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Units)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProjectId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Recommendation);
                entity.HasIndex(e => new { e.ProjectId, e.UnitNumber }).IsUnique();
            });

            // ============================================
            // UnitFee Configuration
            // ============================================
            builder.Entity<UnitFee>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Unit)
                    .WithMany(u => u.Fees)
                    .HasForeignKey(e => e.UnitId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================
            // UnitPurchaser Configuration
            // ============================================
            builder.Entity<UnitPurchaser>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Unit)
                    .WithMany(u => u.Purchasers)
                    .HasForeignKey(e => e.UnitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Purchaser)
                    .WithMany(u => u.PurchaserUnits)
                    .HasForeignKey(e => e.PurchaserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.UnitId, e.PurchaserId }).IsUnique();
            });

            // ============================================
            // MortgageInfo Configuration
            // ============================================
            builder.Entity<MortgageInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.UnitPurchaser)
                    .WithOne(up => up.MortgageInfo)
                    .HasForeignKey<MortgageInfo>(e => e.UnitPurchaserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================
            // PurchaserFinancials Configuration
            // ============================================
            builder.Entity<PurchaserFinancials>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.UnitPurchaser)
                    .WithOne(up => up.Financials)
                    .HasForeignKey<PurchaserFinancials>(e => e.UnitPurchaserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================
            // Deposit Configuration
            // ============================================
            builder.Entity<Deposit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                
                entity.HasOne(e => e.Unit)
                    .WithMany(u => u.Deposits)
                    .HasForeignKey(e => e.UnitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UnitId);
                entity.HasIndex(e => e.Status);
            });

            // ============================================
            // OccupancyFee Configuration
            // ============================================
            builder.Entity<OccupancyFee>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Unit)
                    .WithMany(u => u.OccupancyFees)
                    .HasForeignKey(e => e.UnitId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================
            // StatementOfAdjustments Configuration
            // ============================================
            builder.Entity<StatementOfAdjustments>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Unit)
                    .WithOne(u => u.SOA)
                    .HasForeignKey<StatementOfAdjustments>(e => e.UnitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UnitId).IsUnique();
                entity.HasIndex(e => e.IsConfirmedByLawyer);
            });

            // ============================================
            // ShortfallAnalysis Configuration
            // ============================================
            builder.Entity<ShortfallAnalysis>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Unit)
                    .WithOne(u => u.ShortfallAnalysis)
                    .HasForeignKey<ShortfallAnalysis>(e => e.UnitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UnitId).IsUnique();
                entity.HasIndex(e => e.RiskLevel);
                entity.HasIndex(e => e.Recommendation);
            });

            // ============================================
            // LawyerAssignment Configuration
            // ============================================
            builder.Entity<LawyerAssignment>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.LawyerAssignments)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Lawyer)
                    .WithMany(u => u.LawyerAssignments)
                    .HasForeignKey(e => e.LawyerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.UnitId, e.LawyerId }).IsUnique();
            });

            // ============================================
            // Document Configuration
            // ============================================
            // Document Configuration
            builder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.FilePath).HasMaxLength(500).IsRequired();

                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Documents)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Unit)
                    .WithMany(u => u.Documents)
                    .HasForeignKey(e => e.UnitId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.UploadedBy)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedById)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => e.ProjectId);
                entity.HasIndex(e => e.UnitId);
                entity.HasIndex(e => e.DocumentType);
            });

            // ============================================
            // AuditLog Configuration
            // ============================================
            builder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
                
                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.EntityId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
            });

            // ============================================
            // ProjectSummary Configuration
            // ============================================
            builder.Entity<ProjectSummary>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Project)
                    .WithOne()
                    .HasForeignKey<ProjectSummary>(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProjectId).IsUnique();
            });

            // ============================================
            // Lawyer Configuration
            // ============================================
            builder.Entity<LawyerNote>(entity =>
            {
                entity.HasOne(ln => ln.LawyerAssignment)
                    .WithMany(la => la.LawyerNotes)
                    .HasForeignKey(ln => ln.LawyerAssignmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Notification
            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.HasOne(n => n.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => new { n.UserId, n.IsRead });
                entity.HasIndex(n => n.CreatedAt);
                entity.HasIndex(n => n.GroupKey);
            });

            // ============================================
            // ProjectFinancials Configuration
            // ============================================
            builder.Entity<ProjectFinancials>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Project)
                    .WithOne(p => p.Financials)
                    .HasForeignKey<ProjectFinancials>(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProjectId).IsUnique();
            });

            // ============================================
            // ClosingExtensionRequest Configuration
            // ============================================
            builder.Entity<ClosingExtensionRequest>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Unit)
                    .WithMany(u => u.ExtensionRequests)
                    .HasForeignKey(e => e.UnitId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.RequestedByPurchaser)
                    .WithMany()
                    .HasForeignKey(e => e.RequestedByPurchaserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ReviewedByBuilder)
                    .WithMany()
                    .HasForeignKey(e => e.ReviewedByBuilderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.UnitId);
                entity.HasIndex(e => e.Status);
            });
        }

    }
}
