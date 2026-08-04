namespace ARTR.Pien.Limits;

/// <summary>
/// Absolute hard ceilings that configuration must never exceed.
/// Values are intentionally above product defaults and cannot be disabled.
/// </summary>
public static class HardLimits
{
    /// <summary>Maximum allowed global request concurrency.</summary>
    public const int MaxGlobalRequestConcurrency = 64;

    /// <summary>Maximum allowed per-host concurrency.</summary>
    public const int MaxPerHostConcurrency = 32;

    /// <summary>Maximum allowed crawl page count.</summary>
    public const int MaxCrawlPages = 10_000;

    /// <summary>Maximum allowed crawl depth.</summary>
    public const int MaxCrawlDepth = 50;

    /// <summary>Maximum allowed redirect hops.</summary>
    public const int MaxRedirects = 50;

    /// <summary>Maximum allowed links processed per page.</summary>
    public const int MaxLinksPerPage = 10_000;

    /// <summary>Maximum DNS addresses processed per resolution.</summary>
    public const int MaxDnsAddresses = 64;

    /// <summary>Maximum connect timeout.</summary>
    public static readonly TimeSpan MaxConnectTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Maximum per-request timeout.</summary>
    public static readonly TimeSpan MaxRequestTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Maximum overall scan timeout.</summary>
    public static readonly TimeSpan MaxOverallScanTimeout = TimeSpan.FromHours(2);

    /// <summary>Hard body inspection limit in bytes (50 MiB).</summary>
    public const long MaxBodyInspectionBytes = 50L * 1024 * 1024;

    /// <summary>Maximum header count retained from a response.</summary>
    public const int MaxHeaderCount = 1_000;

    /// <summary>Maximum evidence excerpt size in bytes (64 KiB).</summary>
    public const int MaxEvidenceExcerptBytes = 64 * 1024;

    /// <summary>Maximum findings retained per check.</summary>
    public const int MaxFindingsPerCheck = 10_000;

    /// <summary>Maximum findings retained in a report.</summary>
    public const int MaxReportFindings = 100_000;

    /// <summary>Maximum regex match timeout.</summary>
    public static readonly TimeSpan MaxRegexTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Minimum positive concurrency value.</summary>
    public const int MinConcurrency = 1;

    /// <summary>Minimum positive count value.</summary>
    public const int MinPositiveCount = 1;
}
