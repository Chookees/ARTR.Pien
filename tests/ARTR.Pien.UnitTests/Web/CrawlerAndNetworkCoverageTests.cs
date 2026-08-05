using System.Net;
using System.Net.Sockets;
using System.Text;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Web.Crawl;
using ARTR.Pien.Web.Network;

namespace ARTR.Pien.UnitTests.Web;

public sealed class CrawlerAndNetworkCoverageTests
{
    private sealed class MapTransport : ISafeHttpTransport
    {
        private readonly Dictionary<string, Func<ProbeResult>> _map;

        public MapTransport(Dictionary<string, Func<ProbeResult>> map) => _map = map;

        public Task<ProbeResult> SendAsync(ProbeRequest request, ScanContext context, CancellationToken cancellationToken = default)
        {
            var key = request.Uri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
            if (key.Length == 0)
            {
                key = "/";
            }

            if (_map.TryGetValue(key, out var factory) || _map.TryGetValue(request.Uri.AbsoluteUri, out factory))
            {
                return Task.FromResult(factory());
            }

            return Task.FromResult(ProbeResult.Create(new ProbeResult
            {
                FinalUri = request.Uri,
                StatusCode = HttpStatusCode.NotFound,
                Duration = TimeSpan.FromMilliseconds(1),
            }));
        }
    }

    private static ScanTarget Target(string url = "http://127.0.0.1/")
        => ScanTarget.Create(new ScanTarget
        {
            Id = "site",
            Kind = ScanTargetKind.Website,
            BaseUrl = new Uri(url),
            Authorization = new TargetAuthorization(true),
        });

    private static ProbeResult Html(Uri uri, string html, HttpStatusCode status = HttpStatusCode.OK)
        => ProbeResult.Create(new ProbeResult
        {
            FinalUri = uri,
            StatusCode = status,
            ContentType = "text/html",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "text/html" },
            Body = Encoding.UTF8.GetBytes(html),
            Duration = TimeSpan.FromMilliseconds(2),
        });

    [Fact]
    public async Task Crawler_follows_links_and_honors_robots_and_sitemap()
    {
        var baseUri = new Uri("http://127.0.0.1/");
        var transport = new MapTransport(new Dictionary<string, Func<ProbeResult>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/robots.txt"] = () => ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri(baseUri, "/robots.txt"),
                StatusCode = HttpStatusCode.OK,
                Body = Encoding.UTF8.GetBytes("User-agent: *\nDisallow: /private\n"),
                Duration = TimeSpan.FromMilliseconds(1),
            }),
            ["/sitemap.xml"] = () => ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri(baseUri, "/sitemap.xml"),
                StatusCode = HttpStatusCode.OK,
                ContentType = "application/xml",
                Body = Encoding.UTF8.GetBytes("""<?xml version="1.0"?><urlset><url><loc>http://127.0.0.1/from-sitemap</loc></url></urlset>"""),
                Duration = TimeSpan.FromMilliseconds(1),
            }),
            ["/"] = () => Html(baseUri, """<html><body><a href="/about">a</a><a href="/private">p</a><a href="https://evil.example/">x</a></body></html>"""),
            ["/about"] = () => Html(new Uri(baseUri, "/about"), "<html><body>about</body></html>"),
            ["/from-sitemap"] = () => Html(new Uri(baseUri, "/from-sitemap"), "<html><body>map</body></html>"),
        });

        var crawler = new WebsiteCrawler(transport, new NetworkSafetyOptions { AllowPrivateNetworks = true, AllowedHosts = ["127.0.0.1"] });
        var pages = new List<CrawlPage>();
        await foreach (var page in crawler.CrawlAsync(
            Target(),
            ScanLimits.Default with { MaxCrawlPages = 5, MaxCrawlDepth = 2, MaxLinksPerPage = 10 },
            TestContext.Current.CancellationToken))
        {
            pages.Add(page);
        }

        Assert.Contains(pages, p => p.Uri.AbsolutePath is "/" or "");
        Assert.Contains(pages, p => p.Uri.AbsolutePath.Contains("about", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(pages, p => p.Uri.AbsolutePath.Contains("from-sitemap", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(pages, p => p.Uri.AbsolutePath.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ip_classifier_covers_private_and_v6()
    {
        Assert.True(IpAddressClassifier.IsLoopback(IPAddress.Loopback));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("10.0.0.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("172.16.5.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("192.168.1.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("169.254.1.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("224.0.0.1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("::1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("fe80::1")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("fc00::1")));
        Assert.False(IpAddressClassifier.IsRestricted(IPAddress.Parse("8.8.8.8")));
        Assert.True(IpAddressClassifier.IsRestricted(IPAddress.Parse("::ffff:10.0.0.1")));
    }

    [Fact]
    public void Uri_canonicalizer_normalizes_default_ports()
    {
        var http = UriCanonicalizer.Canonicalize(new Uri("http://Example.com:80/a#frag"));
        Assert.Equal("http://example.com/a", http.AbsoluteUri.TrimEnd('/').ToLowerInvariant().Replace(":80", "", StringComparison.Ordinal));
        Assert.Throws<TargetSafetyException>(() => UriCanonicalizer.Canonicalize(new Uri("ftp://example.com/")));
        Assert.Throws<TargetSafetyException>(() => UriCanonicalizer.Canonicalize(new Uri("/relative", UriKind.Relative)));
    }

    [Fact]
    public async Task Destination_validator_allows_allowlisted_loopback()
    {
        var validator = new DestinationValidator();
        var endpoint = await validator.ValidateAsync(
            new Uri("http://127.0.0.1/"),
            new NetworkSafetyOptions { AllowPrivateNetworks = true, AllowedHosts = ["127.0.0.1"] },
            TestContext.Current.CancellationToken);
        Assert.Equal("127.0.0.1", endpoint.Addresses[0].ToString());

        await Assert.ThrowsAsync<TargetSafetyException>(() => validator.ValidateAsync(
            new Uri("http://127.0.0.1/"),
            new NetworkSafetyOptions { AllowPrivateNetworks = false },
            TestContext.Current.CancellationToken));
    }
}
