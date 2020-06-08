using System;
using System.Runtime.Loader;

namespace ElectronNET.API
{
    internal static class BridgeConnector
    {
        private static readonly object id = new object();
        
        public static Socket Socket { get; internal set; }
    }
}
