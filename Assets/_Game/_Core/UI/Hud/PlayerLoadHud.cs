using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.UI
{
    // The carried-weight gauge for the active player (IPlayer.Load): fill = carried / Max, notch at Comfortable, "kg / max kg"
    // rebuilt only when a shown number or the language changes. Hidden without a player.
    public sealed class PlayerLoadHud : PlayerHudView
    {
        static readonly LocalizedString WeightFormat = Loc.Hud("hud.weight");

        [SerializeField] BagGauge _gauge;

        int _shownTenths = -1;
        int _shownMax = -1;
        Locale _shownLocale;

        protected override void Bind(IPlayer player)
        {
            _shownTenths = _shownMax = -1;
            if (_gauge != null)
                _gauge.gameObject.SetActive(player.Equipment != null);
        }

        protected override void Unbind(IPlayer player)
        {
            if (_gauge != null)
                _gauge.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            IPlayer player = Player;
            if (player == null || _gauge == null || player.Equipment == null)
                return;
            CarryLoad load = player.Load;
            float max = Mathf.Max(0.01f, load.Max);
            _gauge.SetLoad(load.Weight / max, load.Comfortable / max, load.State);
            int tenths = Mathf.RoundToInt(load.Weight * 10f);
            int maxShown = Mathf.RoundToInt(load.Max);
            if ((tenths == _shownTenths && maxShown == _shownMax && _shownLocale == Loc.Current) || _gauge.Label == null)
                return;
            _shownTenths = tenths;
            _shownMax = maxShown;
            _shownLocale = Loc.Current;
            _gauge.Label.SetText(WeightFormat.Text(), tenths / 10f, maxShown);
        }
    }
}
