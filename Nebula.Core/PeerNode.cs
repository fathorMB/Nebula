using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Core
{
    // File: PeerNode.cs
    public class PeerNode : IDisposable
    {
        private readonly NetworkManager networkManager;
        private readonly FileManager fileManager;
        private readonly PeerDiscoveryService peerDiscovery;
        private bool isRunning;

        public PeerNode(int port, IPEndPoint bootstrapServer = null)
        {
            networkManager = new NetworkManager(port, port + 1);
            fileManager = new FileManager(port);
            peerDiscovery = new PeerDiscoveryService(networkManager, port, bootstrapServer);

            networkManager.TcpConnectionReceived += HandleTcpConnection;
            networkManager.UdpMessageReceived += HandleUdpMessage;
        }

        public void Start()
        {
            isRunning = true;
            networkManager.Start();
            peerDiscovery.Start();
            StartUserInterface();
        }

        private void HandleTcpConnection(TcpClient client)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    string message = networkManager.ReadTcpMessage(stream);
                    ProcessTcpMessage(message, stream);
                }
            });
        }

        private void ProcessTcpMessage(string message, NetworkStream stream)
        {
            string[] parts = message.Split(':');
            switch (parts[0])
            {
                case "SEARCH":
                    HandleFileSearch(parts[1], stream);
                    break;

                case "REQUEST":
                    HandleFileRequest(parts[1], stream);
                    break;
            }
        }

        private void HandleFileSearch(string fileId, NetworkStream stream)
        {
            if (fileManager.TryGetFile(fileId, out _))
            {
                networkManager.SendTcpMessage(stream, $"FOUND:{fileId}");
            }
            else
            {
                networkManager.SendTcpMessage(stream, "NOT_FOUND");
            }
        }

        private void HandleFileRequest(string fileId, NetworkStream stream)
        {
            if (fileManager.TryGetFile(fileId, out string filePath))
            {
                using var fileStream = File.OpenRead(filePath);
                fileStream.CopyTo(stream);
            }
        }

        private void HandleUdpMessage(UdpReceiveResult message)
        {
            // Gestione aggiuntiva messaggi UDP se necessario
        }

        private void StartUserInterface()
        {
            new Thread(() =>
            {
                while (isRunning)
                {
                    Console.WriteLine("\nComandi:");
                    Console.WriteLine("add <file> - Condividi file");
                    Console.WriteLine("search <id> - Cerca file");
                    Console.WriteLine("peers - Lista peer");
                    Console.WriteLine("exit - Esci");

                    var input = Console.ReadLine()?.Split(' ');
                    if (input == null || input.Length < 1) continue;

                    try
                    {
                        switch (input[0].ToLower())
                        {
                            case "add":
                                if (input.Length > 1) HandleAddFile(input[1]);
                                break;
                            case "search":
                                if (input.Length > 1) HandleFileSearch(input[1]);
                                break;
                            case "peers":
                                ListKnownPeers();
                                break;
                            case "exit":
                                isRunning = false;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Errore: {ex.Message}");
                    }
                }
            }).Start();
        }

        private void HandleAddFile(string filePath)
        {
            string fileId = fileManager.AddFile(filePath);
            Console.WriteLine($"File condiviso - ID: {fileId}");
        }

        private void HandleFileSearch(string fileId)
        {
            foreach (var peer in peerDiscovery.GetKnownPeers())
            {
                try
                {
                    using var client = new TcpClient();
                    client.Connect(peer);
                    using var stream = client.GetStream();

                    networkManager.SendTcpMessage(stream, $"SEARCH:{fileId}");
                    string response = networkManager.ReadTcpMessage(stream);

                    if (response.StartsWith("FOUND"))
                    {
                        Console.WriteLine($"File trovato presso {peer}");
                        DownloadFile(fileId, peer);
                        return;
                    }
                }
                catch { /* Ignora peer non raggiungibile */ }
            }
            Console.WriteLine("File non trovato");
        }

        private void DownloadFile(string fileId, IPEndPoint peer)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(peer);
                using var stream = client.GetStream();

                networkManager.SendTcpMessage(stream, $"REQUEST:{fileId}");
                fileManager.SaveDownloadedFile(fileId, stream);

                Console.WriteLine($"File scaricato: {fileId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download fallito: {ex.Message}");
            }
        }

        private void ListKnownPeers()
        {
            Console.WriteLine("Peer conosciuti:");
            foreach (var peer in peerDiscovery.GetKnownPeers())
            {
                Console.WriteLine(peer);
            }
        }

        public void Dispose()
        {
            networkManager.Dispose();
        }
    }
}
