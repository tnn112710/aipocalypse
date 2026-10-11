using UnityEngine;

namespace FayLabs.Kroma
{
    [ExecuteAlways]
    public class GetMainLightDirection : MonoBehaviour
    {
        [SerializeField] private Material skyboxMaterial;

        private void Start()
        {
            UpdateMaterialVectors();
        }

        private void Update()
        {
            if (transform.hasChanged)
            {
                UpdateMaterialVectors();
                transform.hasChanged = false;
            }
        }

        private void UpdateMaterialVectors()
        {
            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetVector("_MainLightDirection", transform.forward);
                skyboxMaterial.SetVector("_MainLightUp", transform.up);
                skyboxMaterial.SetVector("_MainLightRight", transform.right);
            }
        }
    }
}
