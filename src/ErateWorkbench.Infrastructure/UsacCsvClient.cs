namespace ErateWorkbench.Infrastructure;

public class UsacCsvClient(HttpClient httpClient)
{
    public async Task<Stream> DownloadStreamAsync(string url, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
}
