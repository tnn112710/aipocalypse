//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    /// <summary>
    /// A define definition for an integration for COZY 4
    /// </summary>
    readonly struct IntegrationDefine
    {
        public readonly string defineSymbol;
        public readonly string checkedType;
        public readonly string assembly;
        public IntegrationDefine(string defineSymbol, string checkedType, string assembly)
        {
            this.defineSymbol = defineSymbol;
            this.checkedType = checkedType;
            this.assembly = assembly;
        }
    }

    /// <summary>
    /// Add scripting define symbols for integrations with packages that are not setup with their own define symbol or as a UPM package.
    /// </summary>
    [InitializeOnLoad]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class AddDefinesForIntegrations
    {
        private static readonly List<IntegrationDefine> DEFINE_SYMBOLS = new()
    {
        new("COZY_SW3", "StylizedWater3.WaveProfile", "sc.stylizedwater3.runtime"),
        new("COZY_BK", "BKPureNature.BK_EnvironmentManager", "Assembly-CSharp")
    };

        static AddDefinesForIntegrations()
        {
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            List<string> definesList = currentDefines.Split(';').ToList();

            bool definesChanged = false;

            foreach (var symbol in DEFINE_SYMBOLS)
            {
                bool isInstalled = System.Type.GetType($"{symbol.checkedType}, {symbol.assembly}") != null;

                if (isInstalled && !definesList.Contains(symbol.defineSymbol))
                {
                    definesList.Add(symbol.defineSymbol);
                    definesChanged = true;
                    Debug.Log($"[AssetChecker] Asset found. Added define: {symbol.defineSymbol}");
                }

                else if (!isInstalled && definesList.Contains(symbol.defineSymbol))
                {
                    definesList.Remove(symbol.defineSymbol);
                    definesChanged = true;
                    Debug.Log($"[AssetChecker] Asset missing. Removed define: {symbol.defineSymbol}");
                }
            }

            if (definesChanged)
            {
                string newDefines = string.Join(";", definesList);
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newDefines);
            }
        }
    }
}