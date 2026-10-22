using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
public static class PlayerPost
{
    public static object Report()
    {
        var player = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Player");
        var sb = new StringBuilder();
        foreach (var cam in player.GetComponentsInChildren<Camera>(true))
        {
            var d = cam.GetUniversalAdditionalCameraData();
            sb.AppendLine($"{cam.name}: type {d.renderType} post {d.renderPostProcessing} stack [{(d.renderType == CameraRenderType.Base && d.cameraStack != null ? string.Join(",", d.cameraStack.Select(c => c ? c.name : "null")) : "-")}] aa {d.antialiasing} far {cam.farClipPlane} fov {cam.fieldOfView}");
        }
        return sb.ToString();
    }

    // Scene-only override (the Player prefab asset is untouched): grade the whole stack via its last camera.
    public static object Enable(string cameraName)
    {
        var player = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Player");
        var cam = player.GetComponentsInChildren<Camera>(true).First(c => c.name == cameraName);
        var d = cam.GetUniversalAdditionalCameraData();
        Undo.RecordObject(d, "Post processing");
        d.renderPostProcessing = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(d);
        EditorSceneManager.MarkSceneDirty(player.scene);
        return Report();
    }
}
