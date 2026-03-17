using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<EpcEntity> EpcEntities => Set<EpcEntity>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<Disbursement> Disbursements => Set<Disbursement>();
    public DbSet<FundingCommitment> FundingCommitments => Set<FundingCommitment>();
    public DbSet<ServiceProvider> ServiceProviders => Set<ServiceProvider>();
    public DbSet<Form471Application> Form471Applications => Set<Form471Application>();
    public DbSet<ApplicantYearCommitmentSummary> ApplicantYearCommitmentSummaries => Set<ApplicantYearCommitmentSummary>();
    public DbSet<ApplicantYearDisbursementSummary> ApplicantYearDisbursementSummaries => Set<ApplicantYearDisbursementSummary>();
    public DbSet<ApplicantYearRiskSummary> ApplicantYearRiskSummaries => Set<ApplicantYearRiskSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Applicant>(e =>
        {
            e.HasIndex(a => new { a.Ben, a.FundingYear }).IsUnique();
            e.Property(a => a.Ben).HasMaxLength(20);
            e.Property(a => a.Name).HasMaxLength(255);
            e.Property(a => a.State).HasMaxLength(2);
            e.Property(a => a.Zip).HasMaxLength(10);
        });

        modelBuilder.Entity<ImportJob>(e =>
        {
            e.Property(j => j.DatasetName).HasMaxLength(100);
        });

        modelBuilder.Entity<EpcEntity>(e =>
        {
            e.HasIndex(en => en.EntityNumber).IsUnique();
            e.Property(en => en.EntityNumber).HasMaxLength(20);
            e.Property(en => en.EntityName).HasMaxLength(255);
            e.Property(en => en.PhysicalState).HasMaxLength(2);
            e.Property(en => en.PhysicalZip).HasMaxLength(10);
            e.Property(en => en.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<Entity>(e =>
        {
            e.HasIndex(en => en.EntityNumber).IsUnique();
            e.HasIndex(en => en.State);
            e.HasIndex(en => en.EntityType);
            e.Property(en => en.EntityNumber).HasMaxLength(20);
            e.Property(en => en.EntityName).HasMaxLength(255);
            e.Property(en => en.EntityType).HasMaxLength(50);
            e.Property(en => en.UrbanRuralStatus).HasMaxLength(50);
            e.Property(en => en.State).HasMaxLength(2);
        });

        modelBuilder.Entity<Disbursement>(e =>
        {
            e.HasIndex(d => d.RawSourceKey).IsUnique();
            e.HasIndex(d => d.FundingRequestNumber);
            e.HasIndex(d => d.FundingYear);
            e.HasIndex(d => d.ApplicantEntityNumber);
            e.HasIndex(d => d.ServiceProviderSpin);
            // Composite: year-filtered BEN aggregation used by RiskInsightsRepository.
            // Lets SQLite satisfy WHERE FundingYear=? AND ApplicantEntityNumber IN (...)
            // GROUP BY ApplicantEntityNumber with a single contiguous index range scan.
            e.HasIndex(d => new { d.FundingYear, d.ApplicantEntityNumber });
            e.Property(d => d.RawSourceKey).HasMaxLength(120);
            e.Property(d => d.FundingRequestNumber).HasMaxLength(50);
            e.Property(d => d.InvoiceId).HasMaxLength(30);
            e.Property(d => d.ApplicationNumber).HasMaxLength(20);
            e.Property(d => d.ApplicantEntityNumber).HasMaxLength(20);
            e.Property(d => d.ServiceProviderSpin).HasMaxLength(15);
            e.Property(d => d.InvoiceType).HasMaxLength(10);
            e.Property(d => d.InvoiceLineStatus).HasMaxLength(100);
            e.Property(d => d.CategoryOfService).HasMaxLength(50);
        });

        modelBuilder.Entity<FundingCommitment>(e =>
        {
            e.HasIndex(c => c.RawSourceKey).IsUnique();
            e.HasIndex(c => c.FundingYear);
            e.HasIndex(c => c.CategoryOfService);
            e.HasIndex(c => c.ServiceProviderName);
            e.HasIndex(c => c.ApplicantEntityNumber);
            e.HasIndex(c => c.CommitmentStatus);
            // Composite: year-filtered BEN aggregation used by RiskInsightsRepository.
            // Covers WHERE FundingYear=? AND ApplicantEntityNumber IS NOT NULL
            // GROUP BY ApplicantEntityNumber for GetTopRiskApplicants, GetTopGaps,
            // GetTopReductions. SQLite can resolve the group boundaries entirely from
            // the index without re-scanning the heap for each group key change.
            e.HasIndex(c => new { c.FundingYear, c.ApplicantEntityNumber });
            e.Property(c => c.RawSourceKey).HasMaxLength(100);
            e.Property(c => c.FundingRequestNumber).HasMaxLength(50);
            e.Property(c => c.ApplicantEntityNumber).HasMaxLength(20);
            e.Property(c => c.CommitmentStatus).HasMaxLength(100);
        });

        modelBuilder.Entity<ServiceProvider>(e =>
        {
            e.HasIndex(sp => sp.Spin).IsUnique();
            e.Property(sp => sp.Spin).HasMaxLength(15);
            e.Property(sp => sp.RawSourceKey).HasMaxLength(15);
            e.Property(sp => sp.ProviderName).HasMaxLength(255);
            e.Property(sp => sp.Status).HasMaxLength(50);
            e.Property(sp => sp.State).HasMaxLength(2);
            e.Property(sp => sp.Zip).HasMaxLength(10);
        });

        modelBuilder.Entity<Form471Application>(e =>
        {
            e.HasIndex(a => a.RawSourceKey).IsUnique();
            e.Property(a => a.RawSourceKey).HasMaxLength(60);
            e.Property(a => a.ApplicationNumber).HasMaxLength(20);
            e.Property(a => a.ApplicantEntityNumber).HasMaxLength(20);
            e.Property(a => a.ApplicantState).HasMaxLength(2);
            e.Property(a => a.CategoryOfService).HasMaxLength(50);
            e.Property(a => a.ApplicationStatus).HasMaxLength(100);
        });

        modelBuilder.Entity<ApplicantYearCommitmentSummary>(e =>
        {
            // Primary lookup: one row per (year, BEN).
            e.HasIndex(s => new { s.FundingYear, s.ApplicantEntityNumber }).IsUnique();
            // Single-column indexes for year-only and BEN-only filters.
            e.HasIndex(s => s.FundingYear);
            e.HasIndex(s => s.ApplicantEntityNumber);
            e.Property(s => s.ApplicantEntityNumber).HasMaxLength(20);
        });

        modelBuilder.Entity<ApplicantYearDisbursementSummary>(e =>
        {
            // Primary lookup: one row per (year, BEN).
            e.HasIndex(s => new { s.FundingYear, s.ApplicantEntityNumber }).IsUnique();
            // Single-column indexes for year-only and BEN-only filters.
            e.HasIndex(s => s.FundingYear);
            e.HasIndex(s => s.ApplicantEntityNumber);
            e.Property(s => s.ApplicantEntityNumber).HasMaxLength(20);
        });

        modelBuilder.Entity<ApplicantYearRiskSummary>(e =>
        {
            // Primary lookup: one row per (year, BEN).
            e.HasIndex(s => new { s.FundingYear, s.ApplicantEntityNumber }).IsUnique();
            // Single-column indexes for year-only, BEN-only, and level filters.
            e.HasIndex(s => s.FundingYear);
            e.HasIndex(s => s.ApplicantEntityNumber);
            e.HasIndex(s => s.RiskLevel);
            // Composite: year + level filter used by risk dashboard and analytics queries.
            e.HasIndex(s => new { s.FundingYear, s.RiskLevel });
            e.Property(s => s.ApplicantEntityNumber).HasMaxLength(20);
            e.Property(s => s.RiskLevel).HasMaxLength(20);
        });
    }
}
