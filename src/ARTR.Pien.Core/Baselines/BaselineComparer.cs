using System.Security.Cryptography;
using System.Text;

using ARTR.Pien.Findings;
using ARTR.Pien.Policy;

namespace ARTR.Pien.Baselines;

/// <summary>
/// Deterministic finding fingerprint helpers used by baseline create/compare and report diffs.
/// </summary>
public static class FindingFingerprint
{
    /// <summary>
    /// Computes a stable fingerprint for a finding used in baseline comparison.
    /// </summary>
    public static string Compute(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var material = $"{finding.CheckId}|{finding.Title}|{finding.Status}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}

/// <summary>
/// Builds baseline comparison rows for report documents.
/// </summary>
public static class BaselineComparer
{
    /// <summary>
    /// Compares current findings against a stored baseline fingerprint set.
    /// </summary>
    public static IReadOnlyList<BaselineComparison> Compare(Baseline baseline, IReadOnlyList<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(findings);

        var currentMap = new Dictionary<string, Finding>(StringComparer.OrdinalIgnoreCase);
        foreach (var finding in findings)
        {
            var fp = FindingFingerprint.Compute(finding);
            currentMap.TryAdd(fp, finding);
        }

        var baselineSet = baseline.FindingFingerprints.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<BaselineComparison>();

        foreach (var (fp, finding) in currentMap)
        {
            var kind = baselineSet.Contains(fp) ? BaselineComparisonKind.Unchanged : BaselineComparisonKind.New;
            rows.Add(BaselineComparison.Create(new BaselineComparison
            {
                FindingFingerprint = fp,
                Kind = kind,
                CurrentFindingId = finding.Id,
            }));
        }

        foreach (var fp in baselineSet)
        {
            if (currentMap.ContainsKey(fp))
            {
                continue;
            }

            rows.Add(BaselineComparison.Create(new BaselineComparison
            {
                FindingFingerprint = fp,
                Kind = BaselineComparisonKind.Resolved,
            }));
        }

        return rows
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.FindingFingerprint, StringComparer.Ordinal)
            .ToArray();
    }
}
