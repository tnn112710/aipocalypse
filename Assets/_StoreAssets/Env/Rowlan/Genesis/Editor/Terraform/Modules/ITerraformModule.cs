using JBooth.MicroVerseCore;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
    public interface ITerraformModule
    {
        public void OnInspectorGUI();

        /// <summary>
        /// Whether this gameobject is supported by terraform or not
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public bool IsSupported(GameObject gameObject);
    }
}