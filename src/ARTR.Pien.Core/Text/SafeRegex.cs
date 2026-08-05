using System.Text.RegularExpressions;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Text;

/// <summary>
/// Bounded regex helper that always applies an explicit match timeout.
/// </summary>
public static class SafeRegex
{
    /// <summary>
    /// Returns whether <paramref name="input"/> matches <paramref name="pattern"/> within the scan regex timeout.
    /// </summary>
    public static bool IsMatch(string input, string pattern, ScanLimits limits, RegexOptions options = RegexOptions.CultureInvariant)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(limits);
        try
        {
            return Regex.IsMatch(input, pattern, options, limits.RegexTimeout);
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new PienTimeoutException("Regular expression match timed out.", ex);
        }
    }

    /// <summary>
    /// Creates a timeout-bounded regex.
    /// </summary>
    public static Regex Create(string pattern, ScanLimits limits, RegexOptions options = RegexOptions.CultureInvariant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(limits);
        return new Regex(pattern, options, limits.RegexTimeout);
    }
}
