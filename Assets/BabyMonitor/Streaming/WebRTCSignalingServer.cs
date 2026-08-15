using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BabyMonitor.Streaming
{
    public sealed class WebRTCSignalingServer : IDisposable
    {
        public const int PortOffset = 1;
        private readonly int port; private readonly string token;
        private readonly ConcurrentQueue<WebRTCSignalingMessage> received = new ConcurrentQueue<WebRTCSignalingMessage>();
        private TcpListener listener; private StreamWriter writer; private CancellationTokenSource cancellation;
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);

        public WebRTCSignalingServer(int port, string token) { this.port = port; this.token = token; }
        public bool IsConnected => writer != null;
        public void Start() { cancellation = new CancellationTokenSource(); listener = new TcpListener(IPAddress.Any, port); listener.Start(); _ = AcceptAsync(cancellation.Token); }
        public bool TryDequeue(out WebRTCSignalingMessage message) => received.TryDequeue(out message);
        public async Task SendAsync(WebRTCSignalingMessage message)
        {
            if (writer == null) return;
            await writeLock.WaitAsync();
            try { if (writer != null) { await writer.WriteLineAsync(JsonUtility.ToJson(message)); await writer.FlushAsync(); } }
            catch (Exception e) { Debug.LogWarning($"[BabyMonitor] Signaling send failed: {e.Message}"); }
            finally { writeLock.Release(); }
        }
        private async Task AcceptAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    using (client)
                    using (NetworkStream stream = client.GetStream())
                    using (StreamReader reader = new StreamReader(stream))
                    using (StreamWriter newWriter = new StreamWriter(stream) { AutoFlush = true })
                    {
                    string auth = await reader.ReadLineAsync();
                    WebRTCSignalingMessage hello = string.IsNullOrEmpty(auth) ? null : JsonUtility.FromJson<WebRTCSignalingMessage>(auth);
                    if (hello == null || hello.type != "auth" || !ConstantTimeEquals(hello.token, token)) continue;
                    writer = newWriter;
                    await writer.WriteLineAsync(JsonUtility.ToJson(new WebRTCSignalingMessage { type = "auth-ok" }));
                    Debug.Log("[BabyMonitor] Signaling connected");
                    while (!ct.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync();
                        if (line == null) break;
                        WebRTCSignalingMessage message = JsonUtility.FromJson<WebRTCSignalingMessage>(line);
                        if (message != null) received.Enqueue(message);
                    }
                    }
                }
            }
            catch (Exception e) { if (!ct.IsCancellationRequested) Debug.LogWarning($"[BabyMonitor] Signaling server stopped: {e.Message}"); }
            finally { writer = null; }
        }
        public void Dispose() { cancellation?.Cancel(); writer = null; try { listener?.Stop(); } catch { } listener = null; }
        private static bool ConstantTimeEquals(string a, string b) { if (a == null || b == null) return false; int d = a.Length ^ b.Length; for (int i = 0; i < Math.Max(a.Length, b.Length); i++) d |= (i < a.Length ? a[i] : '\0') ^ (i < b.Length ? b[i] : '\0'); return d == 0; }
    }
}
