using System.Diagnostics;
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
    // Resource API returns snake_case headers (matches CsvRow field names).
    // Views bulk download returns display names — do not use.
    private const string DefaultBaseUrl = "https://datahub.usac.org/resource/x5px-esft.csv";
    private const int DefaultPageSize = 10000;
    private const int BatchSize = 2000;

    /// <summary>
    /// Pages through the USAC Form 471 Consultants dataset (x5px-esft) via the Socrata resource API.
    /// Requests pages of <paramref name="pageSize"/> rows until an empty page is returned.
    ///
    /// Uses snake_case resource API — the views bulk download uses display-name headers
    /// and is not compatible with the CSV row mapping.
    ///
    /// Rows are upserted by RawSourceKey — re-runs are safe and idempotent.
    /// Dataset grain: one row per consultant per Form 471 application (~593K total rows).
    /// See docs/schema_consultants.md for identity model and field semantics.
    /// </summary>
    public async Task<FundingImportResult> RunAsync(
        string? baseUrl = null,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var url = baseUrl ?? DefaultBaseUrl;
        var started = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        var job = new ImportJob
        {
            DatasetName = DatasetName,
            Status = ImportJobStatus.Running,
            StartedAt = started,
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Consultant application import job {JobId} started — pageSize={PageSize}, source={BaseUrl}",
            job.Id, pageSize, url);

        int totalInserted = 0;
        int totalUpdated = 0;
        int totalFailed = 0;
        int pageNumber = 0;
        int offset = 0;

        try
        {
            var probeUrl = $"{url}?$limit=1";
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

            while (!cancellationToken.IsCancellationRequested)
            {
                var pageUrl = $"{url}?$limit={pageSize}&$offset={offset}";
                logger.LogInformation(
                    "Consultant application import job {JobId} — page {Page}, rows {From:N0}–{To:N0}",
                    job.Id, pageNumber + 1, offset, offset + pageSize - 1);

                int pageRows = 0;
                await using var stream = await csvClient.DownloadStreamAsync(pageUrl, cancellationToken);
                var batch = new List<ConsultantApplication>(BatchSize);

                foreach (var record in parser.Parse(stream))
                {
                    pageRows++;
                    batch.Add(record);

                    if (batch.Count >= BatchSize)
                    {
                        var (ins, upd, err) = await SaveBatchAsync(batch, cancellationToken);
                        totalInserted += ins; totalUpdated += upd; totalFailed += err;
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    var (ins, upd, err) = await SaveBatchAsync(batch, cancellationToken);
                    totalInserted += ins; totalUpdated += upd; totalFailed += err;
                }

                pageNumber++;
                offset += pageSize;

                logger.LogInformation(
                    "Consultant application import job {JobId} — page {Page} done, {PageRows:N0} rows | cumulative +{Ins:N0} inserted, ~{Upd:N0} updated | {Elapsed}",
                    job.Id, pageNumber, pageRows, totalInserted, totalUpdated, sw.Elapsed.ToString(@"m\:ss"));

                if (pageRows == 0) break;
            }

            job.Status = ImportJobStatus.Succeeded;
            job.RecordsProcessed = totalInserted + totalUpdated;
            job.RecordsFailed = totalFailed;
            job.CompletedAt = DateTime.UtcNow;

            logger.LogInformation(
                "Consultant application import job {JobId} complete — {Pages} pages | {Total:N0} processed | +{Ins:N0} inserted, ~{Upd:N0} updated, {Failed} failed | {Elapsed}",
                job.Id, pageNumber, job.RecordsProcessed, totalInserted, totalUpdated, totalFailed,
                sw.Elapsed.ToString(@"h\:mm\:ss"));

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

            logger.LogError(ex,
                "Consultant application import job {JobId} failed after {Pages} pages — {Total:N0} rows saved | {Elapsed}",
                job.Id, pageNumber, totalInserted + totalUpdated, sw.Elapsed.ToString(@"h\:mm\:ss"));

            return new FundingImportResult(
                totalInserted + totalUpdated, totalInserted, totalUpdated, totalFailed,
                job.CompletedAt.Value - started, DatasetName, ImportJobStatus.Failed, ex.Message);
        }
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
