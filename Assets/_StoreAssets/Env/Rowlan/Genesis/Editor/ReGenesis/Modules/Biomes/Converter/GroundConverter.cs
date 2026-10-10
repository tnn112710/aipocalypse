using JBooth.MicroVerseCore;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public class GroundConverter
    {
        private static GUIContent defaultsContent = new GUIContent("Defaults", "Default configurations for various use cases. Changes clear stamp factor, height stamp factor, etc to predefined values");
        private static GUIContent removalTypeContent = new GUIContent("Removal Stamp Type", "Stamp type to be used for removal of trees, details and objects");
        private static GUIContent clearStampFactorContent = new GUIContent("Clear Stamp Factor", "Factor to be multiplied to the bounds which are used for the scale");
        private static GUIContent occlusionStampFactorContent = new GUIContent("Occlusion Stamp Factor", "Factor to be multiplied to the bounds which are used for the scale");
        private static GUIContent heightStampFactorContent = new GUIContent("Height Stamp Factor", "Factor to be multiplied to the bounds which are used for the scale");
        private static GUIContent useBoundsPositionOffsetContent = new GUIContent("Bounds Position Offset", "Use the mesh bounds for the position offset. This is useful when eg for a house the pivot isn't at the bottom center");
        private static GUIContent falloffTypeContent = new GUIContent("Falloff Type", "The falloff type to use");
        private static GUIContent rangeFalloffContent = new GUIContent("Falloff Range", "The falloff to use for the range");
        private static GUIContent splineAreaHeightOffsetYContent = new GUIContent("Height Offset Y", "The offset to be applied to the height stamp in y axis");
        private static GUIContent splineAreaClearFalloffContent = new GUIContent("Clear Falloff", "The falloff to use for the clear stamps which use the spline area");
        private static GUIContent splineAreaHeightFalloffContent = new GUIContent("Height Falloff", "The falloff to use for the height stamps which use the spline area");
        private static GUIContent splineAreaTextureFalloffContent = new GUIContent("Texture Falloff", "The falloff to use for the texture stamps which use the spline area");

        private static GUIContent convertToGroundObjectContent = new GUIContent("Convert to Ground Object", "Reparent and add Clear and Height Stamps");
        private static GUIContent convertToRegularObjectContent = new GUIContent("Convert to Regular Object", "Unparent and remove Clear and Height Stamps");

        private ReGenesisModule editor;

        private enum Defaults
        {
            Smooth,
            Approximate,
            Hard
        }

        private enum RemovalType
        {
            ClearStamp,
            OcclusionStamp,
        }

        private enum FalloffType
        {
            Range,
            SplineArea,
        }

        private class RangeSettings
        {
            public Vector2 falloff = new Vector2(0.8f, 1.0f);
        }

        private class SplineAreaSettings
        {
            public float heightOffsetY = 0f;
            public float clearFalloff = 0f;
            public float heightFalloff = 2f;
            public float textureFalloff = 2f;
        }

        private Defaults defaults = Defaults.Smooth;
        private RemovalType removalType = RemovalType.ClearStamp;
        private float clearStampFactor = 1.0f;
        private float occlusionStampFactor = 1.0f;
        private float heightStampFactor = 1.4f;
        private bool useBoundsPositionOffset = true;
        private FalloffType falloffType = FalloffType.Range;
        private RangeSettings rangeSettings = new RangeSettings();
        private SplineAreaSettings splineAreaSettings = new SplineAreaSettings();


        public GroundConverter(ReGenesisModule editor)
        {
            this.editor = editor;

            UpdateDefaults();

        }

        private void UpdateDefaults()
        {
            switch (defaults)
            {
                case Defaults.Smooth:
                    clearStampFactor = 1.2f;
                    occlusionStampFactor = 1.2f;
                    heightStampFactor = 1.8f;
                    falloffType = FalloffType.Range;
                    rangeSettings.falloff = new Vector2(0.5f, 1.0f);
                    splineAreaSettings.heightOffsetY = 0f;
                    splineAreaSettings.clearFalloff = 0f;
                    splineAreaSettings.heightFalloff = 4f;
                    splineAreaSettings.textureFalloff = 4f;
                    break;
                case Defaults.Approximate:
                    clearStampFactor = 1.0f;
                    occlusionStampFactor = 1.0f;
                    heightStampFactor = 1.4f;
                    falloffType = FalloffType.Range;
                    rangeSettings.falloff = new Vector2(0.8f, 1.0f);
                    splineAreaSettings.heightOffsetY = 0f;
                    splineAreaSettings.clearFalloff = 0f;
                    splineAreaSettings.heightFalloff = 2f;
                    splineAreaSettings.textureFalloff = 2f;
                    break;
                case Defaults.Hard:
                    clearStampFactor = 1.0f;
                    occlusionStampFactor = 1.0f;
                    heightStampFactor = 1.0f;
                    falloffType = FalloffType.Range;
                    rangeSettings.falloff = new Vector2(1.0f, 1.0f);
                    splineAreaSettings.heightOffsetY = 0f;
                    splineAreaSettings.clearFalloff = 0f;
                    splineAreaSettings.heightFalloff = 1f;
                    splineAreaSettings.textureFalloff = 1f;
                    break;
            }
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Selected gameobject\n* reparent to gameobject with same name\n* add Ground gameobject\n* add Clear (or Occlusion) and Height stamps to Ground\n* calculate bounds\n* apply bounds to Clear and Height stamps considering falloff type", MessageType.None);
            EditorGUILayout.HelpBox("This is just a helper to get you faster into the modification for the ground alignment. Manual adjustment may be required (eg because of pivot adjustment, detail bounds, etc)", MessageType.None);

            using (var check = new EditorGUI.ChangeCheckScope())
            {

                defaults = (Defaults)EditorGUILayout.EnumPopup(defaultsContent, defaults);

                if( check.changed )
                {
                    UpdateDefaults();
                }
            }

            removalType = (RemovalType)EditorGUILayout.EnumPopup(removalTypeContent, removalType);

            switch (removalType)
            {
                case RemovalType.ClearStamp:
                    clearStampFactor = EditorGUILayout.FloatField(clearStampFactorContent, clearStampFactor);
                    break;
                case RemovalType.OcclusionStamp:
                    occlusionStampFactor = EditorGUILayout.FloatField(occlusionStampFactorContent, occlusionStampFactor);
                    break;
            }

            heightStampFactor = EditorGUILayout.FloatField(heightStampFactorContent, heightStampFactor);
            useBoundsPositionOffset = EditorGUILayout.Toggle(useBoundsPositionOffsetContent, useBoundsPositionOffset);
            falloffType = (FalloffType)EditorGUILayout.EnumPopup(falloffTypeContent, falloffType);

            EditorGUI.indentLevel++;
            {
                switch (falloffType)
                {
                    case FalloffType.Range:
                        EditorGUILayout.MinMaxSlider(rangeFalloffContent, ref rangeSettings.falloff.x, ref rangeSettings.falloff.y, 0f, 1f);
                        break;
                    case FalloffType.SplineArea:
                        splineAreaSettings.heightOffsetY = EditorGUILayout.FloatField(splineAreaHeightOffsetYContent, splineAreaSettings.heightOffsetY);
                        splineAreaSettings.clearFalloff = EditorGUILayout.FloatField(splineAreaClearFalloffContent, splineAreaSettings.clearFalloff);
                        splineAreaSettings.heightFalloff = EditorGUILayout.FloatField(splineAreaHeightFalloffContent, splineAreaSettings.heightFalloff);
                        splineAreaSettings.textureFalloff = EditorGUILayout.FloatField(splineAreaTextureFalloffContent, splineAreaSettings.textureFalloff);
                        break;
                }
            }
            EditorGUI.indentLevel--;


            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(convertToGroundObjectContent, GUILayout.Height(30)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(editor.GetEditorTarget().gameObject, "ReGenesis Convert to Ground Object");

                    ConvertToGroundObject();

                    MicroVerse.instance.Invalidate();

                }

                if (GUILayout.Button(convertToRegularObjectContent, GUILayout.Height(30)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(editor.GetEditorTarget().gameObject, "ReGenesis Convert to Regular Object");

                    ConvertToRegularObject();

                    MicroVerse.instance.Invalidate();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ConvertToGroundObject()
        {
#if __MICROVERSE_VEGETATION__
            GameObject targetGo = editor.GetEditorTarget().gameObject;

            if (targetGo == null)
                return;

            // save transform data
            Vector3 originalPosition = targetGo.transform.localPosition;
            Quaternion originalRotation = targetGo.transform.localRotation;
            Vector3 originalScale = targetGo.transform.localScale;

            GameObject parentGo = new GameObject(targetGo.name);

            // set new parent to same as target parent
            GameObjectUtils.SetParentAndAlign_KeepHierarchyLayers(parentGo, targetGo.transform.parent.gameObject);

            // set target parent to new parent
            GameObjectUtils.SetParentAndAlign_KeepHierarchyLayers(targetGo, parentGo);

            // spline area specific: whether spline area is used or not depends on whether a spline area exists or not
            bool useSplineArea = false;

#if __MICROVERSE_SPLINES__
            SplineArea splineArea = targetGo.GetComponent<SplineArea>();
            useSplineArea = falloffType == FalloffType.SplineArea && splineArea != null;
#endif

#if __MICROVERSE_SPLINES__

            // apply falloff to stamps which use the spline area
            List<FalloffFilter> falloffFilters = new List<FalloffFilter>();

            // texture stamps
            falloffFilters.AddRange(targetGo
                .GetComponentsInChildren<TextureStamp>()
                .Where(x => x.filterSet.falloffFilter.filterType == FalloffFilter.FilterType.SplineArea)
                .Where(x => x.filterSet.falloffFilter.splineArea == splineArea)
                .Select(x => x.filterSet.falloffFilter)
                );

            // falloff overrides
            falloffFilters.AddRange(targetGo
                .GetComponentsInChildren<FalloffOverride>()
                .Where(x => x.filter.filterType == FalloffFilter.FilterType.SplineArea)
                .Where(x => x.filter.splineArea == splineArea)
                .Select(x => x.filter)
                );

            foreach (FalloffFilter falloffFilter in falloffFilters)
            {
                falloffFilter.splineAreaFalloff = splineAreaSettings.textureFalloff;
            }
#endif

            #region clear stamp

            // occlusion and clear stamp
            ClearStamp clearStamp = null;
            OcclusionStamp occlusionStamp = null;
            FilterSet filterSet = null;

            switch (removalType)
            {
                case RemovalType.ClearStamp:

                    GameObject clearStampGo = new GameObject("Clear Stamp");
                    GameObjectUtility.SetParentAndAlign(clearStampGo, parentGo);

                    // clear stamp settings
                    clearStamp = clearStampGo.AddComponent<ClearStamp>();

                    clearStamp.clearDetails = true;
                    clearStamp.clearObjects = true;
                    clearStamp.clearTrees = true;

                    filterSet = clearStamp.filterSet;
                    break;

                case RemovalType.OcclusionStamp:

                    GameObject occlusionStampGo = new GameObject("Occlusion Stamp");
                    GameObjectUtility.SetParentAndAlign(occlusionStampGo, parentGo);

                    // occlusion stamp settings
                    occlusionStamp = occlusionStampGo.AddComponent<OcclusionStamp>();

                    occlusionStamp.occludeTreeWeight = 1f;
                    occlusionStamp.occludeDetailWeight = 1f;
                    occlusionStamp.occludeObjectWeight = 1f;

                    filterSet = occlusionStamp.filterSet;
                    break;

            }

            // falloff filter: range or spline area
            if( filterSet != null)
            {
                FalloffFilter falloffFilter = new FalloffFilter();

                if (useSplineArea)
                {
#if __MICROVERSE_SPLINES__
                    falloffFilter.filterType = FalloffFilter.FilterType.SplineArea;
                    falloffFilter.splineArea = splineArea;
                    falloffFilter.splineAreaFalloff = splineAreaSettings.clearFalloff;
#endif
                }
                else
                {
                    falloffFilter.filterType = FalloffFilter.FilterType.Range;
                }

                filterSet.falloffFilter = falloffFilter;
            }

            #endregion clear stamp

            #region height stamp

            // height stamp
            GameObject heightStampGo = new GameObject("Height Stamp");
            GameObjectUtility.SetParentAndAlign(heightStampGo, parentGo);

            // height stamp
            HeightStamp heightStamp = heightStampGo.AddComponent<HeightStamp>();

            heightStamp.mode = HeightStamp.CombineMode.Override;

            heightStamp.falloff = new FalloffFilter();
            switch (falloffType)
            {
                case FalloffType.SplineArea:
#if __MICROVERSE_SPLINES__
                    if (useSplineArea)
                    {
                        if (SplineUtils.GetSplineAreaBounds(GetEditorTargetTransform(), splineArea, out Bounds targetBounds))
                        {
                            heightStamp.falloff.filterType = FalloffFilter.FilterType.SplineArea;
                            heightStamp.falloff.splineArea = splineArea;
                            heightStamp.falloff.splineAreaFalloff = splineAreaSettings.heightFalloff;

                            heightStamp.transform.position = targetBounds.center;
                            heightStamp.transform.localScale = targetBounds.size + heightStamp.falloff.splineAreaFalloffBoost * 2f * Vector3.one;

                            heightStamp.transform.position = heightStamp.transform.position + splineAreaSettings.heightOffsetY * Vector3.up;

                        }
                    }
#endif
                    break;

                case FalloffType.Range:
                default:
                    heightStamp.falloff.filterType = FalloffFilter.FilterType.Range;
                    heightStamp.falloff.falloffRange = rangeSettings.falloff;
                    break;
            }

            #endregion height stamp

            // gameobject order
            // first height stamp then clear stamp => results in clear stamp first, height stamp 2nd
            heightStamp.transform.SetAsFirstSibling();
            switch (removalType)
            {
                case RemovalType.ClearStamp:
                    clearStamp.transform.SetAsFirstSibling();
                    break;

                case RemovalType.OcclusionStamp:
                    occlusionStamp.transform.SetAsFirstSibling();
                    break;
            }

            // set scale of parent
            parentGo.transform.localPosition = originalPosition;
            parentGo.transform.localRotation = originalRotation;
            parentGo.transform.localScale = originalScale;

            // reset transform of target
            targetGo.transform.localPosition = Vector3.zero;
            targetGo.transform.localRotation = Quaternion.identity;
            targetGo.transform.localScale = Vector3.one;

            // everything is as before ... now adjust the clear and falloff data
            if( BoundsUtils.CalculateBounds( targetGo, out Bounds bounds))
            {
                if (useBoundsPositionOffset)
                {
                    targetGo.transform.localRotation = Quaternion.identity;

                    // try adjusting to pivot that's not at center or not on ground
                    Vector3 worldPos = new Vector3(-bounds.center.x + originalPosition.x, 0, -bounds.center.z + originalPosition.z);
                    Vector3 localPos = parentGo.transform.InverseTransformVector(worldPos);

                    targetGo.transform.Translate(localPos);

                    // consider pivot relatedchanges for parent
                    parentGo.transform.Translate(-localPos);
                }

                // note about division by original scale:
                // the parent already got the scale, we need to consider that, otherwise the clear and height stamp scale would be too large

                switch (removalType)
                {
                    case RemovalType.ClearStamp:
                        {
                            float x = bounds.size.x * clearStampFactor / originalScale.x;
                            float y = 1f;
                            float z = bounds.size.z * clearStampFactor / originalScale.z;

                            clearStamp.transform.localScale = new Vector3(x, y, z);
                            clearStamp.transform.localRotation = originalRotation;
                        }
                        break;

                    case RemovalType.OcclusionStamp:
                        {
                            float x = bounds.size.x * occlusionStampFactor / originalScale.x;
                            float y = 1f;
                            float z = bounds.size.z * occlusionStampFactor / originalScale.z;

                            occlusionStamp.transform.localScale = new Vector3(x, y, z);
                            occlusionStamp.transform.localRotation = originalRotation;
                        }
                        break;
                }

                // height stamp
                {
                    Vector2 falloffRange = heightStamp.falloff.falloffRange;
                    float x = bounds.size.x * heightStampFactor / originalScale.x * ((falloffRange.y - falloffRange.x) + 1);
                    float y = 1f;
                    float z = bounds.size.z * heightStampFactor / originalScale.z * ((falloffRange.y - falloffRange.x) + 1);

                    heightStamp.transform.localScale = new Vector3(x, y, z);
                    heightStamp.transform.localRotation = originalRotation;
                }
            }


            // update selection
            Selection.activeObject = parentGo;

            // spline area specific
#if __MICROVERSE_SPLINES__
            if ( useSplineArea )
            {
                EditorUtility.SetDirty(heightStamp.transform);

                MicroVerse.instance.Invalidate();

                SceneView.lastActiveSceneView.Repaint();

                // center main handle on spline container
                // TODO: applied multiple times changes the parent transform; use "Fit & Center" of terraform for now
                // SplineUtils.CenterMainHandle(splineArea.spline);
        }
#endif
#endif
        }

        private void ConvertToRegularObject()
        {
#if __MICROVERSE_VEGETATION__
            GameObject targetGo = editor.GetEditorTarget().gameObject;

            if (targetGo == null)
                return;

            // remove clear and height stamps
            ClearStamp[] clearStamps = targetGo.transform.GetComponentsInChildren<ClearStamp>();
            OcclusionStamp[] occlusionStamps = targetGo.transform.GetComponentsInChildren<OcclusionStamp>();
            HeightStamp[] heightStamps = targetGo.transform.GetComponentsInChildren<HeightStamp>();
            
            // check if it's a converted object via heuristics
            bool isConvertedObject = (clearStamps.Length > 0 || occlusionStamps.Length > 0) && heightStamps.Length > 0;

            if( !isConvertedObject)
            {
                Debug.LogWarning("No clear and height stamps found. Aborting");
                return;
            }

            foreach (ClearStamp stamp in clearStamps)
            {
                Object.DestroyImmediate(stamp);
            }

            foreach (OcclusionStamp stamp in occlusionStamps)
            {
                Object.DestroyImmediate(stamp);
            }

            foreach (HeightStamp stamp in heightStamps)
            {
                Object.DestroyImmediate(stamp);
            }

            // remove empty gameobjects now that the stamps were removed
            DestroyEmptyGameObjects(targetGo);

            // run again in case the stamps were collected in another dedicated gameobject
            DestroyEmptyGameObjects(targetGo);

            // unparent if it's an empty aprent
            int parentComponentCount = targetGo.GetComponents<Component>().Length;
            if( parentComponentCount == 1)
            {
                List<GameObject> directChildren = new();

                for (int i = 0; i < targetGo.transform.childCount; i++)
                {
                    directChildren.Add(targetGo.transform.GetChild(i).gameObject);
                }

                // detach from parent, this places the children on the root
                targetGo.transform.DetachChildren();

                // reparent
                directChildren.ForEach(child => child.transform.parent = targetGo.transform.parent.transform);

                // destroy parent
                GameObject.DestroyImmediate(targetGo.gameObject);

                // update selection
                Selection.objects = directChildren.ToArray();

            }
#endif
        }

        private void DestroyEmptyGameObjects( GameObject targetGo)
        {
            Transform[] children = targetGo.GetComponentsInChildren<Transform>();

            foreach (Transform child in children)
            {
                if (child.childCount == 0)
                {
                    int componentCount = child.GetComponents<Component>().Length;

                    if (componentCount == 1) // 1 = the transform
                    {
                        GameObject.DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        private Transform GetEditorTargetTransform()
        {
            return editor.GetEditorTarget().gameObject.transform;
        }
    }
}