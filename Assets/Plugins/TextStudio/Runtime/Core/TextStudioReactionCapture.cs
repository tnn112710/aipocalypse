using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Declares whether capture requests a glyph-presentation copy or detachment from live text; the provider reports support separately.</summary>
    public enum TextStudioReactionCaptureIntent { Copy, Detach }

    /// <summary>Flags the glyph geometry, material state, sprites, decorations, and layered geometry retained by a Reaction capture.</summary>
    [Flags]
    public enum TextStudioReactionResourceClass
    {
        None = 0,
        GlyphGeometry = 1,
        MaterialState = 2,
        InlineSprite = 4,
        Decoration = 8,
        MultiLayerGeometry = 16
    }

    /// <summary>Reports whether outgoing-glyph capture is unsupported, supported with declared visual limitations, or exact.</summary>
    public enum TextStudioReactionCaptureSupport
    {
        Unsupported,
        SupportedWithDeclaredLimitations,
        Exact
    }

    /// <summary>Side-effect-free backend support result with a stable, allocation-free explanation.</summary>
    public readonly struct TextStudioReactionCaptureCapability
    {
        public readonly TextStudioReactionCaptureSupport Support;
        public readonly string Explanation;

        public bool IsSupported => Support != TextStudioReactionCaptureSupport.Unsupported;

        /// <summary>Creates a capture support result with an optional explanation of its limitations.</summary>
        public TextStudioReactionCaptureCapability(TextStudioReactionCaptureSupport support,
            string explanation = null)
        {
            Support = support;
            Explanation = explanation;
        }

        /// <summary>Creates an unsupported capture result with the supplied explanation.</summary>
        public static TextStudioReactionCaptureCapability Unsupported(string explanation) =>
            new TextStudioReactionCaptureCapability(TextStudioReactionCaptureSupport.Unsupported,
                explanation);
    }

    /// <summary>Requests a retained outgoing resource or a current-frame anchor snapshot.</summary>
    public readonly struct TextStudioReactionCaptureRequest
    {
        public readonly TextStudioReactionContext Context;
        public readonly TextStudioGlyphChangeSet Changes;
        public readonly TextStudioReactionCaptureIntent Intent;
        public readonly TextStudioReactionResourceClass Resources;
        public readonly string AnchorKey;

        /// <summary>Creates a retained-capture request from the reaction context, glyph changes, intent, resources, and optional named anchor.</summary>
        public TextStudioReactionCaptureRequest(in TextStudioReactionContext context,
            in TextStudioGlyphChangeSet changes, TextStudioReactionCaptureIntent intent,
            TextStudioReactionResourceClass resources, string anchorKey = null)
        {
            Context = context;
            Changes = changes;
            Intent = intent;
            Resources = resources;
            AnchorKey = anchorKey;
        }
    }

    /// <summary>Backend-owned retained output. Slots are versioned; releasing a copied handle twice must be harmless.</summary>
    public readonly struct TextStudioReactionLease
    {
        readonly ITextStudioReactionCaptureProvider _owner;
        readonly int _slot;
        readonly uint _version;

        /// <summary>Creates a versioned retained-output lease for a provider slot.</summary>
        public TextStudioReactionLease(ITextStudioReactionCaptureProvider owner, int slot, uint version)
        {
            _owner = owner;
            _slot = slot;
            _version = version;
        }

        public bool IsValid => _owner != null && _slot >= 0 && _version != 0;
        /// <summary>Attempts to resolve this retained output's current anchor when the lease is valid.</summary>
        public bool TryGetAnchor(out TextStudioReactionAnchor anchor)
        {
            if (IsValid) return _owner.TryGetAnchor(_slot, _version, out anchor);
            anchor = default;
            return false;
        }

        /// <summary>Releases this retained output through its owning provider when the lease is valid.</summary>
        public void Release()
        {
            if (IsValid) _owner.Release(_slot, _version);
        }

        public bool SupportsVisualOutput => _owner is ITextStudioReactionVisualOutputProvider;

        /// <summary>Attempts to apply a visual sample when this valid lease supports retained visual output.</summary>
        public bool TrySubmit(in TextStudioReactionVisualSample sample) =>
            IsValid && _owner is ITextStudioReactionVisualOutputProvider output &&
            output.TrySubmit(_slot, _version, in sample);

        /// <summary>Attempts to obtain this capture's retained world object and launch velocities when its provider supports them.</summary>
        public bool TryGetRetainedWorldObject(out GameObject output,
            out Vector3 linearVelocity, out Vector3 angularVelocity)
        {
            if (IsValid && _owner is ITextStudioReactionRetainedWorldObjectProvider provider)
                return provider.TryGetRetainedWorldObject(_slot, _version,
                    out output, out linearVelocity, out angularVelocity);
            output = null;
            linearVelocity = default;
            angularVelocity = default;
            return false;
        }

        /// <summary>Attempts to apply scale, opacity, and tint when this valid lease supports retained world-object output.</summary>
        public bool TrySubmitAppearance(Vector3 scale, float alpha, Color32 tint) =>
            IsValid && _owner is ITextStudioReactionRetainedWorldObjectProvider provider &&
            provider.TrySubmitAppearance(_slot, _version, scale, alpha, tint);
    }

    /// <summary>Motion and color of one retained unit in its source renderer's local text space.</summary>
    public readonly struct TextStudioReactionVisualSample
    {
        public readonly Vector3 LocalOffset;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;
        public readonly float Alpha;
        public readonly Color32 Tint;

        /// <summary>Creates a retained-output transform sample with clamped opacity and a white tint.</summary>
        public TextStudioReactionVisualSample(Vector3 localOffset, Quaternion localRotation,
            Vector3 localScale, float alpha)
            : this(localOffset, localRotation, localScale, alpha,
                new Color32(255, 255, 255, 255)) { }

        /// <summary>Creates a retained-output transform sample with clamped opacity and the supplied tint.</summary>
        public TextStudioReactionVisualSample(Vector3 localOffset, Quaternion localRotation,
            Vector3 localScale, float alpha, Color32 tint)
        {
            LocalOffset = localOffset;
            LocalRotation = localRotation;
            LocalScale = localScale;
            Alpha = Mathf.Clamp01(alpha);
            Tint = tint;
        }

        /// <summary>Reports whether opacity or any tint channel differs from the previously submitted color state.</summary>
        public bool NeedsColorUpdate(float lastAlpha, Color32 lastTint) =>
            !Mathf.Approximately(Alpha, lastAlpha) ||
            Tint.r != lastTint.r || Tint.g != lastTint.g ||
            Tint.b != lastTint.b || Tint.a != lastTint.a;

        /// <summary>Multiplies a source color by this sample's tint and opacity for retained-output rendering.</summary>
        public Color32 ApplyColor(Color32 source) => new Color32(
            (byte)(source.r * Tint.r / 255),
            (byte)(source.g * Tint.g / 255),
            (byte)(source.b * Tint.b / 255),
            (byte)Mathf.RoundToInt(source.a * (Tint.a / 255f) * Alpha));
    }

    /// <summary>Native renderer owns copied or detached resources and validates every versioned access.</summary>
    public interface ITextStudioReactionCaptureProvider
    {
        /// <summary>Reports whether the requested retained capture is supported and explains any limitation.</summary>
        TextStudioReactionCaptureCapability QuerySupport(in TextStudioReactionCaptureRequest request);
        /// <summary>Attempts to retain the requested output and return a versioned lease owned by this provider.</summary>
        bool TryCapture(in TextStudioReactionCaptureRequest request, out TextStudioReactionLease lease);
        /// <summary>Attempts to resolve the current anchor for the specified capture slot and version.</summary>
        bool TryGetAnchor(int slot, uint version, out TextStudioReactionAnchor anchor);
        /// <summary>Releases the retained capture identified by its slot and version.</summary>
        void Release(int slot, uint version);
    }

    /// <summary>Lets a text backend expose a native provider whose leases remain owned by its adapter.</summary>
    public interface ITextStudioReactionCaptureProviderSource
    {
        ITextStudioReactionCaptureProvider ReactionCaptureProvider { get; }
    }

    /// <summary>Lets a native provider prepare short-lived source data only while outgoing capture is needed.</summary>
    public interface ITextStudioReactionCapturePreparation
    {
        /// <summary>Controls whether the native provider keeps short-lived source data prepared for outgoing capture.</summary>
        void SetOutgoingCapturePrepared(bool prepared);
    }

    /// <summary>Optional presentation seam; the capture provider keeps native geometry ownership.</summary>
    public interface ITextStudioReactionVisualOutputProvider
    {
        /// <summary>Attempts to apply a local transform and color sample to the specified retained output version.</summary>
        bool TrySubmit(int slot, uint version, in TextStudioReactionVisualSample sample);
    }

    /// <summary>Optional Play Mode bridge to one backend-owned world object for a physical retained response.</summary>
    public interface ITextStudioReactionRetainedWorldObjectProvider
    {
        /// <summary>Attempts to obtain a retained world object and its launch velocities for the specified capture version.</summary>
        bool TryGetRetainedWorldObject(int slot, uint version, out GameObject output,
            out Vector3 linearVelocity, out Vector3 angularVelocity);
        /// <summary>Attempts to update scale, opacity, and tint on the specified retained world-object capture.</summary>
        bool TrySubmitAppearance(int slot, uint version, Vector3 scale, float alpha, Color32 tint);
    }

    /// <summary>Host-local semantic poses for actions that do not need a visual output lease.</summary>
    internal sealed class TextStudioReactionAnchorCaptureProvider : ITextStudioReactionCaptureProvider
    {
        struct Slot
        {
            internal TextStudioReactionAnchor Anchor;
            internal uint Version;
            internal int Next;
            internal bool Active;
        }

        readonly ITextStudioReactionActionHost _host;
        Slot[] _slots = Array.Empty<Slot>();
        int _count;
        int _freeHead = -1;

        internal TextStudioReactionAnchorCaptureProvider(ITextStudioReactionActionHost host) =>
            _host = host;

        public TextStudioReactionCaptureCapability QuerySupport(
            in TextStudioReactionCaptureRequest request) =>
            request.Intent == TextStudioReactionCaptureIntent.Copy &&
            request.Resources == TextStudioReactionResourceClass.None
                ? new TextStudioReactionCaptureCapability(TextStudioReactionCaptureSupport.Exact)
                : TextStudioReactionCaptureCapability.Unsupported(
                    "Anchor snapshots support Copy without glyph geometry or materials.");

        public bool TryCapture(in TextStudioReactionCaptureRequest request,
            out TextStudioReactionLease lease)
        {
            lease = default;
            if (!QuerySupport(in request).IsSupported) return false;
            TextStudioReactionAnchor anchor = default;
            bool anchored = string.IsNullOrEmpty(request.AnchorKey)
                ? _host.TryGetReactionAnchor(in request.Context, out anchor)
                : _host is ITextStudioReactionNamedAnchorHost namedHost &&
                  namedHost.TryGetReactionAnchor(in request.Context,
                      request.AnchorKey, out anchor);
            if (!anchored) return false;
            return TryCaptureAnchor(in anchor, out lease);
        }

        internal bool TryCaptureAnchor(in TextStudioReactionAnchor anchor,
            out TextStudioReactionLease lease)
        {
            lease = default;
            int slot;
            if (_freeHead >= 0)
            {
                slot = _freeHead;
                _freeHead = _slots[slot].Next;
            }
            else
            {
                if (_count == 4096) return false;
                if (_count == _slots.Length)
                    Array.Resize(ref _slots, Math.Min(4096,
                        Math.Max(4, _slots.Length * 2)));
                slot = _count++;
            }
            ref Slot captured = ref _slots[slot];
            unchecked { captured.Version++; }
            if (captured.Version == 0) captured.Version = 1;
            captured.Anchor = anchor;
            captured.Active = true;
            captured.Next = -1;
            lease = new TextStudioReactionLease(this, slot, captured.Version);
            return true;
        }

        public bool TryGetAnchor(int slot, uint version,
            out TextStudioReactionAnchor anchor)
        {
            if (slot >= 0 && slot < _count && _slots[slot].Active &&
                _slots[slot].Version == version)
            {
                anchor = _slots[slot].Anchor;
                return true;
            }
            anchor = default;
            return false;
        }

        public void Release(int slot, uint version)
        {
            if (slot < 0 || slot >= _count || !_slots[slot].Active ||
                _slots[slot].Version != version) return;
            _slots[slot].Active = false;
            _slots[slot].Anchor = default;
            _slots[slot].Next = _freeHead;
            _freeHead = slot;
        }
    }
}
