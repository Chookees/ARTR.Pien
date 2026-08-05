using System.Net;
using System.Text;

using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace ARTR.Pien.Checks.Api;

internal static class OpenApiCheckSupport
{
    public static async Task<(OpenApiDocument? Document, string? Error)> LoadAsync(
        string? relativeOrAbsolute,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            return (null, null);
        }

        var path = Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.GetFullPath(Path.Combine(workingDirectory, relativeOrAbsolute));
        if (!File.Exists(path))
        {
            return (null, $"OpenAPI document missing: {path}");
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var format = path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                ? "yaml"
                : "json";
            var settings = new OpenApiReaderSettings();
            settings.AddYamlReader();
            var readResult = await OpenApiDocument.LoadAsync(stream, format, settings, cancellationToken).ConfigureAwait(false);
            if (readResult.Document is null)
            {
                var errors = string.Join("; ", readResult.Diagnostic?.Errors.Select(e => e.Message) ?? []);
                return (null, string.IsNullOrWhiteSpace(errors) ? "OpenAPI parse failed." : errors);
            }

            return (readResult.Document, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ex.Message);
        }
    }

    public static IEnumerable<(string Path, string Method, string? OperationId, OpenApiOperation Operation)> EnumerateOperations(OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            yield break;
        }

        foreach (var (pathKey, pathItem) in document.Paths)
        {
            if (pathItem?.Operations is null)
            {
                continue;
            }

            foreach (var (method, operation) in pathItem.Operations)
            {
                if (operation is null)
                {
                    continue;
                }

                yield return (pathKey, method.ToString().ToUpperInvariant(), operation.OperationId, operation);
            }
        }
    }
}

/// <summary>OpenAPI operationId uniqueness and server URL hygiene.</summary>
public sealed class OpenApiHygieneCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.OpenApi002),
        Name = "OpenAPI hygiene",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Flags duplicate operationId values and insecure http server URLs in OpenAPI documents.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public async Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(context.Target.OpenApiDocument))
        {
            return ApiCheckHelpers.NotApplicable(Definition);
        }

        var workingDirectory = evidence.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var (document, error) = await OpenApiCheckSupport.LoadAsync(context.Target.OpenApiDocument, workingDirectory, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return ApiCheckHelpers.Fail(Definition, context, "OpenAPI hygiene inspect failed", error ?? "parse failed", FindingSeverity.Medium);
        }

        var operationIds = OpenApiCheckSupport.EnumerateOperations(document)
            .Select(o => o.OperationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToArray();
        var duplicate = operationIds.GroupBy(id => id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return ApiCheckHelpers.Fail(
                Definition,
                context,
                "Duplicate OpenAPI operationId",
                $"operationId '{duplicate.Key}' is not unique.",
                FindingSeverity.Medium);
        }

        if (document.Servers is not null)
        {
            foreach (var server in document.Servers)
            {
                if (server?.Url is string url &&
                    url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiCheckHelpers.Fail(
                        Definition,
                        context,
                        "Insecure OpenAPI server URL",
                        $"Server URL '{url}' uses http.",
                        FindingSeverity.Medium);
                }
            }
        }

        return ApiCheckHelpers.Pass(Definition);
    }
}

/// <summary>Response status/content-type vs OpenAPI operation.</summary>
public sealed class OpenApiResponseConformanceCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.OpenApi003),
        Name = "OpenAPI response conformance",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.Low,
        Description = "Compares configured API case responses to documented OpenAPI status codes and content types.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public async Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(context.Target.OpenApiDocument) || evidence.ApiCaseResults.Count == 0)
        {
            return ApiCheckHelpers.NotApplicable(Definition);
        }

        var workingDirectory = evidence.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var (document, error) = await OpenApiCheckSupport.LoadAsync(context.Target.OpenApiDocument, workingDirectory, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return ApiCheckHelpers.Fail(Definition, context, "OpenAPI conformance inspect failed", error ?? "parse failed", FindingSeverity.Low);
        }

        var casesById = context.Target.ApiCases.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var result in evidence.ApiCaseResults)
        {
            if (result.Blocked || result.Probe?.StatusCode is null || !casesById.TryGetValue(result.CaseId, out var apiCase))
            {
                continue;
            }

            var operation = OpenApiCheckSupport.EnumerateOperations(document)
                .FirstOrDefault(o =>
                    PathsMatch(o.Path, apiCase.Path) &&
                    string.Equals(o.Method, apiCase.Method, StringComparison.OrdinalIgnoreCase));
            if (operation.Operation is null)
            {
                continue;
            }

            var statusKey = ((int)result.Probe.StatusCode.Value).ToString();
            var responses = operation.Operation.Responses;
            if (responses is null || (!responses.ContainsKey(statusKey) && !responses.ContainsKey("default")))
            {
                return ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"Undocumented status for '{result.CaseId}'",
                    $"Status {statusKey} is not documented for {apiCase.Method} {apiCase.Path}.",
                    FindingSeverity.Low);
            }

            if (responses.TryGetValue(statusKey, out var response) &&
                response?.Content is { Count: > 0 } content)
            {
                var contentType = result.Probe.ContentType ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(contentType) &&
                    !content.Keys.Any(k => contentType.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(k, "*/*", StringComparison.Ordinal)))
                {
                    return ApiCheckHelpers.Fail(
                        Definition,
                        context,
                        $"Content-Type mismatch for '{result.CaseId}'",
                        $"Observed '{contentType}' is not among documented content types.",
                        FindingSeverity.Low);
                }
            }
        }

        return ApiCheckHelpers.Pass(Definition);
    }

    private static bool PathsMatch(string openApiPath, string casePath)
    {
        var left = openApiPath.TrimEnd('/');
        var right = casePath.TrimEnd('/');
        if (!right.StartsWith('/'))
        {
            right = "/" + right;
        }

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Coverage of configured operations vs OpenAPI document.</summary>
public sealed class OpenApiCoverageCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.OpenApi004),
        Name = "OpenAPI operation coverage",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.Info,
        Description = "Advises when configured API cases cover only a subset of documented OpenAPI operations.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public async Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(context.Target.OpenApiDocument))
        {
            return ApiCheckHelpers.NotApplicable(Definition);
        }

        var workingDirectory = evidence.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var (document, error) = await OpenApiCheckSupport.LoadAsync(context.Target.OpenApiDocument, workingDirectory, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return ApiCheckHelpers.Fail(Definition, context, "OpenAPI coverage inspect failed", error ?? "parse failed", FindingSeverity.Info);
        }

        var documented = OpenApiCheckSupport.EnumerateOperations(document)
            .Select(o => $"{o.Method} {o.Path}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (documented.Count == 0)
        {
            return ApiCheckHelpers.Pass(Definition);
        }

        var covered = context.Target.ApiCases
            .Select(c => $"{c.Method.Trim().ToUpperInvariant()} {NormalizePath(c.Path)}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = documented.Where(op => !covered.Contains(op)).ToArray();
        if (missing.Length > 0)
        {
            return ApiCheckHelpers.Fail(
                Definition,
                context,
                "OpenAPI operations not covered by cases",
                $"Uncovered {missing.Length}/{documented.Count}: {string.Join(", ", missing.Take(5))}",
                FindingSeverity.Info);
        }

        return ApiCheckHelpers.Pass(Definition);
    }

    private static string NormalizePath(string path)
    {
        var value = path.Trim();
        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        return value.TrimEnd('/');
    }
}
