using UnityEngine;

namespace ArmoryCore
{
    /// <summary>Foregrip/bipod module. A bipod can be deployed at runtime.</summary>
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreGripModule : ArmoryCoreAttachmentModule
    {
        [Header("Grip")]
        [SerializeField, Min(0.01f)] private float verticalRecoilMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float horizontalRecoilMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float spreadMultiplier = 1f;
        [SerializeField] private bool supportsDeployment;
        [SerializeField] private bool deployed;
        [SerializeField, Range(0.05f, 1f)] private float deployedRecoilMultiplier = 0.55f;
        [SerializeField, Range(0.05f, 1f)] private float deployedSpreadMultiplier = 0.55f;

        public override float VerticalRecoilMultiplier => verticalRecoilMultiplier
            * (deployed ? deployedRecoilMultiplier : 1f);
        public override float HorizontalRecoilMultiplier => horizontalRecoilMultiplier
            * (deployed ? deployedRecoilMultiplier : 1f);
        public override float SpreadMultiplier => spreadMultiplier
            * (deployed ? deployedSpreadMultiplier : 1f);

        public bool SupportsDeployment => supportsDeployment;
        public bool IsDeployed => deployed;

        public void ConfigureDeployment(bool supported)
        {
            supportsDeployment = supported;
            if (!supported)
                deployed = false;
        }

        public bool SetDeployed(bool value)
        {
            if (!supportsDeployment)
                return false;
            deployed = value;
            RequestStatsRefresh();
            return true;
        }

        public bool ToggleDeployed()
        {
            return SetDeployed(!deployed);
        }

        private void OnValidate()
        {
            verticalRecoilMultiplier = Mathf.Max(0.01f, verticalRecoilMultiplier);
            horizontalRecoilMultiplier = Mathf.Max(0.01f, horizontalRecoilMultiplier);
            spreadMultiplier = Mathf.Max(0.01f, spreadMultiplier);
            if (!supportsDeployment)
                deployed = false;
        }
    }
}
