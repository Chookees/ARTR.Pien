using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using ARTR.Pien.Abstractions;
using ARTR.Pien.Probing;
using ARTR.Pien.Scanning;
using ARTR.Pien.Web.Network;
using ARTR.Pien.Web.Tls;
using ARTR.Pien.Web.Transport;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ARTR.Pien.IntegrationTests.Loopback;

public sealed class HttpsTlsProbeTests : IAsyncLifetime
{
    private IHost? _host;
    private Uri? _baseUri;
    private X509Certificate2? _cert;

    public async ValueTask InitializeAsync()
    {
        _cert = CreateSelfSigned();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, listen =>
            {
                listen.UseHttps(_cert);
            });
        });
        var app = builder.Build();
        app.MapGet("/", async ctx =>
        {
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.WriteAsync("secure");
        });
        app.MapGet("/big", async ctx =>
        {
            ctx.Response.ContentType = "application/octet-stream";
            await ctx.Response.Body.WriteAsync(new byte[8192]);
        });
        app.MapMethods("/echo", ["POST"], async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.WriteAsync(body);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        _host = app;
        var url = app.Urls.First(u => u.StartsWith("https://127.0.0.1", StringComparison.Ordinal));
        _baseUri = new Uri(url);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
        }

        _cert?.Dispose();
    }

    [Fact]
    public async Task Tls_probe_and_https_transport_succeed_on_loopback()
    {
        var network = new NetworkSafetyOptions
        {
            AllowPrivateNetworks = true,
            AllowedHosts = ["127.0.0.1", "localhost"],
        };
        var validator = new DestinationValidator();
        var tls = new TlsProbe(validator);
        var tlsResult = await tls.ProbeAsync(_baseUri!, ScanLimits.Default, TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(tlsResult.CertificateFingerprintSha256));

        using var transport = new SafeHttpTransport(validator, network);
        var target = ScanTarget.Create(new ScanTarget
        {
            Id = "tls",
            Kind = ScanTargetKind.Website,
            BaseUrl = _baseUri!,
            Authorization = new TargetAuthorization(true),
        });
        var definition = ScanDefinition.Create(new ScanDefinition
        {
            SchemaVersion = 1,
            ProfileName = "quick",
            Targets = [target],
            Limits = ScanLimits.Default,
        });
        var context = new ScanContext(ScanRunId.NewId(), definition, definition.Limits, target, static () => DateTimeOffset.UtcNow);
        var get = await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest { Uri = _baseUri!, Method = ProbeMethod.Get, MaxResponseBodyBytes = 1024 }),
            context,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var truncated = await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest
            {
                Uri = new Uri(_baseUri!, "/big"),
                Method = ProbeMethod.Get,
                MaxResponseBodyBytes = 128,
            }),
            context,
            TestContext.Current.CancellationToken);
        Assert.True(truncated.BodyTruncated);

        var posted = await transport.SendAsync(
            ProbeRequest.Create(new ProbeRequest
            {
                Uri = new Uri(_baseUri!, "/echo"),
                Method = ProbeMethod.Post,
                Body = Encoding.UTF8.GetBytes("hello"),
                ContentType = "text/plain",
                MaxResponseBodyBytes = 1024,
            }),
            context,
            TestContext.Current.CancellationToken);
        Assert.Equal("hello", Encoding.UTF8.GetString(posted.Body.Span));
    }

    private static X509Certificate2 CreateSelfSigned()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), password: null, X509KeyStorageFlags.Exportable);
    }
}
