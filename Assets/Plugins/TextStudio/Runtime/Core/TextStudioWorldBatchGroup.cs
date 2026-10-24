using System;
using ImpossibleRobert.Common;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio
{
    /// <summary>
    /// Opt-in world batching for one atomic text assembly. Every enabled descendant world-space Text Studio text draws its
    /// complete Appearance in painter order through shared mesh shards, ordered by source sorting layer, sorting order and
    /// hierarchy order, so contiguous primitives that share a material across texts become one draw. The required
    /// <see cref="SortingGroup"/> sorts the assembly as one unit against the rest of the scene. Foreign renderers, nested
    /// sorting groups and members that cannot share the group's space keep every member on its own output, and
    /// <see cref="IneligibleReason"/> reports why. Default world text without this component stays independently sortable.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SortingGroup)), DefaultExecutionOrder(10010)]
    [AddComponentMenu("Text Studio/Appearance/World Batch Group")]
    public sealed class TextStudioWorldBatchGroup : MonoBehaviour
    {
        // UInt16 indices keep shards bounded and portable; a single larger member gets a UInt32 shard of its own.
        const int MaximumShardVertices = ushort.MaxValue;
        const string ShardName = "_TSWorldBatch";
        const string OrderedOutputName = "_TSOrderedOutput";
        const string HeroOutputName = "_TSHeroOutput";
        // Foreign renderers can appear deep in the subtree without a membership event; recheck at this interval.
        const int EligibilityIntervalFrames = 30;

        readonly List<TextStudioText> _members = new List<TextStudioText>();
        readonly List<MemberState> _states = new List<MemberState>();
        readonly List<Shard> _shards = new List<Shard>();
        readonly List<TextStudioText> _scan = new List<TextStudioText>();
        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly List<SortingGroup> _sortingGroups = new List<SortingGroup>();
        readonly ShardBuilder _builder = new ShardBuilder();
        SortingGroup _sortingGroup;
        bool _membershipDirty = true;
        bool _geometryDirty = true;
        bool _publishing;
        int _nextEligibilityFrame;

        /// <summary>True while the group draws its members through shared shards.</summary>
        public bool IsMerging { get; private set; }

        /// <summary>Why the members keep their own outputs; empty while merging.</summary>
        public string IneligibleReason { get; private set; } = string.Empty;

        /// <summary>Enabled world texts collected by this group.</summary>
        public int MemberCount => _members.Count;

        /// <summary>Shard renderers drawing the members. Render state, a new material run or shard capacity starts another.</summary>
        public int ShardCount { get; private set; }

        /// <summary>Draw runs across all shards; contiguous primitives that share a material and render state form one.</summary>
        public int DrawCount { get; private set; }

        /// <summary>Shard rebuilds since this group was enabled; unchanged members never rebuild.</summary>
        public int RebuildCount { get; private set; }

        internal void MarkMembershipDirty() => _membershipDirty = true;

        void OnEnable()
        {
            _sortingGroup = GetComponent<SortingGroup>();
            _membershipDirty = true;
            _geometryDirty = true;
            RebuildCount = 0;
            Refresh();
        }

        void OnDisable() => Release();

        void OnTransformChildrenChanged()
        {
            if (!_publishing) _membershipDirty = true;
        }

        void LateUpdate() => Refresh();

        /// <summary>Collects members, evaluates eligibility and rebuilds shards whose members changed.</summary>
        internal void Refresh()
        {
            if (!isActiveAndEnabled) return;
            bool membership = _membershipDirty;
            if (membership) CollectMembers();
            bool changed = membership || _geometryDirty || MembersChanged();
            bool merging = IsMerging;
            if (changed || !Application.isPlaying || Time.frameCount >= _nextEligibilityFrame)
            {
                _nextEligibilityFrame = Time.frameCount + EligibilityIntervalFrames;
                merging = Evaluate(out string reason);
                IneligibleReason = reason;
            }
            if (merging != IsMerging)
            {
                IsMerging = merging;
                _geometryDirty = true;
                changed = true;
            }
            foreach (TextStudioText member in _members)
                if (member != null) member.SetWorldBatchState(this, merging);
            if (!merging)
            {
                HideShards();
                return;
            }
            if (changed) Rebuild();
        }

        void CollectMembers()
        {
            _membershipDirty = false;
            _geometryDirty = true;
            GetComponentsInChildren(false, _scan);
            List<TextStudioText> previous = new List<TextStudioText>(_members);
            _members.Clear();
            // Children of a nested group belong to it; GetComponentsInChildren already returns hierarchy order.
            foreach (TextStudioText text in _scan)
                if (text != null && text.isActiveAndEnabled && text.GetComponentInParent<TextStudioWorldBatchGroup>() == this)
                    _members.Add(text);
            foreach (TextStudioText text in previous)
                if (text != null && !_members.Contains(text)) text.SetWorldBatchState(null, false);
            _scan.Clear();
        }

        bool Evaluate(out string reason)
        {
            reason = string.Empty;
            if (_members.Count == 0)
            {
                reason = "Add enabled world-space Text Studio texts below this object.";
                return false;
            }
            GetComponentsInChildren(false, _sortingGroups);
            foreach (SortingGroup group in _sortingGroups)
            {
                if (group == _sortingGroup || !group.isActiveAndEnabled) continue;
                reason = $"Nested Sorting Group '{group.name}' orders its own renderers, so every text keeps its own output.";
                return false;
            }
            foreach (TextStudioText member in _members)
            {
                TMP_Text text = member.GetComponent<TMP_Text>();
                if (text == null || text is TextMeshProUGUI)
                {
                    reason = $"'{member.name}' is not world-space TextMeshPro text; Canvas text batches through its Canvas.";
                    return false;
                }
                if (!TryGetRequirements(member, out TextStudio2DStyleRequirements requirements))
                {
                    reason = $"'{member.name}' has no Appearance, so its native text draws on its own.";
                    return false;
                }
                if ((requirements & TextStudio2DStyleRequirements.ObjectCoordinates) != 0)
                {
                    reason = $"'{member.name}' maps paint in object space, which would move inside a shared shard.";
                    return false;
                }
                // The field shader's perspective filter reads the object Z axis; texts may turn only within the group plane.
                Vector3 axis = transform.InverseTransformDirection(member.transform.forward);
                if (Mathf.Abs(axis.normalized.z) < .9999f)
                {
                    reason = $"'{member.name}' is tilted out of the group plane; keep member texts parallel to the group.";
                    return false;
                }
            }
            GetComponentsInChildren(false, _renderers);
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || IsGroupRenderer(renderer)) continue;
                TextStudioText owner = renderer.GetComponentInParent<TextStudioText>();
                if (owner != null && _members.Contains(owner))
                {
                    if (IsMemberSource(renderer, owner)) continue;
                    reason = $"'{owner.name}' draws '{renderer.name}' with its own renderer, which a shared shard cannot order.";
                    return false;
                }
                if (renderer.GetComponentInParent<TextStudioWorldBatchGroup>() != this) continue;
                reason = $"Renderer '{renderer.name}' is not Text Studio output; merging would change how it sorts against the texts.";
                return false;
            }
            return true;
        }

        static bool TryGetRequirements(TextStudioText member, out TextStudio2DStyleRequirements requirements)
        {
            requirements = TextStudio2DStyleRequirements.None;
            CompiledTextStudioAppearancePlan plan = member.CompiledAppearancePlan;
            if (plan != null && plan.IsValid && !plan.IsPassThrough)
            {
                foreach (CompiledTextStudio2DStyle program in plan.Programs)
                    if (program != null) requirements |= program.Requirements;
                return true;
            }
            CompiledTextStudio2DStyle style = member.CompiledAppearanceStyle;
            if (style == null || !style.IsValid || style.IsPassThrough) return false;
            requirements = style.Requirements;
            return true;
        }

        bool IsGroupRenderer(Renderer renderer) => renderer.transform.parent == transform && renderer.name == ShardName;

        // The source TMP renderer and its sub-meshes are suppressed while replaced; ordered and Hero outputs merge.
        static bool IsMemberSource(Renderer renderer, TextStudioText owner)
        {
            if (renderer.gameObject == owner.gameObject) return true;
            if (renderer.GetComponent<TMP_SubMesh>() != null) return true;
            return renderer.transform.parent == owner.transform &&
                   (renderer.name == OrderedOutputName || renderer.name == HeroOutputName);
        }

        bool MembersChanged()
        {
            if (_states.Count != _members.Count) return true;
            for (int index = 0; index < _members.Count; index++)
                if (!_states[index].Matches(Capture(_members[index]))) return true;
            return false;
        }

        MemberState Capture(TextStudioText member)
        {
            MemberState state = new MemberState { Member = member };
            if (member == null || !member.TryGetWorldBatchPrimitives(out TextStudio2DOrderedRenderer ordered, out TextStudio2DHeroCompositor hero))
                return state;
            Transform space = null;
            if (hero != null && hero.TryGetWorldBatch(out _, out Material material, out space, out int heroRevision))
            {
                state.Kind = 2;
                state.Revision = heroRevision;
                state.Material = material;
            }
            else if (ordered != null && ordered.TryGetWorldBatch(out _, out _, out space, out int orderedRevision))
            {
                state.Kind = 1;
                state.Revision = orderedRevision;
            }
            if (space != null) state.Space = transform.worldToLocalMatrix * space.localToWorldMatrix;
            MeshRenderer source = member.GetComponent<MeshRenderer>();
            if (source != null)
            {
                TextStudioSurfaceGroup.ResolveWorldSorting(member.GetComponent<TMP_Text>(), source, out int layer, out int order);
                state.SortingLayerValue = SortingLayer.GetLayerValueFromID(layer);
                state.SortingOrder = order;
                state.RenderState = new RenderState(member.gameObject.layer, source.shadowCastingMode, source.receiveShadows, source.renderingLayerMask);
            }
            return state;
        }

        void Rebuild()
        {
            _geometryDirty = false;
            RebuildCount++;
            _states.Clear();
            foreach (TextStudioText member in _members) _states.Add(Capture(member));
            // Stable ordering: sorting layer, then sorting order, then hierarchy order for ties.
            List<int> order = new List<int>(_states.Count);
            for (int index = 0; index < _states.Count; index++) order.Add(index);
            order.Sort((a, b) =>
            {
                int layer = _states[a].SortingLayerValue.CompareTo(_states[b].SortingLayerValue);
                if (layer != 0) return layer;
                int sorting = _states[a].SortingOrder.CompareTo(_states[b].SortingOrder);
                return sorting != 0 ? sorting : a.CompareTo(b);
            });
            _builder.Begin();
            foreach (int index in order)
            {
                MemberState state = _states[index];
                if (state.Kind == 0 || !state.Member.TryGetWorldBatchPrimitives(out TextStudio2DOrderedRenderer ordered, out TextStudio2DHeroCompositor hero))
                    continue;
                if (state.Kind == 2 && hero.TryGetWorldBatch(out Mesh quad, out Material material, out _, out _))
                    _builder.AppendQuad(quad, material, state.Space, state.RenderState);
                else if (state.Kind == 1 && ordered.TryGetWorldBatch(out TextStudio2DOrderedRenderer.Geometry geometry,
                             out List<TextStudio2DOrderedRenderer.Run> runs, out _, out _))
                    _builder.AppendOrdered(geometry, runs, state.Space, state.RenderState);
            }
            _builder.End();
            Publish();
        }

        void Publish()
        {
            int shardCount = _builder.ShardCount;
            _publishing = true;
            try
            {
                while (_shards.Count < shardCount) _shards.Add(new Shard(transform, ShardName));
            }
            finally { _publishing = false; }
            int draws = 0;
            for (int index = 0; index < _shards.Count; index++)
            {
                if (index >= shardCount)
                {
                    _shards[index].Hide();
                    continue;
                }
                _builder.Upload(index, _shards[index], _sortingGroup.sortingLayerID, index);
                draws += _builder.RunCount(index);
            }
            ShardCount = shardCount;
            DrawCount = draws;
        }

        void HideShards()
        {
            foreach (Shard shard in _shards) shard.Hide();
            ShardCount = 0;
            DrawCount = 0;
            _states.Clear();
        }

        void Release()
        {
            foreach (TextStudioText member in _members)
                if (member != null) member.SetWorldBatchState(null, false);
            _members.Clear();
            _states.Clear();
            foreach (Shard shard in _shards) shard.Dispose();
            _shards.Clear();
            IsMerging = false;
            ShardCount = 0;
            DrawCount = 0;
        }

        readonly struct RenderState : IEquatable<RenderState>
        {
            readonly int _layer;
            readonly ShadowCastingMode _shadows;
            readonly bool _receiveShadows;
            readonly uint _renderingLayers;

            internal RenderState(int layer, ShadowCastingMode shadows, bool receiveShadows, uint renderingLayers)
            {
                _layer = layer;
                _shadows = shadows;
                _receiveShadows = receiveShadows;
                _renderingLayers = renderingLayers;
            }

            internal int Layer => _layer;
            internal void Apply(MeshRenderer renderer)
            {
                renderer.gameObject.layer = _layer;
                renderer.shadowCastingMode = _shadows;
                renderer.receiveShadows = _receiveShadows;
                renderer.renderingLayerMask = _renderingLayers;
            }

            public bool Equals(RenderState other) => _layer == other._layer && _shadows == other._shadows &&
                                                    _receiveShadows == other._receiveShadows && _renderingLayers == other._renderingLayers;
            public override bool Equals(object obj) => obj is RenderState other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_layer, (int)_shadows, _receiveShadows, _renderingLayers);
        }

        struct MemberState
        {
            internal TextStudioText Member;
            internal int Kind;
            internal int Revision;
            internal Material Material;
            internal Matrix4x4 Space;
            internal int SortingLayerValue;
            internal int SortingOrder;
            internal RenderState RenderState;

            internal bool Matches(in MemberState other) => Member == other.Member && Kind == other.Kind && Revision == other.Revision &&
                                                          Material == other.Material && Space == other.Space &&
                                                          SortingLayerValue == other.SortingLayerValue && SortingOrder == other.SortingOrder &&
                                                          RenderState.Equals(other.RenderState);
        }

        /// <summary>One shard renderer: a child mesh drawing contiguous runs in painter order.</summary>
        sealed class Shard : IDisposable
        {
            readonly GameObject _object;
            readonly Mesh _mesh;
            internal readonly MeshRenderer Renderer;

            internal Shard(Transform parent, string name)
            {
                _object = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                _object.transform.SetParent(parent, false);
                _mesh = new Mesh { name = "Text Studio World Batch", hideFlags = HideFlags.HideAndDontSave };
                _mesh.MarkDynamic();
                _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
                Renderer = _object.AddComponent<MeshRenderer>();
                Renderer.lightProbeUsage = LightProbeUsage.Off;
                Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                Renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                Renderer.SetDynamicOcclusionCulling(false);
            }

            internal Mesh Mesh => _mesh;
            internal void Hide() { if (Renderer != null) Renderer.enabled = false; }

            public void Dispose()
            {
                DestroyOwnedObject(_mesh);
                DestroyOwnedObject(_object);
            }
        }

        /// <summary>Appends members' primitives into bounded shards, merging contiguous runs that share material and state.</summary>
        sealed class ShardBuilder
        {
            sealed class Data
            {
                internal readonly List<Vector3> Vertices = new List<Vector3>();
                internal readonly List<Vector3> Normals = new List<Vector3>();
                internal readonly List<Color32> Colors = new List<Color32>();
                internal readonly List<Vector4> Uv0 = new List<Vector4>();
                internal readonly List<Vector4> Uv1 = new List<Vector4>();
                internal readonly List<Vector4> Uv2 = new List<Vector4>();
                internal readonly List<Vector4> Uv3 = new List<Vector4>();
                internal readonly List<Vector4> Local = new List<Vector4>();
                internal readonly List<List<int>> RunIndices = new List<List<int>>();
                internal readonly List<Material> Materials = new List<Material>();
                internal RenderState State;
                internal int RunCount;

                internal void Clear()
                {
                    Vertices.Clear(); Normals.Clear(); Colors.Clear(); Uv0.Clear(); Uv1.Clear(); Uv2.Clear(); Uv3.Clear(); Local.Clear();
                    foreach (List<int> indices in RunIndices) indices.Clear();
                    Materials.Clear();
                    RunCount = 0;
                }
            }

            readonly List<Data> _shards = new List<Data>();
            readonly List<Vector3> _quadVertices = new List<Vector3>(4);
            readonly List<Color32> _quadColors = new List<Color32>(4);
            readonly List<Vector4> _quadUv = new List<Vector4>(4);
            readonly List<int> _quadIndices = new List<int>(6);
            int _count;

            internal int ShardCount => _count;
            internal int RunCount(int shard) => _shards[shard].RunCount;

            internal void Begin() => _count = 0;
            internal void End() { }

            Data Current => _count > 0 ? _shards[_count - 1] : null;

            Data Reserve(int vertexCount, in RenderState state)
            {
                Data current = Current;
                if (current != null && current.State.Equals(state) &&
                    (current.Vertices.Count == 0 || current.Vertices.Count + vertexCount <= MaximumShardVertices))
                    return current;
                if (_count == _shards.Count) _shards.Add(new Data());
                current = _shards[_count++];
                current.Clear();
                current.State = state;
                return current;
            }

            List<int> Run(Data shard, Material material)
            {
                if (shard.RunCount > 0 && shard.Materials[shard.RunCount - 1] == material)
                    return shard.RunIndices[shard.RunCount - 1];
                if (shard.RunIndices.Count == shard.RunCount) shard.RunIndices.Add(new List<int>());
                shard.Materials.Add(material);
                return shard.RunIndices[shard.RunCount++];
            }

            internal void AppendOrdered(TextStudio2DOrderedRenderer.Geometry geometry, List<TextStudio2DOrderedRenderer.Run> runs,
                in Matrix4x4 space, in RenderState state)
            {
                Data shard = Reserve(geometry.Vertices.Count, state);
                int baseVertex = shard.Vertices.Count;
                foreach (Vector3 vertex in geometry.Vertices) shard.Vertices.Add(space.MultiplyPoint3x4(vertex));
                // Normals, UVs and local coordinates are payloads rather than geometry, so they transfer unchanged.
                shard.Normals.AddRange(geometry.Normals);
                shard.Colors.AddRange(geometry.Colors);
                shard.Uv0.AddRange(geometry.Uv0);
                shard.Uv1.AddRange(geometry.Uv1);
                shard.Uv2.AddRange(geometry.Uv2);
                shard.Uv3.AddRange(geometry.Uv3);
                shard.Local.AddRange(geometry.Local);
                foreach (TextStudio2DOrderedRenderer.Run run in runs)
                {
                    List<int> indices = Run(shard, run.Material);
                    for (int index = run.IndexStart; index < run.IndexStart + run.IndexCount; index++)
                        indices.Add(geometry.Indices[index] + baseVertex);
                }
            }

            internal void AppendQuad(Mesh quad, Material material, in Matrix4x4 space, in RenderState state)
            {
                quad.GetVertices(_quadVertices);
                quad.GetColors(_quadColors);
                quad.GetUVs(0, _quadUv);
                quad.GetTriangles(_quadIndices, 0);
                Data shard = Reserve(_quadVertices.Count, state);
                int baseVertex = shard.Vertices.Count;
                for (int index = 0; index < _quadVertices.Count; index++)
                {
                    shard.Vertices.Add(space.MultiplyPoint3x4(_quadVertices[index]));
                    shard.Normals.Add(Vector3.zero);
                    shard.Colors.Add(index < _quadColors.Count ? _quadColors[index] : new Color32(255, 255, 255, 255));
                    shard.Uv0.Add(index < _quadUv.Count ? _quadUv[index] : Vector4.zero);
                    shard.Uv1.Add(Vector4.zero);
                    shard.Uv2.Add(Vector4.zero);
                    shard.Uv3.Add(Vector4.zero);
                    shard.Local.Add(Vector4.zero);
                }
                List<int> indices = Run(shard, material);
                foreach (int index in _quadIndices) indices.Add(index + baseVertex);
            }

            internal void Upload(int index, Shard target, int sortingLayer, int sortingOrder)
            {
                Data data = _shards[index];
                Mesh mesh = target.Mesh;
                mesh.Clear();
                mesh.indexFormat = data.Vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(data.Vertices);
                mesh.SetNormals(data.Normals);
                mesh.SetColors(data.Colors);
                mesh.SetUVs(0, data.Uv0);
                mesh.SetUVs(1, data.Uv1);
                mesh.SetUVs(2, data.Uv2);
                mesh.SetUVs(3, data.Uv3);
                mesh.SetUVs(4, data.Local);
                mesh.subMeshCount = data.RunCount;
                for (int run = 0; run < data.RunCount; run++)
                    mesh.SetTriangles(data.RunIndices[run], run, false);
                mesh.RecalculateBounds();
                MeshRenderer renderer = target.Renderer;
                renderer.SetSharedMaterials(data.Materials);
                data.State.Apply(renderer);
                // Inside the SortingGroup every shard shares one layer and a monotonic order, preserving painter order.
                renderer.sortingLayerID = sortingLayer;
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = true;
            }
        }

        static void DestroyOwnedObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
