using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio
{
    /// <summary>
    /// Drives a legacy <see cref="Text"/> through Text Studio while preserving Unity's Canvas mesh pipeline.
    /// The component must run before other mesh modifiers so Outline, Shadow, and custom effects receive
    /// the already animated glyph geometry.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    [AddComponentMenu("")]
    [HelpURL("https://www.wetzold.com/tools/text-studio/docs/")]
    public sealed class TextStudioUnityUITextBackend : BaseMeshEffect,
        ITextStudioTextBackend,
        ITextStudioTextBackendAppearanceSupport,
        ITextStudioTextSourceBackend,
        ITextStudioLayoutBackend,
        ITextStudioMeshBackend,
        ITextStudioTextBackendRenderChannelSupport
    {
        const int BackendPriority = 50;
        const string InputFieldDiagnostic =
            "Unity UI Text used by an editable Input Field cannot be driven directly. Use a separate presentation Text component.";

        readonly TextStudioUnityUITextLayout _layout =
            new TextStudioUnityUITextLayout();
        readonly StringBuilder _displayBuilder = new StringBuilder(128);

        TextStudioText _owner;
        Text _target;
        string _lastSeenText = string.Empty;
        string _lastPushedText = string.Empty;
        int[] _pushedCharacterToTextUnit = Array.Empty<int>();
        int _pushedCharacterCount;
        bool _hasReceivedOwnerText;
        bool _settingTargetText;
        bool _settingVerticesDirty;
        bool _suppressOutput;
        bool _hasFrame;
        bool _pendingTextChange;
        bool _pendingLayoutChange;
        bool _fontEventSubscribed;

        EvaluatedGlyphState[] _states = Array.Empty<EvaluatedGlyphState>();
        GlyphPlacement[] _placements = Array.Empty<GlyphPlacement>();
        Vector3[] _basePositions = Array.Empty<Vector3>();
        float[] _effectUnitScales = Array.Empty<float>();
        float[] _alphaMultipliers = Array.Empty<float>();
        int _stateCount;
        int _placementCount;

        TextStudioUnityUITextLayoutSignature _observedSignature;

        /// <summary>Stable backend identifier used by selection, diagnostics, and integrations.</summary>
        public string BackendId => TextStudioTextBackendIds.UnityUIText;

        /// <summary>User-facing renderer name.</summary>
        public string DisplayName => "Unity UI Text (Legacy)";

        /// <summary>Selection priority below specialized optional shaping backends.</summary>
        public int Priority => BackendPriority;

        /// <summary>Legacy Text supports the complete shared vertex channel set except UV animation.</summary>
        public TextStudioRenderChannels SupportedRenderChannels =>
            TextStudioRenderChannels.All & ~TextStudioRenderChannels.UvOffset;

        /// <summary>Legacy bitmap-font rendering cannot execute Text Studio's SDF Appearance stack.</summary>
        public bool SupportsTextStudio2DAppearance => false;

        /// <summary>Gets whether the target can currently be controlled safely.</summary>
        public bool IsAvailable =>
            isActiveAndEnabled &&
            ResolveTarget() != null &&
            !IsEditableInputFieldTarget;

        /// <summary>Gets the legacy Text component controlled by this backend.</summary>
        public UnityEngine.Object TargetObject => ResolveTarget();

        /// <summary>Gets the strongly typed legacy Text target.</summary>
        public Text Target => ResolveTarget();

        /// <summary>Gets whether this Text is the editable output of a legacy Input Field.</summary>
        public bool IsEditableInputFieldTarget
        {
            get
            {
                Text target = ResolveTarget();
                if (target == null)
                    return false;
                InputField input = target.GetComponentInParent<InputField>();
                return input != null && ReferenceEquals(input.textComponent, target);
            }
        }

        /// <summary>Gets actionable setup guidance when the backend is unavailable.</summary>
        public string AvailabilityDiagnostic =>
            IsEditableInputFieldTarget ? InputFieldDiagnostic : string.Empty;

        /// <summary>Gets the capabilities implemented by the legacy Canvas backend.</summary>
        public TextStudioTextBackendCapabilities Capabilities
        {
            get
            {
                TextStudioTextBackendCapabilities capabilities =
                    TextStudioTextBackendCapabilities.SourceVertexColors |
                    TextStudioTextBackendCapabilities.TextStudioVertexAnimation;
                Text target = ResolveTarget();
                if (target != null && target.supportRichText)
                    capabilities |= TextStudioTextBackendCapabilities.RichText;
                return capabilities;
            }
        }

        /// <summary>Gets whether target text changed after Text Studio last synchronized it.</summary>
        public bool HasUnobservedTextChange
        {
            get
            {
                Text target = ResolveTarget();
                return target != null &&
                       !string.Equals(
                           target.text ?? string.Empty,
                           _lastSeenText,
                           StringComparison.Ordinal);
            }
        }

        /// <summary>Gets the text currently stored by the legacy Text target.</summary>
        public string CurrentText
        {
            get
            {
                Text target = ResolveTarget();
                return target != null ? target.text ?? string.Empty : _lastSeenText;
            }
        }

        /// <summary>Gets whether public TextGenerator data is ready for layout publication.</summary>
        public bool IsLayoutReady => ResolveTarget() != null && !IsEditableInputFieldTarget;

        /// <summary>Gets whether the visible legacy Text output is enabled.</summary>
        public bool IsVisualOutputEnabled
        {
            get
            {
                Text target = ResolveTarget();
                return target != null &&
                       target.isActiveAndEnabled &&
                       !_suppressOutput;
            }
        }

        /// <summary>Legacy Text consumes the shared evaluated vertex state directly.</summary>
        public bool NeedsComposedGlyphState => false;

        /// <summary>Occurs after an external text, layout, or mesh change is observed.</summary>
        public event Action<TextStudioTextBackendChange> Changed;

        internal int PreparedQuadCount => _layout.QuadCount;

        internal int GetPreparedTextUnitForQuad(int quadIndex)
        {
            return _layout.GetTextUnitForQuad(quadIndex);
        }

        /// <summary>Adds or returns the backend for a legacy Text and Text Studio hub on one GameObject.</summary>
        public static TextStudioUnityUITextBackend EnsureForGameObject(GameObject gameObject)
        {
            if (gameObject == null ||
                gameObject.GetComponent<Text>() == null ||
                gameObject.GetComponent<TextStudioText>() == null)
            {
                return null;
            }

            TextStudioUnityUITextBackend backend =
                gameObject.GetComponent<TextStudioUnityUITextBackend>();
            return backend != null
                ? backend
                : gameObject.AddComponent<TextStudioUnityUITextBackend>();
        }

        /// <summary>Attaches this backend to its Text Studio host.</summary>
        public void Attach(TextStudioText owner)
        {
            Text target = ResolveTarget();
            if (target == null)
            {
                throw new InvalidOperationException(
                    "The Unity UI Text backend requires a legacy UnityEngine.UI.Text component on the same GameObject.");
            }
            if (IsEditableInputFieldTarget)
                throw new InvalidOperationException(InputFieldDiagnostic);

            _owner = owner;
            _lastSeenText = target.text ?? string.Empty;
            _observedSignature = TextStudioUnityUITextLayoutSignature.Capture(target);
            SubscribeFontEvent();
        }

        /// <summary>Detaches from the host and restores the unmodified Canvas mesh.</summary>
        public void Detach()
        {
            RestoreOutput();
            _owner = null;
            _pendingTextChange = false;
            _pendingLayoutChange = false;
            _hasReceivedOwnerText = false;
        }

        /// <summary>Resolves an external Text.text mutation according to the host write policy.</summary>
        public bool TryPullText(
            bool isDriving,
            TextStudioBackendWriteHandling handling,
            string currentMarkup,
            out string newMarkup,
            out bool ignoredExternalWrite)
        {
            newMarkup = currentMarkup ?? string.Empty;
            ignoredExternalWrite = false;
            Text target = ResolveTarget();
            if (target == null)
                return false;

            string observed = target.text ?? string.Empty;
            if (isDriving && !_hasReceivedOwnerText)
            {
                _lastSeenText = observed;
                return false;
            }
            if (string.Equals(observed, _lastSeenText, StringComparison.Ordinal))
                return false;

            _lastSeenText = observed;
            InvalidatePushedCharacterMap();
            if (handling == TextStudioBackendWriteHandling.Ignore ||
                handling == TextStudioBackendWriteHandling.Warn)
            {
                ignoredExternalWrite = true;
                return false;
            }

            newMarkup = observed;
            return true;
        }

        /// <summary>Pushes compiled Text Studio output into the legacy Text source.</summary>
        public void PushText(in TextStudioBackendTextRequest request)
        {
            Text target = ResolveTarget();
            if (target == null)
                return;

            string targetText;
            if (!request.IsDriving || request.IsLiteralMarkup)
            {
                targetText = request.Markup ?? string.Empty;
                InvalidatePushedCharacterMap();
            }
            else if (target.supportRichText)
            {
                targetText = BuildLegacyDisplayText(request.Plan);
            }
            else
            {
                targetText = request.ResolvedText ?? string.Empty;
                CaptureResolvedCharacterMap(request.Plan, targetText.Length);
            }

            _hasReceivedOwnerText = true;
            SetTargetText(targetText);
        }

        /// <summary>Writes raw authoring markup into the legacy Text field.</summary>
        public void PushRawAuthoringText(string markup)
        {
            InvalidatePushedCharacterMap();
            SetTargetText(markup ?? string.Empty);
        }

        /// <summary>Builds renderer-neutral glyph layout from the public TextGenerator output.</summary>
        public bool TryBuildLayout(
            in TextStudioBackendLayoutRequest request,
            TextStudioBackendLayoutBuilder builder)
        {
            Text target = ResolveTarget();
            if (target == null || IsEditableInputFieldTarget)
                return false;

            bool built = _layout.TryBuild(
                target,
                in request,
                builder,
                _lastPushedText,
                _pushedCharacterToTextUnit,
                _pushedCharacterCount);
            if (built)
            {
                _observedSignature = TextStudioUnityUITextLayoutSignature.Capture(target);
                _pendingLayoutChange = false;
            }
            return built;
        }

        /// <summary>Copies the transient Text Studio frame and schedules one Canvas vertex rebuild.</summary>
        public void Render(in TextStudioFrame frame, bool suppressOutput)
        {
            int stateCount = frame.Animated.IsCreated ? frame.AnimatedCount : 0;
            bool frameChanged = !_hasFrame ||
                                _stateCount != stateCount ||
                                _suppressOutput != suppressOutput ||
                                frame.GlobalDirty != DirtyFlags.None ||
                                frame.Dirty.AreAllDirty(stateCount) ||
                                frame.Dirty.AnyDirtyInRange(0, stateCount);
            if (!frameChanged)
                return;

            EnsureFrameCapacity(stateCount);
            for (int i = 0; i < stateCount; i++)
            {
                _states[i] = frame.Animated[i];
                _placements[i] = i < frame.PlacementCount
                    ? frame.Placements[i]
                    : default;
                _basePositions[i] = i < frame.BasePositions.Length
                    ? frame.BasePositions[i]
                    : Vector3.zero;
                _effectUnitScales[i] = frame.GetEffectUnitScale(i);
                _alphaMultipliers[i] = i < frame.PresentationGlyphCount
                    ? frame.PresentationGlyphs[i].alphaMultiplier
                    : 1f;
            }

            _stateCount = stateCount;
            _placementCount = Mathf.Min(frame.PlacementCount, stateCount);
            _suppressOutput = suppressOutput;
            _hasFrame = true;
            SetVerticesDirty();
        }

        /// <summary>Clears retained animation state and restores the source Text mesh.</summary>
        public void RestoreOutput()
        {
            bool needsRebuild = _hasFrame || _suppressOutput;
            _hasFrame = false;
            _suppressOutput = false;
            _stateCount = 0;
            _placementCount = 0;
            if (needsRebuild)
                SetVerticesDirty();
        }

        /// <summary>Applies retained Text Studio glyph state inside Unity's normal Canvas modifier chain.</summary>
        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper == null)
                return;

            int vertexCount = vertexHelper.currentVertCount;
            if (_suppressOutput)
            {
                HideVertices(vertexHelper, vertexCount);
                return;
            }
            if (!_hasFrame || _stateCount <= 0 || vertexCount <= 0)
                return;
            if ((vertexCount & 3) != 0 || vertexCount / 4 != _layout.QuadCount)
            {
                _pendingLayoutChange = true;
                return;
            }

            int quadCount = vertexCount / 4;
            for (int quadIndex = 0; quadIndex < quadCount; quadIndex++)
            {
                int textUnitIndex = _layout.GetTextUnitForQuad(quadIndex);
                if ((uint)textUnitIndex >= (uint)_stateCount)
                    continue;

                int vertexIndex = quadIndex * 4;
                UIVertex topLeft = default;
                UIVertex topRight = default;
                UIVertex bottomRight = default;
                UIVertex bottomLeft = default;
                vertexHelper.PopulateUIVertex(ref topLeft, vertexIndex);
                vertexHelper.PopulateUIVertex(ref topRight, vertexIndex + 1);
                vertexHelper.PopulateUIVertex(ref bottomRight, vertexIndex + 2);
                vertexHelper.PopulateUIVertex(ref bottomLeft, vertexIndex + 3);

                EvaluatedGlyphState state = _states[textUnitIndex];
                GlyphPlacement placement = _placements[textUnitIndex];
                float effectUnitScale = _effectUnitScales[textUnitIndex];
                TextStudioGlyphQuadTransform transform =
                    TextStudioGlyphQuadTransformer.CreateFromQuad(
                        bottomLeft.position,
                        topLeft.position,
                        topRight.position,
                        bottomRight.position,
                        _basePositions[textUnitIndex],
                        in state,
                        in placement,
                        textUnitIndex < _placementCount,
                        effectUnitScale,
                        _alphaMultipliers[textUnitIndex]);

                ApplyVertex(ref topLeft, in transform, in state, 1, effectUnitScale);
                ApplyVertex(ref topRight, in transform, in state, 2, effectUnitScale);
                ApplyVertex(ref bottomRight, in transform, in state, 3, effectUnitScale);
                ApplyVertex(ref bottomLeft, in transform, in state, 0, effectUnitScale);

                vertexHelper.SetUIVertex(topLeft, vertexIndex);
                vertexHelper.SetUIVertex(topRight, vertexIndex + 1);
                vertexHelper.SetUIVertex(bottomRight, vertexIndex + 2);
                vertexHelper.SetUIVertex(bottomLeft, vertexIndex + 3);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveTarget();
            SubscribeFontEvent();
            TextStudioText owner = GetComponent<TextStudioText>();
            if (owner != null && owner.isActiveAndEnabled)
                owner.Backends.Refresh();
        }

        protected override void OnDisable()
        {
            TextStudioText owner = GetComponent<TextStudioText>();
            UnsubscribeFontEvent();
            Detach();
            base.OnDisable();
            if (owner != null && owner.isActiveAndEnabled)
                owner.Backends.Refresh();
        }

        protected override void OnDestroy()
        {
            UnsubscribeFontEvent();
            Detach();
            base.OnDestroy();
        }

        /// <summary>Marks native layout as pending after the RectTransform's dimensions change.</summary>
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            _pendingLayoutChange = true;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveTarget();
            _pendingLayoutChange = true;
            if (!isActiveAndEnabled)
                return;
            TextStudioText owner = GetComponent<TextStudioText>();
            if (owner != null && owner.isActiveAndEnabled)
                owner.Backends.Refresh();
        }
#endif

        void Update()
        {
            Text target = ResolveTarget();
            if (target == null || _owner == null)
                return;

            string current = target.text ?? string.Empty;
            if (!_settingTargetText &&
                !string.Equals(current, _lastSeenText, StringComparison.Ordinal))
            {
                _pendingTextChange = true;
            }

            TextStudioUnityUITextLayoutSignature signature =
                TextStudioUnityUITextLayoutSignature.Capture(target);
            if (!_observedSignature.Equals(signature))
                _pendingLayoutChange = true;

            if (_pendingTextChange)
            {
                _pendingTextChange = false;
                _lastSeenText = current;
                InvalidatePushedCharacterMap();
                Changed?.Invoke(TextStudioTextBackendChange.Text);
            }
            else if (_pendingLayoutChange)
            {
                _pendingLayoutChange = false;
                _observedSignature = signature;
                Changed?.Invoke(TextStudioTextBackendChange.Layout);
            }
        }

        Text ResolveTarget()
        {
            if (_target == null)
                _target = GetComponent<Text>();
            return _target;
        }

        void SetTargetText(string value)
        {
            Text target = ResolveTarget();
            if (target == null)
                return;

            value ??= string.Empty;
            _lastPushedText = value;
            _lastSeenText = value;
            if (string.Equals(target.text, value, StringComparison.Ordinal))
            {
                SetVerticesDirty();
                return;
            }

            _settingTargetText = true;
            try
            {
                target.text = value;
            }
            finally
            {
                _settingTargetText = false;
            }
            _pendingLayoutChange = true;
        }

        string BuildLegacyDisplayText(TextStudioPlanView plan)
        {
            string display = plan.DisplayText ?? string.Empty;
            _displayBuilder.Clear();
            EnsurePushedCharacterMapCapacity(display.Length);
            int written = 0;
            for (int index = 0; index < display.Length; index++)
            {
                int textUnitIndex = plan.GetUnitIndexForDisplayChar(index);
                if (display[index] == '<' && textUnitIndex < 0)
                {
                    int end = display.IndexOf('>', index + 1);
                    if (end >= 0)
                    {
                        if (IsSupportedLegacyTag(display, index + 1, end))
                        {
                            for (int tagIndex = index; tagIndex <= end; tagIndex++)
                            {
                                _displayBuilder.Append(display[tagIndex]);
                                _pushedCharacterToTextUnit[written++] = -1;
                            }
                        }
                        index = end;
                        continue;
                    }
                }

                _displayBuilder.Append(display[index]);
                _pushedCharacterToTextUnit[written++] = textUnitIndex;
            }

            _pushedCharacterCount = written;
            // Steady frames push the same display text, so reuse the previous string instead of allocating one.
            if (!BuilderEquals(_displayBuilder, _lastPushedText))
                _lastPushedText = _displayBuilder.ToString();
            return _lastPushedText;
        }

        static bool BuilderEquals(StringBuilder builder, string value)
        {
            if (value == null || builder.Length != value.Length)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (builder[i] != value[i])
                    return false;
            }
            return true;
        }

        void CaptureResolvedCharacterMap(TextStudioPlanView plan, int characterCount)
        {
            EnsurePushedCharacterMapCapacity(characterCount);
            for (int i = 0; i < characterCount; i++)
                _pushedCharacterToTextUnit[i] = plan.GetUnitIndexForResolvedChar(i);
            _pushedCharacterCount = characterCount;
            _lastPushedText = plan.ResolvedText ?? string.Empty;
        }

        void InvalidatePushedCharacterMap()
        {
            _lastPushedText = string.Empty;
            _pushedCharacterCount = 0;
        }

        void EnsurePushedCharacterMapCapacity(int count)
        {
            if (_pushedCharacterToTextUnit.Length >= count)
                return;
            _pushedCharacterToTextUnit = new int[CapacityFor(count)];
        }

        static bool IsSupportedLegacyTag(string text, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(text[start]))
                start++;
            if (start < end && text[start] == '/')
                start++;
            while (start < end && char.IsWhiteSpace(text[start]))
                start++;

            int nameEnd = start;
            while (nameEnd < end &&
                   !char.IsWhiteSpace(text[nameEnd]) &&
                   text[nameEnd] != '=')
            {
                nameEnd++;
            }

            int length = nameEnd - start;
            return EqualsOrdinalIgnoreCase(text, start, length, "b") ||
                   EqualsOrdinalIgnoreCase(text, start, length, "i") ||
                   EqualsOrdinalIgnoreCase(text, start, length, "color") ||
                   EqualsOrdinalIgnoreCase(text, start, length, "size");
        }

        static bool EqualsOrdinalIgnoreCase(
            string source,
            int start,
            int length,
            string expected)
        {
            if (length != expected.Length)
                return false;
            return string.Compare(
                       source,
                       start,
                       expected,
                       0,
                       length,
                       StringComparison.OrdinalIgnoreCase) == 0;
        }

        static void ApplyVertex(
            ref UIVertex vertex,
            in TextStudioGlyphQuadTransform transform,
            in EvaluatedGlyphState state,
            int canonicalVertexIndex,
            float effectUnitScale)
        {
            vertex.position = transform.ApplyPosition(
                vertex.position,
                state.cornerOffsets.GetCanonicalVertexOffset(canonicalVertexIndex),
                effectUnitScale);
            vertex.color = transform.ApplyColor(vertex.color);
        }

        static void HideVertices(VertexHelper vertexHelper, int vertexCount)
        {
            UIVertex vertex = default;
            for (int i = 0; i < vertexCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Color32 color = vertex.color;
                color.a = 0;
                vertex.color = color;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }

        void SetVerticesDirty()
        {
            Text target = ResolveTarget();
            if (target == null || _settingVerticesDirty)
                return;

            _settingVerticesDirty = true;
            try
            {
                target.SetVerticesDirty();
            }
            finally
            {
                _settingVerticesDirty = false;
            }
        }

        void EnsureFrameCapacity(int count)
        {
            if (_states.Length >= count)
                return;
            int capacity = CapacityFor(count);
            _states = new EvaluatedGlyphState[capacity];
            _placements = new GlyphPlacement[capacity];
            _basePositions = new Vector3[capacity];
            _effectUnitScales = new float[capacity];
            _alphaMultipliers = new float[capacity];
        }

        void SubscribeFontEvent()
        {
            if (_fontEventSubscribed)
                return;
            Font.textureRebuilt += HandleFontTextureRebuilt;
            _fontEventSubscribed = true;
        }

        void UnsubscribeFontEvent()
        {
            if (!_fontEventSubscribed)
                return;
            Font.textureRebuilt -= HandleFontTextureRebuilt;
            _fontEventSubscribed = false;
        }

        void HandleFontTextureRebuilt(Font rebuiltFont)
        {
            Text target = ResolveTarget();
            if (target == null || !ReferenceEquals(target.font, rebuiltFont))
                return;
            _layout.NotifyFontAtlasChanged();
            _pendingLayoutChange = true;
        }

        static int CapacityFor(int count)
        {
            int capacity = 4;
            while (capacity < count)
                capacity <<= 1;
            return capacity;
        }
    }
}
