using System.Net;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Configuration;
using ARTR.Pien.Findings;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class ApiChecksTests
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

    [Fact]
    public void Json_pointer_eq_and_type_assertions()
    {
        using var doc = JsonDocument.Parse("""{"status":"ok","count":2}""");
        Assert.True(JsonPointerAssertions.TryEvaluate(
            doc.RootElement,
            new PienJsonAssertionConfiguration { Pointer = "/status", Op = "eq", Value = JsonSerializer.SerializeToElement("ok") },
            ScanLimits.Default,
            out _));
        Assert.True(JsonPointerAssertions.TryEvaluate(
            doc.RootElement,
            new PienJsonAssertionConfiguration { Pointer = "/count", Op = "type", TypeName = "integer" },
            ScanLimits.Default,
            out _));
        Assert.False(JsonPointerAssertions.TryEvaluate(
            doc.RootElement,
            new PienJsonAssertionConfiguration { Pointer = "/missing", Op = "exists" },
            ScanLimits.Default,
            out var message));
        Assert.Contains("not found", message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Api_contract_fails_when_case_blocked()
    {
        var context = ApiContext(new PienApiCaseConfiguration { Id = "post", Method = "POST", Path = "/x", AllowNonIdempotent = false });
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            ApiCaseResults =
            [
                new ApiCaseExecutionResult("post", "POST", null, null, true, "blocked", [], []),
            ],
        });
        var result = await new ApiContractCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Api_contract_passes_when_assertions_hold()
    {
        var apiCase = new PienApiCaseConfiguration
        {
            Id = "health",
            Method = "GET",
            Path = "/health",
            ExpectedStatus = JsonSerializer.SerializeToElement(200),
            JsonAssertions =
            [
                new PienJsonAssertionConfiguration { Pointer = "/status", Op = "eq", Value = JsonSerializer.SerializeToElement("ok") },
            ],
        };
        var context = ApiContext(apiCase);
        var probe = ProbeResult.Create(new ProbeResult
        {
            FinalUri = new Uri("http://127.0.0.1:5088/health"),
            StatusCode = HttpStatusCode.OK,
            ContentType = "application/json",
            Body = Encoding.UTF8.GetBytes("""{"status":"ok"}"""),
            Duration = TimeSpan.FromMilliseconds(5),
        });
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
        });
        var result = await new ApiContractCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Openapi_not_applicable_without_document()
    {
        var context = ApiContext();
        var evidence = InspectionEvidence.Create(new InspectionEvidence { Target = context.Target });
        var result = await new OpenApiDocumentCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.NotApplicable, result.Status);
    }

    [Fact]
    public async Task Change_check_detects_content_fingerprint_drift()
    {
        var context = ApiContext();
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = context.Target,
            ContentFingerprint = "AAA",
            Baseline = Baseline.Create(new Baseline
            {
                Id = "b1",
                TargetId = "api",
                ConfigurationFingerprint = "cfg",
                ContentFingerprint = "BBB",
                CreatedAt = DateTimeOffset.UtcNow,
            }),
        });
        var result = await new BaselineChangeCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
        Assert.Equal(FindingStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Local_json_schema_validator_reports_errors()
    {
        var errors = await LocalJsonSchemaValidator.ValidateAsync(
            """{"type":"object","required":["id"]}""",
            """{"name":"x"}""",
            TestContext.Current.CancellationToken);
        Assert.NotEmpty(errors);
    }
}
