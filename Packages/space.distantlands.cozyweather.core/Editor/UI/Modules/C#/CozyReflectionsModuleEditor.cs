//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyReflectionsModule))]
    public class CozyReflectionsModuleEditor : CozyModuleEditor
    {

        CozyReflectionsModule module;


        public VisualElement UpdateContainer => root.Q<VisualElement>("update");
        public VisualElement RenderingContainer => root.Q<VisualElement>("rendering");
        Button widget;
        VisualElement root;

        void OnEnable()
        {
            if (!target)
                return;

            module = (CozyReflectionsModule)target;
        }

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Modules/UXML/reflections-module-editor.uxml"
            );

            asset.CloneTree(root);

            PropertyField framesBetweenRenders = new PropertyField();

            PropertyField updateFrequency = new PropertyField();
            updateFrequency.BindProperty(serializedObject.FindProperty("updateFrequency"));
            updateFrequency.RegisterCallback<ChangeEvent<CozyReflectionsModule.UpdateFrequency>>((ChangeEvent<CozyReflectionsModule.UpdateFrequency> evt) =>
            {
                if (evt.newValue == CozyReflectionsModule.UpdateFrequency.everyFrame)
                    framesBetweenRenders.SetEnabled(true);
                else
                    framesBetweenRenders.SetEnabled(false);
            });
            UpdateContainer.Add(updateFrequency);

            framesBetweenRenders.AddToClassList("indented");
            framesBetweenRenders.SetEnabled(module != null && module.updateFrequency == CozyReflectionsModule.UpdateFrequency.everyFrame);
            framesBetweenRenders.BindProperty(serializedObject.FindProperty("framesBetweenRenders"));
            UpdateContainer.Add(framesBetweenRenders);

            PropertyField refreshOnSceneChange = new PropertyField();
            refreshOnSceneChange.BindProperty(serializedObject.FindProperty("refreshOnSceneChange"));
            UpdateContainer.Add(refreshOnSceneChange);


            // Cubemap generation settings. These drive the render texture COZY builds and manages
            // internally, so resolution/HDR/mip changes are picked up automatically and the texture
            // is rebuilt to match (see CozyReflectionsModule.OnCozyUpdateLoop).
            PropertyField resolution = new PropertyField();
            resolution.BindProperty(serializedObject.FindProperty("resolution"));
            RenderingContainer.Add(resolution);

            PropertyField hdr = new PropertyField();
            hdr.BindProperty(serializedObject.FindProperty("hdr"));
            RenderingContainer.Add(hdr);

            PropertyField useMipMaps = new PropertyField();
            useMipMaps.BindProperty(serializedObject.FindProperty("useMipMaps"));
            useMipMaps.AddToClassList("line-below");
            RenderingContainer.Add(useMipMaps);

            PropertyField layerMask = new PropertyField();
            layerMask.BindProperty(serializedObject.FindProperty("layerMask"));
            RenderingContainer.Add(layerMask);

            PropertyField rendererOverride = new PropertyField();
            rendererOverride.BindProperty(serializedObject.FindProperty("rendererOverride"));
            RenderingContainer.Add(rendererOverride);

            SerializedProperty qualityLevelProp = serializedObject.FindProperty("minimumQualityLevel");
            var qualityLevelNames = QualitySettings.names.ToList();
            int initialIndex = Mathf.Clamp(qualityLevelProp.intValue, 0, Mathf.Max(qualityLevelNames.Count - 1, 0));

            PopupField<string> qLevel = new PopupField<string>(
                "Minimum Quality Level",
                qualityLevelNames,
                initialIndex
            );
            qLevel.RegisterValueChangedCallback((ChangeEvent<string> evt) =>
            {
                qualityLevelProp.intValue = qualityLevelNames.IndexOf(evt.newValue);
                serializedObject.ApplyModifiedProperties();
            });
            qLevel.AddToClassList("unity-base-field__aligned");
            RenderingContainer.Add(qLevel);

            return root;

        }

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/reflections-module");
        }


    }
}