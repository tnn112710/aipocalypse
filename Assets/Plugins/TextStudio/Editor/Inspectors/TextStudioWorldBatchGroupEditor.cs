using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioWorldBatchGroup)), CanEditMultipleObjects]
    internal sealed class TextStudioWorldBatchGroupEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-world-batch-group-inspector");
            Label status = TextStudioInspectorElements.CreateMutedText(string.Empty);
            HelpBox reason = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            root.Add(TextStudioInspectorElements.CreateSection("World Batch Group", "ts-world-batch-group-status",
                TextStudioInspectorElements.CreateMutedText("Draws the world-space texts below this object as one sorted unit, merging compatible appearance runs across texts. The Sorting Group places the whole assembly in the scene."),
                status));
            root.Add(reason);
            void Refresh()
            {
                if (targets.Length != 1)
                {
                    status.text = "Select one group to see its batching status.";
                    reason.style.display = DisplayStyle.None;
                    return;
                }
                TextStudioWorldBatchGroup group = (TextStudioWorldBatchGroup)target;
                status.text = group.IsMerging
                    ? $"{group.MemberCount} texts draw in {group.DrawCount} runs across {group.ShardCount} shards."
                    : $"{group.MemberCount} texts keep their own outputs.";
                reason.text = group.IneligibleReason;
                reason.style.display = string.IsNullOrEmpty(group.IneligibleReason) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            CommonInspectorElements.SchedulePeriodicRefresh(root, Refresh, 500);
            Refresh();
            return root;
        }
    }
}
