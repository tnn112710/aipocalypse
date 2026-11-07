//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    [InitializeOnLoad]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public static class CozyModuleCollection
    {
        public static List<Type> moduleTypes;
        public static List<Type> ModuleTypes
        {
            get
            {
                if (moduleTypes == null || moduleTypes.Count == 0)
                    moduleTypes = FindAllModules();

                return moduleTypes;
            }
        }
        static CozyModuleCollection()
        {
            if (!SessionState.GetBool("FirstInitDone", false))
            {
                moduleTypes = FindAllModules();

                SessionState.SetBool("FirstInitDone", true);
            }
        }

        public static List<Type> FindAllModules()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            var filteredTypes = TypeCache.GetTypesDerivedFrom<CozyModule>()
                    .Where(type => type.IsClass && !type.IsAbstract)
                    .Where(type => type.IsDefined(typeof(CozyModuleAttribute), false))
                    .ToList();

            return filteredTypes;
        }
    }
}