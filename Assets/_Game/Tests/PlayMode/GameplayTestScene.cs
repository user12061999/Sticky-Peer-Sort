using UnityEngine;
using UnityEngine.SceneManagement;

namespace StickyPeerSort.Tests
{
    // Editor tests must not depend on a platform Build Profile shipping the review scene.
    static class GameplayTestScene
    {
        public static AsyncOperation LoadReference()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Game/Scenes/SampleStickerLevel.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync("SampleStickerLevel");
#endif
        }
    }
}
