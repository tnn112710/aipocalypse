using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreShowcaseWeaponCard : MonoBehaviour
    {
        [SerializeField] private ArmoryCoreWeaponController weapon;
        [SerializeField] private Renderer panelRenderer;
        [SerializeField] private Renderer accentRenderer;

        private MaterialPropertyBlock propertyBlock;
        private Color panelColor = Color.white;
        private Color accentColor = Color.white;
        private bool colorsCaptured;

        public ArmoryCoreWeaponController Weapon => weapon;
        public string DisplayName => weapon?.Definition == null
            ? name
            : weapon.Definition.displayName;

        public void Configure(
            ArmoryCoreWeaponController value,
            Renderer panel,
            Renderer accent)
        {
            weapon = value;
            panelRenderer = panel;
            accentRenderer = accent;
            CaptureColors();
        }

        public void SetHighlighted(bool highlighted)
        {
            CaptureColors();
            SetRendererColor(
                panelRenderer,
                highlighted ? new Color(0.12f, 0.3f, 0.52f, 1f) : panelColor);
            SetRendererColor(
                accentRenderer,
                highlighted ? new Color(0.15f, 0.85f, 1f, 1f) : accentColor);
        }

        private void CaptureColors()
        {
            if (colorsCaptured)
                return;
            panelColor = ReadRendererColor(panelRenderer, panelColor);
            accentColor = ReadRendererColor(accentRenderer, accentColor);
            colorsCaptured = true;
        }

        private static Color ReadRendererColor(Renderer target, Color fallback)
        {
            var material = target == null ? null : target.sharedMaterial;
            if (material == null)
                return fallback;
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            return material.HasProperty("_Color") ? material.GetColor("_Color") : fallback;
        }

        private void SetRendererColor(Renderer target, Color color)
        {
            if (target == null)
                return;
            propertyBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            target.SetPropertyBlock(propertyBlock);
        }
    }
}
