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
    /// Downloads and imports the USAC FCC Form 471 dataset (9s6i-myen).
    ///
    /// When <paramref name="fundingYear"/> is specified the import uses a year-scoped
    /// resource URL so only that year's records are fetched and upserted — suitable for
    /// incremental FY2026 refreshes. When null the full dataset is downloaded.
    ///
    /// Year-scoped strategy: full re-import of the target FY. This handles both new
    /// applications and status changes on existing ones because all rows are upserted
    /// by RawSourceKey. The Socrata resource endpoint supports $where filtering;
    /// the bulk views endpoint does not.
    /// </summary>
    public async Task<FundingImportResult> RunAsync(
        string? datasetUrl = null,
        int? fundingYear = null,
        CancellationToken cancellationToken = default)
    {
        var url = datasetUrl ?? BuildUrl(fundingYear);
        var started = DateTime.UtcNow;

        var job = new ImportJob
        {
            DatasetName = DatasetName,
            Status = ImportJobStatus.Running,
            StartedAt = started,
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Form 471 import job {JobId} started (url={Url})", job.Id, url);

        int totalInserted = 0;
        int totalUpdated = 0;
        int totalFailed = 0;

        try
        {
            var probeUrl = BuildProbeUrl(url);
            if (!await csvClient.CheckAvailabilityAsync(probeUrl, cancellationToken))
            {
                const string unavailableMsg = "USAC data source is unavailable. Import aborted.";
                logger.LogError("Form 471 import job {JobId} aborted — {Message}", job.Id, unavailableMsg);
                job.Status = ImportJobStatus.Failed;
                job.ErrorMessage = unavailableMsg;
                job.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return new FundingImportResult(
                    0, 0, 0, 0, job.CompletedAt.Value - started,
                    DatasetName, ImportJobStatus.Failed, unavailableMsg);
            }

            await using var stream = await csvClient.DownloadStreamAsync(url, cancellationToken);

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

    private static string BuildUrl(int? fundingYear)
    {
        if (fundingYear.HasValue)
            // Resource endpoint supports $where; funding_year is stored as TEXT in 9s6i-myen.
            return $"https://datahub.usac.org/resource/9s6i-myen.csv?$where=funding_year='{fundingYear}'&$limit=50000";

        return "https://datahub.usac.org/api/views/9s6i-myen/rows.csv?accessType=DOWNLOAD";
    }

    /// <summary>
    /// Builds a minimal probe URL from the dataset URL by stripping any existing query string
    /// and appending <c>?$limit=1</c>. Works for both the resource API and views API.
    /// </summary>
    internal static string BuildProbeUrl(string datasetUrl)
    {
        var q = datasetUrl.IndexOf('?');
        var baseUrl = q >= 0 ? datasetUrl[..q] : datasetUrl;
        return baseUrl + "?$limit=1";
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
