using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

public class ConsultantApplicationImportService(
    AppDbContext db,
    UsacCsvClient csvClient,
    ConsultantApplicationCsvParser parser,
    ConsultantApplicationRepository repo,
    ILogger<ConsultantApplicationImportService> logger)
{
    private const string DatasetName = "consultant-applications";
    private const string DefaultDatasetUrl =
        "https://datahub.usac.org/api/views/x5px-esft/rows.csv?accessType=DOWNLOAD";

    /// <summary>
    /// Downloads and imports the USAC Form 471 Consultants dataset (x5px-esft).
    /// Uses the views bulk download endpoint for full ingestion.
    ///
    /// Pre-flight availability check is performed before starting the download.
    /// Rows are upserted by RawSourceKey — re-runs are safe and idempotent.
    ///
    /// Dataset grain: one row per consultant per Form 471 application.
    /// See docs/schema_consultants.md for identity model and field semantics.
    /// </summary>
    public async Task<FundingImportResult> RunAsync(
        string? datasetUrl = null,
        CancellationToken cancellationToken = default)
    {
        var url = datasetUrl ?? DefaultDatasetUrl;
        var started = DateTime.UtcNow;

        var job = new ImportJob
        {
            DatasetName = DatasetName,
            Status = ImportJobStatus.Running,
            StartedAt = started,
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Consultant application import job {JobId} started (url={Url})", job.Id, url);

        int totalInserted = 0;
        int totalUpdated = 0;
        int totalFailed = 0;

        try
        {
            var probeUrl = BuildProbeUrl(url);
            if (!await csvClient.CheckAvailabilityAsync(probeUrl, cancellationToken))
            {
                const string unavailableMsg = "USAC data source is unavailable. Import aborted.";
                logger.LogError("Consultant application import job {JobId} aborted — {Message}", job.Id, unavailableMsg);
                job.Status = ImportJobStatus.Failed;
                job.ErrorMessage = unavailableMsg;
                job.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return new FundingImportResult(
                    0, 0, 0, 0, job.CompletedAt.Value - started,
                    DatasetName, ImportJobStatus.Failed, unavailableMsg);
            }

            await using var stream = await csvClient.DownloadStreamAsync(url, cancellationToken);

            var batch = new List<ConsultantApplication>(500);

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
                "Consultant application import job {JobId} succeeded. Inserted: {Inserted}, Updated: {Updated}, Failed: {Failed}",
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

            logger.LogError(ex, "Consultant application import job {JobId} failed", job.Id);

            return new FundingImportResult(
                totalInserted + totalUpdated, totalInserted, totalUpdated, totalFailed,
                job.CompletedAt.Value - started, DatasetName, ImportJobStatus.Failed, ex.Message);
        }
    }

    internal static string BuildProbeUrl(string datasetUrl)
    {
        var q = datasetUrl.IndexOf('?');
        var baseUrl = q >= 0 ? datasetUrl[..q] : datasetUrl;
        return baseUrl + "?$limit=1";
    }

    private async Task<(int inserted, int updated, int failed)> SaveBatchAsync(
        List<ConsultantApplication> batch,
        CancellationToken cancellationToken)
    {
        try
        {
            var (ins, upd) = await repo.UpsertBatchAsync(batch, cancellationToken);
            return (ins, upd, 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save batch of {Count} consultant applications", batch.Count);
            return (0, 0, batch.Count);
        }
    }
}
