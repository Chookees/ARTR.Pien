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

    /// <summary>Broken or unsafe link detection.</summary>
    public const string Link001 = "PIEN-LINK-001";

    /// <summary>API response contract fundamentals.</summary>
    public const string Api001 = "PIEN-API-001";

    /// <summary>OpenAPI document and coverage fundamentals.</summary>
    public const string OpenApi001 = "PIEN-OPENAPI-001";

    /// <summary>Baseline change detection.</summary>
    public const string Change001 = "PIEN-CHANGE-001";

    /// <summary>Performance budget fundamentals.</summary>
    public const string Perf001 = "PIEN-PERF-001";

    /// <summary>
    /// Returns the well-known built-in check identifier constants.
    /// </summary>
    /// <returns>A read-only list of built-in check IDs.</returns>
    public static IReadOnlyList<string> All { get; } =
    [
        Http001,
        Http002,
        Tls001,
        Tls002,
        Headers001,
        Headers002,
        Cookie001,
        Html001,
        A11y001,
        Seo001,
        Link001,
        Api001,
        OpenApi001,
        Change001,
        Perf001,
    ];
}
