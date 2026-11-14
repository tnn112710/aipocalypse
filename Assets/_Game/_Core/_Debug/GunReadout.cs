using System.Text;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WeaponSystem;
using TMPro;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Debugging
{
    // Weapons Test readout (a dev tool, not localized, like the console): the held gun's stat sheet with each part's
    // share, damage per second and shots / time to kill a target at the range's distances, to tune guns and parts by eye.
    // Lives under the test scene's HUD, so the HUD's scope injects it.
    public sealed class GunReadout : PlayerHudView
    {
        static readonly string[] Units = { "", " rpm", "", " s", " m", " m/s", "°", "°", " s", "°", "%", "°" };

        [SerializeField] TMP_Text _text;
        [SerializeField, Min(1f), Tooltip("Health of the target the kill numbers are for (a goblin: 100)")] float _targetHealth = 100f;
        [SerializeField, Min(1f), Tooltip("Head hit multiplier (enemy Hitbox: 2)")] float _headMultiplier = 2f;
        [SerializeField, Tooltip("Metres the kill numbers are worked out at")] float[] _distances = { 10f, 25f, 50f, 100f };
        [SerializeField, Min(0.05f), Tooltip("Seconds between refreshes (unscaled)")] float _interval = 0.25f;

        readonly StringBuilder _sb = new();
        float _next;

        void LateUpdate()
        {
            if (_text == null || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + _interval;
            HeldItem held = Player?.HeldItem;
            if (held == null || held.Item is not GunInstance gun)
            {
                _text.SetText("<b>Gun readout</b>\nHold a gun.");
                return;
            }
            Write(gun, held);
            _text.SetText(_sb);
        }

        void Write(GunInstance gun, HeldItem held)
        {
            GunStats stats = gun.Stats;
            GunDefinition definition = gun.Gun;
            _sb.Clear();
            _sb.Append("<b>").Append(definition.name).Append("</b>  ").Append(stats.Weight.ToString("0.00")).Append(" kg  ·  heard at ")
                .Append(Mathf.RoundToInt(stats.Loudness * 100f)).Append(" %\n");
            AppendParts(gun);

            _sb.Append("\n<b>Stat</b><pos=34%>base<pos=52%>final<pos=70%>parts\n");
            for (int i = 0; i < GunStats.StatCount; i++)
            {
                var stat = (GunStat)i;
                float baseValue = stats.GetBase(stat);
                if (Mathf.Approximately(baseValue, 0f))
                    continue;
                float value = stats.Get(stat);
                bool changed = !Mathf.Approximately(stats.Percent(stat), 0f);
                _sb.Append(stat).Append("<pos=34%>").Append(Format(baseValue)).Append(Units[i]).Append("<pos=52%>");
                if (changed)
                    _sb.Append(GunStats.HigherIsBetter(stat) == value > baseValue ? "<color=#7CFC8A>" : "<color=#FF7A6E>");
                _sb.Append(Format(value)).Append(Units[i]);
                if (changed)
                    _sb.Append("</color>");
                _sb.Append("<pos=70%><size=80%>");
                AppendShares(gun, stat);
                _sb.Append("</size>\n");
            }

            float perShot = stats.Damage * definition.Fire.Pellets;
            float interval = Mathf.Max(60f / Mathf.Max(1f, stats.RoundsPerMinute), definition.Fire.CycleTime);
            int magazine = stats.MagazineSize;
            float reload = definition.Magazine.Reload == ReloadType.PerRound ? stats.ReloadTime * magazine : stats.ReloadTime;
            _sb.Append("\n<b>DPS</b> ").Append(Mathf.RoundToInt(perShot / interval)).Append(" firing, ")
                .Append(Mathf.RoundToInt(perShot * magazine / (magazine * interval + reload))).Append(" with reloads");
            if (definition.Fire.Pellets > 1)
                _sb.Append(" (every pellet hits)");

            _sb.Append("\n<b>To kill ").Append(_targetHealth.ToString("0")).Append(" HP</b>  body / head, time\n");
            foreach (float distance in _distances)
            {
                _sb.Append(distance.ToString("0")).Append(" m<pos=18%>");
                if (distance > stats.Range)
                {
                    _sb.Append("out of range\n");
                    continue;
                }
                float hit = perShot * stats.Falloff.At(distance);
                int body = Mathf.CeilToInt(_targetHealth / hit);
                int head = Mathf.CeilToInt(_targetHealth / (hit * _headMultiplier));
                _sb.Append(body).Append(" / ").Append(head).Append("<pos=40%>").Append(((body - 1) * interval).ToString("0.00")).Append(" s / ")
                    .Append(((head - 1) * interval).ToString("0.00")).Append(" s\n");
            }

            _sb.Append("\n<b>Cone</b> ").Append(held.Spread.ToString("0.00")).Append("° now  (hip ").Append(stats.HipSpread.ToString("0.00"))
                .Append("°, aimed ").Append(stats.AimSpread.ToString("0.00")).Append("°)");
        }

        void AppendParts(GunInstance gun)
        {
            bool any = false;
            for (int i = 0; i < GunSlots.Count; i++)
            {
                ItemInstance part = gun.AttachmentIn((GunSlot)i);
                if (part == null)
                    continue;
                _sb.Append(any ? ", " : "<size=85%>").Append(part.Definition.name);
                any = true;
            }
            _sb.Append(any ? "</size>\n" : "<size=85%>no parts</size>\n");
        }

        // Each fitted part's percentage on the stat ("+25 Scope4x").
        void AppendShares(GunInstance gun, GunStat stat)
        {
            for (int i = 0; i < GunSlots.Count; i++)
            {
                if (gun.AttachmentIn((GunSlot)i)?.Definition is not AttachmentDefinition part)
                    continue;
                foreach (GunStatModifier modifier in part.Modifiers)
                    if (modifier.Stat == stat)
                        _sb.Append(modifier.Percent > 0f ? "+" : "").Append(modifier.Percent.ToString("0")).Append(' ').Append(part.name).Append("  ");
            }
        }

        static string Format(float value) => value >= 100f ? value.ToString("0") : value >= 10f ? value.ToString("0.#") : value.ToString("0.##");
    }
}
