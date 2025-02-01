using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Nebula.Core
{
    public class PeerNode : IDisposable
    {
        private readonly NetworkManager networkManager;
        private readonly FileManager fileManager;
        private readonly PeerDiscoveryService peerDiscovery;
        private bool isRunning;

        public PeerNode(int port, IPEndPoint bootstrapServer = null)
        {
            networkManager = new NetworkManager(port, port); // Modifica qui
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
                    try
                    {
                        string message = networkManager.ReadTcpMessage(stream);
                        ProcessTcpMessage(message, stream);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"TCP connection error: {ex}");
                    }
                }
            });
        }

        private void ProcessTcpMessage(string message, NetworkStream stream)
        {
            string[] parts = message.Split(':', 3);
            switch (parts[0])
            {
                case "SEARCH":
                    HandleFileSearch(parts[1], stream);
                    break;

                case "REQUEST":
                    HandleFileRequest(parts[1], parts[2], stream);
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

        private void HandleFileRequest(string fileId, string fileName, NetworkStream stream)
        {
            if (fileManager.TryGetFile(fileId, out string filePath))
            {
                networkManager.SendTcpMessage(stream, $"START:{fileName}");
                using var fileStream = File.OpenRead(filePath);
                fileStream.CopyTo(stream);
            }
        }

        private void HandleUdpMessage(UdpReceiveResult message)
        {
            peerDiscovery.HandleUdpMessage(message);
        }

        private void StartUserInterface()
        {
            new Thread(() =>
            {
                while (isRunning)
                {
                    Console.WriteLine("\nCommands:");
                    Console.WriteLine("add <file> - Share file");
                    Console.WriteLine("search <term> - Search files");
                    Console.WriteLine("peers - List known peers");
                    Console.WriteLine("exit - Quit");

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
                        Logger.LogError($"Command error: {ex.Message}");
                    }
                }
            }).Start();
        }

        private void HandleAddFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.LogError("File not found");
                return;
            }

            string fileId = fileManager.AddFile(filePath);
            Logger.LogInfo($"File shared - ID: {fileId}");
        }

        private void HandleFileSearch(string searchTerm)
        {
            if (fileManager.TryGetMetadata(searchTerm, out var metadata))
            {
                foreach (var peer in peerDiscovery.GetKnownPeers())
                {
                    try
                    {
                        using var client = new TcpClient();
                        client.Connect(peer);
                        using var stream = client.GetStream();

                        networkManager.SendTcpMessage(stream, $"SEARCH:{metadata.FileId}");
                        string response = networkManager.ReadTcpMessage(stream);

                        if (response.StartsWith("FOUND"))
                        {
                            DownloadFile(metadata.FileId, metadata.FileName, peer);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Search error: {ex.Message}");
                    }
                }
            }
            Logger.LogInfo("File not found");
        }

        private void DownloadFile(string fileId, string fileName, IPEndPoint peer)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(peer);
                using var stream = client.GetStream();

                networkManager.SendTcpMessage(stream, $"REQUEST:{fileId}:{fileName}");
                string response = networkManager.ReadTcpMessage(stream);

                if (response.StartsWith("START"))
                {
                    string receivedFileName = response.Split(':')[1];
                    fileManager.SaveDownloadedFile(fileId, receivedFileName, stream);
                    Logger.LogInfo($"File downloaded: {receivedFileName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Download failed: {ex.Message}");
            }
        }

        private void ListKnownPeers()
        {
            Logger.LogInfo("Known peers:");
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