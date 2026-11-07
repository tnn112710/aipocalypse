using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JBooth.MicroVerseCore
{
    
    [ExecuteAlways]
    public class ClearMeshStamp : Stamp, ITreeModifier, IDetailModifier
#if __MICROVERSE_OBJECTS__
        , IObjectModifier
#endif
    {
        public bool clearTrees = true;
        public bool clearDetails = true;
#if __MICROVERSE_OBJECTS__
        public bool clearObjects = false;
#endif

        // Mask trees / details / objects against the top-down silhouette of all
        // scene renderers whose GameObject.layer matches this mask. A value of 0
        // disables the silhouette pass entirely and the stamp falls back to its
        // regular filter-set behaviour.
        public LayerMask objectLayerMask = 1; // Default layer

        // Temporarily disables all Terrain components while the silhouette is
        // rendered so the terrain's own surface doesn't fill the mask.
        public bool excludeTerrain = true;

        // Extra vertical margin above the terrain's max height when deciding
        // which renderers count as "on" the terrain — keeps backdrop / sky
        // geometry out of the mask.
        [Range(0f, 100f)]
        public float yMargin = 10f;

        // World-space tolerance for the per-fragment terrain-height clip in
        // the silhouette shader. A rock fragment is discarded only if it is
        // *below* the terrain surface by more than this distance, so:
        //   - 0      — strict, every fragment at-or-below terrain is cut
        //              (can flicker at the contact line on uneven ground);
        //   - 0.02   — ~2 cm slack, default; tolerates float / heightmap
        //              precision so a flush-resting rock keeps its full
        //              footprint;
        //   - larger — lets meshes "dig" a bit into the surface before any
        //              of their projection gets clipped. Useful for objects
        //              authored to sink slightly into the ground.
        // The clip itself is what stops half-buried rocks from painting
        // their full XZ footprint into the mask — only the part of the mesh
        // that actually sticks up out of the terrain contributes.
        [Range(0f, 1f)]
        public float submergeTolerance = 0.02f;

        // Resolution of the silhouette RT (square). 512² gives 4× less JFA
        // work and 4× less draw fill than 1024² with no visible quality loss
        // for typical terrain padding — the JFA produces sub-texel SDF
        // accuracy anyway. Bump back up if you have very small or very thin
        // objects whose silhouettes would alias at 512².
        const int kSilhouetteResolution = 512;

        public FilterSet filterSet = new FilterSet();

        Material material;

        // One silhouette per terrain, rebuilt each pass (Initialize releases).
        readonly Dictionary<Terrain, RenderTexture> silhouetteCache = new Dictionary<Terrain, RenderTexture>();

        // Per-bake scene-renderer cache. Filled lazily on the first
        // BuildSilhouette() call after Initialize() and held until the next
        // Initialize / Dispose / OnDestroy releases it (same lifecycle as
        // silhouetteCache — they're invalidated together).
        //
        // We partition every Renderer in the scene into two disjoint lists:
        //   - sceneLODPrimary:     one entry per LODGroup, namely its
        //                          highest-detail LOD (LOD0, falling through
        //                          to the next LOD if LOD0 is empty — e.g.
        //                          impostor-only groups). This is the single
        //                          canonical silhouette source per group.
        //   - sceneNonLODRenderers: every Renderer that isn't owned by any
        //                          LODGroup at all.
        //
        // Partitioning at scan time means BuildSilhouette has no per-iter
        // HashSet lookups in the hot path — just two flat list iterations.
        // Critically, this scan is independent of which terrain we're
        // rendering for, so a multi-tile bake pays the
        // FindObjectsByType + LODGroup walk only once instead of once per
        // tile.
        List<Renderer> sceneLODPrimary;
        List<Renderer> sceneNonLODRenderers;

        public bool NeedCurvatureMap() { return filterSet.NeedCurvatureMap(); }
        public bool NeedFlowMap() { return filterSet.NeedFlowMap(); }

        public bool NeedTreeClear() { return clearTrees; }
        public bool NeedDetailClear() { return clearDetails; }
#if __MICROVERSE_OBJECTS__
        public bool NeedObjectClear() { return clearObjects; }
#endif

        public override FilterSet GetFilterSet()
        {
            return filterSet;
        }

        public override Bounds GetBounds()
        {
            FalloffOverride fo = GetComponentInParent<FalloffOverride>();
            var foType = filterSet.falloffFilter.filterType;
            var filter = filterSet.falloffFilter;
            if (fo != null && fo.enabled)
            {
                foType = fo.filter.filterType;
                filter = fo.filter;
            }
#if __MICROVERSE_SPLINES__
            if (foType == FalloffFilter.FilterType.SplineArea && filterSet.falloffFilter.splineArea != null)
            {
                return filterSet.falloffFilter.splineArea.GetBounds();
            }
#endif

            if (foType == FalloffFilter.FilterType.Global && filter != null && filter.paintArea != null && filter.paintArea.clampOutsideOfBounds)
            {
                return filter.paintArea.GetBounds();
            }

            if (foType == FalloffFilter.FilterType.Global)
                return new Bounds(Vector3.zero, new Vector3(99999, 999999, 99999));
            else
            {
                return TerrainUtil.GetBounds(transform);
            }
        }

        public bool OccludesOthers()
        {
            return false;
        }

        public bool NeedSDF()
        {
            return false;
        }

        public bool UsesOtherTreeSDF() { return false; }
        public bool UsesOtherObjectSDF() { return false; }
        public bool NeedParentSDF() { return false; }
        public bool NeedToGenerateSDFForChilden() { return false; }
        public void SetSDF(Terrain t, RenderTexture rt) { }
        public RenderTexture GetSDF(Terrain t) { return null; }

        static Shader clearShader = null;
        static Shader silhouetteShader = null;

        // Cached material for the silhouette pass — created lazily on first
        // bake and kept for the lifetime of the process. Avoids the per-bake
        // Material allocation that the previous local instance required.
        static Material silhouetteMat = null;

        public void Initialize()
        {
            if (clearShader == null)
            {
                clearShader = Shader.Find("Hidden/MicroVerse/ClearMeshFilter");
            }
            if (silhouetteShader == null)
            {
                silhouetteShader = Shader.Find("Hidden/MicroVerse/ObjectSilhouette");
            }
            if (material == null)
            {
                material = new Material(clearShader);
            }
            keywordBuilder.ClearInitial();
            filterSet.PrepareMaterial(this.transform, material, keywordBuilder.initialKeywords);

            ReleaseSilhouetteCache();
        }

        public void InqTreePrototypes(List<TreePrototypeSerializable> trees)
        {
            
        }

        static int _Heightmap = Shader.PropertyToID("_Heightmap");
        static int _Normalmap = Shader.PropertyToID("_Normalmap");
        static int _Curvemap = Shader.PropertyToID("_Curvemap");
        static int _Flowmap = Shader.PropertyToID("_Flowmap");
        static int _IndexMap = Shader.PropertyToID("_IndexMap");
        static int _WeightMap = Shader.PropertyToID("_WeightMap");
        static int _ObjectLayerMask = Shader.PropertyToID("_ObjectLayerMask");
        static int _ObjectLayerMaskParams = Shader.PropertyToID("_ObjectLayerMaskParams");

        // Per-category padding (metres) added around each object's footprint
        // and the smooth-falloff width at the outer edge. The clear shader
        // thresholds the silhouette SDF against these values — values up to
        // `padding - falloff` from the object surface are fully cleared, then
        // smoothly fade to no-clear over `falloff` metres.
        //
        // Splitting per-category lets trees keep a wider buffer than grass.

        [Range(0f, 16f)] public float treePadding = 0f;
        [Range(0f, 16f)] public float treeFalloff = 0f;

        [Range(0f, 16f)] public float detailPadding = 0f;
        [Range(0f, 16f)] public float detailFalloff = 0f;

        [Range(0f, 16f)] public float objectPadding = 0f;
        [Range(0f, 16f)] public float objectFalloff = 0f;

        // Binds the layer-mask SDF texture on the material plus the
        // padding/falloff params (in texel units, converted from the user's
        // metre inputs). Padding and falloff are passed in per-call so
        // ApplyTreeClear / ApplyDetailClear / ApplyObjectClear can each use
        // their own category-specific values.
        void ApplyObjectLayerMaskToMaterial(Terrain terrain, float paddingMeters, float falloffMeters)
        {
            if (objectLayerMask.value == 0)
            {
                material.SetTexture(_ObjectLayerMask, Texture2D.whiteTexture);
                // padding huge → everything reads as inside the mask → no gate
                material.SetVector(_ObjectLayerMaskParams, new Vector4(1e9f, 1f, 0f, 0f));
                return;
            }

            var sil = EnsureSilhouette(terrain);
            if (sil == null)
            {
                material.SetTexture(_ObjectLayerMask, Texture2D.whiteTexture);
                material.SetVector(_ObjectLayerMaskParams, new Vector4(1e9f, 1f, 0f, 0f));
                return;
            }

            // Convert metre values to mask-texel values. Mask resolution is
            // kSilhouetteResolution; terrain.size.x is the world width.
            float metersPerTexel = Mathf.Max(terrain.terrainData.size.x / kSilhouetteResolution, 0.0001f);
            float paddingTexels = paddingMeters / metersPerTexel;
            float falloffTexels = falloffMeters / metersPerTexel;

            material.SetTexture(_ObjectLayerMask, sil);
            material.SetVector(_ObjectLayerMaskParams, new Vector4(paddingTexels, falloffTexels, 0f, 0f));
        }

        public void ApplyTreeClear(TreeData td)
        {
            if (!clearTrees)
                return;

            keywordBuilder.Clear();
            keywordBuilder.Add("_RECONSTRUCTNORMAL");
            var textureLayerWeights = filterSet.GetTextureWeights(td.terrain.terrainData.terrainLayers);
            material.SetVectorArray("_TextureLayerWeights", textureLayerWeights);
            material.SetTexture(_Heightmap, td.heightMap);
            material.SetTexture(_Normalmap, td.normalMap);
            material.SetTexture(_Curvemap, td.curveMap);
            material.SetTexture(_Flowmap, td.flowMap);
            filterSet.PrepareTransform(this.transform, td.terrain, material, keywordBuilder.keywords, GetTerrainScalingFactor(td.terrain));

            keywordBuilder.Assign(material);

            // Bind the silhouette + keyword AFTER Assign so they aren't
            // overwritten by the keyword commit.
            ApplyObjectLayerMaskToMaterial(td.terrain, treePadding, treeFalloff);

            RenderTexture temp = RenderTexture.GetTemporary(td.treeClearMap.descriptor);
            material.SetFloat("_LayerIndex", td.layerIndex);
            material.SetTexture(_IndexMap, td.dataCache.indexMaps[td.terrain]);
            material.SetTexture(_WeightMap, td.dataCache.weightMaps[td.terrain]);
            temp.name = "TreeClear";
            Graphics.Blit(td.treeClearMap, temp, material);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(td.treeClearMap);
            td.treeClearMap = temp;
            td.layerIndex++;
        }

        public void ApplyDetailClear(DetailData dd)
        {
            if (!clearDetails)
                return;

            keywordBuilder.Clear();
            keywordBuilder.Add("_RECONSTRUCTNORMAL");
            var textureLayerWeights = filterSet.GetTextureWeights(dd.terrain.terrainData.terrainLayers);
            material.SetVectorArray("_TextureLayerWeights", textureLayerWeights);
            material.SetTexture(_Heightmap, dd.heightMap);
            material.SetTexture(_Normalmap, dd.normalMap);
            material.SetTexture(_Curvemap, dd.curveMap);
            material.SetTexture(_Flowmap, dd.flowMap);
            filterSet.PrepareTransform(this.transform, dd.terrain, material, keywordBuilder.keywords, GetTerrainScalingFactor(dd.terrain));

            keywordBuilder.Assign(material);

            ApplyObjectLayerMaskToMaterial(dd.terrain, detailPadding, detailFalloff);

            RenderTexture temp = RenderTexture.GetTemporary(dd.clearMap.descriptor);
            material.SetFloat("_LayerIndex", dd.layerIndex);
            material.SetTexture(_IndexMap, dd.dataCache.indexMaps[dd.terrain]);
            material.SetTexture(_WeightMap, dd.dataCache.weightMaps[dd.terrain]);
            temp.name = "DetailClear";
            Graphics.Blit(dd.clearMap, temp, material);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(dd.clearMap);
            dd.clearMap = temp;
            dd.layerIndex++;
        }

        public void ApplyTreeStamp(TreeData td, Dictionary<Terrain, List<TreeJobHolder>> jobs, OcclusionData od)
        {
            if (clearTrees)
               td.layerIndex++;
        }

        
        public void ProcessTreeStamp(TreeData vd, Dictionary<Terrain, List<TreeJobHolder>> jobs, OcclusionData od)
        {
            
        }

        public void Dispose()
        {
            ReleaseSilhouetteCache();
        }

        protected override void OnDestroy()
        {
            if (material != null) DestroyImmediate(material);
            ReleaseSilhouetteCache();
            base.OnDestroy();
        }


        void OnDrawGizmosSelected()
        {
            if (filterSet.falloffFilter.filterType != FalloffFilter.FilterType.Global &&
                filterSet.falloffFilter.filterType != FalloffFilter.FilterType.SplineArea)
            {
                if (MicroVerse.instance != null)
                {
                    Gizmos.color = MicroVerse.instance.options.colors.treeStampColor;
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(new Vector3(0, 0.5f, 0), Vector3.one);
                }
            }
        }


        public void ApplyDetailStamp(DetailData dd, Dictionary<Terrain, Dictionary<int, List<RenderTexture>>> resultBuffers, OcclusionData od)
        {
            if (!clearDetails)
                return;
            dd.layerIndex++;
        }

        public void InqDetailPrototypes(List<DetailPrototypeSerializable> prototypes)
        {
            
        }

#if __MICROVERSE_OBJECTS__
        public void ApplyObjectClear(ObjectData td)
        {
            if (!clearObjects)
                return;
            keywordBuilder.Clear();
            keywordBuilder.Add("_RECONSTRUCTNORMAL");
            var textureLayerWeights = filterSet.GetTextureWeights(td.terrain.terrainData.terrainLayers);
            material.SetVectorArray("_TextureLayerWeights", textureLayerWeights);
            material.SetTexture(_Heightmap, td.heightMap);
            material.SetTexture(_Normalmap, td.normalMap);
            material.SetTexture(_Curvemap, td.curveMap);
            material.SetTexture(_Flowmap, td.flowMap);
            material.SetTexture(_IndexMap, td.indexMap);
            material.SetTexture(_WeightMap, td.weightMap);
            filterSet.PrepareTransform(this.transform, td.terrain, material, keywordBuilder.keywords, GetTerrainScalingFactor(td.terrain));

            keywordBuilder.Assign(material);

            ApplyObjectLayerMaskToMaterial(td.terrain, objectPadding, objectFalloff);

            RenderTexture temp = RenderTexture.GetTemporary(td.clearMap.descriptor);
            material.SetFloat("_LayerIndex", td.layerIndex);
            temp.name = "ObjectClear";
            Graphics.Blit(td.clearMap, temp, material);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(td.clearMap);
            td.clearMap = temp;
            td.layerIndex++;
        }

        public void ApplyObjectStamp(ObjectData vd, Dictionary<Terrain, List<ObjectJobHolder>> jobs, OcclusionData od)
        {
            if (!clearObjects)
                return;
            vd.layerIndex++;
        }

        public void ProcessObjectStamp(ObjectData vd, Dictionary<Terrain, List<ObjectJobHolder>> jobs, OcclusionData od)
        {
            
        }

#endif // MV Objects


        // ----- Object-layer silhouette --------------------------------------
        //
        // Top-down ortho capture of all scene renderers whose layer is in
        // objectLayerMask. UV space of the resulting RT matches terrain UV
        // (origin = SW corner, +U = east, +V = north) so the ClearMeshFilter
        // shader samples it directly with the heightmap UV — no flip needed.

        // Public entry point for the editor preview button. Builds a fresh
        // silhouette outside the cache; caller is responsible for releasing.
        public RenderTexture BuildSilhouetteForPreview(Terrain terrain)
        {
            if (silhouetteShader == null)
                silhouetteShader = Shader.Find("Hidden/MicroVerse/ObjectSilhouette");
            return BuildSilhouette(terrain);
        }

        RenderTexture EnsureSilhouette(Terrain terrain)
        {
            if (terrain == null)
                return null;

            if (silhouetteCache.TryGetValue(terrain, out var existing) && existing != null)
                return existing;

            var rt = BuildSilhouette(terrain);
            silhouetteCache[terrain] = rt;
            return rt;
        }

        RenderTexture BuildSilhouette(Terrain terrain)
        {
            if (silhouetteShader == null)
            {
                Debug.LogWarning("[ClearMeshStamp] Shader 'Hidden/MicroVerse/ObjectSilhouette' not found — object layer mask disabled.");
                return null;
            }

            int layerBits = objectLayerMask.value;
            if (layerBits == 0)
                return null;

            Vector3 tOrigin = terrain.GetPosition();
            Vector3 tSize   = terrain.terrainData.size;
            if (tSize.x <= 0f || tSize.z <= 0f)
                return null;

            int res = kSilhouetteResolution;

            // Lazily initialise the silhouette material once and keep it
            // around for the lifetime of the process. Avoids the per-bake
            // allocation + DestroyImmediate that previously happened in the
            // try/finally below.
            if (silhouetteMat == null)
                silhouetteMat = new Material(silhouetteShader) { hideFlags = HideFlags.HideAndDontSave };

            RenderTexture rt                 = null;
            CommandBuffer cmd                = null;
            Terrain[]     terrains           = null;
            bool[]        terrainPrevEnabled = null;
            RenderTexture prevActive         = RenderTexture.active;

            try
            {
                // Binary silhouette — single 8-bit channel is enough (JFA only
                // reads channel 0). 4× less memory + bandwidth than ARGB32.
                // The 16-bit depth buffer is still needed because the
                // silhouette shader uses ZWrite/ZTest to handle overlapping
                // geometry without overdraw artefacts.
                rt = new RenderTexture(res, res, 16, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
                {
                    name = "ClearMeshStamp_ObjectSilhouette",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                rt.Create();

                if (excludeTerrain)
                {
#if UNITY_2023_1_OR_NEWER
                    terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
#else
                    terrains = Object.FindObjectsOfType<Terrain>();
#endif
                    terrainPrevEnabled = new bool[terrains.Length];
                    for (int i = 0; i < terrains.Length; i++)
                    {
                        terrainPrevEnabled[i] = terrains[i].enabled;
                        terrains[i].enabled = false;
                    }
                }

                float yMarginClamped = Mathf.Max(0f, yMargin);
                float camY = tOrigin.y + tSize.y + yMarginClamped + 100f;
                Vector3 camPos = new Vector3(
                    tOrigin.x + tSize.x * 0.5f,
                    camY,
                    tOrigin.z + tSize.z * 0.5f);
                Quaternion camRot = Quaternion.Euler(90f, 0f, 0f);

                Matrix4x4 worldToCamera =
                    Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                    Matrix4x4.TRS(camPos, camRot, Vector3.one).inverse;

                Matrix4x4 projGL = Matrix4x4.Ortho(
                    -tSize.x * 0.5f, tSize.x * 0.5f,
                    -tSize.z * 0.5f, tSize.z * 0.5f,
                    1f, camY - (tOrigin.y - 100f));

                Matrix4x4 projGPU = GL.GetGPUProjectionMatrix(projGL, true);

                cmd = new CommandBuffer { name = "ClearMeshStamp_ObjectSilhouette" };
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.SetViewProjectionMatrices(worldToCamera, projGPU);

                // Bind the active terrain's heightmap + bounds onto the
                // silhouette material so the fragment shader can clip away
                // the buried part of each mesh. The shader's _TexelSize
                // companion is auto-populated by Unity. We set this on the
                // material directly (not via MaterialPropertyBlock) because
                // BuildSilhouette runs serially per terrain and the cmd
                // buffer is executed immediately below; no risk of stale
                // state leaking across terrains.
                Texture heightmapTex = terrain.terrainData != null
                    ? terrain.terrainData.heightmapTexture
                    : null;
                if (heightmapTex != null)
                {
                    silhouetteMat.SetTexture("_MicroVerseHM", heightmapTex);
                    silhouetteMat.SetVector(
                        "_MicroVerseHMOriginSize",
                        new Vector4(tOrigin.x, tOrigin.z, tSize.x, tSize.z));
                    silhouetteMat.SetVector(
                        "_MicroVerseHMHeight",
                        new Vector4(tOrigin.y, tSize.y, 0f, 0f));
                    silhouetteMat.SetFloat("_MicroVerseHMEpsilon", submergeTolerance);
                    silhouetteMat.SetFloat("_MicroVerseHMEnabled", 1f);
                }
                else
                {
                    // No heightmap available — fall back to the original
                    // "draw every fragment white" behaviour rather than
                    // sampling garbage. Half-buried rocks will go back to
                    // painting their full XZ footprint, but at least the
                    // mask doesn't get corrupted by a stale texture.
                    silhouetteMat.SetFloat("_MicroVerseHMEnabled", 0f);
                }

                // Build (or reuse) the per-bake scene-renderer partition.
                EnsureSceneRendererCache();

                // The cache is partitioned into:
                //   - sceneLODPrimary:     highest-detail LOD per LODGroup
                //                          (LOD0, falling through to the first
                //                          non-empty LOD for impostor-only
                //                          groups).
                //   - sceneNonLODRenderers: every renderer not owned by any
                //                          LODGroup.
                // Together these exhaustively cover the scene with no double-
                // counting and no per-iter HashSet lookups in the hot path.
                // Built once per bake (Initialize releases the cache), so a
                // multi-tile world iterates the partition N times but only
                // pays the FindObjectsByType + LODGroup walk once.
                // For background on *why* we partition this way, see the
                // EnsureSceneRendererCache implementation below.

                float tMinY = tOrigin.y;
                float tMaxY = tOrigin.y + tSize.y + yMarginClamped;

                // (a) Canonical LOD-per-group set.
                List<Renderer> primary = sceneLODPrimary;
                for (int i = 0; i < primary.Count; i++)
                    DrawRendererToSilhouette(cmd, primary[i], silhouetteMat, layerBits, tMinY, tMaxY);

                // (b) Everything else — non-LODGroup geometry only.
                List<Renderer> nonLOD = sceneNonLODRenderers;
                for (int i = 0; i < nonLOD.Count; i++)
                    DrawRendererToSilhouette(cmd, nonLOD[i], silhouetteMat, layerBits, tMinY, tMaxY);

                Graphics.ExecuteCommandBuffer(cmd);

                // Convert the binary silhouette into a signed distance field
                // via MicroVerse's JumpFloodSDF. The clear shader thresholds
                // it to produce dilated / smooth-falloff masks at arbitrary
                // radii without a separate dilation pass per padding value.
                // CreateTemporaryRT returns a temp RT; we Blit it into our
                // owned `rt` so cleanup is uniform (Release + DestroyImmediate).
                RenderTexture sdfTemp = JumpFloodSDF.CreateTemporaryRT(rt, channel: 0);
                if (sdfTemp != null)
                {
                    // Reallocate `rt` to match the SDF format (RHalf, 16-bit float).
                    rt.Release();
                    DestroyImmediate(rt);

                    rt = new RenderTexture(sdfTemp.descriptor)
                    {
                        name = "ClearMeshStamp_ObjectLayerSDF",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    rt.Create();
                    Graphics.Blit(sdfTemp, rt);
                    RenderTexture.ReleaseTemporary(sdfTemp);
                }
                else
                {
                    Debug.LogWarning("[ClearMeshStamp] JumpFloodSDF returned null — falling back to raw binary silhouette.", this);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ClearMeshStamp] Failed to build object silhouette: {ex.Message}");
                if (rt != null)
                {
                    rt.Release();
                    DestroyImmediate(rt);
                    rt = null;
                }
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (cmd != null) cmd.Dispose();

                if (terrains != null)
                {
                    for (int i = 0; i < terrains.Length; i++)
                    {
                        if (terrains[i] != null) terrains[i].enabled = terrainPrevEnabled[i];
                    }
                }
            }

            return rt;
        }

        // Shared per-renderer logic for BuildSilhouette. Applies layer, enabled,
        // and vertical-bounds gating, then issues one DrawRenderer per submesh
        // (so the silhouette material covers every material slot — Unity skips
        // submesh indices that don't exist on the source mesh).
        //
        // `Renderer.enabled` is honored here because LODGroup does not toggle
        // that flag for distance culling — it uses an internal visibility mask
        // — so an `enabled == false` on a renderer means the user explicitly
        // disabled it and we should respect that. This is also why iterating
        // LODGroup.GetLODs() (rather than relying on the flag) is what de-dupes
        // LOD levels above; see BuildSilhouette for the rationale.
        static void DrawRendererToSilhouette(
            CommandBuffer cmd,
            Renderer r,
            Material silhouetteMat,
            int layerBits,
            float tMinY,
            float tMaxY)
        {
            if (r == null) return;
            if (!r.enabled) return;
            if (((1 << r.gameObject.layer) & layerBits) == 0) return;

            Bounds b = r.bounds;
            if (b.max.y < tMinY || b.min.y > tMaxY) return;

            int subCount = 1;
            Mesh mesh = null;
            if (r is MeshRenderer meshR)
            {
                MeshFilter mf = meshR.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
            }
            if (mesh != null) subCount = mesh.subMeshCount;

            for (int s = 0; s < subCount; s++)
                cmd.DrawRenderer(r, silhouetteMat, s);
        }

        // Builds the per-bake scene-renderer partition on first use. Subsequent
        // calls (e.g. from BuildSilhouette for additional terrain tiles) are
        // no-ops, so a multi-tile bake pays the FindObjectsByType + LODGroup
        // walk once. ReleaseSilhouetteCache nulls these lists, so the next
        // Initialize() forces a fresh scan.
        void EnsureSceneRendererCache()
        {
            if (sceneNonLODRenderers != null) return;

#if UNITY_2023_1_OR_NEWER
            LODGroup[] lodGroups = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
            LODGroup[] lodGroups = Object.FindObjectsOfType<LODGroup>();
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
#endif

            // (1) Walk every LODGroup once.
            //   - lodMembers: every renderer owned by any LOD level of any
            //     group. Used during the partition step below to detect
            //     "renderer is LODGroup-managed".
            //   - primary: the renderers we'll actually draw for each group —
            //     namely its highest-detail LOD that contains at least one
            //     non-null renderer. Almost always LOD0; the fallback covers
            //     groups whose LOD0 slot is empty (impostor-only authoring).
            //
            // Building the skip-set is what dedupes LOD levels: LODGroup
            // doesn't toggle Renderer.enabled when it culls a level (it uses
            // an internal visibility mask), so an enabled-flag filter alone
            // wouldn't catch inactive LODs and a naive scan would draw the
            // same tree 4-5× — see the prior bug.
            var lodMembers = new HashSet<Renderer>();
            var primary = new List<Renderer>(lodGroups.Length);

            for (int g = 0; g < lodGroups.Length; g++)
            {
                LODGroup grp = lodGroups[g];
                if (grp == null) continue;
                LOD[] lods = grp.GetLODs();
                if (lods == null || lods.Length == 0) continue;

                for (int l = 0; l < lods.Length; l++)
                {
                    Renderer[] lr = lods[l].renderers;
                    if (lr == null) continue;
                    for (int k = 0; k < lr.Length; k++)
                    {
                        if (lr[k] != null) lodMembers.Add(lr[k]);
                    }
                }

                int chosen = -1;
                for (int l = 0; l < lods.Length && chosen < 0; l++)
                {
                    Renderer[] lr = lods[l].renderers;
                    if (lr == null) continue;
                    for (int k = 0; k < lr.Length; k++)
                    {
                        if (lr[k] != null) { chosen = l; break; }
                    }
                }
                if (chosen < 0) continue;

                Renderer[] chosenRenderers = lods[chosen].renderers;
                for (int k = 0; k < chosenRenderers.Length; k++)
                {
                    if (chosenRenderers[k] != null)
                        primary.Add(chosenRenderers[k]);
                }
            }

            // (2) Partition every Renderer in the scene into "non-LOD" by
            // subtracting the skip-set. The result is disjoint from `primary`,
            // so BuildSilhouette can iterate both lists back-to-back without
            // double-counting and without per-iter HashSet lookups.
            var nonLOD = new List<Renderer>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (lodMembers.Contains(r)) continue;
                nonLOD.Add(r);
            }

            // Publish last — if anything above threw, the cache stays null
            // and a future call retries cleanly.
            sceneLODPrimary = primary;
            sceneNonLODRenderers = nonLOD;
        }

        void ReleaseSilhouetteCache()
        {
            // Invalidate the per-bake scene-renderer partition first. We
            // always do this even if silhouetteCache is empty — Initialize()
            // calls Release before any silhouette has been built and we
            // still want a fresh scan next bake.
            sceneLODPrimary = null;
            sceneNonLODRenderers = null;

            if (silhouetteCache.Count == 0)
                return;

            foreach (var kvp in silhouetteCache)
            {
                var rt = kvp.Value;
                if (rt == null) continue;
                rt.Release();
                DestroyImmediate(rt);
            }
            silhouetteCache.Clear();
        }
    }
}
