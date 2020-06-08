using System.Threading;
using System.Threading.Tasks;
using ElectronNET.API;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ElectronNET.WebApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using(var cts = new CancellationTokenSource())
            {
                var host = CreateWebHostBuilder(args, cts).Build();
                // TODO: Use another thread so ASP does not use the JS thread context?
                //await Task.Run(() => host.RunAsync(cts.Token), cts.Token);
                await host.RunAsync(cts.Token);
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args, CancellationTokenSource cts)
        {
            return WebHost.CreateDefaultBuilder(args)
                .ConfigureLogging((hostingContext, logging) => { logging.AddConsole(); })
                .UseElectron(args, cts)
                .UseStartup<Startup>();
        }
    }
}