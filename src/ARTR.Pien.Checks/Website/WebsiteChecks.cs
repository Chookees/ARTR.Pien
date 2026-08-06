using System.Text;
using System.Text.Json;

using AngleSharp.Html.Parser;

using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Text;

namespace ARTR.Pien.Checks.Website;

internal static class CheckHelpers
{
    public static CheckResult Pass(CheckDefinition definition)
        => CheckResult.Create(new CheckResult
        {
            CheckId = definition.Id,
            Status = FindingStatus.Pass,
            Findings = [],
        });

    public static CheckResult NotApplicable(CheckDefinition definition)
        => CheckResult.Create(new CheckResult
        {
            CheckId = definition.Id,
            Status = FindingStatus.NotApplicable,
            Findings = [],
        });

    public static CheckResult Fail(
        CheckDefinition definition,
        ScanContext context,
        string title,
        string detail,
        FindingSeverity severity,
        string? evidence = null,
        string? expected = null,
        string? remediation = null,
        FindingLocation? location = null,
        Uri? resourceUri = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        var excerpts = BuildEvidenceExcerpts(detail, evidence);
        var resolvedLocation = location ?? ResolveLocation(context, resourceUri);
        var resolvedExpected = string.IsNullOrWhiteSpace(expected)
            ? definition.Description
            : expected.Trim();
        var resolvedRemediation = string.IsNullOrWhiteSpace(remediation)
            ? $"Address '{definition.Name}' ({definition.Id.Value}): {definition.Description}"
            : remediation.Trim();

        var finding = Finding.Create(new Finding
        {
            Id = FindingId.NewId(),
            CheckId = definition.Id.Value,
            RuleVersion = definition.RuleVersion,
            Title = title,
            Summary = title,
            Explanation = detail,
            Severity = severity,
            Status = FindingStatus.Fail,
            TargetId = context.Target.Id,
            Location = resolvedLocation,
            Evidence = excerpts,
            Expected = resolvedExpected,
            Observed = detail,
            Remediation = resolvedRemediation,
            Timestamp = context.UtcNow(),
            RunId = context.RunId,
        });

        return CheckResult.Create(new CheckResult
        {
            CheckId = definition.Id,
            Status = FindingStatus.Fail,
            Findings = [finding],
        });
    }

    private static IReadOnlyList<EvidenceExcerpt> BuildEvidenceExcerpts(string detail, string? evidence)
    {
        var excerpts = new List<EvidenceExcerpt>(2);
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            excerpts.Add(EvidenceExcerpt.Create("text/plain", evidence.Trim(), 1024));
        }

        // Always retain the observed detail as bounded evidence so reports are actionable
        // even when a check did not supply a separate excerpt.
        if (excerpts.Count == 0 ||
            !string.Equals(excerpts[0].Text, detail, StringComparison.Ordinal))
        {
            excerpts.Add(EvidenceExcerpt.Create("text/plain", detail.Trim(), 1024));
        }

        return excerpts;
    }

    private static FindingLocation ResolveLocation(ScanContext context, Uri? resourceUri)
    {
        var uri = resourceUri ?? context.Target.BaseUrl;
        return FindingLocation.Create("url", uri.AbsoluteUri);
    }

    public static ProbeResult? Primary(InspectionEvidence evidence)
        => evidence.Probes.TryGetValue("primary", out var primary) ? primary : evidence.Probes.Values.FirstOrDefault();

    public static bool IsHtml(ProbeResult probe)
    {
        var contentType = probe.ContentType ?? (probe.Headers.TryGetValue("Content-Type", out var ct) ? ct : "");
        return contentType.Contains("html", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>HTTP availability fundamentals.</summary>
public sealed class HttpAvailabilityCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Http001),
        Name = "HTTP availability",
        Category = CheckCategory.Reliability,
        DefaultSeverity = FindingSeverity.High,
        Description = "Verifies the primary target returns a successful HTTP status.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary?.StatusCode is null)
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "No HTTP response", "Probe produced no status code.", FindingSeverity.High));
        }

        var code = (int)primary.StatusCode.Value;
        return code is >= 200 and < 400
            ? Task.FromResult(CheckHelpers.Pass(Definition))
            : Task.FromResult(CheckHelpers.Fail(Definition, context, $"HTTP {code}", "Primary response was not successful.", FindingSeverity.High));
    }
}

/// <summary>HTTP redirect chain sanity.</summary>
public sealed class HttpRedirectCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Http002),
        Name = "HTTP redirect chain",
        Category = CheckCategory.Reliability,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Detects redirect loops and excessive redirect chains.",
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

        var chain = primary.RedirectChain;
        if (chain.Count == 0)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var uri in chain)
        {
            if (!seen.Add(uri.AbsoluteUri))
            {
                return Task.FromResult(CheckHelpers.Fail(Definition, context, "Redirect loop", uri.AbsoluteUri, FindingSeverity.Medium));
            }
        }

        if (chain.Count > context.Limits.MaxRedirects)
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Excessive redirects",
                $"Redirect chain length {chain.Count} exceeds limit {context.Limits.MaxRedirects}.",
                FindingSeverity.Medium));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Security headers fundamentals.</summary>
public sealed class SecurityHeadersCheck : ICheck
{
    private static readonly string[] RequiredHeaders =
    [
        "Content-Security-Policy",
        "X-Content-Type-Options",
        "Referrer-Policy",
        "Permissions-Policy",
    ];

    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Headers001),
        Name = "Security headers",
        Category = CheckCategory.HttpSecurity,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Checks for foundational security response headers.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null)
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "Missing probe", "No probe evidence.", FindingSeverity.Medium));
        }

        var missing = RequiredHeaders.Where(h => !primary.Headers.ContainsKey(h)).ToArray();
        return missing.Length == 0
            ? Task.FromResult(CheckHelpers.Pass(Definition))
            : Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Missing security headers",
                "Missing: " + string.Join(", ", missing),
                FindingSeverity.Medium,
                evidence: SummarizeResponseHeaders(primary, missing),
                expected: "Response includes foundational security headers: " + string.Join(", ", RequiredHeaders),
                remediation: "Configure the origin to emit the missing security headers on HTML document responses.",
                location: FindingLocation.Create("header", missing[0]),
                resourceUri: primary.FinalUri));
    }

    private static string SummarizeResponseHeaders(ProbeResult primary, IReadOnlyList<string> missing)
    {
        var present = primary.Headers.Keys
            .Where(k => RequiredHeaders.Contains(k, StringComparer.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        return $"finalUri={primary.FinalUri.AbsoluteUri}; status={(int?)primary.StatusCode}; present=[{string.Join(", ", present)}]; missing=[{string.Join(", ", missing)}]";
    }
}

/// <summary>CSP fundamentals.</summary>
public sealed class ContentSecurityPolicyCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Headers002),
        Name = "Content-Security-Policy",
        Category = CheckCategory.HttpSecurity,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Requires a Content-Security-Policy header and flags unsafe-inline script policies.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || !primary.Headers.TryGetValue("Content-Security-Policy", out var csp) || string.IsNullOrWhiteSpace(csp))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "CSP missing",
                "Content-Security-Policy header was not present.",
                FindingSeverity.Medium,
                expected: "A Content-Security-Policy response header is present and enforced.",
                remediation: "Add a Content-Security-Policy header that restricts script and resource origins appropriately.",
                location: FindingLocation.Create("header", "Content-Security-Policy"),
                resourceUri: primary?.FinalUri ?? context.Target.BaseUrl));
        }

        if (csp.Contains("unsafe-inline", StringComparison.OrdinalIgnoreCase) &&
            csp.Contains("script-src", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "CSP allows unsafe-inline",
                csp,
                FindingSeverity.Low,
                evidence: csp,
                expected: "script-src does not allow unsafe-inline.",
                remediation: "Remove 'unsafe-inline' from script-src and use nonces or hashes instead.",
                location: FindingLocation.Create("header", "Content-Security-Policy"),
                resourceUri: primary.FinalUri));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>HTML structure fundamentals.</summary>
public sealed class HtmlStructureCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Html001),
        Name = "HTML structure",
        Category = CheckCategory.ContentQuality,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Requires html/head/title/body fundamentals when HTML is returned.",
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

        var parser = new HtmlParser();
        var document = parser.ParseDocument(Encoding.UTF8.GetString(primary.Body.Span));
        var ok = document.DocumentElement is not null &&
                 document.Head is not null &&
                 document.Title is { Length: > 0 } &&
                 document.Body is not null;
        return ok
            ? Task.FromResult(CheckHelpers.Pass(Definition))
            : Task.FromResult(CheckHelpers.Fail(Definition, context, "Incomplete HTML structure", "Missing html/title/body fundamentals.", FindingSeverity.Low));
    }
}

/// <summary>Accessibility fundamentals.</summary>
public sealed class AccessibilityFundamentalsCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.A11y001),
        Name = "Accessibility fundamentals",
        Category = CheckCategory.Accessibility,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Checks lang, title, and image alt fundamentals on HTML responses.",
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

        var parser = new HtmlParser();
        var document = parser.ParseDocument(Encoding.UTF8.GetString(primary.Body.Span));
        var hasLang = !string.IsNullOrWhiteSpace(document.DocumentElement?.GetAttribute("lang"));
        var hasTitle = !string.IsNullOrWhiteSpace(document.Title);
        var missingAlt = document.Images.Any(img => !img.HasAttribute("alt"));
        if (hasLang && hasTitle && !missingAlt)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var detail = $"lang={hasLang}, title={hasTitle}, missingAlt={missingAlt}";
        return Task.FromResult(CheckHelpers.Fail(Definition, context, "Missing a11y fundamentals", detail, FindingSeverity.Low));
    }
}

/// <summary>Cookie attribute fundamentals.</summary>
public sealed class CookieAttributeCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Cookie001),
        Name = "Cookie attributes",
        Category = CheckCategory.Cookies,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "When Set-Cookie is present, expects Secure, HttpOnly, and SameSite attributes.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || !primary.Headers.TryGetValue("Set-Cookie", out var cookie))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var ok = cookie.Contains("Secure", StringComparison.OrdinalIgnoreCase) &&
                 cookie.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase) &&
                 cookie.Contains("SameSite", StringComparison.OrdinalIgnoreCase);
        return ok
            ? Task.FromResult(CheckHelpers.Pass(Definition))
            : Task.FromResult(CheckHelpers.Fail(Definition, context, "Weak cookie attributes", "Set-Cookie missing Secure, HttpOnly, and/or SameSite.", FindingSeverity.Medium));
    }
}

/// <summary>SEO / discoverability fundamentals.</summary>
public sealed class SeoFundamentalsCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Seo001),
        Name = "SEO fundamentals",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Requires title and meta description on HTML responses.",
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

        var parser = new HtmlParser();
        var document = parser.ParseDocument(Encoding.UTF8.GetString(primary.Body.Span));
        var title = document.Title;
        var description = document.Head?.QuerySelector("meta[name='description']")?.GetAttribute("content");
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(description))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        return Task.FromResult(CheckHelpers.Fail(Definition, context, "Missing SEO fundamentals", "Expected title and meta description.", FindingSeverity.Low));
    }
}

/// <summary>Broken or unsafe link detection.</summary>
public sealed class LinkSafetyCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Link001),
        Name = "Link safety",
        Category = CheckCategory.Discoverability,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Detects javascript: links and broken crawled page probes.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is not null && CheckHelpers.IsHtml(primary))
        {
            var html = Encoding.UTF8.GetString(primary.Body.Span);
            if (SafeRegex.IsMatch(html, @"href\s*=\s*[""']javascript:", context.Limits, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                return Task.FromResult(CheckHelpers.Fail(Definition, context, "Unsafe javascript: link", "HTML contains javascript: href.", FindingSeverity.Medium));
            }
        }

        foreach (var page in evidence.CrawledPages)
        {
            if (page.Probe?.StatusCode is { } code && (int)code >= 400)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Broken crawled link",
                    $"{page.Page.Uri} returned {(int)code}.",
                    FindingSeverity.Medium));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Performance budget fundamentals.</summary>
public sealed class PerformanceBudgetCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Perf001),
        Name = "Performance budget",
        Category = CheckCategory.Performance,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags primary responses slower than 3 seconds or larger than 2 MiB inspected body.",
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

        if (primary.Duration > TimeSpan.FromSeconds(3))
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "Slow response", $"Duration {primary.Duration.TotalMilliseconds:F0}ms exceeded 3000ms.", FindingSeverity.Low));
        }

        if (primary.Body.Length > 2 * 1024 * 1024)
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "Large response body", $"Body size {primary.Body.Length} bytes exceeded 2 MiB budget.", FindingSeverity.Low));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>TLS protocol and certificate fundamentals.</summary>
public sealed class TlsFundamentalsCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Tls001),
        Name = "TLS fundamentals",
        Category = CheckCategory.TransportSecurity,
        DefaultSeverity = FindingSeverity.High,
        Description = "Requires a successful TLS handshake and certificate for https targets.",
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

        if (evidence.Probes.ContainsKey("tls-error"))
        {
            var message = evidence.Probes["tls-error"].ErrorMessage ?? "TLS probe failed.";
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "TLS probe failed", message, FindingSeverity.High));
        }

        if (evidence.Tls is null || string.IsNullOrWhiteSpace(evidence.Tls.CertificateFingerprintSha256))
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "TLS certificate missing", "No TLS certificate fingerprint observed.", FindingSeverity.High));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>TLS certificate expiration proximity.</summary>
public sealed class TlsExpirationCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Tls002),
        Name = "TLS certificate expiration",
        Category = CheckCategory.TransportSecurity,
        DefaultSeverity = FindingSeverity.High,
        Description = "Fails when the certificate is expired or expires within 14 days.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (evidence.Tls?.CertificateNotAfter is not DateTimeOffset notAfter)
        {
            return Task.FromResult(CheckHelpers.NotApplicable(Definition));
        }

        var remaining = notAfter - context.UtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "Certificate expired", $"NotAfter {notAfter:O}", FindingSeverity.Critical));
        }

        if (remaining < TimeSpan.FromDays(14))
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "Certificate expiring soon", $"Expires in {remaining.TotalDays:F1} days.", FindingSeverity.High));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}
