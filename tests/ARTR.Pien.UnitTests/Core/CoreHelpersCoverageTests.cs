using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Redaction;
using ARTR.Pien.Scanning;
using ARTR.Pien.Text;

namespace ARTR.Pien.UnitTests.Core;

public sealed class CoreHelpersCoverageTests
{
    [Fact]
    public void SafeRegex_match_and_create()
    {
        Assert.True(SafeRegex.IsMatch("abc", "^a", ScanLimits.Default));
        Assert.False(SafeRegex.IsMatch("abc", "^z", ScanLimits.Default));
        Assert.NotNull(SafeRegex.Create("a+", ScanLimits.Default));
    }

    [Fact]
    public void Redaction_helpers_cover_paths()
    {
        Assert.False(RedactionHelpers.IsSensitiveHeaderName(null));
        Assert.True(RedactionHelpers.IsSensitiveHeaderName("Authorization"));
        Assert.True(RedactionHelpers.IsSensitiveHeaderName("X-My-Token"));
        Assert.Equal(RedactionMarkers.Authorization, RedactionHelpers.RedactHeaderValue("Authorization", "Bearer x"));
        Assert.Equal(RedactionMarkers.Cookie, RedactionHelpers.RedactHeaderValue("Set-Cookie", "a=b"));
        Assert.Equal(RedactionMarkers.Redacted, RedactionHelpers.RedactHeaderValue("X-Api-Key", "k"));
        Assert.Equal("ok", RedactionHelpers.RedactHeaderValue("Content-Type", "ok"));
        Assert.Equal(RedactionMarkers.Secret, RedactionHelpers.MaskSecretReference(null));
        Assert.Equal("secret://env/NAME", RedactionHelpers.MaskSecretReference("secret://env/NAME"));
        Assert.StartsWith("secret://file/", RedactionHelpers.MaskSecretReference("secret://file/path"), StringComparison.Ordinal);
        Assert.Equal(RedactionMarkers.Secret, RedactionHelpers.MaskSecretReference("other"));
        Assert.Equal(string.Empty, RedactionHelpers.Truncate(null, 3));
        Assert.Equal("ab", RedactionHelpers.Truncate("ab", 3));
        Assert.Equal("ab…", RedactionHelpers.Truncate("abcd", 2));
    }

    [Fact]
    public void Baseline_comparison_and_expected_status_create()
    {
        var comparison = BaselineComparison.Create(new BaselineComparison
        {
            FindingFingerprint = "abc",
            Kind = BaselineComparisonKind.Changed,
        });
        Assert.Equal(BaselineComparisonKind.Changed, comparison.Kind);
        var constraint = new ExpectedStatusConstraint(200, 299);
        Assert.Equal(200, constraint.Min);
        Assert.Equal(299, constraint.Max);
    }

    [Fact]
    public void Probe_request_create_validates()
    {
        var request = ProbeRequest.Create(new ProbeRequest
        {
            Uri = new Uri("http://127.0.0.1/"),
            Method = ProbeMethod.Get,
            MaxResponseBodyBytes = 1024,
        });
        Assert.Equal(ProbeMethod.Get, request.Method);
    }
}
