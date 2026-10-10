using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        TextStudioWorldBatchGroup _worldBatchGroup;
        bool _worldBatched;

        /// <summary>True while an explicit <see cref="TextStudioWorldBatchGroup"/> draws this text's output in its shards.</summary>
        internal bool IsWorldBatched => _worldBatched;

        /// <summary>The nearest enabled world batch group that collected this text.</summary>
        internal TextStudioWorldBatchGroup WorldBatchGroup => _worldBatchGroup;

        /// <summary>
        /// Applied by the owning group. A batched text keeps building its primitives, draws them only through the group and
        /// hides its own world renderers; leaving the group restores them on the next presentation update.
        /// </summary>
        internal void SetWorldBatchState(TextStudioWorldBatchGroup group, bool batched)
        {
            _worldBatchGroup = group;
            if (_worldBatched == batched) return;
            _worldBatched = batched;
            _tmpBridge?.SetWorldOutputSuppressed(batched);
            ScheduleMeshRefresh();
        }

        internal bool TryGetWorldBatchPrimitives(out TextStudio2DOrderedRenderer ordered, out TextStudio2DHeroCompositor hero)
        {
            ordered = _tmpBridge?.OrderedRenderer;
            hero = _tmpBridge?.HeroCompositor;
            return ordered != null || hero != null;
        }

        // Enable, disable and reparenting change which group owns this text; both the old and the new group rescan.
        void NotifyWorldBatchGroups()
        {
            TextStudioWorldBatchGroup group = GetComponentInParent<TextStudioWorldBatchGroup>();
            if (group != null) group.MarkMembershipDirty();
            if (_worldBatchGroup != null && _worldBatchGroup != group) _worldBatchGroup.MarkMembershipDirty();
        }

        void ReleaseWorldBatchState()
        {
            TextStudioWorldBatchGroup group = _worldBatchGroup;
            _worldBatchGroup = null;
            _worldBatched = false;
            if (group != null) group.MarkMembershipDirty();
        }

        void OnTransformParentChanged()
        {
            if (isActiveAndEnabled) NotifyWorldBatchGroups();
        }
    }
}
