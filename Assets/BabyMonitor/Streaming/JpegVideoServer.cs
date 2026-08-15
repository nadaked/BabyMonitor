using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BabyMonitor.Streaming
{
    public sealed class JpegVideoServer : IDisposable
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

        public JpegVideoServer(int port, string token)
        {
            this.port = port;
            this.token = token;
        }

        public void Start()
        {
            if (running)
                return;

            listener = new TcpListener(IPAddress.Any, port);
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
                    newClient = await listener.AcceptTcpClientAsync();

                    NetworkStream newStream =
                        newClient.GetStream();

                    string receivedToken =
                        await ReadLineAsync(newStream);

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

        public void TrySendFrame(
            byte[] jpeg,
            int rotation,
            bool verticallyMirrored)
        {
            if (!HasClient)
                return;

            if (jpeg == null || jpeg.Length == 0)
                return;

            // Önceki frame hâlâ gönderiliyorsa yenisini drop et.
            if (Interlocked.CompareExchange(
                    ref sending,
                    1,
                    0) != 0)
                return;

            _ = SendFrameAsync(
                jpeg,
                rotation,
                verticallyMirrored
            );
        }

        private async Task SendFrameAsync(
            byte[] jpeg,
            int rotation,
            bool mirrored)
        {
            try
            {
                NetworkStream currentStream = stream;

                if (currentStream == null)
                    return;

                byte[] header = new byte[9];

                WriteInt(header, 0, jpeg.Length);
                WriteInt(header, 4, rotation);

                header[8] =
                    mirrored ? (byte)1 : (byte)0;

                await currentStream.WriteAsync(
                    header,
                    0,
                    header.Length
                );

                await currentStream.WriteAsync(
                    jpeg,
                    0,
                    jpeg.Length
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

        private static void WriteInt(
            byte[] buffer,
            int offset,
            int value)
        {
            int networkValue =
                IPAddress.HostToNetworkOrder(value);

            byte[] bytes =
                BitConverter.GetBytes(networkValue);

            Buffer.BlockCopy(
                bytes,
                0,
                buffer,
                offset,
                4
            );
        }

        private static async Task<string>
            ReadLineAsync(NetworkStream stream)
        {
            byte[] oneByte = new byte[1];
            StringBuilder builder =
                new StringBuilder();

            while (true)
            {
                int count =
                    await stream.ReadAsync(
                        oneByte,
                        0,
                        1
                    );

                if (count <= 0)
                    return null;

                char c = (char)oneByte[0];

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