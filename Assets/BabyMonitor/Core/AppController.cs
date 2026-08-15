using UnityEngine;

namespace BabyMonitor.Core
{
    public class AppController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject modeSelectionPanel;
        [SerializeField] private GameObject monitorPanel;
        [SerializeField] private GameObject viewerPanel;

        public AppMode CurrentMode { get; private set; } = AppMode.None;

        private void Start()
        {
            ShowModeSelection();
        }

        public void SelectMonitor()
        {
            CurrentMode = AppMode.Monitor;

            modeSelectionPanel.SetActive(false);
            monitorPanel.SetActive(true);
            viewerPanel.SetActive(false);
        }

        public void SelectViewer()
        {
            CurrentMode = AppMode.Viewer;

            modeSelectionPanel.SetActive(false);
            monitorPanel.SetActive(false);
            viewerPanel.SetActive(true);
        }

        public void BackToModeSelection()
        {
            ShowModeSelection();
        }

        private void ShowModeSelection()
        {
            CurrentMode = AppMode.None;

            modeSelectionPanel.SetActive(true);
            monitorPanel.SetActive(false);
            viewerPanel.SetActive(false);
        }
    }
}