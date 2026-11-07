using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace JBooth.MicroVerseCore
{

    [CustomEditor(typeof(ClearMeshStamp))]
    public class ClearMeshStampEditor : Editor
    {
        static List<ISpawner> spawners = new List<ISpawner>(2);
        public override void OnInspectorGUI()
        {
            GUIUtil.DrawHeaderLogo();
            ClearMeshStamp tp = (ClearMeshStamp)target;
            tp.transform.GetComponents<ISpawner>(spawners);
            if (spawners.Count > 1)
            {
                EditorGUILayout.HelpBox("Clear Stamp should be on it's own game object, otherwise SDF fields can be incorrectly computed", MessageType.Error);
            }

            serializedObject.Update();
            
            using var changeScope = new EditorGUI.ChangeCheckScope();

            DrawClearToggleWithPadding("clearTrees",   "treePadding",   "treeFalloff",   "Trees");
            DrawClearToggleWithPadding("clearDetails", "detailPadding", "detailFalloff", "Details");
#if __MICROVERSE_OBJECTS__
            DrawClearToggleWithPadding("clearObjects", "objectPadding", "objectFalloff", "Objects");
#endif

            // Object-layer silhouette controls. Renders a top-down binary
            // silhouette of all scene renderers on the selected layers and
            // gates the clear operation by it — so trees / details don't
            // spawn inside the footprint of buildings / props.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Object Layer Mask", EditorStyles.boldLabel);

            var layerMaskProp = serializedObject.FindProperty("objectLayerMask");
            EditorGUILayout.PropertyField(layerMaskProp, new GUIContent("Layer Mask",
                "Clear trees / details / objects only where scene renderers on these layers appear (top-down). Set to Nothing to disable."));

            using (new EditorGUI.DisabledScope(layerMaskProp.intValue == 0))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("excludeTerrain"), new GUIContent("Exclude Terrain",
                    "Temporarily disable all Terrain components during silhouette capture so the terrain surface doesn't fill the mask."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("yMargin"), new GUIContent("Y Margin",
                    "Extra vertical margin (metres) above the terrain's max height when deciding which renderers count as on the terrain — keeps backdrop / sky geometry out of the mask."));
            }

            if (tp.GetComponentInParent<MicroVerse>() == null)
            {
                EditorGUILayout.HelpBox("Stamp is not under MicroVerse in the heriarchy, will have no effect", MessageType.Warning);
            }


            var otherTextureWeight = serializedObject.FindProperty("filterSet").FindPropertyRelative("otherTextureWeight");
            var filters = serializedObject.FindProperty("filterSet").FindPropertyRelative("textureFilters");
            var filtersEnabled = serializedObject.FindProperty("filterSet").FindPropertyRelative("textureFilterEnabled");
            GUIUtil.DrawFilterSet(tp, tp.filterSet, otherTextureWeight, filters, filtersEnabled, tp.transform, true);

            serializedObject.ApplyModifiedProperties();

            if (changeScope.changed)
            {
                MicroVerse.instance?.Invalidate(tp.GetBounds(), MicroVerse.InvalidateType.All); // because tree's can now mod height/splats
            }

        }

        private void OnSceneGUI()
        {
            var stamp = (ClearMeshStamp)target;
            if (stamp.filterSet.falloffFilter.filterType == FalloffFilter.FilterType.PaintMask)
            {
                GUIUtil.DoPaintSceneView(stamp, SceneView.currentDrawingSceneView, stamp.filterSet.falloffFilter.paintMask, stamp.GetBounds(), stamp.transform);
            }
        }

        private bool HasFrameBounds() => Selection.objects.Length > 0;

        public Bounds OnGetFrameBounds()
        {
            var transforms = Selection.GetTransforms(SelectionMode.Unfiltered);
            Bounds result = new Bounds(transforms[0].position, transforms[0].lossyScale);
            for (int i = 1; i < transforms.Length; i++)
                result.Encapsulate(new Bounds(transforms[i].position, transforms[i].lossyScale));

            result.extents *= 0.5f;
            return result;
        }


        private void OnEnable()
        {
            EditorApplication.update += OnUpdate;
            SceneView.duringSceneGui += OnSceneRepaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            SceneView.duringSceneGui -= OnSceneRepaint;
        }

        static Texture2D overlayTex;
        private void OnSceneRepaint(SceneView sceneView)
        {
            RenderTexture.active = sceneView.camera.activeTexture;
            if (MicroVerse.instance != null)
            {
                if (overlayTex == null)
                {
                    overlayTex = Resources.Load<Texture2D>("microverse_stamp_tree");
                }
                var terrains = MicroVerse.instance.terrains;
                var hs = (target as ClearMeshStamp);
                if (hs == null) return;
                Color color = Color.green;
                if (MicroVerse.instance != null)
                {
                    color = MicroVerse.instance.options.colors.treeStampColor;
                }
                PreviewRenderer.DrawStampPreview(hs, terrains, hs.transform, hs.filterSet.falloffFilter, color, overlayTex);
            }
        }

        private void OnUpdate()
        {
            foreach (var target in targets)
            {
                if (target == null)
                    continue;
                var clear = (ClearMeshStamp)target;
                if (clear != null && clear.transform.hasChanged)
                {
                    clear.transform.hasChanged = false;
                    var r = clear.transform.localRotation.eulerAngles;
                    r.z = 0;
                    clear.transform.localRotation = Quaternion.Euler(r);
                    MicroVerse.instance?.Invalidate(clear.GetBounds(), MicroVerse.InvalidateType.All);
                }
            }
        }

        // Draws a clear-category toggle (e.g. clearTrees) and, when enabled,
        // its associated SDF padding/falloff sliders indented underneath.
        // Per-category settings let trees keep a wider buffer than grass.
        void DrawClearToggleWithPadding(string togglePropName, string paddingPropName, string falloffPropName, string label)
        {
            var toggleProp = serializedObject.FindProperty(togglePropName);
            EditorGUILayout.PropertyField(toggleProp);
            if (toggleProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty(paddingPropName), new GUIContent(label + " Padding (m)",
                    "Full-clear buffer distance (metres) around each object's footprint for " + label.ToLower() +
                    ", measured via exact-Euclidean SDF (jump-flood). 0 = clear only the footprint itself."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(falloffPropName), new GUIContent(label + " Falloff (m)",
                    "Width of the smooth fade that extends OUTWARD past the padded buffer for " + label.ToLower() +
                    ". 0 = hard cutoff at padding edge. Total clear reach = padding + falloff."));
                EditorGUI.indentLevel--;
            }
        }
    }
}
