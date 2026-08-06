using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Baselines;
using ARTR.Pien.Checks;
using ARTR.Pien.Configuration;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
using ARTR.Pien.Notifications;
using ARTR.Pien.Policy;
using ARTR.Pien.Probing;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Engine;

/// <summary>
/// Default policy evaluator.
/// </summary>
public sealed class PolicyEvaluator : IPolicyEvaluator
{
    /// <inheritdoc />
    public PolicyResult Evaluate(Policy.Policy policy, IReadOnlyList<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(findings);
        policy = Policy.Policy.Create(policy);

        var failed = findings
            .Where(f => f.Status == FindingStatus.Fail && f.Severity >= policy.FailOnSeverityAtOrAbove)
            .ToArray();

        return PolicyResult.Create(new PolicyResult
        {
            PolicyName = policy.Name,
            Passed = failed.Length == 0,
            FailedFindings = failed,
            Summary = failed.Length == 0 ? "Policy passed." : $"{failed.Length} finding(s) failed policy.",
        });
    }
}

/// <summary>
/// Advisory category score calculator (never certification language).
/// </summary>
public sealed class ScoreCalculator : IScoreCalculator
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, double> ScoreByCategory(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<CheckResult> results)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(results);

        var byCategory = new Dictionary<string, List<FindingStatus>>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            var key = result.CheckId.Value;
            if (!byCategory.TryGetValue(key, out var list))
            {
                list = [];
                byCategory[key] = list;
            }

            list.Add(result.Status);
        }

        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, statuses) in byCategory)
        {
            var pass = statuses.Count(s => s is FindingStatus.Pass or FindingStatus.NotApplicable or FindingStatus.Skipped);
            scores[key] = statuses.Count == 0 ? 100d : Math.Round(100d * pass / statuses.Count, 2);
        }

        return scores;
    }
}

internal sealed record TargetProbeBundle(
    Dictionary<string, ProbeResult> Probes,
    TlsProbeResult? Tls);

/// <summary>
/// Four-stage PIEN scan engine.
/// </summary>
public sealed class ScanEngine : IScanEngine
{
    private readonly ISafeHttpTransport _transport;
    private readonly ICheckCatalog _catalog;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IScoreCalculator _scoreCalculator;
    private readonly IScanStore? _store;
    private readonly IBaselineStore? _baselineStore;
    private readonly ICrawler? _crawler;
    private readonly ITlsProbe? _tlsProbe;
    private readonly INotificationSender? _notifications;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScanEngine"/> class.
    /// </summary>
    public ScanEngine(
        ISafeHttpTransport transport,
        ICheckCatalog catalog,
        IPolicyEvaluator policyEvaluator,
        IScoreCalculator scoreCalculator,
        IClock clock,
        IScanStore? store = null,
        IBaselineStore? baselineStore = null,
        ICrawler? crawler = null,
        ITlsProbe? tlsProbe = null,
        INotificationSender? notifications = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _policyEvaluator = policyEvaluator ?? throw new ArgumentNullException(nameof(policyEvaluator));
        _scoreCalculator = scoreCalculator ?? throw new ArgumentNullException(nameof(scoreCalculator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store;
        _baselineStore = baselineStore;
        _crawler = crawler;
        _tlsProbe = tlsProbe;
        _notifications = notifications;
    }

    /// <inheritdoc />
    public async Task<ScanRun> RunAsync(
        ScanDefinition definition,
        ScanEngineOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        definition = ScanDefinition.Create(definition);

        var runId = ScanRunId.NewId();
        var selected = SelectChecks(definition);
        var plan = ScanPlan.Create(new ScanPlan
        {
            Definition = definition,
            SelectedChecks = selected.Select(c => c.Definition.Id).ToArray(),
            Limits = definition.Limits,
        });

        var run = ScanRun.Create(new ScanRun
        {
            Id = runId,
            Plan = plan,
            Status = ScanRunStatus.Running,
            StartedAt = _clock.UtcNow,
        });

        try
        {
            return await ExecuteAsync(definition, options, selected, run, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Finalize(run, ScanRunStatus.Failed, []);
        }
        catch (OperationCanceledException)
        {
            return Finalize(run, ScanRunStatus.Cancelled, []);
        }
        catch (TargetSafetyException)
        {
            throw;
        }
        catch (AuthorizationException)
        {
            throw;
        }
        catch (NotificationException)
        {
            throw;
        }
        catch (PienException)
        {
            return Finalize(run, ScanRunStatus.Failed, []);
        }
        catch (IOException)
        {
            return Finalize(run, ScanRunStatus.Failed, []);
        }
        catch (HttpRequestException)
        {
            return Finalize(run, ScanRunStatus.Failed, []);
        }
    }

    private async Task<ScanRun> ExecuteAsync(
        ScanDefinition definition,
        ScanEngineOptions options,
        IReadOnlyList<ICheck> selected,
        ScanRun run,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new ScanProgress(ScanStage.Testing, "Testing targets", 5));
        var findings = new List<Finding>();
        var checkResults = new List<CheckResult>();
        var baseline = await LoadBaselineAsync(options, cancellationToken).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(definition.Limits.OverallScanTimeout);

        foreach (var target in definition.Targets)
        {
            await ExamineTargetAsync(
                definition, options, selected, run.Id, target, baseline, findings, checkResults, progress, timeoutCts.Token)
                .ConfigureAwait(false);
        }

        progress?.Report(new ScanProgress(ScanStage.Reporting, "Reporting", 95));
        var policyResult = _policyEvaluator.Evaluate(options.Policy, findings);
        var scores = _scoreCalculator.ScoreByCategory(findings, checkResults);
        run = Finalize(run, ScanRunStatus.Completed, findings.Take(definition.Limits.MaxReportFindings).ToArray());
        await PersistAndNotifyAsync(options, run, policyResult, scores, baseline, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress(ScanStage.Reporting, "Done", 100));
        return run;
    }

    private async Task<Baseline?> LoadBaselineAsync(ScanEngineOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.BaselineId) || _baselineStore is null)
        {
            return null;
        }

        return await _baselineStore.GetAsync(options.BaselineId, cancellationToken).ConfigureAwait(false);
    }

    private async Task PersistAndNotifyAsync(
        ScanEngineOptions options,
        ScanRun run,
        PolicyResult policyResult,
        IReadOnlyDictionary<string, double> categoryScores,
        Baseline? baseline,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<BaselineComparison> comparisons = [];
        if (baseline is not null && options.CompareBaseline)
        {
            comparisons = BaselineComparer.Compare(baseline, run.Findings);
        }

        var report = ReportDocument.Create(new ReportDocument
        {
            SchemaVersion = 1,
            RunId = run.Id,
            GeneratedAt = _clock.UtcNow,
            Findings = run.Findings,
            PolicyResult = policyResult,
            BaselineComparisons = comparisons,
            CategoryScores = categoryScores,
        });

        if (_store is not null)
        {
            await _store.SaveRunAsync(run, report, cancellationToken).ConfigureAwait(false);
            await _store.CleanupAsync(Math.Max(1, options.RetainRuns), cancellationToken).ConfigureAwait(false);
        }

        await TryNotifyAsync(options, run, policyResult, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExamineTargetAsync(
        ScanDefinition definition,
        ScanEngineOptions options,
        IReadOnlyList<ICheck> selected,
        ScanRunId runId,
        ScanTarget target,
        Baseline? baseline,
        List<Finding> findings,
        List<CheckResult> checkResults,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!target.Authorization.Confirmed)
        {
            throw new AuthorizationException($"Target '{target.Id}' is not authorized.");
        }

        var context = new ScanContext(runId, definition, definition.Limits, target, () => _clock.UtcNow);
        progress?.Report(new ScanProgress(ScanStage.Testing, $"Probing {target.Id}", 20));
        var bundle = await CollectPrimaryAndTlsAsync(definition, target, context, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress(ScanStage.Inspecting, $"Inspecting {target.Id}", 40));
        var crawled = await CollectCrawlEvidenceAsync(definition, target, context, bundle.Probes, cancellationToken).ConfigureAwait(false);
        var apiResults = await CollectApiEvidenceAsync(definition, target, context, cancellationToken).ConfigureAwait(false);
        var evidence = BuildEvidence(options, target, bundle, crawled, apiResults, baseline);
        await RunChecksAsync(definition, selected, context, evidence, findings, checkResults, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TargetProbeBundle> CollectPrimaryAndTlsAsync(
        ScanDefinition definition,
        ScanTarget target,
        ScanContext context,
        CancellationToken cancellationToken)
    {
        var probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal)
        {
            ["primary"] = await _transport.SendAsync(
                ProbeRequest.Create(new ProbeRequest
                {
                    Uri = target.BaseUrl,
                    Method = ProbeMethod.Get,
                    MaxResponseBodyBytes = definition.Limits.BodyInspectionLimitBytes,
                }),
                context,
                cancellationToken).ConfigureAwait(false),
        };

        if (_tlsProbe is null ||
            !string.Equals(target.BaseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new TargetProbeBundle(probes, null);
        }

        try
        {
            var tls = await _tlsProbe.ProbeAsync(target.BaseUrl, definition.Limits, cancellationToken).ConfigureAwait(false);
            return new TargetProbeBundle(probes, tls);
        }
        catch (TlsFailureException ex)
        {
            probes["tls-error"] = ProbeResult.Create(new ProbeResult
            {
                FinalUri = target.BaseUrl,
                Duration = TimeSpan.Zero,
                ErrorMessage = ex.Message,
            });
            return new TargetProbeBundle(probes, null);
        }
    }

    private static InspectionEvidence BuildEvidence(
        ScanEngineOptions options,
        ScanTarget target,
        TargetProbeBundle bundle,
        IReadOnlyList<CrawledPageEvidence> crawled,
        IReadOnlyList<ApiCaseExecutionResult> apiResults,
        Baseline? baseline)
    {
        bundle.Probes.TryGetValue("primary", out var primary);
        return InspectionEvidence.Create(new InspectionEvidence
        {
            Target = target,
            Probes = bundle.Probes,
            Tls = bundle.Tls,
            TlsCertificateFingerprint = bundle.Tls?.CertificateFingerprintSha256,
            ContentFingerprint = primary is null ? null : Convert.ToHexString(SHA256.HashData(primary.Body.Span)),
            CrawledPages = crawled,
            ApiCaseResults = apiResults,
            WorkingDirectory = options.WorkingDirectory,
            Baseline = options.CompareBaseline ? baseline : null,
        });
    }

    private async Task RunChecksAsync(
        ScanDefinition definition,
        IReadOnlyList<ICheck> selected,
        ScanContext context,
        InspectionEvidence evidence,
        List<Finding> findings,
        List<CheckResult> checkResults,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new ScanProgress(ScanStage.Examining, $"Examining {context.Target.Id}", 60, 0, selected.Count));
        for (var i = 0; i < selected.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var check = selected[i];
            var result = await EvaluateCheckAsync(check, context, evidence, cancellationToken).ConfigureAwait(false);
            checkResults.Add(result);
            findings.AddRange(result.Findings.Take(definition.Limits.MaxFindingsPerCheck));
            progress?.Report(new ScanProgress(
                ScanStage.Examining,
                $"Examined {check.Definition.Id}",
                60 + (30 * (i + 1) / Math.Max(1, selected.Count)),
                i + 1,
                selected.Count));
        }
    }

    private static async Task<CheckResult> EvaluateCheckAsync(
        ICheck check,
        ScanContext context,
        InspectionEvidence evidence,
        CancellationToken cancellationToken)
    {
        try
        {
            return await check.EvaluateAsync(context, evidence, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PienException ex)
        {
            return CheckResult.Create(new CheckResult
            {
                CheckId = check.Definition.Id,
                Status = FindingStatus.Error,
                Findings = [],
                ErrorMessage = ex.Message,
            });
        }
    }

    private async Task<IReadOnlyList<CrawledPageEvidence>> CollectCrawlEvidenceAsync(
        ScanDefinition definition,
        ScanTarget target,
        ScanContext context,
        Dictionary<string, ProbeResult> probes,
        CancellationToken cancellationToken)
    {
        if (_crawler is null || target.Kind != ScanTargetKind.Website || definition.Limits.MaxCrawlPages <= 1)
        {
            return [];
        }

        var pages = new List<CrawledPageEvidence>();
        var index = 0;
        await foreach (var page in _crawler.CrawlAsync(target, definition.Limits, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index >= definition.Limits.MaxCrawlPages)
            {
                break;
            }

            pages.Add(await ProbeCrawlPageAsync(page, target, context, definition, probes, index, cancellationToken)
                .ConfigureAwait(false));
            index++;
        }

        return pages;
    }

    private async Task<CrawledPageEvidence> ProbeCrawlPageAsync(
        CrawlPage page,
        ScanTarget target,
        ScanContext context,
        ScanDefinition definition,
        Dictionary<string, ProbeResult> probes,
        int index,
        CancellationToken cancellationToken)
    {
        if (string.Equals(page.Uri.AbsoluteUri, target.BaseUrl.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
        {
            probes.TryGetValue("primary", out var primary);
            return new CrawledPageEvidence(page, primary);
        }

        try
        {
            var probe = await _transport.SendAsync(
                ProbeRequest.Create(new ProbeRequest
                {
                    Uri = page.Uri,
                    Method = ProbeMethod.Get,
                    MaxResponseBodyBytes = definition.Limits.BodyInspectionLimitBytes,
                }),
                context,
                cancellationToken).ConfigureAwait(false);
            probes[$"crawl:{index}"] = probe;
            return new CrawledPageEvidence(page, probe);
        }
        catch (PienException)
        {
            return new CrawledPageEvidence(page, null);
        }
        catch (HttpRequestException)
        {
            return new CrawledPageEvidence(page, null);
        }
    }

    private async Task<IReadOnlyList<ApiCaseExecutionResult>> CollectApiEvidenceAsync(
        ScanDefinition definition,
        ScanTarget target,
        ScanContext context,
        CancellationToken cancellationToken)
    {
        if (target.Kind != ScanTargetKind.Api || target.ApiCases.Count == 0)
        {
            return [];
        }

        var results = new List<ApiCaseExecutionResult>();
        foreach (var apiCase in target.ApiCases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ExecuteApiCaseAsync(apiCase, target, context, definition.Limits, cancellationToken)
                .ConfigureAwait(false));
        }

        return results;
    }

    private async Task<ApiCaseExecutionResult> ExecuteApiCaseAsync(
        PienApiCaseConfiguration apiCase,
        ScanTarget target,
        ScanContext context,
        ScanLimits limits,
        CancellationToken cancellationToken)
    {
        var methodName = string.IsNullOrWhiteSpace(apiCase.Method) ? "GET" : apiCase.Method.Trim().ToUpperInvariant();
        if (!IsIdempotent(methodName) && !apiCase.AllowNonIdempotent)
        {
            return BlockedCase(apiCase.Id, methodName, $"Non-idempotent method '{methodName}' blocked unless allowNonIdempotent=true.");
        }

        if (!TryParseMethod(methodName, out var method))
        {
            return BlockedCase(apiCase.Id, methodName, $"Unsupported method '{methodName}'.");
        }

        var uri = ResolveUri(target.BaseUrl, apiCase.Path, apiCase.Query);
        var headers = apiCase.Headers is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(apiCase.Headers, StringComparer.OrdinalIgnoreCase);
        var body = string.IsNullOrEmpty(apiCase.Body) ? ReadOnlyMemory<byte>.Empty : Encoding.UTF8.GetBytes(apiCase.Body);
        var probe = await _transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest
            {
                Uri = uri,
                Method = method,
                Headers = headers,
                Body = body,
                ContentType = apiCase.ContentType,
                MaxResponseBodyBytes = apiCase.MaxBodyBytes ?? limits.BodyInspectionLimitBytes,
            }),
            context,
            cancellationToken).ConfigureAwait(false);

        return new ApiCaseExecutionResult(apiCase.Id, methodName, probe.FinalUri, probe, false, null, [], []);
    }

    private async Task TryNotifyAsync(
        ScanEngineOptions options,
        ScanRun run,
        PolicyResult policyResult,
        CancellationToken cancellationToken)
    {
        if (_notifications is null || options.Notifications is null || string.IsNullOrWhiteSpace(options.Notifications.WebhookUrl))
        {
            return;
        }

        var notification = Notification.Create(new Notification
        {
            Id = Guid.NewGuid().ToString("N"),
            RunId = run.Id,
            Title = policyResult.Passed ? "Pien scan completed" : "Pien scan policy failed",
            Message = $"Run {run.Id.Value}: {policyResult.Summary ?? run.Status.ToString()}",
            Severity = policyResult.Passed ? NotificationSeverity.Info : NotificationSeverity.Warning,
            CreatedAt = _clock.UtcNow,
        });

        try
        {
            await _notifications.SendAsync(notification, cancellationToken).ConfigureAwait(false);
        }
        catch (NotificationException) when (!options.Notifications.Required)
        {
            // Optional webhook failures must not erase scan results.
        }
    }

    private ScanRun Finalize(ScanRun run, ScanRunStatus status, IReadOnlyList<Finding> findings)
        => ScanRun.Create(run with
        {
            Status = status,
            CompletedAt = _clock.UtcNow,
            Findings = findings,
        });

    private List<ICheck> SelectChecks(ScanDefinition definition)
    {
        var all = _catalog.List().Select(d => _catalog.Get(d.Id)).Where(c => c is not null).Cast<ICheck>().ToList();
        if (definition.EnabledCheckIds.Count > 0)
        {
            all = all.Where(c => definition.EnabledCheckIds.Contains(c.Definition.Id.Value, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        if (definition.DisabledCheckIds.Count > 0)
        {
            all = all.Where(c => !definition.DisabledCheckIds.Contains(c.Definition.Id.Value, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        return all;
    }

    private static ApiCaseExecutionResult BlockedCase(string id, string method, string reason)
        => new(id, method, null, null, true, reason, [], []);

    private static bool IsIdempotent(string method) => method is "GET" or "HEAD" or "OPTIONS";

    private static bool TryParseMethod(string method, out ProbeMethod probeMethod)
    {
        probeMethod = method switch
        {
            "GET" => ProbeMethod.Get,
            "HEAD" => ProbeMethod.Head,
            "OPTIONS" => ProbeMethod.Options,
            "POST" => ProbeMethod.Post,
            "PUT" => ProbeMethod.Put,
            "PATCH" => ProbeMethod.Patch,
            "DELETE" => ProbeMethod.Delete,
            _ => ProbeMethod.Get,
        };
        return method is "GET" or "HEAD" or "OPTIONS" or "POST" or "PUT" or "PATCH" or "DELETE";
    }

    private static Uri ResolveUri(Uri baseUrl, string path, Dictionary<string, JsonElement>? query)
    {
        // On Linux/macOS, Uri.TryCreate("/health", Absolute) succeeds as file:///health.
        // Only treat http/https absolute URLs as override targets; otherwise resolve against baseUrl.
        Uri uri;
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) &&
            absolute.Scheme is "http" or "https")
        {
            uri = absolute;
        }
        else
        {
            var relative = string.IsNullOrWhiteSpace(path)
                ? "/"
                : path.StartsWith('/') ? path : "/" + path;
            uri = new Uri(baseUrl, relative);
        }

        if (query is null || query.Count == 0)
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Join(
                '&',
                query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(JsonElementToQueryValue(kv.Value))}")),
        };
        return builder.Uri;
    }

    private static string JsonElementToQueryValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => value.ToString(),
        };
}

/// <summary>
/// In-memory check catalog with explicit registration.
/// </summary>
public sealed class CheckCatalog : ICheckCatalog
{
    private readonly Dictionary<string, ICheck> _checks;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckCatalog"/> class.
    /// </summary>
    public CheckCatalog(IEnumerable<ICheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        _checks = checks.ToDictionary(c => c.Definition.Id.Value, c => c, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<CheckDefinition> List()
        => _checks.Values.Select(c => c.Definition).OrderBy(d => d.Id.Value, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <inheritdoc />
    public ICheck? Get(CheckId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _checks.TryGetValue(id.Value, out var check) ? check : null;
    }
}
