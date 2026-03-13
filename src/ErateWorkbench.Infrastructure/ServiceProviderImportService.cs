using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

public class ServiceProviderImportService(
    AppDbContext db,
    UsacCsvClient csvClient,
    ServiceProviderCsvParser parser,
    ServiceProviderRepository repo,
    ILogger<ServiceProviderImportService> logger)
{
    private const string DatasetName = "service-providers";

    /// <summary>
    /// Downloads and imports the USAC E-Rate Service Provider (SPIN) dataset.
    /// The default URL targets the full dataset export. Override for testing or alternate sources.
    /// </summary>
    public async Task<FundingImportResult> RunAsync(
        string datasetUrl = "https://datahub.usac.org/api/views/s8d5-n6qr/rows.csv?accessType=DOWNLOAD",
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;

        var job = new ImportJob
        {
            DatasetName = DatasetName,
            Status = ImportJobStatus.Running,
            StartedAt = started,
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Service provider import job {JobId} started", job.Id);

        int totalInserted = 0;
        int totalUpdated = 0;
        int totalFailed = 0;

        try
        {
            await using var stream = await csvClient.DownloadStreamAsync(datasetUrl, cancellationToken);

            var batch = new List<ServiceProvider>(500);

            foreach (var record in parser.Parse(stream))
            {
                batch.Add(record);

                if (batch.Count >= 500)
                {
                    var (ins, upd, err) = await SaveBatchAsync(batch, cancellationToken);
                    totalInserted += ins;
                    totalUpdated += upd;
                    totalFailed += err;
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                var (ins, upd, err) = await SaveBatchAsync(batch, cancellationToken);
                totalInserted += ins;
                totalUpdated += upd;
                totalFailed += err;
            }

            job.Status = ImportJobStatus.Succeeded;
            job.RecordsProcessed = totalInserted + totalUpdated;
            job.RecordsFailed = totalFailed;
            job.CompletedAt = DateTime.UtcNow;

            logger.LogInformation(
                "Service provider import job {JobId} succeeded. Inserted: {Inserted}, Updated: {Updated}, Failed: {Failed}",
                job.Id, totalInserted, totalUpdated, totalFailed);

            await db.SaveChangesAsync(cancellationToken);

            return new FundingImportResult(
                job.RecordsProcessed, totalInserted, totalUpdated, totalFailed,
                job.CompletedAt.Value - started, DatasetName, ImportJobStatus.Succeeded);
        }
        catch (Exception ex)
        {
            job.Status = ImportJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogError(ex, "Service provider import job {JobId} failed", job.Id);

            return new FundingImportResult(
                totalInserted + totalUpdated, totalInserted, totalUpdated, totalFailed,
                job.CompletedAt.Value - started, DatasetName, ImportJobStatus.Failed, ex.Message);
        }
    }

    private async Task<(int inserted, int updated, int failed)> SaveBatchAsync(
        List<ServiceProvider> batch,
        CancellationToken cancellationToken)
    {
        try
        {
            var (ins, upd) = await repo.UpsertBatchAsync(batch, cancellationToken);
            return (ins, upd, 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save batch of {Count} service providers", batch.Count);
            return (0, 0, batch.Count);
        }
    }
}
