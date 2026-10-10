using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Editor
{
    internal static class TextStudioLongTextPreset
    {
        public static void ApplyToSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Text Studio", "Select a Text Studio object first.", "OK");
                return;
            }

            TextStudioText hub = selected.GetComponent<TextStudioText>();
            TMP_Text tmp = selected.GetComponent<TMP_Text>();
            if (hub == null || tmp == null)
            {
                EditorUtility.DisplayDialog("Text Studio", "The selected object needs TextStudioText and TMP_Text.", "OK");
                return;
            }

            Undo.RecordObject(tmp, "Apply Text Studio Long Text Preset");
            tmp.overflowMode = TextOverflowModes.Page;
            EditorUtility.SetDirty(tmp);

            TextStudioPageNavigator navigator = selected.GetComponent<TextStudioPageNavigator>();
            if (navigator == null)
                navigator = Undo.AddComponent<TextStudioPageNavigator>(selected);
            else
                Undo.RecordObject(navigator, "Apply Text Studio Long Text Preset");

            navigator.SetTargets(hub, tmp);
            navigator.FollowReveal = true;
            EditorUtility.SetDirty(navigator);

            ScrollRect scrollRect = selected.GetComponentInParent<ScrollRect>();
            if (scrollRect == null)
                return;

            TextStudioAutoScrollFollower follower = selected.GetComponent<TextStudioAutoScrollFollower>();
            if (follower == null)
                follower = Undo.AddComponent<TextStudioAutoScrollFollower>(selected);
            else
                Undo.RecordObject(follower, "Apply Text Studio Long Text Preset");

            follower.SetTargets(hub, tmp, scrollRect);
            EditorUtility.SetDirty(follower);
        }
    }
}
