#if UNITY_EDITOR

using System;
using BabyMonitor.Core;
using BabyMonitor.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BabyMonitor.Editor
{
    public static class BabyMonitorSceneBuilder
    {
        private const string RootName = "BabyMonitor_UI_ROOT";

        private static readonly Color Background =
            new Color32(14, 17, 22, 255);

        private static readonly Color Surface =
            new Color32(24, 29, 37, 255);

        private static readonly Color SurfaceLight =
            new Color32(34, 41, 52, 255);

        private static readonly Color Primary =
            new Color32(105, 214, 203, 255);

        private static readonly Color PrimaryDark =
            new Color32(18, 54, 53, 255);

        private static readonly Color TextPrimary =
            new Color32(245, 248, 250, 255);

        private static readonly Color TextSecondary =
            new Color32(159, 171, 187, 255);

        private static readonly Color Border =
            new Color32(53, 62, 75, 255);

        // --------------------------------------------------------------------
        // MENU
        // --------------------------------------------------------------------

        [MenuItem("BabyMonitor/Setup Main Scene UI")]
        public static void Build()
        {
            if (!ValidateScene())
                return;

            RemoveOldRoot();

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create BabyMonitor UI");

            // ------------------------------------------------------------
            // APP CONTROLLER
            // ------------------------------------------------------------

            GameObject appObject = new GameObject("App");
            appObject.transform.SetParent(root.transform);

            AppController appController =
                appObject.AddComponent<AppController>();

            // ------------------------------------------------------------
            // CANVAS
            // ------------------------------------------------------------

            Canvas canvas = CreateCanvas(root.transform);

            CreateBackground(canvas.transform);

            GameObject safeArea =
                CreateUIObject("SafeArea", canvas.transform);

            Stretch(safeArea.GetComponent<RectTransform>());
            safeArea.AddComponent<SafeAreaFitter>();

            // ------------------------------------------------------------
            // PANELS
            // ------------------------------------------------------------

            GameObject modeSelectionPanel =
                CreatePanel("ModeSelectionPanel", safeArea.transform);

            GameObject monitorPanel =
                CreatePanel("MonitorPanel", safeArea.transform);

            GameObject viewerPanel =
                CreatePanel("ViewerPanel", safeArea.transform);

            BuildModeSelection(
                modeSelectionPanel.transform,
                appController
            );

            TMP_Text monitorStatus = BuildMonitorPanel(
                monitorPanel.transform,
                appController
            );

            BuildViewerPanel(
                viewerPanel.transform,
                appController
            );

            ConfigureResponsiveLayouts(monitorPanel, viewerPanel);

            // ------------------------------------------------------------
            // APP CONTROLLER REFERENCES
            // ------------------------------------------------------------

            SerializedObject appSerialized =
                new SerializedObject(appController);

            appSerialized
                .FindProperty("modeSelectionPanel")
                .objectReferenceValue = modeSelectionPanel;

            appSerialized
                .FindProperty("monitorPanel")
                .objectReferenceValue = monitorPanel;

            appSerialized
                .FindProperty("viewerPanel")
                .objectReferenceValue = viewerPanel;

            appSerialized.ApplyModifiedProperties();

            // ------------------------------------------------------------
            // MONITOR CONTROLLER
            // ------------------------------------------------------------

            MonitorController monitorController =
                monitorPanel.AddComponent<MonitorController>();

            SerializedObject monitorSerialized =
                new SerializedObject(monitorController);

            monitorSerialized
                    .FindProperty("statusText")
                    .objectReferenceValue =
                monitorPanel.transform
                    .Find("StatusText")
                    .GetComponent<TMP_Text>();

            monitorSerialized
                    .FindProperty("qrImage")
                    .objectReferenceValue =
                monitorPanel.transform
                    .Find("QRCodeCard/QRCode")
                    .GetComponent<RawImage>();

            Transform qrPlaceholder =
                monitorPanel.transform
                    .Find("QRCodeCard/QRCode/Placeholder");

            if (qrPlaceholder != null)
            {
                monitorSerialized
                        .FindProperty("qrPlaceholder")
                        .objectReferenceValue =
                    qrPlaceholder.gameObject;
            }

            monitorSerialized.ApplyModifiedProperties();

            // ------------------------------------------------------------
// VIEWER CONTROLLER
// ------------------------------------------------------------

            ViewerController viewerController =
                viewerPanel.AddComponent<ViewerController>();

            SerializedObject viewerSerialized =
                new SerializedObject(viewerController);

            viewerSerialized
                    .FindProperty("scannerPreview")
                    .objectReferenceValue =
                viewerPanel.transform
                    .Find("ScannerFrame/ScannerPreview")
                    .GetComponent<RawImage>();

            viewerSerialized
                    .FindProperty("statusText")
                    .objectReferenceValue =
                viewerPanel.transform
                    .Find("StatusText")
                    .GetComponent<TMP_Text>();

            Transform cameraPlaceholder =
                viewerPanel.transform
                    .Find("ScannerFrame/ScannerPreview/CameraPlaceholder");

            if (cameraPlaceholder != null)
            {
                viewerSerialized
                        .FindProperty("cameraPlaceholder")
                        .objectReferenceValue =
                    cameraPlaceholder.gameObject;
            }

            viewerSerialized.ApplyModifiedProperties();

            // ------------------------------------------------------------
            // INITIAL STATE
            // ------------------------------------------------------------

            modeSelectionPanel.SetActive(true);
            monitorPanel.SetActive(false);
            viewerPanel.SetActive(false);

            EnsureEventSystem(root.transform);

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene()
            );

            Selection.activeGameObject = root;

            Debug.Log(
                "<b>BabyMonitor:</b> Main Scene UI oluşturuldu."
            );
        }

        // ====================================================================
        // MODE SELECTION
        // ====================================================================

        private static void BuildModeSelection(
            Transform parent,
            AppController appController)
        {
            // Brand
            TMP_Text brand = CreateText(
                "Brand",
                parent,
                "BabyMonitor",
                34,
                TextSecondary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            SetRect(
                brand.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0, -90),
                new Vector2(800, 60)
            );

            // Title
            TMP_Text title = CreateText(
                "Title",
                parent,
                "Bu cihazı nasıl\nkullanacaksın?",
                64,
                TextPrimary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            title.lineSpacing = -5;

            SetRect(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0, -190),
                new Vector2(900, 180)
            );

            // Subtitle
            TMP_Text subtitle = CreateText(
                "Subtitle",
                parent,
                "İki cihaz da aynı Wi-Fi ağına bağlı olmalı.",
                28,
                TextSecondary,
                FontStyles.Normal,
                TextAlignmentOptions.Center
            );

            SetRect(
                subtitle.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0, -390),
                new Vector2(900, 70)
            );

            // Monitor
            Button monitorButton = CreateChoiceButton(
                parent,
                "MonitorButton",
                "MONİTÖR",
                "Bu cihaz bebeğin yanında kalır.\nKamera ve sesi yayınlar.",
                "M",
                Primary,
                PrimaryDark,
                -560
            );

            UnityEventTools.AddPersistentListener(
                monitorButton.onClick,
                appController.SelectMonitor
            );

            // Viewer
            Button viewerButton = CreateChoiceButton(
                parent,
                "ViewerButton",
                "İZLEYİCİ",
                "Monitör cihazındaki QR kodunu okut.\nCanlı görüntüyü buradan izle.",
                "V",
                SurfaceLight,
                TextPrimary,
                -800
            );

            UnityEventTools.AddPersistentListener(
                viewerButton.onClick,
                appController.SelectViewer
            );

            // Privacy Card
            GameObject privacyCard =
                CreateCard("PrivacyCard", parent, Surface);

            SetRect(
                privacyCard.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0, 100),
                new Vector2(-144, 180)
            );

            TMP_Text privacyTitle = CreateText(
                "PrivacyTitle",
                privacyCard.transform,
                "Yerel bağlantı",
                28,
                TextPrimary,
                FontStyles.Bold,
                TextAlignmentOptions.Left
            );

            SetRect(
                privacyTitle.rectTransform,
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0.5f, 1),
                new Vector2(32, -28),
                new Vector2(-64, 45)
            );

            TMP_Text privacyDescription = CreateText(
                "PrivacyDescription",
                privacyCard.transform,
                "Hesap veya bulut bağlantısı gerekmez.",
                24,
                TextSecondary,
                FontStyles.Normal,
                TextAlignmentOptions.Left
            );

            SetRect(
                privacyDescription.rectTransform,
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0.5f, 1),
                new Vector2(32, -82),
                new Vector2(-64, 55)
            );
        }

        // ====================================================================
        // MONITOR
        // ====================================================================

        private static TMP_Text BuildMonitorPanel(
            Transform parent,
            AppController appController)
        {
            CreateTopBar(
                parent,
                "Monitör",
                appController
            );

            TMP_Text eyebrow = CreateText(
                "Eyebrow",
                parent,
                "EŞLEŞTİRME",
                24,
                Primary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            SetRect(
                eyebrow.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -250),
                new Vector2(700, 50)
            );

            TMP_Text title = CreateText(
                "Title",
                parent,
                "İzleyici cihazı bağla",
                48,
                TextPrimary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            SetRect(
                title.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -315),
                new Vector2(900, 80)
            );

            TMP_Text description = CreateText(
                "Description",
                parent,
                "Diğer cihazda İzleyici seçeneğini aç ve\nbu QR kodunu okut.",
                28,
                TextSecondary,
                FontStyles.Normal,
                TextAlignmentOptions.Center
            );

            SetRect(
                description.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -405),
                new Vector2(900, 100)
            );

            // QR container
            GameObject qrCard =
                CreateCard("QRCodeCard", parent, TextPrimary);

            SetRect(
                qrCard.GetComponent<RectTransform>(),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -560),
                new Vector2(640, 640)
            );

            RawImage qrImage =
                CreateRawImage("QRCode", qrCard.transform);

            qrImage.color = Color.white;

            RectTransform qrRect = qrImage.rectTransform;

            qrRect.anchorMin = Vector2.zero;
            qrRect.anchorMax = Vector2.one;
            qrRect.offsetMin = new Vector2(35, 35);
            qrRect.offsetMax = new Vector2(-35, -35);

            TMP_Text placeholder = CreateText(
                "Placeholder",
                qrImage.transform,
                "QR",
                72,
                PrimaryDark,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            Stretch(placeholder.rectTransform);

            // Status
            TMP_Text statusText = CreateText(
                "StatusText",
                parent,
                "Eşleşme hazırlanıyor...",
                30,
                TextPrimary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            SetRect(
                statusText.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -1250),
                new Vector2(900, 100)
            );

            // Indicator
            GameObject statusDot =
                CreateImageObject(
                    "StatusDot",
                    parent,
                    Primary
                );

            SetRect(
                statusDot.GetComponent<RectTransform>(),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(-205, -1300),
                new Vector2(16, 16)
            );

            TMP_Text hint = CreateText(
                "Hint",
                parent,
                "QR kodu yalnızca bu eşleşme oturumu için kullanılacak.",
                24,
                TextSecondary,
                FontStyles.Normal,
                TextAlignmentOptions.Center
            );

            SetRect(
                hint.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -1380),
                new Vector2(850, 70)
            );

            return statusText;
        }

        // ====================================================================
        // VIEWER
        // ====================================================================

        private static void BuildViewerPanel(
            Transform parent,
            AppController appController)
        {
            CreateTopBar(
                parent,
                "İzleyici",
                appController
            );

            TMP_Text title = CreateText(
                "Title",
                parent,
                "QR kodunu okut",
                48,
                TextPrimary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            SetRect(
                title.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -250),
                new Vector2(900, 75)
            );

            TMP_Text description = CreateText(
                "Description",
                parent,
                "Monitör cihazında gösterilen QR kodunu\nçerçevenin içine getir.",
                28,
                TextSecondary,
                FontStyles.Normal,
                TextAlignmentOptions.Center
            );

            SetRect(
                description.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -340),
                new Vector2(900, 100)
            );

            // Scanner
            GameObject scannerFrame =
                CreateCard(
                    "ScannerFrame",
                    parent,
                    Surface
                );

            SetRect(
                scannerFrame.GetComponent<RectTransform>(),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -500),
                new Vector2(820, 820)
            );

            RectMask2D mask =
                scannerFrame.AddComponent<RectMask2D>();

            RawImage preview =
                CreateRawImage(
                    "ScannerPreview",
                    scannerFrame.transform
                );

            preview.color = Color.white;

            RectTransform previewRect = preview.rectTransform;

            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = new Vector2(10, 10);
            previewRect.offsetMax = new Vector2(-10, -10);

            TMP_Text cameraPlaceholder = CreateText(
                "CameraPlaceholder",
                preview.transform,
                "KAMERA",
                32,
                TextSecondary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            Stretch(cameraPlaceholder.rectTransform);

            CreateScannerCorners(scannerFrame.transform);

            TMP_Text scannerStatus = CreateText(
                "StatusText",
                parent,
                "Kamera hazırlanıyor...",
                28,
                TextPrimary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            SetRect(
                scannerStatus.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -1390),
                new Vector2(900, 80)
            );

            TMP_Text footer = CreateText(
                "Footer",
                parent,
                "Her iki cihazın aynı Wi-Fi ağına bağlı olduğundan emin ol.",
                24,
                TextSecondary,
                FontStyles.Normal,
                TextAlignmentOptions.Center
            );

            SetRect(
                footer.rectTransform,
                new Vector2(.5f, 0),
                new Vector2(.5f, 0),
                new Vector2(.5f, 0),
                new Vector2(0, 90),
                new Vector2(900, 80)
            );
        }

        // ====================================================================
        // TOP BAR
        // ====================================================================

        private static void CreateTopBar(
            Transform parent,
            string title,
            AppController appController)
        {
            Button backButton = CreateButton(
                "BackButton",
                parent,
                SurfaceLight
            );

            SetRect(
                backButton.GetComponent<RectTransform>(),
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(48, -48),
                new Vector2(96, 96)
            );

            TMP_Text backText = CreateText(
                "Text",
                backButton.transform,
                "‹",
                58,
                TextPrimary,
                FontStyles.Normal,
                TextAlignmentOptions.Center
            );

            Stretch(backText.rectTransform);

            backText.rectTransform.anchoredPosition =
                new Vector2(0, 5);

            UnityEventTools.AddPersistentListener(
                backButton.onClick,
                appController.BackToModeSelection
            );

            TMP_Text topTitle = CreateText(
                "TopBarTitle",
                parent,
                title,
                34,
                TextPrimary,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            SetRect(
                topTitle.rectTransform,
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(.5f, 1),
                new Vector2(0, -62),
                new Vector2(600, 70)
            );
        }

        // ====================================================================
        // RESPONSIVE LAYOUT
        // ====================================================================

        private static void ConfigureResponsiveLayouts(
            GameObject monitorPanel,
            GameObject viewerPanel)
        {
            ResponsiveMonitorLayout monitorLayout =
                monitorPanel.AddComponent<ResponsiveMonitorLayout>();

            SerializedObject monitorSerialized =
                new SerializedObject(monitorLayout);

            SetReference(monitorSerialized, "backButton", monitorPanel.transform.Find("BackButton"));
            SetReference(monitorSerialized, "topBarTitle", monitorPanel.transform.Find("TopBarTitle"));
            SetReference(monitorSerialized, "eyebrow", monitorPanel.transform.Find("Eyebrow"));
            SetReference(monitorSerialized, "title", monitorPanel.transform.Find("Title"));
            SetReference(monitorSerialized, "description", monitorPanel.transform.Find("Description"));
            SetReference(monitorSerialized, "qrCodeCard", monitorPanel.transform.Find("QRCodeCard"));
            SetReference(monitorSerialized, "statusText", monitorPanel.transform.Find("StatusText"));
            SetReference(monitorSerialized, "statusDot", monitorPanel.transform.Find("StatusDot"));
            SetReference(monitorSerialized, "hint", monitorPanel.transform.Find("Hint"));
            monitorSerialized.ApplyModifiedProperties();

            ResponsiveViewerLayout viewerLayout =
                viewerPanel.AddComponent<ResponsiveViewerLayout>();

            SerializedObject viewerSerialized =
                new SerializedObject(viewerLayout);

            SetReference(viewerSerialized, "backButton", viewerPanel.transform.Find("BackButton"));
            SetReference(viewerSerialized, "topBarTitle", viewerPanel.transform.Find("TopBarTitle"));
            SetReference(viewerSerialized, "title", viewerPanel.transform.Find("Title"));
            SetReference(viewerSerialized, "description", viewerPanel.transform.Find("Description"));
            SetReference(viewerSerialized, "scannerFrame", viewerPanel.transform.Find("ScannerFrame"));
            SetReference(viewerSerialized, "statusText", viewerPanel.transform.Find("StatusText"));
            SetReference(viewerSerialized, "footer", viewerPanel.transform.Find("Footer"));
            viewerSerialized.ApplyModifiedProperties();
        }

        private static void SetReference(
            SerializedObject serializedObject,
            string propertyName,
            Transform transform)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);

            if (property == null || transform == null)
            {
                Debug.LogError(
                    $"BabyMonitor responsive reference missing: {propertyName}"
                );
                return;
            }

            if (property.type == "PPtr<$RectTransform>")
            {
                property.objectReferenceValue =
                    transform.GetComponent<RectTransform>();
            }
            else
            {
                property.objectReferenceValue =
                    transform.GetComponent<TMP_Text>();
            }
        }

        // ====================================================================
        // CHOICE BUTTON
        // ====================================================================

        private static Button CreateChoiceButton(
            Transform parent,
            string name,
            string title,
            string description,
            string iconText,
            Color background,
            Color foreground,
            float y)
        {
            Button button =
                CreateButton(name, parent, background);

            RectTransform rt =
                button.GetComponent<RectTransform>();

            SetRect(
                rt,
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(.5f, 1),
                new Vector2(0, y),
                new Vector2(-144, 200)
            );

            // Icon
            GameObject icon =
                CreateImageObject(
                    "Icon",
                    button.transform,
                    foreground
                );

            SetRect(
                icon.GetComponent<RectTransform>(),
                new Vector2(0, .5f),
                new Vector2(0, .5f),
                new Vector2(0, .5f),
                new Vector2(36, 0),
                new Vector2(94, 94)
            );

            TMP_Text iconLabel = CreateText(
                "IconText",
                icon.transform,
                iconText,
                34,
                background,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

            Stretch(iconLabel.rectTransform);

            // Title
            TMP_Text titleText = CreateText(
                "Title",
                button.transform,
                title,
                32,
                foreground,
                FontStyles.Bold,
                TextAlignmentOptions.Left
            );

            SetRect(
                titleText.rectTransform,
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(.5f, 1),
                new Vector2(165, -42),
                new Vector2(-215, 45)
            );

            // Description
            Color descriptionColor = foreground;
            descriptionColor.a = 0.72f;

            TMP_Text descriptionText = CreateText(
                "Description",
                button.transform,
                description,
                23,
                descriptionColor,
                FontStyles.Normal,
                TextAlignmentOptions.Left
            );

            descriptionText.lineSpacing = 2;

            SetRect(
                descriptionText.rectTransform,
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(.5f, 1),
                new Vector2(165, -92),
                new Vector2(-215, 85)
            );

            // Arrow
            TMP_Text arrow = CreateText(
                "Arrow",
                button.transform,
                "›",
                48,
                foreground,
                FontStyles.Normal,
                TextAlignmentOptions.Center
            );

            SetRect(
                arrow.rectTransform,
                new Vector2(1, .5f),
                new Vector2(1, .5f),
                new Vector2(1, .5f),
                new Vector2(-28, 0),
                new Vector2(60, 80)
            );

            return button;
        }

        // ====================================================================
        // SCANNER CORNERS
        // ====================================================================

        private static void CreateScannerCorners(Transform parent)
        {
            float inset = 35f;
            float length = 100f;
            float thickness = 10f;

            // Top Left
            CreateCornerLine(
                parent,
                "TL_H",
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(inset, -inset),
                new Vector2(length, thickness)
            );

            CreateCornerLine(
                parent,
                "TL_V",
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(inset, -inset),
                new Vector2(thickness, length)
            );

            // Top Right
            CreateCornerLine(
                parent,
                "TR_H",
                new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(-inset, -inset),
                new Vector2(length, thickness)
            );

            CreateCornerLine(
                parent,
                "TR_V",
                new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(-inset, -inset),
                new Vector2(thickness, length)
            );

            // Bottom Left
            CreateCornerLine(
                parent,
                "BL_H",
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(inset, inset),
                new Vector2(length, thickness)
            );

            CreateCornerLine(
                parent,
                "BL_V",
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(inset, inset),
                new Vector2(thickness, length)
            );

            // Bottom Right
            CreateCornerLine(
                parent,
                "BR_H",
                new Vector2(1, 0),
                new Vector2(1, 0),
                new Vector2(-inset, inset),
                new Vector2(length, thickness)
            );

            CreateCornerLine(
                parent,
                "BR_V",
                new Vector2(1, 0),
                new Vector2(1, 0),
                new Vector2(-inset, inset),
                new Vector2(thickness, length)
            );
        }

        private static void CreateCornerLine(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            GameObject line =
                CreateImageObject(name, parent, Primary);

            SetRect(
                line.GetComponent<RectTransform>(),
                anchor,
                anchor,
                pivot,
                position,
                size
            );
        }

        // ====================================================================
        // CANVAS
        // ====================================================================

        private static Canvas CreateCanvas(Transform parent)
        {
            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            canvasObject.transform.SetParent(parent);

            Canvas canvas = canvasObject.GetComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                new Vector2(1080, 1920);

            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void CreateBackground(Transform parent)
        {
            GameObject background =
                CreateImageObject(
                    "Background",
                    parent,
                    Background
                );

            Stretch(
                background.GetComponent<RectTransform>()
            );
        }

        // ====================================================================
        // EVENT SYSTEM
        // ====================================================================

        private static void EnsureEventSystem(Transform parent)
        {
            EventSystem existing =
                UnityEngine.Object.FindFirstObjectByType<EventSystem>();

            if (existing != null)
                return;

            GameObject eventSystemObject =
                new GameObject(
                    "EventSystem",
                    typeof(EventSystem)
                );

            eventSystemObject.transform.SetParent(parent);

            // New Input System varsa onu kullan.
            Type inputSystemType = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"
            );

            if (inputSystemType != null)
            {
                eventSystemObject.AddComponent(inputSystemType);
            }
            else
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
        }

        // ====================================================================
        // UI FACTORIES
        // ====================================================================

        private static GameObject CreatePanel(
            string name,
            Transform parent)
        {
            GameObject panel =
                CreateUIObject(name, parent);

            Stretch(panel.GetComponent<RectTransform>());

            return panel;
        }

        private static GameObject CreateCard(
            string name,
            Transform parent,
            Color color)
        {
            GameObject card =
                CreateImageObject(name, parent, color);

            Image image = card.GetComponent<Image>();

            Sprite sprite = GetBuiltinUISprite();

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            Shadow shadow = card.AddComponent<Shadow>();

            shadow.effectColor =
                new Color(0, 0, 0, 0.18f);

            shadow.effectDistance =
                new Vector2(0, -6);

            return card;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Color background)
        {
            GameObject buttonObject =
                CreateUIObject(name, parent);

            Image image =
                buttonObject.AddComponent<Image>();

            Sprite sprite = GetBuiltinUISprite();

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            Button button =
                buttonObject.AddComponent<Button>();

            button.targetGraphic = image;
            button.transition =
                Selectable.Transition.ColorTint;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            ColorBlock colors = button.colors;

            colors.normalColor = background;

            colors.highlightedColor =
                Color.Lerp(background, Color.white, 0.08f);

            colors.pressedColor =
                Color.Lerp(background, Color.black, 0.12f);

            colors.selectedColor =
                colors.highlightedColor;

            colors.disabledColor =
                new Color(
                    background.r,
                    background.g,
                    background.b,
                    .4f
                );

            colors.fadeDuration = .08f;

            button.colors = colors;

            return button;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float fontSize,
            Color color,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            GameObject textObject =
                CreateUIObject(name, parent);

            TextMeshProUGUI text =
                textObject.AddComponent<TextMeshProUGUI>();

            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = fontStyle;
            text.alignment = alignment;

            text.enableWordWrapping = true;
            text.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font =
                    TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static RawImage CreateRawImage(
            string name,
            Transform parent)
        {
            GameObject go =
                CreateUIObject(name, parent);

            return go.AddComponent<RawImage>();
        }

        private static GameObject CreateImageObject(
            string name,
            Transform parent,
            Color color)
        {
            GameObject go =
                CreateUIObject(name, parent);

            Image image = go.AddComponent<Image>();

            image.color = color;

            Sprite sprite = GetBuiltinUISprite();

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            return go;
        }

        private static GameObject CreateUIObject(
            string name,
            Transform parent)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform)
                );

            go.transform.SetParent(parent, false);

            return go;
        }

        // ====================================================================
        // RECT TRANSFORM
        // ====================================================================

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;

            rt.pivot =
                new Vector2(.5f, .5f);

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            rt.localScale = Vector3.one;
        }

        private static void SetRect(
            RectTransform rt,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;

            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;

            rt.localScale = Vector3.one;
        }

        // ====================================================================
        // UTIL
        // ====================================================================

        private static Sprite GetBuiltinUISprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd"
            );
        }

        private static bool ValidateScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "BabyMonitor",
                    "Play Mode'dan çıktıktan sonra tekrar çalıştır.",
                    "Tamam"
                );

                return false;
            }

            return true;
        }

        private static void RemoveOldRoot()
        {
            GameObject old =
                GameObject.Find(RootName);

            if (old == null)
                return;

            bool replace =
                EditorUtility.DisplayDialog(
                    "BabyMonitor UI",
                    "Sahnede daha önce oluşturulmuş BabyMonitor UI bulundu.\n\nYeniden oluşturulsun mu?",
                    "Yeniden Oluştur",
                    "İptal"
                );

            if (!replace)
                throw new OperationCanceledException();

            Undo.DestroyObjectImmediate(old);
        }
    }
}

#endif
