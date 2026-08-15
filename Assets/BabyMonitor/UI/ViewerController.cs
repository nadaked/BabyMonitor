using System;
using System.Collections;
using System.Net;
using System.Threading.Tasks;
using BabyMonitor.Pairing;
using BabyMonitor.Streaming;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.Common;

namespace BabyMonitor.UI
{
    public sealed class ViewerController : MonoBehaviour
    {
        [SerializeField] private RawImage scannerPreview;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject cameraPlaceholder;
        [SerializeField] private float scanInterval = .25f;
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFPS = 30;

        private WebCamTexture scannerCamera; private BarcodeReader barcodeReader; private bool scanCompleted;
        private byte[] scanBytes;
        private float nextScanTime; private int connectionVersion; private WebRTCSignalingClient signalingClient; private WebRTCViewer webRtcViewer;
        public PairingPayload ScannedPayload { get; private set; }
        private void OnEnable() { ++connectionVersion; scanCompleted = false; ScannedPayload = null; StartCoroutine(StartScanner()); }
        private void OnDisable() { ++connectionVersion; Cleanup(); }
        private void Update() { if (scannerCamera == null || !scannerCamera.isPlaying || scanCompleted || !scannerCamera.didUpdateThisFrame || Time.unscaledTime < nextScanTime) return; nextScanTime = Time.unscaledTime + scanInterval; Scan(); }

        private IEnumerator StartScanner()
        {
            SetStatus("Kamera izni bekleniyor..."); yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) { SetStatus("Kamera izni verilmedi."); yield break; }
            WebCamDevice[] devices = WebCamTexture.devices; if (devices.Length == 0) { SetStatus("Kamera bulunamadı."); yield break; }
            string device = devices[0].name; foreach (WebCamDevice candidate in devices) if (!candidate.isFrontFacing) { device = candidate.name; break; }
            scannerCamera = new WebCamTexture(device, requestedWidth, requestedHeight, requestedFPS); scannerCamera.Play();
            if (scannerPreview != null) scannerPreview.texture = scannerCamera;
            if (cameraPlaceholder != null) cameraPlaceholder.SetActive(false);
            barcodeReader = new BarcodeReader { AutoRotate = true, Options = new DecodingOptions { PossibleFormats = new[] { BarcodeFormat.QR_CODE } } };
            SetStatus("QR kodunu okut."); while (scannerCamera != null && scannerCamera.width <= 16) yield return null;
        }

        private void Scan()
        {
            try
            {
                Color32[] pixels = scannerCamera.GetPixels32();
                int byteCount = pixels.Length * 4;

                if (scanBytes == null || scanBytes.Length != byteCount)
                    scanBytes = new byte[byteCount];

                for (int i = 0; i < pixels.Length; i++)
                {
                    int offset = i * 4;
                    scanBytes[offset] = pixels[i].r;
                    scanBytes[offset + 1] = pixels[i].g;
                    scanBytes[offset + 2] = pixels[i].b;
                    scanBytes[offset + 3] = pixels[i].a;
                }

                Result result = barcodeReader.Decode(
                    scanBytes,
                    scannerCamera.width,
                    scannerCamera.height,
                    RGBLuminanceSource.BitmapFormat.RGBA32
                );

                if (result == null) return;
                PairingPayload payload = JsonUtility.FromJson<PairingPayload>(result.Text); if (!Valid(payload)) return; scanCompleted = true; ScannedPayload = payload; _ = ConnectAsync(payload, connectionVersion);
            }
            catch (Exception e) { Debug.LogWarning($"[BabyMonitor] QR scan failed: {e.Message}"); }
        }
        private async Task ConnectAsync(PairingPayload payload, int version)
        {
            SetStatus("Monitöre bağlanılıyor..."); bool paired = await PairingClient.ConnectAsync(payload);
            if (version != connectionVersion || !isActiveAndEnabled) return;
            if (!paired) { SetStatus("Bağlantı kurulamadı."); scanCompleted = false; return; }
            Debug.Log("[BabyMonitor] Pairing successful"); StopScanner(); SetStatus("Yayın hazırlanıyor...");
            signalingClient = new WebRTCSignalingClient();
            if (!await signalingClient.ConnectAsync(payload.address, payload.port + WebRTCSignalingServer.PortOffset, payload.token)) { SetStatus("Signaling bağlantısı kurulamadı."); return; }
            if (version != connectionVersion || !isActiveAndEnabled) return;
            webRtcViewer = gameObject.AddComponent<WebRTCViewer>(); webRtcViewer.StatusChanged += SetStatus; webRtcViewer.Begin(signalingClient, scannerPreview);
        }
        private void StopScanner() { if (scannerCamera != null) { scannerCamera.Stop(); scannerCamera = null; } barcodeReader = null; }
        private void Cleanup() { StopScanner(); if (webRtcViewer != null) { webRtcViewer.StatusChanged -= SetStatus; Destroy(webRtcViewer); webRtcViewer = null; } signalingClient?.Dispose(); signalingClient = null; }
        private static bool Valid(PairingPayload p) => p != null && p.version == 1 && p.port > 0 && p.port <= 65535 && !string.IsNullOrEmpty(p.token) && IPAddress.TryParse(p.address, out _);
        private void SetStatus(string value) { if (statusText != null) statusText.text = value; }
    }
}
