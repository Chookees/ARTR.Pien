using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using ARTR.Pien.CodeAnalysis;

namespace ARTR.Pien.CodeAnalysis.Tests;

public sealed class PowerOfTenAnalyzerTests
{
    [Fact]
    public async Task Reports_goto_as_PIEN0004()
    {
        const string source = """
            class C
            {
                void M()
                {
                    goto label;
                    label: ;
                }
            }
            """;

        var tester = new CSharpAnalyzerTest<PowerOfTenAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        tester.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("PIEN0004").WithSpan(5, 9, 5, 20));
        await tester.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reports_async_void_as_PIEN0006()
    {
        const string source = """
            using System.Threading.Tasks;
            class C
            {
                async void M()
                {
                    await Task.CompletedTask;
                }
            }
            """;

        var tester = new CSharpAnalyzerTest<PowerOfTenAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        tester.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("PIEN0006").WithSpan(4, 5, 7, 6).WithArguments("M"));
        await tester.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reports_direct_recursion_as_PIEN0002()
    {
        const string source = """
            class C
            {
                int Fact(int n) => n <= 1 ? 1 : n * Fact(n - 1);
            }
            """;

        var tester = new CSharpAnalyzerTest<PowerOfTenAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        tester.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("PIEN0002").WithSpan(3, 41, 3, 52).WithArguments("Fact"));
        await tester.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Allows_short_method()
    {
        const string source = """
            class C
            {
                int Add(int a, int b) => a + b;
            }
            """;

        var tester = new CSharpAnalyzerTest<PowerOfTenAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await tester.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Allows_overload_forwarding_without_PIEN0002()
    {
        const string source = """
            class C
            {
                bool TryParse(string? value, out int n) => TryParse(value, out n, out _);
                bool TryParse(string? value, out int n, out string? error)
                {
                    error = null;
                    n = 0;
                    return value == "1";
                }
            }
            """;

        var tester = new CSharpAnalyzerTest<PowerOfTenAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await tester.RunAsync(TestContext.Current.CancellationToken);
    }
}
