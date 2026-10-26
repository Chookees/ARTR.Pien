using ARTR.Pien.UnitTests.Brotli;
using ARTR.Pien.Web.Transport;

namespace ARTR.Pien.UnitTests.Cli;

/// <summary>
/// Proves <see cref="ARTR.Pien.Cli.ScanCommandHandler"/> surfaces the Brotli warning before the first request.
/// </summary>
[Collection(nameof(BrotliDiagnosticsCollection))]
public sealed class ScanCommandBrotliWarningWiringTests : IDisposable
{
    /// <summary>
    /// Resets Brotli CLI/Web seams before each wiring case.
    /// </summary>
    public ScanCommandBrotliWarningWiringTests()
    {
        ARTR.Pien.Cli.BrotliRuntimeWarningWriter.ResetForTests();
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        DecompressionPolicy.ResetWarningLatchForTests();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ARTR.Pien.Cli.BrotliRuntimeWarningWriter.ResetForTests();
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
        DecompressionPolicy.ResetWarningLatchForTests();
    }

    /// <summary>
    /// On seam 10.0.13, a non-quiet scan writes exactly one Brotli <c>warning:</c> line to stderr.
    /// </summary>
    [Fact]
    public async Task Scan_writes_brotli_warning_once_before_request_on_10_0_13()
    {
        DecompressionPolicy.SetRuntimeVersionOverrideForTests(new Version(10, 0, 13));
        ARTR.Pien.Cli.BrotliRuntimeWarningWriter.ResetForTests();

        var root = Path.Combine(Path.GetTempPath(), "pien-scan-br-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previous = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(root);
            Console.SetOut(stdout);
            Console.SetError(stderr);

            // Valid enough to pass validation; connection failure happens after the warning is written.
            var code = await ARTR.Pien.Cli.Program.RunAsync(
                ["scan", "--target", "http://127.0.0.1:9/", "--confirm-authorization", "--max-pages", "1", "--max-depth", "0"],
                TestContext.Current.CancellationToken);

            Assert.True(code != 0, "Expected a non-success exit from the unreachable target.");
            var text = stderr.ToString();
            var required = DecompressionPolicy.MinimumPatchedRuntime.ToString(3);
            var expected = "warning: Brotli decompression disabled: runtime 10.0.13 is older than " + required
                + " (CVE-2026-111497). 'br' is not advertised; responses with Content-Encoding: br are returned undecoded.";
            Assert.Contains(expected, text, StringComparison.Ordinal);
            Assert.Equal(1, text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.StartsWith("warning:", StringComparison.Ordinal)));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(root, recursive: true);
            DecompressionPolicy.SetRuntimeVersionOverrideForTests(null);
            ARTR.Pien.Cli.BrotliRuntimeWarningWriter.ResetForTests();
        }
    }
}
