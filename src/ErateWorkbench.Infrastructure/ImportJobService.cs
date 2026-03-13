using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

public class ImportJobService(
    AppDbContext db,
    UsacCsvClient csvClient,
    ApplicantCsvParser parser,
    ApplicantRepository applicantRepository,
    ILogger<ImportJobService> logger)
{
    public async Task<ImportJob> RunApplicantImportAsync(string datasetUrl, int fundingYear, CancellationToken cancellationToken = default)
    {
        var datasetName = $"applicants-fy{fundingYear}";

        var job = new ImportJob
        {
            DatasetName = datasetName,
            Status = ImportJobStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Import job {JobId} started for dataset {DatasetName}", job.Id, datasetName);

        try
        {
            await using var stream = await csvClient.DownloadStreamAsync(datasetUrl, cancellationToken);

            var applicants = parser.Parse(stream)
                .Where(a => a.FundingYear == 0 ? true : a.FundingYear == fundingYear)
                .Select(a => { a.FundingYear = fundingYear; return a; })
                .ToList();

            await applicantRepository.UpsertBatchAsync(applicants, cancellationToken);

            job.Status = ImportJobStatus.Succeeded;
            job.RecordsProcessed = applicants.Count;
            job.CompletedAt = DateTime.UtcNow;

            logger.LogInformation("Import job {JobId} succeeded. Records processed: {Count}", job.Id, applicants.Count);
        }
        catch (Exception ex)
        {
            job.Status = ImportJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;

            logger.LogError(ex, "Import job {JobId} failed", job.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<ImportJob?> GetJobAsync(int id, CancellationToken cancellationToken = default) =>
        await db.ImportJobs.FindAsync([id], cancellationToken);

    public IQueryable<ImportJob> QueryJobs() =>
        db.ImportJobs.OrderByDescending(j => j.StartedAt);

    public async Task<(int Total, int Succeeded, int Failed, ImportJob? Last)> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var total = await db.ImportJobs.CountAsync(cancellationToken);
        var succeeded = await db.ImportJobs.CountAsync(j => j.Status == ImportJobStatus.Succeeded, cancellationToken);
        var failed = await db.ImportJobs.CountAsync(j => j.Status == ImportJobStatus.Failed, cancellationToken);
        var last = await db.ImportJobs.OrderByDescending(j => j.StartedAt).FirstOrDefaultAsync(cancellationToken);
        return (total, succeeded, failed, last);
    }
}
