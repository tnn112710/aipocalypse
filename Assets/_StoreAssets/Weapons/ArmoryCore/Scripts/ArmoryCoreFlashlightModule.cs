using UnityEngine;

namespace ArmoryCore
{
    /// <summary>Utility module that controls one or more child lights.</summary>
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreFlashlightModule : ArmoryCoreAttachmentModule
    {
        [Header("Flashlight")]
        [SerializeField] private Light[] lights;
        [SerializeField] private bool startEnabled;

        public bool IsLightEnabled { get; private set; }

        public void ConfigureLights(Light[] values)
        {
            lights = values;
            SetLightEnabled(startEnabled);
        }

        public void SetLightEnabled(bool value)
        {
            CacheLights();
            IsLightEnabled = value;
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    lights[i].enabled = value;
            }
        }

        public bool ToggleLight()
        {
            SetLightEnabled(!IsLightEnabled);
            return IsLightEnabled;
        }

        public override void OnInstalled(ArmoryCoreWeaponController weapon)
        {
            base.OnInstalled(weapon);
            SetLightEnabled(startEnabled);
        }

        public override void OnRemoved(ArmoryCoreWeaponController weapon)
        {
            SetLightEnabled(false);
            base.OnRemoved(weapon);
        }

        private void Awake()
        {
            CacheLights();
            SetLightEnabled(startEnabled);
        }

        private void CacheLights()
        {
            if (lights == null || lights.Length == 0)
                lights = GetComponentsInChildren<Light>(true);
        }
    }
}
