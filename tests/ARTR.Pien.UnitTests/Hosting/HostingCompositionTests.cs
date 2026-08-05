using ARTR.Pien.Abstractions;
using ARTR.Pien.Checks;
using ARTR.Pien.Exceptions;
using ARTR.Pien.Hosting;
using ARTR.Pien.Hosting.Notifications;
using ARTR.Pien.Hosting.Secrets;
using ARTR.Pien.Notifications;
using ARTR.Pien.Secrets;

using Microsoft.Extensions.DependencyInjection;

namespace ARTR.Pien.UnitTests.Hosting;

public sealed class HostingCompositionTests
{
    [Fact]
    public void AddPien_resolves_core_services()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var services = new ServiceCollection();
            services.AddPien(o =>
            {
                o.WorkingDirectory = root;
                o.StateDirectory = Path.Combine(root, ".pien");
                o.AllowPrivateNetworks = true;
                o.AllowedHosts = ["127.0.0.1"];
            });
            using var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetRequiredService<IScanEngine>());
            Assert.NotNull(provider.GetRequiredService<ICheckCatalog>());
            Assert.NotNull(provider.GetRequiredService<ISafeHttpTransport>());
            Assert.NotNull(provider.GetRequiredService<ICrawler>());
            Assert.NotNull(provider.GetRequiredService<ITlsProbe>());
            Assert.NotNull(provider.GetRequiredService<IScanStore>());
            Assert.NotNull(provider.GetRequiredService<IBaselineStore>());
            Assert.NotNull(provider.GetRequiredService<IRunHistoryStore>());
            Assert.NotNull(provider.GetRequiredService<ISecretResolver>());
            Assert.Equal(48, provider.GetRequiredService<ICheckCatalog>().List().Count);
            Assert.Null(provider.GetService<INotificationSender>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AddPien_registers_webhook_when_configured()
    {
        var services = new ServiceCollection();
        services.AddPien(o =>
        {
            o.WebhookUrl = "https://example.com/hook";
            o.WebhookSecretReference = "secret://env/HOOK";
        });
        using var provider = services.BuildServiceProvider();
        Assert.IsType<HmacWebhookNotificationSender>(provider.GetRequiredService<INotificationSender>());
    }

    [Fact]
    public async Task Default_secret_resolver_reads_env_and_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "pien-sec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var envName = "PIEN_TEST_SECRET_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(envName, "env-value");
        try
        {
            var file = Path.Combine(root, "secret.txt");
            await File.WriteAllTextAsync(file, "file-value\n", TestContext.Current.CancellationToken);
            var resolver = new DefaultSecretResolver(root);
            using (var env = await resolver.ResolveAsync(SecretReference.Parse($"secret://env/{envName}"), TestContext.Current.CancellationToken))
            {
                Assert.Equal("env-value", env.Reveal());
            }

            using (var fileSecret = await resolver.ResolveAsync(SecretReference.Parse("secret://file/secret.txt"), TestContext.Current.CancellationToken))
            {
                Assert.Equal("file-value", fileSecret.Reveal());
            }

            await Assert.ThrowsAsync<SecretResolutionException>(() =>
                resolver.ResolveAsync(SecretReference.Parse("secret://env/MISSING_PIEN_SECRET"), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<SecretResolutionException>(() =>
                resolver.ResolveAsync(SecretReference.Parse("secret://file/missing.txt"), TestContext.Current.CancellationToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Webhook_retries_then_fails_on_persistent_errors()
    {
        var handler = new FailingHandler();
        var sender = new HmacWebhookNotificationSender(
            "https://example.com/hook",
            null,
            new StaticSecretResolver(),
            handler);
        await Assert.ThrowsAsync<NotificationException>(() => sender.SendAsync(
            Notification.Create(new Notification
            {
                Id = "n",
                Title = "t",
                Message = "m",
                Severity = NotificationSeverity.Warning,
                CreatedAt = DateTimeOffset.UtcNow,
            }),
            TestContext.Current.CancellationToken));
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Webhook_rejects_non_https()
    {
        var sender = new HmacWebhookNotificationSender(
            "http://example.com/hook",
            null,
            new StaticSecretResolver(),
            new FailingHandler());
        await Assert.ThrowsAsync<NotificationException>(() => sender.SendAsync(
            Notification.Create(new Notification
            {
                Id = "n",
                Title = "t",
                Message = "m",
                Severity = NotificationSeverity.Info,
                CreatedAt = DateTimeOffset.UtcNow,
            }),
            TestContext.Current.CancellationToken));
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        }
    }

    private sealed class StaticSecretResolver : ISecretResolver
    {
        public Task<ResolvedSecret> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(ResolvedSecret.FromString("x"));
    }
}
