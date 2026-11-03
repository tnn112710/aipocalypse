using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // How one kind of enemy's glowing eyes look and where they sit on its head (EnemyEyes → Place Eyes). One per kind, since
    // heads differ between packs; the colour and dot size live in the material.
    [CreateAssetMenu(fileName = "EyeGlowStyle", menuName = "Aipocalypse/Enemies/Eye Glow Style")]
    public sealed class EyeGlowStyle : ScriptableObject
    {
        [Tooltip("An Aipocalypse/Enemies/EnemyEyeGlow material: colour, dot size, minimum size far away")] public Material Material;
        [Min(0f), Tooltip("Metres between the two eyes")] public float Spacing = 0.07f;
        [Tooltip("Metres from the head bone to the point between the eyes, along the body (x right, y up, z forward). " +
            "Used when the rig has no humanoid eye bones")]
        public Vector3 Offset = new(0f, 0.08f, 0.1f);
        [Min(0f), Tooltip("Metres in front of humanoid eye bones, when the rig has them")] public float EyeBoneForward = 0.02f;
    }
}
