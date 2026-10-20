// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace sc.screeneffects.runtime
{
    #if SCPE_DEV
    [CreateAssetMenu(fileName = "PostProcessingResources", menuName = "Rendering/Post Processing Resources", order = 0)]
    #endif
    public class PostProcessingResources : ScriptableObject
    {
        //The asset generated and included in the package
        private const string mainGUID = "88b4703b877d026479c280fa369e1449";
        
        [SerializeField]
        public Shader[] shaders;
        
        public static PostProcessingResources FindInEditor()
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(mainGUID);

            if (path == string.Empty)
            {
                Debug.LogError("The PostProcessingResources asset could not be found with the GUID " + mainGUID + ". Was it not imported? Meta file deleted?");
                return null;
            }
            
            Object asset = UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(PostProcessingResources));

            if (asset is not PostProcessingResources r)
            {
                Debug.LogError("The PostProcessingResources asset could not be found at path " + path + ".");
            }
            PostProcessingResources resources = UnityEditor.AssetDatabase.LoadAssetAtPath<PostProcessingResources>(path);
            
            return resources;
#else
            return null;
#endif
        }
        
        [ContextMenu("Assign All Shaders")]
        public void AssignAllShaders()
        {
            FieldInfo[] fields = typeof(ShaderNames).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            List<Shader> m_shaders = new List<Shader>();

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(string) || field.IsLiteral == false)
                {
                    continue;
                }

                string shaderName = field.GetRawConstantValue() as string;
                if (string.IsNullOrEmpty(shaderName))
                {
                    continue;
                }

                Shader shader = Shader.Find(shaderName);
                if (shader && m_shaders.Contains(shader) == false)
                {
                    m_shaders.Add(shader);
                }
            }

            shaders = m_shaders.ToArray();
        }
    }
}