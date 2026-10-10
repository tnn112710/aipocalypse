using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Owns every native output captured for one semantic reaction.</summary>
    internal sealed class TextStudioReactionCompositeCaptureProvider :
        ITextStudioReactionCaptureProvider, ITextStudioReactionVisualOutputProvider
    {
        struct Slot
        {
            internal TextStudioReactionLease[] Leases;
            internal int Count;
            internal int Next;
            internal uint Version;
            internal bool Active;
        }

        Slot[] _slots = Array.Empty<Slot>();
        int _count;
        int _freeHead = -1;

        internal bool TryBegin(int outputCount, out int index, out uint version)
        {
            index = -1;
            version = 0;
            if (outputCount < 2) return false;
            if (_freeHead >= 0)
            {
                index = _freeHead;
                _freeHead = _slots[index].Next;
            }
            else
            {
                if (_count == 4096) return false;
                if (_count == _slots.Length)
                    Array.Resize(ref _slots, Math.Min(4096, Math.Max(4, _slots.Length * 2)));
                index = _count++;
            }
            ref Slot slot = ref _slots[index];
            if (slot.Leases == null || slot.Leases.Length < outputCount)
                Array.Resize(ref slot.Leases, outputCount);
            slot.Count = 0;
            slot.Active = true;
            unchecked { slot.Version++; }
            if (slot.Version == 0) slot.Version = 1;
            version = slot.Version;
            return true;
        }

        internal bool Add(int index, uint version, in TextStudioReactionLease lease)
        {
            if (!lease.IsValid || !TryGetSlot(index, version, out Slot slot) ||
                slot.Count == slot.Leases.Length) return false;
            _slots[index].Leases[_slots[index].Count++] = lease;
            return true;
        }

        internal TextStudioReactionLease Seal(int index, uint version)
        {
            return TryGetSlot(index, version, out Slot slot) && slot.Count >= 2
                ? new TextStudioReactionLease(this, index, version)
                : default;
        }

        public TextStudioReactionCaptureCapability QuerySupport(
            in TextStudioReactionCaptureRequest request) =>
            TextStudioReactionCaptureCapability.Unsupported(
                "Composite capture is assembled from native renderer leases.");

        public bool TryCapture(in TextStudioReactionCaptureRequest request,
            out TextStudioReactionLease lease)
        {
            lease = default;
            return false;
        }

        public bool TryGetAnchor(int index, uint version, out TextStudioReactionAnchor anchor)
        {
            anchor = default;
            if (!TryGetSlot(index, version, out Slot slot) || slot.Count == 0) return false;
            for (int i = 0; i < slot.Count; i++)
            {
                if (!slot.Leases[i].TryGetAnchor(out TextStudioReactionAnchor current))
                    return false;
                if (i == 0) anchor = current;
            }
            return true;
        }

        public bool TrySubmit(int index, uint version,
            in TextStudioReactionVisualSample sample)
        {
            if (!TryGetSlot(index, version, out Slot slot) || slot.Count == 0) return false;
            bool allSubmitted = true;
            for (int i = 0; i < slot.Count; i++)
                allSubmitted &= slot.Leases[i].TrySubmit(in sample);
            return allSubmitted;
        }

        public void Release(int index, uint version)
        {
            if (!TryGetSlot(index, version, out Slot slot)) return;
            _slots[index].Active = false;
            _slots[index].Count = 0;
            for (int i = 0; i < slot.Count; i++)
            {
                TextStudioReactionLease lease = slot.Leases[i];
                _slots[index].Leases[i] = default;
                try { lease.Release(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            _slots[index].Next = _freeHead;
            _freeHead = index;
        }

        internal void ReleaseAll()
        {
            for (int i = 0; i < _count; i++)
                if (_slots[i].Active) Release(i, _slots[i].Version);
        }

        bool TryGetSlot(int index, uint version, out Slot slot)
        {
            if (index >= 0 && index < _count && _slots[index].Active &&
                _slots[index].Version == version)
            {
                slot = _slots[index];
                return true;
            }
            slot = default;
            return false;
        }
    }
}
