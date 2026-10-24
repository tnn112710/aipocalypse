using System;
using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Persistent 2D appearance stack compiled into renderer-specific execution plans.</summary>
    [CreateAssetMenu(fileName = "TextStudio2DStyle", menuName = "Text Studio/Appearance/2D Style", order = 101)]
#if UNITY_6000_7_OR_NEWER
    // Asset change subscriptions serve Edit Mode too; subscribers release their own leases.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class TextStudio2DStyleAsset : ScriptableObject, ITextStudioStackAsset
    {
        public const int CurrentSchemaVersion = 7;

        [SerializeField] int _schemaVersion = CurrentSchemaVersion;
        [SerializeField] string _id;
        [SerializeField] TextStudioAppearanceApplyMode _defaultApplyMode;
        [SerializeField] TextStudioAppearanceExposure[] _exposures = Array.Empty<TextStudioAppearanceExposure>();
        [SerializeField] TextStudio2DStyleAsset _baseStyle;
        [SerializeField] TextStudio2DStyleInheritanceMode _inheritanceMode =
            TextStudio2DStyleInheritanceMode.Append;
        [SerializeField] TextStudio2DSourceFieldQuality _sourceFieldQuality =
            TextStudio2DSourceFieldQuality.Automatic;
        [SerializeField] TextStudio2DPainterOrder _painterOrder =
            TextStudio2DPainterOrder.LayerMajor;
        [SerializeField] TextStudio2DWorldShadowMode _worldShadowMode =
            TextStudio2DWorldShadowMode.Off;
        [SerializeField] bool _exactLayering;
        [SerializeField] TextStudio2DLayer[] _layers = Array.Empty<TextStudio2DLayer>();
        [NonSerialized] int _revision;
        [NonSerialized] int[] _definitionDomains;
        [NonSerialized] CompiledTextStudio2DStyle _sharedProgram;
        [NonSerialized] TextStudio2DPaintAsset[] _paintSources;
        [NonSerialized] bool[] _paintSourcePresent;
        [NonSerialized] int[] _paintSourceRevisions;
        [NonSerialized] Texture2D[] _paintTextures;
        [NonSerialized] int _paintDependencyRevision;
        [NonSerialized] ulong _paintDependencyBaseKey;
        [NonSerialized] int _frameDependencyFrame = -1;
        [NonSerialized] int _frameDependencyRevision;
        [NonSerialized] ulong _frameDependencyBaseKey;
        [NonSerialized] ulong _frameDependencyValue;

        internal static event Action<TextStudio2DStyleAsset> Changed;

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseChangeSubscriptions() => Changed = null;
#endif

        public int SchemaVersion => _schemaVersion;
        public string Id => _id ?? string.Empty;
        public TextStudioAppearanceApplyMode DefaultApplyMode => _defaultApplyMode;
        public int Revision => _revision;
        public TextStudio2DStyleAsset BaseStyle => _baseStyle;
        public TextStudio2DStyleInheritanceMode InheritanceMode => _inheritanceMode;
        public TextStudio2DSourceFieldQuality SourceFieldQuality => _sourceFieldQuality;
        public TextStudio2DPainterOrder PainterOrder => _painterOrder;
        public TextStudio2DWorldShadowMode WorldShadowMode => _worldShadowMode;
        /// <summary>
        /// Keeps Text Studio's exact layered rendering. Off, the default, draws a style TextMeshPro can express (one solid fill, a
        /// round stroke and a distance-field outer shadow at Native source quality) through the text's TextMeshPro SDF material.
        /// </summary>
        public bool ExactLayering => _exactLayering;
        public int ItemCount => _layers?.Length ?? 0;
        /// <summary>Returns an independent snapshot of this asset's local settings.</summary>
        public TextStudio2DStyleSettings Settings => GetSettingsForCompilation().Clone();

        internal TextStudio2DStyleSettings GetSettingsForCompilation()
        {
            if (_definitionDomains == null || _definitionDomains.Length != ItemCount) _definitionDomains = TextStudioAppearanceDomains.Build(_layers);
            return new TextStudio2DStyleSettings(
                _baseStyle, _inheritanceMode, _sourceFieldQuality, _painterOrder,
                _worldShadowMode, _layers, _exposures, _definitionDomains, _exactLayering);
        }

        /// <summary>Sets a stable catalog ID independently from the asset filename. Empty IDs remain ordinary styles.</summary>
        public void SetTagIdentity(string id, TextStudioAppearanceApplyMode defaultApplyMode = TextStudioAppearanceApplyMode.Replace)
        {
            if (!string.IsNullOrEmpty(id) && !TextStudioAppearanceActivation.IsValidId(id)) throw new ArgumentException("Use a letter or underscore, followed by letters, numbers, _, -, . or :.", nameof(id));
            if ((uint)defaultApplyMode > (uint)TextStudioAppearanceApplyMode.Overlay) throw new ArgumentOutOfRangeException(nameof(defaultApplyMode));
            if (Id == (id ?? string.Empty) && _defaultApplyMode == defaultApplyMode) return;
            _id = id ?? string.Empty;
            _defaultApplyMode = defaultApplyMode;
            NotifyChanged();
        }

        /// <summary>Replaces the asset exposure declarations with independent copies and notifies appearance consumers of the change.</summary>
        public void SetExposures(TextStudioAppearanceExposure[] exposures)
        {
            _exposures = TextStudioAppearanceActivation.CloneExposures(exposures);
            NotifyChanged();
        }

        /// <summary>Copies appearance settings into this reusable asset.</summary>
        public void ApplySettings(TextStudio2DStyleSettings settings)
        {
            _schemaVersion = CurrentSchemaVersion;
            _baseStyle = settings.BaseStyle;
            _inheritanceMode = settings.InheritanceMode;
            _sourceFieldQuality = settings.SourceFieldQuality;
            _painterOrder = settings.PainterOrder;
            _worldShadowMode = settings.WorldShadowMode;
            _exactLayering = settings.ExactLayering;
            _layers = settings.CloneAuthoredLayers();
            _exposures = settings.Exposures;
            RepairLayers();
            NotifyChanged();
        }

        /// <summary>Returns the persistent stack-item identity of the layer at the specified index.</summary>
        public TextStudioStackItemId GetItemId(int index)
        {
            return GetLayer(index).ItemId;
        }

        /// <summary>Returns an independent copy of the layer at the specified index, throwing if the index is outside the layer array.</summary>
        public TextStudio2DLayer GetLayer(int index)
        {
            if (_layers == null || index < 0 || index >= _layers.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _layers[index].Clone();
        }

        /// <summary>Replaces the authored stack with a defensive deep copy.</summary>
        public void SetLayers(TextStudio2DLayer[] layers)
        {
            _layers = TextStudio2DStyleSettings.CloneLayers(layers);
            RepairLayers();
            NotifyChanged();
        }

        /// <summary>Changes the optional base style and flattening behavior.</summary>
        public void SetInheritance(
            TextStudio2DStyleAsset baseStyle,
            TextStudio2DStyleInheritanceMode mode)
        {
            _baseStyle = baseStyle;
            _inheritanceMode = mode;
            RepairLayers();
            NotifyChanged();
        }

        /// <summary>Changes renderer-neutral field, painter-order, and world-shadow requests.</summary>
        public void SetRenderingOptions(
            TextStudio2DSourceFieldQuality sourceFieldQuality,
            TextStudio2DPainterOrder painterOrder,
            TextStudio2DWorldShadowMode worldShadowMode)
        {
            _sourceFieldQuality = sourceFieldQuality;
            _painterOrder = painterOrder;
            _worldShadowMode = worldShadowMode;
            NotifyChanged();
        }

        /// <summary>Sets whether the style keeps Text Studio's exact layered rendering (<see cref="ExactLayering"/>).</summary>
        public void SetExactLayering(bool exactLayering)
        {
            if (_exactLayering == exactLayering)
                return;
            _exactLayering = exactLayering;
            NotifyChanged();
        }

        internal TextStudio2DLayer[] CloneAuthoredLayers()
        {
            return TextStudio2DStyleSettings.CloneLayers(_layers);
        }

        void Reset()
        {
            _schemaVersion = CurrentSchemaVersion;
            _baseStyle = null;
            _inheritanceMode = TextStudio2DStyleInheritanceMode.Append;
            _sourceFieldQuality = TextStudio2DSourceFieldQuality.Automatic;
            _painterOrder = TextStudio2DPainterOrder.LayerMajor;
            _worldShadowMode = TextStudio2DWorldShadowMode.Off;
            _exactLayering = false;
            _layers = new[]
            {
                TextStudio2DLayer.CreateFill("Fill", TextStudio2DPaint.Solid(Color.white))
            };
            NotifyChanged();
        }

        void OnValidate()
        {
            _schemaVersion = CurrentSchemaVersion;
            RepairLayers();
            NotifyChanged();
        }

        void RepairLayers()
        {
            TextStudio2DStyleSettings.RepairLayers(ref _layers, _baseStyle, _inheritanceMode);
            _definitionDomains = TextStudioAppearanceDomains.Build(_layers);
        }

        internal bool TryGetSharedProgram(ulong dependencyRevision, out CompiledTextStudio2DStyle program)
        {
            program = _sharedProgram;
            if (program != null && program.DependencyRevision == dependencyRevision) return true;
            _sharedProgram = null;
            program = null;
            return false;
        }

        internal void PublishSharedProgram(CompiledTextStudio2DStyle program) => _sharedProgram = program;

        // Every tick checks whether an assigned style changed, so resolve its paint sources and textures once per revision instead of walking all layers.
        internal void HashPaintDependencies(ref TextStudioDeterministicHash64 hash)
        {
            if (!ArePaintDependenciesCurrent()) CapturePaintDependencies();
            for (int index = 0; index < _paintTextures.Length; index++)
                TextStudio2DStyleSettings.HashPaintDependency(_paintSources[index], _paintTextures[index], ref hash);
        }

        bool ArePaintDependenciesCurrent()
        {
            if (_paintTextures == null || _paintDependencyRevision != _revision || _paintDependencyBaseKey != ComputePaintDependencyBaseKey()) return false;
            for (int index = 0; index < _paintSources.Length; index++)
            {
                bool present = _paintSources[index] != null;
                // A replaced or destroyed source changes which texture the paint resolves to.
                if (present != _paintSourcePresent[index] || present && _paintSources[index].Revision != _paintSourceRevisions[index]) return false;
            }
            return true;
        }

        void CapturePaintDependencies()
        {
            System.Collections.Generic.List<TextStudio2DPaint> paints = new System.Collections.Generic.List<TextStudio2DPaint>();
            GetSettingsForCompilation().CollectPaintDependencies(paints);
            _paintSources = new TextStudio2DPaintAsset[paints.Count];
            _paintSourcePresent = new bool[paints.Count];
            _paintSourceRevisions = new int[paints.Count];
            _paintTextures = new Texture2D[paints.Count];
            for (int index = 0; index < paints.Count; index++)
            {
                TextStudio2DPaintAsset source = paints[index].Source;
                _paintSources[index] = source;
                _paintSourcePresent[index] = source != null;
                _paintSourceRevisions[index] = source != null ? source.Revision : 0;
                _paintTextures[index] = paints[index].Resolve().Texture;
            }
            _paintDependencyRevision = _revision;
            _paintDependencyBaseKey = ComputePaintDependencyBaseKey();
        }

        // Tagged-domain filtering consults inherited layers, so base styles are part of the captured dependency set.
        ulong ComputePaintDependencyBaseKey()
        {
            TextStudioDeterministicHash64 hash = default;
            if (_inheritanceMode == TextStudio2DStyleInheritanceMode.Replace) return hash.Value;
            TextStudio2DStyleAsset current = _baseStyle;
            for (int depth = 0; current != null && depth < 64; depth++)
            {
                hash.Add(current.GetStableId());
                hash.Add(current.Revision);
                current = current.BaseStyle;
            }
            return hash.Value;
        }

        // Settled texts poll a shared style every frame; one result serves the rest of a player frame while this asset's revision and base chain are unchanged.
        internal bool TryGetFrameDependencyRevision(int frame, out ulong revision)
        {
            revision = _frameDependencyValue;
            return frame == _frameDependencyFrame && _frameDependencyRevision == _revision &&
                   _frameDependencyBaseKey == ComputePaintDependencyBaseKey();
        }

        // Edit Mode records frame -1, so Play Mode never reuses a result computed while editor ticks did not advance frames.
        internal void RecordFrameDependencyRevision(int frame, ulong revision)
        {
            _frameDependencyFrame = frame;
            if (frame < 0) return;
            _frameDependencyRevision = _revision;
            _frameDependencyBaseKey = ComputePaintDependencyBaseKey();
            _frameDependencyValue = revision;
        }

        void OnDisable() => _sharedProgram = null;

        void NotifyChanged()
        {
            _sharedProgram = null;
            _definitionDomains = null;
            unchecked
            {
                _revision++;
            }
            Changed?.Invoke(this);
        }

    }
}
