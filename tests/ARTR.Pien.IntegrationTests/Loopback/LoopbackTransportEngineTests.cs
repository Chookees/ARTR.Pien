using System.IO.Compression;
using System.Net;
using System.Text;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Engine;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Hosting;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Web.Crawl;
using ARTR.Pien.Web.Network;
using ARTR.Pien.Web.Transport;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ARTR.Pien.IntegrationTests.Loopback;

public sealed class LoopbackTransportEngineTests : IAsyncLifetime
{
    private IHost? _host;
    private Uri? _baseUri;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapGet("/", async ctx =>
        {
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            ctx.Response.Headers["Permissions-Policy"] = "geolocation=()";
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync("<!doctype html><html lang=\"en\"><head><title>Home</title><meta name=\"description\" content=\"d\"/></head><body><a href=\"/about\">About</a><img alt=\"x\"/></body></html>");
        });
        app.MapGet("/about", async ctx =>
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync("<!doctype html><html lang=\"en\"><head><title>About</title></head><body>About</body></html>");
        });
        app.MapGet("/redirect", ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status302Found;
            ctx.Response.Headers.Location = "/";
            return Task.CompletedTask;
        });
        app.MapGet("/bad-redirect", ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status302Found;
            return Task.CompletedTask;
        });
        app.MapGet("/invalid-location", ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status302Found;
            ctx.Response.Headers.Location = "http://[";
            return Task.CompletedTask;
        });
        app.MapGet("/cross-host-redirect", ctx =>
        {
            var port = ctx.Connection.LocalPort;
            ctx.Response.StatusCode = StatusCodes.Status302Found;
            ctx.Response.Headers.Location = $"http://127.0.0.1:{port}/";
            return Task.CompletedTask;
        });
        app.MapMethods("/echo", ["POST", "PUT", "PATCH", "DELETE", "OPTIONS"], async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.WriteAsync("echo");
        });
        app.MapGet("/large", async ctx =>
        {
            ctx.Response.ContentType = "application/octet-stream";
            var payload = new byte[4096];
            Random.Shared.NextBytes(payload);
            await ctx.Response.Body.WriteAsync(payload);
        });
        app.MapMethods("/head-only", ["HEAD"], ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        app.MapGet("/accept-encoding", async ctx =>
        {
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.WriteAsync(ctx.Request.Headers.AcceptEncoding.ToString());
        });
        app.MapGet("/fake-brotli", async ctx =>
        {
            // Not valid Brotli on purpose: the transport must hand these bytes back verbatim
            // instead of feeding them to the native Brotli decoder.
            ctx.Response.ContentType = "text/plain";
            ctx.Response.Headers.ContentEncoding = "br";
            await ctx.Response.WriteAsync("not-brotli");
        });
        // GAP-PIEN27-GZIP-BOMB-TEST: compressed zeros that expand past BodyInspectionLimitBytes.
        app.MapGet("/gzip-over-limit", async ctx =>
        {
            var limit = ScanLimits.Default.BodyInspectionLimitBytes;
            await WriteGzipZerosAsync(ctx, limit + 1024);
        });
        // Control: decompressed size stays just under the transport body inspection limit.
        app.MapGet("/gzip-under-limit", async ctx =>
        {
            var limit = ScanLimits.Default.BodyInspectionLimitBytes;
            await WriteGzipZerosAsync(ctx, limit - 1);
        });
        // GAP-PIEN28-GZIP-TEST-LIMIT-SOURCE: non-default 1 MiB bound (over / just-under).
        app.MapGet("/gzip-1mib-over", async ctx =>
        {
            await WriteGzipZerosAsync(ctx, (1L * 1024 * 1024) + 1024);
        });
        app.MapGet("/gzip-1mib-under", async ctx =>
        {
            await WriteGzipZerosAsync(ctx, (1L * 1024 * 1024) - 1);
        });
        app.MapGet("/api/health", async ctx =>
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"status":"ok"}""");
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        _host = app;
        _baseUri = new Uri(app.Urls.First(u => u.StartsWith("http://127.0.0.1", StringComparison.Ordinal)));
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
        }
    }

    [Fact]
    public async Task Safe_transport_gets_home_page()
    {
        var network = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1", "localhost"],
        };
        using var transport = new SafeHttpTransport(new DestinationValidator(), network);
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "loop",
            Kind = ScanTargetKind.Website,
            BaseUrl = _baseUri!,
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [target],
            Limits = ScanLimits.Default with { MaxCrawlPages = 2, MaxCrawlDepth = 1 },
        });
        var context = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
        var result = await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest { Uri = _baseUri!, Method = ProbeMethod.Get }),
            context,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("Home", Encoding.UTF8.GetString(result.Body.Span), StringComparison.Ordinal);
    }

    // CVE-2026-111497 / GHSA-9v8h-qqgm-4m35: Brotli must not be advertised to untrusted servers.
    [Fact]
    public async Task Transport_does_not_advertise_brotli()
    {
        var result = await SendLoopbackGetAsync("/accept-encoding");
        var acceptEncoding = Encoding.UTF8.GetString(result.Body.Span);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("gzip", acceptEncoding, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("br", acceptEncoding, StringComparison.OrdinalIgnoreCase);
    }

    // CVE-2026-111497 / GHSA-9v8h-qqgm-4m35: a "Content-Encoding: br" body must reach the caller
    // undecoded, which proves the native Brotli decoder never runs on server bytes.
    [Fact]
    public async Task Transport_does_not_decode_brotli_responses()
    {
        var result = await SendLoopbackGetAsync("/fake-brotli");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("not-brotli", Encoding.UTF8.GetString(result.Body.Span));
    }

    // GAP-PIEN27-GZIP-BOMB-TEST: HttpClient auto-decompresses gzip before ReadBoundedAsync runs.
    // The transport must enforce ScanLimits.Default.BodyInspectionLimitBytes on the decompressed
    // stream (BodyTruncated + never more than the limit), not on the tiny wire payload.
    [Fact]
    public async Task Transport_truncates_gzip_body_above_inspection_limit()
    {
        var limit = ScanLimits.Default.BodyInspectionLimitBytes;
        var result = await SendLoopbackGetAsync("/gzip-over-limit");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.BodyTruncated);
        Assert.Equal(limit, result.Body.Length);
        Assert.True(result.Body.Length <= limit);
    }

    // GAP-PIEN27-GZIP-BOMB-TEST control: a gzip body that decompresses to just under the limit
    // must be returned in full with BodyTruncated == false.
    [Fact]
    public async Task Transport_returns_full_gzip_body_just_under_inspection_limit()
    {
        var limit = ScanLimits.Default.BodyInspectionLimitBytes;
        var result = await SendLoopbackGetAsync("/gzip-under-limit");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.False(result.BodyTruncated);
        Assert.Equal(limit - 1, result.Body.Length);
    }

    // GAP-PIEN28-GZIP-TEST-LIMIT-SOURCE: MaxResponseBodyBytes must be taken from the probe
    // request (1 MiB here). A transport that hard-wires 5 MiB would accept the over-limit
    // body and fail these assertions.
    [Fact]
    public async Task Transport_honors_non_default_one_mib_gzip_body_limit()
    {
        const long limit = 1L * 1024 * 1024;
        var over = await SendLoopbackGetAsync("/gzip-1mib-over", limit);
        Assert.Equal(HttpStatusCode.OK, over.StatusCode);
        Assert.True(over.BodyTruncated);
        Assert.Equal(limit, over.Body.Length);
        Assert.True(over.Body.Length <= limit);

        var under = await SendLoopbackGetAsync("/gzip-1mib-under", limit);
        Assert.Equal(HttpStatusCode.OK, under.StatusCode);
        Assert.False(under.BodyTruncated);
        Assert.Equal(limit - 1, under.Body.Length);
    }

    [Fact]
    public async Task Transport_follows_redirect_and_revalidates()
    {
        var network = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1", "localhost"],
            MaxRedirects = 3,
        };
        using var transport = new SafeHttpTransport(new DestinationValidator(), network);
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "loop",
            Kind = ScanTargetKind.Website,
            BaseUrl = _baseUri!,
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [target],
            Limits = ScanLimits.Default,
        });
        var context = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
        var result = await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest { Uri = new Uri(_baseUri!, "/redirect"), Method = ProbeMethod.Get }),
            context,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotEmpty(result.RedirectChain);
    }

    [Fact]
    public async Task Transport_rejects_redirect_without_location()
    {
        var network = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1", "localhost"],
            MaxRedirects = 3,
        };
        using var transport = new SafeHttpTransport(new DestinationValidator(), network);
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "loop",
            Kind = ScanTargetKind.Website,
            BaseUrl = _baseUri!,
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [target],
            Limits = ScanLimits.Default,
        });
        var context = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ARTR.Pien.Exceptions.HttpProtocolException>(() => transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest { Uri = new Uri(_baseUri!, "/bad-redirect"), Method = ProbeMethod.Get }),
            context,
            TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ARTR.Pien.Exceptions.TargetSafetyException>(() => transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest { Uri = new Uri(_baseUri!, "/invalid-location"), Method = ProbeMethod.Get }),
            context,
            TestContext.Current.CancellationToken));

        var userInfoUri = new UriBuilder(_baseUri!) { UserName = "u", Password = "p", Path = "/cross-host-redirect" }.Uri;
        var stripped = await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest { Uri = userInfoUri, Method = ProbeMethod.Get }),
            context,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stripped.StatusCode);

        foreach (var method in new[] { ProbeMethod.Post, ProbeMethod.Put, ProbeMethod.Patch, ProbeMethod.Delete, ProbeMethod.Options })
        {
            var echo = await transport.SendAsync(
                ProbeRequest.Create(new ProbeRequest
                {
                    Uri = new Uri(_baseUri!, "/echo"),
                    Method = method,
                    Body = method is ProbeMethod.Post or ProbeMethod.Put or ProbeMethod.Patch
                        ? Encoding.UTF8.GetBytes("body")
                        : ReadOnlyMemory<byte>.Empty,
                    ContentType = "text/plain",
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Custom"] = "1",
                        ["Content-Language"] = "en",
                    },
                    MaxResponseBodyBytes = 1024,
                }),
                context,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, echo.StatusCode);
        }

        var truncated = await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest
            {
                Uri = new Uri(_baseUri!, "/large"),
                Method = ProbeMethod.Get,
                MaxResponseBodyBytes = 64,
            }),
            context,
            TestContext.Current.CancellationToken);
        Assert.True(truncated.BodyTruncated);
        Assert.True(truncated.Body.Length <= 64);

        using (var zeroRedirect = new SafeHttpTransport(new DestinationValidator(), network with { MaxRedirects = 0 }))
        {
            var stopped = await zeroRedirect.SendAsync(
                ProbeRequest.Create(new ProbeRequest { Uri = new Uri(_baseUri!, "/redirect"), Method = ProbeMethod.Get }),
                context,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Found, stopped.StatusCode);
        }

        var head = await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest { Uri = new Uri(_baseUri!, "/head-only"), Method = ProbeMethod.Head, MaxResponseBodyBytes = 1 }),
            context,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Throws<ArgumentNullException>(() => new SafeHttpTransport(null!, network));
        Assert.Throws<ArgumentNullException>(() => new SafeHttpTransport(new DestinationValidator(), null!));
        transport.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest { Uri = _baseUri!, Method = ProbeMethod.Get }),
            context,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Crawler_yields_same_origin_pages()
    {
        var network = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1", "localhost"],
        };
        using var transport = new SafeHttpTransport(new DestinationValidator(), network);
        var crawler = new WebsiteCrawler(transport, network);
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "loop",
            Kind = ScanTargetKind.Website,
            BaseUrl = _baseUri!,
            Authorization = new TargetAuthorization(true),
        });
        var limits = ScanLimits.Default with { MaxCrawlPages = 5, MaxCrawlDepth = 2 };
        var pages = new List<CrawlPage>();
        await foreach (var page in crawler.CrawlAsync(target, limits, TestContext.Current.CancellationToken))
        {
            pages.Add(page);
        }

        Assert.NotEmpty(pages);
        Assert.Contains(pages, p => p.Uri.AbsolutePath == "/" || p.Uri.AbsoluteUri.TrimEnd('/') == _baseUri!.AbsoluteUri.TrimEnd('/'));
    }

    [Fact]
    public async Task Engine_scan_against_loopback_completes()
    {
        await using var provider = new ServiceCollection()
            .AddPien(o =>
            {
                o.AllowPrivateNetworks = true;
                o.AllowedHosts = ["127.0.0.1", "localhost"];
                o.StateDirectory = Path.Combine(Path.GetTempPath(), "pien-int-" + Guid.NewGuid().ToString("N"));
            })
            .BuildServiceProvider();

        var engine = provider.GetRequiredService<IScanEngine>();
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "loop",
            Kind = ScanTargetKind.Website,
            BaseUrl = _baseUri!,
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [target],
            Limits = ScanLimits.Default with { MaxCrawlPages = 3, MaxCrawlDepth = 1 },
            EnabledCheckIds =
            [
                CheckIds.Http001,
                CheckIds.Headers001,
                CheckIds.Html001,
                CheckIds.A11y001,
                CheckIds.Seo001,
            ],
        });
        var run = await engine.RunAsync(
            definition,
            new ScanEngineOptions
            {
                Policy = Policy.Policy.Create(new Policy.Policy { Name = "balanced", Description = "t", FailOnSeverityAtOrAbove = Findings.FindingSeverity.Critical }),
                WorkingDirectory = Directory.GetCurrentDirectory(),
                Network = new NetworkSafetyOptions { AllowPrivateNetworks = true, AllowedHosts = ["127.0.0.1"] },
            },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ScanRunStatus.Completed, run.Status);
    }

    /// <summary>
    /// Sends one GET for <paramref name="path"/> through a loopback-allowed <see cref="SafeHttpTransport"/>
    /// and returns the probe result, so tests can inspect exactly what the transport hands back.
    /// </summary>
    private async Task<ProbeResult> SendLoopbackGetAsync(string path, long? maxResponseBodyBytes = null)
    {
        var network = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1", "localhost"],
        };
        using var transport = new SafeHttpTransport(new DestinationValidator(), network);
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "loop",
            Kind = ScanTargetKind.Website,
            BaseUrl = _baseUri!,
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [target],
            Limits = ScanLimits.Default,
        });
        var context = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
        return await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest
            {
                Uri = new Uri(_baseUri!, path),
                Method = ProbeMethod.Get,
                MaxResponseBodyBytes = maxResponseBodyBytes ?? ScanLimits.Default.BodyInspectionLimitBytes,
            }),
            context,
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Writes a gzip payload of <paramref name="uncompressedLength"/> zero bytes with
    /// <c>Content-Encoding: gzip</c> so AutomaticDecompression expands it before the body bound runs.
    /// </summary>
    private static async Task WriteGzipZerosAsync(HttpContext ctx, long uncompressedLength)
    {
        ctx.Response.ContentType = "application/octet-stream";
        ctx.Response.Headers.ContentEncoding = "gzip";
        await using var gzip = new GZipStream(ctx.Response.Body, CompressionLevel.Optimal, leaveOpen: true);
        var chunk = new byte[64 * 1024];
        var remaining = uncompressedLength;
        while (remaining > 0)
        {
            var write = (int)Math.Min(remaining, chunk.Length);
            await gzip.WriteAsync(chunk.AsMemory(0, write), ctx.RequestAborted);
            remaining -= write;
        }
    }
}
