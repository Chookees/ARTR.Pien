using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Checks.Api;
using ARTR.Pien.Checks.Website;
using ARTR.Pien.Configuration;
using ARTR.Pien.Engine;
using ARTR.Pien.Hosting.Notifications;
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
        if (!string.IsNullOrWhiteSpace(options.WebhookUrl))
        {
            services.AddSingleton<INotificationSender>(sp => new HmacWebhookNotificationSender(
                options.WebhookUrl!,
                options.WebhookSecretReference,
                sp.GetRequiredService<ISecretResolver>()));
        }

        services.AddSingleton<IPolicyEvaluator, PolicyEvaluator>();
        services.AddSingleton<IScoreCalculator, ScoreCalculator>();
        services.AddPienChecks();
        services.AddSingleton<ICheckCatalog>(sp => new CheckCatalog(sp.GetServices<ICheck>()));
        services.AddSingleton<IScanEngine>(sp => new ScanEngine(
            sp.GetRequiredService<ISafeHttpTransport>(),
            sp.GetRequiredService<ICheckCatalog>(),
            sp.GetRequiredService<IPolicyEvaluator>(),
            sp.GetRequiredService<IScoreCalculator>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IScanStore>(),
            sp.GetRequiredService<IBaselineStore>(),
            sp.GetRequiredService<ICrawler>(),
            sp.GetRequiredService<ITlsProbe>(),
            sp.GetService<INotificationSender>()));
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
        services.AddSingleton<ICheck, HttpRedirectCheck>();
        services.AddSingleton<ICheck, HttpHttpsDowngradeCheck>();
        services.AddSingleton<ICheck, HttpExcessiveRedirectsCheck>();
        services.AddSingleton<ICheck, HttpServerErrorCheck>();
        services.AddSingleton<ICheck, HttpContentTypeCharsetCheck>();
        services.AddSingleton<ICheck, SecurityHeadersCheck>();
        services.AddSingleton<ICheck, ContentSecurityPolicyCheck>();
        services.AddSingleton<ICheck, HstsHeaderCheck>();
        services.AddSingleton<ICheck, FrameAncestorsCheck>();
        services.AddSingleton<ICheck, CrossOriginIsolationHeadersCheck>();
        services.AddSingleton<ICheck, ServerDisclosureHeadersCheck>();
        services.AddSingleton<ICheck, HtmlStructureCheck>();
        services.AddSingleton<ICheck, HtmlMetaViewportCharsetCheck>();
        services.AddSingleton<ICheck, HtmlMixedContentCheck>();
        services.AddSingleton<ICheck, HtmlDuplicateIdHeadingCheck>();
        services.AddSingleton<ICheck, AccessibilityFundamentalsCheck>();
        services.AddSingleton<ICheck, AccessibilityFormLabelCheck>();
        services.AddSingleton<ICheck, AccessibilityEmptyControlsCheck>();
        services.AddSingleton<ICheck, AccessibilityHeadingJumpCheck>();
        services.AddSingleton<ICheck, AccessibilityPositiveTabindexCheck>();
        services.AddSingleton<ICheck, CookieAttributeCheck>();
        services.AddSingleton<ICheck, CookieOverHttpCheck>();
        services.AddSingleton<ICheck, CookiePrefixRulesCheck>();
        services.AddSingleton<ICheck, SeoFundamentalsCheck>();
        services.AddSingleton<ICheck, SeoMetadataBudgetCheck>();
        services.AddSingleton<ICheck, SeoIndexabilityCheck>();
        services.AddSingleton<ICheck, SeoRobotsSitemapCheck>();
        services.AddSingleton<ICheck, LinkSafetyCheck>();
        services.AddSingleton<ICheck, LinkInternalCrawlCheck>();
        services.AddSingleton<ICheck, LinkTextCheck>();
        services.AddSingleton<ICheck, LinkExternalBrokenCheck>();
        services.AddSingleton<ICheck, LegalImpressumCheck>();
        services.AddSingleton<ICheck, LegalPrivacyPolicyCheck>();
        services.AddSingleton<ICheck, PerformanceBudgetCheck>();
        services.AddSingleton<ICheck, PerformanceTimingBudgetCheck>();
        services.AddSingleton<ICheck, PerformanceBodySizeBudgetCheck>();
        services.AddSingleton<ICheck, TlsFundamentalsCheck>();
        services.AddSingleton<ICheck, TlsExpirationCheck>();
        services.AddSingleton<ICheck, TlsProtocolStrengthCheck>();
        services.AddSingleton<ICheck, ApiContractCheck>();
        services.AddSingleton<ICheck, ApiExpectedStatusContentTypeCheck>();
        services.AddSingleton<ICheck, ApiJsonAssertionCheck>();
        services.AddSingleton<ICheck, ApiLocalJsonSchemaCheck>();
        services.AddSingleton<ICheck, ApiNonIdempotentGuardCheck>();
        services.AddSingleton<ICheck, OpenApiDocumentCheck>();
        services.AddSingleton<ICheck, OpenApiHygieneCheck>();
        services.AddSingleton<ICheck, OpenApiResponseConformanceCheck>();
        services.AddSingleton<ICheck, OpenApiCoverageCheck>();
        services.AddSingleton<ICheck, OpenApiMissingOperationIdCheck>();
        services.AddSingleton<ICheck, OpenApiResponseSchemaCheck>();
        services.AddSingleton<ICheck, BaselineChangeCheck>();
        services.AddSingleton<ICheck, BaselineVolatilityCheck>();
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

    /// <summary>Optional webhook URL.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Optional webhook HMAC secret reference.</summary>
    public string? WebhookSecretReference { get; set; }
}
