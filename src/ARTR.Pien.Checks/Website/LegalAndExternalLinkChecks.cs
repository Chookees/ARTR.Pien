using System.Text;

using AngleSharp.Dom;
using AngleSharp.Html.Parser;

using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Checks.Website;

/// <summary>
/// Verifies that an Impressum / legal-notice link is discoverable.
/// This is a heuristic discoverability check — not legal advice or TMG/DDG certification.
/// </summary>
public sealed class LegalImpressumCheck : ICheck
{
    private static readonly string[] HrefHints =
    [
        "impressum", "imprint", "legal-notice", "legal_notice", "legalnotice", "anbieterkennzeichnung",
    ];

    private static readonly string[] TextHints =
    [
        "impressum", "imprint", "legal notice", "legal information", "anbieterkennzeichnung",
    ];

    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Legal001),
        Name = "Impressum / legal notice link",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Requires a discoverable Impressum/imprint/legal-notice link. Not a legal compliance certification.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LegalLinkEvaluator.Evaluate(
            Definition,
            context,
            evidence,
            HrefHints,
            TextHints,
            missingTitle: "Impressum link missing",
            missingDetail: "No Impressum/imprint/legal-notice link was found in inspected HTML.",
            brokenTitle: "Impressum link broken",
            expected: "A reachable Impressum/imprint/legal-notice link is present for operator-facing sites.",
            remediation: "Add a clearly labeled Impressum (or equivalent legal-notice) link and ensure the target returns HTTP 2xx."));
    }
}

/// <summary>
/// Verifies that a privacy policy / Datenschutzerklärung link is discoverable.
/// Heuristic only — not a GDPR/DSGVO certification.
/// </summary>
public sealed class LegalPrivacyPolicyCheck : ICheck
{
    private static readonly string[] HrefHints =
    [
        "datenschutz", "datenschutzerklaerung", "datenschutzerklärung", "privacy", "privacy-policy", "privacy_policy", "privacypolicy",
    ];

    private static readonly string[] TextHints =
    [
        "datenschutz", "datenschutzerklärung", "datenschutzerklaerung", "privacy policy", "privacy", "privacy notice",
    ];

    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Legal002),
        Name = "Privacy / Datenschutz link",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Requires a discoverable privacy-policy / Datenschutzerklärung link. Not a GDPR certification.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LegalLinkEvaluator.Evaluate(
            Definition,
            context,
            evidence,
            HrefHints,
            TextHints,
            missingTitle: "Privacy policy link missing",
            missingDetail: "No Datenschutz/privacy-policy link was found in inspected HTML.",
            brokenTitle: "Privacy policy link broken",
            expected: "A reachable Datenschutz/privacy-policy link is present.",
            remediation: "Add a clearly labeled Datenschutzerklärung (or privacy policy) link and ensure the target returns HTTP 2xx."));
    }
}

/// <summary>Fails when externally probed links return HTTP 4xx/5xx.</summary>
public sealed class LinkExternalBrokenCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Link004),
        Name = "External broken links",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Fails when crawl.external link probes (checkExternalLinks) return HTTP 4xx/5xx. Requires checkExternalLinks=true.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.Limits.CheckExternalLinks)
        {
            return Task.FromResult(CheckHelpers.NotApplicable(Definition));
        }

        var origin = context.Target.BaseUrl;
        foreach (var page in evidence.CrawledPages)
        {
            if (string.Equals(page.Page.Uri.Host, origin.Host, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (page.Probe?.StatusCode is { } code && (int)code >= 400)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Broken external link",
                    $"{page.Page.Uri} returned {(int)code}.",
                    FindingSeverity.Low,
                    evidence: $"referrer={page.Page.Referrer}; status={(int)code}",
                    expected: "External links probed during the crawl return HTTP status below 400.",
                    remediation: "Fix or remove the broken external link, or exclude it from the site navigation.",
                    location: FindingLocation.Create("url", page.Page.Uri.AbsoluteUri),
                    resourceUri: page.Page.Uri));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

internal static class LegalLinkEvaluator
{
    public static CheckResult Evaluate(
        CheckDefinition definition,
        ScanContext context,
        InspectionEvidence evidence,
        IReadOnlyList<string> hrefHints,
        IReadOnlyList<string> textHints,
        string missingTitle,
        string missingDetail,
        string brokenTitle,
        string expected,
        string remediation)
    {
        var matches = FindMatches(evidence, hrefHints, textHints).ToArray();
        if (matches.Length == 0)
        {
            return CheckHelpers.Fail(
                definition,
                context,
                missingTitle,
                missingDetail,
                FindingSeverity.Medium,
                expected: expected,
                remediation: remediation,
                location: FindingLocation.Create("url", context.Target.BaseUrl.AbsoluteUri),
                resourceUri: context.Target.BaseUrl);
        }

        foreach (var match in matches)
        {
            var probed = evidence.CrawledPages
                .FirstOrDefault(p => string.Equals(p.Page.Uri.AbsoluteUri, match.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
            if (probed?.Probe?.StatusCode is { } code && (int)code >= 400)
            {
                return CheckHelpers.Fail(
                    definition,
                    context,
                    brokenTitle,
                    $"{match.AbsoluteUri} returned {(int)code}.",
                    FindingSeverity.Medium,
                    evidence: $"matchedUrl={match.AbsoluteUri}; status={(int)code}",
                    expected: expected,
                    remediation: remediation,
                    location: FindingLocation.Create("url", match.AbsoluteUri),
                    resourceUri: match);
            }
        }

        return CheckHelpers.Pass(definition);
    }

    private static IEnumerable<Uri> FindMatches(
        InspectionEvidence evidence,
        IReadOnlyList<string> hrefHints,
        IReadOnlyList<string> textHints)
    {
        var bodies = new List<(Uri Base, string Html)>();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is not null && CheckHelpers.IsHtml(primary))
        {
            bodies.Add((primary.FinalUri, Encoding.UTF8.GetString(primary.Body.Span)));
        }

        foreach (var page in evidence.CrawledPages)
        {
            if (page.Probe is null || !CheckHelpers.IsHtml(page.Probe))
            {
                continue;
            }

            bodies.Add((page.Page.Uri, Encoding.UTF8.GetString(page.Probe.Body.Span)));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (baseUri, html) in bodies)
        {
            IDocument document;
            try
            {
                document = new HtmlParser().ParseDocument(html);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Malformed HTML is skipped; AngleSharp usually recovers, but keep the check bounded.
                continue;
            }

            foreach (var anchor in document.Links)
            {
                var href = anchor.GetAttribute("href") ?? string.Empty;
                var text = (anchor.TextContent ?? string.Empty).Trim();
                if (!LooksLike(href, hrefHints) && !LooksLike(text, textHints))
                {
                    continue;
                }

                if (!Uri.TryCreate(baseUri, href, out var absolute) || absolute.Scheme is not ("http" or "https"))
                {
                    continue;
                }

                if (seen.Add(absolute.AbsoluteUri))
                {
                    yield return absolute;
                }
            }
        }
    }

    private static bool LooksLike(string value, IReadOnlyList<string> hints)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        // Bound input length before regex-less substring scans.
        if (normalized.Length > 512)
        {
            normalized = normalized[..512];
        }

        return hints.Any(h => normalized.Contains(h, StringComparison.Ordinal));
    }
}
