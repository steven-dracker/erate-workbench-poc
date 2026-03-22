using ErateWorkbench.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests for UsacCsvClient.CheckAvailabilityAsync and DownloadStreamAsync behavior.
/// </summary>
public class UsacCsvClientTests
{
    private static UsacCsvClient BuildClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<UsacCsvClient>.Instance);

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
        // 503 triggers one retry (1s delay) then returns false.
        // This test takes ~1s due to the retry delay.
        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        var result = await BuildClient(handler)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.False(result);
        Assert.Equal(2, callCount); // initial + 1 retry
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsFalse_ImmediatelyForNonTransientStatus()
    {
        // 404 is not a transient error — no retry should fire.
        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var result = await BuildClient(handler)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.False(result);
        Assert.Equal(1, callCount); // no retry for 4xx
    }

    [Fact]
    public async Task CheckAvailabilityAsync_RetriesOnce_ThenReturnsFalse_OnNetworkError()
    {
        // Network error (HttpRequestException with no status) triggers one retry then false.
        // This test takes ~1s due to the retry delay.
        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            throw new HttpRequestException("connection refused");
        });

        var result = await BuildClient(handler)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.False(result);
        Assert.Equal(2, callCount); // initial + 1 retry
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsTrue_AfterTransientFailureThenSuccess()
    {
        // First attempt: 503. Second attempt: 200.
        // This test takes ~1s due to the retry delay.
        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            var status = callCount == 1
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        });

        var result = await BuildClient(handler)
            .CheckAvailabilityAsync("https://test.example.com/resource/test.csv?$limit=1");

        Assert.True(result);
        Assert.Equal(2, callCount);
    }

    // ── DownloadStreamAsync improved classification tests ───────────────────

    [Fact]
    public async Task DownloadStreamAsync_ThrowsHttpRequestException_With503Description()
    {
        // Verify that a 503 response produces an exception message clearly stating "unavailable".
        // Three retries (1s + 2s + 4s) then throws — this test takes ~7s.
        // Skip if running in CI to avoid slow tests; included for local validation.
        var handler = new StubHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var client = BuildClient(handler);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.DownloadStreamAsync("https://test.example.com/data.csv"));

        Assert.Contains("unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    [Fact]
    public async Task DownloadStreamAsync_ThrowsImmediately_ForNonTransient404()
    {
        // A 404 should not be retried — verify only one HTTP call is made.
        var callCount = 0;
        var handler = new StubHttpHandler(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => BuildClient(handler).DownloadStreamAsync("https://test.example.com/data.csv"));

        Assert.Equal(1, callCount); // no retries for 404
    }
}

file sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
}
