using System;
using System.Collections;
using System.Threading.Tasks;
using BabyMonitor.Pairing;
using BabyMonitor.Streaming;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BabyMonitor.UI
{
    public sealed class MonitorController : MonoBehaviour
    {
        [SerializeField] private RawImage qrImage;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject qrPlaceholder;

        private PairingSession pairingSession;
        private PairingServer pairingServer;
        private WebRTCSignalingServer signalingServer;
        private WebRTCMonitor webRtcMonitor;
        private Texture2D qrTexture;
        private int sessionVersion;

        public PairingSession PairingSession => pairingSession;
        private void OnEnable() => StartSession();
        private void OnDisable() => Cleanup();

        private void StartSession()
        {
            Cleanup();
            int version = ++sessionVersion;
            try
            {
                pairingSession = new PairingSession();
                pairingServer = new PairingServer(pairingSession.Payload.port, pairingSession.Payload.token);
                pairingServer.Start();
                signalingServer = new WebRTCSignalingServer(pairingSession.Payload.port + WebRTCSignalingServer.PortOffset, pairingSession.Payload.token);
                signalingServer.Start();
                qrTexture = QRCodeGenerator.Generate(pairingSession.ToJson(), 512);
                if (qrImage != null) { qrImage.texture = qrTexture; qrImage.color = Color.white; }
                if (qrPlaceholder != null) qrPlaceholder.SetActive(false);
                SetStatus("İzleyici bekleniyor...");
                _ = WaitForPairingAsync(version);
            }
            catch (Exception e) { Debug.LogException(e); SetStatus("Eşleştirme başlatılamadı."); Cleanup(); }
        }

        private async Task WaitForPairingAsync(int version)
        {
            try
            {
                await pairingServer.WaitForPairAsync();
                if (version != sessionVersion || !isActiveAndEnabled) return;
                Debug.Log("[BabyMonitor] Pairing successful"); SetStatus("Eşleşti ✓");
                webRtcMonitor = gameObject.AddComponent<WebRTCMonitor>();
                webRtcMonitor.StatusChanged += SetStatus;
                webRtcMonitor.Begin(signalingServer, qrImage);
            }
            catch (Exception e) { if (version == sessionVersion && isActiveAndEnabled) { Debug.LogException(e); SetStatus("Eşleştirme hatası."); } }
        }

        private void Cleanup()
        {
            ++sessionVersion;
            if (webRtcMonitor != null) { webRtcMonitor.StatusChanged -= SetStatus; Destroy(webRtcMonitor); webRtcMonitor = null; }
            signalingServer?.Dispose(); signalingServer = null;
            pairingServer?.Dispose(); pairingServer = null;
            if (qrTexture != null) { Destroy(qrTexture); qrTexture = null; }
        }
        private void SetStatus(string value) { if (statusText != null) statusText.text = value; }
    }
}
