namespace ARTR.Pien.Findings;

/// <summary>
/// Unique identifier for a finding within a scan run.
/// </summary>
/// <param name="Value">Canonical finding identifier value.</param>
public sealed record FindingId(string Value)
{
    /// <summary>
    /// Creates a validated <see cref="FindingId"/>.
    /// </summary>
    /// <param name="value">Candidate identifier.</param>
    /// <returns>A validated finding identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
    public static FindingId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new FindingId(value.Trim());
    }

    /// <summary>
    /// Creates a new random finding identifier.
    /// </summary>
    /// <returns>A new finding identifier.</returns>
    public static FindingId NewId() => new(Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public override string ToString() => Value;
}
