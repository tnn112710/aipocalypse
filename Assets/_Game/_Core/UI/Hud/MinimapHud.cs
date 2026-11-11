using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.MapSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // Radar around the active player, heading up: living enemies within MinimapStyle.Range (EnemyRegistry, when the scene has
    // one) and every MapMarker (its kind may pin it to the rim when out of range). Rebuilt every frame on its own canvas.
    public sealed class MinimapHud : PlayerHudView
    {
        [SerializeField] MinimapView _view;

        EnemyRegistry _enemies;
        MapMarkerRegistry _markers;
        Vector2[] _enemyPositions;

        [Inject]
        void Construct(IObjectResolver resolver, MapMarkerRegistry markers)
        {
            resolver.TryResolve(out _enemies);
            _markers = markers;
        }

        protected override void Start()
        {
            _enemyPositions = new Vector2[_view.MaxEnemies];
            base.Start();
        }

        void LateUpdate()
        {
            IPlayer player = Player;
            MinimapStyle style = _view.Style;
            bool show = player != null && style != null && style.Enabled && _enemyPositions != null;
            _view.Show(show);
            if (!show)
                return;

            Vector3 origin = player.Transform.position;
            Transform look = player.View != null ? player.View : player.Transform;
            float heading = MinimapMath.Heading(look.forward);
            float radius = Mathf.Max(_view.Radius - style.EdgeInset, 1f);
            float scale = _view.Radius / style.Range;
            _view.SetHeading(heading);

            int count = 0;
            if (_enemies != null)
            {
                float radiusSquared = radius * radius;
                for (int i = 0; i < _enemies.Count && count < _enemyPositions.Length; i++)
                {
                    EnemyBrain enemy = _enemies[i];
                    if (enemy.IsDead)
                        continue;
                    Vector2 position = MinimapMath.ToDisc(enemy.transform.position - origin, heading, scale);
                    if (position.sqrMagnitude <= radiusSquared)
                        _enemyPositions[count++] = position;
                }
            }
            _view.SetEnemies(_enemyPositions, count);

            int shown = 0;
            if (_markers != null)
            {
                var markers = _markers.Markers;
                for (int i = 0; i < markers.Count && shown < _view.MaxMarkers; i++)
                {
                    MapMarker marker = markers[i];
                    MapMarkerKind kind = marker.Kind;
                    if (kind == null)
                        continue;
                    Vector2 position = MinimapMath.ToDisc(marker.transform.position - origin, heading, scale);
                    if (!MinimapMath.ClampToRadius(ref position, radius) && !kind.PinToEdge)
                        continue;
                    _view.SetMarker(shown++, position, kind);
                }
            }
            _view.HideMarkersFrom(shown);
        }

        protected override void Unbind(IPlayer player) => _view.Show(false);
    }
}
