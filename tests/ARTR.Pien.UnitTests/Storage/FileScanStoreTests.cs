using ARTR.Pien.Abstractions;
using ARTR.Pien.Policy;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;
using ARTR.Pien.Storage;
using ARTR.Pien.Text;

namespace ARTR.Pien.UnitTests.Storage;

public sealed class FileScanStoreTests
{
    [Fact]
    public async Task Save_get_list_clean_and_baselines_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileScanStore(root);
            var target = ScanTarget.Create(new ScanTarget
            {
                Id = "t1",
                Kind = ScanTargetKind.Website,
                BaseUrl = new Uri("http://127.0.0.1/"),
                Authorization = new TargetAuthorization(true),
            });
            var definition = ScanDefinition.Create(new ScanDefinition
            {
                SchemaVersion = 1,
                ProfileName = "standard",
                Targets = [target],
                Limits = ScanLimits.Default,
            });
            var plan = ScanPlan.Create(new ScanPlan { Definition = definition, SelectedChecks = [], Limits = definition.Limits });
            var run = ScanRun.Create(new ScanRun
            {
                Id = ScanRunId.NewId(),
                Plan = plan,
                Status = ScanRunStatus.Completed,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            var report = ReportDocument.Create(new ReportDocument
            {
                SchemaVersion = 1,
                RunId = run.Id,
                GeneratedAt = DateTimeOffset.UtcNow,
            });
            await store.SaveRunAsync(run, report, TestContext.Current.CancellationToken);
            var loaded = await store.GetRunAsync(run.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(loaded);
            Assert.NotEmpty(await store.ListRecentAsync(10, TestContext.Current.CancellationToken));

            var baseline = Baseline.Create(new Baseline
            {
                Id = "base-1",
                TargetId = "t1",
                ConfigurationFingerprint = "cfg",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await store.SaveAsync(baseline, TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetAsync("base-1", TestContext.Current.CancellationToken));
            Assert.Contains("base-1", await store.ListIdsAsync(TestContext.Current.CancellationToken));
            Assert.True(await store.DeleteAsync("base-1", TestContext.Current.CancellationToken));
            Assert.Equal(0, await store.CleanAsync(50, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Safe_regex_matches_within_timeout()
    {
        Assert.True(SafeRegex.IsMatch("abcdef", "^abc", ScanLimits.Default));
        Assert.False(SafeRegex.IsMatch("zzz", "^abc", ScanLimits.Default));
    }
}
