using System;
using System.Net;
using System.Threading;

namespace Nebula.Core
{
    internal class Program
    {
        static void Main(string[] args)
        {
            int port = args.Length > 0 ? int.Parse(args[0]) : PortFinder.FindAvailablePortAuto();
            IPEndPoint bootstrap = args.Length > 1 ? NetworkUtils.ParseEndPoint(args[1]) : null;

            try
            {
                using var node = new PeerNode(port, bootstrap);
                node.Start();
                Logger.LogInfo($"Node started on port {port}" +
                    (bootstrap != null ? $" with bootstrap server {bootstrap}" : ""));

                while (true) Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Critical error: {ex}");
                Environment.Exit(1);
            }
        }
    }
}