using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BabyMonitor.Streaming
{
    public sealed class JpegVideoClient : IDisposable
    {
        public sealed class Frame
        {
            public byte[] Data;
            public int Rotation;
            public bool VerticallyMirrored;
        }

        private TcpClient client;
        private NetworkStream stream;

        private readonly ConcurrentQueue<Frame>
            frames = new ConcurrentQueue<Frame>();

        private bool running;

        public async Task<bool> ConnectAsync(
            string address,
            int port,
            string token,
            int timeoutMilliseconds = 5000)
        {
            try
            {
                client = new TcpClient();

                Task connectTask =
                    client.ConnectAsync(
                        address,
                        port
                    );

                Task completed =
                    await Task.WhenAny(
                        connectTask,
                        Task.Delay(timeoutMilliseconds)
                    );

                if (completed != connectTask)
                    return false;

                await connectTask;

                stream = client.GetStream();

                byte[] tokenBytes =
                    Encoding.UTF8.GetBytes(
                        token + "\n"
                    );

                await stream.WriteAsync(
                    tokenBytes,
                    0,
                    tokenBytes.Length
                );

                running = true;

                _ = ReceiveLoopAsync();

                return true;
            }
            catch
            {
                Dispose();
                return false;
            }
        }

        private async Task ReceiveLoopAsync()
        {
            byte[] header = new byte[9];

            try
            {
                while (running)
                {
                    if (!await ReadExactAsync(
                            stream,
                            header,
                            header.Length))
                        break;

                    int length =
                        ReadInt(header, 0);

                    int rotation =
                        ReadInt(header, 4);

                    bool mirrored =
                        header[8] != 0;

                    if (length <= 0 ||
                        length > 10 * 1024 * 1024)
                        break;

                    byte[] jpeg =
                        new byte[length];

                    if (!await ReadExactAsync(
                            stream,
                            jpeg,
                            jpeg.Length))
                        break;

                    // Viewer geride kalırsa eski frameleri çöpe at.
                    while (frames.TryDequeue(out _))
                    {
                    }

                    frames.Enqueue(
                        new Frame
                        {
                            Data = jpeg,
                            Rotation = rotation,
                            VerticallyMirrored = mirrored
                        }
                    );
                }
            }
            catch
            {
                // Disconnect.
            }

            running = false;
        }

        public bool TryGetLatestFrame(
            out Frame frame)
        {
            frame = null;

            while (frames.TryDequeue(
                       out Frame next))
            {
                frame = next;
            }

            return frame != null;
        }

        private static int ReadInt(
            byte[] buffer,
            int offset)
        {
            int value =
                BitConverter.ToInt32(
                    buffer,
                    offset
                );

            return IPAddress.NetworkToHostOrder(
                value
            );
        }

        private static async Task<bool>
            ReadExactAsync(
                NetworkStream stream,
                byte[] buffer,
                int length)
        {
            int offset = 0;

            while (offset < length)
            {
                int read =
                    await stream.ReadAsync(
                        buffer,
                        offset,
                        length - offset
                    );

                if (read <= 0)
                    return false;

                offset += read;
            }

            return true;
        }

        public void Dispose()
        {
            running = false;

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

            while (frames.TryDequeue(out _))
            {
            }
        }
    }
}