//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(ThunderFX))]
    [CanEditMultipleObjects]
    public class ThunderFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField timeBetweenStrikes = new PropertyField();
            timeBetweenStrikes.BindProperty(serializedObject.FindProperty("timeBetweenStrikes"));
            container.Add(timeBetweenStrikes);

            PropertyField thunderPrefab = new PropertyField();
            thunderPrefab.BindProperty(serializedObject.FindProperty("thunderPrefab"));
            container.Add(thunderPrefab);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);

            PropertyField thunderSounds = new PropertyField();
            thunderSounds.BindProperty(serializedObject.FindProperty("thunderSounds"));
            container.Add(thunderSounds);

            PropertyField sunLightDimming = new PropertyField();
            sunLightDimming.BindProperty(serializedObject.FindProperty("sunLightDimming"));
            container.Add(sunLightDimming);

            PropertyField lightningLightIntensityCurve = new PropertyField();
            lightningLightIntensityCurve.BindProperty(serializedObject.FindProperty("lightningLightIntensityCurve"));
            container.Add(lightningLightIntensityCurve);

            PropertyField lightningLightIntensity = new PropertyField();
            lightningLightIntensity.BindProperty(serializedObject.FindProperty("lightningLightIntensity"));
            container.Add(lightningLightIntensity);

            PropertyField thunderAudioDelay = new PropertyField();
            thunderAudioDelay.BindProperty(serializedObject.FindProperty("thunderAudioDelay"));
            container.Add(thunderAudioDelay);
            
            return container;
        }
    }
}