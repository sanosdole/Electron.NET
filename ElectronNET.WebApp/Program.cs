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
            using (var cts = new CancellationTokenSource())
            {
                await CreateWebHostBuilder(args, cts).Build().RunAsync(cts.Token);
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
