//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEditor.Build;

namespace DistantLands.Cozy.EditorScripts
{
    /// <summary>
    /// Adds defines to your project on load
    /// </summary>
    [InitializeOnLoad]
    public class AddCozyDefines : Editor
    {
        public static readonly string[] Symbols = new string[] {
            "COZY_WEATHER",
            "COZY_4_AND_UP"
        };

        static AddCozyDefines()
        {
            NamedBuildTarget nbt = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

            string defines = PlayerSettings.GetScriptingDefineSymbols(nbt);
            string newDefines = defines;

            foreach (var symbol in Symbols)
                if (!HasDefine(newDefines, symbol))
                {
                    if (!string.IsNullOrEmpty(newDefines))
                        newDefines += ";";
                    newDefines += symbol;
                }

            PlayerSettings.SetScriptingDefineSymbols(nbt, newDefines);
        }

        static bool HasDefine(string definesString, string defineToCheck)
        {
            if (string.IsNullOrEmpty(definesString))
                return false;

            string[] parts = definesString.Split(';');
            foreach (var d in parts)
                if (d.Trim() == defineToCheck)
                    return true;
            return false;
        }
    }
}
