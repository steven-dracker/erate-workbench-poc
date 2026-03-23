using ErateWorkbench.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests for UsacCsvClient.CheckAvailabilityAsync and DownloadStreamAsync behavior.
/// All retry tests inject a no-op delay so they run in milliseconds, not seconds.
/// </summary>
public class UsacCsvClientTests
{
    private static UsacCsvClient BuildClient(
        HttpMessageHandler handler,
        Action<TimeSpan>? onDelay = null)
    {
        // No-op delay by default; callers can observe invocations via onDelay.
        Task NoOp(TimeSpan d, CancellationToken _) { onDelay?.Invoke(d); return Task.CompletedTask; }
        return new(new HttpClient(handler), NullLogger<UsacCsvClient>.Instance, NoOp);
    }

    // ── IsTransientStatusCode unit tests ────────────────────────────────────

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void IsTransientStatusCode_ReturnsTrue_ForTransientCodes(int code) =>
        Assert.True(UsacCsvClient.IsTransientStatusCode((HttpStatusCode)code));

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    public void IsTransientStatusCode_ReturnsFalse_ForNonTransientCodes(int code) =>
        Assert.False(UsacCsvClient.IsTransientStatusCode((HttpStatusCode)code));

    // ── CheckAvailabilityAsync tests ─────────────────────────────────────────

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsTrue_WhenProbeSucceeds()
    {
        var handler = new StubHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await BuildClient(handler)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.True(result);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsFalse_WhenProbeReturns503()
    {
        // 503 triggers one retry then returns false. Delay is injected as no-op.
        var callCount = 0;
        var delayCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        var result = await BuildClient(handler, _ => delayCount++)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.False(result);
        Assert.Equal(2, callCount);   // initial + 1 retry
        Assert.Equal(1, delayCount);  // one backoff wait before the retry
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsFalse_ImmediatelyForNonTransientStatus()
    {
        // 404 is not a transient error — no retry, no delay.
        var callCount = 0;
        var delayCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var result = await BuildClient(handler, _ => delayCount++)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.False(result);
        Assert.Equal(1, callCount);  // no retry for 4xx
        Assert.Equal(0, delayCount); // no backoff wait
    }

    [Fact]
    public async Task CheckAvailabilityAsync_RetriesOnce_ThenReturnsFalse_OnNetworkError()
    {
        // Network error (HttpRequestException with no status) triggers one retry then false.
        var callCount = 0;
        var delayCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            throw new HttpRequestException("connection refused");
        });

        var result = await BuildClient(handler, _ => delayCount++)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.False(result);
        Assert.Equal(2, callCount);   // initial + 1 retry
        Assert.Equal(1, delayCount);  // one backoff wait before the retry
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsTrue_AfterTransientFailureThenSuccess()
    {
        // First attempt: 503 → wait → retry. Second attempt: 200.
        var callCount = 0;
        var delayCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            var status = callCount == 1
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        });

        var result = await BuildClient(handler, _ => delayCount++)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.True(result);
        Assert.Equal(2, callCount);
        Assert.Equal(1, delayCount); // one backoff wait between attempts
    }

    // ── DownloadStreamAsync tests ───────────────────────────────────────────

    [Fact]
    public async Task DownloadStreamAsync_ThrowsHttpRequestException_With503Description()
    {
        // 503 exhausts all 3 retries then throws. Delay is injected as no-op.
        var callCount = 0;
        var delayCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => BuildClient(handler, _ => delayCount++)
                .DownloadStreamAsync("https://test.example.com/data.csv"));

        Assert.Contains("unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(4, callCount);   // initial + 3 retries
        Assert.Equal(3, delayCount);  // 3 backoff waits (1s→2s→4s in production)
    }

    [Fact]
    public async Task DownloadStreamAsync_ThrowsImmediately_ForNonTransient404()
    {
        // A 404 should not be retried — verify only one HTTP call is made and no delay fires.
        var callCount = 0;
        var delayCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => BuildClient(handler, _ => delayCount++)
                .DownloadStreamAsync("https://test.example.com/data.csv"));

        Assert.Equal(1, callCount);  // no retries for 404
        Assert.Equal(0, delayCount); // no backoff wait
    }

    [Fact]
    public async Task DownloadStreamAsync_RetriesOnTransient_ThenSucceeds()
    {
        // First two attempts: 503. Third attempt: 200. Verifies retry sequence.
        var callCount = 0;
        var delayCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            var status = callCount < 3
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("col\nval", Encoding.UTF8, "text/csv"),
            });
        });

        await using var stream = await BuildClient(handler, _ => delayCount++)
            .DownloadStreamAsync("https://test.example.com/data.csv");

        Assert.Equal(3, callCount);   // 2 failures + 1 success
        Assert.Equal(2, delayCount);  // 2 backoff waits
        Assert.NotNull(stream);
    }
}

file sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
}
