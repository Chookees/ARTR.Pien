using System.Text;

using AngleSharp.Dom;
using AngleSharp.Html.Parser;

using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Text;

namespace ARTR.Pien.Checks.Website;

/// <summary>HTTPS → HTTP scheme downgrade.</summary>
public sealed class HttpHttpsDowngradeCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Http003),
        Name = "HTTPS downgrade",
        Category = CheckCategory.Reliability,
        DefaultSeverity = FindingSeverity.High,
        Description = "Fails when an https target finalizes or redirects to http.",
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

        var primary = CheckHelpers.Primary(evidence);
        if (primary is null)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        if (string.Equals(primary.FinalUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "HTTPS downgrade",
                $"Final URI scheme is http: {primary.FinalUri}",
                FindingSeverity.High));
        }

        foreach (var hop in primary.RedirectChain)
        {
            if (string.Equals(hop.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "HTTPS redirect downgrade",
                    $"Redirect hop uses http: {hop}",
                    FindingSeverity.High));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Excessive redirects vs maxRedirects.</summary>
public sealed class HttpExcessiveRedirectsCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Http004),
        Name = "Excessive redirects",
        Category = CheckCategory.Reliability,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Fails when the redirect chain length exceeds the scan maxRedirects limit.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || primary.RedirectChain.Count == 0)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        if (primary.RedirectChain.Count > context.Limits.MaxRedirects)
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Excessive redirects",
                $"Redirect chain length {primary.RedirectChain.Count} exceeds limit {context.Limits.MaxRedirects}.",
                FindingSeverity.Medium));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Unexpected 5xx responses.</summary>
public sealed class HttpServerErrorCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Http005),
        Name = "HTTP server error",
        Category = CheckCategory.Reliability,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Fails when the primary probe or crawled pages return a 5xx status.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary?.StatusCode is { } code && (int)code >= 500)
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                $"HTTP {(int)code}",
                "Primary response returned a server error.",
                FindingSeverity.Medium));
        }

        foreach (var page in evidence.CrawledPages)
        {
            if (page.Probe?.StatusCode is { } crawlCode && (int)crawlCode >= 500)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    $"HTTP {(int)crawlCode} on crawled page",
                    $"{page.Page.Uri} returned {(int)crawlCode}.",
                    FindingSeverity.Medium));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Content-Type / charset fundamentals for HTML.</summary>
public sealed class HttpContentTypeCharsetCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Http006),
        Name = "Content-Type charset",
        Category = CheckCategory.Reliability,
        DefaultSeverity = FindingSeverity.Low,
        Description = "When Content-Type indicates HTML, expects an explicit charset parameter.",
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

        var contentType = primary.ContentType ??
                          (primary.Headers.TryGetValue("Content-Type", out var ct) ? ct : string.Empty);
        if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        if (!contentType.Contains("charset=", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Missing charset",
                $"HTML Content-Type lacks charset: '{contentType}'.",
                FindingSeverity.Low));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>HSTS on HTTPS.</summary>
public sealed class HstsHeaderCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Headers003),
        Name = "HSTS",
        Category = CheckCategory.HttpSecurity,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Requires Strict-Transport-Security on https responses.",
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

        var primary = CheckHelpers.Primary(evidence);
        if (primary is null ||
            !primary.Headers.TryGetValue("Strict-Transport-Security", out var hsts) ||
            string.IsNullOrWhiteSpace(hsts))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "HSTS missing",
                "Strict-Transport-Security header was not present.",
                FindingSeverity.Medium));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Frame ancestors / X-Frame-Options.</summary>
public sealed class FrameAncestorsCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Headers004),
        Name = "Frame protection",
        Category = CheckCategory.HttpSecurity,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Requires CSP frame-ancestors or X-Frame-Options.",
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

        var hasXfo = primary.Headers.TryGetValue("X-Frame-Options", out var xfo) && !string.IsNullOrWhiteSpace(xfo);
        var hasFrameAncestors = primary.Headers.TryGetValue("Content-Security-Policy", out var csp) &&
                                csp.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase);
        if (hasXfo || hasFrameAncestors)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        return Task.FromResult(CheckHelpers.Fail(
            Definition,
            context,
            "Missing frame protection",
            "Neither X-Frame-Options nor CSP frame-ancestors was present.",
            FindingSeverity.Low));
    }
}

/// <summary>COOP / CORP / COEP observations.</summary>
public sealed class CrossOriginIsolationHeadersCheck : ICheck
{
    private static readonly string[] IsolationHeaders =
    [
        "Cross-Origin-Opener-Policy",
        "Cross-Origin-Resource-Policy",
        "Cross-Origin-Embedder-Policy",
    ];

    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Headers005),
        Name = "Cross-origin isolation headers",
        Category = CheckCategory.HttpSecurity,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Observes absence of COOP/CORP/COEP headers (advisory).",
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

        var missing = IsolationHeaders.Where(h => !primary.Headers.ContainsKey(h)).ToArray();
        if (missing.Length == IsolationHeaders.Length)
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Cross-origin isolation headers absent",
                "Missing: " + string.Join(", ", IsolationHeaders),
                FindingSeverity.Low));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Server disclosure headers.</summary>
public sealed class ServerDisclosureHeadersCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Headers006),
        Name = "Server disclosure headers",
        Category = CheckCategory.HttpSecurity,
        DefaultSeverity = FindingSeverity.Info,
        Description = "Flags Server and X-Powered-By response headers (informational).",
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

        var disclosed = new List<string>();
        if (primary.Headers.ContainsKey("Server"))
        {
            disclosed.Add("Server");
        }

        if (primary.Headers.ContainsKey("X-Powered-By"))
        {
            disclosed.Add("X-Powered-By");
        }

        if (disclosed.Count == 0)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        return Task.FromResult(CheckHelpers.Fail(
            Definition,
            context,
            "Server disclosure headers present",
            "Present: " + string.Join(", ", disclosed),
            FindingSeverity.Info));
    }
}

/// <summary>Cookies over HTTP without Secure.</summary>
public sealed class CookieOverHttpCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Cookie002),
        Name = "Cookie over HTTP",
        Category = CheckCategory.Cookies,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Fails when Set-Cookie is observed on http without the Secure attribute.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(context.Target.BaseUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.NotApplicable(Definition));
        }

        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || !primary.Headers.TryGetValue("Set-Cookie", out var cookie) || string.IsNullOrWhiteSpace(cookie))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        if (!cookie.Contains("Secure", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Insecure cookie over HTTP",
                "Set-Cookie on http lacks Secure attribute.",
                FindingSeverity.Medium));
        }

        return Task.FromResult(CheckHelpers.Fail(
            Definition,
            context,
            "Secure cookie attribute on HTTP",
            "Set-Cookie with Secure was issued over http (ineffective).",
            FindingSeverity.Medium));
    }
}

/// <summary>__Host- / __Secure- cookie prefix rules.</summary>
public sealed class CookiePrefixRulesCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Cookie003),
        Name = "Cookie prefix rules",
        Category = CheckCategory.Cookies,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Validates __Host- and __Secure- cookie prefix attribute rules. Cookie values are never reported.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || !primary.Headers.TryGetValue("Set-Cookie", out var cookie) || string.IsNullOrWhiteSpace(cookie))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var name = cookie.Split('=', 2)[0].Trim();
        var hasSecure = cookie.Contains("Secure", StringComparison.OrdinalIgnoreCase);
        var hasDomain = cookie.Contains("Domain=", StringComparison.OrdinalIgnoreCase);
        var pathExactRoot = cookie.Contains("Path=/", StringComparison.OrdinalIgnoreCase) &&
                            !SafeRegex.IsMatch(cookie, @"Path=/[^;\s]", context.Limits);

        if (name.StartsWith("__Host-", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasSecure || hasDomain || !pathExactRoot)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "__Host- prefix violation",
                    "__Host- cookies require Secure, Path=/, and no Domain.",
                    FindingSeverity.Low));
            }
        }
        else if (name.StartsWith("__Secure-", StringComparison.OrdinalIgnoreCase) && !hasSecure)
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "__Secure- prefix violation",
                "__Secure- cookies require the Secure attribute.",
                FindingSeverity.Low));
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Meta description, viewport, charset.</summary>
public sealed class HtmlMetaViewportCharsetCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Html002),
        Name = "HTML meta fundamentals",
        Category = CheckCategory.ContentQuality,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Requires meta description, viewport, and charset declarations on HTML responses.",
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
        var hasDescription = !string.IsNullOrWhiteSpace(
            document.Head?.QuerySelector("meta[name='description']")?.GetAttribute("content"));
        var hasViewport = document.Head?.QuerySelector("meta[name='viewport']") is not null;
        var hasCharset = document.Head?.QuerySelector("meta[charset]") is not null ||
                         document.Head?.QuerySelector("meta[http-equiv='content-type']") is not null;
        if (hasDescription && hasViewport && hasCharset)
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        return Task.FromResult(CheckHelpers.Fail(
            Definition,
            context,
            "Missing HTML meta fundamentals",
            $"description={hasDescription}, viewport={hasViewport}, charset={hasCharset}",
            FindingSeverity.Low));
    }
}

/// <summary>Mixed-content references on HTTPS pages.</summary>
public sealed class HtmlMixedContentCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Html003),
        Name = "Mixed content",
        Category = CheckCategory.ContentQuality,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Flags http:// resource references embedded in https HTML pages.",
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

        var primary = CheckHelpers.Primary(evidence);
        if (primary is null || !CheckHelpers.IsHtml(primary))
        {
            return Task.FromResult(CheckHelpers.Pass(Definition));
        }

        var document = new HtmlParser().ParseDocument(Encoding.UTF8.GetString(primary.Body.Span));
        foreach (var element in document.QuerySelectorAll("[src],[href]"))
        {
            var attr = element.GetAttribute("src") ?? element.GetAttribute("href");
            if (attr is not null &&
                attr.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Mixed content reference",
                    $"Insecure http resource reference: {attr}",
                    FindingSeverity.Medium));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Duplicate IDs / heading outline basics.</summary>
public sealed class HtmlDuplicateIdHeadingCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Html004),
        Name = "HTML duplicate IDs / headings",
        Category = CheckCategory.ContentQuality,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags duplicate element IDs and basic heading outline issues.",
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
        var ids = document.All
            .Select(e => e.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id!, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (ids is not null)
        {
            return Task.FromResult(CheckHelpers.Fail(
                Definition,
                context,
                "Duplicate HTML id",
                $"Duplicate id '{ids.Key}' observed.",
                FindingSeverity.Low));
        }

        var headings = document.QuerySelectorAll("h1,h2,h3,h4,h5,h6")
            .Select(HeadingLevel)
            .Where(level => level > 0)
            .ToArray();
        for (var i = 1; i < headings.Length; i++)
        {
            if (headings[i] > headings[i - 1] + 1)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Heading outline jump",
                    $"Heading level jumped from h{headings[i - 1]} to h{headings[i]}.",
                    FindingSeverity.Low));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }

    private static int HeadingLevel(IElement element)
        => element.LocalName?.ToLowerInvariant() switch
        {
            "h1" => 1,
            "h2" => 2,
            "h3" => 3,
            "h4" => 4,
            "h5" => 5,
            "h6" => 6,
            _ => 0,
        };
}

/// <summary>Form controls without associated labels.</summary>
public sealed class AccessibilityFormLabelCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.A11y002),
        Name = "Form control labels",
        Category = CheckCategory.Accessibility,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags form controls without associated labels. Not a WCAG certification.",
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
        foreach (var control in document.QuerySelectorAll("input,select,textarea"))
        {
            var type = control.GetAttribute("type") ?? "text";
            if (string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "submit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "button", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "image", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = control.Id;
            var hasLabel = (!string.IsNullOrWhiteSpace(id) && document.QuerySelector($"label[for='{id}']") is not null) ||
                           control.Closest("label") is not null ||
                           !string.IsNullOrWhiteSpace(control.GetAttribute("aria-label")) ||
                           !string.IsNullOrWhiteSpace(control.GetAttribute("aria-labelledby"));
            if (!hasLabel)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Unlabeled form control",
                    $"A {control.LocalName} control lacks an associated label.",
                    FindingSeverity.Low));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Empty links / buttons.</summary>
public sealed class AccessibilityEmptyControlsCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.A11y003),
        Name = "Empty links and buttons",
        Category = CheckCategory.Accessibility,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags anchors and buttons with empty accessible names. Not a WCAG certification.",
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
        foreach (var element in document.QuerySelectorAll("a[href],button"))
        {
            var text = element.TextContent?.Trim() ?? string.Empty;
            var aria = element.GetAttribute("aria-label")?.Trim() ?? string.Empty;
            var titled = element.GetAttribute("title")?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(aria) && string.IsNullOrWhiteSpace(titled))
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Empty interactive control",
                    $"Empty {element.LocalName} has no accessible name.",
                    FindingSeverity.Low));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Heading-level jumps.</summary>
public sealed class AccessibilityHeadingJumpCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.A11y004),
        Name = "Heading level jumps",
        Category = CheckCategory.Accessibility,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Flags heading level jumps greater than one. Not a WCAG certification.",
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
        var levels = document.QuerySelectorAll("h1,h2,h3,h4,h5,h6")
            .Select(e => e.LocalName?.ToLowerInvariant() switch
            {
                "h1" => 1,
                "h2" => 2,
                "h3" => 3,
                "h4" => 4,
                "h5" => 5,
                "h6" => 6,
                _ => 0,
            })
            .Where(level => level > 0)
            .ToArray();
        for (var i = 1; i < levels.Length; i++)
        {
            if (levels[i] > levels[i - 1] + 1)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Heading level jump",
                    $"Heading jumped from h{levels[i - 1]} to h{levels[i]}.",
                    FindingSeverity.Low));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}

/// <summary>Positive tabindex observation.</summary>
public sealed class AccessibilityPositiveTabindexCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.A11y005),
        Name = "Positive tabindex",
        Category = CheckCategory.Accessibility,
        DefaultSeverity = FindingSeverity.Info,
        Description = "Observes positive tabindex values that reorder focus. Not a WCAG certification.",
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
        foreach (var element in document.QuerySelectorAll("[tabindex]"))
        {
            if (int.TryParse(element.GetAttribute("tabindex"), out var value) && value > 0)
            {
                return Task.FromResult(CheckHelpers.Fail(
                    Definition,
                    context,
                    "Positive tabindex observed",
                    $"Element uses tabindex={value}.",
                    FindingSeverity.Info));
            }
        }

        return Task.FromResult(CheckHelpers.Pass(Definition));
    }
}
