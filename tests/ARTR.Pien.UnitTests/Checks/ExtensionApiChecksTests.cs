using System.Net;
using System.Text;

using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Configuration;
using ARTR.Pien.Findings;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.UnitTests.Checks;

public sealed class ExtensionApiChecksTests
{
    private static ScanContext ApiContext(string? openApiRelative = "openapi.json", params PienApiCaseConfiguration[] cases)
    {
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "api",
            Kind = ScanTargetKind.Api,
            BaseUrl = new Uri("http://127.0.0.1:5088/"),
            Authorization = new TargetAuthorization(true),
            OpenApiDocument = openApiRelative,
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
    public async Task OpenApi002_flags_duplicate_operation_ids_and_http_servers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pien-oa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "openapi.json"),
                """
                {
                  "openapi": "3.0.3",
                  "info": { "title": "t", "version": "1.0.0" },
                  "servers": [ { "url": "http://127.0.0.1:5088" } ],
                  "paths": {
                    "/a": { "get": { "operationId": "dup", "responses": { "200": { "description": "ok" } } } },
                    "/b": { "get": { "operationId": "dup", "responses": { "200": { "description": "ok" } } } }
                  }
                }
                """,
                TestContext.Current.CancellationToken);
            var context = ApiContext("openapi.json");
            var evidence = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = context.Target,
                WorkingDirectory = dir,
            });
            var result = await new OpenApiHygieneCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
            Assert.Equal(FindingStatus.Fail, result.Status);
            Assert.Equal(CheckIds.OpenApi002, result.CheckId.Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenApi003_flags_status_not_documented()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pien-oa3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "openapi.json"),
                """
                {
                  "openapi": "3.0.3",
                  "info": { "title": "t", "version": "1.0.0" },
                  "paths": {
                    "/health": {
                      "get": {
                        "operationId": "health",
                        "responses": { "200": { "description": "ok", "content": { "application/json": {} } } }
                      }
                    }
                  }
                }
                """,
                TestContext.Current.CancellationToken);
            var apiCase = new PienApiCaseConfiguration { Id = "health", Method = "GET", Path = "/health" };
            var context = ApiContext("openapi.json", apiCase);
            var probe = ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1:5088/health"),
                StatusCode = HttpStatusCode.Created,
                ContentType = "application/json",
                Body = Encoding.UTF8.GetBytes("""{"ok":true}"""),
                Duration = TimeSpan.FromMilliseconds(5),
            });
            var evidence = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = context.Target,
                WorkingDirectory = dir,
                ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
            });
            var result = await new OpenApiResponseConformanceCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
            Assert.Equal(FindingStatus.Fail, result.Status);
            Assert.Equal(CheckIds.OpenApi003, result.CheckId.Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenApi004_reports_uncovered_operations()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pien-oa4-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "openapi.json"),
                """
                {
                  "openapi": "3.0.3",
                  "info": { "title": "t", "version": "1.0.0" },
                  "paths": {
                    "/health": { "get": { "operationId": "health", "responses": { "200": { "description": "ok" } } } },
                    "/ready": { "get": { "operationId": "ready", "responses": { "200": { "description": "ok" } } } }
                  }
                }
                """,
                TestContext.Current.CancellationToken);
            var apiCase = new PienApiCaseConfiguration { Id = "health", Method = "GET", Path = "/health" };
            var context = ApiContext("openapi.json", apiCase);
            var probe = ProbeResult.Create(new ProbeResult
            {
                FinalUri = new Uri("http://127.0.0.1:5088/health"),
                StatusCode = HttpStatusCode.OK,
                Duration = TimeSpan.FromMilliseconds(5),
            });
            var evidence = InspectionEvidence.Create(new InspectionEvidence
            {
                Target = context.Target,
                WorkingDirectory = dir,
                ApiCaseResults = [new ApiCaseExecutionResult("health", "GET", probe.FinalUri, probe, false, null, [], [])],
            });
            var result = await new OpenApiCoverageCheck().EvaluateAsync(context, evidence, TestContext.Current.CancellationToken);
            Assert.Equal(FindingStatus.Fail, result.Status);
            Assert.Equal(CheckIds.OpenApi004, result.CheckId.Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
