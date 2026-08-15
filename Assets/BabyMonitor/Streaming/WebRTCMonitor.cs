using System;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

namespace BabyMonitor.Streaming
{
    public sealed class WebRTCMonitor : MonoBehaviour
    {
        [SerializeField] private RawImage previewTarget;
        private WebRTCSignalingServer signaling;
        private RTCPeerConnection peer; private VideoStreamTrack videoTrack; private AudioStreamTrack audioTrack;
        private WebCamTexture cameraTexture; private AudioSource microphoneSource;
        private string microphoneDevice; private bool answerApplied;
        public event Action<string> StatusChanged;
        public void Begin(WebRTCSignalingServer server, RawImage preview)
        {
            signaling = server; previewTarget = preview; StartCoroutine(StartMediaAndOffer());
        }
        private IEnumerator StartMediaAndOffer()
        {
            StatusChanged?.Invoke("Yayın hazırlanıyor...");
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam | UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam) || !Application.HasUserAuthorization(UserAuthorization.Microphone)) { StatusChanged?.Invoke("Kamera veya mikrofon izni verilmedi."); yield break; }
            WebCamDevice[] devices = WebCamTexture.devices; if (devices.Length == 0 || Microphone.devices.Length == 0) { StatusChanged?.Invoke("Kamera veya mikrofon bulunamadı."); yield break; }
            string cameraDevice = devices[0].name; foreach (WebCamDevice device in devices) if (!device.isFrontFacing) { cameraDevice = device.name; break; }
            cameraTexture = new WebCamTexture(cameraDevice, 1280, 720, 30); cameraTexture.Play();
            while (cameraTexture != null && cameraTexture.width <= 16) yield return null;
            if (cameraTexture == null) yield break;
            if (previewTarget != null) previewTarget.texture = cameraTexture;
            microphoneDevice = Microphone.devices[0];
            AudioClip clip = Microphone.Start(microphoneDevice, true, 1, 48000);
            while (Microphone.GetPosition(microphoneDevice) <= 0) yield return null;
            microphoneSource = gameObject.AddComponent<AudioSource>();
            microphoneSource.clip = clip;
            microphoneSource.loop = true;
            microphoneSource.mute = false;
            microphoneSource.volume = 1f;
            microphoneSource.spatialBlend = 0f;
            microphoneSource.Play();
            StartCoroutine(WebRTC.Update());
            peer = new RTCPeerConnection();
            peer.OnIceCandidate = candidate => _ = signaling.SendAsync(new WebRTCSignalingMessage { type = "ice", candidate = candidate.Candidate, sdpMid = candidate.SdpMid, sdpMLineIndex = candidate.SdpMLineIndex ?? 0 });
            peer.OnConnectionStateChange = state => { if (state == RTCPeerConnectionState.Connected) { Debug.Log("[BabyMonitor] WebRTC connected"); StatusChanged?.Invoke("CANLI ●  SES ●"); } else if (state == RTCPeerConnectionState.Disconnected || state == RTCPeerConnectionState.Failed) { Debug.Log("[BabyMonitor] WebRTC disconnected"); StatusChanged?.Invoke("Bağlantı kesildi"); } };
            videoTrack = new VideoStreamTrack(cameraTexture); audioTrack = new AudioStreamTrack(microphoneSource); audioTrack.Loopback = false;
            RTCRtpSender videoSender = peer.AddTrack(videoTrack); peer.AddTrack(audioTrack);
            RTCRtpSendParameters parameters = videoSender.GetParameters(); foreach (RTCRtpEncodingParameters encoding in parameters.encodings) { encoding.maxBitrate = 4000000; encoding.maxFramerate = 30; } videoSender.SetParameters(parameters);
            while (signaling != null && !signaling.IsConnected) yield return null;
            if (signaling == null) yield break;
            yield return new WaitForEndOfFrame();
            RTCOfferAnswerOptions options = default; RTCSessionDescriptionAsyncOperation offer = peer.CreateOffer(ref options); yield return offer;
            RTCSessionDescription description = offer.Desc; RTCSetSessionDescriptionAsyncOperation set = peer.SetLocalDescription(ref description); yield return set;
            _ = signaling.SendAsync(new WebRTCSignalingMessage { type = "offer", sdp = description.sdp }); Debug.Log("[BabyMonitor] WebRTC offer sent"); StatusChanged?.Invoke("Bağlanıyor...");
        }
        private void Update()
        {
            if (signaling == null || peer == null) return;
            while (signaling.TryDequeue(out WebRTCSignalingMessage message))
            {
                if (message.type == "answer" && !answerApplied) { answerApplied = true; RTCSessionDescription description = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = message.sdp }; _ = peer.SetRemoteDescription(ref description); Debug.Log("[BabyMonitor] WebRTC answer received"); }
                else if (message.type == "ice") { peer.AddIceCandidate(new RTCIceCandidate(new RTCIceCandidateInit { candidate = message.candidate, sdpMid = message.sdpMid, sdpMLineIndex = message.sdpMLineIndex })); Debug.Log("[BabyMonitor] ICE candidate added"); }
            }
        }
        private void OnDestroy() { if (cameraTexture != null) cameraTexture.Stop(); if (!string.IsNullOrEmpty(microphoneDevice)) Microphone.End(microphoneDevice); peer?.Close(); peer?.Dispose(); videoTrack?.Dispose(); audioTrack?.Dispose(); }
    }

}
