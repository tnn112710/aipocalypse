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

using sc.screeneffects.runtime;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace sc.screeneffects.editor
{
    public static class MenuItems
    {
        [MenuItem("GameObject/Rendering/Post Effect Mask", priority = CoreUtils.Priorities.gameObjectMenuPriority)]
        static void CreatePostEffectMask(MenuCommand menuCommand)
        {
            var parent = menuCommand.context as GameObject;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObjectUtility.SetParentAndAlign(go, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;

            if (parent != null)
                go.transform.localPosition = Vector3.zero;
            else
            {
                if (EditorPrefs.GetBool("Create3DObject.PlaceAtWorldOrigin", false))
                    go.transform.localPosition = Vector3.zero;
                else
                    EditorApplication.ExecuteMenuItem("GameObject/Move To View");
            }
            
            PostEffectMask mask = go.AddComponent<PostEffectMask>();
            
            Collider collider = go.GetComponent<SphereCollider>();
            CoreUtils.Destroy(collider);

            mask.UpdateMaterial();
        }
        
        [MenuItem("GameObject/Volume/Scene Volume", priority = CoreUtils.Priorities.gameObjectMenuPriority)]
        static void CreateGlobalVolume(MenuCommand menuCommand)
        {
            var go = CoreEditorUtils.CreateGameObject("Scene Volume", menuCommand.context);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            
            volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile.name = SceneManager.GetActiveScene().name;
        }
        
        [MenuItem("CONTEXT/Volume/Create scene profile")]
        static void CreateNonPersistentProfile(MenuCommand cmd)
        {
            Volume volume = (Volume)cmd.context;

            VolumeProfile profile = volume.sharedProfile;
            volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            
            if(profile) EditorUtility.CopySerialized(profile, volume.sharedProfile);
            
            volume.sharedProfile.name = profile ? profile.name : volume.gameObject.name + " (Scene)";
            
            EditorUtility.SetDirty(volume);
        }

        [MenuItem("Window/SC Screen Effects/Open demo scene", false, 1004)]
        public static void OpenDemoScene()
        {
            string path = AssetDatabase.GUIDToAssetPath("68fa2f4adc4c19840a3b3b3546150064");

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        [MenuItem("Window/SC Screen Effects/Show Prefabs", false, 1005)]
        public static void OpenPrefabsFolder()
        {
            string path = AssetDatabase.GUIDToAssetPath("57f139e9dcef96e49975a2bd899be49e");
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(path);

            EditorGUIUtility.PingObject(folder);
        }
    }
}