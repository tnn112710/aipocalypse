namespace Sergio.Rendering
{

    using UnityEngine;

    [ExecuteAlways] // run in edit mode too
    public class GlobalWindController : MonoBehaviour
    {
        [Header("Shader Graph Wind Controls")]

        [Range(0f, 1f)]
        public float GlobalWindStrength = 1f;

        [Range(0f, 2f)]
        public float DirectionalWindStrengthMultiplier = 1f;

        [Range(0f, 2f)]
        public float DirectionalWindSpeedMultiplier = 1f;

        [Range(0f, 360f)]
        public float WindDirection = 0f;

        [Range(0f, 1f)]
        public float OmnidirectionalWindStrengthMultiplier = 1f;

        [Range(0f, 1f)]
        public float OmnidirectionalWindSpeedMultiplier = 1f;

        [Range(0f, 1f)]
        public float DetailWindMultiplier = 1f;

        //  [Header("Interaction")]
        // public Transform player;
        //  private Vector3 playerPos;

        [Header("Editor Visualization")]
        public bool EnableEditorVisualization = true;
        public GameObject ArrowModelPrefab; // assign arrow prefab
        [Range(0f, 50f)] public float GizmoRadius = 3f;
        public Color GizmoColor = Color.cyan;

        private GameObject arrowInstance;
        private float lastWindDirection;
        private float lastTransformY;

        void Update()
        {
            //  playerPos = player.transform.position;
            SyncWindAndTransform();
            ApplyGlobals();
            UpdateArrow();
        }

        void OnValidate()
        {
            SyncWindAndTransform();
            ApplyGlobals();
            UpdateArrow();
        }

        private void SyncWindAndTransform()
        {
            float currentTransformY = transform.eulerAngles.y;

            // If WindDirection changed externally, update transform
            if (!Mathf.Approximately(WindDirection, lastWindDirection))
            {
                transform.rotation = Quaternion.Euler(0f, WindDirection, 0f);
                lastTransformY = transform.eulerAngles.y;
            }
            // If transform rotated manually, update WindDirection
            else if (!Mathf.Approximately(currentTransformY, lastTransformY))
            {
                WindDirection = currentTransformY;
                lastWindDirection = WindDirection;
            }

            lastWindDirection = WindDirection;
            lastTransformY = transform.eulerAngles.y;
        }

        private void ApplyGlobals()
        {
            // Shader.SetGlobalVector("_PlayerPos", playerPos);

            Shader.SetGlobalFloat("_Global_Wind_Multiplier", GlobalWindStrength);
            Shader.SetGlobalFloat("_Directional_Wind_Multiplier", DirectionalWindStrengthMultiplier);
            Shader.SetGlobalFloat("_Directional_Wind_Speed_Multiplier", DirectionalWindSpeedMultiplier);
            Shader.SetGlobalFloat("_Global_Wind_Direction", WindDirection);

            Shader.SetGlobalFloat("_Omnidirectional_Wind_Strength_Multiplier", OmnidirectionalWindStrengthMultiplier);
            Shader.SetGlobalFloat("_Omnidirectional_Wind_Speed_Multiplier", OmnidirectionalWindSpeedMultiplier);
            Shader.SetGlobalFloat("_Detail_Wind_Multiplier", DetailWindMultiplier);

        }

        private void UpdateArrow()
        {
#if UNITY_EDITOR
            if (!EnableEditorVisualization)
            {
                if (arrowInstance != null)
                    arrowInstance.SetActive(false);
                return;
            }

            if (!Application.isPlaying)
            {
                if (ArrowModelPrefab != null && arrowInstance == null)
                {
                    arrowInstance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(ArrowModelPrefab, transform);
                    arrowInstance.hideFlags = HideFlags.DontSave;
                }

                if (arrowInstance != null)
                {
                    arrowInstance.transform.localPosition = Vector3.zero;
                    arrowInstance.transform.rotation = Quaternion.Euler(0f, WindDirection, 0f);
                    arrowInstance.SetActive(true);
                }
            }
            else
            {
                if (arrowInstance != null)
                    arrowInstance.SetActive(false);
            }
#endif
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!EnableEditorVisualization) return;
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(transform.position, GizmoRadius);
        }
#endif
    }
}
