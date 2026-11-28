using MB13.Aipocalypse.Core.UI;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // The home campfire's fuel bar (fuel / max) with a fire icon, faded in only when it matters: lit and below
    // CampfireSettings.WarnBelow (anywhere: time to head home), or out while it's dark and the player is within FuelBarRange
    // and can do something about it (fuel in the pit, wood carried or in a pile).
    // Warning colour below the shrink share or while out. Hidden in scenes without a CampfireInstaller.
    public sealed class CampfireFuelHud : PlayerHudView
    {
        const float FadeSpeed = 3f;
        const float NightDarkness = 0.5f;

        [SerializeField] CanvasGroup _group;
        [SerializeField] StatBar _bar;

        CampfireRegistry _campfires;

        [Inject]
        void Construct(IObjectResolver resolver) => resolver.TryResolve(out _campfires);

        public void Setup(CanvasGroup group, StatBar bar)
        {
            _group = group;
            _bar = bar;
        }

        void Awake()
        {
            if (_group != null)
                _group.alpha = 0f;
        }

        public static bool ShouldShow(bool hasFire, bool lit, float share, float warnBelow, float darkness, bool nearFire, bool canAct) =>
            hasFire && (lit ? share < warnBelow : darkness > NightDarkness && nearFire && canAct);

        // Wood to put on an empty pit: carried, or in a pile.
        bool CanFeed(Campfire fire)
        {
            if (Player?.Equipment != null && fire.ActionFor(Player.Equipment.Inventory) == CampfireAction.AddWood)
                return true;
            foreach (WoodPile pile in _campfires.Piles)
                if (pile != null && pile.Stock > 0)
                    return true;
            return false;
        }

        void LateUpdate()
        {
            Campfire fire = _campfires?.Home;
            CampfireSettings settings = fire != null ? fire.Settings : null;
            bool show = false;
            if (fire != null && settings != null)
            {
                CampfireFuel fuel = fire.Fuel;
                float darkness = 0f;
                bool near = false;
                if (Player != null)
                {
                    darkness = Player.Darkness;
                    Vector3 offset = Player.Transform.position - fire.transform.position;
                    near = offset.x * offset.x + offset.z * offset.z <= settings.FuelBarRange * settings.FuelBarRange;
                }
                bool canAct = fuel.Lit || fuel.Fuel > 0f || CanFeed(fire);
                show = ShouldShow(true, fuel.Lit, fuel.Share, settings.WarnBelow, darkness, near, canAct);
                if (show && _bar != null)
                {
                    _bar.SetValue(fuel.Fuel, fuel.Max);
                    _bar.SetWarning(!fuel.Lit || fuel.Share < settings.ShrinkBelow);
                }
            }
            if (_group != null)
                _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
