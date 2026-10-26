using System.Net;

using ARTR.Pien.UnitTests.Brotli;
using ARTR.Pien.Web.Transport;

namespace ARTR.Pien.UnitTests.Web;

/// <summary>
/// Unit tests for the runtime-gated Brotli decompression policy (CVE-2026-111497).
/// </summary>
[Collection(nameof(BrotliDiagnosticsCollection))]
public sealed class DecompressionPolicyTests : IDisposable
{
    private const DecompressionMethods GzipDeflate =
        DecompressionMethods.GZip | DecompressionMethods.Deflate;

    private const DecompressionMethods GzipDeflateBrotli =
        DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;

    /// <summary>
    /// Builds the expected UX warning for <paramref name="runtime"/> from <see cref="DecompressionPolicy.MinimumPatchedRuntime"/>.
    /// </summary>
    private static string ExpectedWarning(Version runtime)
        => $"Brotli decompression disabled: runtime {runtime} is older than {DecompressionPolicy.MinimumPatchedRuntime.ToString(3)} (CVE-2026-111497). 'br' is not advertised; responses with Content-Encoding: br are returned undecoded.";

    /// <summary>
    /// Resets process latch and test seams so cases stay order-independent.
    /// </summary>
    public DecompressionPolicyTests()
    {
        DecompressionPolicy.ResetWarningLatchForTests();
        DecompressionPolicy.WarningEmitter = null;
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
    }

    /// <summary>
    /// Restores warning/override seams and clears the latch after each test.
    /// </summary>
    public void Dispose()
    {
        DecompressionPolicy.WarningEmitter = null;
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        DecompressionPolicy.ResetWarningLatchForTests();
    }

    /// <summary>
    /// Patched and newer runtimes must advertise and decode Brotli.
    /// </summary>
    [Theory]
    [InlineData(10, 0, 14)]
    [InlineData(10, 0, 15)]
    [InlineData(10, 1, 0)]
    [InlineData(11, 0, 0)]
    public void Select_enables_brotli_on_patched_or_newer_runtime(int major, int minor, int build)
    {
        var runtime = new Version(major, minor, build);
        Assert.Equal(GzipDeflateBrotli, DecompressionPolicy.Select(runtime));
    }

    /// <summary>
    /// Unpatched and older major runtimes must keep Brotli off (GZip|Deflate only).
    /// </summary>
    [Theory]
    [InlineData(10, 0, 13)]
    [InlineData(10, 0, 0)]
    [InlineData(9, 0, 0)]
    [InlineData(9, 0, 100)]
    [InlineData(8, 0, 33)]
    public void Select_keeps_brotli_off_on_unpatched_runtime(int major, int minor, int build)
    {
        var runtime = new Version(major, minor, build);
        var sink = new List<string>();
        DecompressionPolicy.WarningEmitter = sink.Add;
        try
        {
            Assert.Equal(GzipDeflate, DecompressionPolicy.Select(runtime));
        }
        finally
        {
            DecompressionPolicy.WarningEmitter = null;
            DecompressionPolicy.ResetWarningLatchForTests();
        }
    }

    /// <summary>
    /// Status for 10.0.13 reports Enabled=false with warning text derived from MinimumPatchedRuntime.
    /// </summary>
    [Fact]
    public void GetStatus_reports_disabled_with_warning_on_10_0_13()
    {
        var runtime = new Version(10, 0, 13);
        var status = DecompressionPolicy.GetStatus(runtime);
        Assert.False(status.Enabled);
        Assert.Equal(runtime, status.DetectedRuntime);
        Assert.Equal(DecompressionPolicy.MinimumPatchedRuntime, status.RequiredRuntime);
        Assert.Equal(ExpectedWarning(runtime), status.Warning);
    }

    /// <summary>
    /// Status for 10.0.14 reports Enabled=true with no warning text.
    /// </summary>
    [Fact]
    public void GetStatus_reports_enabled_without_warning_on_10_0_14()
    {
        var runtime = new Version(10, 0, 14);
        var status = DecompressionPolicy.GetStatus(runtime);
        Assert.True(status.Enabled);
        Assert.Equal(runtime, status.DetectedRuntime);
        Assert.Equal(DecompressionPolicy.MinimumPatchedRuntime, status.RequiredRuntime);
        Assert.Null(status.Warning);
    }

    /// <summary>
    /// Warning text embeds <see cref="DecompressionPolicy.MinimumPatchedRuntime"/>.<c>ToString(3)</c>, not a separate literal.
    /// </summary>
    [Fact]
    public void FormatUnpatchedRuntimeWarning_embeds_MinimumPatchedRuntime_ToString3()
    {
        var runtime = new Version(10, 0, 13);
        var text = DecompressionPolicy.FormatUnpatchedRuntimeWarning(runtime);
        var required = DecompressionPolicy.MinimumPatchedRuntime.ToString(3);
        Assert.Equal(ExpectedWarning(runtime), text);
        Assert.Contains($"older than {required}", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Library default must not write to Console.Error (hosts own stderr).
    /// </summary>
    [Fact]
    public void Select_default_emitter_does_not_write_to_console_error()
    {
        var runtime = new Version(10, 0, 13);
        DecompressionPolicy.WarningEmitter = null;
        var original = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            _ = DecompressionPolicy.Select(runtime);
            _ = DecompressionPolicy.Select(runtime);
            Assert.Equal(string.Empty, captured.ToString());
        }
        finally
        {
            Console.SetError(original);
            DecompressionPolicy.ResetWarningLatchForTests();
        }
    }

    /// <summary>
    /// On an unpatched runtime the Trace/WarningEmitter path fires exactly once per process latch.
    /// </summary>
    [Fact]
    public void Select_emits_unpatched_warning_once_on_old_runtime()
    {
        var runtime = new Version(10, 0, 13);
        var messages = new List<string>();
        DecompressionPolicy.WarningEmitter = messages.Add;
        try
        {
            _ = DecompressionPolicy.Select(runtime);
            _ = DecompressionPolicy.Select(runtime);
            Assert.Single(messages);
            Assert.Equal(ExpectedWarning(runtime), messages[0]);
        }
        finally
        {
            DecompressionPolicy.WarningEmitter = null;
            DecompressionPolicy.ResetWarningLatchForTests();
        }
    }

    /// <summary>
    /// Patched runtimes must never emit the unpatched-runtime Brotli warning.
    /// </summary>
    [Fact]
    public void Select_does_not_warn_on_patched_runtime()
    {
        var messages = new List<string>();
        DecompressionPolicy.WarningEmitter = messages.Add;
        try
        {
            _ = DecompressionPolicy.Select(new Version(10, 0, 14));
            _ = DecompressionPolicy.Select(new Version(10, 0, 15));
            Assert.Empty(messages);
        }
        finally
        {
            DecompressionPolicy.WarningEmitter = null;
            DecompressionPolicy.ResetWarningLatchForTests();
        }
    }

    /// <summary>
    /// With runtime override 10.0.13, two SelectForCurrentRuntime calls emit once; 10.0.14 emits nothing.
    /// </summary>
    [Fact]
    public void SelectForCurrentRuntime_warns_once_on_10_0_13_override_and_never_on_10_0_14()
    {
        var messages = new List<string>();
        DecompressionPolicy.WarningEmitter = messages.Add;
        try
        {
            DecompressionPolicy.SetRuntimeVersionOverrideForTests(new Version(10, 0, 13));
            _ = DecompressionPolicy.SelectForCurrentRuntime();
            _ = DecompressionPolicy.SelectForCurrentRuntime();
            Assert.Single(messages);
            Assert.Equal(ExpectedWarning(new Version(10, 0, 13)), messages[0]);

            messages.Clear();
            DecompressionPolicy.ResetWarningLatchForTests();
            DecompressionPolicy.SetRuntimeVersionOverrideForTests(new Version(10, 0, 14));
            _ = DecompressionPolicy.SelectForCurrentRuntime();
            _ = DecompressionPolicy.SelectForCurrentRuntime();
            Assert.Empty(messages);
        }
        finally
        {
            DecompressionPolicy.WarningEmitter = null;
            DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
            DecompressionPolicy.ResetWarningLatchForTests();
        }
    }
}
