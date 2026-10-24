using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>One lease per unique active program. Occurrences own mapping rows, never materials or style operations.</summary>
    internal sealed class TextStudioTMPAppearancePlanAdapter
    {
        readonly List<TextStudioTMPAppearanceAdapter> _programs = new List<TextStudioTMPAppearanceAdapter>();
        readonly List<CompiledTextStudio2DStyle> _styles = new List<CompiledTextStudio2DStyle>();
        bool[] _used = Array.Empty<bool>();
        ulong _geometryFingerprint;
        readonly Dictionary<int, CompiledTextStudio2DStyle> _nativePrograms = new Dictionary<int, CompiledTextStudio2DStyle>();
        readonly Dictionary<ulong, byte> _fontCoverage = new Dictionary<ulong, byte>();
        internal readonly TextStudio2DAppearanceBinding Binding = new TextStudio2DAppearanceBinding();
        internal CompiledTextStudioAppearancePlan Plan { get; private set; }
        internal IReadOnlyList<TextStudioTMPAppearanceAdapter> Programs => _programs;
        internal bool UsesHero { get; private set; }
        /// <summary>Why the plan composites offscreen, or None when it draws directly.</summary>
        internal TextStudio2DRouteReason CompositionReason { get; private set; }
        internal bool RequiresFieldMaterial { get; private set; }
        internal TextStudio2DFieldPaddingTier PaddingTier { get; private set; }
        internal bool IsReady { get; private set; }
        internal TextStudioTMPAppearanceAdapter PrimaryFieldAdapter { get; private set; }
        internal CompiledTextStudio2DStyle PrimaryFieldStyle { get; private set; }
        internal CompiledTextStudio2DStyle WorldShadowStyle { get; private set; }
        internal float OutputPixelsPerUnit { get; private set; }
        internal int Version { get; private set; }
        internal TextStudioAppearanceSourceStatistics SourceStatistics
        {
            get
            {
                _fontCoverage.Clear();
                int minimumTier = int.MaxValue, maximumTier = -1;
                float density = 0, distance = 0;
                for (int index = 0; index < _programs.Count; index++)
                {
                    TextStudioPreparedFieldDemand demand = _programs[index].SourceDemand;
                    if (!_used[index] || demand == null) continue;
                    demand.CollectUsedFontCoverage(_fontCoverage);
                    TextStudioAppearanceSourceStatistics statistics = demand.Statistics;
                    if (statistics.MinimumSelectedTier >= 0) minimumTier = Mathf.Min(minimumTier, statistics.MinimumSelectedTier);
                    maximumTier = Mathf.Max(maximumTier, statistics.MaximumSelectedTier);
                    density = Mathf.Max(density, statistics.MaximumDensity); distance = Mathf.Max(distance, statistics.MaximumDistance);
                }
                int prepared = 0, fallback = 0;
                foreach (byte coverage in _fontCoverage.Values) { if ((coverage & 1) != 0) prepared++; if ((coverage & 2) != 0) fallback++; }
                return new TextStudioAppearanceSourceStatistics(_fontCoverage.Count, prepared, fallback,
                    minimumTier == int.MaxValue ? -1 : minimumTier, maximumTier, density, distance);
            }
        }
        internal long SelectionPayloadBytes
        {
            get
            {
                long bytes = Binding.ManagedPayloadBytes;
                foreach (TextStudioTMPAppearanceAdapter program in _programs) bytes += program.BoundStyle.ManagedPayloadBytes;
                return bytes;
            }
        }
        internal int ResolvedMatchCount
        {
            get
            {
                int count = 0;
                foreach (TextStudioTMPAppearanceAdapter program in _programs) count += program.BoundStyle.ResolvedMatchCount;
                return count;
            }
        }
        internal string SourceQualityHint
        {
            get
            {
                // Every program reports the same actionable source-field hint. Publish it once.
                for (int index = 0; index < _programs.Count; index++)
                    if (_used[index] && !string.IsNullOrEmpty(_programs[index].SourceQualityHint)) return _programs[index].SourceQualityHint;
                return string.Empty;
            }
        }

        internal Vector4 GetPreparedQuality(int renderGlyph)
        {
            if (Binding.Plan == null) return default;
            Vector4 result = default;
            for (int program = 0; program < _programs.Count; program++)
            {
                if (!_used[program]) continue;
                IReadOnlyList<TextStudio2DAppearanceBinding.GlyphBinding> rows = Binding.GetProgramGlyphs(program);
                bool included = false;
                for (int row = 0; row < rows.Count && !included; row++) included = rows[row].RenderGlyphIndex == renderGlyph;
                if (included) result = Vector4.Max(result, _programs[program].GetPreparedQuality(renderGlyph));
            }
            return result;
        }

        internal bool Prepare(CompiledTextStudioAppearancePlan plan, TMP_Text text, bool forceHero = false)
        {
            bool changed = !ReferenceEquals(Plan, plan);
            Plan = plan;
            if (_used.Length < plan.Programs.Count) Array.Resize(ref _used, TextStudioBufferCapacity.ForCount(plan.Programs.Count));
            Array.Clear(_used, 0, _used.Length);
            while (_programs.Count < plan.Programs.Count) { _programs.Add(new TextStudioTMPAppearanceAdapter()); _styles.Add(null); }
            while (_programs.Count > plan.Programs.Count)
            { int last = _programs.Count - 1; _programs[last].Release(); _programs.RemoveAt(last); _styles.RemoveAt(last); }
            TextStudio2DRouteReason reason = forceHero ? TextStudio2DRouteReason.LensMagnifier : TextStudio2DRouteReason.None;
            for (int index = 0; index < plan.Occurrences.Count; index++)
            {
                TextStudioAppearanceOccurrence occurrence = plan.Occurrences[index];
                if (!occurrence.IsActive || occurrence.VisibleRanges.Count == 0) continue;
                if (occurrence.ApplyMode == TextStudioAppearanceApplyMode.Overlay && plan.Programs[occurrence.ProgramIndex].IsPassThrough) continue;
                _used[occurrence.ProgramIndex] = true;
                // Opacity and tint apply to the composed occurrence as a group, so overlapping rows must not blend twice.
                if (reason == TextStudio2DRouteReason.None && (occurrence.Tint != Color.white || occurrence.Opacity != 1))
                    reason = TextStudio2DRouteReason.OccurrenceOpacity;
            }
            WorldShadowStyle = null;
            for (int index = 0; index < plan.Programs.Count; index++)
            {
                CompiledTextStudio2DStyle style = plan.Programs[index];
                if (style.IsPassThrough) style = NativeProgram(style);
                _styles[index] = style;
                if (!_used[index]) continue;
                TextStudio2DRouteDecision route = TextStudio2DRoutePlanner.Resolve(style, text);
                if (reason == TextStudio2DRouteReason.None && route.IsComposited) reason = route.Reason;
                // A native face layered under other rows keeps its complete native contribution offscreen.
                for (int operation = 0; operation < style.OperationCount && reason == TextStudio2DRouteReason.None; operation++)
                {
                    ref readonly TextStudio2DCompiledOperation op = ref style.OperationAt(operation);
                    if (op.Kind == TextStudio2DLayerKind.Fill && op.Fill.PreservesNativeAppearance && style.OperationCount > 1)
                        reason = TextStudio2DRouteReason.NativeFaceLayering;
                }
                if (style.WorldShadowMode == TextStudio2DWorldShadowMode.AlphaClipped) WorldShadowStyle = style;
            }
            forceHero = reason != TextStudio2DRouteReason.None;
            bool wasHero = UsesHero;
            for (int index = 0; index < _programs.Count; index++)
            {
                if (!_used[index]) { _programs[index].Release(); continue; }
                changed |= _programs[index].Update(_styles[index], forceHero, text, reason);
                if (!forceHero && _programs[index].HasHeroProgram) { forceHero = true; reason = _programs[index].Route.Reason; }
            }
            if (forceHero)
                for (int index = 0; index < _programs.Count; index++)
                    if (_used[index] && !_programs[index].HasHeroProgram) changed |= _programs[index].Update(_styles[index], true, text, reason);
            UsesHero = forceHero;
            CompositionReason = forceHero ? reason : TextStudio2DRouteReason.None;
            RequiresFieldMaterial = false; PaddingTier = TextStudio2DFieldPaddingTier.None;
            PrimaryFieldAdapter = null; PrimaryFieldStyle = null;
            for (int index = 0; index < _programs.Count; index++)
            {
                if (!_used[index]) continue;
                TextStudioTMPAppearanceAdapter adapter = _programs[index];
                RequiresFieldMaterial |= adapter.RequiresFieldMaterial;
                if ((int)adapter.PaddingTier > (int)PaddingTier) PaddingTier = adapter.PaddingTier;
                if (PrimaryFieldAdapter == null && adapter.RequiresFieldMaterial)
                { PrimaryFieldAdapter = adapter; PrimaryFieldStyle = _styles[index]; }
            }
            changed |= wasHero != UsesHero;
            return changed;
        }

        internal bool UpdateMappings(in TextStudioFrame frame, TMP_Text text, TextStudioTMPMaterialEffectAdapter materials,
            TextStudio2DExternalGlyphSource external = null)
        {
            bool changed = Binding.Update(in frame);
            IsReady = true; OutputPixelsPerUnit = 0;
            TextStudioDeterministicHash64 geometry = default;
            geometry.Add(UsesHero);
            for (int index = 0; index < _programs.Count; index++)
            {
                if (!_used[index]) continue;
                TextStudioFrame programFrame = new TextStudioFrame(in frame, _styles[index], -1, Binding, index);
                TextStudioTMPAppearanceAdapter adapter = _programs[index];
                adapter.UpdateMappings(in programFrame, text, materials, external);
                adapter.UpdateVisualModifiers(in programFrame, frame.MotionPolicy);
                IsReady &= UsesHero ? adapter.HasHeroProgram : adapter.FieldStyleSlot > 0 || adapter.HasRetainedProgram;
                OutputPixelsPerUnit = Mathf.Max(OutputPixelsPerUnit, adapter.OutputPixelsPerUnit);
                geometry.Add(index); geometry.Add(_styles[index].GeometryFingerprint);
                geometry.Add(adapter.FieldStyleSlot); geometry.Add(Binding.GetProgramRevision(index));
            }
            if (_geometryFingerprint != geometry.Value) { _geometryFingerprint = geometry.Value; Version++; }
            return changed;
        }

        internal bool NeedsCameraQualityRefresh(TMP_Text text)
        {
            for (int index = 0; index < _programs.Count; index++)
                if (_used[index] && _programs[index].NeedsCameraQualityRefresh(text)) return true;
            return false;
        }

        internal bool HasStaticCameraQuality
        {
            get
            {
                for (int index = 0; index < _programs.Count; index++)
                    if (_used[index] && !_programs[index].HasStaticCameraQuality) return false;
                return true;
            }
        }

        internal void FlushAndBind()
        {
            for (int index = 0; index < _programs.Count; index++) if (_used[index]) _programs[index].FlushAndBind();
        }

        internal string CompatibilityWarning
        {
            get
            {
                string warning = string.Empty;
                for (int index = 0; index < _programs.Count; index++)
                    if (_used[index] && !string.IsNullOrEmpty(_programs[index].CompatibilityWarning))
                        warning = warning.Length == 0 ? _programs[index].CompatibilityWarning : warning + " " + _programs[index].CompatibilityWarning;
                return warning;
            }
        }

        internal void Release()
        {
            if (Plan == null && _programs.Count == 0) return;
            foreach (TextStudioTMPAppearanceAdapter program in _programs) program.Release();
            _programs.Clear(); _styles.Clear(); Plan = null; IsReady = false;
            PrimaryFieldAdapter = null; PrimaryFieldStyle = null; WorldShadowStyle = null;
            Binding.Clear(); _fontCoverage.Clear(); UsesHero = false; CompositionReason = TextStudio2DRouteReason.None; RequiresFieldMaterial = false;
            _geometryFingerprint = 0;
        }

        CompiledTextStudio2DStyle NativeProgram(CompiledTextStudio2DStyle source)
        {
            int key = (int)source.SourceFieldRequest.Quality | (int)source.PainterOrder << 8 | (int)source.WorldShadowMode << 16;
            if (_nativePrograms.TryGetValue(key, out CompiledTextStudio2DStyle native)) return native;
            TextStudio2DStyleSettings settings = new TextStudio2DStyleSettings(null, default,
                source.SourceFieldRequest.Quality, source.PainterOrder, source.WorldShadowMode,
                new[] { TextStudio2DLayerDefaults.CreateNativeFill().WithIdentity(TextStudioStackItemId.Create("textstudio.appearance.native")) });
            native = TextStudio2DStyleCompiler.Compile(in settings);
            _nativePrograms.Add(key, native);
            return native;
        }
    }
}
