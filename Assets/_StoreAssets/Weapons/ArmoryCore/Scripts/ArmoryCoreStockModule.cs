using UnityEngine;

namespace ArmoryCore
{
    /// <summary>Stock module with optional folded/unfolded handling.</summary>
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreStockModule : ArmoryCoreAttachmentModule
    {
        [Header("Stock")]
        [SerializeField] private bool foldable;
        [SerializeField] private bool folded;
        [SerializeField, Min(0.01f)] private float recoilMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float aimTimeMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float foldedRecoilMultiplier = 1.45f;
        [SerializeField, Min(0.01f)] private float foldedAimTimeMultiplier = 0.82f;

        public override float VerticalRecoilMultiplier => recoilMultiplier
            * (folded ? foldedRecoilMultiplier : 1f);
        public override float HorizontalRecoilMultiplier => recoilMultiplier
            * (folded ? foldedRecoilMultiplier : 1f);
        public override float AimTimeMultiplier => aimTimeMultiplier
            * (folded ? foldedAimTimeMultiplier : 1f);

        public bool IsFolded => folded;

        public bool SetFolded(bool value)
        {
            if (!foldable)
                return false;
            folded = value;
            RequestStatsRefresh();
            return true;
        }

        public bool ToggleFolded()
        {
            return SetFolded(!folded);
        }

        private void OnValidate()
        {
            recoilMultiplier = Mathf.Max(0.01f, recoilMultiplier);
            aimTimeMultiplier = Mathf.Max(0.01f, aimTimeMultiplier);
            foldedRecoilMultiplier = Mathf.Max(0.01f, foldedRecoilMultiplier);
            foldedAimTimeMultiplier = Mathf.Max(0.01f, foldedAimTimeMultiplier);
            if (!foldable)
                folded = false;
        }
    }
}
