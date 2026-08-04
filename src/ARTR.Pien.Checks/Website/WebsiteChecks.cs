using System.Text;
using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

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

    public static CheckResult Fail(
        CheckDefinition definition,
        ScanContext context,
        string title,
        string detail,
        FindingSeverity severity,
        string? evidence = null)
    {
        var excerpts = new List<EvidenceExcerpt>();
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            excerpts.Add(EvidenceExcerpt.Create("text/plain", evidence, 512));
        }

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
            Evidence = excerpts,
            Timestamp = context.UtcNow(),
            RunId = context.RunId,
            Observed = detail,
        });

        return CheckResult.Create(new CheckResult
        {
            CheckId = definition.Id,
            Status = FindingStatus.Fail,
            Findings = [finding],
        });
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
        var primary = evidence.Probes.Values.FirstOrDefault();
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
        var primary = evidence.Probes.Values.FirstOrDefault();
        if (primary is null)
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "Missing probe", "No probe evidence.", FindingSeverity.Medium));
        }

        var missing = RequiredHeaders.Where(h => !primary.Headers.ContainsKey(h)).ToArray();
        return missing.Length == 0
            ? Task.FromResult(CheckHelpers.Pass(Definition))
            : Task.FromResult(CheckHelpers.Fail(Definition, context, "Missing security headers", "Missing: " + string.Join(", ", missing), FindingSeverity.Medium));
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
        Description = "Requires a Content-Security-Policy header.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = evidence.Probes.Values.FirstOrDefault();
        if (primary is null || !primary.Headers.TryGetValue("Content-Security-Policy", out var csp) || string.IsNullOrWhiteSpace(csp))
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "CSP missing", "Content-Security-Policy header was not present.", FindingSeverity.Medium));
        }

        if (csp.Contains("unsafe-inline", StringComparison.OrdinalIgnoreCase) &&
            csp.Contains("script-src", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Fail(Definition, context, "CSP allows unsafe-inline", csp, FindingSeverity.Low, csp));
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
        var primary = evidence.Probes.Values.FirstOrDefault();
        if (primary is null)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var contentType = primary.ContentType ?? (primary.Headers.TryGetValue("Content-Type", out var ct) ? ct : "");
        if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var html = Encoding.UTF8.GetString(primary.Body.Span);
        if (html.Contains("<html", StringComparison.OrdinalIgnoreCase) &&
            html.Contains("<title", StringComparison.OrdinalIgnoreCase) &&
            html.Contains("<body", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        return Task.FromResult(CheckHelpers.Fail(Definition, context, "Incomplete HTML structure", "Missing html/title/body fundamentals.", FindingSeverity.Low));
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
        Description = "Checks lang and title fundamentals on HTML responses.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = evidence.Probes.Values.FirstOrDefault();
        if (primary is null)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var html = Encoding.UTF8.GetString(primary.Body.Span);
        var hasLang = html.Contains("lang=", StringComparison.OrdinalIgnoreCase);
        var hasTitle = html.Contains("<title", StringComparison.OrdinalIgnoreCase);
        return hasLang && hasTitle
            ? Task.FromResult(CheckHelpers.Pass(Definition))
            : Task.FromResult(CheckHelpers.Fail(Definition, context, "Missing a11y fundamentals", "Expected lang and title.", FindingSeverity.Low));
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
        Description = "When Set-Cookie is present, expects Secure and HttpOnly attributes.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = evidence.Probes.Values.FirstOrDefault();
        if (primary is null || !primary.Headers.TryGetValue("Set-Cookie", out var cookie))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var ok = cookie.Contains("Secure", StringComparison.OrdinalIgnoreCase) &&
                 cookie.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase);
        return ok
            ? Task.FromResult(CheckHelpers.Pass(Definition))
            : Task.FromResult(CheckHelpers.Fail(Definition, context, "Weak cookie attributes", "Set-Cookie missing Secure and/or HttpOnly.", FindingSeverity.Medium));
    }
}
