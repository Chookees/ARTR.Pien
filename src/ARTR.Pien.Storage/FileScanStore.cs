using System.Text.Json;
using ARTR.Pien.Abstractions;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Policy;
using ARTR.Pien.Reporting;
using ARTR.Pien.Scanning;

namespace ARTR.Pien.Storage;

/// <summary>
/// File-system storage under <c>.pien/</c> with atomic replace writes.
/// </summary>
public sealed class FileScanStore : IScanStore, IBaselineStore, IRunHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _root;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileScanStore"/> class.
    /// </summary>
    /// <param name="stateDirectory">State directory (typically <c>.pien</c>).</param>
    public FileScanStore(string stateDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
        _root = Path.GetFullPath(stateDirectory);
        Directory.CreateDirectory(Path.Combine(_root, "runs"));
        Directory.CreateDirectory(Path.Combine(_root, "baselines"));
        Directory.CreateDirectory(Path.Combine(_root, "state"));
        Directory.CreateDirectory(Path.Combine(_root, "locks"));
    }

    /// <inheritdoc />
    public async Task SaveRunAsync(ScanRun run, ReportDocument? report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        var dir = Path.Combine(_root, "runs", run.Id.Value);
        Directory.CreateDirectory(dir);
        await AtomicWriteJsonAsync(Path.Combine(dir, "run.json"), run, cancellationToken).ConfigureAwait(false);
        if (report is not null)
        {
            await AtomicWriteJsonAsync(Path.Combine(dir, "report.json"), report, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<ScanRun?> GetRunAsync(ScanRunId id, CancellationToken cancellationToken = default)
        => await GetAsync(id, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task CleanupAsync(int keepCount, CancellationToken cancellationToken = default)
        => CleanAsync(keepCount, cancellationToken);

    /// <inheritdoc />
    public async Task SaveAsync(ScanRun run, CancellationToken cancellationToken = default)
        => await SaveRunAsync(run, report: null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ScanRun?> GetAsync(ScanRunId runId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runId);
        var path = Path.Combine(_root, "runs", runId.Value, "run.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ScanRun>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScanRun>> ListRecentAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        var runsDir = Path.Combine(_root, "runs");
        if (!Directory.Exists(runsDir))
        {
            return [];
        }

        var runs = new List<ScanRun>();
        foreach (var dir in Directory.EnumerateDirectories(runsDir).OrderByDescending(d => d).Take(maxCount))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = ScanRunId.Create(Path.GetFileName(dir));
            var run = await GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (run is not null)
            {
                runs.Add(run);
            }
        }

        return runs;
    }

    /// <inheritdoc />
    public async Task<int> CleanAsync(int keepCount, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keepCount);
        var runsDir = Path.Combine(_root, "runs");
        if (!Directory.Exists(runsDir))
        {
            return 0;
        }

        var dirs = Directory.EnumerateDirectories(runsDir).OrderByDescending(d => d).Skip(keepCount).ToArray();
        foreach (var dir in dirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(dir, recursive: true);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return dirs.Length;
    }

    /// <inheritdoc />
    public async Task SaveAsync(Baseline baseline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        var path = Path.Combine(_root, "baselines", baseline.Id + ".json");
        await AtomicWriteJsonAsync(path, baseline, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Baseline?> GetAsync(string baselineId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineId);
        var path = Path.Combine(_root, "baselines", baselineId + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Baseline>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string baselineId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineId);
        var path = Path.Combine(_root, "baselines", baselineId + ".json");
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(_root, "baselines");
        if (!Directory.Exists(dir))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var ids = Directory.EnumerateFiles(dir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(ids);
    }

    private static async Task AtomicWriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new StorageException("Invalid storage path.");
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, path, overwrite: true);
    }
}
