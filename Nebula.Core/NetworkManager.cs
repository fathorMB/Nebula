using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Core
{
    // File: NetworkManager.cs
    public class NetworkManager : IDisposable
    {
        private TcpListener tcpListener;
        private UdpClient udpClient;
        private readonly int tcpPort;
        private readonly int udpPort;
        private bool isRunning;
        public event Action<TcpClient> TcpConnectionReceived;
        public event Action<UdpReceiveResult> UdpMessageReceived;

        public NetworkManager(int tcpPort, int udpPort)
        {
            this.tcpPort = tcpPort;
            this.udpPort = tcpPort; // Forza stessa porta per UDP
            Logger.LogInfo($"Initialized NetworkManager TCP:{tcpPort} UDP:{udpPort}");
        }


        // Modifica temporanea in NetworkManager.cs
        public void Start()
        {
            try
            {
                Logger.LogInfo($"Starting TCP listener on {tcpPort} and UDP on {udpPort}");
                isRunning = true;
                StartTcpListener();
                StartUdpListener();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Network startup failed: {ex}");
                throw;
            }
        }

        private void StartTcpListener()
        {
            tcpListener = new TcpListener(IPAddress.Any, tcpPort);
            tcpListener.Start();
            new Thread(() =>
            {
                while (isRunning)
                {
                    try
                    {
                        var client = tcpListener.AcceptTcpClient();
                        TcpConnectionReceived?.Invoke(client);
                    }
                    catch (SocketException) { /* Listener stopped */ }
                }
            }).Start();
        }

        private void StartUdpListener()
        {
            try
            {
                udpClient = new UdpClient(udpPort);
                Logger.LogInfo($"UDP listener started on port {udpPort}");
                new Thread(() =>
                {
                    while (isRunning)
                    {
                        try
                        {
                            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                            byte[] data = udpClient.Receive(ref remoteEP);
                            UdpMessageReceived?.Invoke(new UdpReceiveResult(data, remoteEP));
                        }
                        catch { /* UDP listener error */ }
                    }
                }).Start();
            }
            catch (Exception ex)
            {
                Logger.LogError($"UDP listener failed: {ex}");
            }
        }

        public void SendTcpMessage(NetworkStream stream, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }

        public string ReadTcpMessage(NetworkStream stream)
        {
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        public void SendUdpMessage(IPEndPoint endpoint, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            udpClient.Send(data, data.Length, endpoint);
        }

        public void Dispose()
        {
            isRunning = false;
            tcpListener?.Stop();
            udpClient?.Close();
        }
    }

}
