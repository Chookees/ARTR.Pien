using ARTR.Pien.Findings;

namespace ARTR.Pien.UnitTests.Core;

public sealed class FindingSeverityTests
{
    [Fact]
    public void Severity_values_are_ordered_by_impact()
    {
        Assert.True(FindingSeverity.Info < FindingSeverity.Low);
        Assert.True(FindingSeverity.Low < FindingSeverity.Medium);
        Assert.True(FindingSeverity.Medium < FindingSeverity.High);
        Assert.True(FindingSeverity.High < FindingSeverity.Critical);
    }

    [Theory]
    [InlineData(FindingSeverity.Info)]
    [InlineData(FindingSeverity.Low)]
    [InlineData(FindingSeverity.Medium)]
    [InlineData(FindingSeverity.High)]
    [InlineData(FindingSeverity.Critical)]
    public void Severity_is_defined(FindingSeverity severity)
    {
        Assert.True(Enum.IsDefined(severity));
    }

    [Fact]
    public void Status_includes_error_distinct_from_pass_and_fail()
    {
        Assert.NotEqual(FindingStatus.Pass, FindingStatus.Error);
        Assert.NotEqual(FindingStatus.Fail, FindingStatus.Error);
        Assert.True(Enum.IsDefined(FindingStatus.Suppressed));
        Assert.True(Enum.IsDefined(FindingStatus.NotApplicable));
        Assert.True(Enum.IsDefined(FindingStatus.Skipped));
        Assert.True(Enum.IsDefined(FindingStatus.Warning));
    }
}
