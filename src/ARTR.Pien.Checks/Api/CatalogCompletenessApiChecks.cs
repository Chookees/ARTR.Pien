using System.Text;
using System.Text.Json;

using ARTR.Pien.Configuration;
using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Checks.Api;

/// <summary>Per-case expected status and content-type.</summary>
public sealed class ApiExpectedStatusContentTypeCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Api002),
        Name = "API expected status and content-type",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.High,
        Description = "Validates configured expected status and content-type for API cases.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Target.Kind != ScanTargetKind.Api)
        {
            return Task.FromResult(ApiCheckHelpers.NotApplicable(Definition));
        }

        if (evidence.ApiCaseResults.Count == 0)
        {
            return Task.FromResult(ApiCheckHelpers.Pass(Definition));
        }

        var casesById = context.Target.ApiCases.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var result in evidence.ApiCaseResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Blocked || result.Probe is null || !casesById.TryGetValue(result.CaseId, out var apiCase))
            {
                continue;
            }

            if (apiCase.ExpectedStatus is JsonElement statusElement &&
                result.Probe.StatusCode is { } code &&
                !MatchesExpectedStatus(statusElement, (int)code))
            {
                return Task.FromResult(ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"Unexpected status for '{result.CaseId}'",
                    $"Observed {(int)code} did not match expected status.",
                    FindingSeverity.High));
            }

            if (!string.IsNullOrWhiteSpace(apiCase.ExpectedContentType))
            {
                var contentType = result.Probe.ContentType ??
                                  (result.Probe.Headers.TryGetValue("Content-Type", out var ct) ? ct : string.Empty);
                if (!contentType.Contains(apiCase.ExpectedContentType, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(ApiCheckHelpers.Fail(
                        Definition,
                        context,
                        $"Unexpected content-type for '{result.CaseId}'",
                        $"Expected content-type containing '{apiCase.ExpectedContentType}'.",
                        FindingSeverity.High));
                }
            }
        }

        return Task.FromResult(ApiCheckHelpers.Pass(Definition));
    }

    private static bool MatchesExpectedStatus(JsonElement element, int code)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var single))
        {
            return code == single;
        }

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("min", out var minEl) &&
            element.TryGetProperty("max", out var maxEl) &&
            minEl.TryGetInt32(out var min) &&
            maxEl.TryGetInt32(out var max))
        {
            return code >= min && code <= max;
        }

        return true;
    }
}

/// <summary>JSON Pointer assertions for API cases.</summary>
public sealed class ApiJsonAssertionCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Api003),
        Name = "API JSON assertions",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Evaluates configured JSON Pointer assertions against API case responses.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Target.Kind != ScanTargetKind.Api)
        {
            return Task.FromResult(ApiCheckHelpers.NotApplicable(Definition));
        }

        var casesById = context.Target.ApiCases.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var result in evidence.ApiCaseResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Blocked || result.Probe is null || !casesById.TryGetValue(result.CaseId, out var apiCase) ||
                apiCase.JsonAssertions.Count == 0)
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(result.Probe.Body.Span));
                foreach (var assertion in apiCase.JsonAssertions)
                {
                    if (!JsonPointerAssertions.TryEvaluate(document.RootElement, assertion, context.Limits, out var message))
                    {
                        return Task.FromResult(ApiCheckHelpers.Fail(
                            Definition,
                            context,
                            $"JSON assertion failed for '{result.CaseId}'",
                            message ?? "JSON assertion failed.",
                            FindingSeverity.Medium));
                    }
                }
            }
            catch (JsonException)
            {
                return Task.FromResult(ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"JSON assertion parse failed for '{result.CaseId}'",
                    "Response body is not valid JSON for assertions.",
                    FindingSeverity.Medium));
            }
        }

        return Task.FromResult(ApiCheckHelpers.Pass(Definition));
    }
}

/// <summary>Local JSON Schema validation for API cases.</summary>
public sealed class ApiLocalJsonSchemaCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Api004),
        Name = "API local JSON Schema",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Validates API case responses against local JSON Schema documents (no remote $ref fetch).",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public async Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Target.Kind != ScanTargetKind.Api)
        {
            return ApiCheckHelpers.NotApplicable(Definition);
        }

        var workingDirectory = evidence.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var casesById = context.Target.ApiCases.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var result in evidence.ApiCaseResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Blocked || result.Probe is null || !casesById.TryGetValue(result.CaseId, out var apiCase) ||
                string.IsNullOrWhiteSpace(apiCase.JsonSchemaPath))
            {
                continue;
            }

            var path = Path.IsPathRooted(apiCase.JsonSchemaPath)
                ? apiCase.JsonSchemaPath
                : Path.GetFullPath(Path.Combine(workingDirectory, apiCase.JsonSchemaPath));
            if (!File.Exists(path))
            {
                return ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"JSON Schema missing for '{result.CaseId}'",
                    $"JSON Schema file not found: {apiCase.JsonSchemaPath}",
                    FindingSeverity.Medium);
            }

            var schemaJson = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var schemaErrors = await LocalJsonSchemaValidator.ValidateAsync(
                schemaJson,
                Encoding.UTF8.GetString(result.Probe.Body.Span),
                cancellationToken).ConfigureAwait(false);
            if (schemaErrors.Count > 0)
            {
                return ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"JSON Schema mismatch for '{result.CaseId}'",
                    string.Join(" ", schemaErrors.Take(3)),
                    FindingSeverity.Medium);
            }
        }

        return ApiCheckHelpers.Pass(Definition);
    }
}

/// <summary>Non-idempotent method guard.</summary>
public sealed class ApiNonIdempotentGuardCheck : ICheck
{
    private static readonly HashSet<string> NonIdempotentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Api005),
        Name = "API non-idempotent guard",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.High,
        Description = "Fails when non-idempotent API cases are blocked or configured without allowNonIdempotent.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Target.Kind != ScanTargetKind.Api)
        {
            return Task.FromResult(ApiCheckHelpers.NotApplicable(Definition));
        }

        var casesById = context.Target.ApiCases.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var result in evidence.ApiCaseResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Blocked &&
                (result.BlockReason?.Contains("Non-idempotent", StringComparison.OrdinalIgnoreCase) == true ||
                 NonIdempotentMethods.Contains(result.Method)))
            {
                return Task.FromResult(ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"Non-idempotent case '{result.CaseId}' blocked",
                    result.BlockReason ?? "Non-idempotent method blocked unless allowNonIdempotent=true.",
                    FindingSeverity.High));
            }

            if (!result.Blocked &&
                casesById.TryGetValue(result.CaseId, out var apiCase) &&
                NonIdempotentMethods.Contains(apiCase.Method) &&
                !apiCase.AllowNonIdempotent)
            {
                return Task.FromResult(ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"Non-idempotent case '{result.CaseId}' not allowed",
                    $"Method '{apiCase.Method}' requires allowNonIdempotent=true.",
                    FindingSeverity.High));
            }
        }

        foreach (var apiCase in context.Target.ApiCases)
        {
            if (NonIdempotentMethods.Contains(apiCase.Method) && !apiCase.AllowNonIdempotent &&
                evidence.ApiCaseResults.All(r => !string.Equals(r.CaseId, apiCase.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"Non-idempotent case '{apiCase.Id}' not allowed",
                    $"Method '{apiCase.Method}' requires allowNonIdempotent=true.",
                    FindingSeverity.High));
            }
        }

        return Task.FromResult(ApiCheckHelpers.Pass(Definition));
    }
}
