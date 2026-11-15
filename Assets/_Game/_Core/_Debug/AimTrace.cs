using MB13.Aipocalypse.Core.UI;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Debugging
{
    // Weapons Test dev overlay: the crosshair's path over the last seconds, magnified, sampled at a fixed rate so uneven
    // speed shows as uneven dot spacing, centred on the path's middle with a ring at the wobble envelope (plus pellets).
    // Freezes in pause. Lives under the test scene's HUD, so the HUD's scope injects it.
    public sealed class AimTrace : PlayerHudView
    {
        [SerializeField] AimTraceGraphic _graphic;
        [SerializeField, Min(0.5f), Tooltip("Seconds of path shown")] float _seconds = 2f;
        [SerializeField, Min(5f), Tooltip("Dots per second (game time): even spacing = even speed")] float _rate = 30f;
        [SerializeField, Min(1f), Tooltip("Magnification over the real screen size")] float _zoom = 3f;

        Vector2[] _samples;
        int _count;
        int _next;
        float _clock;

        void LateUpdate()
        {
            if (_graphic == null)
                return;
            int capacity = Mathf.Max(2, Mathf.RoundToInt(_seconds * _rate));
            if (_samples == null || _samples.Length != capacity)
            {
                _samples = new Vector2[capacity];
                _count = _next = 0;
            }

            if (Player?.HeldItem == null)
            {
                _count = 0;
                _graphic.Points.Clear();
                _graphic.Redraw(0f);
                return;
            }

            // CrosshairPosition and CrosshairSpread are in half screen heights.
            float scale = ((RectTransform)_graphic.canvas.rootCanvas.transform).rect.height * 0.5f * _zoom;
            float step = 1f / _rate;
            for (_clock += Time.deltaTime; _clock >= step; _clock -= step)
            {
                _samples[_next] = Player.CrosshairPosition * scale;
                _next = (_next + 1) % capacity;
                _count = Mathf.Min(_count + 1, capacity);
            }

            // The wander circles the crosshair's rest point, not the screen centre: centre on the path's middle.
            Vector2 middle = Vector2.zero;
            for (int i = 0; i < _count; i++)
                middle += _samples[i];
            middle /= Mathf.Max(1, _count);
            _graphic.Points.Clear();
            for (int i = _count; i > 0; i--)
                _graphic.Points.Add(_samples[(_next - i + capacity) % capacity] - middle);
            _graphic.Redraw(Player.CrosshairSpread * scale);
        }
    }
}
