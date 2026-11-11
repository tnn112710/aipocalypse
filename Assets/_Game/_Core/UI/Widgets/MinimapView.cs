using MB13.Aipocalypse.Core.MapSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // Heading-up radar disc: N / E / S / W on the rim, the player fixed in the middle, pooled enemy dots and marker icons placed
    // where it's told (pixels from the centre). Knows nothing about the player, enemies or the world.
    public sealed class MinimapView : MonoBehaviour
    {
        [SerializeField, InlineEditor] MinimapStyle _style;
        [SerializeField, Tooltip("Shown and hidden as a whole")] GameObject _content;
        [SerializeField, Tooltip("Its half width is the radius")] RectTransform _disc;
        [SerializeField] Image _background;
        [SerializeField] Image _ring;
        [SerializeField] Image _viewCone;
        [SerializeField] Image _player;
        [SerializeField] TMP_Text _north;
        [SerializeField] TMP_Text _east;
        [SerializeField] TMP_Text _south;
        [SerializeField] TMP_Text _west;
        [SerializeField, Tooltip("Inactive template, cloned once per dot")] Image _enemyTemplate;
        [SerializeField, Tooltip("Inactive template (background + an Icon child image), cloned once per marker")] Image _markerTemplate;
        [SerializeField, Min(1)] int _maxEnemies = 48;
        [SerializeField, Min(1)] int _maxMarkers = 12;

        RectTransform[] _enemies;
        Image[] _markerBacks;
        Image[] _markerIcons;
        MapMarkerKind[] _markerKinds;
        int _shownEnemies;
        int _shownMarkers;
        float _heading = float.NaN;
        bool _visible = true;

        public MinimapStyle Style => _style;
        public float Radius => _disc.rect.width * 0.5f;
        public int MaxEnemies => _maxEnemies;
        public int MaxMarkers => _maxMarkers;

        void Awake()
        {
            _enemies = new RectTransform[_maxEnemies];
            _enemyTemplate.gameObject.SetActive(false);
            for (int i = 0; i < _maxEnemies; i++)
            {
                Image dot = Instantiate(_enemyTemplate, _enemyTemplate.transform.parent, false);
                dot.name = $"Enemy{i}";
                _enemies[i] = dot.rectTransform;
            }

            _markerBacks = new Image[_maxMarkers];
            _markerIcons = new Image[_maxMarkers];
            _markerKinds = new MapMarkerKind[_maxMarkers];
            _markerTemplate.gameObject.SetActive(false);
            for (int i = 0; i < _maxMarkers; i++)
            {
                Image back = Instantiate(_markerTemplate, _markerTemplate.transform.parent, false);
                back.name = $"Marker{i}";
                _markerBacks[i] = back;
                _markerIcons[i] = back.transform.GetChild(0).GetComponent<Image>();
            }
            ApplyStyle();
        }

        public void Show(bool visible)
        {
            if (visible == _visible)
                return;
            _visible = visible;
            _content.SetActive(visible);
        }

        // Compass heading the player faces (0 = north): turns the letters around the rim.
        public void SetHeading(float heading)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(heading, _heading)) < 0.05f)
                return;
            _heading = heading;
            float radius = Radius + (_style != null ? _style.LetterOffset : 0f);
            Place(_north, Vector3.forward, heading, radius);
            Place(_east, Vector3.right, heading, radius);
            Place(_south, Vector3.back, heading, radius);
            Place(_west, Vector3.left, heading, radius);
        }

        // The first `count` positions get a dot; the rest of the pool hides.
        public void SetEnemies(Vector2[] positions, int count)
        {
            count = Mathf.Min(count, _maxEnemies);
            for (int i = 0; i < count; i++)
            {
                RectTransform dot = _enemies[i];
                dot.anchoredPosition = positions[i];
                if (i >= _shownEnemies)
                    dot.gameObject.SetActive(true);
            }
            for (int i = count; i < _shownEnemies; i++)
                _enemies[i].gameObject.SetActive(false);
            _shownEnemies = count;
        }

        public void SetMarker(int index, Vector2 position, MapMarkerKind kind)
        {
            if (index >= _maxMarkers)
                return;
            Image back = _markerBacks[index];
            back.rectTransform.anchoredPosition = position;
            if (_markerKinds[index] != kind)
            {
                _markerKinds[index] = kind;
                Paint(back, _markerIcons[index], kind);
            }
            if (index >= _shownMarkers)
            {
                back.gameObject.SetActive(true);
                _shownMarkers = index + 1;
            }
        }

        // Hides markers from `index` on (call with the number set this frame).
        public void HideMarkersFrom(int index)
        {
            for (int i = Mathf.Max(index, 0); i < _shownMarkers; i++)
                _markerBacks[i].gameObject.SetActive(false);
            _shownMarkers = Mathf.Min(_shownMarkers, Mathf.Max(index, 0));
        }

        [Button, PropertyOrder(-1)]
        public void ApplyStyle()
        {
            if (_style == null || _enemyTemplate == null)
                return;

            Paint(_background, _style.BackgroundSprite, _style.BackgroundColor);
            Paint(_ring, _style.RingSprite, _style.RingColor);
            Paint(_player, _style.PlayerSprite, _style.PlayerColor);
            Paint(_viewCone, _style.ViewConeSprite, _style.ViewConeColor);

            Paint(_enemyTemplate, _style.EnemySprite, _style.EnemyColor);
            _enemyTemplate.rectTransform.sizeDelta = Vector2.one * _style.EnemySize;
            if (_enemies != null)
                foreach (RectTransform dot in _enemies)
                {
                    var image = dot.GetComponent<Image>();
                    Paint(image, _style.EnemySprite, _style.EnemyColor);
                    dot.sizeDelta = Vector2.one * _style.EnemySize;
                }

            foreach (TMP_Text letter in new[] { _north, _east, _south, _west })
            {
                if (_style.LetterFont != null)
                    letter.font = _style.LetterFont;
                if (_style.LetterMaterial != null)
                    letter.fontSharedMaterial = _style.LetterMaterial;
                letter.fontSize = _style.LetterSize;
                letter.color = letter == _north ? _style.NorthColor : _style.LetterColor;
            }
            _heading = float.NaN;
            SetHeading(0f);
        }

        void Place(TMP_Text letter, Vector3 direction, float heading, float radius) =>
            letter.rectTransform.anchoredPosition = MinimapMath.ToDisc(direction, heading, radius);

        static void Paint(Image back, Image icon, MapMarkerKind kind)
        {
            back.rectTransform.sizeDelta = Vector2.one * kind.Size;
            Paint(back, kind.Background, kind.BackgroundColor);
            Paint(icon, kind.Icon, kind.IconColor);
        }

        static void Paint(Image image, Sprite sprite, Color color)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.type = StyleUtils.ImageTypeFor(sprite);
            image.color = color;
            image.enabled = sprite != null;
        }

#if UNITY_EDITOR
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                ApplyStyle();
        };
#endif
    }
}
