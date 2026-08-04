using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using AngleSharp.Html.Parser;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Web.Crawl;

/// <summary>
/// Deterministic iterative same-origin crawler with robots.txt and sitemap support.
/// </summary>
public sealed class WebsiteCrawler : ICrawler
{
    private readonly ISafeHttpTransport _transport;
    private readonly NetworkSafetyOptions _networkOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsiteCrawler"/> class.
    /// </summary>
    public WebsiteCrawler(ISafeHttpTransport transport, NetworkSafetyOptions networkOptions)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _networkOptions = networkOptions ?? throw new ArgumentNullException(nameof(networkOptions));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CrawlPage> CrawlAsync(
        ScanTarget target,
        ScanLimits limits,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(limits);
        ScanTarget.Create(target);

        var context = new ScanContext(
            ScanRunId.NewId(),
            ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = ScanProfileNames.Standard,
                Targets = [target],
                Limits = limits,
            }),
            limits,
            target,
            static () => DateTimeOffset.UtcNow);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<CrawlPage>();
        var robots = await LoadRobotsAsync(target, context, cancellationToken).ConfigureAwait(false);
        Enqueue(queue, visited, target.BaseUrl, 0, null, target.BaseUrl, limits, robots);

        if (robots.UseSitemap)
        {
            foreach (var seed in await LoadSitemapSeedsAsync(target, context, limits, cancellationToken).ConfigureAwait(false))
            {
                Enqueue(queue, visited, seed, 0, target.BaseUrl, target.BaseUrl, limits, robots);
            }
        }

        var pages = 0;
        while (queue.Count > 0 && pages < limits.MaxCrawlPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = queue.Dequeue();
            pages++;
            yield return page;

            if (page.Depth >= limits.MaxCrawlDepth)
            {
                continue;
            }

            ProbeResult result;
            try
            {
                result = await _transport.SendAsync(
                    ProbeRequest.Create(new ProbeRequest
                    {
                        Uri = page.Uri,
                        Method = ProbeMethod.Get,
                        MaxResponseBodyBytes = limits.BodyInspectionLimitBytes,
                    }),
                    context,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            if (result.StatusCode is null || (int)result.StatusCode >= 400)
            {
                continue;
            }

            var html = Encoding.UTF8.GetString(result.Body.Span);
            foreach (var link in ExtractLinks(html, page.Uri, limits.MaxLinksPerPage))
            {
                Enqueue(queue, visited, link, page.Depth + 1, page.Uri, target.BaseUrl, limits, robots);
            }
        }
    }

    private async Task<RobotsRules> LoadRobotsAsync(ScanTarget target, ScanContext context, CancellationToken cancellationToken)
    {
        try
        {
            var robotsUri = new Uri(target.BaseUrl, "/robots.txt");
            var result = await _transport.SendAsync(
                ProbeRequest.Create(new ProbeRequest
                {
                    Uri = robotsUri,
                    Method = ProbeMethod.Get,
                    MaxResponseBodyBytes = 64 * 1024,
                }),
                context,
                cancellationToken).ConfigureAwait(false);
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return RobotsRules.AllowAll;
            }

            return RobotsRules.Parse(Encoding.UTF8.GetString(result.Body.Span));
        }
        catch
        {
            return RobotsRules.AllowAll;
        }
    }

    private async Task<IReadOnlyList<Uri>> LoadSitemapSeedsAsync(
        ScanTarget target,
        ScanContext context,
        ScanLimits limits,
        CancellationToken cancellationToken)
    {
        var seeds = new List<Uri>();
        try
        {
            var sitemapUri = new Uri(target.BaseUrl, "/sitemap.xml");
            var result = await _transport.SendAsync(
                ProbeRequest.Create(new ProbeRequest
                {
                    Uri = sitemapUri,
                    Method = ProbeMethod.Get,
                    MaxResponseBodyBytes = Math.Min(limits.BodyInspectionLimitBytes, 1024 * 1024),
                }),
                context,
                cancellationToken).ConfigureAwait(false);
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return seeds;
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,
                Async = true,
            };
            await using var stream = new MemoryStream(result.Body.ToArray());
            using var reader = XmlReader.Create(stream, settings);
            var count = 0;
            while (await reader.ReadAsync().ConfigureAwait(false) && count < limits.MaxCrawlPages)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name is "loc" or "xhtml:link")
                {
                    var value = await reader.ReadElementContentAsStringAsync().ConfigureAwait(false);
                    if (Uri.TryCreate(value, UriKind.Absolute, out var loc))
                    {
                        seeds.Add(loc);
                        count++;
                    }
                }
            }
        }
        catch
        {
            // Sitemap is optional.
        }

        return seeds;
    }

    private static IEnumerable<Uri> ExtractLinks(string html, Uri baseUri, int maxLinks)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var count = 0;
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            if (count >= maxLinks)
            {
                yield break;
            }

            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, href, out var absolute))
            {
                continue;
            }

            if (absolute.Scheme is not ("http" or "https"))
            {
                continue;
            }

            count++;
            yield return absolute;
        }
    }

    private static void Enqueue(
        Queue<CrawlPage> queue,
        HashSet<string> visited,
        Uri uri,
        int depth,
        Uri? referrer,
        Uri origin,
        ScanLimits limits,
        RobotsRules robots)
    {
        if (depth > limits.MaxCrawlDepth)
        {
            return;
        }

        if (!string.Equals(uri.Host, origin.Host, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var key = uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
        if (!visited.Add(key))
        {
            return;
        }

        if (!robots.IsAllowed(uri.AbsolutePath))
        {
            return;
        }

        queue.Enqueue(new CrawlPage(uri, depth, referrer));
    }

    private sealed class RobotsRules
    {
        private readonly List<string> _disallow = [];
        public bool UseSitemap { get; private set; } = true;

        public static RobotsRules AllowAll { get; } = new();

        public static RobotsRules Parse(string text)
        {
            var rules = new RobotsRules();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal) || line.Length == 0)
                {
                    continue;
                }

                var parts = line.Split(':', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim();
                if (key.Equals("Disallow", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                {
                    rules._disallow.Add(value);
                }
            }

            return rules;
        }

        public bool IsAllowed(string path)
            => !_disallow.Any(d => path.StartsWith(d, StringComparison.OrdinalIgnoreCase));
    }
}
