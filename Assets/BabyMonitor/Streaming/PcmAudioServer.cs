using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BabyMonitor.Streaming
{
    public sealed class PcmAudioServer : IDisposable
    {
        private readonly int port;
        private readonly string token;

        private TcpListener listener;
        private TcpClient client;
        private NetworkStream stream;

        private bool running;
        private int sending;

        public bool HasClient =>
            client != null &&
            client.Connected &&
            stream != null;

        public PcmAudioServer(
            int port,
            string token)
        {
            this.port = port;
            this.token = token;
        }

        public void Start()
        {
            if (running)
                return;

            listener =
                new TcpListener(
                    IPAddress.Any,
                    port
                );

            listener.Start();

            running = true;

            _ = AcceptLoopAsync();
        }

        private async Task AcceptLoopAsync()
        {
            while (running)
            {
                TcpClient newClient = null;

                try
                {
                    newClient =
                        await listener
                            .AcceptTcpClientAsync();

                    newClient.NoDelay = true;

                    NetworkStream newStream =
                        newClient.GetStream();

                    string receivedToken =
                        await ReadLineAsync(
                            newStream
                        );

                    if (receivedToken != token)
                    {
                        newClient.Close();
                        continue;
                    }

                    CloseClient();

                    client = newClient;
                    stream = newStream;
                }
                catch
                {
                    newClient?.Close();

                    if (!running)
                        return;
                }
            }
        }

        public void TrySendSamples(
            float[] samples)
        {
            if (!HasClient)
                return;

            if (samples == null ||
                samples.Length == 0)
                return;

            if (Interlocked.CompareExchange(
                    ref sending,
                    1,
                    0) != 0)
            {
                return;
            }

            byte[] pcm =
                new byte[samples.Length * 2];

            for (int i = 0;
                 i < samples.Length;
                 i++)
            {
                float value =
                    Math.Max(
                        -1f,
                        Math.Min(
                            1f,
                            samples[i]
                        )
                    );

                short sample =
                    (short)(
                        value *
                        short.MaxValue
                    );

                pcm[i * 2] =
                    (byte)(
                        sample & 0xFF
                    );

                pcm[i * 2 + 1] =
                    (byte)(
                        (sample >> 8) &
                        0xFF
                    );
            }

            _ = SendAsync(pcm);
        }

        private async Task SendAsync(
            byte[] pcm)
        {
            try
            {
                NetworkStream currentStream =
                    stream;

                if (currentStream == null)
                    return;

                byte[] lengthBytes =
                    BitConverter.GetBytes(
                        IPAddress.HostToNetworkOrder(
                            pcm.Length
                        )
                    );

                await currentStream.WriteAsync(
                    lengthBytes,
                    0,
                    4
                );

                await currentStream.WriteAsync(
                    pcm,
                    0,
                    pcm.Length
                );
            }
            catch
            {
                CloseClient();
            }
            finally
            {
                Interlocked.Exchange(
                    ref sending,
                    0
                );
            }
        }

        private static async Task<string>
            ReadLineAsync(
                NetworkStream stream)
        {
            byte[] buffer =
                new byte[1];

            StringBuilder builder =
                new StringBuilder();

            while (true)
            {
                int read =
                    await stream.ReadAsync(
                        buffer,
                        0,
                        1
                    );

                if (read <= 0)
                    return null;

                char c =
                    (char)buffer[0];

                if (c == '\n')
                    break;

                if (c != '\r')
                    builder.Append(c);

                if (builder.Length > 1024)
                    return null;
            }

            return builder.ToString();
        }

        private void CloseClient()
        {
            try
            {
                stream?.Close();
            }
            catch { }

            try
            {
                client?.Close();
            }
            catch { }

            stream = null;
            client = null;
        }

        public void Dispose()
        {
            running = false;

            CloseClient();

            try
            {
                listener?.Stop();
            }
            catch { }

            listener = null;
        }
    }
}