using ARTR.Pien.Exceptions;
using ARTR.Pien.Secrets;

namespace ARTR.Pien.UnitTests.Core;

public sealed class SecretReferenceTests
{
    [Fact]
    public void Parses_environment_secret_reference()
    {
        var reference = SecretReference.Parse("secret://env/PIEN_TEST_API_KEY");

        Assert.Equal(SecretScheme.Environment, reference.Scheme);
        Assert.Equal("PIEN_TEST_API_KEY", reference.Path);
        Assert.Equal("secret://env/PIEN_TEST_API_KEY", reference.Uri);
    }

    [Fact]
    public void Parses_windows_file_secret_reference()
    {
        var reference = SecretReference.Parse("secret://file/C:/ProgramData/ARTR/Pien/secrets/api-key");

        Assert.Equal(SecretScheme.File, reference.Scheme);
        Assert.Equal("C:/ProgramData/ARTR/Pien/secrets/api-key", reference.Path);
    }

    [Fact]
    public void Parses_unix_absolute_file_secret_reference()
    {
        var reference = SecretReference.Parse("secret://file//etc/artr/pien/secrets/api-key");

        Assert.Equal(SecretScheme.File, reference.Scheme);
        Assert.Equal("/etc/artr/pien/secrets/api-key", reference.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("env/PIEN_KEY")]
    [InlineData("secret://")]
    [InlineData("secret://env/")]
    [InlineData("secret://vault/token")]
    [InlineData("secret://env/FOO/BAR")]
    public void Rejects_invalid_secret_references(string? value)
    {
        Assert.False(SecretReference.TryParse(value, out _));
        Assert.Throws<SecretResolutionException>(() => SecretReference.Parse(value!));
    }

    [Fact]
    public void Resolved_secret_ToString_does_not_leak_value()
    {
        using var secret = ResolvedSecret.FromString("super-secret-value");
        Assert.Equal("<redacted-secret>", secret.ToString());
        Assert.Equal("super-secret-value", secret.Reveal());
    }

    [Fact]
    public void Resolved_secret_clears_buffer_on_dispose()
    {
        var secret = ResolvedSecret.FromString("abc");
        secret.Dispose();
        Assert.Throws<ObjectDisposedException>(() => secret.Reveal());
    }
}
