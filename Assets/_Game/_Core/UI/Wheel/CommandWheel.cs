using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // The command wheel's view (passive): slices laid round the middle, the highlighted one lit. CommandWheelPresenter
    // drives it; the view attaches itself when the scene has a presenter.
    public sealed class CommandWheel : MonoBehaviour
    {
        [SerializeField] CommandWheelStyle _style;
        [SerializeField, Tooltip("Shown while open")] CanvasGroup _root;
        [SerializeField] Image _backdrop;
        [SerializeField, Tooltip("Slot backings, in slice order (as many as the most slices a wheel shows)")] Image[] _slots;
        [SerializeField, Tooltip("One label per slot")] TMP_Text[] _labels;

        readonly List<WheelSlice> _shown = new();

        [Inject]
        void Construct(IObjectResolver resolver)
        {
            if (resolver.TryResolve(out CommandWheelPresenter presenter))
                presenter.Attach(this);
        }

        void Awake() => Hide();

        public void Setup(CommandWheelStyle style, CanvasGroup root, Image backdrop, Image[] slots, TMP_Text[] labels)
        {
            _style = style;
            _root = root;
            _backdrop = backdrop;
            _slots = slots;
            _labels = labels;
        }

        public void Show(IReadOnlyList<WheelSlice> slices)
        {
            _shown.Clear();
            for (int i = 0; i < slices.Count; i++)
                _shown.Add(slices[i]);
            if (_backdrop != null && _style != null)
                _backdrop.sprite = _style.Backdrop;
            for (int i = 0; i < _slots.Length; i++)
            {
                bool used = i < slices.Count;
                _slots[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                float angle = i * Mathf.PI * 2f / slices.Count;
                float radius = _style != null ? _style.Radius : 170f;
                _slots[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                if (_style != null)
                    _slots[i].sprite = _style.Slice;
                _labels[i].SetText(slices[i].Label);
            }
            Highlight(-1);
            if (_root != null)
                _root.alpha = 1f;
        }

        public void Highlight(int index)
        {
            if (_style == null)
                return;
            for (int i = 0; i < _shown.Count && i < _slots.Length; i++)
            {
                WheelSlice slice = _shown[i];
                _slots[i].color = !slice.Enabled ? _style.Disabled : i == index ? _style.Highlighted : _style.Normal;
                _labels[i].color = !slice.Enabled ? _style.DisabledLabel : slice.Current ? _style.Current : _style.Label;
                _slots[i].rectTransform.localScale = Vector3.one * (i == index && slice.Enabled ? 1.08f : 1f);
            }
        }

        public void Hide()
        {
            _shown.Clear();
            if (_root != null)
                _root.alpha = 0f;
        }
    }
}
