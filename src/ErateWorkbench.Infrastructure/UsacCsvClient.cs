using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace ErateWorkbench.Infrastructure;

public class UsacCsvClient(HttpClient httpClient, ILogger<UsacCsvClient> logger)
{
    private static readonly TimeSpan[] RetryDelays = [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    /// <summary>
    /// Performs a lightweight availability probe against the upstream Socrata endpoint.
    /// Makes a minimal GET request (headers only) and returns true if a 2xx response is received.
    /// Retries once on transient failures (5xx, 429, network errors) before reporting unavailable.
    ///
    /// Intended as a pre-flight check before starting a full import. A false result means the
    /// upstream is unreachable or returning error responses — callers should abort the import.
    /// </summary>
    public async Task<bool> CheckAvailabilityAsync(string probeUrl, CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 2;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(
                    probeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.IsSuccessStatusCode)
                    return true;

                var code = (int)response.StatusCode;

                if (IsTransientStatusCode(response.StatusCode) && attempt < maxAttempts - 1)
                {
                    logger.LogWarning(
                        "Availability probe to {ProbeUrl} returned HTTP {Code} (upstream temporarily unavailable). " +
                        "Retrying in {Delay}s.",
                        probeUrl, code, RetryDelays[attempt].TotalSeconds);
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                    continue;
                }

                logger.LogError(
                    "Availability probe to {ProbeUrl} returned HTTP {Code} — upstream is unavailable.",
                    probeUrl, code);
                return false;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (ex is HttpRequestException or SocketException or IOException && attempt < maxAttempts - 1)
                {
                    logger.LogWarning(
                        "Availability probe to {ProbeUrl} failed (attempt {Attempt}/{Max}): {Error}. Retrying in {Delay}s.",
                        probeUrl, attempt + 1, maxAttempts, ex.Message, RetryDelays[attempt].TotalSeconds);
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                    continue;
                }

                logger.LogError(
                    "Availability probe to {ProbeUrl} failed: {Error}",
                    probeUrl, ex.Message);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Downloads the content at <paramref name="url"/> as a stream.
    /// Retries up to 3 times on transient failures (network errors, 5xx, 429)
    /// with exponential backoff: 1s → 2s → 4s.
    ///
    /// HTTP 5xx and 429 responses are logged as "upstream temporarily unavailable" before retrying.
    /// Non-transient HTTP errors (4xx other than 429) propagate immediately without retry.
    /// </summary>
    public async Task<Stream> DownloadStreamAsync(string url, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var code = response.StatusCode;
                    response.Dispose();

                    var description = IsTransientStatusCode(code)
                        ? $"HTTP {(int)code} ({code}) — upstream temporarily unavailable"
                        : $"HTTP {(int)code} ({code})";

                    throw new HttpRequestException(description, null, code);
                }

                return await response.Content.ReadAsStreamAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                && attempt < RetryDelays.Length
                && IsTransientException(ex))
            {
                var delay = RetryDelays[attempt];
                logger.LogWarning(
                    "Download attempt {Attempt}/{Max} failed for {Url}: {Error}. Retrying in {Delay}s.",
                    attempt + 1, RetryDelays.Length + 1, url, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>HTTP status codes that indicate a transient upstream outage worth retrying.</summary>
    internal static bool IsTransientStatusCode(HttpStatusCode code) =>
        (int)code is 429 or 500 or 502 or 503 or 504;

    private static bool IsTransientException(Exception ex) =>
        ex is IOException or SocketException
        || ex is HttpRequestException { StatusCode: null }      // network-level failure, no response
        || ex is HttpRequestException hre && hre.StatusCode.HasValue && IsTransientStatusCode(hre.StatusCode.Value);
}
