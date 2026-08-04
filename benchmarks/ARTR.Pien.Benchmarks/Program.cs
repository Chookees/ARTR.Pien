namespace ARTR.Pien.Benchmarks;

/// <summary>
/// BenchmarkDotNet host entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs BenchmarkDotNet when invoked.
    /// </summary>
    /// <param name="args">BenchmarkDotNet arguments.</param>
    public static void Main(string[] args)
    {
        _ = args;
        Console.WriteLine("ARTR Pien benchmarks host. Pass --filter to run BenchmarkDotNet suites.");
    }
}
