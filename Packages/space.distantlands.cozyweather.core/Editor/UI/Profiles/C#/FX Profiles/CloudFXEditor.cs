//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CloudFX))]
    [CanEditMultipleObjects]
    public class CloudFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField cumulusCoverage = new PropertyField();
            cumulusCoverage.BindProperty(serializedObject.FindProperty("cumulusCoverage"));
            container.Add(cumulusCoverage);

            PropertyField altocumulusCoverage = new PropertyField();
            altocumulusCoverage.BindProperty(serializedObject.FindProperty("altocumulusCoverage"));
            container.Add(altocumulusCoverage);

            PropertyField contrailsCoverage = new PropertyField();
            contrailsCoverage.BindProperty(serializedObject.FindProperty("contrailsCoverage"));
            container.Add(contrailsCoverage);

            PropertyField cirrostratusCoverage = new PropertyField();
            cirrostratusCoverage.BindProperty(serializedObject.FindProperty("cirrostratusCoverage"));
            container.Add(cirrostratusCoverage);

            PropertyField cirrusCoverage = new PropertyField();
            cirrusCoverage.BindProperty(serializedObject.FindProperty("cirrusCoverage"));
            container.Add(cirrusCoverage);

            PropertyField borderCloudsCoverage = new PropertyField();
            borderCloudsCoverage.BindProperty(serializedObject.FindProperty("borderCloudsCoverage"));
            container.Add(borderCloudsCoverage);

            PropertyField cumulonimbusCoverage = new PropertyField();
            cumulonimbusCoverage.BindProperty(serializedObject.FindProperty("cumulonimbusCoverage"));
            container.Add(cumulonimbusCoverage);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            
            return container;
        }
    }
}