using ARTR.Pien.Checks;

namespace ARTR.Pien.UnitTests.Core;

public sealed class CheckIdTests
{
    [Theory]
    [InlineData(CheckIds.Http001)]
    [InlineData(CheckIds.Tls001)]
    [InlineData(CheckIds.Headers001)]
    [InlineData(CheckIds.Cookie001)]
    [InlineData(CheckIds.Html001)]
    [InlineData(CheckIds.A11y001)]
    [InlineData(CheckIds.Seo001)]
    [InlineData(CheckIds.Link001)]
    [InlineData(CheckIds.Api001)]
    [InlineData(CheckIds.OpenApi001)]
    [InlineData(CheckIds.Change001)]
    [InlineData(CheckIds.Perf001)]
    public void Built_in_check_ids_parse(string value)
    {
        var id = CheckId.Create(value);
        Assert.Equal(value, id.Value);
        Assert.True(CheckId.TryCreate(value, out var parsed));
        Assert.Equal(id, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("HTTP-001")]
    [InlineData("PIEN-http-001")]
    [InlineData("PIEN-HTTP-1")]
    [InlineData("PIEN-HTTP-0001")]
    [InlineData("pien-HTTP-001")]
    public void Invalid_check_ids_are_rejected(string? value)
    {
        Assert.False(CheckId.TryCreate(value, out _));
        Assert.ThrowsAny<ArgumentException>(() => CheckId.Create(value!));
    }

    [Fact]
    public void CheckIds_All_contains_stable_catalog_entries()
    {
        Assert.Contains(CheckIds.Http001, CheckIds.All);
        Assert.Contains(CheckIds.Tls001, CheckIds.All);
        Assert.Contains(CheckIds.Headers001, CheckIds.All);
        Assert.Equal(CheckIds.All.Distinct(StringComparer.Ordinal).Count(), CheckIds.All.Count);
    }
}
