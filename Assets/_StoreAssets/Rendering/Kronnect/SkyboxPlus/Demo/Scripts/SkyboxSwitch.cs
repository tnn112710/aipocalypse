using UnityEngine;
using UnityEngine.UI;

namespace StylizedSkybox {

    public class SkyboxSwitch : MonoBehaviour {

        public Canvas UI;
        public Text text;
        public Light sun;
        public Rotate sunRotator;
        public Material[] skybox;

        int index;

        void OnEnable() {
            InputProxy.SetupEventSystem();
        }

        void Start() {
            SetSkybox();
        }

        void Update() {
            if (InputProxy.GetKeyDown(KeyCode.Space)) {
                SetSkybox();
                SelectSkyboxMaterial();
            }
            if (InputProxy.GetKeyDown(KeyCode.Alpha1)) {
                sun.transform.eulerAngles = new Vector3(5, 0, 0);
                sunRotator.enabled = false;
            }
            if (InputProxy.GetKeyDown(KeyCode.Alpha2)) {
                sun.transform.eulerAngles = new Vector3(90, 0, 0);
                sunRotator.enabled = false;
            }
            if (InputProxy.GetKeyDown(KeyCode.Alpha3)) {
                sun.transform.eulerAngles = new Vector3(210, 0, 0);
                sunRotator.enabled = false;
            }
            if (InputProxy.GetKeyDown(KeyCode.T)) {
                sunRotator.enabled = !sunRotator.enabled;
            }
            if (InputProxy.GetKeyDown(KeyCode.U)) {
                UI.enabled = !UI.enabled;
            }
        }

        void SelectSkyboxMaterial() {
#if UNITY_EDITOR
            Material skyboxMaterial = RenderSettings.skybox;
            if (skyboxMaterial == null) return;
            UnityEditor.Selection.activeObject = skyboxMaterial;
            UnityEditor.EditorGUIUtility.PingObject(skyboxMaterial);
#endif
        }

        void SetSkybox() {
            if (skybox == null || skybox.Length == 0) return;
            if (++index >= skybox.Length) index = 0;
            Material skyboxMaterial = skybox[index];
            RenderSettings.skybox = skyboxMaterial;
            text.text = "Skybox material: " + skyboxMaterial.name;
        }
    }

}