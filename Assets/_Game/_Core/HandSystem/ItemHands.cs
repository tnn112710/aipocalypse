using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.HandSystem
{
    // Where the hands hold an item: one grip per hand. Grips sit under the part they hold (or on clip-animated hand
    // targets), so hands follow everything the item's view moves. FirstPersonHands places the hands.
    [DisallowMultipleComponent]
    public sealed class ItemHands : MonoBehaviour
    {
        [SerializeField] HandGrip _right;
        [SerializeField, Tooltip("None = this hand stays off screen (one-handed item)")] HandGrip _left;
        [SerializeField, Min(0.1f), Tooltip("Hand size for this item; the hand's grip point stays on its contact")] float _handScale = 1f;

        public float HandScale => _handScale;

        public HandGrip GetGrip(HandSide side) => side == HandSide.Left ? _left : _right;

#if UNITY_EDITOR
        bool _previewing;

        // Ghost hands on both grips, kept there every editor frame so clips can be scrubbed with the hands visible.
        [FoldoutGroup("Preview"), Button("Show hands"), HideIf(nameof(_previewing))]
        void ShowPreview()
        {
            if (Application.isPlaying)
                return;
            _previewing = true;
            HandPreview.Show(this, HandSide.Left, () => _left);
            HandPreview.Show(this, HandSide.Right, () => _right);
        }

        [FoldoutGroup("Preview"), Button("End preview"), ShowIf(nameof(_previewing))]
        void EndPreview()
        {
            _previewing = false;
            HandPreview.Hide(this);
        }

        // Fits every grip of this item to its meshes.
        [FoldoutGroup("Preview"), Button("Bake fit for all grips")]
        void BakeAll()
        {
            foreach (HandGrip grip in GetComponentsInChildren<HandGrip>(true))
            {
                UnityEditor.Undo.RecordObject(grip, "Bake hand fit");
                HandFit.Bake(grip);
                UnityEditor.EditorUtility.SetDirty(grip);
            }
        }

        void OnDestroy()
        {
            if (!Application.isPlaying && _previewing)
                EndPreview();
        }
#endif
    }
}
