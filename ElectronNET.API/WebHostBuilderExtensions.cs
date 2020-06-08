using System;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NodeHostEnvironment;

namespace ElectronNET.API
{
    /// <summary>
    /// 
    /// </summary>
    public static class WebHostBuilderExtensions
    {
        /// <summary>
        /// Use a Electron support for this .NET Core Project.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="cts">Source for stopping the web app from electron</param>
        /// <returns></returns>
        public static IWebHostBuilder UseElectron(this IWebHostBuilder builder, string[] args, CancellationTokenSource cts)
        {
            foreach (string argument in args)
            {
                if (argument.ToUpper().Contains("ELECTRONWEBPORT"))
                {
                    BridgeSettings.WebPort = argument.ToUpper().Replace("/ELECTRONWEBPORT=", "");
                }
            }

            var node = NodeHost.Instance;
            if (null == node)
                return builder; // We are not running in electron, so we do not enable the API

            HybridSupport.IsElectronActive = true;
            var global = node.Global;
            global.stopCoreApp = new Action(() => cts.Cancel());

            var socket = new Socket(node);

            global.initializeElectronNetApi(new Action<string, string>(socket.HandleJsMessage),
                new Action<string, dynamic>(socket.RegisterJsHandler));
            BridgeConnector.Socket = socket;            

            builder.ConfigureServices(services =>
            {
                services.AddHostedService<LifetimeServiceHost>();
            });

            // check for the content folder if its exists in base director otherwise no need to include
            // It was used before because we are publishing the project which copies everything to bin folder and contentroot wwwroot was folder there.
            // now we have implemented the live reload if app is run using /watch then we need to use the default project path.
            if (Directory.Exists($"{AppDomain.CurrentDomain.BaseDirectory}\\wwwroot"))
            {
                builder.UseContentRoot(AppDomain.CurrentDomain.BaseDirectory)
                    .UseUrls("http://localhost:" + BridgeSettings.WebPort);
            }
            else
            {
                builder.UseUrls("http://localhost:" + BridgeSettings.WebPort);
            }

            return builder;
        }
    }
}