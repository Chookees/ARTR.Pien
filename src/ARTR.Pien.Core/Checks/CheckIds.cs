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

    /// <summary>HTTPS downgrade / scheme regression.</summary>
    public const string Http003 = "PIEN-HTTP-003";

    /// <summary>Excessive redirects vs maxRedirects.</summary>
    public const string Http004 = "PIEN-HTTP-004";

    /// <summary>Unexpected server error (5xx).</summary>
    public const string Http005 = "PIEN-HTTP-005";

    /// <summary>Content-Type / charset fundamentals.</summary>
    public const string Http006 = "PIEN-HTTP-006";

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

    /// <summary>HSTS on HTTPS.</summary>
    public const string Headers003 = "PIEN-HEADERS-003";

    /// <summary>Frame ancestors / X-Frame-Options.</summary>
    public const string Headers004 = "PIEN-HEADERS-004";

    /// <summary>COOP / CORP / COEP observations.</summary>
    public const string Headers005 = "PIEN-HEADERS-005";

    /// <summary>Server disclosure headers.</summary>
    public const string Headers006 = "PIEN-HEADERS-006";

    /// <summary>Cookie Secure / HttpOnly / SameSite attributes.</summary>
    public const string Cookie001 = "PIEN-COOKIE-001";

    /// <summary>Cookie over HTTP / Secure missing.</summary>
    public const string Cookie002 = "PIEN-COOKIE-002";

    /// <summary>__Host- / __Secure- prefix rules.</summary>
    public const string Cookie003 = "PIEN-COOKIE-003";

    /// <summary>HTML document structure fundamentals.</summary>
    public const string Html001 = "PIEN-HTML-001";

    /// <summary>Meta / viewport / charset.</summary>
    public const string Html002 = "PIEN-HTML-002";

    /// <summary>Mixed-content references.</summary>
    public const string Html003 = "PIEN-HTML-003";

    /// <summary>Duplicate IDs / heading basics.</summary>
    public const string Html004 = "PIEN-HTML-004";

    /// <summary>Accessibility fundamentals (lang, title, alt).</summary>
    public const string A11y001 = "PIEN-A11Y-001";

    /// <summary>Form controls without labels.</summary>
    public const string A11y002 = "PIEN-A11Y-002";

    /// <summary>Empty links / buttons.</summary>
    public const string A11y003 = "PIEN-A11Y-003";

    /// <summary>Heading-level jumps.</summary>
    public const string A11y004 = "PIEN-A11Y-004";

    /// <summary>Positive tabindex observation.</summary>
    public const string A11y005 = "PIEN-A11Y-005";

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

    /// <summary>Broken external links observed when checkExternalLinks is enabled.</summary>
    public const string Link004 = "PIEN-LINK-004";

    /// <summary>API response contract fundamentals.</summary>
    public const string Api001 = "PIEN-API-001";

    /// <summary>Expected status / content-type.</summary>
    public const string Api002 = "PIEN-API-002";

    /// <summary>JSON Pointer assertions.</summary>
    public const string Api003 = "PIEN-API-003";

    /// <summary>Local JSON Schema validation.</summary>
    public const string Api004 = "PIEN-API-004";

    /// <summary>Non-idempotent guard.</summary>
    public const string Api005 = "PIEN-API-005";

    /// <summary>OpenAPI document and coverage fundamentals.</summary>
    public const string OpenApi001 = "PIEN-OPENAPI-001";

    /// <summary>OpenAPI operationId uniqueness and server URL hygiene.</summary>
    public const string OpenApi002 = "PIEN-OPENAPI-002";

    /// <summary>Response status/content-type vs OpenAPI operation.</summary>
    public const string OpenApi003 = "PIEN-OPENAPI-003";

    /// <summary>Coverage of configured operations vs OpenAPI document.</summary>
    public const string OpenApi004 = "PIEN-OPENAPI-004";

    /// <summary>OpenAPI operations missing operationId.</summary>
    public const string OpenApi005 = "PIEN-OPENAPI-005";

    /// <summary>OpenAPI documented JSON responses missing schemas.</summary>
    public const string OpenApi006 = "PIEN-OPENAPI-006";

    /// <summary>Impressum / legal notice link discoverability.</summary>
    public const string Legal001 = "PIEN-LEGAL-001";

    /// <summary>Privacy / Datenschutz link discoverability.</summary>
    public const string Legal002 = "PIEN-LEGAL-002";

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
        Http003,
        Http004,
        Http005,
        Http006,
        Tls001,
        Tls002,
        Tls003,
        Headers001,
        Headers002,
        Headers003,
        Headers004,
        Headers005,
        Headers006,
        Cookie001,
        Cookie002,
        Cookie003,
        Html001,
        Html002,
        Html003,
        Html004,
        A11y001,
        A11y002,
        A11y003,
        A11y004,
        A11y005,
        Seo001,
        Seo002,
        Seo003,
        Seo004,
        Link001,
        Link002,
        Link003,
        Link004,
        Api001,
        Api002,
        Api003,
        Api004,
        Api005,
        OpenApi001,
        OpenApi002,
        OpenApi003,
        OpenApi004,
        OpenApi005,
        OpenApi006,
        Legal001,
        Legal002,
        Change001,
        Change002,
        Perf001,
        Perf002,
        Perf003,
    ];
}
