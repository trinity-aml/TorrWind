using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class TorrServerArgumentBuilderTests
{
    [Fact]
    public void Build_IncludesRequiredEndpointAndDataPathArguments()
    {
        var settings = new LocalServerSettings
        {
            Port = 8095,
            ListenAddress = "127.0.0.2",
            DataDirectory = @"C:\TorrWind\Data\TorrServer"
        };

        var args = TorrServerArgumentBuilder.Build(settings);

        Assert.Equal(
            [
                "--port",
                "8095",
                "--ip",
                "127.0.0.2",
                "--path",
                @"C:\TorrWind\Data\TorrServer"
            ],
            args);
    }

    [Fact]
    public void Build_UsesLanBindAddressWhenLanAccessIsEnabled()
    {
        var settings = new LocalServerSettings
        {
            AllowLanAccess = true,
            ListenAddress = "127.0.0.1"
        };

        var args = TorrServerArgumentBuilder.Build(settings);

        Assert.Equal("0.0.0.0", args[3]);
    }

    [Theory]
    [InlineData("", "127.0.0.1")]
    [InlineData("   ", "127.0.0.1")]
    [InlineData(" 192.168.1.10 ", "192.168.1.10")]
    public void Build_NormalizesListenAddress(string listenAddress, string expected)
    {
        var settings = new LocalServerSettings
        {
            ListenAddress = listenAddress
        };

        var args = TorrServerArgumentBuilder.Build(settings);

        AssertOption(args, "--ip", expected);
    }

    [Fact]
    public void Build_AddsSslArgumentsOnlyWhenSslIsEnabled()
    {
        var settings = new LocalServerSettings
        {
            UseSsl = true,
            SslPort = 9443,
            CertificatePath = @"C:\certs\torrwind.pem",
            CertificateKeyPath = @"C:\certs\torrwind.key",
            ForceHttps = true
        };

        var args = TorrServerArgumentBuilder.Build(settings);

        Assert.Contains("--ssl", args);
        AssertOption(args, "--sslport", "9443");
        AssertOption(args, "--sslcert", @"C:\certs\torrwind.pem");
        AssertOption(args, "--sslkey", @"C:\certs\torrwind.key");
        Assert.Contains("--force-https", args);
    }

    [Fact]
    public void Build_DoesNotAddForceHttpsWhenSslIsDisabled()
    {
        var settings = new LocalServerSettings
        {
            UseSsl = false,
            ForceHttps = true
        };

        var args = TorrServerArgumentBuilder.Build(settings);

        Assert.DoesNotContain("--ssl", args);
        Assert.DoesNotContain("--force-https", args);
    }

    [Fact]
    public void Build_AddsAuthDatabaseSearchAndWebDavFlags()
    {
        var settings = new LocalServerSettings
        {
            UseHttpAuth = true,
            ReadOnlyDatabase = true,
            AllowSearchWithoutAuth = true,
            EnableWebDav = true
        };

        var args = TorrServerArgumentBuilder.Build(settings);

        Assert.Contains("--httpauth", args);
        Assert.Contains("--rdb", args);
        Assert.Contains("--searchwa", args);
        Assert.Contains("--webdav", args);
    }

    private static void AssertOption(IReadOnlyList<string> args, string name, string expectedValue)
    {
        var index = args
            .Select((value, itemIndex) => new { value, itemIndex })
            .FirstOrDefault(item => string.Equals(item.value, name, StringComparison.Ordinal))
            ?.itemIndex ?? -1;
        Assert.True(index >= 0, $"Expected option {name}.");
        Assert.True(index + 1 < args.Count, $"Expected value for option {name}.");
        Assert.Equal(expectedValue, args[index + 1]);
    }
}
