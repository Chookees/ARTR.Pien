namespace ARTR.Pien.Checks;

/// <summary>
/// Broad functional category for a check. Used for scoring and reporting grouping.
/// </summary>
public enum CheckCategory
{
    /// <summary>Availability and HTTP reliability.</summary>
    Reliability = 0,

    /// <summary>TLS and transport security.</summary>
    TransportSecurity = 1,

    /// <summary>HTTP security headers and related controls.</summary>
    HttpSecurity = 2,

    /// <summary>HTML and content quality fundamentals.</summary>
    ContentQuality = 3,

    /// <summary>Accessibility fundamentals.</summary>
    Accessibility = 4,

    /// <summary>SEO and discoverability fundamentals.</summary>
    Discoverability = 5,

    /// <summary>API contract and schema conformance.</summary>
    ApiContract = 6,

    /// <summary>Performance budgets and timing.</summary>
    Performance = 7,

    /// <summary>Baseline and change stability.</summary>
    ChangeStability = 8,

    /// <summary>Cookie security and attributes.</summary>
    Cookies = 9,

    /// <summary>General or uncategorized checks.</summary>
    General = 10,
}
