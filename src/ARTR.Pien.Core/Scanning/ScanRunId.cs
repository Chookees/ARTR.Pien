namespace ARTR.Pien.Scanning;

/// <summary>
/// Unique identifier for a scan run.
/// </summary>
/// <param name="Value">Canonical run identifier value.</param>
public sealed record ScanRunId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="ScanRunId"/>.
    /// </summary>
    /// <param name="value">Candidate identifier.</param>
    /// <returns>A validated scan run identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
    public static ScanRunId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new ScanRunId(value.Trim());
    }

    /// <summary>
    /// Creates a new random scan run identifier.
    /// </summary>
    /// <returns>A new scan run identifier.</returns>
    public static ScanRunId NewId() => new(Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public override string ToString() => Value;
}
