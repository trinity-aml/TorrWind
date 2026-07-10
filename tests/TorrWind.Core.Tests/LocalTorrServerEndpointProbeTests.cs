using TorrWind.Core.Models;
using TorrWind.Core.Services;

namespace TorrWind.Core.Tests;

public sealed class LocalTorrServerEndpointProbeTests
{
    [Theory]
    [InlineData("not a host ???", 8090, false, 8091)]
    [InlineData("127.0.0.1", 70000, false, 8091)]
    [InlineData("127.0.0.1", 8090, true, 70000)]
    public async Task IsOnlineAsync_ReturnsFalseForInvalidProbeEndpoint(
        string listenAddress,
        int port,
        bool useSsl,
        int sslPort)
    {
        var result = await LocalTorrServerEndpointProbe.IsOnlineAsync(
            new LocalServerSettings
            {
                ListenAddress = listenAddress,
                Port = port,
                UseSsl = useSsl,
                SslPort = sslPort
            },
            TimeSpan.FromMilliseconds(1));

        Assert.False(result);
    }
}
