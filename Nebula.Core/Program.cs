using System.Net;

namespace Nebula.Core
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //if (args.Length < 1)
            //{
            //    Console.WriteLine("Utilizzo: PeerNode <porta> [bootstrap-server]");
            //    return;
            //}

            int availablePort = PortFinder.FindAvailablePortAuto();

            IPEndPoint bootstrap = args.Length > 1 ? ParseEndPoint(args[1]) : null;

            using var node = new PeerNode(availablePort, bootstrap);
            node.Start();

            while (true) Thread.Sleep(1000); // Mantiene l'applicazione attiva
        }

        private static IPEndPoint ParseEndPoint(string endpoint)
        {
            string[] parts = endpoint.Split(':');
            return new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
        }
    }
}
