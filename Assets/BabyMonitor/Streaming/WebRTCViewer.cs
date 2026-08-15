using System;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

namespace BabyMonitor.Streaming
{
    public sealed class WebRTCViewer : MonoBehaviour
    {
        private WebRTCSignalingClient signaling; private RawImage target; private AudioSource audioSource;
        private GameObject audioOutput;
        private RTCPeerConnection peer; private MediaStream receiveStream; private VideoStreamTrack remoteVideoTrack; private bool offerHandled;
        public event Action<string> StatusChanged;
        public void Begin(WebRTCSignalingClient client, RawImage preview)
        {
            signaling = client; target = preview;
            audioOutput = new GameObject("WebRTCRemoteAudio");
            audioOutput.transform.SetParent(transform, false);
            audioSource = audioOutput.AddComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.mute = false;
            audioSource.volume = 1f;
            audioSource.spatialBlend = 0f;
            StartCoroutine(WebRTC.Update());
            peer = new RTCPeerConnection(); receiveStream = new MediaStream();
            receiveStream.OnAddTrack = e =>
            {
                if (e.Track is VideoStreamTrack video) { remoteVideoTrack = video; Debug.Log("[BabyMonitor] WebRTC video track received"); }
                if (e.Track is AudioStreamTrack audio) { audioSource.SetTrack(audio); audioSource.Play(); }
            };
            peer.OnTrack = e => receiveStream.AddTrack(e.Track);
            peer.OnIceCandidate = candidate => _ = signaling.SendAsync(new WebRTCSignalingMessage { type = "ice", candidate = candidate.Candidate, sdpMid = candidate.SdpMid, sdpMLineIndex = candidate.SdpMLineIndex ?? 0 });
            peer.OnConnectionStateChange = state => { if (state == RTCPeerConnectionState.Connected) { Debug.Log("[BabyMonitor] WebRTC connected"); StatusChanged?.Invoke("CANLI ●  SES ●"); } else if (state == RTCPeerConnectionState.Disconnected || state == RTCPeerConnectionState.Failed) { Debug.Log("[BabyMonitor] WebRTC disconnected"); StatusChanged?.Invoke("Bağlantı kesildi"); } };
            StatusChanged?.Invoke("Bağlanıyor...");
        }
        private void Update()
        {
            if (remoteVideoTrack != null && remoteVideoTrack.Texture != null && target != null)
                target.texture = remoteVideoTrack.Texture;

            if (peer == null || signaling == null) return;
            while (signaling.TryDequeue(out WebRTCSignalingMessage message))
            {
                if (message.type == "offer" && !offerHandled) { offerHandled = true; StartCoroutine(HandleOffer(message)); }
                else if (message.type == "ice") { peer.AddIceCandidate(new RTCIceCandidate(new RTCIceCandidateInit { candidate = message.candidate, sdpMid = message.sdpMid, sdpMLineIndex = message.sdpMLineIndex })); Debug.Log("[BabyMonitor] ICE candidate added"); }
            }
        }
        private System.Collections.IEnumerator HandleOffer(WebRTCSignalingMessage message)
        {
            RTCSessionDescription offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = message.sdp };
            RTCSetSessionDescriptionAsyncOperation set = peer.SetRemoteDescription(ref offer);
            yield return set;
            if (set.IsError) yield break;
            RTCOfferAnswerOptions options = default;
            RTCSessionDescriptionAsyncOperation answer = peer.CreateAnswer(ref options);
            yield return answer;
            if (answer.IsError) yield break;
            RTCSessionDescription description = answer.Desc;
            RTCSetSessionDescriptionAsyncOperation local = peer.SetLocalDescription(ref description);
            yield return local;
            if (local.IsError) yield break;
            _ = signaling.SendAsync(new WebRTCSignalingMessage { type = "answer", sdp = description.sdp });
            Debug.Log("[BabyMonitor] WebRTC answer sent");
        }
        private void OnDestroy() { if (audioSource != null) audioSource.Stop(); receiveStream?.Dispose(); peer?.Close(); peer?.Dispose(); if (audioOutput != null) Destroy(audioOutput); }
    }
}
