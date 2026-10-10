#if USE_TIMELINE
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Timeline.Editor
{
    [CustomEditor(typeof(TextEffectClip)), CanEditMultipleObjects]
    internal sealed class TextStudioEffectClipInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            SerializedProperty effectProperty = serializedObject.FindProperty("effect");
            root.Add(new PropertyField(effectProperty));
            root.Add(new PropertyField(serializedObject.FindProperty("intensityCurve")));
            root.Add(new PropertyField(serializedObject.FindProperty("range")));
            root.Add(new HelpBox("Timeline owns clip timing, blending and the exclusive text-unit range. A Composed Effect owns the layered animation. Bind the Text Effects track to a Text Studio text component.", HelpBoxMessageType.Info));
            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            Button open = new Button(OpenComposition) { text = "Open Composition", tooltip = "Edit the shared Composed Effect used by this clip." };
            Button variant = new Button(CreateVariant) { text = "Create Variant…", tooltip = "Save an independent Composed Effect and assign it to this clip, preserving timing, blending, intensity and range." };
            actions.Add(open);
            actions.Add(variant);
            root.Add(actions);
            System.Action refresh = () => actions.SetEnabled(!serializedObject.isEditingMultipleObjects && effectProperty.objectReferenceValue is TextStudioComposedEffectAsset);
            root.TrackPropertyValue(effectProperty, _ => refresh());
            refresh();
            return root;
        }

        void OpenComposition()
        {
            TextEffectClip clip = target as TextEffectClip;
            if (clip?.effect is not TextStudioComposedEffectAsset composition) return;
            Selection.activeObject = composition;
            EditorGUIUtility.PingObject(composition);
        }

        void CreateVariant()
        {
            TextEffectClip clip = target as TextEffectClip;
            if (clip?.effect is not TextStudioComposedEffectAsset composition) return;
            string path = EditorUtility.SaveFilePanelInProject("Create Composed Effect Variant", composition.name + " Variant", "asset", "Choose where to save the editable variant.");
            if (string.IsNullOrEmpty(path)) return;
            TextStudioComposedEffectAsset variant = TextStudioTimelineAuthoring.CreateVariant(clip, path);
            serializedObject.Update();
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
            EditorGUIUtility.PingObject(variant);
        }
    }
}
#endif
