using ARTR.Pien;
using ARTR.Pien.Redaction;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Core;

public sealed class CoreContractsTests
{
    [Fact]
    public void Exit_codes_cover_zero_through_ten()
    {
        Assert.Equal(0, (int)PienExitCode.Success);
        Assert.Equal(1, (int)PienExitCode.PolicyFailed);
        Assert.Equal(2, (int)PienExitCode.InvalidArguments);
        Assert.Equal(3, (int)PienExitCode.InvalidConfiguration);
        Assert.Equal(4, (int)PienExitCode.TargetRejected);
        Assert.Equal(5, (int)PienExitCode.ScanFailed);
        Assert.Equal(6, (int)PienExitCode.ReportFailed);
        Assert.Equal(7, (int)PienExitCode.BaselineFailed);
        Assert.Equal(8, (int)PienExitCode.StorageFailed);
        Assert.Equal(9, (int)PienExitCode.NotificationFailed);
        Assert.Equal(10, (int)PienExitCode.Cancelled);
    }

    [Fact]
    public void Redaction_helpers_mask_sensitive_headers()
    {
        Assert.True(RedactionHelpers.IsSensitiveHeaderName("Authorization"));
        Assert.Equal(RedactionMarkers.Authorization, RedactionHelpers.RedactHeaderValue("Authorization", "Bearer abc"));
        Assert.Equal(RedactionMarkers.Cookie, RedactionHelpers.RedactHeaderValue("Set-Cookie", "sid=1"));
        Assert.Equal("text/html", RedactionHelpers.RedactHeaderValue("Content-Type", "text/html"));
    }

    [Fact]
    public void Scan_target_rejects_non_http_schemes()
    {
        Assert.ThrowsAny<ArgumentException>(() => ScanTarget.Create(new ScanTarget
        {
            Id = "local",
            Kind = ScanTargetKind.Website,
            BaseUrl = new Uri("file:///tmp/index.html"),
            Authorization = new TargetAuthorization(Confirmed: true),
        }));
    }

    [Fact]
    public void Scan_definition_requires_unique_targets()
    {
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "public-site",
            Kind = ScanTargetKind.Website,
            BaseUrl = new Uri("https://example.com/"),
            Authorization = new TargetAuthorization(Confirmed: true),
        });

        Assert.ThrowsAny<ArgumentException>(() => ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = ScanProfileNames.Standard,
            Targets = [target, target],
            Limits = ScanLimits.Default,
        }));
    }
}
