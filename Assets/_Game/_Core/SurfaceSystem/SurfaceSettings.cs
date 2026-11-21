using System;
using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.Effects;
using UnityEngine;

namespace MB13.Aipocalypse.Core.SurfaceSystem
{
    // What something is made of, for footsteps and impacts. Append only: the settings store looks by number.
    public enum SurfaceKind
    {
        Grass,
        Dirt,
        Stone,
        Wood,
        Sand,
        Mud,
        Metal,
        Snow,
    }

    // A word that marks a surface kind when it appears in a collider's, its material's or a terrain layer's name.
    [Serializable]
    public struct SurfaceRule
    {
        [Tooltip("Matched anywhere in the name, ignoring case")] public string Keyword;
        public SurfaceKind Kind;
    }

    [Serializable]
    public sealed class SurfaceLook
    {
        [Tooltip("One per footstep on it")] public SoundID Footstep;
        [Tooltip("A bullet landing on it (3D, at the hit)")] public SoundID BulletImpact;
        [Tooltip("Shown where a bullet lands on it; empty = the gun's own impact")] public PooledEffect ImpactEffect;
    }

    // Which surface is which, and what each sounds and looks like when stepped on or shot. Asset: Data/Surfaces/.
    [CreateAssetMenu(fileName = "SurfaceSettings", menuName = "Aipocalypse/Surfaces/Surface Settings")]
    public sealed class SurfaceSettings : ScriptableObject
    {
        [Tooltip("Off: every surface is the default one, with no surface sounds or effects")] public bool Enabled = true;
        [Tooltip("Anything no rule names")] public SurfaceKind Default = SurfaceKind.Dirt;
        [Tooltip("Terrain whose layer no rule names")] public SurfaceKind DefaultTerrain = SurfaceKind.Grass;
        [Tooltip("Checked in order; the first keyword found wins")] public List<SurfaceRule> Rules = new();
        public Dictionary<SurfaceKind, SurfaceLook> Looks = new();
        [Tooltip("A bullet landing on something that bleeds (3D, at the hit)")] public SoundID FleshImpact;
        [Min(1), Tooltip("Most bullet impact sounds started in one frame (a shotgun blast lands many at once)")]
        public int MaxImpactSoundsPerFrame = 2;
        [Min(0), Tooltip("Copies of each surface's impact effect made at startup")] public int ImpactPrewarm = 4;

        public SurfaceLook Look(SurfaceKind kind) => Looks != null && Looks.TryGetValue(kind, out SurfaceLook look) ? look : null;

        public bool TryMatch(string text, out SurfaceKind kind)
        {
            if (!string.IsNullOrEmpty(text))
                foreach (SurfaceRule rule in Rules)
                    if (!string.IsNullOrEmpty(rule.Keyword) && text.IndexOf(rule.Keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        kind = rule.Kind;
                        return true;
                    }
            kind = Default;
            return false;
        }
    }
}
