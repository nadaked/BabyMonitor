using UnityEngine;

namespace BabyMonitor.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            if (lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height ||
                lastSafeArea != Screen.safeArea)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);

            if (screenSize.x <= 0f || screenSize.y <= 0f)
                return;

            rectTransform.anchorMin = safeArea.position / screenSize;
            rectTransform.anchorMax = (safeArea.position + safeArea.size) / screenSize;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
