using MB13.Aipocalypse.Core.DogSystem;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // The companion dog's toughness bar with its icon: shown while it follows within HudRange or fights, faded out
    // otherwise; the warning colour below HudWarnBelow; a dimmed icon while it's down. Scenes without a dog: hidden.
    public sealed class DogHud : MonoBehaviour
    {
        const float FadeSpeed = 4f;

        [SerializeField] CanvasGroup _group;
        [SerializeField] StatBar _bar;
        [SerializeField] Image _icon;

        DogRoster _roster;
        DogManager _manager;

        [Inject]
        void Construct(IObjectResolver resolver)
        {
            resolver.TryResolve(out _roster);
            resolver.TryResolve(out _manager);
        }

        public void Setup(CanvasGroup group, StatBar bar, Image icon)
        {
            _group = group;
            _bar = bar;
            _icon = icon;
        }

        void Awake()
        {
            if (_group != null)
                _group.alpha = 0f;
        }

        void LateUpdate()
        {
            Dog dog = _roster?.Dog;
            bool show = false;
            if (dog != null && dog.Active && _manager != null && _roster.Enabled)
            {
                DogSettings settings = _roster.Settings;
                float distance = _roster.HasPlayer ? Vector3.Distance(dog.transform.position, _roster.PlayerPosition) : float.MaxValue;
                show = _manager.InFight || dog.State.Down || (dog.State.Order == DogOrder.Follow && distance <= settings.HudRange);
                float max = _manager.Stats.Get(DogStat.Toughness);
                _bar.SetValue(dog.State.Toughness, max);
                _bar.SetWarning(dog.State.Down || dog.State.Toughness < max * settings.HudWarnBelow);
                if (_icon != null)
                    _icon.color = dog.State.Down ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }
            if (_group != null)
                _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
