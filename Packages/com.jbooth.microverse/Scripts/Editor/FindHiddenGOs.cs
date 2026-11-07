using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JBooth.MicroVerseCore
{
    public static class HiddenGOFinder
    {
        [MenuItem("Window/MicroVerse/Reveal Hidden GameObjects")]
        private static void RevealHiddenGameObjects()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                RevealHiddenGameObject(gameObject);
            }
        }

        private static void RevealHiddenGameObject(GameObject gameObject)
        {
            if (gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy))
            {
                Debug.Log("Revealing hidden GameObject " + gameObject.name, gameObject);
                gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
            }

            foreach (Transform child in gameObject.transform)
            {
                RevealHiddenGameObject(child.gameObject);
            }
        }
    }
}