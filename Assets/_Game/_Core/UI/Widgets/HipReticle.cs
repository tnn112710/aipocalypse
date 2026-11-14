using System;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // One hip reticle design (a prefab under UI/Prefabs/Widgets/Reticles): a centre mark and spread parts pushed out to the
    // gun's wobble. CrosshairHud spawns it and moves it with the barrel; a new design is a new prefab with this component.
    public sealed class HipReticle : MonoBehaviour
    {
        [Serializable]
        public struct SpreadPart
        {
            [Tooltip("Its pivot sits at the end nearest the centre, so the gap is the distance to that end")] public Graphic Graphic;
            [Tooltip("Which way it moves out as the wobble grows (e.g. up = 0, 1)")] public Vector2 Direction;
        }

        [SerializeField, Tooltip("Where the barrel points: always drawn (faded while aiming or sprinting); may be empty")] Graphic _dot;
        [SerializeField, Tooltip("Pushed out to the wobble's edge; hidden for items that don't wobble; may be empty")] SpreadPart[] _spreadParts;

        float _dotAlpha = -1f;
        float _shownGap = -1f;
        float _spreadAlpha = -1f;

        // `gap` in pixels from the centre; the spread parts hide at `spreadAlpha` 0.
        public void Show(float gap, float dotAlpha, float spreadAlpha)
        {
            if (_dot != null && !Mathf.Approximately(dotAlpha, _dotAlpha))
                _dot.canvasRenderer.SetAlpha(_dotAlpha = dotAlpha);
            if (_spreadParts == null)
                return;
            // Under half a pixel of change isn't worth a canvas rebuild.
            bool move = Mathf.Abs(gap - _shownGap) > 0.5f;
            bool fade = !Mathf.Approximately(spreadAlpha, _spreadAlpha);
            if (!move && !fade)
                return;
            if (move)
                _shownGap = gap;
            _spreadAlpha = spreadAlpha;
            foreach (SpreadPart part in _spreadParts)
            {
                if (part.Graphic == null)
                    continue;
                if (move)
                    part.Graphic.rectTransform.anchoredPosition = part.Direction.normalized * gap;
                part.Graphic.canvasRenderer.SetAlpha(spreadAlpha);
            }
        }
    }
}
