using System.Net;

using ARTR.Pien.Web.Transport;

namespace ARTR.Pien.UnitTests.Web;

/// <summary>
/// Unit tests for the runtime-gated Brotli decompression policy (CVE-2026-111497).
/// </summary>
public sealed class DecompressionPolicyTests
{
    private const DecompressionMethods GzipDeflate =
        DecompressionMethods.GZip | DecompressionMethods.Deflate;

    private const DecompressionMethods GzipDeflateBrotli =
        DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;

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
        DecompressionPolicy.ResetWarningLatchForTests();
        try
        {
            Assert.Equal(GzipDeflate, DecompressionPolicy.Select(runtime));
        }
        finally
        {
            DecompressionPolicy.ResetWarningLatchForTests();
        }
    }

    /// <summary>
    /// On an unpatched runtime the operator-facing warning is emitted exactly once per process latch.
    /// </summary>
    [Fact]
    public void Select_emits_unpatched_warning_once_on_old_runtime()
    {
        var runtime = new Version(10, 0, 13);
        var messages = new List<string>();
        DecompressionPolicy.ResetWarningLatchForTests();
        DecompressionPolicy.WarningEmitter = messages.Add;
        try
        {
            _ = DecompressionPolicy.Select(runtime);
            _ = DecompressionPolicy.Select(runtime);
            Assert.Single(messages);
            Assert.Equal(DecompressionPolicy.FormatUnpatchedRuntimeWarning(runtime), messages[0]);
            Assert.Equal(
                "Brotli decompression disabled: runtime 10.0.13 is older than 10.0.14 (CVE-2026-111497). 'br' is not advertised; responses with Content-Encoding: br are returned undecoded.",
                messages[0]);
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
        DecompressionPolicy.ResetWarningLatchForTests();
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
}
