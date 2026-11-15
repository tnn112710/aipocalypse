// The jam-clearing shake (GunShakeSettings), for `unity command script run`.
//   Create()               Data/Weapons/GunShake.asset with the default values, if it isn't there yet
//   Assign(gun, true)      that gun's <Gun>_Repair profile shows its repair as the shake (false = back to its clips)
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEngine;

public static class GunShakeSetup
{
    const string AssetPath = "Assets/_Game/Data/Weapons/GunShake.asset";

    public static string Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GunShakeSettings>(AssetPath) != null)
            return AssetPath + " exists";
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GunShakeSettings>(), AssetPath);
        return "created " + AssetPath;
    }

    public static string Assign(string gun, bool shake)
    {
        string path = $"Assets/_Game/Content/Weapons/{gun}/{gun}_Repair.asset";
        var profile = AssetDatabase.LoadAssetAtPath<GunRepairProfile>(path);
        if (profile == null)
            return "no repair profile at " + path;
        profile.Shake = shake ? AssetDatabase.LoadAssetAtPath<GunShakeSettings>(AssetPath) : null;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return $"{gun}: repair shake {(profile.Shake != null ? "on" : "off")}";
    }
}
