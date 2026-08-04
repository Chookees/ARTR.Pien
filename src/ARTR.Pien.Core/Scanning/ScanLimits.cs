using ARTR.Pien.Exceptions;
using ARTR.Pien.Limits;

namespace ARTR.Pien.Scanning;

/// <summary>
/// Immutable resource limits for a scan. Defaults match the product specification;
/// values are validated against <see cref="HardLimits"/>.
/// </summary>
public sealed record ScanLimits
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScanLimits"/> record.
    /// Prefer <see cref="Create"/> or <see cref="Default"/> for validated instances.
    /// </summary>
    public ScanLimits()
    {
    }

    /// <summary>Global concurrent outbound requests. Default: 8.</summary>
    public int GlobalRequestConcurrency { get; init; } = 8;

    /// <summary>Per-host concurrent outbound requests. Default: 4.</summary>
    public int PerHostConcurrency { get; init; } = 4;

    /// <summary>Maximum crawl pages. Default: 100.</summary>
    public int MaxCrawlPages { get; init; } = 100;

    /// <summary>Maximum crawl depth. Default: 5.</summary>
    public int MaxCrawlDepth { get; init; } = 5;

    /// <summary>Maximum redirects followed. Default: 10.</summary>
    public int MaxRedirects { get; init; } = 10;

    /// <summary>Maximum links processed per page. Default: 1,000.</summary>
    public int MaxLinksPerPage { get; init; } = 1_000;

    /// <summary>Maximum DNS addresses processed. Default: 16.</summary>
    public int MaxDnsAddresses { get; init; } = 16;

    /// <summary>TCP/TLS connect timeout. Default: 10 seconds.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Per-request timeout. Default: 30 seconds.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Overall scan timeout. Default: 10 minutes.</summary>
    public TimeSpan OverallScanTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Default body inspection limit in bytes. Default: 5 MiB.</summary>
    public long BodyInspectionLimitBytes { get; init; } = 5L * 1024 * 1024;

    /// <summary>Maximum header count retained. Default: 200.</summary>
    public int MaxHeaderCount { get; init; } = 200;

    /// <summary>Maximum evidence excerpt size in bytes. Default: 4 KiB.</summary>
    public int MaxEvidenceExcerptBytes { get; init; } = 4 * 1024;

    /// <summary>Maximum findings retained per check. Default: 1,000.</summary>
    public int MaxFindingsPerCheck { get; init; } = 1_000;

    /// <summary>Maximum findings retained in a report. Default: 10,000.</summary>
    public int MaxReportFindings { get; init; } = 10_000;

    /// <summary>Regular-expression match timeout. Default: 250 milliseconds.</summary>
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Product-default limits as documented in the specification.
    /// </summary>
    public static ScanLimits Default { get; } = Create(new ScanLimits());

    /// <summary>
    /// Creates a validated limits instance.
    /// </summary>
    /// <param name="limits">Candidate limits.</param>
    /// <returns>A validated copy of <paramref name="limits"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when any value violates hard limits or invariants.</exception>
    public static ScanLimits Create(ScanLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        return limits;
    }

    /// <summary>
    /// Validates that all values are positive and within <see cref="HardLimits"/>.
    /// </summary>
    /// <exception cref="ConfigurationException">Thrown when validation fails.</exception>
    public void Validate()
    {
        EnsureInRange(nameof(GlobalRequestConcurrency), GlobalRequestConcurrency, HardLimits.MinConcurrency, HardLimits.MaxGlobalRequestConcurrency);
        EnsureInRange(nameof(PerHostConcurrency), PerHostConcurrency, HardLimits.MinConcurrency, HardLimits.MaxPerHostConcurrency);
        if (PerHostConcurrency > GlobalRequestConcurrency)
        {
            throw new ConfigurationException("Per-host concurrency cannot exceed global request concurrency.");
        }

        EnsureInRange(nameof(MaxCrawlPages), MaxCrawlPages, HardLimits.MinPositiveCount, HardLimits.MaxCrawlPages);
        EnsureInRange(nameof(MaxCrawlDepth), MaxCrawlDepth, HardLimits.MinPositiveCount, HardLimits.MaxCrawlDepth);
        EnsureInRange(nameof(MaxRedirects), MaxRedirects, HardLimits.MinPositiveCount, HardLimits.MaxRedirects);
        EnsureInRange(nameof(MaxLinksPerPage), MaxLinksPerPage, HardLimits.MinPositiveCount, HardLimits.MaxLinksPerPage);
        EnsureInRange(nameof(MaxDnsAddresses), MaxDnsAddresses, HardLimits.MinPositiveCount, HardLimits.MaxDnsAddresses);
        EnsureInRange(nameof(MaxHeaderCount), MaxHeaderCount, HardLimits.MinPositiveCount, HardLimits.MaxHeaderCount);
        EnsureInRange(nameof(MaxEvidenceExcerptBytes), MaxEvidenceExcerptBytes, HardLimits.MinPositiveCount, HardLimits.MaxEvidenceExcerptBytes);
        EnsureInRange(nameof(MaxFindingsPerCheck), MaxFindingsPerCheck, HardLimits.MinPositiveCount, HardLimits.MaxFindingsPerCheck);
        EnsureInRange(nameof(MaxReportFindings), MaxReportFindings, HardLimits.MinPositiveCount, HardLimits.MaxReportFindings);

        EnsureTimeout(nameof(ConnectTimeout), ConnectTimeout, HardLimits.MaxConnectTimeout);
        EnsureTimeout(nameof(RequestTimeout), RequestTimeout, HardLimits.MaxRequestTimeout);
        EnsureTimeout(nameof(OverallScanTimeout), OverallScanTimeout, HardLimits.MaxOverallScanTimeout);
        EnsureTimeout(nameof(RegexTimeout), RegexTimeout, HardLimits.MaxRegexTimeout);

        if (BodyInspectionLimitBytes < 1)
        {
            throw new ConfigurationException("Body inspection limit must be at least 1 byte.");
        }

        if (BodyInspectionLimitBytes > HardLimits.MaxBodyInspectionBytes)
        {
            throw new ConfigurationException(
                $"Body inspection limit {BodyInspectionLimitBytes} exceeds the hard maximum of {HardLimits.MaxBodyInspectionBytes} bytes.");
        }
    }

    private static void EnsureInRange(string name, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            throw new ConfigurationException($"{name} must be between {min} and {max}, inclusive. Received {value}.");
        }
    }

    private static void EnsureTimeout(string name, TimeSpan value, TimeSpan max)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ConfigurationException($"{name} must be greater than zero.");
        }

        if (value > max)
        {
            throw new ConfigurationException($"{name} must not exceed {max}.");
        }
    }
}
