using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;

namespace TextStudio
{
    /// <summary>One named definition. Invalid and disabled definitions still shadow enclosing names.</summary>
    public sealed class TextStudioAppearanceDefinition
    {
        readonly TextStudio2DStyleSettings _settings;
        readonly TextStudio2DLayer? _container;
        TextStudio2DStyleSettings _wrappedSettings;
        bool _hasWrappedSettings;
        string _diagnostic;
        public string Id { get; }
        public bool IsEnabled { get; }
        public bool IsValid => string.IsNullOrEmpty(_diagnostic);
        public string Diagnostic => _diagnostic ?? string.Empty;
        public TextStudioStackItemId ItemId { get; }
        public TextStudio2DStyleAsset Asset { get; }
        public TextStudio2DStyleAsset OwnerAsset { get; }
        public TextStudioAppearanceApplyMode DefaultApplyMode { get; }
        public ulong Revision { get; }
        public TextStudio2DStyleSettings Settings => CompilationSettings.Clone();
        internal TextStudio2DStyleSettings CompilationSettings
        {
            get
            {
                if (!_container.HasValue) return _settings;
                if (_hasWrappedSettings) return _wrappedSettings;
                List<string> errors = new List<string>();
                TextStudio2DStyleSettings flattened = TextStudio2DStyleCompiler.FlattenAuthoredSettings(in _settings, errors);
                if (errors.Count > 0)
                {
                    _diagnostic = string.Join(" ", errors);
                    _wrappedSettings = _settings;
                    _hasWrappedSettings = true;
                    return _wrappedSettings;
                }
                TextStudio2DLayer root = _container.Value.WithAppearanceActivation(default).WithState(true);
                TextStudio2DLayer[] layers = flattened.CloneAuthoredLayers();
                int[] domains = TextStudioAppearanceDomains.Build(layers);
                TextStudio2DLayer[] wrapped = new TextStudio2DLayer[layers.Length + 1];
                wrapped[0] = root;
                for (int index = 0; index < layers.Length; index++)
                    wrapped[index + 1] = domains[index] < 0 && !layers[index].ParentId.IsValid
                        ? layers[index].WithRelationships(root.ItemId, layers[index].MaskId) : layers[index];
                flattened.SetLayers(wrapped);
                _wrappedSettings = flattened;
                _hasWrappedSettings = true;
                return _wrappedSettings;
            }
        }

        internal TextStudioAppearanceDefinition(string id, bool enabled, string diagnostic,
            TextStudioStackItemId itemId, TextStudio2DStyleAsset asset, TextStudio2DStyleAsset owner,
            TextStudioAppearanceApplyMode mode, TextStudio2DStyleSettings settings, ulong revision, TextStudio2DLayer? container = null)
        {
            Id = id; IsEnabled = enabled; _diagnostic = diagnostic ?? string.Empty;
            ItemId = itemId; Asset = asset; OwnerAsset = owner; DefaultApplyMode = mode; _settings = settings; Revision = revision; _container = container;
        }

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            if (!memory.Visit(this)) return 0;
            return memory.Text(Id) + memory.Text(_diagnostic) + _settings.CaptureManagedPayload(memory) +
                (_hasWrappedSettings ? _wrappedSettings.CaptureManagedPayload(memory) : 0);
        }
    }

    /// <summary>A case-insensitive lookup of resolved appearance definitions with their extraction diagnostics.</summary>
    public sealed class TextStudioAppearanceDefinitionTable
    {
        readonly Dictionary<string, TextStudioAppearanceDefinition> _lookup;
        public IReadOnlyList<TextStudioAppearanceDefinition> Definitions { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        internal TextStudioAppearanceDefinitionTable(List<TextStudioAppearanceDefinition> definitions, List<string> diagnostics)
        {
            Definitions = definitions.AsReadOnly(); Diagnostics = diagnostics.AsReadOnly();
            _lookup = new Dictionary<string, TextStudioAppearanceDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (TextStudioAppearanceDefinition definition in definitions)
                if (!_lookup.ContainsKey(definition.Id)) _lookup.Add(definition.Id, definition);
        }
        /// <summary>Attempts to find an appearance definition by its case-insensitive identifier, including disabled or invalid definitions.</summary>
        public bool TryGet(string id, out TextStudioAppearanceDefinition definition)
        {
            definition = null;
            return id != null && _lookup.TryGetValue(id, out definition);
        }

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            if (!memory.Visit(this)) return 0;
            long bytes = (long)(Definitions.Count + Diagnostics.Count) * IntPtr.Size;
            foreach (TextStudioAppearanceDefinition definition in Definitions) bytes += definition.CaptureManagedPayload(memory);
            foreach (string message in Diagnostics) bytes += memory.Text(message);
            return bytes;
        }
    }

    /// <summary>Extracts named domains without compiling or activating unused definitions.</summary>
    public static class TextStudioAppearanceDefinitions
    {
        internal static TextStudioAppearanceDefinition FromAsset(TextStudio2DStyleAsset asset)
        {
            if (asset == null) return null;
            return new TextStudioAppearanceDefinition(asset.Id, true,
                (uint)asset.DefaultApplyMode <= (uint)TextStudioAppearanceApplyMode.Overlay ? string.Empty : "The default application mode is invalid.", default, asset, asset,
                asset.DefaultApplyMode, asset.GetSettingsForCompilation(), TextStudio2DStyleCompiler.ComputeDependencyRevision(asset));
        }

        /// <summary>Extracts local and inherited tagged appearance definitions with diagnostics, allowing nearer definitions to shadow inherited names.</summary>
        public static TextStudioAppearanceDefinitionTable Resolve(TextStudio2DStyleSettings settings, TextStudio2DStyleAsset ownerAsset = null)
        {
            List<TextStudioAppearanceDefinition> definitions = new List<TextStudioAppearanceDefinition>();
            List<string> diagnostics = new List<string>();
            HashSet<string> resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> path = new HashSet<int>();
            TextStudio2DStyleSettings effective = TextStudio2DStyleCompiler.FlattenAuthoredSettings(in settings, diagnostics);
            TextStudio2DLayer[] effectiveLayers = effective.CloneAuthoredLayers();
            int[] effectiveDomains = TextStudioAppearanceDomains.Build(effectiveLayers);
            ulong resolutionRevision = ownerAsset != null ? TextStudio2DStyleCompiler.ComputeDependencyRevision(ownerAsset)
                : TextStudio2DStyleCompiler.ComputeDependencyRevision(in settings);
            ResolveSettings(settings, ownerAsset, definitions, diagnostics, resolved, path, effectiveLayers, effectiveDomains, in settings, resolutionRevision);
            return new TextStudioAppearanceDefinitionTable(definitions, diagnostics);
        }

        static void ResolveSettings(TextStudio2DStyleSettings settings, TextStudio2DStyleAsset owner,
            List<TextStudioAppearanceDefinition> output, List<string> diagnostics, HashSet<string> resolved, HashSet<int> path,
            TextStudio2DLayer[] effectiveLayers, int[] effectiveDomains, in TextStudio2DStyleSettings domainOwner, ulong resolutionRevision)
        {
            if (path.Count >= 64 || (owner != null && !path.Add(owner.GetStableId())))
            { diagnostics.Add("The appearance definition inheritance graph contains a cycle or exceeds 64 assets."); return; }
            HashSet<string> localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TextStudio2DLayer[] layers = settings.CloneAuthoredLayers();
            for (int index = 0; index < layers.Length; index++)
            {
                TextStudio2DLayer group = layers[index];
                TextStudioAppearanceActivation activation = group.AppearanceActivation;
                if (activation.Mode != TextStudioAppearanceActivationMode.Tagged) continue;
                if (!localNames.Add(activation.Id))
                { diagnostics.Add("Duplicate appearance Tag ID '" + activation.Id + "'. The first definition is used."); continue; }
                int effectiveIndex = -1;
                for (int candidate = 0; candidate < effectiveLayers.Length; candidate++)
                    if (effectiveLayers[candidate].ItemId.Equals(group.ItemId)) { effectiveIndex = candidate; break; }
                if (effectiveIndex < 0 || effectiveLayers[effectiveIndex].AppearanceActivation.Mode != TextStudioAppearanceActivationMode.Tagged ||
                    !string.Equals(effectiveLayers[effectiveIndex].AppearanceActivation.Id, activation.Id, StringComparison.OrdinalIgnoreCase)) continue;
                group = effectiveLayers[effectiveIndex];
                activation = group.AppearanceActivation;
                if (!resolved.Add(activation.Id)) continue;
                string error = string.Empty;
                if (!TextStudioAppearanceActivation.IsValidId(activation.Id)) error = "Assign a valid Tag ID.";
                else if (group.Kind != TextStudio2DLayerKind.Group || group.ParentId.IsValid) error = "Only top-level groups can define an appearance tag.";
                else if ((uint)activation.Source > (uint)TextStudioAppearanceDefinitionSource.Asset) error = "The appearance definition source is invalid.";
                else if ((uint)activation.ApplyMode > (uint)TextStudioAppearanceApplyMode.Overlay) error = "The appearance application mode is invalid.";
                TextStudio2DStyleAsset asset = activation.Source == TextStudioAppearanceDefinitionSource.Asset ? activation.Asset : null;
                TextStudio2DStyleSettings definitionSettings = default;
                TextStudioAppearanceApplyMode mode = activation.ApplyMode;
                if (activation.Source == TextStudioAppearanceDefinitionSource.Asset)
                {
                    if (group.MaskId.IsValid) error = "An asset reference cannot use a mask outside the referenced style.";
                    if (asset == null) error = "Assign an appearance style asset.";
                    else
                    {
                        definitionSettings = asset.GetSettingsForCompilation();
                        if (!activation.OverrideApplyMode) mode = asset.DefaultApplyMode;
                    }
                }
                else
                {
                    definitionSettings = ExtractInline(domainOwner, effectiveLayers, effectiveDomains, effectiveIndex, out string domainError);
                    if (string.IsNullOrEmpty(error)) error = domainError;
                }
                TextStudioDeterministicHash64 hash = default;
                hash.Add(resolutionRevision);
                hash.Add(owner != null ? owner.Revision : settings.Revision);
                hash.Add(index);
                // Asset settings snapshots have no local edit revision. Retain the referenced
                // asset's revision so direct layer, rendering-option and exposure edits expire variants.
                hash.Add(asset != null
                    ? TextStudio2DStyleCompiler.ComputeDependencyRevision(asset)
                    : TextStudio2DStyleCompiler.ComputeDependencyRevision(in definitionSettings));
                output.Add(new TextStudioAppearanceDefinition(activation.Id, group.IsEnabled, error, group.ItemId,
                    asset, owner, mode, definitionSettings, hash.Value,
                    asset != null && !IsIdentityContainer(group, definitionSettings.PainterOrder) ? group : (TextStudio2DLayer?)null));
                if (!string.IsNullOrEmpty(error)) diagnostics.Add(activation.Id + ": " + error);
            }
            if (settings.BaseStyle != null && settings.InheritanceMode != TextStudio2DStyleInheritanceMode.Replace)
                ResolveSettings(settings.BaseStyle.GetSettingsForCompilation(), settings.BaseStyle, output, diagnostics, resolved, path, effectiveLayers, effectiveDomains, in domainOwner, resolutionRevision);
            if (owner != null) path.Remove(owner.GetStableId());
        }

        static TextStudio2DStyleSettings ExtractInline(TextStudio2DStyleSettings settings, TextStudio2DLayer[] layers,
            int[] domains, int rootIndex, out string error)
        {
            error = string.Empty;
            TextStudio2DLayer root = layers[rootIndex];
            TextStudioAppearanceExposure[] exposures = root.AppearanceActivation.Exposures;
            HashSet<TextStudioStackItemId> members = new HashSet<TextStudioStackItemId>();
            for (int index = 0; index < layers.Length; index++) if (domains[index] == rootIndex) members.Add(layers[index].ItemId);
            bool flatten = IsIdentityContainer(root, settings.PainterOrder);
            foreach (TextStudioAppearanceExposure exposure in exposures) if (exposure.LayerId.Equals(root.ItemId)) flatten = false;
            foreach (TextStudio2DLayer layer in layers) if (layer.MaskId.Equals(root.ItemId)) flatten = false;
            List<TextStudio2DLayer> extracted = new List<TextStudio2DLayer>();
            for (int index = 0; index < layers.Length; index++)
            {
                if (domains[index] != rootIndex) continue;
                TextStudio2DLayer layer = layers[index];
                if (index == rootIndex)
                {
                    if (flatten) continue;
                    layer = layer.WithAppearanceActivation(default).WithState(true, false);
                }
                if (layer.MaskId.IsValid && !members.Contains(layer.MaskId)) error = "A tagged style cannot reference a mask outside its definition.";
                if (layer.ParentId.IsValid && !members.Contains(layer.ParentId)) error = "A tagged style cannot reference a parent outside its definition.";
                if (flatten && layer.ParentId.Equals(root.ItemId)) layer = layer.WithRelationships(default, layer.MaskId);
                extracted.Add(layer);
            }
            TextStudio2DStyleSettings result = new TextStudio2DStyleSettings(null, TextStudio2DStyleInheritanceMode.Replace,
                settings.SourceFieldQuality, settings.PainterOrder, settings.WorldShadowMode, extracted.ToArray(),
                exactLayering: settings.ExactLayering);
            result.SetExposures(exposures);
            return result;
        }

        internal static bool IsIdentityContainer(TextStudio2DLayer group, TextStudio2DPainterOrder order)
        {
            bool sameOrder = group.GroupOrder == TextStudio2DGroupOrder.Inherit ||
                (group.GroupOrder == TextStudio2DGroupOrder.ByLayer && order == TextStudio2DPainterOrder.LayerMajor) ||
                (group.GroupOrder == TextStudio2DGroupOrder.ByCharacter && order == TextStudio2DPainterOrder.GlyphMajor);
            return group.Opacity == 1f && group.BlendMode == TextStudio2DBlendMode.Normal && !group.MaskId.IsValid &&
                group.Selector.IsTrivialAll && group.ExecutionPath == TextStudio2DExecutionPath.Automatic &&
                group.QualityTier == TextStudio2DQualityTier.Balanced &&
                group.ParticipatesInAnimatedTint && group.SourceApplicability == TextStudio2DSourceApplicability.Automatic && sameOrder;
        }
    }

    internal static class TextStudioAppearanceDomains
    {
        internal static Dictionary<TextStudioStackItemId, TextStudio2DLayer> Ancestry(TextStudio2DLayer[] local,
            TextStudio2DStyleAsset baseStyle = null, TextStudio2DStyleInheritanceMode mode = TextStudio2DStyleInheritanceMode.Replace)
        {
            Dictionary<TextStudioStackItemId, TextStudio2DLayer> result = new Dictionary<TextStudioStackItemId, TextStudio2DLayer>();
            foreach (TextStudio2DLayer layer in local) result[layer.ItemId] = layer;
            for (int depth = 0; baseStyle != null && mode != TextStudio2DStyleInheritanceMode.Replace && depth < 64; depth++)
            {
                for (int index = 0; index < baseStyle.ItemCount; index++)
                {
                    TextStudio2DLayer layer = baseStyle.GetLayer(index);
                    if (!result.ContainsKey(layer.ItemId)) result.Add(layer.ItemId, layer);
                }
                mode = baseStyle.InheritanceMode;
                baseStyle = baseStyle.BaseStyle;
            }
            return result;
        }

        internal static TextStudioStackItemId Domain(in TextStudio2DLayer layer, Dictionary<TextStudioStackItemId, TextStudio2DLayer> ancestry)
        {
            TextStudio2DLayer cursor = ancestry.TryGetValue(layer.ItemId, out TextStudio2DLayer effective) ? effective : layer;
            for (int depth = 0; depth <= ancestry.Count; depth++)
            {
                if (cursor.IsTagDefinition) return cursor.ItemId;
                TextStudioStackItemId parent = cursor.ParentId;
                if (!parent.IsValid) return default;
                // Preserve separate unresolved parent domains until inheritance is assigned or diagnosed.
                if (!ancestry.TryGetValue(parent, out cursor)) return parent;
            }
            return default;
        }

        // Built only at authoring/import boundaries; default dependency polling reuses this map.
        internal static int[] Build(TextStudio2DLayer[] layers)
        {
            if (layers == null || layers.Length == 0) return Array.Empty<int>();
            Dictionary<TextStudioStackItemId, int> lookup = new Dictionary<TextStudioStackItemId, int>();
            for (int index = 0; index < layers.Length; index++) if (!lookup.ContainsKey(layers[index].ItemId)) lookup.Add(layers[index].ItemId, index);
            int[] domains = new int[layers.Length];
            for (int index = 0; index < layers.Length; index++)
            {
                domains[index] = -1;
                int cursor = index;
                for (int depth = 0; depth <= layers.Length; depth++)
                {
                    if (layers[cursor].IsTagDefinition) { domains[index] = cursor; break; }
                    TextStudioStackItemId parent = layers[cursor].ParentId;
                    if (!parent.IsValid || !lookup.TryGetValue(parent, out cursor)) break;
                }
            }
            return domains;
        }
        internal static bool IsDormant(TextStudio2DLayer[] layers, int index)
        {
            for (int depth = 0; depth <= layers.Length; depth++)
            {
                if (layers[index].IsTagDefinition) return true;
                TextStudioStackItemId parent = layers[index].ParentId;
                if (!parent.IsValid) return false;
                int parentIndex = -1;
                for (int candidate = 0; candidate < layers.Length; candidate++)
                    if (layers[candidate].ItemId.Equals(parent)) { parentIndex = candidate; break; }
                if (parentIndex < 0) return false;
                index = parentIndex;
            }
            return false;
        }
        internal static TextStudio2DLayer[] DefaultLayers(TextStudio2DLayer[] layers)
        {
            int[] domains = Build(layers);
            List<TextStudio2DLayer> result = new List<TextStudio2DLayer>(layers.Length);
            for (int index = 0; index < layers.Length; index++) if (domains[index] < 0) result.Add(layers[index]);
            return result.ToArray();
        }
    }
}
