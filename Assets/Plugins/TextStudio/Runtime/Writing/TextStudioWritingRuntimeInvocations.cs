using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>One direct or Composed Writing invocation resolved for a renderer frame.</summary>
    internal sealed class TextStudioWritingRuntimeInvocation
    {
        internal string Key = string.Empty;
        internal CompiledTextStudioWritingStyle Style;
        internal TextStudioRenderPropertyRegistration Registration;
        internal bool HasRegistration;
        internal TextStudioCompositionLayer Layer;
        internal float[] Progress = Array.Empty<float>();
        internal float[] Opacity = Array.Empty<float>();
        internal float[] SourceAlpha = Array.Empty<float>();
        internal TextStudioWritingSourceKind[] SourceKinds =
            Array.Empty<TextStudioWritingSourceKind>();

        internal void EnsureCapacity(int count)
        {
            if (Progress.Length >= count)
                return;
            int capacity = TextStudioBufferCapacity.ForCount(count);
            Progress = new float[capacity];
            Opacity = new float[capacity];
            SourceAlpha = new float[capacity];
            SourceKinds = new TextStudioWritingSourceKind[capacity];
        }
    }

    /// <summary>
    /// Allocation-stable direct and Composed Writing collector shared by every renderer. This is
    /// the canonical invocation order and prevents a renderer-specific motion or fallback stack.
    /// </summary>
    internal sealed class TextStudioWritingRuntimeInvocationSet
    {
        const string DirectKey = "__direct";

        readonly List<TextStudioWritingRuntimeInvocation> _items =
            new List<TextStudioWritingRuntimeInvocation>(4);
        int _count;

        internal int Count => _count;

        internal TextStudioWritingRuntimeInvocation this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _items[index];
            }
        }

        internal void Build(in TextStudioFrame frame)
        {
            int writeIndex = 0;
            CompiledTextStudioWritingStyle direct = frame.WritingStyle;
            if (direct != null && direct.IsValid && !direct.IsPassThrough)
            {
                TextStudioWritingRuntimeInvocation invocation =
                    GetOrCreate(writeIndex++);
                invocation.Key = DirectKey;
                invocation.Style = direct;
                invocation.Registration = default;
                invocation.HasRegistration = false;
                invocation.Layer = null;
            }

            TextStudioRenderPropertyView properties = frame.RenderProperties;
            for (int activeIndex = 0;
                 activeIndex < properties.ActiveEntryCount;
                 activeIndex++)
            {
                int runtimeIndex = properties.GetActiveRuntimeIndex(activeIndex);
                if (runtimeIndex < 0 ||
                    !TextStudioRenderPropertyRegistry.TryGetRegistration(
                        runtimeIndex,
                        out TextStudioRenderPropertyRegistration registration) ||
                    !TextStudioComposedWritingRegistry.TryGetLayer(
                        registration.Descriptor.Id,
                        out TextStudioCompositionLayer layer))
                {
                    continue;
                }
                CompiledTextStudioWritingStyle style =
                    layer.ResolveCompiledWritingStyle();
                if (style == null || !style.IsValid || style.IsPassThrough)
                    continue;
                TextStudioWritingRuntimeInvocation invocation =
                    GetOrCreate(writeIndex++);
                invocation.Key = registration.Descriptor.Id;
                invocation.Style = style;
                invocation.Registration = registration;
                invocation.HasRegistration = true;
                invocation.Layer = layer;
            }
            _count = writeIndex;
        }

        internal ulong BuildFingerprint()
        {
            TextStudioDeterministicHash64 hash = default;
            hash.Add(_count);
            for (int index = 0; index < _count; index++)
            {
                TextStudioWritingRuntimeInvocation invocation = _items[index];
                hash.Add(invocation.Key);
                hash.Add(invocation.Style.Fingerprint.Topology);
                hash.Add(invocation.Style.Fingerprint.Parameters);
                hash.Add(invocation.Style.Fingerprint.Bounds);
                hash.Add(invocation.Style.Fingerprint.RenderState);
            }
            return hash.Value;
        }

        TextStudioWritingRuntimeInvocation GetOrCreate(int index)
        {
            if (index < _items.Count)
                return _items[index];
            TextStudioWritingRuntimeInvocation invocation =
                new TextStudioWritingRuntimeInvocation();
            _items.Add(invocation);
            return invocation;
        }
    }
}
