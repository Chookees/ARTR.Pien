using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ARTR.Pien.Checks;

/// <summary>
/// Stable, order-independent identifier for a check (for example <c>PIEN-HTTP-001</c>).
/// </summary>
/// <param name="Value">Canonical check identifier value.</param>
public sealed record CheckId(string Value)
{
    private static readonly Regex Pattern = new(
        @"^PIEN-[A-Z][A-Z0-9]*-\d{3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    /// <summary>
    /// Creates a validated <see cref="CheckId"/>.
    /// </summary>
    /// <param name="value">Candidate identifier.</param>
    /// <returns>A validated check identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or malformed.</exception>
    public static CheckId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (!Pattern.IsMatch(trimmed))
        {
            throw new ArgumentException(
                $"Check ID '{trimmed}' must match the pattern PIEN-<CATEGORY>-<NNN>.",
                nameof(value));
        }

        return new CheckId(trimmed);
    }

    /// <summary>
    /// Attempts to create a <see cref="CheckId"/> without throwing.
    /// </summary>
    /// <param name="value">Candidate identifier.</param>
    /// <param name="checkId">The created identifier when parsing succeeds.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out CheckId? checkId)
    {
        checkId = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!Pattern.IsMatch(trimmed))
        {
            return false;
        }

        checkId = new CheckId(trimmed);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
