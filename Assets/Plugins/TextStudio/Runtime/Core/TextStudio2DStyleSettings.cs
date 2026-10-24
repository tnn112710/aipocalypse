using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Chooses the owner of a text component's persistent 2D appearance.</summary>
    public enum TextStudioAppearanceSource
    {
        Inline,
        Style
    }

    /// <summary>Serializable appearance settings with independently owned layer data.</summary>
    [Serializable]
    public struct TextStudio2DStyleSettings
    {
        [SerializeField] TextStudio2DStyleAsset _baseStyle;
        [SerializeField] TextStudio2DStyleInheritanceMode _inheritanceMode;
        [SerializeField] TextStudio2DSourceFieldQuality _sourceFieldQuality;
        [SerializeField] TextStudio2DPainterOrder _painterOrder;
        [SerializeField] TextStudio2DWorldShadowMode _worldShadowMode;
        [SerializeField] bool _exactLayering;
        [SerializeField] TextStudio2DLayer[] _layers;
        [SerializeField] TextStudioAppearanceExposure[] _exposures;
        [NonSerialized] int _revision;
        [NonSerialized] int[] _definitionDomains;

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
        internal bool IsEmpty => ItemCount == 0 && _baseStyle == null && (_exposures == null || _exposures.Length == 0);

        /// <summary>No default treatment; named appearances remain available.</summary>
        public static TextStudio2DStyleSettings Empty => default;
        public TextStudioAppearanceExposure[] Exposures => TextStudioAppearanceActivation.CloneExposures(_exposures);
        internal int Revision => _revision;

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            long bytes = memory.Array(_layers) + memory.Array(_exposures) + memory.Array(_definitionDomains);
            if (_exposures != null) foreach (TextStudioAppearanceExposure exposure in _exposures) bytes += memory.Text(exposure.Name);
            if (_layers != null)
                foreach (TextStudio2DLayer layer in _layers)
                    bytes += layer.Paint.CaptureManagedPayload(memory) + layer.Stroke.CaptureManagedPayload(memory) + layer.Decoration.CaptureManagedPayload(memory);
            return bytes;
        }

        internal void HashPaintDependencies(ref TextStudioDeterministicHash64 hash)
        {
            for (int index = 0; index < ItemCount; index++)
            {
                if (IsLayerInTaggedDomain(index)) continue;
                HashPaintDependency(_layers[index].Paint, ref hash);
                TextStudio2DDecorationSpec decoration = _layers[index].Decoration;
                for (int treatmentIndex = 0; treatmentIndex < decoration.TreatmentCount; treatmentIndex++)
                    HashPaintDependency(decoration.GetTreatmentUnsafe(treatmentIndex).Paint, ref hash);
            }
        }

        /// <summary>Appends the paints <see cref="HashPaintDependencies"/> hashes, in the same order.</summary>
        internal void CollectPaintDependencies(List<TextStudio2DPaint> paints)
        {
            for (int index = 0; index < ItemCount; index++)
            {
                if (IsLayerInTaggedDomain(index)) continue;
                paints.Add(_layers[index].Paint);
                TextStudio2DDecorationSpec decoration = _layers[index].Decoration;
                for (int treatmentIndex = 0; treatmentIndex < decoration.TreatmentCount; treatmentIndex++)
                    paints.Add(decoration.GetTreatmentUnsafe(treatmentIndex).Paint);
            }
        }

        bool IsLayerInTaggedDomain(int index)
        {
            if (_definitionDomains != null && _definitionDomains.Length == ItemCount && _definitionDomains[index] >= 0) return true;
            if (_baseStyle == null || _inheritanceMode == TextStudio2DStyleInheritanceMode.Replace)
                return _definitionDomains == null || _definitionDomains.Length != ItemCount ? TextStudioAppearanceDomains.IsDormant(_layers, index) : false;
            TextStudio2DLayer layer = _layers[index];
            for (int depth = 0; depth < 64; depth++)
            {
                if (layer.IsTagDefinition) return true;
                if (!layer.ParentId.IsValid || !TryFindInheritedLayer(layer.ParentId, out layer)) return false;
            }
            return false;
        }

        bool TryFindInheritedLayer(TextStudioStackItemId identity, out TextStudio2DLayer layer)
        {
            TextStudio2DStyleSettings current = this;
            for (int depth = 0; depth < 64; depth++)
            {
                for (int index = 0; index < current.ItemCount; index++)
                    if (current._layers[index].ItemId.Equals(identity)) { layer = current._layers[index]; return true; }
                if (current._baseStyle == null || current._inheritanceMode == TextStudio2DStyleInheritanceMode.Replace) break;
                current = current._baseStyle.GetSettingsForCompilation();
            }
            layer = default;
            return false;
        }

        static void HashPaintDependency(TextStudio2DPaint paint, ref TextStudioDeterministicHash64 hash) =>
            HashPaintDependency(paint.Source, paint.Resolve().Texture, ref hash);

        internal static void HashPaintDependency(TextStudio2DPaintAsset source, Texture2D texture, ref TextStudioDeterministicHash64 hash)
        {
            hash.Add(source != null);
            if (source != null) hash.Add(source.Revision);
            hash.Add(texture != null);
            if (texture != null) TextStudioTextureContent.HashRevision(texture, ref hash);
        }

        /// <summary>A new editable white fill, matching a newly created 2D Style asset.</summary>
        public static TextStudio2DStyleSettings Default
        {
            get
            {
                TextStudio2DStyleSettings settings = default;
                settings.SetLayers(new[]
                {
                    TextStudio2DLayer.CreateFill("Fill", TextStudio2DPaint.Solid(Color.white))
                });
                return settings;
            }
        }

        internal TextStudio2DStyleSettings(
            TextStudio2DStyleAsset baseStyle,
            TextStudio2DStyleInheritanceMode inheritanceMode,
            TextStudio2DSourceFieldQuality sourceFieldQuality,
            TextStudio2DPainterOrder painterOrder,
            TextStudio2DWorldShadowMode worldShadowMode,
            TextStudio2DLayer[] ownedLayers,
            TextStudioAppearanceExposure[] ownedExposures = null,
            int[] definitionDomains = null,
            bool exactLayering = false)
        {
            _baseStyle = baseStyle;
            _inheritanceMode = inheritanceMode;
            _sourceFieldQuality = sourceFieldQuality;
            _painterOrder = painterOrder;
            _worldShadowMode = worldShadowMode;
            _exactLayering = exactLayering;
            _layers = ownedLayers;
            _exposures = ownedExposures ?? Array.Empty<TextStudioAppearanceExposure>();
            _definitionDomains = definitionDomains ?? TextStudioAppearanceDomains.Build(ownedLayers);
            _revision = 0;
        }

        /// <summary>Returns a defensive copy of one authored layer.</summary>
        public TextStudio2DLayer GetLayer(int index)
        {
            if (index < 0 || index >= ItemCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _layers[index].Clone();
        }

        /// <summary>Replaces the layer stack with a defensive deep copy.</summary>
        public void SetLayers(TextStudio2DLayer[] layers)
        {
            _layers = CloneLayers(layers);
            RepairLayers(ref _layers, _baseStyle, _inheritanceMode);
            _definitionDomains = TextStudioAppearanceDomains.Build(_layers);
            unchecked { _revision++; }
        }

        /// <summary>Sets the base style and inheritance mode, repairs local layer identities and solo states, and advances the settings revision.</summary>
        public void SetInheritance(TextStudio2DStyleAsset baseStyle, TextStudio2DStyleInheritanceMode mode)
        {
            _baseStyle = baseStyle;
            _inheritanceMode = mode;
            _layers = CloneLayers(_layers);
            RepairLayers(ref _layers, _baseStyle, _inheritanceMode);
            _definitionDomains = TextStudioAppearanceDomains.Build(_layers);
            unchecked { _revision++; }
        }

        /// <summary>Sets source-field quality, painter ordering, and world-shadow behavior, then advances the settings revision.</summary>
        public void SetRenderingOptions(
            TextStudio2DSourceFieldQuality sourceFieldQuality,
            TextStudio2DPainterOrder painterOrder,
            TextStudio2DWorldShadowMode worldShadowMode)
        {
            _sourceFieldQuality = sourceFieldQuality;
            _painterOrder = painterOrder;
            _worldShadowMode = worldShadowMode;
            unchecked { _revision++; }
        }

        /// <summary>Sets whether the style keeps Text Studio's exact layered rendering (<see cref="ExactLayering"/>), then advances the settings revision.</summary>
        public void SetExactLayering(bool exactLayering)
        {
            _exactLayering = exactLayering;
            unchecked { _revision++; }
        }

        internal TextStudio2DStyleSettings Clone()
        {
            TextStudio2DStyleSettings clone = this;
            clone._layers = CloneLayers(_layers);
            clone._exposures = TextStudioAppearanceActivation.CloneExposures(_exposures);
            return clone;
        }

        /// <summary>Replaces the settings exposure declarations with independent copies and advances the settings revision.</summary>
        public void SetExposures(TextStudioAppearanceExposure[] exposures)
        {
            _exposures = TextStudioAppearanceActivation.CloneExposures(exposures);
            unchecked { _revision++; }
        }

        internal TextStudio2DLayer[] CloneAuthoredLayers() => CloneLayers(_layers);

        internal void Validate()
        {
            // A copied value can share its old array until a mutator is used.
            _layers = CloneLayers(_layers);
            RepairLayers(ref _layers, _baseStyle, _inheritanceMode);
            _definitionDomains = TextStudioAppearanceDomains.Build(_layers);
            unchecked { _revision++; }
        }

        internal static TextStudio2DLayer[] CloneLayers(TextStudio2DLayer[] layers)
        {
            if (layers == null || layers.Length == 0)
                return Array.Empty<TextStudio2DLayer>();
            TextStudio2DLayer[] clone = new TextStudio2DLayer[layers.Length];
            for (int i = 0; i < layers.Length; i++)
                clone[i] = layers[i].Clone();
            return clone;
        }

        internal static void RepairLayers(ref TextStudio2DLayer[] layers, TextStudio2DStyleAsset baseStyle = null,
            TextStudio2DStyleInheritanceMode inheritanceMode = TextStudio2DStyleInheritanceMode.Replace)
        {
            if (layers == null)
            {
                layers = Array.Empty<TextStudio2DLayer>();
                return;
            }
            HashSet<TextStudioStackItemId> identities = new HashSet<TextStudioStackItemId>();
            Dictionary<TextStudioStackItemId, TextStudio2DLayer> ancestry = TextStudioAppearanceDomains.Ancestry(layers, baseStyle, inheritanceMode);
            HashSet<TextStudioStackItemId> soloDomains = new HashSet<TextStudioStackItemId>();
            for (int i = 0; i < layers.Length; i++)
            {
                TextStudio2DLayer layer = layers[i];
                bool keepSolo = layer.IsEnabled && layer.IsSolo && soloDomains.Add(TextStudioAppearanceDomains.Domain(in layer, ancestry));
                if (layer.IsSolo != keepSolo)
                    layers[i] = layer = layer.WithState(layer.IsEnabled, keepSolo);
                TextStudioStackItemId identity = layer.ItemId;
                if (identity.IsValid && identities.Add(identity))
                    continue;
                do
                {
                    identity = TextStudioStackItemId.CreateUnique("layer");
                }
                while (!identities.Add(identity));
                layers[i] = layer.WithRepairedIdentity(identity);
            }
            for (int i = 0; i < layers.Length; i++)
                layers[i] = layers[i].WithRepairedTreatmentIdentities(identities);
        }
    }
}
