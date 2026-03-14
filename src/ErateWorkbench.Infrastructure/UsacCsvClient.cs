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

    public async Task<Stream> DownloadStreamAsync(string url, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStreamAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                && attempt < RetryDelays.Length
                && ex is HttpRequestException or SocketException)
            {
                var delay = RetryDelays[attempt];
                logger.LogWarning(
                    "Download attempt {Attempt} failed for {Url}: {Error}. Retrying in {Delay}s.",
                    attempt + 1, url, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
