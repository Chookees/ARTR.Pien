using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;

using AngleSharp.Html.Parser;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Web.Crawl;

/// <summary>
/// Deterministic iterative website crawler with robots.txt, sitemap, and optional external link probing.
/// </summary>
/// <remarks>
/// Same-origin expansion is the default. When <see cref="ScanLimits.CheckExternalLinks"/> is enabled,
/// external http(s) links discovered on same-origin pages are enqueued as leaf probes (no further expansion).
/// </remarks>
public sealed class WebsiteCrawler : ICrawler
{
    private readonly ISafeHttpTransport _transport;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsiteCrawler"/> class.
    /// </summary>
    public WebsiteCrawler(ISafeHttpTransport transport, NetworkSafetyOptions networkOptions)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        ArgumentNullException.ThrowIfNull(networkOptions);
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

        var state = await CreateStateAsync(target, limits, cancellationToken).ConfigureAwait(false);
        Enqueue(state, target.BaseUrl, 0, null, target.BaseUrl, limits, isExternalLeaf: false);
        if (limits.UseSitemap)
        {
            foreach (var seed in await LoadSitemapSeedsAsync(target, state.Context, limits, cancellationToken).ConfigureAwait(false))
            {
                Enqueue(state, seed, 0, target.BaseUrl, target.BaseUrl, limits, isExternalLeaf: false);
            }
        }

        var pages = 0;
        while (state.Queue.Count > 0 && pages < limits.MaxCrawlPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = state.Queue.Dequeue();
            pages++;
            yield return page;
            await ExpandPageAsync(page, target, limits, state, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<CrawlState> CreateStateAsync(ScanTarget target, ScanLimits limits, CancellationToken cancellationToken)
    {
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

        return new CrawlState
        {
            Context = context,
            Robots = limits.RespectRobotsTxt
                ? await LoadRobotsAsync(target, context, cancellationToken).ConfigureAwait(false)
                : RobotsRules.AllowAll,
        };
    }

    private async Task ExpandPageAsync(
        CrawlPage page,
        ScanTarget target,
        ScanLimits limits,
        CrawlState state,
        CancellationToken cancellationToken)
    {
        if (page.Depth >= limits.MaxCrawlDepth ||
            !string.Equals(page.Uri.Host, target.BaseUrl.Host, StringComparison.OrdinalIgnoreCase))
        {
            return;
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
                state.Context,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return;
        }

        if (result.StatusCode is null || (int)result.StatusCode >= 400)
        {
            return;
        }

        foreach (var link in ExtractLinks(Encoding.UTF8.GetString(result.Body.Span), page.Uri, limits.MaxLinksPerPage))
        {
            var sameHost = string.Equals(link.Host, target.BaseUrl.Host, StringComparison.OrdinalIgnoreCase);
            if (!sameHost && (!limits.CheckExternalLinks || state.ExternalProbed >= limits.MaxExternalLinks))
            {
                continue;
            }

            Enqueue(
                state,
                link,
                depth: sameHost ? page.Depth + 1 : limits.MaxCrawlDepth,
                referrer: page.Uri,
                origin: target.BaseUrl,
                limits,
                isExternalLeaf: !sameHost);
        }
    }

    private async Task<RobotsRules> LoadRobotsAsync(ScanTarget target, ScanContext context, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _transport.SendAsync(
                ProbeRequest.Create(new ProbeRequest
                {
                    Uri = new Uri(target.BaseUrl, "/robots.txt"),
                    Method = ProbeMethod.Get,
                    MaxResponseBodyBytes = 64 * 1024,
                }),
                context,
                cancellationToken).ConfigureAwait(false);
            if (result.StatusCode is null || (int)result.StatusCode >= 400)
            {
                return RobotsRules.AllowAll;
            }

            return RobotsRules.Parse(Encoding.UTF8.GetString(result.Body.Span));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        try
        {
            var result = await _transport.SendAsync(
                ProbeRequest.Create(new ProbeRequest
                {
                    Uri = new Uri(target.BaseUrl, "/sitemap.xml"),
                    Method = ProbeMethod.Get,
                    MaxResponseBodyBytes = 256 * 1024,
                }),
                context,
                cancellationToken).ConfigureAwait(false);
            if (result.StatusCode is null || (int)result.StatusCode >= 400)
            {
                return [];
            }

            return ParseSitemapUrls(Encoding.UTF8.GetString(result.Body.Span), target.BaseUrl, limits.MaxCrawlPages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private static IReadOnlyList<Uri> ParseSitemapUrls(string xml, Uri origin, int max)
    {
        var list = new List<Uri>();
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,
            };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read() && list.Count < max)
            {
                if (reader.NodeType != XmlNodeType.Element ||
                    !string.Equals(reader.LocalName, "loc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = reader.ReadElementContentAsString();
                if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                    uri.Scheme is "http" or "https" &&
                    string.Equals(uri.Host, origin.Host, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(uri);
                }
            }
        }
        catch (XmlException)
        {
            return list;
        }

        return list;
    }

    private static IEnumerable<Uri> ExtractLinks(string html, Uri baseUri, int maxLinks)
    {
        var document = new HtmlParser().ParseDocument(html);
        var count = 0;
        foreach (var anchor in document.Links)
        {
            if (count >= maxLinks)
            {
                yield break;
            }

            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(baseUri, href, out var absolute))
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
        CrawlState state,
        Uri uri,
        int depth,
        Uri? referrer,
        Uri origin,
        ScanLimits limits,
        bool isExternalLeaf)
    {
        if (depth > limits.MaxCrawlDepth)
        {
            return;
        }

        var sameHost = string.Equals(uri.Host, origin.Host, StringComparison.OrdinalIgnoreCase);
        if (!sameHost && (!limits.CheckExternalLinks || !isExternalLeaf || state.ExternalProbed >= limits.MaxExternalLinks))
        {
            return;
        }

        var key = uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
        if (!state.Visited.Add(key))
        {
            return;
        }

        if (sameHost && limits.RespectRobotsTxt && !state.Robots.IsAllowed(uri.AbsolutePath))
        {
            return;
        }

        if (!sameHost)
        {
            state.ExternalProbed++;
        }

        state.Queue.Enqueue(new CrawlPage(uri, depth, referrer));
    }

    private sealed class CrawlState
    {
        public HashSet<string> Visited { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Queue<CrawlPage> Queue { get; } = new();

        public int ExternalProbed { get; set; }

        public required RobotsRules Robots { get; init; }

        public required ScanContext Context { get; init; }
    }

    private sealed class RobotsRules
    {
        private readonly List<string> _disallow = [];

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

                if (parts[0].Trim().Equals("Disallow", StringComparison.OrdinalIgnoreCase) && parts[1].Trim().Length > 0)
                {
                    rules._disallow.Add(parts[1].Trim());
                }
            }

            return rules;
        }

        public bool IsAllowed(string path)
            => !_disallow.Any(d => path.StartsWith(d, StringComparison.OrdinalIgnoreCase));
    }
}
