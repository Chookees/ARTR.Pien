using ARTR.Pien.Web.Transport;

namespace ARTR.Pien.Cli;

/// <summary>
/// Surfaces the unpatched-runtime Brotli warning on the CLI stderr channel.
/// </summary>
internal static class BrotliRuntimeWarningWriter
{
    private static int _written;

    /// <summary>
    /// Clears the once-per-process stderr latch for unit tests.
    /// </summary>
    internal static void ResetForTests()
        => Interlocked.Exchange(ref _written, 0);

    /// <summary>
    /// Writes <c>warning: …</c> to <see cref="Console.Error"/> from <paramref name="status"/>
    /// at most once per process, unless <paramref name="quiet"/> is set or Brotli is enabled.
    /// </summary>
    /// <param name="quiet">When true, matches <c>--quiet</c> and suppresses stderr progress/warnings.</param>
    /// <param name="status">Brotli gate status from <see cref="DecompressionPolicy.GetStatus"/>.</param>
    internal static void WriteToStderrIfNeeded(bool quiet, BrotliStatus status)
    {
        if (quiet || status.Warning is null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _written, 1) != 0)
        {
            return;
        }

        Console.Error.WriteLine("warning: " + status.Warning);
    }
}
