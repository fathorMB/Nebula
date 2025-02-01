using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Nebula.Core
{
    public class PeerDiscoveryService
    {
        private readonly NetworkManager networkManager;
        private readonly Dictionary<IPEndPoint, DateTime> peerActivity = new Dictionary<IPEndPoint, DateTime>();
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

        // In PeerDiscoveryService.cs modifica:
        private void RegisterWithBootstrapServer()
        {
            if (bootstrapServer != null)
            {
                try
                {
                    Logger.LogInfo($"Registering with bootstrap server: {bootstrapServer}");
                    networkManager.SendUdpMessage(bootstrapServer, $"REGISTER:{tcpPort}");

                    // Aggiungi una richiesta PEERS immediata
                    networkManager.SendUdpMessage(bootstrapServer, $"REQUEST_PEERS:{tcpPort}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Bootstrap registration failed: {ex}");
                }
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
            foreach (var peer in peerActivity.Keys.ToList())
            {
                networkManager.SendUdpMessage(peer, $"PING:{tcpPort}");
            }
        }

        private void RemoveInactivePeers()
        {
            lock (peerActivity)
            {
                var cutoff = DateTime.Now.AddMinutes(-5);
                var inactive = peerActivity.Where(kvp => kvp.Value < cutoff).ToList();
                foreach (var peer in inactive)
                {
                    peerActivity.Remove(peer.Key);
                    Logger.LogInfo($"Removed inactive peer: {peer.Key}");
                }
            }
        }

        // In PeerDiscoveryService.cs aggiorna:
        public void HandleUdpMessage(UdpReceiveResult message)
        {
            try
            {
                string msg = Encoding.UTF8.GetString(message.Buffer);
                Logger.LogInfo($"Received UDP message: {msg} from {message.RemoteEndPoint}");

                string[] parts = msg.Split(':');
                var peerEndpoint = new IPEndPoint(message.RemoteEndPoint.Address, int.Parse(parts[1]));

                switch (parts[0])
                {
                    case "PING":
                        Logger.LogInfo($"Processing PING from {peerEndpoint}");
                        UpdatePeerList(peerEndpoint);
                        break;

                    case "PEERS":
                        Logger.LogInfo($"Received PEERS list: {msg}");
                        UpdatePeerList(parts[1..].Select(NetworkUtils.ParseEndPoint).ToArray());
                        break;

                    case "REGISTER":
                        Logger.LogInfo($"New registration from {peerEndpoint}");
                        UpdatePeerList(peerEndpoint);
                        SendPeerList(message.RemoteEndPoint);
                        break;

                    case "REQUEST_PEERS":
                        Logger.LogInfo($"Peer request from {peerEndpoint}");
                        SendPeerList(message.RemoteEndPoint);
                        break;
                }

                lock (peerActivity)
                {
                    peerActivity[peerEndpoint] = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling UDP message: {ex}");
            }
        }

        private void SendPeerList(IPEndPoint recipient)
        {
            try
            {
                var peers = GetKnownPeers()
                    .Where(p => !p.Equals(recipient))
                    .Select(p => $"{p.Address}:{p.Port}");

                if (peers.Any())
                {
                    string peerList = string.Join(",", peers);
                    networkManager.SendUdpMessage(recipient, $"PEERS:{peerList}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sending peer list: {ex}");
            }
        }

        private void UpdatePeerList(params IPEndPoint[] peers)
        {
            lock (peerActivity)
            {
                foreach (var peer in peers)
                {
                    if (!peerActivity.ContainsKey(peer) && peer.Port != tcpPort)
                    {
                        peerActivity[peer] = DateTime.Now;
                        Logger.LogInfo($"Added new peer: {peer}");
                    }
                }
            }
        }

        public IPEndPoint[] GetKnownPeers()
        {
            lock (peerActivity)
            {
                return peerActivity.Keys.ToArray();
            }
        }
    }
}