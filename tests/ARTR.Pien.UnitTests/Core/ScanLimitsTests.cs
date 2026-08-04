using ARTR.Pien.Exceptions;
using ARTR.Pien.Limits;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Core;

public sealed class ScanLimitsTests
{
    [Fact]
    public void Default_limits_match_product_specification()
    {
        var limits = ScanLimits.Default;

        Assert.Equal(8, limits.GlobalRequestConcurrency);
        Assert.Equal(4, limits.PerHostConcurrency);
        Assert.Equal(100, limits.MaxCrawlPages);
        Assert.Equal(5, limits.MaxCrawlDepth);
        Assert.Equal(10, limits.MaxRedirects);
        Assert.Equal(1_000, limits.MaxLinksPerPage);
        Assert.Equal(16, limits.MaxDnsAddresses);
        Assert.Equal(TimeSpan.FromSeconds(10), limits.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), limits.RequestTimeout);
        Assert.Equal(TimeSpan.FromMinutes(10), limits.OverallScanTimeout);
        Assert.Equal(5L * 1024 * 1024, limits.BodyInspectionLimitBytes);
        Assert.Equal(200, limits.MaxHeaderCount);
        Assert.Equal(4 * 1024, limits.MaxEvidenceExcerptBytes);
        Assert.Equal(1_000, limits.MaxFindingsPerCheck);
        Assert.Equal(10_000, limits.MaxReportFindings);
        Assert.Equal(TimeSpan.FromMilliseconds(250), limits.RegexTimeout);
    }

    [Fact]
    public void Default_limits_are_within_hard_limits()
    {
        ScanLimits.Default.Validate();
        Assert.True(ScanLimits.Default.BodyInspectionLimitBytes <= HardLimits.MaxBodyInspectionBytes);
        Assert.True(HardLimits.MaxBodyInspectionBytes == 50L * 1024 * 1024);
    }

    [Fact]
    public void Limits_reject_values_above_hard_maximums()
    {
        var limits = ScanLimits.Default with { MaxCrawlPages = HardLimits.MaxCrawlPages + 1 };
        Assert.Throws<ConfigurationException>(() => limits.Validate());
    }

    [Fact]
    public void Limits_reject_non_positive_concurrency()
    {
        var limits = ScanLimits.Default with { GlobalRequestConcurrency = 0 };
        Assert.Throws<ConfigurationException>(() => ScanLimits.Create(limits));
    }

    [Fact]
    public void Per_host_concurrency_cannot_exceed_global()
    {
        var limits = ScanLimits.Default with
        {
            GlobalRequestConcurrency = 2,
            PerHostConcurrency = 4,
        };

        Assert.Throws<ConfigurationException>(limits.Validate);
    }

    [Fact]
    public void Body_limit_cannot_exceed_hard_body_ceiling()
    {
        var limits = ScanLimits.Default with
        {
            BodyInspectionLimitBytes = HardLimits.MaxBodyInspectionBytes + 1,
        };

        Assert.Throws<ConfigurationException>(limits.Validate);
    }
}
