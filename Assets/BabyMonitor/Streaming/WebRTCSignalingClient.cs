using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BabyMonitor.Streaming
{
    public sealed class WebRTCSignalingClient : IDisposable
    {
        private readonly ConcurrentQueue<WebRTCSignalingMessage> received = new ConcurrentQueue<WebRTCSignalingMessage>();
        private TcpClient client; private StreamWriter writer; private CancellationTokenSource cancellation;
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        public bool IsConnected => client != null && client.Connected;
        public bool TryDequeue(out WebRTCSignalingMessage message) => received.TryDequeue(out message);
        public async Task<bool> ConnectAsync(string address, int port, string token)
        {
            try { cancellation = new CancellationTokenSource(); client = new TcpClient(); await client.ConnectAsync(address, port); NetworkStream stream = client.GetStream(); writer = new StreamWriter(stream) { AutoFlush = true }; StreamReader reader = new StreamReader(stream); await writer.WriteLineAsync(JsonUtility.ToJson(new WebRTCSignalingMessage { type = "auth", token = token })); string response = await reader.ReadLineAsync(); WebRTCSignalingMessage acknowledgement = string.IsNullOrEmpty(response) ? null : JsonUtility.FromJson<WebRTCSignalingMessage>(response); if (acknowledgement == null || acknowledgement.type != "auth-ok") { Dispose(); return false; } _ = ReadAsync(reader, cancellation.Token); Debug.Log("[BabyMonitor] Signaling connected"); return true; }
            catch (Exception e) { Debug.LogWarning($"[BabyMonitor] Signaling connection failed: {e.Message}"); Dispose(); return false; }
        }
        public async Task SendAsync(WebRTCSignalingMessage message) { if (writer == null) return; await writeLock.WaitAsync(); try { if (writer != null) { await writer.WriteLineAsync(JsonUtility.ToJson(message)); await writer.FlushAsync(); } } catch (Exception e) { Debug.LogWarning($"[BabyMonitor] Signaling send failed: {e.Message}"); } finally { writeLock.Release(); } }
        private async Task ReadAsync(StreamReader reader, CancellationToken ct) { try { while (!ct.IsCancellationRequested) { string line = await reader.ReadLineAsync(); if (line == null) break; WebRTCSignalingMessage message = JsonUtility.FromJson<WebRTCSignalingMessage>(line); if (message != null) received.Enqueue(message); } } catch (Exception e) { if (!ct.IsCancellationRequested) Debug.LogWarning($"[BabyMonitor] Signaling read failed: {e.Message}"); } }
        public void Dispose() { cancellation?.Cancel(); writer = null; try { client?.Close(); } catch { } client = null; }
    }
}
