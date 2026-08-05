using ARTR.Pien.Configuration;
using ARTR.Pien.Exceptions;

namespace ARTR.Pien.UnitTests.Configuration;

public sealed class JsonConfigLoaderTests
{
    [Fact]
    public async Task Explicit_missing_config_path_throws_ConfigurationException()
    {
        var loader = new JsonConfigLoader();
        var missing = Path.Combine(Path.GetTempPath(), "pien-missing-" + Guid.NewGuid().ToString("N") + ".json");
        var ex = await Assert.ThrowsAsync<ConfigurationException>(() =>
            loader.LoadAsync(new ConfigLoadRequest(missing, Directory.GetCurrentDirectory()), TestContext.Current.CancellationToken));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(missing, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Malformed_json_throws_ConfigurationException()
    {
        var path = Path.Combine(Path.GetTempPath(), "pien-bad-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(path, "{ \"schemaVersion\": 1,", TestContext.Current.CancellationToken);
        try
        {
            var loader = new JsonConfigLoader();
            var ex = await Assert.ThrowsAsync<ConfigurationException>(() =>
                loader.LoadAsync(new ConfigLoadRequest(path, Directory.GetCurrentDirectory()), TestContext.Current.CancellationToken));
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(ex.InnerException);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Implicit_missing_pien_json_loads_empty_defaults()
    {
        var working = Path.Combine(Path.GetTempPath(), "pien-wd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(working);
        try
        {
            var loader = new JsonConfigLoader();
            var configuration = await loader.LoadAsync(
                new ConfigLoadRequest(null, working),
                TestContext.Current.CancellationToken);
            Assert.Equal(1, configuration.SchemaVersion);
            Assert.Empty(configuration.Targets);
        }
        finally
        {
            Directory.Delete(working, recursive: true);
        }
    }
}
