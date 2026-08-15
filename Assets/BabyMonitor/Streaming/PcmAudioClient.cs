using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BabyMonitor.Streaming
{
    public sealed class PcmAudioClient :
        IDisposable
    {
        private readonly object bufferLock =
            new object();

        private readonly float[] ringBuffer;

        private int readPosition;
        private int writePosition;
        private int bufferedSamples;

        private TcpClient client;
        private NetworkStream stream;

        private bool running;

        public PcmAudioClient(
            int sampleRate,
            int bufferSeconds = 3)
        {
            ringBuffer =
                new float[
                    sampleRate *
                    bufferSeconds
                ];
        }

        public async Task<bool> ConnectAsync(
            string address,
            int port,
            string token,
            int timeoutMilliseconds = 5000)
        {
            try
            {
                client =
                    new TcpClient();

                Task connectTask =
                    client.ConnectAsync(
                        address,
                        port
                    );

                Task completed =
                    await Task.WhenAny(
                        connectTask,
                        Task.Delay(
                            timeoutMilliseconds
                        )
                    );

                if (completed != connectTask)
                    return false;

                await connectTask;

                client.NoDelay = true;

                stream =
                    client.GetStream();

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
            byte[] header =
                new byte[4];

            try
            {
                while (running)
                {
                    if (!await ReadExactAsync(
                            header,
                            4))
                        break;

                    int length =
                        IPAddress
                            .NetworkToHostOrder(
                                BitConverter
                                    .ToInt32(
                                        header,
                                        0
                                    )
                            );

                    if (length <= 0 ||
                        length > 1024 * 1024)
                    {
                        break;
                    }

                    byte[] pcm =
                        new byte[length];

                    if (!await ReadExactAsync(
                            pcm,
                            length))
                        break;

                    WritePcm(pcm);
                }
            }
            catch
            {
            }

            running = false;
        }

        private void WritePcm(
            byte[] pcm)
        {
            lock (bufferLock)
            {
                for (int i = 0;
                     i + 1 < pcm.Length;
                     i += 2)
                {
                    short sample =
                        (short)(
                            pcm[i] |
                            (pcm[i + 1] << 8)
                        );

                    float value =
                        sample /
                        32768f;

                    // Buffer doluysa en eski
                    // sample'ı drop et.
                    if (bufferedSamples >=
                        ringBuffer.Length)
                    {
                        readPosition++;

                        if (readPosition >=
                            ringBuffer.Length)
                        {
                            readPosition = 0;
                        }

                        bufferedSamples--;
                    }

                    ringBuffer[writePosition] =
                        value;

                    writePosition++;

                    if (writePosition >=
                        ringBuffer.Length)
                    {
                        writePosition = 0;
                    }

                    bufferedSamples++;
                }
            }
        }

        public void ReadSamples(
            float[] output)
        {
            lock (bufferLock)
            {
                for (int i = 0;
                     i < output.Length;
                     i++)
                {
                    if (bufferedSamples > 0)
                    {
                        output[i] =
                            ringBuffer[
                                readPosition
                            ];

                        readPosition++;

                        if (readPosition >=
                            ringBuffer.Length)
                        {
                            readPosition = 0;
                        }

                        bufferedSamples--;
                    }
                    else
                    {
                        output[i] = 0f;
                    }
                }
            }
        }

        private async Task<bool>
            ReadExactAsync(
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
        }
    }
}