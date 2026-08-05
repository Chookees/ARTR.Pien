using System.Net;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Configuration;
using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class CatalogCompletenessApiChecksTests
{
    private static ScanContext ApiContext(params PienApiCaseConfiguration[] cases)
    {
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "api",
            Kind = ScanTargetKind.Api,
            BaseUrl = new Uri("http://127.0.0.1:5088/"),
            Authorization = new TargetAuthorization(true),
            ApiCases = cases,
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "api",
            Targets = [target],
            Limits = ScanLimits.Default,
        });
        return new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
    }

    private static ProbeResult JsonProbe(HttpStatusCode status, string json, string contentType = "application/json")
        => ProbeResult.Create(new ProbeResult
        {
            FinalUri = new Uri("http://127.0.0.1:5088/health"),
            StatusCode = status,
            ContentType = contentType,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = contentType,
            },
            Body = Encoding.UTF8.GetBytes(json),
            Duration = TimeSpan.FromMilliseconds(5),
        });

    [Fact]
    public async Task Api002_flags_unexpected_status_or_content_type()
    {
        var apiCase = new PienApiCaseConfiguration
        {
            Id = "health",
            Method = "GET",
            Path = "/health",
            ExpectedStatus = JsonDocument.Parse("200").RootElement.Clone(),
            ExpectedContentType = "application/json",
        };
        var context = ApiContext(apiCase);
        var probe = JsonProbe(HttpStatusCode.OK, """{"ok":true}""", "text/plain");
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
        });
        var result = await new ApiExpectedStatusContentTypeCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Api002, result.CheckId.Value);
    }

    [Fact]
    public async Task Api003_flags_failed_json_pointer_assertion()
    {
        var apiCase = new PienApiCaseConfiguration
        {
            Id = "health",
            Method = "GET",
            Path = "/health",
            JsonAssertions =
            [
                new PienJsonAssertionConfiguration { Pointer = "/ok", Op = "eq", Value = JsonDocument.Parse("false").RootElement.Clone() },
            ],
        };
        var context = ApiContext(apiCase);
        var probe = JsonProbe(HttpStatusCode.OK, """{"ok":true}""");
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
        });
        var result = await new ApiJsonAssertionCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Api003, result.CheckId.Value);
    }

    [Fact]
    public async Task Api004_flags_local_schema_mismatch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pien-schema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var schemaPath = Path.Combine(dir, "schema.json");
            await File.WriteAllTextAsync(
                schemaPath,
                """{"type":"object","required":["id"],"properties":{"id":{"type":"string"}}}""",
                TestContext.Current.CancellationToken);
            var apiCase = new PienApiCaseConfiguration
            {
                Id = "health",
                Method = "GET",
                Path = "/health",
                JsonSchemaPath = "schema.json",
            };
            var context = ApiContext(apiCase);
            var probe = JsonProbe(HttpStatusCode.OK, """{"ok":true}""");
            var evidence = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = context.Target,
                WorkingDirectory = dir,
                ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
            });
            var result = await new ApiLocalJsonSchemaCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
            Assert.Equal(FindingStatus.Fail, result.Status);
            Assert.Equal(CheckIds.Api004, result.CheckId.Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Api005_flags_blocked_non_idempotent_case()
    {
        var apiCase = new PienApiCaseConfiguration
        {
            Id = "create",
            Method = "POST",
            Path = "/items",
            AllowNonIdempotent = false,
        };
        var context = ApiContext(apiCase);
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            ApiCaseResults =
            [
                new ApiCaseExecutionResult(
                    "create",
                    "POST",
                    new Uri("http://127.0.0.1:5088/items"),
                    null,
                    true,
                    "Non-idempotent method 'POST' blocked unless allowNonIdempotent=true.",
                    [],
                    []),
            ],
        });
        var result = await new ApiNonIdempotentGuardCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
        Assert.Equal(CheckIds.Api005, result.CheckId.Value);
    }
}
