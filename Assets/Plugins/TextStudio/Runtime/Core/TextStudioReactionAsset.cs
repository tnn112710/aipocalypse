using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Renderer-neutral reaction configuration selected by a host or behavior profile.</summary>
    [Serializable]
    public struct TextStudioReactionRecipe : IEquatable<TextStudioReactionRecipe>
    {
        public TextStudioReactionSource Source;
        public string BuiltInPresetId;
        public TextStudioReactionAsset CustomAsset;
        public TextStudioReactionLayer[] InlineLayers;
        [SerializeField, Range(1, 4096)] int _inlineCapacity;
        [SerializeField, Min(1)] int _inlineSeed;

        public int InlineCapacity => Mathf.Clamp(_inlineCapacity == 0 ? 64 : _inlineCapacity, 1, 4096);
        public uint InlineSeed => (uint)Mathf.Max(1, _inlineSeed);
        public uint Seed => Source == TextStudioReactionSource.Custom && CustomAsset != null
            ? CustomAsset.Seed
            : Source == TextStudioReactionSource.BuiltIn
                ? BuiltIns.BuiltInReactionPresets.ForId(BuiltInPresetId)?.Seed ?? InlineSeed
                : InlineSeed;

        public static TextStudioReactionRecipe None => new TextStudioReactionRecipe
        {
            Source = TextStudioReactionSource.None,
            BuiltInPresetId = string.Empty,
            InlineLayers = Array.Empty<TextStudioReactionLayer>(),
            _inlineCapacity = 64,
            _inlineSeed = 1
        };

        /// <summary>Creates a built-in recipe reference, using the default preset when the supplied identifier is empty.</summary>
        public static TextStudioReactionRecipe BuiltIn(string presetId)
        {
            TextStudioReactionRecipe recipe = None;
            recipe.Source = TextStudioReactionSource.BuiltIn;
            recipe.BuiltInPresetId = string.IsNullOrEmpty(presetId)
                ? BuiltIns.BuiltInReactionPresets.Default.Id
                : presetId;
            return recipe;
        }

        /// <summary>Creates a reaction recipe referencing the supplied custom asset.</summary>
        public static TextStudioReactionRecipe Custom(TextStudioReactionAsset asset)
        {
            TextStudioReactionRecipe recipe = None;
            recipe.Source = TextStudioReactionSource.Custom;
            recipe.CustomAsset = asset;
            return recipe;
        }

        /// <summary>Creates an inline recipe from the supplied layer array, clamping response capacity and normalizing its seed.</summary>
        public static TextStudioReactionRecipe Inline(TextStudioReactionLayer[] layers, int capacity = 64, uint seed = 1)
        {
            TextStudioReactionRecipe recipe = None;
            recipe.Source = TextStudioReactionSource.Inline;
            recipe.InlineLayers = layers ?? Array.Empty<TextStudioReactionLayer>();
            recipe._inlineCapacity = Mathf.Clamp(capacity, 1, 4096);
            recipe._inlineSeed = seed > int.MaxValue ? int.MaxValue : (int)Math.Max(1u, seed);
            return recipe;
        }

        internal void Resolve(out IReadOnlyList<TextStudioReactionLayer> layers, out int capacity)
        {
            capacity = InlineCapacity;
            switch (Source)
            {
                case TextStudioReactionSource.Custom:
                    layers = CustomAsset != null ? CustomAsset.Layers : Array.Empty<TextStudioReactionLayer>();
                    capacity = CustomAsset != null ? CustomAsset.Capacity : 64;
                    break;
                case TextStudioReactionSource.Inline:
                    layers = InlineLayers ?? Array.Empty<TextStudioReactionLayer>();
                    break;
                case TextStudioReactionSource.BuiltIn:
                    string presetId = string.IsNullOrEmpty(BuiltInPresetId)
                        ? BuiltIns.BuiltInReactionPresets.Default.Id
                        : BuiltInPresetId;
                    BuiltIns.TextStudioBuiltInReactionPreset? preset =
                        BuiltIns.BuiltInReactionPresets.ForId(presetId);
                    layers = preset.HasValue ? preset.Value.RuntimeLayers : Array.Empty<TextStudioReactionLayer>();
                    capacity = preset.HasValue ? preset.Value.Capacity : 64;
                    break;
                default:
                    layers = Array.Empty<TextStudioReactionLayer>();
                    break;
            }
        }

        internal bool HasOutgoingCaptureLayer()
        {
            if (Source == TextStudioReactionSource.None)
                return false;
            Resolve(out IReadOnlyList<TextStudioReactionLayer> layers, out _);
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioReactionLayer layer = layers[i];
                if (layer != null && layer.Enabled &&
                    layer.When == TextStudioReactionTrigger.TextChanged &&
                    layer.Which == TextStudioReactionTarget.ChangedOutgoingUnits &&
                    layer.Action == TextStudioReactionAction.Custom && layer.CustomAction != null)
                    return true;
            }
            return false;
        }

        internal void SetInlineCapacity(int capacity)
        {
            _inlineCapacity = Mathf.Clamp(capacity, 1, 4096);
        }

        internal void SetInlineSeed(int seed)
        {
            _inlineSeed = Mathf.Max(1, seed);
        }

        internal int GetContentSignature()
        {
            unchecked
            {
                int hash = (int)Source;
                if (Source == TextStudioReactionSource.BuiltIn)
                {
                    hash = (hash * 397) ^ (BuiltInPresetId ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ unchecked((int)
                        BuiltIns.BuiltInReactionPresets.CatalogRevision);
                    return (hash * 397) ^ unchecked((int)
                        (BuiltIns.BuiltInReactionPresets.ForId(BuiltInPresetId)?.Revision ?? 0u));
                }
                if (Source == TextStudioReactionSource.Custom)
                {
                    hash = (hash * 397) ^ (CustomAsset != null ? CustomAsset.GetHashCode() : 0);
                    return (hash * 397) ^ (CustomAsset != null ? CustomAsset.Revision.GetHashCode() : 0);
                }
                if (Source != TextStudioReactionSource.Inline)
                    return hash;
                hash = (hash * 397) ^ InlineCapacity;
                hash = (hash * 397) ^ InlineSeed.GetHashCode();
                if (InlineLayers == null)
                    return hash;
                for (int i = 0; i < InlineLayers.Length; i++)
                {
                    TextStudioReactionLayer layer = InlineLayers[i];
                    if (layer == null)
                    {
                        hash = (hash * 397) ^ 0;
                        continue;
                    }
                    hash = (hash * 397) ^ (layer.Id ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ (layer.Name ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ layer.Enabled.GetHashCode();
                    hash = (hash * 397) ^ (int)layer.When;
                    hash = (hash * 397) ^ (layer.TriggerName ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ (int)layer.Which;
                    hash = (hash * 397) ^ (int)layer.Action;
                    hash = (hash * 397) ^ (int)layer.EffectSource;
                    hash = (hash * 397) ^ (layer.Effect != null ? layer.Effect.GetHashCode() : 0);
                    hash = (hash * 397) ^ (layer.EffectId ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ (layer.Signal ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ (layer.AudioCue ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ (layer.CustomAction != null ? layer.CustomAction.GetHashCode() : 0);
                    hash = (hash * 397) ^ (layer.TargetBindingKey ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ (layer.EnabledInputKey ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ layer.Delay.GetHashCode();
                    hash = (hash * 397) ^ layer.Duration.GetHashCode();
                    hash = (hash * 397) ^ layer.Intensity.GetHashCode();
                    hash = (hash * 397) ^ layer.Chance.GetHashCode();
                    hash = (hash * 397) ^ layer.Cooldown.GetHashCode();
                    hash = (hash * 397) ^ (int)layer.Overlap;
                    hash = (hash * 397) ^ (layer.DependsOnId ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ (int)layer.DependencyEvent;
                }
                return hash;
            }
        }

        internal int GetRuntimeSignature(uint serializedRevision)
        {
            if (Source != TextStudioReactionSource.Inline)
                return GetContentSignature();

            unchecked
            {
                int hash = (int)Source;
                hash = (hash * 397) ^ InlineCapacity;
                hash = (hash * 397) ^ InlineSeed.GetHashCode();
                hash = (hash * 397) ^ serializedRevision.GetHashCode();
                TextStudioReactionLayer[] layers = InlineLayers;
                if (layers == null)
                    return hash;
                hash = (hash * 397) ^ layers.Length;
                for (int i = 0; i < layers.Length; i++)
                {
                    TextStudioReactionLayer layer = layers[i];
                    hash = (hash * 397) ^ (layer != null ? layer.GetHashCode() : 0);
                    hash = (hash * 397) ^ (layer != null ? layer.Revision.GetHashCode() : 0);
                }
                return hash;
            }
        }

        public bool Equals(TextStudioReactionRecipe other)
        {
            if (Source != other.Source)
                return false;
            switch (Source)
            {
                case TextStudioReactionSource.BuiltIn:
                    return string.Equals(BuiltInPresetId ?? string.Empty,
                        other.BuiltInPresetId ?? string.Empty, StringComparison.Ordinal);
                case TextStudioReactionSource.Custom:
                    return CustomAsset == other.CustomAsset;
                case TextStudioReactionSource.Inline:
                    return InlineLayers == other.InlineLayers &&
                           InlineCapacity == other.InlineCapacity && InlineSeed == other.InlineSeed;
                default:
                    return true;
            }
        }

        public override bool Equals(object obj) => obj is TextStudioReactionRecipe other && Equals(other);
        public override int GetHashCode() => GetContentSignature();
    }

    /// <summary>Reusable reaction definitions. Live state and scene bindings belong to each host.</summary>
    [CreateAssetMenu(fileName = "Text Studio Reactions", menuName = "Text Studio/Reactions/Reaction Set", order = 104)]
    public sealed class TextStudioReactionAsset : ScriptableObject
    {
        [SerializeField] TextStudioReactionLayer[] _layers = Array.Empty<TextStudioReactionLayer>();
        [SerializeField, Range(1, 4096)] int _capacity = 64;
        [SerializeField, Min(1)] int _seed = 1;
        [NonSerialized] uint _revision = 1;
        [NonSerialized] List<TextStudioReactionLayer> _observedLayers;

        public IReadOnlyList<TextStudioReactionLayer> Layers => _layers;
        public int Capacity => Mathf.Clamp(_capacity, 1, 4096);
        public uint Seed => (uint)Mathf.Max(1, _seed);
        internal uint Revision => _revision;

        void OnEnable() => ObserveLayers();
        void OnDisable() => UnobserveLayers();

        void OnValidate()
        {
            unchecked { _revision++; }
            if (_revision == 0) _revision = 1;
            ObserveLayers();
        }

        void ObserveLayers()
        {
            UnobserveLayers();
            if (_layers == null || _layers.Length == 0)
                return;
            _observedLayers = new List<TextStudioReactionLayer>(_layers.Length);
            for (int i = 0; i < _layers.Length; i++)
            {
                TextStudioReactionLayer layer = _layers[i];
                if (layer == null || _observedLayers.Contains(layer))
                    continue;
                layer.Changed += HandleLayerChanged;
                _observedLayers.Add(layer);
            }
        }

        void UnobserveLayers()
        {
            if (_observedLayers == null)
                return;
            for (int i = 0; i < _observedLayers.Count; i++)
                _observedLayers[i].Changed -= HandleLayerChanged;
            _observedLayers.Clear();
        }

        void HandleLayerChanged(TextStudioReactionLayer layer)
        {
            unchecked { _revision++; }
            if (_revision == 0) _revision = 1;
        }

        internal void InitializeForTests(TextStudioReactionLayer[] layers, int capacity = 64)
        {
            _layers = layers ?? Array.Empty<TextStudioReactionLayer>();
            _capacity = capacity;
            OnValidate();
        }
    }
}

namespace TextStudio.BuiltIns
{
    /// <summary>A curated reusable response stack compiled without an asset load.</summary>
    public readonly struct TextStudioBuiltInReactionPreset
    {
        readonly TextStudioReactionLayer[] _layers;
        readonly string _resourcePath;
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        /// <summary>Text outputs this recipe is designed for. Renderer Independent recipes suit every host.</summary>
        public TextStudioReactionOutputKinds Outputs { get; }
        public IReadOnlyList<TextStudioReactionLayer> Layers => CloneLayers();
        internal IReadOnlyList<TextStudioReactionLayer> RuntimeLayers =>
            _resourcePath != null
                ? BuiltInReactionPresets.LoadAsset(_resourcePath)?.Layers ??
                  Array.Empty<TextStudioReactionLayer>()
                : _layers;
        internal uint Revision => _resourcePath != null
            ? BuiltInReactionPresets.LoadAsset(_resourcePath)?.Revision ?? 0u : 0u;
        public int Capacity => _resourcePath != null
            ? BuiltInReactionPresets.LoadAsset(_resourcePath)?.Capacity ?? 64 : 64;
        public uint Seed => _resourcePath != null
            ? BuiltInReactionPresets.LoadAsset(_resourcePath)?.Seed ?? 1u : 1u;

        internal TextStudioBuiltInReactionPreset(string id, string displayName, string description,
            TextStudioReactionLayer[] layers)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Outputs = TextStudioReactionOutputKinds.RendererIndependent;
            _layers = layers ?? Array.Empty<TextStudioReactionLayer>();
            _resourcePath = null;
        }

        internal TextStudioBuiltInReactionPreset(TextStudioReactionBuiltInLibrary.Entry entry)
        {
            Id = entry.Id;
            DisplayName = entry.DisplayName;
            Description = entry.Description;
            Outputs = entry.Outputs;
            _layers = null;
            _resourcePath = entry.ResourcePath;
        }

        /// <summary>Whether this recipe suits a host whose configured outputs are <paramref name="hostOutputs"/>.</summary>
        internal bool SuitsOutputs(TextStudioReactionOutputKinds hostOutputs) =>
            Outputs == TextStudioReactionOutputKinds.RendererIndependent || (Outputs & hostOutputs) != 0;

        internal TextStudioReactionLayer[] CloneLayers()
        {
            return TextStudioReactionLayer.CloneStack(RuntimeLayers);
        }
    }

    /// <summary>Curated reaction starting points. Convert a selection to Inline for per-host edits.</summary>
#if UNITY_6000_7_OR_NEWER
    // Immutable presets and editor catalog revisions use explicit invalidation, not a new Play Mode identity.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class BuiltInReactionPresets
    {
        public const string RewardImpactId = "reaction.reward-impact";
        public const string DamageJoltId = "reaction.damage-jolt";
        public const string CriticalHitId = "reaction.critical-hit";
        public const string CounterTickId = "reaction.counter-tick";
        public const string QuestCompleteId = "reaction.quest-complete";

        static readonly TextStudioBuiltInReactionPreset[] s_presets =
        {
            new TextStudioBuiltInReactionPreset(RewardImpactId, "Reward Impact",
                "Pulse the complete phrase when its reveal finishes.", new[]
                {
                    TextStudioReactionLayer.CreateBuiltIn("Impact Pulse", TextStudioReactionTrigger.ShowComplete,
                        TextStudioBuiltInEffectIds.Pulse, TextStudioReactionTarget.Text, .32f, .8f,
                        TextStudioReactionOverlap.Ignore)
                }),
            new TextStudioBuiltInReactionPreset(DamageJoltId, "Damage Jolt",
                "Play a short jolt when gameplay sends the named damage signal.", new[]
                {
                    TextStudioReactionLayer.CreateBuiltIn("Damage Jolt", TextStudioReactionTrigger.Signal,
                        TextStudioBuiltInEffectIds.Shake, TextStudioReactionTarget.Text, .2f, .7f,
                        TextStudioReactionOverlap.Restart, "damage")
                }),
            new TextStudioBuiltInReactionPreset(CriticalHitId, "Critical Hit",
                "Combine a quick jolt and color pulse when gameplay sends a critical signal.", new[]
                {
                    TextStudioReactionLayer.CreateBuiltIn("Critical Jolt", TextStudioReactionTrigger.Signal,
                        TextStudioBuiltInEffectIds.Shake, TextStudioReactionTarget.Text, .28f, .9f,
                        TextStudioReactionOverlap.Restart, "critical"),
                    TextStudioReactionLayer.CreateBuiltIn("Critical Pulse", TextStudioReactionTrigger.Signal,
                        TextStudioBuiltInEffectIds.ColorPulse, TextStudioReactionTarget.Text, .42f, .8f,
                        TextStudioReactionOverlap.Ignore, "critical")
                }),
            new TextStudioBuiltInReactionPreset(CounterTickId, "Counter Tick",
                "Give named counter markers a small, readable pop.", new[]
                {
                    TextStudioReactionLayer.CreateBuiltIn("Counter Pop", TextStudioReactionTrigger.Marker,
                        TextStudioBuiltInEffectIds.Pop, TextStudioReactionTarget.Text, .18f, .5f,
                        TextStudioReactionOverlap.Ignore, "counter")
                }),
            new TextStudioBuiltInReactionPreset(QuestCompleteId, "Quest Complete",
                "Pulse the phrase and add a brief color accent when its reveal completes.", new[]
                {
                    TextStudioReactionLayer.CreateBuiltIn("Completion Pulse", TextStudioReactionTrigger.ShowComplete,
                        TextStudioBuiltInEffectIds.Pulse, TextStudioReactionTarget.Text, .48f, .85f,
                        TextStudioReactionOverlap.Ignore),
                    TextStudioReactionLayer.CreateBuiltIn("Completion Color", TextStudioReactionTrigger.ShowComplete,
                        TextStudioBuiltInEffectIds.ColorPulse, TextStudioReactionTarget.Text, .64f, .65f,
                        TextStudioReactionOverlap.Ignore)
                })
        };
        static TextStudioBuiltInReactionPreset[] s_all;
        static IReadOnlyList<TextStudioBuiltInReactionPreset> s_readOnlyPresets;
        static Dictionary<string, TextStudioReactionAsset> s_loadedAssets;
        static uint s_catalogRevision;

        internal static uint CatalogRevision => s_catalogRevision;
        internal static int LoadedAssetCount => s_loadedAssets?.Count ?? 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCatalog() => Invalidate();

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseCatalog() => Invalidate();
#endif

        internal static void Invalidate()
        {
            s_all = null;
            s_readOnlyPresets = null;
            s_loadedAssets?.Clear();
            unchecked { s_catalogRevision++; }
        }

        internal static TextStudioReactionAsset LoadAsset(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            s_loadedAssets ??= new Dictionary<string, TextStudioReactionAsset>(StringComparer.Ordinal);
            if (s_loadedAssets.TryGetValue(resourcePath, out TextStudioReactionAsset asset))
                return asset;
            asset = Resources.Load<TextStudioReactionAsset>(resourcePath);
            if (asset == null)
                Debug.LogError("Missing built-in Text Studio reaction recipe: " + resourcePath);
            s_loadedAssets.Add(resourcePath, asset);
            return asset;
        }

        static TextStudioBuiltInReactionPreset[] GetAll()
        {
            if (s_all != null) return s_all;
            TextStudioReactionBuiltInLibrary[] libraries =
                Resources.LoadAll<TextStudioReactionBuiltInLibrary>("TextStudio/ReactionPresets");
            Array.Sort(libraries, (a, b) => StringComparer.Ordinal.Compare(a.name, b.name));
            List<TextStudioBuiltInReactionPreset> result =
                new List<TextStudioBuiltInReactionPreset>(s_presets.Length + libraries.Length * 4);
            result.AddRange(s_presets);
            for (int libraryIndex = 0; libraryIndex < libraries.Length; libraryIndex++)
            {
                IReadOnlyList<TextStudioReactionBuiltInLibrary.Entry> entries =
                    libraries[libraryIndex].Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    TextStudioReactionBuiltInLibrary.Entry entry = entries[entryIndex];
                    if (string.IsNullOrWhiteSpace(entry.Id) ||
                        string.IsNullOrWhiteSpace(entry.ResourcePath)) continue;
                    bool duplicate = false;
                    for (int existing = 0; existing < result.Count; existing++)
                        if (string.Equals(result[existing].Id, entry.Id, StringComparison.Ordinal))
                        { duplicate = true; break; }
                    if (duplicate)
                    {
                        Debug.LogError("Duplicate built-in Text Studio reaction ID: " + entry.Id);
                        continue;
                    }
                    result.Add(new TextStudioBuiltInReactionPreset(entry));
                }
            }
            s_all = result.ToArray();
            s_readOnlyPresets = Array.AsReadOnly(s_all);
            return s_all;
        }

        public static TextStudioBuiltInReactionPreset Default => s_presets[0];
        /// <summary>Returns the read-only catalog of available built-in reaction presets.</summary>
        public static IReadOnlyList<TextStudioBuiltInReactionPreset> All()
        {
            GetAll();
            return s_readOnlyPresets;
        }

        /// <summary>Finds a built-in reaction preset by its exact identifier, or returns null when no preset matches.</summary>
        public static TextStudioBuiltInReactionPreset? ForId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            TextStudioBuiltInReactionPreset[] presets = GetAll();
            for (int i = 0; i < presets.Length; i++)
                if (string.Equals(presets[i].Id, id, StringComparison.Ordinal))
                    return presets[i];
            return null;
        }

        /// <summary>Clones the layers of a built-in preset, or returns an empty array when its identifier is unknown.</summary>
        public static TextStudioReactionLayer[] CopyLayers(string id)
        {
            TextStudioBuiltInReactionPreset? preset = ForId(id);
            if (!preset.HasValue)
                return Array.Empty<TextStudioReactionLayer>();
            return preset.Value.CloneLayers();
        }
    }
}
