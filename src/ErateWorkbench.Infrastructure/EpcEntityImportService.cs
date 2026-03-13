using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

public class EpcEntityImportService(
    AppDbContext db,
    UsacCsvClient csvClient,
    EpcEntityCsvParser parser,
    EpcEntityRepository entityRepository,
    ILogger<EpcEntityImportService> logger)
{
    /// <summary>
    /// Downloads and imports the USAC E-Rate Supplemental Entity Information dataset.
    /// Default URL targets the full dataset export from datahub.usac.org.
    /// </summary>
    public async Task<ImportJob> RunAsync(
        string datasetUrl = "https://datahub.usac.org/api/views/7i5i-83qf/rows.csv?accessType=DOWNLOAD",
        CancellationToken cancellationToken = default)
    {
        const string datasetName = "supplemental-entity-information";

        var job = new ImportJob
        {
            DatasetName = datasetName,
            Status = ImportJobStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("EPC entity import job {JobId} started", job.Id);

        try
        {
            await using var stream = await csvClient.DownloadStreamAsync(datasetUrl, cancellationToken);

            // Parse and batch-save in chunks to avoid holding the full dataset in memory
            var batch = new List<EpcEntity>(500);
            int processed = 0;
            int failed = 0;

            foreach (var entity in parser.Parse(stream))
            {
                batch.Add(entity);

                if (batch.Count >= 500)
                {
                    (int saved, int errors) = await SaveBatchAsync(batch, cancellationToken);
                    processed += saved;
                    failed += errors;
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                (int saved, int errors) = await SaveBatchAsync(batch, cancellationToken);
                processed += saved;
                failed += errors;
            }

            job.Status = ImportJobStatus.Succeeded;
            job.RecordsProcessed = processed;
            job.RecordsFailed = failed;
            job.CompletedAt = DateTime.UtcNow;

            logger.LogInformation(
                "EPC entity import job {JobId} succeeded. Processed: {Processed}, Failed: {Failed}",
                job.Id, processed, failed);
        }
        catch (Exception ex)
        {
            job.Status = ImportJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;

            logger.LogError(ex, "EPC entity import job {JobId} failed", job.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    private async Task<(int saved, int errors)> SaveBatchAsync(
        List<EpcEntity> batch,
        CancellationToken cancellationToken)
    {
        try
        {
            await entityRepository.UpsertBatchAsync(batch, cancellationToken);
            return (batch.Count, 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save batch of {Count} entities", batch.Count);
            return (0, batch.Count);
        }
    }
}
