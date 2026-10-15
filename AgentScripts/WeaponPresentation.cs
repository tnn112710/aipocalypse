using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Aipocalypse.Core.PlayerSystem;

public static class WeaponPresentation
{
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";

    public static object Inspect()
    {
        var scene = EditorSceneManager.GetActiveScene();
        return new { scene = scene.path, dirty = scene.isDirty,
            cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
                .Select(c => new { c.name, near = c.nearClipPlane, c.fieldOfView, mask = c.cullingMask }).ToArray() };
    }

    public static object Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        int layer = LayerMask.NameToLayer("HeldWeapon");
        if (layer < 0)
        {
            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { layer = i; break; }
            if (layer < 0) throw new InvalidOperationException("No free user layer.");
            layers.GetArrayElementAtIndex(layer).stringValue = "HeldWeapon";
            tags.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            Camera world = root.GetComponentsInChildren<Camera>(true).Single(c => c.name == "PlayerCamera");
            var worldData = world.GetUniversalAdditionalCameraData();
            Transform existing = world.transform.Find("WeaponCamera");
            GameObject weaponObject = existing != null ? existing.gameObject : new GameObject("WeaponCamera");
            weaponObject.transform.SetParent(world.transform, false);
            weaponObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Camera weapon = weaponObject.GetComponent<Camera>();
            if (weapon == null) weapon = weaponObject.AddComponent<Camera>();
            weapon.CopyFrom(world);
            weapon.tag = "Untagged";
            weapon.cullingMask = 1 << layer;
            weapon.clearFlags = CameraClearFlags.Nothing;
            weapon.nearClipPlane = 0.01f;
            weapon.farClipPlane = 10f;
            weapon.useOcclusionCulling = false;
            world.cullingMask &= ~(1 << layer);
            var weaponData = weapon.GetUniversalAdditionalCameraData();
            weaponData.renderType = CameraRenderType.Overlay;
            var cameraSettings = new SerializedObject(weaponData);
            cameraSettings.FindProperty("m_ClearDepth").boolValue = true;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            weaponData.renderPostProcessing = false;
            weaponData.antialiasing = AntialiasingMode.None;
            weaponData.renderShadows = false;
            if (!worldData.cameraStack.Contains(weapon)) worldData.cameraStack.Add(weapon);

            var sync = world.GetComponent<WeaponOverlayCamera>();
            if (sync == null) sync = world.gameObject.AddComponent<WeaponOverlayCamera>();
            var serialized = new SerializedObject(sync);
            serialized.FindProperty("_worldCamera").objectReferenceValue = world;
            serialized.FindProperty("_weaponCamera").objectReferenceValue = weapon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var hand = root.transform.Find("CameraRoot/CameraPitch/HeldItems/Hand");
            hand.gameObject.layer = layer;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return new { layer, prefab = PlayerPath };
    }
}
