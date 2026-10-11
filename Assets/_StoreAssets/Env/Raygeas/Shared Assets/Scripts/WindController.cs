using UnityEditor;
using UnityEngine;

namespace Raygeas
{
    [ExecuteInEditMode]

    public class WindController : MonoBehaviour
    {
        //Wind variables
        [Header("Tree Wind Settings")]
        [SerializeField] private float treeWindForce = 2f;
        [SerializeField] private float treeWindSpeed = 15f;
        [SerializeField] private float treeWindWavesScale = 0.5f;
        [SerializeField] private float treeWindFlowDensity = 0.5f;

        [Header("Grass Wind Settings")]
        [SerializeField] private float grassWindForce = 1f;
        [SerializeField] private float grassWindSpeed = 2f;

        private Vector3 windDirection;

        //Arrow variables
        private Color arrowColor = Color.green;

        void Update()
        {
            windDirection = transform.forward;

            //Set wind settings
            Shader.SetGlobalFloat("RaygeasTreeWindForce", treeWindForce);
            Shader.SetGlobalFloat("RaygeasTreeWindSpeed", treeWindSpeed);
            Shader.SetGlobalFloat("RaygeasTreeWindWavesScale", treeWindWavesScale);
            Shader.SetGlobalFloat("RaygeasTreeWindFlowDensity", treeWindFlowDensity);
            
            Shader.SetGlobalFloat("RaygeasGrassWindForce", grassWindForce);
            Shader.SetGlobalFloat("RaygeasGrassWindSpeed", grassWindSpeed);

            Shader.SetGlobalVector("RaygeasWindDirection", windDirection);
        }

#if UNITY_EDITOR
        //Draw arrow
        void OnDrawGizmos()
        {
            if (Selection.activeGameObject == gameObject)
            {
                float arrowLength = 1.5f;
                float arrowWidth = 3f;
                Handles.color = arrowColor;

                Vector3 arrowEnd = transform.position + transform.forward * arrowLength;

                //Draw arrow body
                Handles.DrawLine(transform.position - transform.forward * arrowLength, arrowEnd);

                // Draw arrowhead
                Handles.DrawAAConvexPolygon(
                    new Vector3[] {
                arrowEnd - transform.up * arrowWidth / 4,
                arrowEnd + transform.forward * arrowWidth,
                arrowEnd + transform.up * arrowWidth / 4
                    }
                );
                Handles.DrawAAConvexPolygon(
                    new Vector3[] {
                arrowEnd - transform.right * arrowWidth / 4,
                arrowEnd + transform.forward * arrowWidth,
                arrowEnd + transform.right * arrowWidth / 4
                    }
                );
            }
        }
#endif
    }
}
