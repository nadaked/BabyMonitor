using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace BabyMonitor.Pairing
{
    public sealed class PairingServer : IDisposable
    {
        private const string RequestPrefix = "BABYMONITOR_PAIR_V1|";
        private const string SuccessResponse = "BABYMONITOR_PAIR_OK_V1";

        private readonly int port;
        private readonly string expectedToken;

        private TcpListener listener;
        private bool running;

        public PairingServer(int port, string expectedToken)
        {
            this.port = port;
            this.expectedToken = expectedToken;
        }

        public void Start()
        {
            if (running)
                return;

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            running = true;
        }

        public async Task<IPAddress> WaitForPairAsync()
        {
            while (running)
            {
                TcpClient client = null;

                try
                {
                    client = await listener.AcceptTcpClientAsync();
                }
                catch
                {
                    if (!running)
                        return null;

                    throw;
                }

                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.AutoFlush = true;

                    string line = await ReadLineWithTimeout(
                        reader,
                        5000
                    );

                    if (string.IsNullOrEmpty(line))
                    {
                        await writer.WriteLineAsync(
                            "BABYMONITOR_PAIR_INVALID"
                        );

                        continue;
                    }

                    string expected =
                        RequestPrefix + expectedToken;

                    if (!ConstantTimeEquals(line, expected))
                    {
                        await writer.WriteLineAsync(
                            "BABYMONITOR_PAIR_INVALID"
                        );

                        continue;
                    }

                    await writer.WriteLineAsync(
                        SuccessResponse
                    );

                    IPAddress remoteAddress = null;

                    if (client.Client.RemoteEndPoint
                        is IPEndPoint endpoint)
                    {
                        remoteAddress = endpoint.Address;
                    }

                    // Token tek kullanımlık.
                    Stop();

                    return remoteAddress;
                }
            }

            return null;
        }

        public void Stop()
        {
            if (!running)
                return;

            running = false;

            try
            {
                listener?.Stop();
            }
            catch
            {
                // Ignore shutdown errors.
            }

            listener = null;
        }

        public void Dispose()
        {
            Stop();
        }

        private static async Task<string> ReadLineWithTimeout(
            StreamReader reader,
            int milliseconds)
        {
            Task<string> readTask = reader.ReadLineAsync();
            Task delayTask = Task.Delay(milliseconds);

            Task completed =
                await Task.WhenAny(readTask, delayTask);

            if (completed != readTask)
                return null;

            return await readTask;
        }

        private static bool ConstantTimeEquals(
            string a,
            string b)
        {
            if (a == null || b == null)
                return false;

            int difference = a.Length ^ b.Length;

            int max = Math.Max(a.Length, b.Length);

            for (int i = 0; i < max; i++)
            {
                char ca = i < a.Length ? a[i] : '\0';
                char cb = i < b.Length ? b[i] : '\0';

                difference |= ca ^ cb;
            }

            return difference == 0;
        }
    }
}