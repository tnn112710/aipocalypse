using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;

public static class SceneScan
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        sb.AppendLine($"RP: {rp?.name} renderScale={rp?.renderScale} msaa={rp?.msaaSampleCount} hdr={rp?.supportsHDR} shadowDist={rp?.shadowDistance} cascades={rp?.shadowCascadeCount} mainShadowRes={rp?.mainLightShadowmapResolution} addLightsMode={rp?.additionalLightsRenderingMode} addShadows={rp?.supportsAdditionalLightShadows} addShadowRes={rp?.additionalLightsShadowmapResolution} softShadows={rp?.supportsSoftShadows} depthTex={rp?.supportsCameraDepthTexture} opaqueTex={rp?.supportsCameraOpaqueTexture} srpBatcher={rp?.useSRPBatcher} upscale={rp?.upscalingFilter} gpuResidentDrawer={rp?.gpuResidentDrawerMode}");
        if (rp != null)
        {
            var so = new SerializedObject(rp);
            var list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                var rd = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                if (rd == null) continue;
                sb.AppendLine($" Renderer[{i}] {rd.name} ({rd.GetType().Name})" + (rd is UniversalRendererData urd ? $" path={urd.renderingMode} depthPrimingMode={urd.depthPrimingMode} copyDepth={urd.copyDepthMode}" : ""));
                foreach (var f in rd.rendererFeatures)
                    if (f != null) sb.AppendLine($"   feature {f.name} ({f.GetType().Name}) active={f.isActive}");
            }
        }
        sb.AppendLine($"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]} vsync={QualitySettings.vSyncCount} lodBias={QualitySettings.lodBias} maxLOD={QualitySettings.maximumLODLevel} targetFR={Application.targetFrameRate} pixelLights={QualitySettings.pixelLightCount} realtimeGI={Lightmapping.realtimeGI} terrainQualityOverrides={QualitySettings.terrainQualityOverrides}");

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            var ad = cam.GetUniversalAdditionalCameraData();
            sb.AppendLine($"Camera {Path(cam.transform)} enabled={cam.isActiveAndEnabled} far={cam.farClipPlane} near={cam.nearClipPlane} type={ad?.renderType} pp={ad?.renderPostProcessing} aa={ad?.antialiasing} shadows={ad?.renderShadows} depth={ad?.requiresDepthTexture} stack={ad?.cameraStack?.Count} occl={cam.useOcclusionCulling} rt={cam.targetTexture} culling={cam.cullingMask} layerCull=[{string.Join(",", cam.layerCullDistances.Select((d, i) => d > 0 ? i + ":" + d : null).Where(s => s != null))}]");
        }

        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        sb.AppendLine($"Lights active: {lights.Length}");
        foreach (var l in lights.Take(30)) sb.AppendLine($"  {Path(l.transform)} {l.type} {l.lightmapBakeType} shadows={l.shadows} range={l.range} intensity={l.intensity}");

        var rends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        int shadowCasters = rends.Count(r => r.enabled && r.shadowCastingMode != ShadowCastingMode.Off);
        int skinned = rends.Count(r => r is SkinnedMeshRenderer && r.enabled);
        int particles = rends.Count(r => r is ParticleSystemRenderer && r.enabled);
        long tris = 0;
        foreach (var r in rends)
            if (r is MeshRenderer && r.enabled && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null && mf.sharedMesh.isReadable) tris += mf.sharedMesh.triangles.LongLength / 3;
        sb.AppendLine($"Renderers active: {rends.Length} (enabled {rends.Count(r => r.enabled)}), shadowCasters={shadowCasters} skinned={skinned} particles={particles} readableMeshTris~{tris}");
        sb.AppendLine($"LODGroups: {Object.FindObjectsByType<LODGroup>().Length} Colliders: {Object.FindObjectsByType<Collider>().Length} MeshColliders: {Object.FindObjectsByType<MeshCollider>().Length} Rigidbodies: {Object.FindObjectsByType<Rigidbody>().Length} ReflectionProbes: {Object.FindObjectsByType<ReflectionProbe>().Length} Animators: {Object.FindObjectsByType<Animator>().Length} AudioSources: {Object.FindObjectsByType<AudioSource>().Length}");
        foreach (var rpb in Object.FindObjectsByType<ReflectionProbe>())
            sb.AppendLine($"  ReflProbe {Path(rpb.transform)} mode={rpb.mode} refresh={rpb.refreshMode} slicing={rpb.timeSlicingMode} res={rpb.resolution}");

        // Top materials/shaders by renderer count
        var byShader = rends.Where(r => r.enabled).SelectMany(r => r.sharedMaterials).Where(m => m != null && m.shader != null).GroupBy(m => m.shader.name).OrderByDescending(g => g.Count()).Take(15);
        sb.AppendLine("Top shaders (material slots):");
        foreach (var g in byShader) sb.AppendLine($"  {g.Count()}  {g.Key}  distinctMats={g.Distinct().Count()}");

        foreach (var t in Object.FindObjectsByType<Terrain>())
        {
            var td = t.terrainData;
            sb.AppendLine($"Terrain {Path(t.transform)} size={td.size} heightRes={td.heightmapResolution} alphaRes={td.alphamapResolution} layers={td.terrainLayers.Length} pixelError={t.heightmapPixelError} basemapDist={t.basemapDistance} instanced={t.drawInstanced} shadows={t.shadowCastingMode} drawTrees={t.drawTreesAndFoliage} detailDist={t.detailObjectDistance} detailDensity={t.detailObjectDensity} treeDist={t.treeDistance} billboard={t.treeBillboardDistance} detailProtos={td.detailPrototypes.Length} treeInst={td.treeInstanceCount} mat={t.materialTemplate?.shader?.name}");
        }

        foreach (var v in Object.FindObjectsByType<Volume>())
        {
            var p = v.sharedProfile;
            sb.AppendLine($"Volume {Path(v.transform)} global={v.isGlobal} weight={v.weight} profile={p?.name}: " + (p == null ? "" : string.Join(", ", p.components.Where(c => c.active).Select(c => c.GetType().Name))));
        }

        // MonoBehaviours by type count (find per-frame heavy hitters)
        var mbs = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
        sb.AppendLine($"MonoBehaviours active: {mbs.Length}. Top types:");
        foreach (var g in mbs.Where(m => m != null).GroupBy(m => m.GetType().FullName).OrderByDescending(g => g.Count()).Take(25))
            sb.AppendLine($"  {g.Count()}  {g.Key}");

        // Root objects
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            sb.AppendLine($"Root {go.name} active={go.activeSelf} childRenderers={go.GetComponentsInChildren<Renderer>(false).Length} comps={string.Join(",", go.GetComponents<Component>().Select(c => c?.GetType().Name))}");
        return sb.ToString();
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
