using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio
{
    /// <summary>
    /// Final glyph quads that a text engine with its own glyph atlas publishes for one Text Studio object. The shared
    /// renderer draws them in place of TMP quads and samples the engine's atlas as an external field bank: each render
    /// glyph names a stable handle into the engine's transform table, so atlas relocations need no geometry or mapping
    /// update. Native field coordinates are em; one em spans <see cref="FieldPixelsPerEm"/> virtual field pixels.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Shared native materials are reference-counted by glyph-source owners and released explicitly.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DExternalGlyphSource : IDisposable
    {
        /// <summary>
        /// Virtual field resolution of the em coordinate space. The engine's decode needs no conversion at any value; it
        /// also sets the offscreen composition density of engine-owned glyphs.
        /// </summary>
        internal const float FieldPixelsPerEm = 128f;
        internal const int SingleChannelBank = 0;
        internal const int MultiChannelBank = 1;
        const int BankCount = 2;
        // Each bank has one material per shader tier (TextStudio2DShaderTier: Full, Basic and the isolated families), each with
        // and without the solid-paint specialization (TextStudio2DShaderOptions.SolidPaint).
        const int FamilySlots = 6;
        const int SlotCount = BankCount * FamilySlots * 2;

        static readonly int s_preparedFields = Shader.PropertyToID("_TextStudioPreparedFields");
        static readonly int s_glyphTable = Shader.PropertyToID("_TextStudioExternalGlyphTable");
        static readonly int s_textureWidth = Shader.PropertyToID("_TextureWidth");
        static readonly int s_textureHeight = Shader.PropertyToID("_TextureHeight");
        static readonly int s_gradientScale = Shader.PropertyToID("_GradientScale");
        static readonly int s_pixelsPerEm = Shader.PropertyToID(TextStudioGlyphFieldContract.PixelsPerEmProperty);
        static bool s_fieldShaderWarningLogged;
        static bool s_canvasShaderWarningLogged;

        // Sources that bind the same output kind, bank and textures share one field material. UniText binds one global
        // atlas and glyph table to every text, and Canvas batching needs the same material instance: a material per
        // source drew 200 owned UniText labels in 200 batches. Uniform field materials also share by program rows.
        static readonly Dictionary<MaterialKey, SharedMaterial> s_sharedMaterials = new Dictionary<MaterialKey, SharedMaterial>();
        static readonly Dictionary<UniformMaterialKey, SharedMaterial> s_sharedUniformMaterials =
            new Dictionary<UniformMaterialKey, SharedMaterial>();

        internal static int SharedMaterialCount => s_sharedMaterials.Count + s_sharedUniformMaterials.Count;

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseSharedMaterials()
        {
            foreach (SharedMaterial shared in s_sharedMaterials.Values)
                Destroy(shared.Material);
            s_sharedMaterials.Clear();
            foreach (SharedMaterial shared in s_sharedUniformMaterials.Values)
                Destroy(shared.Material);
            s_sharedUniformMaterials.Clear();
            s_fieldShaderWarningLogged = false;
            s_canvasShaderWarningLogged = false;
        }
#endif

        readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            readonly bool _canvas;
            readonly int _bank;
            readonly Texture _atlas;
            readonly Texture _table;

            internal MaterialKey(bool canvas, int bank, Texture atlas, Texture table)
            {
                _canvas = canvas;
                _bank = bank;
                _atlas = atlas;
                _table = table;
            }

            public bool Equals(MaterialKey other) => _canvas == other._canvas && _bank == other._bank &&
                ReferenceEquals(_atlas, other._atlas) && ReferenceEquals(_table, other._table);

            public override bool Equals(object obj) => obj is MaterialKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int atlas = ReferenceEquals(_atlas, null) ? 0 : RuntimeHelpers.GetHashCode(_atlas);
                    int table = ReferenceEquals(_table, null) ? 0 : RuntimeHelpers.GetHashCode(_table);
                    return ((atlas * 397 ^ table) * 397 ^ _bank) * 2 + (_canvas ? 1 : 0);
                }
            }
        }

        // A uniform field material draws the single-channel bank with the rows of one program. Kept apart from the
        // field keys, which every source holds per slot, so those stay small.
        readonly struct UniformMaterialKey : IEquatable<UniformMaterialKey>
        {
            readonly MaterialKey _textures;
            readonly TextStudio2DUniformFieldProgram _program;

            internal UniformMaterialKey(bool canvas, Texture atlas, Texture table, in TextStudio2DUniformFieldProgram program)
            {
                _textures = new MaterialKey(canvas, SingleChannelBank, atlas, table);
                _program = program;
            }

            public bool Equals(UniformMaterialKey other) => _textures.Equals(other._textures) && _program.Equals(other._program);

            public override bool Equals(object obj) => obj is UniformMaterialKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked { return _textures.GetHashCode() * 397 ^ _program.GetHashCode(); }
            }
        }

        sealed class SharedMaterial
        {
            internal Material Material;
            internal int Users;
        }

        /// <summary>Field identity of one render glyph, stable while the engine's layout is unchanged.</summary>
        internal struct Field
        {
            internal float Handle;
            /// <summary>Em height the engine normalizes this glyph's coordinates by.</summary>
            internal float GlyphHeightEm;
            /// <summary>Em rectangle (min x, min y, max x, max y) for which the engine stores field data.</summary>
            internal Vector4 ValidEm;
            /// <summary>Tight glyph box in em.</summary>
            internal Vector4 GlyphEm;
            /// <summary>Unanimated quad in object space (min x, min y, max x, max y), for source-coordinate paints.</summary>
            internal Vector4 SourceRect;
            /// <summary>Engine cluster the glyph belongs to; with the handle it matches published quads to render glyphs.</summary>
            internal int Cluster;
            internal int Bank;

            internal bool IsValid => GlyphHeightEm > 0 && Handle >= 0 && ValidEm.z > ValidEm.x && ValidEm.w > ValidEm.y;

            internal bool Equals(in Field other) => Handle == other.Handle && GlyphHeightEm == other.GlyphHeightEm &&
                ValidEm == other.ValidEm && GlyphEm == other.GlyphEm && SourceRect == other.SourceRect && Cluster == other.Cluster &&
                Bank == other.Bank;
        }

        /// <summary>One render glyph's quad in the latest publication; corners bottom-left, top-left, top-right, bottom-right.</summary>
        internal struct Quad
        {
            internal Vector3 Position0, Position1, Position2, Position3;
            internal Color32 Color0, Color1, Color2, Color3;
            /// <summary>Em field coordinates at the bottom-left (xy) and top-right (zw) corners.</summary>
            internal Vector4 FieldEm;
            internal bool IsPresent;
        }

        Field[] _fields = Array.Empty<Field>();
        Quad[] _quads = Array.Empty<Quad>();
        readonly Material[] _worldMaterials = new Material[SlotCount];
        readonly Material[] _canvasMaterials = new Material[SlotCount];
        readonly MaterialKey[] _worldKeys = new MaterialKey[SlotCount];
        readonly MaterialKey[] _canvasKeys = new MaterialKey[SlotCount];
        Material _uniformMaterial;
        UniformMaterialKey _uniformKey;
        Texture _singleChannelAtlas, _multiChannelAtlas, _table;
        int _count;
        bool _fieldsChanged;

        internal int Count => _count;
        /// <summary>Changes whenever any field identity changes; mapping rows depend on it.</summary>
        internal int FieldRevision { get; private set; }
        /// <summary>Changes on every publication of quads.</summary>
        internal int GeometryRevision { get; private set; }
        /// <summary>Output parent, sorting and shadow state of the owning engine component.</summary>
        internal Transform Host { get; set; }
        internal int SortingLayerId { get; set; }
        internal int SortingOrder { get; set; }
        internal bool CastShadows { get; set; }
        /// <summary>The owning engine draws into a Canvas; outputs are UI graphics under <see cref="Host"/>.</summary>
        internal bool IsCanvas { get; set; }
        /// <summary>The engine hides its text (Hide or scene visibility); owned output hides with it.</summary>
        internal bool Suppressed { get; set; }
        /// <summary>Canvas renderer color of the engine component, adopted by newly created UI outputs.</summary>
        internal Color CanvasColor { get; set; } = Color.white;
        internal bool HasAtlas => _table != null && (_singleChannelAtlas != null || _multiChannelAtlas != null);

        internal ref readonly Field GetField(int index) => ref _fields[index];
        internal ref readonly Quad GetQuad(int index) => ref _quads[index];

        internal void BeginFields(int count)
        {
            if (_fields.Length < count) Array.Resize(ref _fields, TextStudioBufferCapacity.ForCount(count));
            if (_quads.Length < count) Array.Resize(ref _quads, TextStudioBufferCapacity.ForCount(count));
            _fieldsChanged |= _count != count;
            for (int index = count; index < _count; index++) _quads[index] = default;
            _count = count;
        }

        internal void SetField(int index, in Field field)
        {
            if ((uint)index >= (uint)_count || _fields[index].Equals(in field)) return;
            _fields[index] = field;
            _fieldsChanged = true;
        }

        internal void EndFields()
        {
            if (!_fieldsChanged) return;
            _fieldsChanged = false;
            FieldRevision = unchecked(FieldRevision + 1);
        }

        internal void BeginQuads()
        {
            for (int index = 0; index < _count; index++) _quads[index].IsPresent = false;
        }

        internal void SetQuad(int index, in Quad quad)
        {
            if ((uint)index >= (uint)_count) return;
            _quads[index] = quad;
            _quads[index].IsPresent = true;
        }

        internal void EndQuads() => GeometryRevision = unchecked(GeometryRevision + 1);

        /// <summary>Encodes a render glyph's external mapping row; false leaves the glyph without a field.</summary>
        internal bool TryMap(int index, out Vector4 transform, out Vector4 bounds, out Vector4 decode)
        {
            transform = bounds = decode = Vector4.zero;
            if ((uint)index >= (uint)_count || !_fields[index].IsValid) return false;
            ref readonly Field field = ref _fields[index];
            transform = new Vector4(field.Handle, 1f / field.GlyphHeightEm, field.Bank == SingleChannelBank ? 1 : 0, 0);
            bounds = field.ValidEm;
            decode = new Vector4(-1, 1, FieldPixelsPerEm, 0);
            return true;
        }

        /// <summary>Binds the engine's current atlas arrays and transform table. Engines may replace them at any time.</summary>
        internal void BindAtlas(Texture singleChannel, Texture multiChannel, Texture table)
        {
            if (ReferenceEquals(_singleChannelAtlas, singleChannel) && ReferenceEquals(_multiChannelAtlas, multiChannel) &&
                ReferenceEquals(_table, table))
                return;
            _singleChannelAtlas = singleChannel;
            _multiChannelAtlas = multiChannel;
            _table = table;
            // Shared materials keep the textures they were created for; the next resolve selects the material of the new
            // ones. Engines publish new quads with new textures, so the next build resolves again.
            ReleaseMaterials();
        }

        /// <summary>
        /// The owned field material of one bank, or null when the field shader is unavailable. Each primitive uses the variant
        /// of its program (<see cref="TextStudio2DPrimitivePrograms"/>): isolated operations their family, light runs Full or Basic.
        /// </summary>
        internal Material ResolveMaterial(int bank, TextStudio2DShaderTier tier = TextStudio2DShaderTier.Full, bool solidPaint = false)
        {
            bank = Mathf.Clamp(bank, 0, BankCount - 1);
            int family = Mathf.Clamp((int)tier, 0, FamilySlots - 1);
            int slot = bank + BankCount * (family + (solidPaint ? FamilySlots : 0));
            Material[] materials = IsCanvas ? _canvasMaterials : _worldMaterials;
            MaterialKey[] keys = IsCanvas ? _canvasKeys : _worldKeys;
            if (materials[slot] != null) return materials[slot];
            Release(materials, keys, slot);
            MaterialKey key = new MaterialKey(IsCanvas, slot, bank == SingleChannelBank ? _singleChannelAtlas : _multiChannelAtlas, _table);
            if (!s_sharedMaterials.TryGetValue(key, out SharedMaterial shared) || shared.Material == null)
            {
                Material created = CreateMaterial(bank, tier, solidPaint);
                if (created == null) return null;
                shared = new SharedMaterial { Material = created };
                s_sharedMaterials[key] = shared;
            }
            shared.Users++;
            materials[slot] = shared.Material;
            keys[slot] = key;
            return shared.Material;
        }

        Material CreateMaterial(int bank, TextStudio2DShaderTier tier, bool solidPaint)
        {
            string resource = IsCanvas ? TextStudio2DResourceKeys.FieldCanvasShader : TextStudio2DResourceKeys.FieldShader;
            Shader shader = IsCanvas
                ? TextStudio2DShaderResources.LoadSupported(resource, "2D appearance styling", ref s_canvasShaderWarningLogged, out _)
                : TextStudio2DShaderResources.LoadSupported(resource, "2D appearance styling", ref s_fieldShaderWarningLogged, out _);
            if (shader == null) return null;
            Material material = new Material(shader)
            {
                name = "Text Studio External Glyph Field (" + (bank == SingleChannelBank ? "SDF" : "MSDF") + ")",
                hideFlags = HideFlags.HideAndDontSave
            };
            ConfigureFieldMetrics(material);
            // External quads carry no material-effect payload (EmitExternal writes zero effect streams), so the lean program is exact.
            TextStudio2DShaderTiers.Configure(material, tier,
                solidPaint ? TextStudio2DShaderOptions.SolidPaint : TextStudio2DShaderOptions.None);
            BindMaterial(material, bank);
            return material;
        }

        /// <summary>
        /// The uniform field material drawing this source's single-channel glyphs for <paramref name="program"/>, shared by
        /// sources with the same output kind, textures and rows. Null for an invalid program or when the uniform shader or its
        /// external-field variant is unavailable; those glyphs keep the field material of <see cref="ResolveMaterial"/>.
        /// Multi-channel glyphs always keep it: the uniform program reads one distance.
        /// </summary>
        internal Material ResolveUniformMaterial(in TextStudio2DUniformFieldProgram program)
        {
            if (!program.IsValid)
            {
                ReleaseUniformMaterial();
                return null;
            }
            UniformMaterialKey key = new UniformMaterialKey(IsCanvas, _singleChannelAtlas, _table, in program);
            if (_uniformMaterial != null && _uniformKey.Equals(key)) return _uniformMaterial;
            ReleaseUniformMaterial();
            if (!s_sharedUniformMaterials.TryGetValue(key, out SharedMaterial shared) || shared.Material == null)
            {
                Material created = CreateUniformMaterial(in program);
                if (created == null) return null;
                shared = new SharedMaterial { Material = created };
                s_sharedUniformMaterials[key] = shared;
            }
            shared.Users++;
            _uniformMaterial = shared.Material;
            _uniformKey = key;
            return _uniformMaterial;
        }

        /// <summary>Releases this source's uniform field material, for example when its style stops selecting the program.</summary>
        internal void ReleaseUniformMaterial()
        {
            Material material = _uniformMaterial;
            if (ReferenceEquals(material, null)) return;
            _uniformMaterial = null;
            UniformMaterialKey key = _uniformKey;
            _uniformKey = default;
            if (!s_sharedUniformMaterials.TryGetValue(key, out SharedMaterial shared) || !ReferenceEquals(shared.Material, material))
                return;
            if (--shared.Users > 0) return;
            s_sharedUniformMaterials.Remove(key);
            Destroy(material);
        }

        Material CreateUniformMaterial(in TextStudio2DUniformFieldProgram program)
        {
            Shader shader = TextStudio2DShaderTiers.LoadUniformShader(IsCanvas);
            if (shader == null) return null;
            LocalKeyword external = shader.keywordSpace.FindKeyword(TextStudio2DShaderTiers.UniformExternalFieldKeyword);
            if (!external.isValid) return null;
            Material material = new Material(shader)
            {
                name = "Text Studio External Glyph Uniform Field (SDF)",
                hideFlags = HideFlags.HideAndDontSave
            };
            ConfigureFieldMetrics(material);
            material.SetKeyword(external, true);
            TextStudio2DShaderTiers.ConfigureUniformProgram(material, in program);
            BindMaterial(material, SingleChannelBank);
            return material;
        }

        // Em field metrics and source flags shared by the field and uniform materials of engine glyphs.
        void ConfigureFieldMetrics(Material material)
        {
            // One em spans P virtual pixels and the stored distance is (value - .5) em, so a gradient scale of P / 2
            // reproduces the engine's decode exactly.
            material.SetFloat("_TextureWidth", FieldPixelsPerEm);
            material.SetFloat("_TextureHeight", FieldPixelsPerEm);
            material.SetFloat("_GradientScale", FieldPixelsPerEm * .5f);
            material.SetFloat(TextStudioGlyphFieldContract.PixelsPerEmProperty, FieldPixelsPerEm);
            material.SetFloat(TextStudioTMPMaterialCompatibility.NativeMaterialModelProperty, (float)TextStudioTMPNativeMaterialModel.Full);
            material.SetFloat("_TextStudioPayloadEnabled", 1f);
            material.SetFloat("_TextStudioPayloadFromTangent", IsCanvas ? 1f : 0f);
            material.SetFloat("_OutlineWidth", 0f);
            material.DisableKeyword("OUTLINE_ON");
            material.DisableKeyword("UNDERLAY_ON");
            material.DisableKeyword("UNDERLAY_INNER");
        }

        void ReleaseMaterials()
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                Release(_worldMaterials, _worldKeys, slot);
                Release(_canvasMaterials, _canvasKeys, slot);
            }
            ReleaseUniformMaterial();
        }

        static void Release(Material[] materials, MaterialKey[] keys, int bank)
        {
            Material material = materials[bank];
            if (ReferenceEquals(material, null)) return;
            materials[bank] = null;
            if (!s_sharedMaterials.TryGetValue(keys[bank], out SharedMaterial shared) || !ReferenceEquals(shared.Material, material))
                return;
            if (--shared.Users > 0) return;
            s_sharedMaterials.Remove(keys[bank]);
            Destroy(material);
        }

        /// <summary>
        /// Binds the bank and em field metrics of <paramref name="batchSource"/> to one offscreen layer draw. Hero layer
        /// materials derive from source materials, so the engine bank travels per draw rather than in a shared material.
        /// </summary>
        internal void ApplyLayerProperties(MaterialPropertyBlock properties, Material batchSource)
        {
            int bank = SingleChannelBank;
            for (int slot = MultiChannelBank; slot < SlotCount; slot += BankCount)
            {
                if (ReferenceEquals(batchSource, _worldMaterials[slot]) || ReferenceEquals(batchSource, _canvasMaterials[slot]))
                    bank = MultiChannelBank;
            }
            Texture atlas = bank == SingleChannelBank ? _singleChannelAtlas : _multiChannelAtlas;
            if (atlas != null) properties.SetTexture(s_preparedFields, atlas);
            if (_table != null) properties.SetTexture(s_glyphTable, _table);
            properties.SetFloat(s_textureWidth, FieldPixelsPerEm);
            properties.SetFloat(s_textureHeight, FieldPixelsPerEm);
            properties.SetFloat(s_gradientScale, FieldPixelsPerEm * .5f);
            properties.SetFloat(s_pixelsPerEm, FieldPixelsPerEm);
        }

        void BindMaterial(Material material, int bank)
        {
            if (material == null) return;
            Texture atlas = bank == SingleChannelBank ? _singleChannelAtlas : _multiChannelAtlas;
            material.SetTexture(s_preparedFields, atlas);
            material.SetTexture(s_glyphTable, _table);
        }

        internal void Clear()
        {
            _count = 0;
            _fieldsChanged = false;
            FieldRevision = unchecked(FieldRevision + 1);
        }

        public void Dispose()
        {
            Clear();
            ReleaseMaterials();
            _singleChannelAtlas = _multiChannelAtlas = _table = null;
        }

        static void Destroy(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
