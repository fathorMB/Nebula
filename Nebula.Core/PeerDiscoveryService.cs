using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Core
{
    public class PeerDiscoveryService
    {
        private readonly NetworkManager networkManager;
        private readonly List<IPEndPoint> knownPeers = new List<IPEndPoint>();
        private readonly IPEndPoint bootstrapServer;
        private readonly int tcpPort;
        private Timer peerMaintenanceTimer;

        public PeerDiscoveryService(NetworkManager networkManager, int tcpPort, IPEndPoint bootstrapServer = null)
        {
            this.networkManager = networkManager;
            this.tcpPort = tcpPort;
            this.bootstrapServer = bootstrapServer;
        }

        public void Start()
        {
            networkManager.UdpMessageReceived += HandleUdpMessage;
            RegisterWithBootstrapServer();
            StartPeerMaintenance();
        }

        private void RegisterWithBootstrapServer()
        {
            if (bootstrapServer != null)
            {
                networkManager.SendUdpMessage(bootstrapServer, $"REGISTER:{tcpPort}");
            }
        }

        private void StartPeerMaintenance()
        {
            peerMaintenanceTimer = new Timer(_ =>
            {
                BroadcastPeerDiscovery();
                RemoveInactivePeers();
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        private void BroadcastPeerDiscovery()
        {
            foreach (var peer in knownPeers.ToList())
            {
                networkManager.SendUdpMessage(peer, $"PING:{tcpPort}");
            }
        }

        private void RemoveInactivePeers()
        {
            // Implementare logica di rimozione peer inattivi
        }

        private void HandleUdpMessage(UdpReceiveResult message)
        {
            string[] parts = Encoding.UTF8.GetString(message.Buffer).Split(':');
            switch (parts[0])
            {
                case "PING":
                    UpdatePeerList(new IPEndPoint(message.RemoteEndPoint.Address, int.Parse(parts[1])));
                    break;

                case "PEERS":
                    UpdatePeerList(parts[1..].Select(ParseEndPoint).ToArray());
                    break;

                case "REGISTER":
                    var newPeer = new IPEndPoint(message.RemoteEndPoint.Address, int.Parse(parts[1]));
                    UpdatePeerList(newPeer);
                    break;
            }
        }

        private void UpdatePeerList(params IPEndPoint[] peers)
        {
            lock (knownPeers)
            {
                foreach (var peer in peers)
                {
                    if (!knownPeers.Any(p => p.Equals(peer)) && peer.Port != tcpPort)
                    {
                        knownPeers.Add(peer);
                    }
                }
            }
        }

        private IPEndPoint ParseEndPoint(string endpoint)
        {
            string[] parts = endpoint.Split(':');
            return new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
        }

        public IPEndPoint[] GetKnownPeers()
        {
            lock (knownPeers)
            {
                return knownPeers.ToArray();
            }
        }
    }
}
