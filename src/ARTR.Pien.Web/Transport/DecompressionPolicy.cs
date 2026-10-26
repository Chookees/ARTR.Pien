using System.Diagnostics;
using System.Net;

namespace ARTR.Pien.Web.Transport;

/// <summary>
/// Chooses <see cref="DecompressionMethods"/> for the safe HTTP transport based on the
/// running .NET runtime version, so Brotli is only enabled on patched hosts.
/// </summary>
public static class DecompressionPolicy
{
    /// <summary>
    /// Minimum .NET runtime that includes the CVE-2026-111497 / GHSA-p3jp-9v83-76w9 fix
    /// (upstream google/brotli GHSA-9v8h-qqgm-4m35). Below this version Brotli stays off.
    /// </summary>
    public static readonly Version MinimumPatchedRuntime = new(10, 0, 14);

    private static readonly AsyncLocal<Version?> RuntimeVersionOverride = new();

    private static int _unpatchedWarningEmitted;

    /// <summary>
    /// Optional warning sink for tests. When null, the library default is
    /// <see cref="Trace.TraceWarning(string?)"/> only (never <see cref="Console.Error"/>).
    /// </summary>
    internal static Action<string>? WarningEmitter { get; set; }

    /// <summary>
    /// Runtime version used for the Brotli gate: test override when set, otherwise <see cref="Environment.Version"/>.
    /// </summary>
    internal static Version CurrentRuntimeVersion => RuntimeVersionOverride.Value ?? Environment.Version;

    /// <summary>
    /// Sets a test-only runtime version override for the current async flow; pass null to clear.
    /// Thread-safe across parallel tests via <see cref="AsyncLocal{T}"/>.
    /// </summary>
    /// <param name="version">Injected runtime version, or null to restore <see cref="Environment.Version"/>.</param>
    internal static void SetRuntimeVersionOverrideForTests(Version? version)
        => RuntimeVersionOverride.Value = version;

    /// <summary>
    /// Clears the one-time unpatched-runtime warning latch so unit tests can re-assert emission.
    /// </summary>
    internal static void ResetWarningLatchForTests()
        => Interlocked.Exchange(ref _unpatchedWarningEmitted, 0);

    /// <summary>
    /// Formats the one-time warning logged when Brotli stays disabled on an unpatched runtime.
    /// The threshold is taken from <see cref="MinimumPatchedRuntime"/> (not a separate literal).
    /// </summary>
    /// <param name="runtime">Detected runtime version included in the message.</param>
    /// <returns>Exact operator-facing warning text.</returns>
    internal static string FormatUnpatchedRuntimeWarning(Version runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return $"Brotli decompression disabled: runtime {runtime} is older than {MinimumPatchedRuntime.ToString(3)} (CVE-2026-111497). 'br' is not advertised; responses with Content-Encoding: br are returned undecoded.";
    }

    /// <summary>
    /// Returns the Brotli gate status for <paramref name="runtime"/> without emitting diagnostics.
    /// </summary>
    /// <param name="runtime">Runtime version to evaluate.</param>
    /// <returns>Enabled flag, versions, and warning text when Brotli stays off.</returns>
    public static BrotliStatus GetStatus(Version runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (runtime >= MinimumPatchedRuntime)
        {
            return new BrotliStatus(
                Enabled: true,
                DetectedRuntime: runtime,
                RequiredRuntime: MinimumPatchedRuntime,
                Warning: null);
        }

        return new BrotliStatus(
            Enabled: false,
            DetectedRuntime: runtime,
            RequiredRuntime: MinimumPatchedRuntime,
            Warning: FormatUnpatchedRuntimeWarning(runtime));
    }

    /// <summary>
    /// Returns the Brotli gate status for <see cref="CurrentRuntimeVersion"/> without emitting diagnostics.
    /// </summary>
    /// <returns>Status for the effective runtime (override or <see cref="Environment.Version"/>).</returns>
    public static BrotliStatus GetCurrentStatus()
        => GetStatus(CurrentRuntimeVersion);

    /// <summary>
    /// Selects AutomaticDecompression flags for <paramref name="runtime"/>.
    /// Enables Brotli only when <paramref name="runtime"/> is at least
    /// <see cref="MinimumPatchedRuntime"/>; otherwise keeps GZip|Deflate and emits a one-time Trace warning.
    /// </summary>
    /// <param name="runtime">Runtime version under test or from <see cref="Environment.Version"/>.</param>
    /// <returns>Decompression methods safe for that runtime.</returns>
    internal static DecompressionMethods Select(Version runtime)
    {
        var status = GetStatus(runtime);
        if (!status.Enabled && status.Warning is { } warning)
        {
            EmitUnpatchedWarningOnce(warning);
        }

        return status.Enabled
            ? DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            : DecompressionMethods.GZip | DecompressionMethods.Deflate;
    }

    /// <summary>
    /// Selects decompression methods for <see cref="CurrentRuntimeVersion"/> (production default).
    /// </summary>
    /// <returns>Decompression methods for the effective runtime version.</returns>
    internal static DecompressionMethods SelectForCurrentRuntime()
        => Select(CurrentRuntimeVersion);

    /// <summary>
    /// Emits the unpatched-runtime Brotli warning at most once per process (thread-safe).
    /// Library default is Trace only; hosts (CLI/Hosting) surface the same text via <see cref="GetStatus"/>.
    /// </summary>
    /// <param name="message">Exact warning text to emit.</param>
    private static void EmitUnpatchedWarningOnce(string message)
    {
        if (Interlocked.Exchange(ref _unpatchedWarningEmitted, 1) != 0)
        {
            return;
        }

        if (WarningEmitter is { } emitter)
        {
            emitter(message);
            return;
        }

        Trace.TraceWarning(message);
    }
}
