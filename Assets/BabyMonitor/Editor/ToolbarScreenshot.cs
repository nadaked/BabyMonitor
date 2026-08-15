// Inspired by Yasir Kula's Multi Screenshot Capture:
// https://yasirkula.com/2020/08/08/unity-oyunlarinizdan-screenshotlar-almak-editor-icerisinden/

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Nadaked.Editor.Toolbar
{
    public static class ToolbarScreenshot
    {
        private const string ScreenshotFolderName = "Screenshots";
        private const int WarmupFrameCount = 2;
        private const int MaxCaptureAttempts = 60;

        private static readonly Type GameViewType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        private static EditorWindow _gameView;
        private static Camera _targetCamera;
        private static int _warmupFramesRemaining;
        private static int _captureAttemptsRemaining;
        private static bool _captureQueued;

        [MainToolbarElement("Nadaked/CaptureScreenshot", defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement CreateScreenshotButton()
        {
            var icon = EditorGUIUtility.IconContent("Camera Icon").image as Texture2D;

            var content = icon != null
                ? new MainToolbarContent(icon, "Capture Game View Screenshot")
                : new MainToolbarContent("Capture", "Capture Game View Screenshot");

            return new MainToolbarButton(content, QueueCapture);
        }

        private static void QueueCapture()
        {
            if (_captureQueued)
                return;

            _targetCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();

            if (_targetCamera == null)
            {
                Debug.LogWarning("Screenshot capture failed: No active Camera was found.");
                return;
            }

            if (GameViewType == null)
            {
                Debug.LogWarning("Screenshot capture failed: Game View type could not be found.");
                return;
            }

            _gameView = EditorWindow.GetWindow(GameViewType);

            if (_gameView == null)
            {
                Debug.LogWarning("Screenshot capture failed: Game View could not be opened.");
                return;
            }

            _captureQueued = true;
            _warmupFramesRemaining = WarmupFrameCount;
            _captureAttemptsRemaining = MaxCaptureAttempts;

            EditorApplication.update -= TryCaptureScreenshot;
            EditorApplication.update += TryCaptureScreenshot;

            _gameView.Repaint();
        }

        private static void TryCaptureScreenshot()
        {
            if (!_captureQueued)
            {
                ClearCaptureQueue();
                return;
            }

            if (_targetCamera == null)
            {
                Debug.LogWarning("Screenshot capture failed: Target Camera is no longer available.");
                ClearCaptureQueue();
                return;
            }

            if (_gameView == null)
            {
                Debug.LogWarning("Screenshot capture failed: Game View is no longer available.");
                ClearCaptureQueue();
                return;
            }

            _gameView.Repaint();

            if (_warmupFramesRemaining > 0)
            {
                _warmupFramesRemaining--;
                return;
            }

            var renderTexture = GetGameViewTargetTexture();

            if (renderTexture == null || renderTexture.width <= 0 || renderTexture.height <= 0)
            {
                _captureAttemptsRemaining--;

                if (_captureAttemptsRemaining <= 0)
                {
                    Debug.LogWarning("Screenshot capture failed: Game View render texture was not ready.");
                    ClearCaptureQueue();
                }

                return;
            }

            try
            {
                var filePath = CaptureGameView(renderTexture);

                Debug.Log(
                    $"<b>Screenshot saved:</b> {filePath}\n" +
                    $"<b>Resolution:</b> {renderTexture.width}x{renderTexture.height}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                ClearCaptureQueue();
            }
        }

        private static RenderTexture GetGameViewTargetTexture()
        {
            var currentType = _gameView.GetType();

            while (currentType != null)
            {
                var field = currentType.GetField(
                    "m_TargetTexture",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (field != null)
                    return field.GetValue(_gameView) as RenderTexture;

                currentType = currentType.BaseType;
            }

            return null;
        }

        private static string CaptureGameView(RenderTexture renderTexture)
        {
            var previousRenderTexture = RenderTexture.active;
            Texture2D screenshot = null;

            try
            {
                RenderTexture.active = renderTexture;

                screenshot = new Texture2D(
                    renderTexture.width,
                    renderTexture.height,
                    TextureFormat.RGB24,
                    false);

                screenshot.ReadPixels(
                    new Rect(0, 0, renderTexture.width, renderTexture.height),
                    0,
                    0,
                    false);

                if (SystemInfo.graphicsUVStartsAtTop)
                    FlipVertically(screenshot);

                screenshot.Apply(false, false);

                var filePath = GetUniqueFilePath(
                    renderTexture.width,
                    renderTexture.height);

                File.WriteAllBytes(filePath, screenshot.EncodeToPNG());

                return filePath;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;

                if (screenshot != null)
                    UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }

        private static void FlipVertically(Texture2D texture)
        {
            var width = texture.width;
            var height = texture.height;
            var pixels = texture.GetPixels32();

            for (var y = 0; y < height / 2; y++)
            {
                var upperRow = y * width;
                var lowerRow = (height - y - 1) * width;

                for (var x = 0; x < width; x++)
                {
                    var upperPixel = upperRow + x;
                    var lowerPixel = lowerRow + x;

                    (pixels[upperPixel], pixels[lowerPixel]) =
                        (pixels[lowerPixel], pixels[upperPixel]);
                }
            }

            texture.SetPixels32(pixels);
        }

        private static string GetUniqueFilePath(int width, int height)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                              ?? Application.dataPath;

            var screenshotDirectory = Path.Combine(
                projectRoot,
                ScreenshotFolderName);

            Directory.CreateDirectory(screenshotDirectory);

            var productName = SanitizeFileName(Application.productName);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            var baseFileName =
                $"{productName}_{timestamp}_{width}x{height}";

            var filePath = Path.Combine(
                screenshotDirectory,
                $"{baseFileName}.png");

            var fileIndex = 1;

            while (File.Exists(filePath))
            {
                filePath = Path.Combine(
                    screenshotDirectory,
                    $"{baseFileName}_{fileIndex:00}.png");

                fileIndex++;
            }

            return filePath;
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Screenshot";

            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalidCharacter, '_');

            return fileName;
        }

        private static void ClearCaptureQueue()
        {
            EditorApplication.update -= TryCaptureScreenshot;

            _captureQueued = false;
            _gameView = null;
            _targetCamera = null;
            _warmupFramesRemaining = 0;
            _captureAttemptsRemaining = 0;
        }
    }
}