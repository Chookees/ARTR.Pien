using ARTR.Pien.Web.Transport;

using Microsoft.Extensions.Logging;

namespace ARTR.Pien.Hosting;

/// <summary>
/// Surfaces the unpatched-runtime Brotli warning on the hosting ILogger channel.
/// Hosting has no health/diagnostics endpoint or UI notice surface; ILogger at composition time is the existing channel.
/// </summary>
internal static class BrotliRuntimeHostDiagnostics
{
    /// <summary>
    /// Logs <paramref name="status"/>.Warning at Warning level when Brotli is disabled and a logger is available.
    /// </summary>
    /// <param name="logger">Optional host logger; null skips (hosts that did not call AddLogging).</param>
    /// <param name="status">Brotli gate status from <see cref="DecompressionPolicy.GetStatus"/>.</param>
    internal static void LogIfDisabled(ILogger? logger, BrotliStatus status)
    {
        if (logger is null || status.Warning is null)
        {
            return;
        }

        logger.LogWarning("{Warning}", status.Warning);
    }
}
