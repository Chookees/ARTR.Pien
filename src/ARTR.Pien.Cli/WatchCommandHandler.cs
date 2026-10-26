using ARTR.Pien.Web.Transport;

namespace ARTR.Pien.Cli;

/// <summary>
/// Watch-command diagnostics that quiet per-cycle scans would otherwise suppress.
/// </summary>
internal static class WatchCommandHandler
{
    /// <summary>
    /// Emits the one-time Brotli runtime warning at watch start (scans run with <c>--quiet</c>).
    /// Watch has no quiet flag today, so this always attempts to surface the warning.
    /// </summary>
    internal static void WriteStartupDiagnostics()
        => BrotliRuntimeWarningWriter.WriteToStderrIfNeeded(
            quiet: false,
            DecompressionPolicy.GetCurrentStatus());
}
