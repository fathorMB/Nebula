using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Core
{
    public static class PortFinder
    {
        public static int FindAvailablePort(int startPort = 1024, int endPort = 65535, bool useIPv6 = false)
        {
            if (startPort < 0 || startPort > endPort || endPort > 65535)
                throw new ArgumentException("Invalid port range");

            var addressFamily = useIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
            var loopbackAddress = useIPv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;

            for (int port = startPort; port <= endPort; port++)
            {
                try
                {
                    using (var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
                    {
                        // Configurazione aggiuntiva per IPv6
                        if (useIPv6)
                            socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

                        socket.Bind(new IPEndPoint(loopbackAddress, port));
                        socket.Listen(1);
                        return port;
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    // Porta occupata, continua la ricerca
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied)
                {
                    // Salta le porte privilegiate (su sistemi UNIX-like)
                    if (port < 1024) continue;
                    throw;
                }
            }

            throw new InvalidOperationException(
                $"Nessuna porta disponibile nel range {startPort}-{endPort} ({addressFamily})");
        }

        public static int FindAvailablePortAuto(int preferredPort = 0)
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Bind(new IPEndPoint(IPAddress.Any, preferredPort));
                    socket.Listen(1);
                    return ((IPEndPoint)socket.LocalEndPoint).Port;
                }
            }
            catch (SocketException)
            {
                return FindAvailablePort();
            }
        }
    }
}
