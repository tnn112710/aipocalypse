using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using ImpossibleRobert.Common.Editor.Timing;
using ImpossibleRobert.Common.Timing;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
    /// <summary>
    /// Drives <see cref="TextStudioText"/> ticks in edit mode via the shared
    /// Common editor tick bridge. Manages per-hub timing so each
    /// hub receives correct delta time, and repaints scene views after any hub
    /// ticked so preview animations stay live.
    ///
    /// Also handles <see cref="TextStudioDefaultSettings"/> and <see cref="TextStudioRegistry"/>
    /// editor-side domain-reload initialization that was previously done via
    /// <c>#if UNITY_EDITOR [InitializeOnLoadMethod]</c> inside the runtime assembly.
    /// </summary>
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    // Static-constructor state is code-load scoped; cleanup is handled explicitly where the type crosses Play Mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static partial class TextStudioEditorTicker
    {
        static readonly Dictionary<TextStudioText, double> s_hubs = new Dictionary<TextStudioText, double>();
        static readonly List<TextStudioText> s_tempKeys = new List<TextStudioText>();
        static readonly Dictionary<ITextStudioEditorTickTarget, double> s_editorTickTargets =
            new Dictionary<ITextStudioEditorTickTarget, double>();
        static readonly List<ITextStudioEditorTickTarget> s_tempEditorTickTargets =
            new List<ITextStudioEditorTickTarget>();
        static readonly Dictionary<TextStudioText, TextStudioPreview.State> s_editModePreviewStates =
            new Dictionary<TextStudioText, TextStudioPreview.State>();
        static readonly HashSet<TextStudioText> s_serializationSuspendedHubs =
            new HashSet<TextStudioText>();
        static readonly List<UnityEngine.Object> s_pendingLifecycleDestroy =
            new List<UnityEngine.Object>();
        static uint s_lastEffectContentRevision;

        static TextStudioEditorTicker()
        {
            RegisterRuntimeHooks();
            EditorTickBridge.Subscribe(EditorTick);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
            PrefabStage.prefabSaved -= OnPrefabSaved;
            PrefabStage.prefabSaved += OnPrefabSaved;

            // Domain-reload initialization previously in runtime #if UNITY_EDITOR blocks.
            TextStudioRegistry.EnsureInitialized();
            TextStudioDefaultSettings.ResetOnDomainReload();
            TextStudio2DGpuFallbackResources.EnsureInitialized();

            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseGpuFallbacks;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseGpuFallbacks;
            AssemblyReloadEvents.beforeAssemblyReload -= FlushLifecycleDestroy;
            AssemblyReloadEvents.beforeAssemblyReload += FlushLifecycleDestroy;
            AssemblyReloadEvents.beforeAssemblyReload -= ClearEditorTickTargets;
            AssemblyReloadEvents.beforeAssemblyReload += ClearEditorTickTargets;
            EditorApplication.quitting -= ReleaseGpuFallbacks;
            EditorApplication.quitting += ReleaseGpuFallbacks;
            EditorApplication.quitting -= FlushLifecycleDestroy;
            EditorApplication.quitting += FlushLifecycleDestroy;
            EditorApplication.quitting -= ClearEditorTickTargets;
            EditorApplication.quitting += ClearEditorTickTargets;

            EditorApplication.delayCall -= RestoreEditModeHubsAfterDomainReload;
            EditorApplication.delayCall += RestoreEditModeHubsAfterDomainReload;
        }

        static void ReleaseGpuFallbacks()
        {
            TextStudio2DGpuFallbackResources.Shutdown();
            TextStudioAppearanceResources.ReleaseShared();
        }

        static void RestoreEditModeHubsAfterDomainReload()
        {
            EditorApplication.delayCall -= RestoreEditModeHubsAfterDomainReload;
            if (!Application.isPlaying)
            {
                TextStudioProjectSettingsLoader.ActivateNow();
                RestoreEditModeHubs();
                RestoreEditorTickTargets();
            }
        }

        static void RegisterRuntimeHooks()
        {
            TextStudioEditorHooks.SetEditorBridgeSubscribers(
                OnHubEnabled,
                OnHubDisabled,
                OnRepaintRequested,
                OnCanonicalSourceImporting,
                DestroyAfterLifecycle,
                OnEditorTickTargetEnabled,
                OnEditorTickTargetDisabled);
        }

        static void DestroyAfterLifecycle(UnityEngine.Object value)
        {
            if (value == null) return;
            s_pendingLifecycleDestroy.Add(value);
            EditorApplication.update -= FlushLifecycleDestroy;
            EditorApplication.update += FlushLifecycleDestroy;
        }

        static void FlushLifecycleDestroy()
        {
            int count = s_pendingLifecycleDestroy.Count;
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object value = s_pendingLifecycleDestroy[i];
                if (value == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(value);
                else UnityEngine.Object.DestroyImmediate(value);
            }
            if (count > 0) s_pendingLifecycleDestroy.RemoveRange(0, count);
            if (s_pendingLifecycleDestroy.Count == 0)
                EditorApplication.update -= FlushLifecycleDestroy;
        }

        static void ClearEditorTickTargets()
        {
            s_editorTickTargets.Clear();
            s_tempEditorTickTargets.Clear();
        }

        static void OnHubEnabled(TextStudioText hub)
        {
            double lastHostTime = 0d;
            hub.ResetHostTime(ref lastHostTime);
            s_hubs[hub] = lastHostTime;
        }

        static void OnHubDisabled(TextStudioText hub)
        {
            s_hubs.Remove(hub);
        }

        static void OnEditorTickTargetEnabled(ITextStudioEditorTickTarget target)
        {
            if (target == null || Application.isPlaying) return;
            s_editorTickTargets[target] = HostTime.CurrentHostTime(ClockMode.Unscaled);
        }

        static void OnEditorTickTargetDisabled(ITextStudioEditorTickTarget target)
        {
            if (target != null) s_editorTickTargets.Remove(target);
        }

        static void OnRepaintRequested()
        {
            if (Application.isPlaying) return;
            HostTime.RequestEditorFrame(null, repaintAllViews: false);
        }

        static void OnCanonicalSourceImporting(TextStudioText hub)
        {
            if (Application.isPlaying || hub == null)
                return;

            Undo.RecordObject(hub, "Import Direct Backend Text");
            EditorUtility.SetDirty(hub);
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            if (Application.isPlaying)
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                TextStudioText[] hubs =
                    roots[rootIndex].GetComponentsInChildren<TextStudioText>(true);
                for (int hubIndex = 0; hubIndex < hubs.Length; hubIndex++)
                    SuspendForSerialization(hubs[hubIndex]);
            }
            ScheduleSerializationResumeFallback();
        }

        static void OnSceneSaved(Scene scene)
        {
            ResumeSerializedHubs(scene, null);
        }

        static void OnPrefabSaving(GameObject prefabRoot)
        {
            if (Application.isPlaying || prefabRoot == null)
                return;

            TextStudioText[] hubs = prefabRoot.GetComponentsInChildren<TextStudioText>(true);
            for (int i = 0; i < hubs.Length; i++)
                SuspendForSerialization(hubs[i]);
            ScheduleSerializationResumeFallback();
        }

        static void OnPrefabSaved(GameObject prefabRoot)
        {
            ResumeSerializedHubs(default, prefabRoot);
        }

        static void SuspendForSerialization(TextStudioText hub)
        {
            if (hub == null || !s_serializationSuspendedHubs.Add(hub))
                return;

            hub.PrepareForEditorSerialization();
            RestoreTransientTmpMaterialReferences(hub);
        }

        static void RestoreTransientTmpMaterialReferences(TextStudioText hub)
        {
            TMP_Text text = hub.GetComponent<TMP_Text>();
            if (text == null || text.font == null)
                return;

            List<Material> transientMaterials = new List<Material>();
            SerializedObject serializedText = new SerializedObject(text);
            serializedText.Update();
            bool textChanged = RestoreSerializedMaterial(
                serializedText.FindProperty("m_sharedMaterial"),
                text,
                0,
                transientMaterials);
            textChanged |= RestoreSerializedMaterialArray(
                serializedText.FindProperty("m_fontSharedMaterials"),
                text,
                transientMaterials,
                clearInstances: false);
            textChanged |= ClearSerializedMaterial(
                serializedText.FindProperty("m_fontMaterial"),
                transientMaterials);
            textChanged |= RestoreSerializedMaterialArray(
                serializedText.FindProperty("m_fontMaterials"),
                text,
                transientMaterials,
                clearInstances: true);
            if (textChanged)
                serializedText.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedRenderer = null;
            Renderer renderer = hub.GetComponent<Renderer>();
            if (renderer != null)
            {
                serializedRenderer = new SerializedObject(renderer);
                serializedRenderer.Update();
                if (RestoreSerializedMaterialArray(
                        serializedRenderer.FindProperty("m_Materials"),
                        text,
                        transientMaterials,
                        clearInstances: false))
                {
                    serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            for (int i = 0; i < transientMaterials.Count; i++)
            {
                Material material = transientMaterials[i];
                if (material == null || IsPersistentMaterialAsset(material))
                    continue;

                serializedText.Update();
                serializedRenderer?.Update();
                if (!SerializedObjectReferencesMaterial(
                        serializedText,
                        serializedRenderer,
                        material))
                {
                    UnityEngine.Object.DestroyImmediate(material, true);
                }
            }
        }

        static bool RestoreSerializedMaterialArray(
            SerializedProperty property,
            TMP_Text text,
            List<Material> transientMaterials,
            bool clearInstances)
        {
            if (property == null || !property.isArray)
                return false;

            bool changed = false;
            bool allCleared = property.arraySize > 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                Material material = element.objectReferenceValue as Material;
                if (!ShouldRestoreTransientMaterial(material))
                {
                    allCleared = false;
                    continue;
                }

                Material source = clearInstances
                    ? null
                    : TextStudioTMPMaterialEffectAdapter.ResolveFontSourceMaterial(
                        text,
                        i);
                if (!clearInstances && source == null)
                {
                    allCleared = false;
                    continue;
                }

                element.objectReferenceValue = source;
                AddUniqueMaterial(transientMaterials, material);
                changed = true;
                allCleared &= source == null;
            }

            if (changed && clearInstances && allCleared)
                property.arraySize = 0;
            return changed;
        }

        static bool RestoreSerializedMaterial(
            SerializedProperty property,
            TMP_Text text,
            int materialIndex,
            List<Material> transientMaterials)
        {
            if (property == null)
                return false;

            Material material = property.objectReferenceValue as Material;
            if (!ShouldRestoreTransientMaterial(material))
                return false;

            Material source =
                TextStudioTMPMaterialEffectAdapter.ResolveFontSourceMaterial(
                    text,
                    materialIndex);
            if (source == null)
                return false;

            AddUniqueMaterial(transientMaterials, material);
            property.objectReferenceValue = source;
            return true;
        }

        static bool ClearSerializedMaterial(
            SerializedProperty property,
            List<Material> transientMaterials)
        {
            if (property == null)
                return false;

            Material material = property.objectReferenceValue as Material;
            if (!ShouldRestoreTransientMaterial(material))
                return false;

            AddUniqueMaterial(transientMaterials, material);
            property.objectReferenceValue = null;
            return true;
        }

        static bool ShouldRestoreTransientMaterial(Material material)
        {
            return material != null &&
                   TextStudioTMPMaterialEffectAdapter.IsTransientProductionMaterial(
                       material) &&
                   !IsPersistentMaterialAsset(material);
        }

        static bool IsPersistentMaterialAsset(Material material)
        {
            string assetPath = AssetDatabase.GetAssetPath(material);
            return !string.IsNullOrEmpty(assetPath) &&
                   !assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                   !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        static void AddUniqueMaterial(
            List<Material> materials,
            Material material)
        {
            if (material != null && !materials.Contains(material))
                materials.Add(material);
        }

        static bool SerializedObjectReferencesMaterial(
            SerializedObject serializedText,
            SerializedObject serializedRenderer,
            Material material)
        {
            return SerializedPropertyReferencesMaterial(
                       serializedText.FindProperty("m_sharedMaterial"),
                       material) ||
                   SerializedPropertyReferencesMaterial(
                       serializedText.FindProperty("m_fontSharedMaterials"),
                       material) ||
                   SerializedPropertyReferencesMaterial(
                       serializedText.FindProperty("m_fontMaterial"),
                       material) ||
                   SerializedPropertyReferencesMaterial(
                       serializedText.FindProperty("m_fontMaterials"),
                       material) ||
                   (serializedRenderer != null &&
                    SerializedPropertyReferencesMaterial(
                        serializedRenderer.FindProperty("m_Materials"),
                        material));
        }

        static bool SerializedPropertyReferencesMaterial(
            SerializedProperty property,
            Material material)
        {
            if (property == null)
                return false;
            if (!property.isArray)
                return ReferenceEquals(property.objectReferenceValue, material);

            for (int i = 0; i < property.arraySize; i++)
            {
                if (ReferenceEquals(
                        property.GetArrayElementAtIndex(i).objectReferenceValue,
                        material))
                {
                    return true;
                }
            }
            return false;
        }

        static void ResumeSerializedHubs(Scene scene, GameObject prefabRoot)
        {
            s_tempKeys.Clear();
            foreach (TextStudioText hub in s_serializationSuspendedHubs)
            {
                if (hub == null ||
                    (prefabRoot != null &&
                     (hub.gameObject == prefabRoot || hub.transform.IsChildOf(prefabRoot.transform))) ||
                    (scene.IsValid() && hub.gameObject.scene == scene))
                {
                    s_tempKeys.Add(hub);
                }
            }

            for (int i = 0; i < s_tempKeys.Count; i++)
            {
                TextStudioText hub = s_tempKeys[i];
                s_serializationSuspendedHubs.Remove(hub);
                if (hub != null)
                    hub.ResumeAfterEditorSerialization();
            }
            s_tempKeys.Clear();
        }

        static void ScheduleSerializationResumeFallback()
        {
            EditorApplication.delayCall -= ResumeSerializationSuspendedHubs;
            EditorApplication.delayCall += ResumeSerializationSuspendedHubs;
        }

        static void ResumeSerializationSuspendedHubs()
        {
            EditorApplication.delayCall -= ResumeSerializationSuspendedHubs;
            if (s_serializationSuspendedHubs.Count == 0)
                return;

            s_tempKeys.Clear();
            foreach (TextStudioText hub in s_serializationSuspendedHubs)
                s_tempKeys.Add(hub);
            s_serializationSuspendedHubs.Clear();
            for (int i = 0; i < s_tempKeys.Count; i++)
            {
                TextStudioText hub = s_tempKeys[i];
                if (hub != null)
                    hub.ResumeAfterEditorSerialization();
            }
            s_tempKeys.Clear();
        }

        static void EditorTick()
        {
            if (Application.isPlaying) return;
            if (BuildPipeline.isBuildingPlayer) return;
            if (s_hubs.Count == 0 && s_editorTickTargets.Count == 0) return;

            // Every Hero owner rendered by this tick reaches the GPU as one submission when the tick ends.
            using TextStudio2DHeroSubmission.BatchScope heroBatch = TextStudio2DHeroSubmission.Batch();
            bool anyTicked = false;
            uint currentEffectContentRevision = TextStudioEffectDefinitionAsset.ContentRevision;
            bool effectContentChanged =
                currentEffectContentRevision != s_lastEffectContentRevision;
            s_lastEffectContentRevision = currentEffectContentRevision;

            // Snapshot keys to allow dictionary modification during iteration.
            s_tempKeys.Clear();
            foreach (TextStudioText hub in s_hubs.Keys)
                s_tempKeys.Add(hub);

            for (int i = 0; i < s_tempKeys.Count; i++)
            {
                TextStudioText hub = s_tempKeys[i];
                if (hub == null)
                {
                    s_hubs.Remove(hub);
                    continue;
                }
                if (!s_hubs.TryGetValue(hub, out double lastTime)) continue;
                // Build preparation owns these temporary objects. Their animations must not run between work batches.
                if (TextStudioRuntimePreparation.IsPreparationPreview(hub.gameObject.scene)) continue;

                if (effectContentChanged)
                    hub.ScheduleTextRebuild();

                if (!hub.TickInEditMode)
                {
                    if (effectContentChanged)
                    {
                        hub.ForceMeshRefresh(0f);
                        anyTicked = true;
                    }
                    hub.ResetHostTime(ref lastTime);
                    s_hubs[hub] = lastTime;
                    continue;
                }

                TimeStep step = hub.CreateHostTimeStep(ref lastTime, 0);
                s_hubs[hub] = lastTime;
                if (step.DeltaTime <= 0f)
                {
                    if (effectContentChanged)
                    {
                        hub.ForceMeshRefresh(0f);
                        anyTicked = true;
                    }
                    continue;
                }

                hub.PerformTick(step);
                anyTicked = true;
            }

            s_tempEditorTickTargets.Clear();
            foreach (ITextStudioEditorTickTarget target in s_editorTickTargets.Keys)
                s_tempEditorTickTargets.Add(target);
            for (int i = 0; i < s_tempEditorTickTargets.Count; i++)
            {
                ITextStudioEditorTickTarget target = s_tempEditorTickTargets[i];
                if (!(target is Behaviour behaviour) || behaviour == null ||
                    !behaviour.isActiveAndEnabled || !behaviour.gameObject.scene.isLoaded)
                {
                    s_editorTickTargets.Remove(target);
                    continue;
                }
                if (TextStudioRuntimePreparation.IsPreparationPreview(behaviour.gameObject.scene))
                    continue;
                if (!s_editorTickTargets.TryGetValue(target, out double lastTime)) continue;
                float deltaTime = HostTime.GetDeltaTime(ref lastTime, clockMode: ClockMode.Unscaled);
                s_editorTickTargets[target] = lastTime;
                anyTicked |= target.AdvanceEditorPreview(deltaTime);
            }

            if (anyTicked)
                HostTime.RequestEditorFrame(null, repaintAllViews: false);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                FlushLifecycleDestroy();
                CaptureEditModePreviewStates();
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                PrepareHubsForPlayMode();
                return;
            }

            if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.EnteredEditMode)
                return;

            s_hubs.Clear();
            s_tempKeys.Clear();
            ClearEditorTickTargets();
            TextStudioEditorHooks.ResetEditorSubscribersForFastEnterPlayMode();
            RegisterRuntimeHooks();

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                TextStudioAppearanceResources.BeginSession();
                RestoreEditModeHubs();
                RestoreEditorTickTargets();
            }
        }

        static void CaptureEditModePreviewStates()
        {
            s_editModePreviewStates.Clear();
            foreach (TextStudioText hub in s_hubs.Keys)
            {
                if (hub != null)
                    s_editModePreviewStates[hub] = hub.Preview.CaptureState();
            }
        }

        static void RestoreEditModeHubs()
        {
            TextStudioText[] hubs = UnityUtils.FindAll<TextStudioText>();
            for (int i = 0; i < hubs.Length; i++)
            {
                TextStudioText hub = hubs[i];
                if (hub == null || !hub.isActiveAndEnabled || !hub.gameObject.scene.isLoaded)
                    continue;

                OnHubEnabled(hub);
                if (s_editModePreviewStates.TryGetValue(hub, out TextStudioPreview.State state))
                    hub.Preview.RestoreState(in state);
            }

            s_editModePreviewStates.Clear();
        }

        static void RestoreEditorTickTargets()
        {
            MonoBehaviour[] behaviours = UnityUtils.FindAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.isActiveAndEnabled &&
                    behaviour.gameObject.scene.isLoaded &&
                    behaviour is ITextStudioEditorTickTarget target)
                    OnEditorTickTargetEnabled(target);
            }
        }

        static void PrepareHubsForPlayMode()
        {
            foreach (TextStudioText hub in UnityUtils.FindAll<TextStudioText>())
            {
                if (hub != null && hub.isActiveAndEnabled && hub.gameObject.scene.isLoaded)
                    hub.PreparePreviewForPlayMode();
            }
        }
    }
}
