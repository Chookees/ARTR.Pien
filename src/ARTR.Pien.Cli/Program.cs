namespace ARTR.Pien.Cli;

/// <summary>
/// CLI entry point for ARTR Pien.
/// </summary>
public static class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _ = args;
        Console.WriteLine("ARTR Pien");
        return 0;
    }
}
