using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // The inventory screen's centre: the selected item large and in 3D (ItemPreviewStage), its name, rarity, weight and
    // value, a gun's part slots in Duckov's order with a leader line from the hovered one to its mount on the model, and
    // its stats in two columns. While a part is dragged, the slot it fits lights up and the stats preview the change in
    // green and red. InventoryScreen owns the drag and drop; this shows.
    public sealed class ItemInspectorView : MonoBehaviour
    {
        static readonly LocalizedString InfoFormat = Loc.UI("inspector.info");
        static readonly LocalizedString HintText = Loc.UI("inspector.hint");
        static readonly LocalizedString TurnText = Loc.UI("inspector.turn");
        static readonly LocalizedString WeightName = Loc.UI("stat.weight");
        static readonly LocalizedString ZoomName = Loc.UI("stat.zoom");
        // Indexed by GunSlot.
        static readonly LocalizedString[] SlotNames =
        {
            Loc.Items("slot.optic"), Loc.Items("slot.muzzle"), Loc.Items("slot.grip"), Loc.Items("slot.stock"), Loc.Items("slot.tactical"),
            Loc.Items("slot.magazine"),
        };
        // Indexed by GunStat.
        public static readonly LocalizedString[] StatNames =
        {
            Loc.UI("stat.damage"), Loc.UI("stat.fire_rate"), Loc.UI("stat.magazine"), Loc.UI("stat.reload_time"), Loc.UI("stat.range"),
            Loc.UI("stat.bullet_speed"), Loc.UI("stat.vertical_recoil"), Loc.UI("stat.horizontal_recoil"), Loc.UI("stat.aim_time"),
            Loc.UI("stat.spread"), Loc.UI("stat.move_speed"), Loc.UI("stat.hip_spread"),
        };

        [SerializeField] InventoryStyle _style;
        [SerializeField, Tooltip("Everything but the hint: hidden while nothing is selected")] GameObject _content;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _info;
        [SerializeField, Tooltip("Shown while nothing is selected")] TMP_Text _hint;
        [SerializeField] TMP_Text _turnHint;
        [SerializeField] ItemPreviewStage _stage;
        [SerializeField] ItemPreviewInput _stageInput;
        [SerializeField, Tooltip("One per GunSlot, in its order")] ItemSlotView[] _slots = Array.Empty<ItemSlotView>();
        [SerializeField, Tooltip("Stat rows, filled top to bottom, left column first")] TMP_Text[] _statNames = Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text[] _statValues = Array.Empty<TMP_Text>();
        [SerializeField, Tooltip("Pivot on its left end: stretched from a part slot to its mount on the model")] Image _leader;

        ItemInstance _item;
        ItemInstance _preview;
        bool _shown;
        int _rows;

        public ItemInstance Item => _item;
        public GunInstance Gun => _item as GunInstance;
        public IReadOnlyList<ItemSlotView> SlotViews => _slots;
        // An item dragged from a slot was dropped on the 3D view.
        public event Action StageDropped;

        void Awake()
        {
            if (_stageInput != null)
            {
                _stageInput.Turned += delta => _stage.Turn(delta);
                _stageInput.Zoomed += notches => _stage.Zoom(notches);
                _stageInput.Dropped += () => StageDropped?.Invoke();
            }
            if (_leader != null)
                _leader.enabled = false;
        }

        // Selects `item` (null: nothing, with the hint).
        public void Show(ItemInstance item)
        {
            _shown = true;
            _preview = null;
            if (item != _item || (_stage != null && _stage.Model == null))
            {
                _item = item;
                if (_stage != null)
                    _stage.Show(item);
            }
            Refresh();
        }

        // The screen closed: the 3D view stops rendering.
        // A part went on (or came off) the shown gun.
        public void Nudge(bool attached)
        {
            if (_stage != null)
                _stage.Nudge(attached);
        }

        public void Hide()
        {
            _shown = false;
            _item = null;
            _preview = null;
            if (_stage != null)
                _stage.Hide();
            if (_leader != null)
                _leader.enabled = false;
        }

        // A part being dragged (null when the drag ends): its slot lights up and the stats preview it.
        public void SetPreview(ItemInstance part)
        {
            if (part == _preview)
                return;
            _preview = part;
            Refresh();
        }

        public static string SlotName(GunSlot slot) => SlotNames[(int)slot].Text();

        // The selected item changed (a part fitted, rounds unloaded) or the drag preview did.
        public void Refresh()
        {
            bool any = _item != null;
            if (_content != null && _content.activeSelf != any)
                _content.SetActive(any);
            if (_hint != null)
            {
                _hint.enabled = !any;
                if (!any)
                    _hint.SetText(HintText.Text());
            }
            if (!any)
                return;
            if (_stage != null)
                _stage.Refresh(_item);

            ItemDefinition definition = _item.Definition;
            _title.SetText(definition.DisplayName);
            _title.color = _style != null ? _style.RarityTextColor(definition.Rarity) : Color.white;
            if (_title.color.a <= 0f)
                _title.color = _style != null ? _style.TextColor : Color.white;
            _info.SetText(InfoFormat.Format(InventoryScreen.RarityName(definition.Rarity), _item.Weight, _item.Value));
            if (_turnHint != null)
                _turnHint.SetText(TurnText.Text());

            GunInstance gun = Gun;
            for (int i = 0; i < _slots.Length; i++)
            {
                bool has = gun != null && gun.Gun.HasSlot((GunSlot)i);
                if (_slots[i].gameObject.activeSelf != has)
                    _slots[i].gameObject.SetActive(has);
                if (!has)
                    continue;
                _slots[i].SetCaption(SlotName((GunSlot)i));
                _slots[i].SetMarked(_preview != null && gun.Fits(_preview) && SlotOf(_preview) == (GunSlot)i);
            }
            RefreshStats();
        }

        static GunSlot SlotOf(ItemInstance part) => ((AttachmentDefinition)part.Definition).Slot;

        void RefreshStats()
        {
            _rows = 0;
            switch (_item)
            {
                case GunInstance gun:
                    GunStats now = gun.Stats;
                    GunStats then = _preview != null && gun.Fits(_preview) ? gun.StatsWith(SlotOf(_preview), _preview) : null;
                    for (int i = 0; i < GunStats.StatCount; i++)
                    {
                        var stat = (GunStat)i;
                        if (stat == GunStat.Spread && gun.Gun.Fire.SpreadAngle <= 0f)
                            continue;
                        float value = now.Get(stat);
                        Row(StatNames[i].Text(), Format(stat, value, gun.Gun) + Delta(stat, value, then?.Get(stat)));
                    }
                    Row(WeightName.Text(), $"{now.Weight:0.##} kg" + Delta(now.Weight, then?.Weight, higherIsBetter: false, "0.##"));
                    float zoom = 1f / now.AimZoom;
                    float? zoomThen = then != null ? 1f / then.AimZoom : null;
                    if (zoom > 1.5f || zoomThen > 1.5f)
                        Row(ZoomName.Text(), $"{zoom:0.#}x" + Delta(zoom, zoomThen, higherIsBetter: true, "0.#"));
                    break;
                case { Definition: AttachmentDefinition part }:
                    foreach (GunStatModifier modifier in part.Modifiers)
                    {
                        bool better = GunStats.HigherIsBetter(modifier.Stat) == modifier.Percent > 0f;
                        Row(StatNames[(int)modifier.Stat].Text(), Colored($"{modifier.Percent:+0;-0}%", better));
                    }
                    if (part.Optic.Magnification > 1f)
                        Row(ZoomName.Text(), $"{part.Optic.Magnification:0.#}x");
                    Row(WeightName.Text(), $"{_item.Weight:0.##} kg");
                    break;
                default:
                    Row(WeightName.Text(), $"{_item.Weight:0.##} kg");
                    break;
            }
            for (int i = _rows; i < _statNames.Length; i++)
            {
                _statNames[i].enabled = false;
                _statValues[i].enabled = false;
            }
        }

        void Row(string label, string value)
        {
            if (_rows >= _statNames.Length)
                return;
            _statNames[_rows].enabled = true;
            _statValues[_rows].enabled = true;
            _statNames[_rows].SetText(label);
            _statValues[_rows].SetText(value);
            _rows++;
        }

        static string Format(GunStat stat, float value, GunDefinition gun) => stat switch
        {
            GunStat.Damage => gun.Fire.Pellets > 1 ? $"{value:0.#} x {gun.Fire.Pellets}" : $"{value:0.#}",
            GunStat.FireRate => $"{value:0}/min",
            GunStat.MagazineSize => $"{value:0}",
            GunStat.ReloadTime or GunStat.AimTime => $"{value:0.00} s",
            GunStat.Range => $"{value:0} m",
            GunStat.BulletSpeed => $"{value:0} m/s",
            GunStat.MoveSpeed => $"{value:0}%",
            _ => $"{value:0.0}",
        };

        static string Precision(GunStat stat) => stat switch
        {
            GunStat.ReloadTime or GunStat.AimTime => "0.00",
            GunStat.VerticalRecoil or GunStat.HorizontalRecoil or GunStat.Spread or GunStat.HipSpread or GunStat.Damage => "0.#",
            _ => "0",
        };

        string Delta(GunStat stat, float now, float? then) => Delta(now, then, GunStats.HigherIsBetter(stat), Precision(stat));

        // "  +10" in the better or worse colour; nothing when there's no preview or no change.
        string Delta(float now, float? then, bool higherIsBetter, string precision)
        {
            if (then == null)
                return string.Empty;
            float delta = then.Value - now;
            string text = delta.ToString("+" + precision + ";-" + precision);
            if (Mathf.Abs(delta) < 0.0001f || text == "+0" || text == "-0" || text == "+0.00" || text == "-0.00")
                return string.Empty;
            return "  " + Colored(text, higherIsBetter == delta > 0f);
        }

        string Colored(string text, bool better)
        {
            Color color = _style == null ? Color.white : better ? _style.BetterColor : _style.WorseColor;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        }

        // The leader line: from the hovered part slot (or the slot a dragged part fits) to its mount on the 3D model.
        void LateUpdate()
        {
            if (!_shown || _leader == null)
                return;
            int slot = -1;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].gameObject.activeSelf && _slots[i].IsHovered)
                    slot = i;
            if (slot < 0 && _preview != null && Gun != null && Gun.Fits(_preview))
                slot = (int)SlotOf(_preview);
            Transform mount = slot >= 0 && _stage != null && _stage.Model != null && _stage.Model.GetComponentInChildren<GunMounts>() is { } mounts
                ? mounts.MountOf((GunSlot)slot)
                : null;
            if (mount == null || !_stage.TryProject(mount.position, out Vector2 onImage))
            {
                if (_leader.enabled)
                    _leader.enabled = false;
                return;
            }

            var space = (RectTransform)_leader.transform.parent;
            var from = (RectTransform)_slots[slot].transform;
            Vector2 start = space.InverseTransformPoint(from.TransformPoint(new Vector3(from.rect.center.x, from.rect.yMax, 0f)));
            Vector2 end = space.InverseTransformPoint(_stage.Image.TransformPoint(onImage));
            Vector2 line = end - start;
            RectTransform leader = _leader.rectTransform;
            leader.localPosition = start;
            leader.sizeDelta = new Vector2(line.magnitude, leader.sizeDelta.y);
            leader.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg);
            _leader.color = _style != null ? _style.LeaderColor : Color.white;
            if (!_leader.enabled)
                _leader.enabled = true;
        }
    }
}
