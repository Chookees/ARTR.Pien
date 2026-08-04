using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Findings;
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
        IScanStore? store = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _policyEvaluator = policyEvaluator ?? throw new ArgumentNullException(nameof(policyEvaluator));
        _scoreCalculator = scoreCalculator ?? throw new ArgumentNullException(nameof(scoreCalculator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store;
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
        catch
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

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(definition.Limits.OverallScanTimeout);

        foreach (var target in definition.Targets)
        {
            await ExamineTargetAsync(definition, selected, run.Id, target, findings, checkResults, progress, timeoutCts.Token)
                .ConfigureAwait(false);
        }

        progress?.Report(new ScanProgress(ScanStage.Reporting, "Reporting", 95));
        var policyResult = _policyEvaluator.Evaluate(options.Policy, findings);
        _ = _scoreCalculator.ScoreByCategory(findings, checkResults);
        run = Finalize(run, ScanRunStatus.Completed, findings.Take(definition.Limits.MaxReportFindings).ToArray());

        if (_store is not null)
        {
            var report = ReportDocument.Create(new ReportDocument
            {
                SchemaVersion = 1,
                RunId = run.Id,
                GeneratedAt = _clock.UtcNow,
                Findings = run.Findings,
                PolicyResult = policyResult,
            });
            await _store.SaveRunAsync(run, report, cancellationToken).ConfigureAwait(false);
        }

        return run;
    }

    private async Task ExamineTargetAsync(
        ScanDefinition definition,
        IReadOnlyList<ICheck> selected,
        ScanRunId runId,
        ScanTarget target,
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
        var probe = await _transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest
            {
                Uri = target.BaseUrl,
                Method = ProbeMethod.Get,
                MaxResponseBodyBytes = definition.Limits.BodyInspectionLimitBytes,
            }),
            context,
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new ScanProgress(ScanStage.Inspecting, $"Inspecting {target.Id}", 40));
        var evidence = InspectionEvidence.Create(new InspectionEvidence
        {
            Target = target,
            Probes = new Dictionary<string, ProbeResult>(StringComparer.Ordinal) { ["primary"] = probe },
        });

        progress?.Report(new ScanProgress(ScanStage.Examining, $"Examining {target.Id}", 60, 0, selected.Count));
        for (var i = 0; i < selected.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var check = selected[i];
            var result = await check.EvaluateAsync(context, evidence, cancellationToken).ConfigureAwait(false);
            checkResults.Add(result);
            findings.AddRange(result.Findings);
            progress?.Report(new ScanProgress(
                ScanStage.Examining,
                $"Examined {check.Definition.Id}",
                60 + (30 * (i + 1) / Math.Max(1, selected.Count)),
                i + 1,
                selected.Count));
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
