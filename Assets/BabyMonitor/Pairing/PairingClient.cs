using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace BabyMonitor.Pairing
{
    public static class PairingClient
    {
        private const string RequestPrefix =
            "BABYMONITOR_PAIR_V1|";

        private const string SuccessResponse =
            "BABYMONITOR_PAIR_OK_V1";

        public static async Task<bool> ConnectAsync(
            PairingPayload payload,
            int timeoutMilliseconds = 5000)
        {
            if (payload == null)
                return false;

            TcpClient client = new TcpClient();

            try
            {
                Task connectTask =
                    client.ConnectAsync(
                        payload.address,
                        payload.port
                    );

                Task completed =
                    await Task.WhenAny(
                        connectTask,
                        Task.Delay(timeoutMilliseconds)
                    );

                if (completed != connectTask)
                    return false;

                // Rethrow socket exceptions.
                await connectTask;

                using (NetworkStream stream =
                       client.GetStream())
                using (StreamReader reader =
                       new StreamReader(stream))
                using (StreamWriter writer =
                       new StreamWriter(stream))
                {
                    writer.AutoFlush = true;

                    await writer.WriteLineAsync(
                        RequestPrefix + payload.token
                    );

                    string response =
                        await ReadLineWithTimeout(
                            reader,
                            timeoutMilliseconds
                        );

                    return response ==
                           SuccessResponse;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                client.Close();
            }
        }

        private static async Task<string>
            ReadLineWithTimeout(
                StreamReader reader,
                int milliseconds)
        {
            Task<string> readTask =
                reader.ReadLineAsync();

            Task completed =
                await Task.WhenAny(
                    readTask,
                    Task.Delay(milliseconds)
                );

            if (completed != readTask)
                return null;

            return await readTask;
        }
    }
}