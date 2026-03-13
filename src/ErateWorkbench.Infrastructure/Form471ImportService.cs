using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

public class Form471ImportService(
    AppDbContext db,
    UsacCsvClient csvClient,
    Form471CsvParser parser,
    Form471Repository repo,
    ILogger<Form471ImportService> logger)
{
    private const string DatasetName = "form471-applications";

    /// <summary>
    /// Downloads and imports the USAC FCC Form 471 dataset.
    /// The default URL targets the full dataset export. Override for testing or year-specific exports.
    /// </summary>
    public async Task<FundingImportResult> RunAsync(
        string datasetUrl = "https://datahub.usac.org/api/views/9s85-xeem/rows.csv?accessType=DOWNLOAD",
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

        logger.LogInformation("Form 471 import job {JobId} started", job.Id);

        int totalInserted = 0;
        int totalUpdated = 0;
        int totalFailed = 0;

        try
        {
            await using var stream = await csvClient.DownloadStreamAsync(datasetUrl, cancellationToken);

            var batch = new List<Form471Application>(500);

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
                "Form 471 import job {JobId} succeeded. Inserted: {Inserted}, Updated: {Updated}, Failed: {Failed}",
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

            logger.LogError(ex, "Form 471 import job {JobId} failed", job.Id);

            return new FundingImportResult(
                totalInserted + totalUpdated, totalInserted, totalUpdated, totalFailed,
                job.CompletedAt.Value - started, DatasetName, ImportJobStatus.Failed, ex.Message);
        }
    }

    private async Task<(int inserted, int updated, int failed)> SaveBatchAsync(
        List<Form471Application> batch,
        CancellationToken cancellationToken)
    {
        try
        {
            var (ins, upd) = await repo.UpsertBatchAsync(batch, cancellationToken);
            return (ins, upd, 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save batch of {Count} Form 471 applications", batch.Count);
            return (0, 0, batch.Count);
        }
    }
}
