using ARTR.Pien.Cli;
using ARTR.Pien.UnitTests.Brotli;
using ARTR.Pien.Web.Transport;

namespace ARTR.Pien.UnitTests.Cli;

/// <summary>
/// CLI stderr surfacing of the Brotli runtime gate (CVE-2026-111497).
/// </summary>
[Collection(nameof(BrotliDiagnosticsCollection))]
public sealed class BrotliRuntimeWarningWriterTests : IDisposable
{
    /// <summary>
    /// Builds the expected <c>warning:</c> stderr line from <see cref="DecompressionPolicy.MinimumPatchedRuntime"/>.
    /// </summary>
    private static string ExpectedWarningLine(Version runtime)
        => "warning: Brotli decompression disabled: runtime " + runtime + " is older than "
            + DecompressionPolicy.MinimumPatchedRuntime.ToString(3)
            + " (CVE-2026-111497). 'br' is not advertised; responses with Content-Encoding: br are returned undecoded."
            + Environment.NewLine;

    /// <summary>
    /// Clears CLI/Web test seams so cases stay order-independent.
    /// </summary>
    public BrotliRuntimeWarningWriterTests()
    {
        BrotliRuntimeWarningWriter.ResetForTests();
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        DecompressionPolicy.ResetWarningLatchForTests();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        BrotliRuntimeWarningWriter.ResetForTests();
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        DecompressionPolicy.ResetWarningLatchForTests();
    }

    /// <summary>
    /// With status for 10.0.13 the CLI prints exactly one <c>warning:</c> line even if called twice.
    /// </summary>
    [Fact]
    public void WriteToStderrIfNeeded_prints_warning_exactly_once_for_10_0_13()
    {
        var status = DecompressionPolicy.GetStatus(new Version(10, 0, 13));
        var original = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            BrotliRuntimeWarningWriter.WriteToStderrIfNeeded(quiet: false, status);
            BrotliRuntimeWarningWriter.WriteToStderrIfNeeded(quiet: false, status);
            Assert.Equal(ExpectedWarningLine(new Version(10, 0, 13)), captured.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    /// <summary>
    /// <c>--quiet</c> suppresses the Brotli stderr warning.
    /// </summary>
    [Fact]
    public void WriteToStderrIfNeeded_suppresses_output_when_quiet()
    {
        var status = DecompressionPolicy.GetStatus(new Version(10, 0, 13));
        var original = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            BrotliRuntimeWarningWriter.WriteToStderrIfNeeded(quiet: true, status);
            Assert.Equal(string.Empty, captured.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    /// <summary>
    /// Patched runtime status has no warning text, so the CLI prints nothing.
    /// </summary>
    [Fact]
    public void WriteToStderrIfNeeded_prints_nothing_on_10_0_14()
    {
        var status = DecompressionPolicy.GetStatus(new Version(10, 0, 14));
        Assert.True(status.Enabled);
        Assert.Null(status.Warning);
        var original = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            BrotliRuntimeWarningWriter.WriteToStderrIfNeeded(quiet: false, status);
            Assert.Equal(string.Empty, captured.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

}
