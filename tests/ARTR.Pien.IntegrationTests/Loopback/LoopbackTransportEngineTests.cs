using System.Net;
using System.Text;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Engine;
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
}
