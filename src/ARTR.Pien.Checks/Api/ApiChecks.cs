using System.Text;
using System.Text.Json;

using ARTR.Pien.Configuration;
using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Text;

using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace ARTR.Pien.Checks.Api;

internal static class ApiCheckHelpers
{
    public static CheckResult Pass(CheckDefinition definition)
        => CheckResult.Create(new CheckResult
        {
            CheckId = definition.Id,
            Status = FindingStatus.Pass,
            Findings = [],
        });

    public static CheckResult NotApplicable(CheckDefinition definition)
        => CheckResult.Create(new CheckResult
        {
            CheckId = definition.Id,
            Status = FindingStatus.NotApplicable,
            Findings = [],
        });

    public static CheckResult Fail(
        CheckDefinition definition,
        ScanContext context,
        string title,
        string detail,
        FindingSeverity severity)
    {
        var finding = Finding.Create(new Finding
        {
            Id = FindingId.NewId(),
            CheckId = definition.Id.Value,
            RuleVersion = definition.RuleVersion,
            Title = title,
            Summary = title,
            Explanation = detail,
            Severity = severity,
            Status = FindingStatus.Fail,
            TargetId = context.Target.Id,
            Evidence = [],
            Timestamp = context.UtcNow(),
            RunId = context.RunId,
            Observed = detail,
        });

        return CheckResult.Create(new CheckResult
        {
            CheckId = definition.Id,
            Status = FindingStatus.Fail,
            Findings = [finding],
        });
    }
}

/// <summary>
/// JSON Pointer assertion evaluator (RFC 6901).
/// </summary>
public static class JsonPointerAssertions
{
    /// <summary>
    /// Evaluates an assertion against a JSON element.
    /// </summary>
    public static bool TryEvaluate(
        JsonElement root,
        PienJsonAssertionConfiguration assertion,
        ScanLimits limits,
        out string? failureMessage)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        ArgumentNullException.ThrowIfNull(limits);
        failureMessage = null;
        if (!TryResolve(root, assertion.Pointer, out var element, out var missing))
        {
            if (string.Equals(assertion.Op, "absent", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            failureMessage = missing;
            return false;
        }

        return EvaluateResolved(element, assertion, limits, out failureMessage);
    }

    private static bool EvaluateResolved(
        JsonElement element,
        PienJsonAssertionConfiguration assertion,
        ScanLimits limits,
        out string? failureMessage)
    {
        failureMessage = null;
        return assertion.Op.Trim().ToLowerInvariant() switch
        {
            "exists" => true,
            "absent" => Fail($"Pointer '{assertion.Pointer}' exists but should be absent.", out failureMessage),
            "eq" => EvaluateEq(element, assertion, out failureMessage),
            "contains" => EvaluateContains(element, assertion, out failureMessage),
            "type" => MatchesType(element, assertion.TypeName) || Fail($"Pointer '{assertion.Pointer}' type mismatch.", out failureMessage),
            "range" => EvaluateRange(element, assertion, out failureMessage),
            "length" => EvaluateLength(element, assertion, out failureMessage),
            "regex" => EvaluateRegex(element, assertion, limits, out failureMessage),
            _ => Fail($"Unknown assertion op '{assertion.Op}'.", out failureMessage),
        };
    }

    private static bool EvaluateEq(JsonElement element, PienJsonAssertionConfiguration assertion, out string? failureMessage)
    {
        if (assertion.Value is null)
        {
            return Fail("eq assertion requires value.", out failureMessage);
        }

        if (!string.Equals(element.GetRawText(), assertion.Value.Value.GetRawText(), StringComparison.Ordinal))
        {
            return Fail($"Pointer '{assertion.Pointer}' value mismatch.", out failureMessage);
        }

        failureMessage = null;
        return true;
    }

    private static bool EvaluateContains(JsonElement element, PienJsonAssertionConfiguration assertion, out string? failureMessage)
    {
        if (element.ValueKind != JsonValueKind.String || assertion.Value is null)
        {
            return Fail($"Pointer '{assertion.Pointer}' contains requires string.", out failureMessage);
        }

        if (!element.GetString()!.Contains(assertion.Value.Value.ToString(), StringComparison.Ordinal))
        {
            return Fail($"Pointer '{assertion.Pointer}' does not contain expected value.", out failureMessage);
        }

        failureMessage = null;
        return true;
    }

    private static bool EvaluateRange(JsonElement element, PienJsonAssertionConfiguration assertion, out string? failureMessage)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            return Fail($"Pointer '{assertion.Pointer}' out of range.", out failureMessage);
        }

        var number = element.GetDouble();
        if ((assertion.Min is double min && number < min) || (assertion.Max is double max && number > max))
        {
            return Fail($"Pointer '{assertion.Pointer}' out of range.", out failureMessage);
        }

        failureMessage = null;
        return true;
    }

    private static bool EvaluateLength(JsonElement element, PienJsonAssertionConfiguration assertion, out string? failureMessage)
    {
        var length = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()?.Length ?? 0,
            JsonValueKind.Array => element.GetArrayLength(),
            _ => -1,
        };
        if (length < 0 ||
            (assertion.Min is double minLen && length < minLen) ||
            (assertion.Max is double maxLen && length > maxLen))
        {
            return Fail($"Pointer '{assertion.Pointer}' length out of range.", out failureMessage);
        }

        failureMessage = null;
        return true;
    }

    private static bool EvaluateRegex(
        JsonElement element,
        PienJsonAssertionConfiguration assertion,
        ScanLimits limits,
        out string? failureMessage)
    {
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(assertion.Pattern))
        {
            return Fail($"Pointer '{assertion.Pointer}' regex requires string and pattern.", out failureMessage);
        }

        if (!SafeRegex.IsMatch(element.GetString()!, assertion.Pattern!, limits))
        {
            return Fail($"Pointer '{assertion.Pointer}' regex mismatch.", out failureMessage);
        }

        failureMessage = null;
        return true;
    }

    private static bool Fail(string message, out string? failureMessage)
    {
        failureMessage = message;
        return false;
    }

    private static bool TryResolve(JsonElement root, string pointer, out JsonElement element, out string? error)
    {
        element = root;
        error = null;
        if (string.IsNullOrEmpty(pointer) || pointer == "/")
        {
            return true;
        }

        if (!pointer.StartsWith('/'))
        {
            error = $"JSON Pointer must start with '/': '{pointer}'.";
            return false;
        }

        foreach (var raw in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (!element.TryGetProperty(token, out element))
                {
                    error = $"Pointer '{pointer}' not found.";
                    return false;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array && int.TryParse(token, out var index))
            {
                if (index < 0 || index >= element.GetArrayLength())
                {
                    error = $"Pointer '{pointer}' index out of range.";
                    return false;
                }

                element = element[index];
            }
            else
            {
                error = $"Pointer '{pointer}' not found.";
                return false;
            }
        }

        return true;
    }

    private static bool MatchesType(JsonElement element, string? typeName)
        => typeName?.Trim().ToLowerInvariant() switch
        {
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _),
            "boolean" => element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "object" => element.ValueKind == JsonValueKind.Object,
            "array" => element.ValueKind == JsonValueKind.Array,
            "null" => element.ValueKind == JsonValueKind.Null,
            _ => false,
        };
}

/// <summary>
/// Local-only JSON Schema validation (no remote $ref fetch).
/// </summary>
public static class LocalJsonSchemaValidator
{
    /// <summary>
    /// Validates instance JSON against a local schema document asynchronously.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ValidateAsync(
        string schemaJson,
        string instanceJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);
        ArgumentNullException.ThrowIfNull(instanceJson);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var schema = await NJsonSchema.JsonSchema.FromJsonAsync(schemaJson, cancellationToken).ConfigureAwait(false);
            var errors = schema.Validate(instanceJson);
            return errors.Select(e => e.ToString()).ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [$"JSON Schema validation failed: {ex.Message}"];
        }
    }
}

/// <summary>API response contract fundamentals including configured cases.</summary>
public sealed class ApiContractCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Api001),
        Name = "API contract",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.High,
        Description = "Validates primary API probe and configured cases (status, JSON Pointer, local schema, non-idempotent guard).",
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

        if (evidence.ApiCaseResults.Count == 0)
        {
            var primary = evidence.Probes.TryGetValue("primary", out var p) ? p : null;
            if (primary?.StatusCode is null || (int)primary.StatusCode.Value >= 400)
            {
                return ApiCheckHelpers.Fail(Definition, context, "API probe failed", "Primary API probe did not return a successful status.", FindingSeverity.High);
            }

            return ApiCheckHelpers.Pass(Definition);
        }

        var casesById = context.Target.ApiCases.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var result in evidence.ApiCaseResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Blocked)
            {
                return ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"API case '{result.CaseId}' blocked",
                    result.BlockReason ?? "Case blocked.",
                    FindingSeverity.High);
            }

            if (!casesById.TryGetValue(result.CaseId, out var apiCase) || result.Probe is null)
            {
                return ApiCheckHelpers.Fail(Definition, context, $"API case '{result.CaseId}' incomplete", "Missing probe evidence.", FindingSeverity.High);
            }

            var failures = await EvaluateCaseAsync(apiCase, result.Probe, evidence.WorkingDirectory ?? Directory.GetCurrentDirectory(), context.Limits, cancellationToken)
                .ConfigureAwait(false);
            if (failures.Count > 0)
            {
                return ApiCheckHelpers.Fail(
                    Definition,
                    context,
                    $"API case '{result.CaseId}' failed",
                    string.Join(" ", failures),
                    FindingSeverity.High);
            }
        }

        return ApiCheckHelpers.Pass(Definition);
    }

    private static async Task<List<string>> EvaluateCaseAsync(
        PienApiCaseConfiguration apiCase,
        ProbeResult probe,
        string workingDirectory,
        ScanLimits limits,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        if (apiCase.ExpectedStatus is JsonElement statusElement && probe.StatusCode is { } code)
        {
            if (!MatchesExpectedStatus(statusElement, (int)code))
            {
                failures.Add($"Unexpected status {(int)code}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(apiCase.ExpectedContentType))
        {
            var contentType = probe.ContentType ?? (probe.Headers.TryGetValue("Content-Type", out var ct) ? ct : "");
            if (!contentType.Contains(apiCase.ExpectedContentType, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"Expected content-type containing '{apiCase.ExpectedContentType}'.");
            }
        }

        if (apiCase.ResponseTimeBudgetMs is int budgetMs && probe.Duration.TotalMilliseconds > budgetMs)
        {
            failures.Add($"Response exceeded budget {budgetMs}ms.");
        }

        foreach (var header in apiCase.RequiredHeaders)
        {
            if (!probe.Headers.ContainsKey(header))
            {
                failures.Add($"Missing required header '{header}'.");
            }
        }

        foreach (var header in apiCase.ForbiddenHeaders)
        {
            if (probe.Headers.ContainsKey(header))
            {
                failures.Add($"Forbidden header '{header}' present.");
            }
        }

        if (apiCase.JsonAssertions.Count > 0)
        {
            try
            {
                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(probe.Body.Span));
                foreach (var assertion in apiCase.JsonAssertions)
                {
                    if (!JsonPointerAssertions.TryEvaluate(document.RootElement, assertion, limits, out var message))
                    {
                        failures.Add(message ?? "JSON assertion failed.");
                    }
                }
            }
            catch (JsonException)
            {
                failures.Add("Response body is not valid JSON for assertions.");
            }
        }

        if (!string.IsNullOrWhiteSpace(apiCase.JsonSchemaPath))
        {
            var path = Path.IsPathRooted(apiCase.JsonSchemaPath)
                ? apiCase.JsonSchemaPath
                : Path.GetFullPath(Path.Combine(workingDirectory, apiCase.JsonSchemaPath));
            if (!File.Exists(path))
            {
                failures.Add($"JSON Schema file not found: {apiCase.JsonSchemaPath}");
            }
            else
            {
                var schemaJson = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var schemaErrors = await LocalJsonSchemaValidator.ValidateAsync(
                    schemaJson,
                    Encoding.UTF8.GetString(probe.Body.Span),
                    cancellationToken).ConfigureAwait(false);
                failures.AddRange(schemaErrors);
            }
        }

        return failures;
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

/// <summary>OpenAPI document inspection (never auto-executes operations).</summary>
public sealed class OpenApiDocumentCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.OpenApi001),
        Name = "OpenAPI document",
        Category = CheckCategory.ApiContract,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Loads and inspects a local OpenAPI 3.x document without executing operations.",
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
        var path = Path.IsPathRooted(context.Target.OpenApiDocument)
            ? context.Target.OpenApiDocument
            : Path.GetFullPath(Path.Combine(workingDirectory, context.Target.OpenApiDocument));
        if (!File.Exists(path))
        {
            return ApiCheckHelpers.Fail(Definition, context, "OpenAPI document missing", path, FindingSeverity.Medium);
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
                return ApiCheckHelpers.Fail(Definition, context, "OpenAPI parse failed", errors, FindingSeverity.Medium);
            }

            if (readResult.Document.Paths is null || readResult.Document.Paths.Count == 0)
            {
                return ApiCheckHelpers.Fail(Definition, context, "OpenAPI has no paths", "Document parsed but contained no paths.", FindingSeverity.Low);
            }

            return ApiCheckHelpers.Pass(Definition);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApiCheckHelpers.Fail(Definition, context, "OpenAPI inspect failed", ex.Message, FindingSeverity.Medium);
        }
    }
}

/// <summary>Baseline change detection.</summary>
public sealed class BaselineChangeCheck : ICheck
{
    /// <inheritdoc />
    public CheckDefinition Definition { get; } = CheckDefinition.Create(new CheckDefinition
    {
        Id = CheckId.Create(CheckIds.Change001),
        Name = "Baseline change detection",
        Category = CheckCategory.ChangeStability,
        DefaultSeverity = FindingSeverity.Medium,
        Description = "Compares content and TLS fingerprints against a stored baseline when provided.",
        RuleVersion = "1.0.0",
    });

    /// <inheritdoc />
    public Task<CheckResult> EvaluateAsync(ScanContext context, InspectionEvidence evidence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (evidence.Baseline is null)
        {
            return Task.FromResult(ApiCheckHelpers.NotApplicable(Definition));
        }

        var baseline = evidence.Baseline;
        if (!string.IsNullOrWhiteSpace(baseline.ContentFingerprint) &&
            !string.Equals(baseline.ContentFingerprint, evidence.ContentFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ApiCheckHelpers.Fail(
                Definition,
                context,
                "Content fingerprint changed",
                "Primary content fingerprint differs from baseline.",
                FindingSeverity.Medium));
        }

        if (!string.IsNullOrWhiteSpace(baseline.TlsCertificateFingerprint) &&
            !string.Equals(baseline.TlsCertificateFingerprint, evidence.TlsCertificateFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ApiCheckHelpers.Fail(
                Definition,
                context,
                "TLS fingerprint changed",
                "TLS certificate fingerprint differs from baseline.",
                FindingSeverity.Medium));
        }

        return Task.FromResult(ApiCheckHelpers.Pass(Definition));
    }
}
