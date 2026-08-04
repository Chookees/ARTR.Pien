using ARTR.Pien.Exceptions;
using ARTR.Pien.Secrets;

namespace ARTR.Pien.Hosting.Secrets;

/// <summary>
/// Resolves <c>secret://env/</c> and <c>secret://file/</c> references. Never logs resolved material.
/// </summary>
public sealed class DefaultSecretResolver : ISecretResolver
{
    private readonly string _workingDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSecretResolver"/> class.
    /// </summary>
    /// <param name="workingDirectory">Base directory for relative file secrets.</param>
    public DefaultSecretResolver(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        _workingDirectory = Path.GetFullPath(workingDirectory);
    }

    /// <inheritdoc />
    public async Task<ResolvedSecret> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return reference.Scheme switch
        {
            SecretScheme.Environment => ResolveEnvironment(reference.Path),
            SecretScheme.File => await ResolveFileAsync(reference.Path, cancellationToken).ConfigureAwait(false),
            _ => throw new SecretResolutionException($"Unsupported secret scheme '{reference.Scheme}'."),
        };
    }

    private static ResolvedSecret ResolveEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (value is null)
        {
            throw new SecretResolutionException($"Environment variable '{name}' was not found.");
        }

        return ResolvedSecret.FromString(value);
    }

    private async Task<ResolvedSecret> ResolveFileAsync(string path, CancellationToken cancellationToken)
    {
        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(_workingDirectory, path));

        if (!fullPath.StartsWith(_workingDirectory, StringComparison.OrdinalIgnoreCase) &&
            !Path.IsPathRooted(path))
        {
            throw new SecretResolutionException("Relative secret file path escaped the working directory.");
        }

        if (!File.Exists(fullPath))
        {
            throw new SecretResolutionException("Secret file was not found.");
        }

        var text = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        return ResolvedSecret.FromString(text.TrimEnd('\r', '\n'));
    }
}
