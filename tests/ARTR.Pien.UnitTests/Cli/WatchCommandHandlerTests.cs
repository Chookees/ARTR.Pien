using ARTR.Pien;
using ARTR.Pien.Cli;
using ARTR.Pien.UnitTests.Brotli;
using ARTR.Pien.Web.Transport;

namespace ARTR.Pien.UnitTests.Cli;

/// <summary>
/// Covers the real <see cref="WatchCommandHandler.RunAsync"/> path used by <c>pien watch</c>.
/// </summary>
[Collection(nameof(BrotliDiagnosticsCollection))]
public sealed class WatchCommandHandlerTests : IDisposable
{
    /// <summary>
    /// Clears Brotli CLI/Web seams before each watch case.
    /// </summary>
    public WatchCommandHandlerTests()
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
    /// Builds the expected <c>warning:</c> stderr line from <see cref="DecompressionPolicy.MinimumPatchedRuntime"/>.
    /// </summary>
    private static string ExpectedWarningLine(Version runtime)
        => "warning: Brotli decompression disabled: runtime " + runtime + " is older than "
            + DecompressionPolicy.MinimumPatchedRuntime.ToString(3)
            + " (CVE-2026-111497). 'br' is not advertised; responses with Content-Encoding: br are returned undecoded.";

    /// <summary>
    /// Real <see cref="WatchCommandHandler.RunAsync"/> prints exactly one Brotli warning after the Watching
    /// banner and before cycle 1, across three quiet scan cycles (seam 10.0.13).
    /// </summary>
    [Fact]
    public async Task RunAsync_prints_brotli_warning_once_across_three_cycles_on_10_0_13()
    {
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(new Version(10, 0, 13));
        BrotliRuntimeWarningWriter.ResetForTests();

        var scans = 0;
        using var cts = new CancellationTokenSource();
        var original = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            var code = await WatchCommandHandler.RunAsync(
                seconds: 5,
                runScan: _ =>
                {
                    scans++;
                    // Quiet scans would suppress warnings; the latch must already have fired at startup.
                    BrotliRuntimeWarningWriter.WriteToStderrIfNeeded(
                        quiet: true,
                        DecompressionPolicy.GetCurrentStatus());
                    return Task.FromResult(0);
                },
                timeProvider: TimeProvider.System,
                delayAsync: async (_, ct) =>
                {
                    if (scans >= 3)
                    {
                        await cts.CancelAsync();
                        ct.ThrowIfCancellationRequested();
                    }
                },
                cancellationToken: cts.Token);

            Assert.Equal((int)PienExitCode.Cancelled, code);
            Assert.Equal(3, scans);
            var text = captured.ToString();
            var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            Assert.StartsWith("Watching every 5s.", lines[0], StringComparison.Ordinal);
            Assert.Equal(ExpectedWarningLine(new Version(10, 0, 13)), lines[1]);
            Assert.StartsWith("── watch cycle 1 @", lines[2], StringComparison.Ordinal);
            Assert.Equal(1, CountWarningLines(text));
        }
        finally
        {
            Console.SetError(original);
            DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
            BrotliRuntimeWarningWriter.ResetForTests();
        }
    }

    /// <summary>
    /// Real <see cref="WatchCommandHandler.RunAsync"/> prints no Brotli warning on seam 10.0.14.
    /// </summary>
    [Fact]
    public async Task RunAsync_prints_no_brotli_warning_on_10_0_14()
    {
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(new Version(10, 0, 14));
        BrotliRuntimeWarningWriter.ResetForTests();

        var scans = 0;
        using var cts = new CancellationTokenSource();
        var original = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            var code = await WatchCommandHandler.RunAsync(
                seconds: 5,
                runScan: _ =>
                {
                    scans++;
                    return Task.FromResult(0);
                },
                timeProvider: TimeProvider.System,
                delayAsync: async (_, ct) =>
                {
                    if (scans >= 3)
                    {
                        await cts.CancelAsync();
                        ct.ThrowIfCancellationRequested();
                    }
                },
                cancellationToken: cts.Token);

            Assert.Equal((int)PienExitCode.Cancelled, code);
            Assert.Equal(3, scans);
            Assert.Equal(0, CountWarningLines(captured.ToString()));
        }
        finally
        {
            Console.SetError(original);
            DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
            BrotliRuntimeWarningWriter.ResetForTests();
        }
    }

    /// <summary>
    /// Counts stderr lines that use the CLI <c>warning:</c> prefix.
    /// </summary>
    private static int CountWarningLines(string stderr)
        => stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.StartsWith("warning:", StringComparison.Ordinal));
}
