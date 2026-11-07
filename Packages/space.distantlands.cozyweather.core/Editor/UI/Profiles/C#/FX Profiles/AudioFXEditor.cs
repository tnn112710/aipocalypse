//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(AudioFX))]
    [CanEditMultipleObjects]
    public class AudioFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField clip = new PropertyField();
            clip.BindProperty(serializedObject.FindProperty("clip"));
            container.Add(clip);

            PropertyField audioMixer = new PropertyField();
            audioMixer.BindProperty(serializedObject.FindProperty("audioMixer"));
            container.Add(audioMixer);

            PropertyField maximumVolume = new PropertyField();
            maximumVolume.BindProperty(serializedObject.FindProperty("maximumVolume"));
            container.Add(maximumVolume);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            

            return container;
        }
    }
}