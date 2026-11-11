using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // Everything the minimap looks like, plus its range. Size and placement stay in the HUD prefab (SafeFrame/TopRight/Minimap).
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Minimap Style", fileName = "MinimapStyle")]
    public sealed class MinimapStyle : ScriptableObject
    {
        [Tooltip("Show the minimap at all")] public bool Enabled = true;
        [Min(1f), Tooltip("Metres from the player to the rim")] public float Range = 60f;
        [Min(0f), Tooltip("Pixels between the rim and an icon centre, so icons on the edge stay inside the ring")]
        public float EdgeInset = 12f;

        [BoxGroup("Disc")] public Sprite BackgroundSprite;
        [BoxGroup("Disc")] public Color BackgroundColor = new(0f, 0f, 0f, 0.5f);
        [BoxGroup("Disc")] public Sprite RingSprite;
        [BoxGroup("Disc")] public Color RingColor = new(0.86f, 0.86f, 0.86f, 0.9f);

        [BoxGroup("Player")] public Sprite PlayerSprite;
        [BoxGroup("Player")] public Color PlayerColor = Color.white;
        [BoxGroup("Player")] public Sprite ViewConeSprite;
        [BoxGroup("Player")] public Color ViewConeColor = new(1f, 1f, 1f, 0.3f);

        [BoxGroup("Enemies")] public Sprite EnemySprite;
        [BoxGroup("Enemies")] public Color EnemyColor = new(0.96f, 0.27f, 0.34f, 1f);
        [BoxGroup("Enemies"), Min(1f)] public float EnemySize = 16f;

        [BoxGroup("Directions")] public TMP_FontAsset LetterFont;
        [BoxGroup("Directions")] public Material LetterMaterial;
        [BoxGroup("Directions"), Min(1f)] public float LetterSize = 28f;
        [BoxGroup("Directions")] public Color LetterColor = new(0.97f, 0.97f, 0.95f, 0.9f);
        [BoxGroup("Directions")] public Color NorthColor = new(0.9f, 0.84f, 0.29f, 1f);
        [BoxGroup("Directions"), Tooltip("Pixels from the rim to the letter centres; negative = inside the disc")]
        public float LetterOffset;

#if UNITY_EDITOR
        // Edit-time preview only: minimaps re-apply their style in Awake.
        void OnValidate() => StyleUtils.RefreshViews<MinimapView>(this, view => view.Style, view => view.ApplyStyle());
#endif
    }
}
