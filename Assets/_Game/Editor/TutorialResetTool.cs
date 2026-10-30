using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    public static class TutorialResetTool
    {
        [MenuItem("Tools/Reset Tutorial Progress")]
        public static void ResetTutorialProgress()
        {
            PlayerPrefs.DeleteKey("StickyPeerSort.ReferenceStyle.Save.v1");
            PlayerPrefs.DeleteKey("StickyPeerSort.Campaign.Save.v1");
            PlayerPrefs.DeleteKey("StickyPeerSort.Save.v1");
            PlayerPrefs.Save();
            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller != null && controller.Save != null)
            {
                controller.Save.ResetAllTutorials();
                controller.Restart();
            }
            Debug.Log("[StickyPeerSort] All tutorial progress has been reset. Tutorials will show when playing tutorial levels.");
        }
    }
}
