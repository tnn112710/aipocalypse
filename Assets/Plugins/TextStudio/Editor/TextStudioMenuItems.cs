using ImpossibleRobert.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TextStudio.Editor
{
    /// <summary>
    /// GameObject menu items for instant drop-in of Text Studio components.
    /// All items produce a scene object that renders immediately with sensible defaults.
    /// </summary>
    internal static class TextStudioMenuItems
    {
        const string MenuRoot = "GameObject/Text Studio/";
        const string AnimatedTextMenuRoot = MenuRoot + "Animated Text/";
        const string TemporaryTextMenuRoot = MenuRoot + "Temporary Text/";
        const string DemoText = "Hello <shake>World</shake>!";

        [MenuItem(AnimatedTextMenuRoot + "World Space", false, 10)]
        public static void CreateAnimatedTMP(MenuCommand cmd)
        {
            var go = new GameObject("Animated TMP Text");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = DemoText;
            tmp.fontSize = 10;
            tmp.alignment = TextAlignmentOptions.Center;

            var hub = go.AddComponent<TextStudioText>();
            hub.SetText(DemoText);
            hub.SetAuthoringMode(TextStudioAuthoringMode.Preview);

            Finish(go);
        }

        [MenuItem(AnimatedTextMenuRoot + "UI Canvas", false, 11)]
        public static void CreateAnimatedTMPUGUI(MenuCommand cmd)
        {
            var parent = (cmd.context as GameObject) ?? FindOrCreateCanvas();
            var go = new GameObject("Animated TMP Text (UI)",
                typeof(RectTransform));
            GameObjectUtility.SetParentAndAlign(go, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 80);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = DemoText;
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;

            var hub = go.AddComponent<TextStudioText>();
            hub.SetText(DemoText);
            hub.SetAuthoringMode(TextStudioAuthoringMode.Preview);

            Finish(go);
        }

        [MenuItem(TemporaryTextMenuRoot + "Emitter", false, 20)]
        public static void CreateTextEmitter(MenuCommand cmd)
        {
            GameObject go = new GameObject("Text Emitter");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            go.AddComponent<TextStudioEmitter>();
            Finish(go);
        }

        [MenuItem(TextStudioMenuPaths.HealthCheck, false, TextStudioMenuPaths.HealthCheckPriority)]
        public static void OpenHealthCheck()
        {
            TextStudioHealthWindow.ShowWindow();
        }

        [MenuItem(TextStudioMenuPaths.Integrations, false, TextStudioMenuPaths.IntegrationsPriority)]
        public static void OpenIntegrations()
        {
            TextStudioIntegrationsWindow.ShowWindow();
        }

        static GameObject FindOrCreateCanvas()
        {
            Canvas existing = UnityUtils.FindAny<Canvas>();
            if (existing != null) return existing.gameObject;

            GameObject go = new GameObject("Canvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return go;
        }

        internal static void Finish(GameObject go)
        {
            Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            Selection.activeGameObject = go;
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
