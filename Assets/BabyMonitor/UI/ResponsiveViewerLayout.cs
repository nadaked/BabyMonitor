using TMPro;
using UnityEngine;
using System.Collections.Generic;

namespace BabyMonitor.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResponsiveViewerLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform backButton;
        [SerializeField] private TMP_Text topBarTitle;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private RectTransform scannerFrame;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text footer;

        private RectTransform panel;
        private Vector2 lastSize;
        private bool lastLandscape;
        private readonly List<RectState> portraitStates = new List<RectState>();
        private TextAlignmentOptions[] portraitAlignments;

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            CapturePortrait();
        }
        private void OnEnable() => Refresh(true);
        private void OnRectTransformDimensionsChange() => Refresh(false);
        private void LateUpdate() => Refresh(false);

        private void Refresh(bool force)
        {
            if (panel == null) panel = GetComponent<RectTransform>();
            Vector2 size = panel.rect.size;
            bool landscape = size.x > size.y;
            if (!force && size == lastSize && landscape == lastLandscape) return;
            lastSize = size; lastLandscape = landscape;
            if (landscape) ApplyLandscape(size);
            else RestorePortrait();
        }

        private void CapturePortrait()
        {
            RectTransform[] transforms = { backButton, topBarTitle.rectTransform, title.rectTransform,
                description.rectTransform, scannerFrame, statusText.rectTransform, footer.rectTransform };
            foreach (RectTransform transform in transforms)
                if (transform != null) portraitStates.Add(new RectState(transform));
            portraitAlignments = new[] { topBarTitle.alignment, title.alignment, description.alignment,
                statusText.alignment, footer.alignment };
        }

        private void RestorePortrait()
        {
            foreach (RectState state in portraitStates) state.Restore();
            if (portraitAlignments == null) return;
            topBarTitle.alignment = portraitAlignments[0]; title.alignment = portraitAlignments[1];
            description.alignment = portraitAlignments[2]; statusText.alignment = portraitAlignments[3];
            footer.alignment = portraitAlignments[4];
        }

        private void ApplyLandscape(Vector2 size)
        {
            const float margin = 42f;
            float leftWidth = size.x * .50f;
            float contentWidth = Mathf.Max(1f, leftWidth - 2f * margin);
            float square = Mathf.Max(1f, Mathf.Min(size.y - 2f * margin, size.x - leftWidth - 2f * margin));
            Set(backButton, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(margin, -margin), new Vector2(76, 76));
            Set(topBarTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(136, -50), new Vector2(Mathf.Max(1f, leftWidth - 160f), 56));
            Set(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(margin, -180), new Vector2(contentWidth, 64));
            Set(description.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(margin, -265), new Vector2(contentWidth, 100));
            Set(statusText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(margin, 118), new Vector2(contentWidth, 65));
            Set(footer.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(margin, 38), new Vector2(contentWidth, 58));
            Set(scannerFrame, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-margin, 0), new Vector2(square, square));
            topBarTitle.alignment = TextAlignmentOptions.Left;
            title.alignment = TextAlignmentOptions.Left;
            description.alignment = TextAlignmentOptions.Left;
            statusText.alignment = TextAlignmentOptions.Left;
            footer.alignment = TextAlignmentOptions.Left;
        }

        private static void Set(RectTransform target, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (target == null) return;
            target.anchorMin = min; target.anchorMax = max; target.pivot = pivot;
            target.anchoredPosition = position; target.sizeDelta = size;
        }

        private sealed class RectState
        {
            private readonly RectTransform target; private readonly Vector2 min; private readonly Vector2 max;
            private readonly Vector2 pivot; private readonly Vector2 position; private readonly Vector2 size;
            public RectState(RectTransform value) { target = value; min = value.anchorMin; max = value.anchorMax; pivot = value.pivot; position = value.anchoredPosition; size = value.sizeDelta; }
            public void Restore() { if (target != null) Set(target, min, max, pivot, position, size); }
        }
    }
}
