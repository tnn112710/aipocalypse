using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Effects;
using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // A gun model's part mounts: shows the parts fitted to a gun copy on the model (the first-person Weapon, the World model
    // lying on the ground, the inventory's 3D view). Each mount sits under the gun part it moves with, so the gun's clips
    // carry the fitted parts with them (a grip on the pump, an optic on a pistol's slide).
    [DisallowMultipleComponent]
    public sealed class GunMounts : MonoBehaviour, IItemView
    {
        [Serializable]
        public sealed class Mount
        {
            public GunSlot Slot;
            [Tooltip("The part's model spawns here (its pivot on this point)")] public Transform Point;
            [Tooltip("Shown while a part is fitted: rails and adapters")] public GameObject[] ShowWhenFitted = Array.Empty<GameObject>();
            [Tooltip("Hidden while a part is fitted: iron sights an optic replaces")] public GameObject[] HideWhenFitted = Array.Empty<GameObject>();
        }

        [SerializeField] List<Mount> _mounts = new();
        [SerializeField, Tooltip("First-person model: a point on the iron sights' line in the aim pose, blue axis along it. Optics " +
            "shift the aim pose so their own sight line lands here")] Transform _ironSight;
        [SerializeField, Tooltip("Scaled by magazine parts: the magazine and its reload copy")] Transform[] _magazines = Array.Empty<Transform>();
        [SerializeField, Tooltip("First-person model with a built-in tube scope: its sight window (see AttachmentModel). A fitted " +
            "optic's window replaces it")] Renderer _sightWindow;

        readonly AttachmentModel[] _models = new AttachmentModel[GunSlots.Count];
        readonly AttachmentDefinition[] _shown = new AttachmentDefinition[GunSlots.Count];
        Vector3[] _magazineScales;

        public IReadOnlyList<Mount> Mounts => _mounts;
        public Transform IronSight => _ironSight;
        public AttachmentDefinition Optic => _shown[(int)GunSlot.Optic];

        // The fitted optic's sight point, else the iron sights'.
        public Transform Sight
        {
            get
            {
                AttachmentModel optic = _models[(int)GunSlot.Optic];
                return optic != null && optic.Sight != null ? optic.Sight : _ironSight;
            }
        }

        // The fitted optic's sight window (null if it has none), else the built-in scope's.
        public Renderer SightWindow
        {
            get
            {
                AttachmentModel optic = _models[(int)GunSlot.Optic];
                return optic != null ? optic.SightWindow : _sightWindow;
            }
        }

        // Where shots leave a fitted muzzle part; null without one (the gun's own muzzle).
        public Transform MuzzleTip
        {
            get
            {
                AttachmentModel muzzle = _models[(int)GunSlot.Muzzle];
                return muzzle != null ? muzzle.Tip : null;
            }
        }

        // Where the support hand would hold a fitted grip (for the gun animation rework); null without one.
        public Transform SupportGrip
        {
            get
            {
                AttachmentModel grip = _models[(int)GunSlot.Grip];
                return grip != null ? grip.HandGrip : null;
            }
        }

        public AttachmentModel ModelIn(GunSlot slot) => _models[(int)slot];

        // A fitted part carries a light or a laser (switched with the gun light key).
        public bool HasLights
        {
            get
            {
                foreach (AttachmentModel model in _models)
                    if (model != null && model.HasLights)
                        return true;
                return false;
            }
        }

        public void SetLights(bool on)
        {
            foreach (AttachmentModel model in _models)
                if (model != null)
                    model.SetLights(on);
        }

        // The fitted laser (a tactical part with a beam), or null.
        public AttachmentModel Laser
        {
            get
            {
                AttachmentModel model = _models[(int)GunSlot.Tactical];
                return model != null && model.HasLaser ? model : null;
            }
        }

        // The first fitted part with a light or laser: its switch sound is the gun light key's click.
        public AttachmentDefinition SwitchedPart
        {
            get
            {
                for (int i = 0; i < _models.Length; i++)
                    if (_models[i] != null && _models[i].HasLights)
                        return _shown[i];
                return null;
            }
        }

        public Transform MountOf(GunSlot slot)
        {
            Mount mount = Find(slot);
            return mount != null ? mount.Point : null;
        }

        // World and inventory views: a gun lying or shown, not held.
        public void ShowItem(ItemInstance item) => Show(item as GunInstance, held: false);

        // Shows `gun`'s parts (none for null). Only slots whose part changed are rebuilt. `held`: no shadows, lights as the
        // gun has them switched.
        public void Show(GunInstance gun, bool held)
        {
            for (int i = 0; i < GunSlots.Count; i++)
            {
                var slot = (GunSlot)i;
                var wanted = gun?.AttachmentIn(slot)?.Definition as AttachmentDefinition;
                if (wanted == _shown[i])
                    continue;
                Clear(slot);
                Fit(slot, wanted, held);
            }
            ScaleMagazines(_shown[(int)GunSlot.Magazine]);
            SetLights(held && gun != null && gun.LightOn);
        }

        void Fit(GunSlot slot, AttachmentDefinition part, bool held)
        {
            Mount mount = Find(slot);
            if (part == null || mount == null || mount.Point == null)
                return;
            _shown[(int)slot] = part;
            SetActive(mount.ShowWhenFitted, true);
            SetActive(mount.HideWhenFitted, false);
            if (part.Model == null)
                return;

            AttachmentModel model = Instantiate(part.Model, mount.Point, false);
            model.name = part.Model.name;
            model.transform.SetLayerRecursively(mount.Point.gameObject.layer);
            if (held)
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            _models[(int)slot] = model;
        }

        void Clear(GunSlot slot)
        {
            int index = (int)slot;
            Mount mount = Find(slot);
            if (_shown[index] != null && mount != null)
            {
                SetActive(mount.ShowWhenFitted, false);
                SetActive(mount.HideWhenFitted, true);
            }
            _shown[index] = null;
            AttachmentModel model = _models[index];
            _models[index] = null;
            if (model == null)
                return;
            // A pooled muzzle flash may still ride on the part: it goes back to its pool, not with the part.
            foreach (PooledEffect effect in model.GetComponentsInChildren<PooledEffect>(true))
                effect.transform.SetParent(null, true);
            model.transform.SetParent(null, false);
            model.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(model.gameObject);
            else
                DestroyImmediate(model.gameObject);
        }

        void ScaleMagazines(AttachmentDefinition magazine)
        {
            if (_magazines.Length == 0)
                return;
            if (_magazineScales == null)
            {
                _magazineScales = new Vector3[_magazines.Length];
                for (int i = 0; i < _magazines.Length; i++)
                    _magazineScales[i] = _magazines[i] != null ? _magazines[i].localScale : Vector3.one;
            }
            Vector3 scale = magazine != null ? magazine.MagazineScale : Vector3.one;
            for (int i = 0; i < _magazines.Length; i++)
                if (_magazines[i] != null)
                    _magazines[i].localScale = Vector3.Scale(_magazineScales[i], scale);
        }

        Mount Find(GunSlot slot)
        {
            foreach (Mount mount in _mounts)
                if (mount.Slot == slot)
                    return mount;
            return null;
        }

        static void SetActive(GameObject[] objects, bool active)
        {
            foreach (GameObject node in objects)
                if (node != null && node.activeSelf != active)
                    node.SetActive(active);
        }
    }
}
