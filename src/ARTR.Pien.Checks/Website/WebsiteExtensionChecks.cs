using System.Text;

using AngleSharp.Html.Parser;

using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Checks.Website;

/// <summary>Weak / obsolete TLS protocol observation.</summary>
public sealed class TlsProtocolStrengthCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Tls003),
        Name = "TLS protocol strength",
        Category = CheckCategory.TransportSecurity,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Flags obsolete TLS protocol negotiations (below TLS 1.2).",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(context.Target.BaseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.NotApplicable(Definition));
        }

        if (evidence.Tls is null || string.IsNullOrWhiteSpace(evidence.Tls.Protocol))
        {
            return Task.FromResult(CheckHelpers.NotApplicable(Definition));
        }

        var protocol = evidence.Tls.Protocol;
        if (protocol.Contains("Ssl", StringComparison.OrdinalIgnoreCase) ||
            protocol.Contains("Tls10", StringComparison.OrdinalIgnoreCase) ||
            protocol.Contains("Tls11", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(protocol, "Tls", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Obsolete TLS protocol",
                $"Negotiated protocol '{protocol}' is below TLS 1.2.",
                FindingSeverity.Medium));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Title / description length budgets.</summary>
public sealed class SeoMetadataBudgetCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Seo002),
        Name = "SEO metadata budgets",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags HTML title longer than 60 characters or meta description longer than 160 characters.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || !CheckHelpers.IsHtml(primary))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var document = new HtmlParser().ParseDocument(Encoding.UTF8.GetString(primary.Body.Span));
        var title = document.Title ?? string.Empty;
        var description = document.Head?.QuerySelector("meta[name='description']")?.GetAttribute("content") ?? string.Empty;
        if (title.Length > 60 || description.Length > 160)
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "SEO metadata budget exceeded",
                $"titleLength={title.Length}, descriptionLength={description.Length}",
                FindingSeverity.Low));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Canonical / indexability conflicts.</summary>
public sealed class SeoIndexabilityCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Seo003),
        Name = "SEO indexability",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags conflicting noindex robots directives with a canonical link.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || !CheckHelpers.IsHtml(primary))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var document = new HtmlParser().ParseDocument(Encoding.UTF8.GetString(primary.Body.Span));
        var robots = document.Head?.QuerySelector("meta[name='robots']")?.GetAttribute("content") ?? string.Empty;
        var hasCanonical = document.Head?.QuerySelector("link[rel='canonical']") is not null;
        if (hasCanonical && robots.Contains("noindex", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Canonical conflicts with noindex",
                "Page declares both canonical and robots noindex.",
                FindingSeverity.Low));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>robots.txt / sitemap reachability.</summary>
public sealed class SeoRobotsSitemapCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Seo004),
        Name = "SEO robots/sitemap reachability",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Info,
        Description = "Advises when robots.txt was not observed among probes or crawled pages.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Target.Kind != ScanTargetKind.Website)
        {
            return Task.FromResult(CheckHelpers.NotApplicable(Definition));
        }

        if (evidence.Probes.ContainsKey("robots") ||
            evidence.CrawledPages.Any(p => p.Page.Uri.AbsolutePath.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        return Task.FromResult(CheckHelpers.Fail(
            Definition,
            context,
            "robots.txt not observed",
            "No robots.txt probe or crawled page was present in evidence.",
            FindingSeverity.Info));
    }
}

/// <summary>Broken internal links observed during crawl.</summary>
public sealed class LinkInternalCrawlCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Link002),
        Name = "Internal crawl links",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Fails when same-origin crawled pages return HTTP 4xx/5xx.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var origin = context.Target.BaseUrl;
        foreach (var page in evidence.CrawledPages)
        {
            if (!string.Equals(page.Page.Uri.Host, origin.Host, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (page.Probe?.StatusCode is { } code && (int)code >= 400)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Broken internal link",
                    $"{page.Page.Uri} returned {(int)code}.",
                    FindingSeverity.Medium));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Missing link text.</summary>
public sealed class LinkTextCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Link003),
        Name = "Link text",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags anchor elements that have an href but empty visible text.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || !CheckHelpers.IsHtml(primary))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var document = new HtmlParser().ParseDocument(Encoding.UTF8.GetString(primary.Body.Span));
        foreach (var anchor in document.Links)
        {
            if (string.IsNullOrWhiteSpace(anchor.GetAttribute("href")))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(anchor.TextContent))
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Missing link text",
                    $"Anchor href '{anchor.GetAttribute("href")}' has empty text.",
                    FindingSeverity.Low));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>TTFB / total duration budgets.</summary>
public sealed class PerformanceTimingBudgetCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Perf002),
        Name = "Performance timing budget",
        Category = CheckCategory.Performance,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags primary responses slower than 1 second total duration.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        if (primary.Duration > TimeSpan.FromSeconds(1))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Timing budget exceeded",
                $"Duration {primary.Duration.TotalMilliseconds:F0}ms exceeded 1000ms.",
                FindingSeverity.Low));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Body size budget (advisory).</summary>
public sealed class PerformanceBodySizeBudgetCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Perf003),
        Name = "Performance body size budget",
        Category = CheckCategory.Performance,
        DefaultSeverity = FindingSeverity.Info,
        Description = "Advises when the inspected primary body exceeds 512 KiB.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        const int budget = 512 * 1024;
        if (primary.Body.Length > budget)
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Body size budget exceeded",
                $"Body size {primary.Body.Length} bytes exceeded {budget} bytes.",
                FindingSeverity.Info));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Volatile content fingerprint drift advisory.</summary>
public sealed class BaselineVolatilityCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Change002),
        Name = "Baseline volatility note",
        Category = CheckCategory.ChangeStability,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Advises when a baseline is present without a content fingerprint while the run observed one.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (evidence.Baseline is null)
        {
            return Task.FromResult(CheckHelpers.NotApplicable(Definition));
        }

        if (string.IsNullOrWhiteSpace(evidence.Baseline.ContentFingerprint) &&
            !string.IsNullOrWhiteSpace(evidence.ContentFingerprint))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Baseline missing content fingerprint",
                "Run observed a content fingerprint but the baseline does not track one.",
                FindingSeverity.Low));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}
