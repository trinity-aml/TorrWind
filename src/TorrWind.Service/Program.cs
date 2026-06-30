using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TorrWind.Core;
using TorrWind.Core.Services;

namespace TorrWind.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            AppPaths.EnsureWorkingDirectories();
            FileEventLog.Service.Info("Service", "TorrWind.Service process starting.");
        }
        catch
        {
            // Service startup must not depend on log initialization.
        }

        if (await ServiceCommandRunner.TryRunAsync(args).ConfigureAwait(false))
        {
            return;
        }

        await Host.CreateDefaultBuilder(args)
            .UseWindowsService(options => options.ServiceName = "TorrWindService")
            .ConfigureServices(services =>
            {
                services.AddHostedService<TorrServerWorker>();
            })
            .Build()
            .RunAsync()
            .ConfigureAwait(false);
    }
}
