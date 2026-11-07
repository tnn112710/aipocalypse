//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    public class CozyHub : EditorWindow
    {

        [MenuItem("Tools/COZY: Weather 4/Open Cozy Hub")]
        public static void ShowExample()
        {
            CozyHub wnd = GetWindow<CozyHub>();
            wnd.titleContent = new GUIContent("Cozy Hub");
        }

        public void CreateGUI()
        {
            if (CozyWeather.Instance)
            {
                ScrollView scrollView = new ScrollView();
                InspectorElement inspector = new InspectorElement(CozyWeather.Instance);

                scrollView.Add(inspector);
                rootVisualElement.Add(scrollView);
            }
            else
            {
                Tooltip tooltip = new Tooltip("No instance of COZY detected! Please set up your scene.", StatusType.Fail);
                rootVisualElement.Add(tooltip);

                Button setupButton = new Button(() => { CozySceneSetupHelper.SetupSceneBasicModules(); });
                setupButton.text = "Setup Scene";
                rootVisualElement.Add(setupButton);
            }
        }

    }
}