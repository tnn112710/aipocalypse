using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelLabs.Shared.Lib
{
    /// <summary>
    /// Source-only adapters for Unity APIs that differ between Editor versions.
    /// Ship this assembly as raw scripts so directives compile in the customer's Editor.
    /// Core DLLs call these stable signatures without referencing version-specific ID types.
    /// </summary>
    public static class UnityEditorCompatibility
    {
        /// <summary>Returns the compatibility bucket used by existing engine version checks.</summary>
        public static int GetEditorVersionIdentifier()
        {
#if UNITY_6000_5_OR_NEWER
            return 6000_5;
#elif UNITY_2022_2_OR_NEWER
            return 2022_2;
#elif UNITY_2021_3_OR_NEWER
            return 2021_3;
#elif UNITY_2021_1_OR_NEWER
            return 2021_1;
#elif UNITY_2018_4_OR_NEWER
            return 2018_4;
#else
            return 0;
#endif
        }

        /// <summary>Reads the verbosity symbol from the source compilation.</summary>
        public static bool GetVerbosity()
        {
#if ULTIMATE_PREVIEW_VERBOSE
            return true;
#else
            return false;
#endif
        }

        /// <summary>Returns a session ID without exposing EntityId in the DLL-facing API.</summary>
        public static long GetId(Object target)
        {
            if (target == null)
                return -1;

#if UNITY_6000_4_OR_NEWER
            return unchecked((long)EntityId.ToULong(target.GetEntityId()));
#else
            return target.GetInstanceID();
#endif
        }

        /// <summary>Supplies the ID type used by Unity's internal AssetPreview overload.</summary>
        public static Type GetAssetPreviewIdType()
        {
#if UNITY_6000_4_OR_NEWER
            return typeof(EntityId);
#else
            return typeof(int);
#endif
        }

        /// <summary>Checks the current process before a settings loader accesses assets.</summary>
        public static bool IsAssetImportWorkerProcess()
        {
#if UNITY_2020_2_OR_NEWER
            return AssetDatabase.IsAssetImportWorkerProcess();
#else
            return false;
#endif
        }

        /// <summary>Resolves the changed source without modifying it.</summary>
        public static Object GetChangedObject(ChangeGameObjectOrComponentPropertiesEventArgs change)
        {
#if UNITY_6000_4_OR_NEWER
            return EditorUtility.EntityIdToObject(change.entityId);
#else
            return EditorUtility.InstanceIDToObject(change.instanceId);
#endif
        }

        // Keep the selection array opaque to DLL callers. Capture and restore it through
        // these paired methods so each Editor uses its matching native ID array type.
        public static object GetSelectionIds()
        {
#if UNITY_6000_5_OR_NEWER
            return Selection.entityIds;
#else
            return Selection.instanceIDs;
#endif
        }

        public static void SetSelectionIds(object ids)
        {
#if UNITY_6000_5_OR_NEWER
            Selection.entityIds = ids as EntityId[];
#else
            Selection.instanceIDs = ids as int[];
#endif
        }

        public static void ClearSelection()
        {
#if UNITY_6000_5_OR_NEWER
            Selection.entityIds = new EntityId[0];
#else
            Selection.instanceIDs = new int[0];
#endif
        }
    }
}
