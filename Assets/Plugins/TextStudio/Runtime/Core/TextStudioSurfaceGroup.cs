using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Explicit additional lettering sources for glyph-derived surfaces on this text. The owning style renders the surface once.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TextStudioText)), DefaultExecutionOrder(10000)]
    [AddComponentMenu("Text Studio/Appearance/Surface Group")]
    public sealed class TextStudioSurfaceGroup : MonoBehaviour
    {
        [SerializeField, Tooltip("Additional text objects whose letter shapes join this text's Glyph Silhouette decorations. All must use parallel 2D planes and prepared font contours. Their own appearance stacks stay independent.")]
        TextStudioText[] _sources = Array.Empty<TextStudioText>();
        TextStudioText _owner;
        ulong _fingerprint;
        bool _hasFingerprint;
        readonly List<TextStudioText> _subscribed = new List<TextStudioText>();
        bool _subscriptionsDirty;

        public int SourceCount => _sources?.Length ?? 0;
        /// <summary>Returns the source text component at the specified group index.</summary>
        public TextStudioText GetSource(int index) => _sources[index];

        /// <summary>Copies the source array, invalidates the group's fingerprint, and refreshes subscriptions when enabled.</summary>
        public void SetSources(params TextStudioText[] sources)
        {
            _sources = sources == null ? Array.Empty<TextStudioText>() : (TextStudioText[])sources.Clone();
            _hasFingerprint = false;
            if (isActiveAndEnabled) Subscribe();
        }

        void OnEnable() { _owner = GetComponent<TextStudioText>(); _hasFingerprint = false; Subscribe(); }
        void OnValidate() { _hasFingerprint = false; _subscriptionsDirty = true; }
        void OnDisable()
        {
            Unsubscribe(); _hasFingerprint = false;
            // Teardown and assembly reload must never recreate the owner's native buffers.
            if (_owner != null) _owner.ScheduleMeshRefresh();
        }

        void Subscribe()
        {
            Unsubscribe(); _subscriptionsDirty = false;
            for (int i = 0; i < SourceCount; i++)
                if (IsUniqueSource(i))
                {
                    _subscribed.Add(_sources[i]);
                    _sources[i].PresentationUpdated += RefreshGeometry;
                    _sources[i].RetainHiddenGlyphGeometry(true);
                }
        }

        void Unsubscribe()
        {
            foreach (TextStudioText source in _subscribed)
            {
                if (source == null) continue;
                source.PresentationUpdated -= RefreshGeometry;
                source.RetainHiddenGlyphGeometry(false);
            }
            _subscribed.Clear();
        }

        void LateUpdate() => RefreshGeometry();

        void RefreshGeometry()
        {
            if (_subscriptionsDirty) Subscribe();
            if (_owner == null || !_owner.isActiveAndEnabled) return;
            TextStudioDeterministicHash64 hash = default;
            AddFingerprint(ref hash, includeOwner: true);
            if (_hasFingerprint && _fingerprint == hash.Value) return;
            _fingerprint = hash.Value; _hasFingerprint = true;
            // Source completion also runs after editor-ticker updates, which do not follow MonoBehaviour LateUpdate order.
            _owner.ForceMeshRefresh(0);
        }

        internal void AddFingerprint(ref TextStudioDeterministicHash64 hash, bool includeOwner = false)
        {
            hash.Add(SourceCount);
            if (includeOwner) HashSource(ref hash, _owner);
            for (int i = 0; i < SourceCount; i++)
                if (IsUniqueSource(i)) HashSource(ref hash, _sources[i]);
        }

        bool IsUniqueSource(int index)
        {
            TextStudioText source = _sources[index];
            if (source == null || source == _owner) return false;
            for (int previous = 0; previous < index; previous++) if (_sources[previous] == source) return false;
            return true;
        }

        void HashSource(ref TextStudioDeterministicHash64 hash, TextStudioText source)
        {
            if (source == null) { hash.Add(false); return; }
            hash.Add(source.isActiveAndEnabled);
            if (!source.isActiveAndEnabled) return;
            Matrix4x4 matrix = transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
            for (int i = 0; i < 16; i++) hash.Add(matrix[i]);
            MeshRenderer renderer = source.GetComponent<MeshRenderer>();
            hash.Add(renderer != null ? renderer.sortingLayerID : 0);
            hash.Add(renderer != null ? renderer.sortingOrder : 0);
            TMP_Text text = source.GetComponent<TMP_Text>();
            TMP_TextInfo info = text != null ? text.textInfo : null;
            hash.Add(info?.characterCount ?? 0);
            bool ready = info?.characterInfo != null && info.characterCount >= 0 && info.characterCount <= info.characterInfo.Length;
            hash.Add(ready);
            if (!ready) { _hasFingerprint = false; return; }
            for (int i = 0; i < info.characterCount; i++)
            {
                ref readonly TMP_CharacterInfo character = ref info.characterInfo[i];
                hash.Add(character.isVisible); hash.Add(character.textElement?.glyphIndex ?? 0);
                TextStudioFontAppearanceData font = source.FindAppearanceFont(character.fontAsset) ?? _owner.FindAppearanceFont(character.fontAsset);
                hash.Add(font?.SourceHash); hash.Add(font?.Revision ?? 0);
                if (character.isVisible && TextStudio2DTMPGlyphPlane.TryCreate(text, i, out TextStudio2DTMPGlyphPlane plane))
                {
                    TextStudioGlyphSilhouette.HashPlane(ref hash, in plane);
                    hash.Add(plane.MaximumAlpha);
                }
            }
        }

        internal float MaximumSourceAlpha()
        {
            float alpha = 0;
            for (int index = 0; index < SourceCount; index++)
            {
                if (!IsUniqueSource(index) || !_sources[index].isActiveAndEnabled) continue;
                TMP_Text text = _sources[index].GetComponent<TMP_Text>();
                int count = text != null ? text.textInfo.characterCount : 0;
                for (int glyph = 0; glyph < count; glyph++)
                    if (text.textInfo.characterInfo != null && glyph < text.textInfo.characterInfo.Length && text.textInfo.characterInfo[glyph].isVisible &&
                        TextStudio2DTMPGlyphPlane.TryCreate(text, glyph, out TextStudio2DTMPGlyphPlane plane))
                        alpha = Mathf.Max(alpha, plane.MaximumAlpha);
            }
            return alpha;
        }

        // A shared backing must precede its lettering regardless of camera distance or changing text bounds.
        // Change only the generated output; authored renderer sorting remains untouched.
        internal static void ResolveWorldSorting(TMP_Text text, MeshRenderer authored, out int layer, out int order)
        {
            layer = authored.sortingLayerID;
            order = authored.sortingOrder;
            if (!text.TryGetComponent(out TextStudioSurfaceGroup group) || !group.isActiveAndEnabled) return;
            for (int index = 0; index < group.SourceCount; index++)
            {
                if (!group.IsUniqueSource(index) || !group._sources[index].isActiveAndEnabled) continue;
                if (!group._sources[index].TryGetComponent(out MeshRenderer source)) continue;
                int sourceLayer = SortingLayer.GetLayerValueFromID(source.sortingLayerID);
                int currentLayer = SortingLayer.GetLayerValueFromID(layer);
                if (sourceLayer < currentLayer)
                {
                    layer = source.sortingLayerID;
                    order = Mathf.Max(short.MinValue, source.sortingOrder - 1);
                }
                else if (sourceLayer == currentLayer)
                    order = Mathf.Min(order, Mathf.Max(short.MinValue, source.sortingOrder - 1));
            }
        }

        internal void AppendContours(List<Vector2[]> contours, float tolerance, bool followTypewriter = false)
        {
            if (SourceCount > 16) throw new InvalidOperationException("A surface group supports at most 16 additional text sources.");
            for (int sourceIndex = 0; sourceIndex < SourceCount; sourceIndex++)
            {
                if (!IsUniqueSource(sourceIndex)) continue;
                TextStudioText source = _sources[sourceIndex];
                if (!source.isActiveAndEnabled) continue;
                TMP_Text text = source.GetComponent<TMP_Text>();
                if (text == null) throw new NotSupportedException("Grouped glyph surfaces currently require TextMeshPro sources.");
                if (Mathf.Abs(Vector3.Dot(transform.forward, source.transform.forward)) < .9999f)
                    throw new NotSupportedException("Grouped glyph surfaces require parallel text planes.");
                Matrix4x4 matrix = transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                TMP_TextInfo info = text.textInfo;
                if (info?.characterInfo == null || info.characterCount < 0 || info.characterCount > info.characterInfo.Length)
                    throw new InvalidOperationException("A grouped text source is rebuilding its character layout.");
                for (int index = 0; index < info.characterCount; index++)
                {
                    ref readonly TMP_CharacterInfo character = ref info.characterInfo[index];
                    if (!character.isVisible) continue;
                    TextStudioFontAppearanceData font = source.FindAppearanceFont(character.fontAsset) ?? _owner.FindAppearanceFont(character.fontAsset);
                    if (character.elementType != TMP_TextElementType.Character || font == null)
                        throw new NotSupportedException("Every grouped glyph needs prepared font contours. Inline images are not silhouette sources.");
                    if (!TextStudio2DTMPGlyphPlane.TryCreate(text, index, out TextStudio2DTMPGlyphPlane plane))
                        throw new InvalidOperationException("A grouped text source has not produced a usable glyph plane yet.");
                    if (followTypewriter && !plane.HasVisibleSource) continue;
                    TextStudioGlyphSilhouette.Append(contours, font, character.textElement.glyphIndex, in plane, matrix, tolerance);
                }
            }
        }
    }
}
