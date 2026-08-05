namespace ARTR.Pien.Checks;

/// <summary>
/// Stable built-in check identifier constants. Identifiers never depend on execution order.
/// </summary>
public static class CheckIds
{
    /// <summary>HTTP availability / status fundamentals.</summary>
    public const string Http001 = "PIEN-HTTP-001";

    /// <summary>HTTP redirect chain sanity.</summary>
    public const string Http002 = "PIEN-HTTP-002";

    /// <summary>TLS protocol and certificate fundamentals.</summary>
    public const string Tls001 = "PIEN-TLS-001";

    /// <summary>TLS certificate expiration proximity.</summary>
    public const string Tls002 = "PIEN-TLS-002";

    /// <summary>Weak / obsolete TLS protocol observation.</summary>
    public const string Tls003 = "PIEN-TLS-003";

    /// <summary>Security headers presence and values.</summary>
    public const string Headers001 = "PIEN-HEADERS-001";

    /// <summary>Content-Security-Policy fundamentals.</summary>
    public const string Headers002 = "PIEN-HEADERS-002";

    /// <summary>Cookie Secure / HttpOnly / SameSite attributes.</summary>
    public const string Cookie001 = "PIEN-COOKIE-001";

    /// <summary>HTML document structure fundamentals.</summary>
    public const string Html001 = "PIEN-HTML-001";

    /// <summary>Accessibility fundamentals (lang, title, alt).</summary>
    public const string A11y001 = "PIEN-A11Y-001";

    /// <summary>SEO / discoverability fundamentals.</summary>
    public const string Seo001 = "PIEN-SEO-001";

    /// <summary>Title / description length budgets.</summary>
    public const string Seo002 = "PIEN-SEO-002";

    /// <summary>Canonical / indexability conflicts.</summary>
    public const string Seo003 = "PIEN-SEO-003";

    /// <summary>robots.txt / sitemap reachability.</summary>
    public const string Seo004 = "PIEN-SEO-004";

    /// <summary>Broken or unsafe link detection.</summary>
    public const string Link001 = "PIEN-LINK-001";

    /// <summary>Broken internal links observed during crawl.</summary>
    public const string Link002 = "PIEN-LINK-002";

    /// <summary>Missing link text.</summary>
    public const string Link003 = "PIEN-LINK-003";

    /// <summary>API response contract fundamentals.</summary>
    public const string Api001 = "PIEN-API-001";

    /// <summary>OpenAPI document and coverage fundamentals.</summary>
    public const string OpenApi001 = "PIEN-OPENAPI-001";

    /// <summary>OpenAPI operationId uniqueness and server URL hygiene.</summary>
    public const string OpenApi002 = "PIEN-OPENAPI-002";

    /// <summary>Response status/content-type vs OpenAPI operation.</summary>
    public const string OpenApi003 = "PIEN-OPENAPI-003";

    /// <summary>Coverage of configured operations vs OpenAPI document.</summary>
    public const string OpenApi004 = "PIEN-OPENAPI-004";

    /// <summary>Baseline change detection.</summary>
    public const string Change001 = "PIEN-CHANGE-001";

    /// <summary>Volatile content fingerprint drift advisory.</summary>
    public const string Change002 = "PIEN-CHANGE-002";

    /// <summary>Performance budget fundamentals.</summary>
    public const string Perf001 = "PIEN-PERF-001";

    /// <summary>TTFB / total duration budgets.</summary>
    public const string Perf002 = "PIEN-PERF-002";

    /// <summary>Body size budget (advisory).</summary>
    public const string Perf003 = "PIEN-PERF-003";

    /// <summary>
    /// Returns the well-known built-in check identifier constants.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Http001,
        Http002,
        Tls001,
        Tls002,
        Tls003,
        Headers001,
        Headers002,
        Cookie001,
        Html001,
        A11y001,
        Seo001,
        Seo002,
        Seo003,
        Seo004,
        Link001,
        Link002,
        Link003,
        Api001,
        OpenApi001,
        OpenApi002,
        OpenApi003,
        OpenApi004,
        Change001,
        Change002,
        Perf001,
        Perf002,
        Perf003,
    ];
}
