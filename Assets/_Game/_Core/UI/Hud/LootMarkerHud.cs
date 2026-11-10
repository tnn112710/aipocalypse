using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Input;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // A small bag icon over the nearest crates and bodies that still hold loot (LootRegistry): bright while something in it
    // is unsearched, dim once everything left has been seen. The list is rescanned a few times a second; only the shown
    // markers move each frame, on their own canvas.
    public sealed class LootMarkerHud : PlayerHudView
    {
        [SerializeField, Tooltip("Inactive template, cloned once per marker")] Image _template;
        [SerializeField, Min(1f), Tooltip("Metres from the player")] float _range = 15f;
        [SerializeField, Min(1)] int _maxMarkers = 8;
        [SerializeField, Min(0.02f), Tooltip("Seconds between rescans of what's in range")] float _scanInterval = 0.2f;
        [SerializeField, Tooltip("Something in it hasn't been searched yet")] Color _unsearchedColor = new(1f, 0.85f, 0.55f, 0.95f);
        [SerializeField, Tooltip("Searched, items left in it")] Color _searchedColor = new(1f, 1f, 1f, 0.4f);
        [SerializeField, Range(0.1f, 1f), Tooltip("Marker size at the edge of the range, against close by")] float _farScale = 0.6f;

        LootRegistry _registry;
        InputService _input;
        Image[] _markers;
        LootContainer[] _shown;
        float[] _distances;
        int _shownCount;
        float _nextScan;
        bool _visible;

        [Inject]
        void Construct(LootRegistry registry, InputService input)
        {
            _registry = registry;
            _input = input;
        }

        void Awake()
        {
            _markers = new Image[_maxMarkers];
            _shown = new LootContainer[_maxMarkers];
            _distances = new float[_maxMarkers];
            if (_template == null)
                return;
            _template.gameObject.SetActive(false);
            for (int i = 0; i < _maxMarkers; i++)
            {
                _markers[i] = Instantiate(_template, _template.transform.parent, false);
                _markers[i].name = $"Marker{i}";
            }
        }

        protected override void Unbind(IPlayer player) => HideAll();

        void LateUpdate()
        {
            IPlayer player = Player;
            Camera view = Camera.main;
            if (player == null || player.Health == null || player.Health.IsDead || _registry == null || _template == null || view == null
                || (_input != null && _input.IsUIRequested))
            {
                HideAll();
                return;
            }
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + _scanInterval;
                Scan(player.Transform.position);
            }
            _visible = true;
            object focus = player.Interactor != null ? player.Interactor.Focus : null;
            for (int i = 0; i < _maxMarkers; i++)
            {
                Image marker = _markers[i];
                LootContainer container = i < _shownCount ? _shown[i] : null;
                bool show = container != null && container.HoldsLoot && !ReferenceEquals(container, focus);
                Vector3 screen = show ? view.WorldToScreenPoint(container.MarkerPoint) : default;
                show &= screen.z > 0f;
                if (marker.gameObject.activeSelf != show)
                    marker.gameObject.SetActive(show);
                if (!show)
                    continue;
                marker.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
                marker.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, _farScale, Mathf.Clamp01(_distances[i] / _range));
                marker.color = container.Searched ? _searchedColor : _unsearchedColor;
            }
        }

        // The nearest containers holding loot within range, nearest first.
        void Scan(Vector3 from)
        {
            _shownCount = 0;
            float rangeSquared = _range * _range;
            var containers = _registry.Containers;
            for (int c = 0; c < containers.Count; c++)
            {
                LootContainer container = containers[c];
                if (container == null || !container.HoldsLoot)
                    continue;
                float distanceSquared = (container.transform.position - from).sqrMagnitude;
                if (distanceSquared > rangeSquared)
                    continue;
                int at = _shownCount;
                while (at > 0 && _distances[at - 1] > distanceSquared)
                    at--;
                if (at >= _maxMarkers)
                    continue;
                int last = Mathf.Min(_shownCount, _maxMarkers - 1);
                for (int i = last; i > at; i--)
                {
                    _shown[i] = _shown[i - 1];
                    _distances[i] = _distances[i - 1];
                }
                _shown[at] = container;
                _distances[at] = distanceSquared;
                if (_shownCount < _maxMarkers)
                    _shownCount++;
            }
            for (int i = 0; i < _shownCount; i++)
                _distances[i] = Mathf.Sqrt(_distances[i]);
            for (int i = _shownCount; i < _maxMarkers; i++)
                _shown[i] = null;
        }

        void HideAll()
        {
            if (!_visible || _markers == null)
                return;
            _visible = false;
            foreach (Image marker in _markers)
                if (marker != null && marker.gameObject.activeSelf)
                    marker.gameObject.SetActive(false);
        }
    }
}
