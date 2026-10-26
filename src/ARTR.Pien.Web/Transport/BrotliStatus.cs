namespace ARTR.Pien.Web.Transport;

/// <summary>
/// Read-only status of the runtime-gated Brotli response decompression policy.
/// </summary>
/// <param name="Enabled">True when AutomaticDecompression includes Brotli for the detected runtime.</param>
/// <param name="DetectedRuntime">Runtime version used for the gate (override or <see cref="Environment.Version"/>).</param>
/// <param name="RequiredRuntime">Minimum patched runtime (CVE-2026-111497), currently 10.0.14.</param>
/// <param name="Warning">Operator-facing warning when Brotli is off; null when enabled.</param>
public readonly record struct BrotliStatus(
    bool Enabled,
    Version DetectedRuntime,
    Version RequiredRuntime,
    string? Warning);
