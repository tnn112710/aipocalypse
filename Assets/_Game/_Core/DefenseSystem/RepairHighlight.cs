using Aipocalypse.Core.Ghosts;
using Aipocalypse.Core.Interaction;
using Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using VContainer.Unity;

namespace Aipocalypse.Core.DefenseSystem
{
    // While the player holds a repair tool, every damaged barrier in range gets a see-through tint over its model, seen through
    // walls and coloured by its health (RepairHighlightStyle), so the ones to mend stand out along a long fence.
    public sealed class RepairHighlight : ITickable
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RimColorId = Shader.PropertyToID("_RimColor");

        readonly BarrierRegistry _barriers;
        readonly PlayerRegistry _players;
        readonly RepairHighlightStyle _style;
        readonly MaterialPropertyBlock _props = new();

        public RepairHighlight(BarrierRegistry barriers, PlayerRegistry players, RepairHighlightStyle style)
        {
            _barriers = barriers;
            _players = players;
            _style = style;
        }

        public void Tick()
        {
            if (_style == null || _style.Look == null || _players.Current is not IRepairer { HasTool: true })
                return;
            Vector3 from = _players.Current.Transform.position;
            float range = _style.Range * _style.Range;
            foreach (BarrierSegment segment in _barriers.Segments)
            {
                float sqrDistance = (segment.transform.position - from).sqrMagnitude;
                if (!segment.NeedsRepair || sqrDistance > range)
                    continue;
                GhostShape overlay = segment.Overlay;
                if (overlay == null)
                    continue;
                float opacity = _style.OpacityAt(Mathf.Sqrt(sqrDistance));
                Color fill = _style.Fill.Evaluate(segment.HealthShare);
                // The glow is HDR: fading only its alpha would still add light (and bloom), so its colour fades too.
                Color rim = fill * (_style.RimIntensity * opacity);
                fill.a *= opacity;
                rim.a = _style.RimAlpha * opacity;
                _props.SetColor(ColorId, fill);
                _props.SetColor(RimColorId, rim);
                GhostDrawer.Draw(new GhostView(overlay, Matrix4x4.identity, GhostState.Valid), _style.Look, props: _props);
            }
        }
    }
}
