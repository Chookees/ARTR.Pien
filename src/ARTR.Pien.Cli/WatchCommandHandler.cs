using ARTR.Pien.Web.Transport;

namespace ARTR.Pien.Cli;

/// <summary>
/// Interval watch loop for <c>pien watch</c>, including startup diagnostics quiet scans suppress.
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

    /// <summary>
    /// Runs the watch loop: prints the Watching banner, emits startup diagnostics once,
    /// then repeatedly invokes <paramref name="runScan"/> with bounded delays until cancelled.
    /// </summary>
    /// <param name="seconds">Interval between cycles (already clamped by the CLI).</param>
    /// <param name="runScan">Per-cycle scan invoker (production passes quiet <c>scan</c>).</param>
    /// <param name="timeProvider">Clock for cycle timestamps and next-run scheduling.</param>
    /// <param name="delayAsync">Delay seam so tests avoid real wall-clock waits.</param>
    /// <param name="cancellationToken">Stops the loop (Ctrl+C / test cancel).</param>
    /// <returns>Cancelled exit code when the loop ends or a cycle returns cancelled.</returns>
    internal static async Task<int> RunAsync(
        int seconds,
        Func<CancellationToken, Task<int>> runScan,
        TimeProvider timeProvider,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runScan);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(delayAsync);

        Console.Error.WriteLine($"Watching every {seconds}s. Press Ctrl+C to cancel.");
        // Scans run with --quiet (progress + warnings suppressed); emit Brotli status once at watch start.
        WriteStartupDiagnostics();

        var cycle = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            cycle++;
            var now = timeProvider.GetUtcNow();
            Console.Error.WriteLine($"── watch cycle {cycle} @ {now:O} ──");
            var next = now.AddSeconds(seconds);
            var code = await runScan(cancellationToken).ConfigureAwait(false);
            if (code == (int)PienExitCode.Cancelled)
            {
                return code;
            }

            Console.Error.WriteLine($"Next run at {next:O}");
            var delay = next - timeProvider.GetUtcNow();
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.FromSeconds(seconds);
            }

            try
            {
                await delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return (int)PienExitCode.Cancelled;
            }
        }

        return (int)PienExitCode.Cancelled;
    }
}
