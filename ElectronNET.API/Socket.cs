using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeHostEnvironment;

namespace ElectronNET.API
{
    /// <summary>
    /// Fake socket implementation using NodeHostEnvironment
    /// </summary>
    public sealed class Socket
    {
        private readonly object _syncRoot = new object();
        private readonly IBridgeToNode node;
        private readonly Dictionary<string, Action<string>> dotnetCallbacks = new Dictionary<string, Action<string>>();
        private readonly Dictionary<string, Action<string>> jsCallbacks = new Dictionary<string, Action<string>>();

        internal Socket(IBridgeToNode node)
        {
            this.node = node;
        }

        internal async void HandleJsMessage(string channel, string jsonArgs)
        {
            Action<string> toInvoke;
            lock(_syncRoot)
            {
                if (!dotnetCallbacks.TryGetValue(channel, out toInvoke))
                    return;
            }

            //Console.WriteLine($"JS emit '{channel}' with '{jsonArgs}'");
            try
            {
                await Task.Run(() => toInvoke(jsonArgs));                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }

        internal void RegisterJsHandler(string channel, dynamic callback)
        {
            //Console.WriteLine($"JS register '{channel}'");
            lock(_syncRoot)
            {
                jsCallbacks[channel] = s => callback(s);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="callback"></param>
        public void On(string channel, Action callback)
        {
            //Console.WriteLine($".NET register '{channel}'");
            lock(_syncRoot)
            {
                dotnetCallbacks[channel] = _ => callback();
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="callback"></param>
        public void On(string channel, Action<object> callback)
        {
            //Console.WriteLine($".NET register '{channel}' with data");
            lock(_syncRoot)
            {
                dotnetCallbacks[channel] = jsonArgs =>
                {
                    var parsedArgs = JArray.Parse(jsonArgs);
                    callback(parsedArgs.Count > 0 ? parsedArgs[0] : null);
                };
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="channel"></param>
        public void Off(string channel)
        {
            //Console.WriteLine($".NET de-register '{channel}'");
            lock(_syncRoot)
            {
                dotnetCallbacks.Remove(channel);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="args"></param>
        public async void Emit(string channel, params object[] args)
        {
            Action<string> toInvoke;
            lock(_syncRoot)
            {
                if (!jsCallbacks.TryGetValue(channel, out toInvoke))
                    return;
            }
            var jsonArgs = JsonConvert.SerializeObject(args ?? new object[0]);
            //Console.WriteLine($".NET emit '{channel}' with '{jsonArgs}'");
            try
            {
                await node.Run(() => toInvoke(jsonArgs));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}