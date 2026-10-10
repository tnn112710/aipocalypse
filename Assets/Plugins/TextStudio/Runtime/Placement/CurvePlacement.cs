using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Bends selected glyph baselines while preserving their source x spacing and line breaks.</summary>
    [Serializable]
    public sealed class CurvePlacement : IPlacementContextStrategy
    {
        [SerializeField] TextStudioCurveReference _curve = new TextStudioCurveReference();
        [SerializeField] float _height = 0.5f;
        [SerializeField] TextStudioApplicationSelector _selection = default;
        [SerializeField] TextStudioCurveMapping _mapping = TextStudioCurveMapping.EachMatch;

        [NonSerialized] TextStudioSelectionResolver _resolved;
        [NonSerialized] TextStudioCompiledSelector _compiled;
        [NonSerialized] string _selectionDiagnostic;
        [NonSerialized] TextStudioCompiledPlan _lastPlan;
        [NonSerialized] uint _lastSourceVersion;
        [NonSerialized] int _lastGlyphCount = -1;
        [NonSerialized] bool _selectionDirty = true;
        [NonSerialized] float[] _domainMin;
        [NonSerialized] float[] _domainMax;
        [NonSerialized] int[] _domainCount;

        public PlacementMode Mode => PlacementMode.Curve;
        public float Height => _height;
        public TextStudioCurveMapping Mapping => _mapping;
        public TextStudioApplicationSelector Selection => _selection;
        public TextStudioCurveReference Curve => _curve ??= new TextStudioCurveReference();
        public string SelectionDiagnostic => _selectionDiagnostic ?? string.Empty;

        /// <summary>Copies an inline curve definition and configures its height, selected text, and match mapping.</summary>
        public void ConfigureInline(in TextStudioCurveDefinition definition, float height,
            in TextStudioApplicationSelector selection, TextStudioCurveMapping mapping)
        {
            Curve.SetInline(in definition);
            ConfigureSettings(height, in selection, mapping);
        }

        /// <summary>Links a shared curve profile and configures its height, selected text, and match mapping.</summary>
        public void ConfigureProfile(TextStudioCurveProfile profile, float height,
            in TextStudioApplicationSelector selection, TextStudioCurveMapping mapping)
        {
            Curve.Link(profile);
            ConfigureSettings(height, in selection, mapping);
        }

        /// <summary>Updates curve height, selection, and mapping while invalidating the resolved selection.</summary>
        public void ConfigureSettings(float height, in TextStudioApplicationSelector selection,
            TextStudioCurveMapping mapping)
        {
            _height = float.IsFinite(height) ? height : 0f;
            _selection = selection;
            _mapping = mapping;
            InvalidateSelection();
        }

        internal void InvalidateSelection()
        {
            _selectionDirty = true;
            _lastPlan = null;
        }

        internal void PrepareSelection(TextStudioCompiledPlan plan, int glyphCount,
            TextStudioVisualGlyph[] visualGlyphs = null, bool layoutChanged = false)
        {
            _resolved ??= new TextStudioSelectionResolver();
            bool changed = _selectionDirty || _compiled.NodeCount == 0;
            if (changed)
            {
                TextStudioSelectorCompiler.TryCompile(in _selection,
                    TextStudioSelectorCapabilities.LayoutStable, out _compiled, out _selectionDiagnostic);
                _selectionDirty = false;
            }
            if (!changed && !layoutChanged && ReferenceEquals(plan, _lastPlan) && plan != null &&
                plan.SourceVersion == _lastSourceVersion && glyphCount == _lastGlyphCount)
                return;
            _resolved.Resolve(in _compiled, plan, visualGlyphs, glyphCount);
            _lastPlan = plan;
            _lastSourceVersion = plan != null ? plan.SourceVersion : 0;
            _lastGlyphCount = glyphCount;
        }

        internal bool IsSelected(int unit) => _resolved != null && _resolved.GetOwner(unit) >= 0;
        internal int GetOwner(int unit) => _resolved?.GetOwner(unit) ?? -1;
        internal int GetGuideOwner(int unit)
        {
            int owner = GetOwner(unit);
            return owner >= 0 && _mapping == TextStudioCurveMapping.EntireSelection ? 0 : owner;
        }
        internal int GuideDomainCount => _mapping == TextStudioCurveMapping.EntireSelection
            ? (_resolved != null && _resolved.MatchCount > 0 ? 1 : 0)
            : _resolved?.MatchCount ?? 0;

        internal bool TryGetDomain(int owner, out float minimum, out float maximum)
        {
            int domain = _mapping == TextStudioCurveMapping.EntireSelection ? 0 : owner;
            minimum = maximum = 0f;
            if (domain < 0 || _domainCount == null || domain >= _domainCount.Length ||
                _domainCount[domain] <= 0) return false;
            minimum = _domainMin[domain];
            maximum = _domainMax[domain];
            return float.IsFinite(minimum) && float.IsFinite(maximum);
        }

        public void ComputePlacements(Vector3[] layoutPositions, GlyphPlacement[] placements, int glyphCount)
        {
            PlacementContext context = new PlacementContext(layoutPositions, glyphCount);
            ComputePlacements(in context, placements);
        }

        /// <summary>Writes layout-based glyph placements and applies the resolved curve to selected mapping domains.</summary>
        public void ComputePlacements(in PlacementContext context, GlyphPlacement[] placements)
        {
            int count = Math.Min(context.glyphCount, Math.Min(context.layoutPositions?.Length ?? 0,
                placements?.Length ?? 0));
            for (int unit = 0; unit < count; unit++)
                placements[unit] = GlyphPlacement.Default(context.layoutPositions[unit]);
            if (count == 0 || _resolved == null || !float.IsFinite(_height) || _height == 0f)
                return;

            int domainCount = _mapping == TextStudioCurveMapping.EntireSelection
                ? 1 : _resolved.MatchCount;
            if (domainCount == 0) return;
            EnsureDomains(domainCount);
            for (int domain = 0; domain < domainCount; domain++)
            {
                _domainMin[domain] = float.PositiveInfinity;
                _domainMax[domain] = float.NegativeInfinity;
                _domainCount[domain] = 0;
            }
            for (int unit = 0; unit < count; unit++)
            {
                int owner = _resolved.GetOwner(unit);
                if (owner < 0) continue;
                int domain = _mapping == TextStudioCurveMapping.EntireSelection ? 0 : owner;
                if (domain >= domainCount) continue;
                float x = context.layoutPositions[unit].x;
                if (!float.IsFinite(x)) continue;
                float left = x;
                float right = x;
                if (context.sourceVisualBounds != null && unit < context.sourceVisualBounds.Length)
                {
                    TextStudioGlyphVisualBounds bounds = context.sourceVisualBounds[unit];
                    if (bounds.IsValid && float.IsFinite(bounds.Minimum.x) && float.IsFinite(bounds.Maximum.x))
                    {
                        left = Mathf.Min(left, bounds.Minimum.x);
                        right = Mathf.Max(right, bounds.Maximum.x);
                    }
                }
                _domainMin[domain] = Mathf.Min(_domainMin[domain], left);
                _domainMax[domain] = Mathf.Max(_domainMax[domain], right);
                _domainCount[domain]++;
            }
            for (int unit = 0; unit < count; unit++)
            {
                int owner = _resolved.GetOwner(unit);
                if (owner < 0) continue;
                int domain = _mapping == TextStudioCurveMapping.EntireSelection ? 0 : owner;
                if (domain >= domainCount || _domainCount[domain] == 0) continue;
                float width = _domainMax[domain] - _domainMin[domain];
                float t = _domainCount[domain] == 1 || width <= 0.00001f
                    ? 0.5f : Mathf.Clamp01((context.layoutPositions[unit].x - _domainMin[domain]) / width);
                float offset = _height * Curve.Evaluate(t);
                if (!float.IsFinite(offset)) continue;
                float slope = width > 0.00001f && _domainCount[domain] > 1
                    ? EvaluateSlope(t, width) : 0f;
                Quaternion rotation = Quaternion.AngleAxis(Mathf.Atan(slope) * Mathf.Rad2Deg, Vector3.forward);
                float centerAboveBaseline = context.sourceCenterBaselineOffsets != null &&
                                            unit < context.sourceCenterBaselineOffsets.Length &&
                                            float.IsFinite(context.sourceCenterBaselineOffsets[unit])
                    ? context.sourceCenterBaselineOffsets[unit] : 0f;
                Vector3 sourceCenter = context.layoutPositions[unit];
                Vector3 sourceOffset = Vector3.up * centerAboveBaseline;
                Vector3 baseline = sourceCenter - sourceOffset;
                GlyphPlacement placement = placements[unit];
                placement.position = baseline + Vector3.up * offset + rotation * sourceOffset;
                placement.rotation = rotation;
                placement.tangent = rotation * Vector3.right;
                placement.up = rotation * Vector3.up;
                placement.pathProgress = t;
                placements[unit] = placement;
            }
        }

        float EvaluateSlope(float t, float width)
        {
            const float Delta = 0.001f;
            float left = Mathf.Max(0f, t - Delta);
            float right = Mathf.Min(1f, t + Delta);
            float span = right - left;
            if (span <= 0f) return 0f;
            float rise = _height * (Curve.Evaluate(right) - Curve.Evaluate(left));
            float slope = rise / (span * width);
            return float.IsFinite(slope) ? Mathf.Clamp(slope, -1000f, 1000f) : 0f;
        }

        void EnsureDomains(int count)
        {
            if (_domainMin != null && _domainMin.Length >= count) return;
            int capacity = TextStudioBufferCapacity.ForCount(count);
            _domainMin = new float[capacity];
            _domainMax = new float[capacity];
            _domainCount = new int[capacity];
        }
    }
}
