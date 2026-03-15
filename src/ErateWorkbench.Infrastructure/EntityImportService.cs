using System.Diagnostics;
using System.Net.Sockets;
using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

public class EntityImportService(
    AppDbContext db,
    UsacCsvClient csvClient,
    EntityCsvParser parser,
    EntityRepository repo,
    ILogger<EntityImportService> logger)
{
    private const string DatasetName = "ros-entities";

    // Source: ros_* columns are present in the funding commitments dataset (avi8-svp9).
    // Paging through it extracts a deduplicated entity dimension keyed on EntityNumber.
    private const string DefaultBaseUrl = "https://datahub.usac.org/resource/avi8-svp9.csv";
    private const int DefaultPageSize = 10000;
    private const int BatchSize = 2000;
    private const int PageMaxAttempts = 4; // 1 initial + 3 retries
    private static readonly TimeSpan[] PageRetryDelays = [
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    /// <summary>
    /// Pages through the USAC dataset via the Socrata resource API, extracts ROS entity
    /// fields, and upserts into the Entities table keyed on EntityNumber.
    ///
    /// Because entity data repeats on every commitment row, this produces far fewer
    /// Entities rows than commitment rows — the repository deduplicates within each batch
    /// and the upsert handles cross-page duplicates.
    /// </summary>
    public async Task<FundingImportResult> RunAsync(
        string baseUrl = DefaultBaseUrl,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
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
            "[entity-import:{JobId}] Started — pageSize={PageSize}, source={BaseUrl}",
            job.Id, pageSize, baseUrl);

        int totalInserted = 0;
        int totalUpdated = 0;
        int totalFailed = 0;
        int pageNumber = 0;
        int offset = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pageUrl = $"{baseUrl}?$limit={pageSize}&$offset={offset}";
                logger.LogInformation(
                    "[entity-import:{JobId}] Page {Page} — rows {OffsetFrom:N0}–{OffsetTo:N0}",
                    job.Id, pageNumber + 1, offset, offset + pageSize - 1);

                var (pageRows, pageIns, pageUpd, pageErr) =
                    await ProcessPageWithRetryAsync(pageUrl, offset, pageNumber + 1, job.Id, cancellationToken);

                totalInserted += pageIns;
                totalUpdated += pageUpd;
                totalFailed += pageErr;

                int cumulative = totalInserted + totalUpdated + totalFailed;
                logger.LogInformation(
                    "[entity-import:{JobId}] Page {Page} done — {PageRows:N0} rows | " +
                    "{Cumulative:N0} entities total (+{Ins:N0} new, ~{Upd:N0} updated) | {Elapsed}",
                    job.Id, pageNumber + 1, pageRows,
                    cumulative, totalInserted, totalUpdated, sw.Elapsed.ToString(@"m\:ss"));

                pageNumber++;
                offset += pageSize;

                if (pageRows == 0)
                    break;
            }

            job.Status = ImportJobStatus.Succeeded;
            job.RecordsProcessed = totalInserted + totalUpdated;
            job.RecordsFailed = totalFailed;
            job.CompletedAt = DateTime.UtcNow;

            logger.LogInformation(
                "[entity-import:{JobId}] Complete — {Pages} pages | {Total:N0} entities | " +
                "+{Inserted:N0} inserted, ~{Updated:N0} updated, {Failed} failed | {Elapsed}",
                job.Id, pageNumber, job.RecordsProcessed,
                totalInserted, totalUpdated, totalFailed, sw.Elapsed.ToString(@"h\:mm\:ss"));

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
                "[entity-import:{JobId}] Failed after {Pages} pages — {Total:N0} entities saved | {Elapsed}",
                job.Id, pageNumber, totalInserted + totalUpdated, sw.Elapsed.ToString(@"h\:mm\:ss"));

            return new FundingImportResult(
                totalInserted + totalUpdated, totalInserted, totalUpdated, totalFailed,
                job.CompletedAt.Value - started, DatasetName, ImportJobStatus.Failed, ex.Message);
        }
    }

    private async Task<(int pageRows, int inserted, int updated, int failed)>
        ProcessPageWithRetryAsync(
            string pageUrl, int offset, int pageNumber, int jobId,
            CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= PageMaxAttempts; attempt++)
        {
            try
            {
                return await DownloadAndProcessPageAsync(pageUrl, offset, cancellationToken);
            }
            catch (Exception ex) when (
                !cancellationToken.IsCancellationRequested
                && attempt < PageMaxAttempts
                && FundingCommitmentImportService.IsTransientError(ex))
            {
                var delay = PageRetryDelays[attempt - 1];
                logger.LogWarning(
                    "[entity-import:{JobId}] Page {Page} (offset={Offset}) attempt {Attempt}/{Max} failed — {Error} — retrying in {Delay}s",
                    jobId, pageNumber, offset, attempt, PageMaxAttempts, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        logger.LogWarning(
            "[entity-import:{JobId}] Page {Page} (offset={Offset}) — final attempt {Max}/{Max}",
            jobId, pageNumber, offset, PageMaxAttempts, PageMaxAttempts);
        return await DownloadAndProcessPageAsync(pageUrl, offset, cancellationToken);
    }

    private async Task<(int pageRows, int inserted, int updated, int failed)>
        DownloadAndProcessPageAsync(string pageUrl, int offset, CancellationToken cancellationToken)
    {
        await using var stream = await csvClient.DownloadStreamAsync(pageUrl, cancellationToken);

        int firstByte = stream.ReadByte();
        if (firstByte == '{' || firstByte == '[')
            throw new InvalidOperationException(
                $"Page at offset {offset} returned non-CSV content (JSON/error payload).");

        var pageStream = firstByte == -1
            ? Stream.Null
            : new PrependByteStream((byte)firstByte, stream);

        int pageRows = 0;
        int inserted = 0;
        int updated = 0;
        int failed = 0;
        var batch = new List<Entity>(BatchSize);

        try
        {
            foreach (var record in parser.Parse(pageStream))
            {
                pageRows++;
                batch.Add(record);

                if (batch.Count >= BatchSize)
                {
                    var (ins, upd, err) = await SaveBatchAsync(batch, cancellationToken);
                    inserted += ins; updated += upd; failed += err;
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                var (ins, upd, err) = await SaveBatchAsync(batch, cancellationToken);
                inserted += ins; updated += upd; failed += err;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new IOException($"Stream error reading entity page at offset {offset}: {ex.Message}", ex);
        }

        return (pageRows, inserted, updated, failed);
    }

    private async Task<(int inserted, int updated, int failed)> SaveBatchAsync(
        List<Entity> batch,
        CancellationToken cancellationToken)
    {
        try
        {
            var (ins, upd) = await repo.UpsertBatchAsync(batch, cancellationToken);
            return (ins, upd, 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[entity-import] Failed to save batch of {Count} entities", batch.Count);
            return (0, 0, batch.Count);
        }
    }

    /// <summary>
    /// Minimal stream that prepends a single already-read byte before the inner stream.
    /// Mirrors the ConcatenatedStream in FundingCommitmentImportService — used to
    /// "unread" the first byte consumed during JSON/CSV content-type detection.
    /// </summary>
    private sealed class PrependByteStream(byte firstByte, Stream inner) : Stream
    {
        private bool _firstRead = true;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0) return 0;
            if (_firstRead)
            {
                _firstRead = false;
                buffer[offset] = firstByte;
                if (count == 1) return 1;
                int rest = inner.Read(buffer, offset + 1, count - 1);
                return 1 + rest;
            }
            return inner.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            if (count == 0) return 0;
            if (_firstRead)
            {
                _firstRead = false;
                buffer[offset] = firstByte;
                if (count == 1) return 1;
                int rest = await inner.ReadAsync(buffer, offset + 1, count - 1, ct);
                return 1 + rest;
            }
            return await inner.ReadAsync(buffer, offset, count, ct);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
