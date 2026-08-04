using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Configuration;
using ARTR.Pien.Engine;
using ARTR.Pien.Hosting.Secrets;
using ARTR.Pien.Reporting.Exporters;
using ARTR.Pien.Secrets;
using ARTR.Pien.Storage;
using ARTR.Pien.Web.Crawl;
using ARTR.Pien.Web.Network;
using ARTR.Pien.Web.Tls;
using ARTR.Pien.Web.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace ARTR.Pien.Hosting;

/// <summary>
/// Composition helpers for embedding or hosting Pien.
/// </summary>
public static class PienServiceCollectionExtensions
{
    /// <summary>
    /// Registers Pien services with explicit check registration (no assembly scanning).
    /// </summary>
    public static IServiceCollection AddPien(this IServiceCollection services, Action<PienHostingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new PienHostingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IClock, TimeProviderClock>();
        services.AddSingleton<IConfigLoader, JsonConfigLoader>();
        services.AddSingleton<IDestinationValidator, DestinationValidator>();
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<PienHostingOptions>();
            return new NetworkSafetyOptions
            {
                AllowPrivateNetworks = opts.AllowPrivateNetworks,
                AllowedHosts = opts.AllowedHosts,
                MaxRedirects = opts.MaxRedirects,
            };
        });
        services.AddSingleton<ISafeHttpTransport>(sp =>
        {
            var opts = sp.GetRequiredService<PienHostingOptions>();
            return new SafeHttpTransport(
                sp.GetRequiredService<IDestinationValidator>(),
                sp.GetRequiredService<NetworkSafetyOptions>(),
                opts.UserAgent);
        });
        services.AddSingleton<ITlsProbe, TlsProbe>();
        services.AddSingleton<ICrawler>(sp => new WebsiteCrawler(
            sp.GetRequiredService<ISafeHttpTransport>(),
            sp.GetRequiredService<NetworkSafetyOptions>()));
        services.AddSingleton(sp => new FileScanStore(options.StateDirectory));
        services.AddSingleton<IScanStore>(sp => sp.GetRequiredService<FileScanStore>());
        services.AddSingleton<IBaselineStore>(sp => sp.GetRequiredService<FileScanStore>());
        services.AddSingleton<IRunHistoryStore>(sp => sp.GetRequiredService<FileScanStore>());
        services.AddSingleton<ISecretResolver>(_ => new DefaultSecretResolver(options.WorkingDirectory));
        services.AddSingleton<IPolicyEvaluator, PolicyEvaluator>();
        services.AddSingleton<IScoreCalculator, ScoreCalculator>();
        services.AddPienChecks();
        services.AddSingleton<ICheckCatalog>(sp => new CheckCatalog(sp.GetServices<ICheck>()));
        services.AddSingleton<IScanEngine, ScanEngine>();
        services.AddSingleton<IReportExporter, JsonReportExporter>();
        services.AddSingleton<IReportExporter, ConsoleReportExporter>();
        services.AddSingleton<IReportExporter, SarifReportExporter>();
        services.AddSingleton<IReportExporter, JUnitReportExporter>();
        services.AddSingleton<IReportExporter, MarkdownReportExporter>();
        services.AddSingleton<IReportExporter, HtmlReportExporter>();
        return services;
    }

    /// <summary>
    /// Registers built-in checks explicitly.
    /// </summary>
    public static IServiceCollection AddPienChecks(this IServiceCollection services)
    {
        services.AddSingleton<ICheck, HttpAvailabilityCheck>();
        services.AddSingleton<ICheck, SecurityHeadersCheck>();
        services.AddSingleton<ICheck, ContentSecurityPolicyCheck>();
        services.AddSingleton<ICheck, HtmlStructureCheck>();
        services.AddSingleton<ICheck, AccessibilityFundamentalsCheck>();
        services.AddSingleton<ICheck, CookieAttributeCheck>();
        return services;
    }
}

/// <summary>
/// Hosting options for Pien DI composition.
/// </summary>
public sealed class PienHostingOptions
{
    /// <summary>Working directory.</summary>
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

    /// <summary>State directory.</summary>
    public string StateDirectory { get; set; } = ".pien";

    /// <summary>Allow private networks when allowlisted.</summary>
    public bool AllowPrivateNetworks { get; set; }

    /// <summary>Allowed hosts.</summary>
    public IReadOnlyList<string> AllowedHosts { get; set; } = [];

    /// <summary>Max redirects.</summary>
    public int MaxRedirects { get; set; } = 10;

    /// <summary>User-Agent.</summary>
    public string UserAgent { get; set; } = "ARTR-Pien/0.1 (+https://github.com/ARTR-Projects/Pien)";
}
