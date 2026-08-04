using Microsoft.AspNetCore.Mvc.Testing;

namespace ARTR.Pien.FunctionalTests;

public sealed class SampleSiteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SampleSiteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sample_site_serves_home_page()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Sample Site", html, StringComparison.Ordinal);
    }
}
