#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aipocalypse.Core
{
    // Keeps MicroVerse disabled (baked mode) unless a map rebuild is running. Enabled, it regenerates the terrain on every
    // scene open and domain reload and writes the GPU readback into the LFS TerrainData on each save; the readback isn't
    // bit-exact, so the 59 MB asset changed with nothing edited. MicroVerseKit.Enable(true) sets RebuildKey for the session.
    static class MicroVerseBakedGuard
    {
        public const string RebuildKey = "MicroVerseKit.Enabled";

        static Type _microVerseType;

        [InitializeOnLoadMethod]
        static void Init()
        {
            _microVerseType = Type.GetType("JBooth.MicroVerseCore.MicroVerse, JBooth.MicroVerseCore");
            if (_microVerseType == null)
                return;

            EditorSceneManager.sceneOpened += (_, _) => DisableAll();
            AssemblyReloadEvents.afterAssemblyReload += DisableAll;
            DisableAll();
        }

        static void DisableAll()
        {
            if (SessionState.GetBool(RebuildKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Not marked dirty: the scene stays clean, and any later save stores the disabled state.
            foreach (var obj in UnityEngine.Object.FindObjectsByType(_microVerseType, FindObjectsInactive.Include))
            {
                if (obj is Behaviour { enabled: true } mv)
                {
                    mv.enabled = false;
                    Debug.Log($"MicroVerse in {mv.gameObject.scene.name} disabled (baked mode). Run MicroVerseKit.Enable(true) to rebuild the terrain.");
                }
            }
        }
    }
}
#endif
