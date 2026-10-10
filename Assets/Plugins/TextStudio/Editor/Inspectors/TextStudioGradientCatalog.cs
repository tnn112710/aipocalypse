using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Editor
{
    internal enum TextStudioGradientUseCase
    {
        Interface,
        Combat,
        Fantasy,
        ScienceFiction,
        Nature,
        Luxury
    }

    internal sealed class TextStudioGradientRecipe
    {
        readonly TextStudio2DGradientStop[] _stops;

        internal string Id { get; }
        internal string Name { get; }
        internal string Description { get; }
        internal TextStudio2DPaintKind Kind { get; }
        internal TextStudioGradientUseCase UseCase { get; }
        internal float Angle { get; }
        internal Vector2 Center { get; }
        internal Vector2 Scale { get; }
        internal Vector2 Offset { get; }
        internal TextStudio2DFourCornerGradient FourCorner { get; }
        internal int StopCount => _stops.Length;
        internal string FamilyLabel => TextStudioGradientCatalog.FamilyLabel(Kind);
        internal string UseCaseLabel => TextStudioGradientCatalog.UseCaseLabel(UseCase);
        internal string SearchText { get; }

        internal TextStudioGradientRecipe(
            string id,
            string name,
            string description,
            TextStudio2DPaintKind kind,
            TextStudioGradientUseCase useCase,
            float angle,
            Vector2 center,
            Vector2 scale,
            Vector2 offset,
            TextStudio2DGradientStop[] stops,
            TextStudio2DFourCornerGradient fourCorner = default)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Kind = kind;
            UseCase = useCase;
            Angle = angle;
            Center = center;
            Scale = scale;
            Offset = offset;
            _stops = stops != null
                ? (TextStudio2DGradientStop[])stops.Clone()
                : Array.Empty<TextStudio2DGradientStop>();
            FourCorner = fourCorner;
            SearchText = Name + " " + Description + " " + FamilyLabel + " " + UseCaseLabel;
        }

        internal TextStudio2DGradientStop GetStop(int index)
        {
            if (index < 0 || index >= _stops.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _stops[index];
        }

        internal TextStudio2DGradientStop[] CopyStops()
        {
            return (TextStudio2DGradientStop[])_stops.Clone();
        }

        internal Color Evaluate(float u, float v)
        {
            Vector2 scale = new Vector2(
                Mathf.Abs(Scale.x) > 0.00001f ? Scale.x : 0.00001f,
                Mathf.Abs(Scale.y) > 0.00001f ? Scale.y : 0.00001f);
            Vector2 delta = new Vector2(
                (u - Center.x - Offset.x) / scale.x,
                (v - Center.y - Offset.y) / scale.y);
            if (Kind == TextStudio2DPaintKind.FourCornerGradient)
            {
                Vector2 cornerCoordinate = new Vector2(
                    Mathf.Clamp01(delta.x + 0.5f),
                    Mathf.Clamp01(delta.y + 0.5f));
                return FourCorner.Evaluate(cornerCoordinate.x, cornerCoordinate.y);
            }

            float coordinate;
            if (Kind == TextStudio2DPaintKind.LinearGradient)
            {
                float radians = Angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                coordinate = Mathf.Clamp01(Vector2.Dot(delta, direction) + 0.5f);
            }
            else if (Kind == TextStudio2DPaintKind.RadialGradient)
            {
                coordinate = Mathf.Clamp01(delta.magnitude * 2f);
            }
            else
            {
                float turn = Mathf.Atan2(delta.y, delta.x) / (Mathf.PI * 2f);
                coordinate = Mathf.Repeat(turn - Angle / 360f + 1f, 1f);
            }
            return EvaluateStops(coordinate);
        }

        internal Color EvaluateStops(float coordinate)
        {
            if (_stops.Length == 0)
                return Color.white;
            coordinate = Mathf.Clamp01(coordinate);
            TextStudio2DGradientStop previous = _stops[0];
            if (_stops.Length == 1 || coordinate <= previous.Position)
                return previous.Color;
            for (int i = 1; i < _stops.Length; i++)
            {
                TextStudio2DGradientStop current = _stops[i];
                if (coordinate <= current.Position)
                {
                    float amount = Mathf.InverseLerp(previous.Position, current.Position, coordinate);
                    return Color.Lerp(previous.Color, current.Color, amount);
                }
                previous = current;
            }
            return previous.Color;
        }

        internal Gradient CreateUnityGradient()
        {
            Gradient gradient = new Gradient
            {
                mode = GradientMode.Blend
            };
            GradientColorKey[] colorKeys = new GradientColorKey[_stops.Length];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[_stops.Length];
            for (int i = 0; i < _stops.Length; i++)
            {
                TextStudio2DGradientStop stop = _stops[i];
                colorKeys[i] = new GradientColorKey(stop.Color, stop.Position);
                alphaKeys[i] = new GradientAlphaKey(stop.Color.a, stop.Position);
            }
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioGradientCatalog
    {
        static readonly TextStudioGradientRecipe[] s_recipes = BuildRecipes();
        static readonly TextStudioGradientRecipe[] s_ramps = BuildRamps();
        static readonly TextStudioGradientRecipe[] s_linear =
            BuildFamily(TextStudio2DPaintKind.LinearGradient);
        static readonly TextStudioGradientRecipe[] s_radial =
            BuildFamily(TextStudio2DPaintKind.RadialGradient);
        static readonly TextStudioGradientRecipe[] s_conic =
            BuildFamily(TextStudio2DPaintKind.ConicGradient);
        static readonly TextStudioGradientRecipe[] s_fourCorner =
            BuildFamily(TextStudio2DPaintKind.FourCornerGradient);

        internal static IReadOnlyList<TextStudioGradientRecipe> Recipes => s_recipes;
        internal static IReadOnlyList<TextStudioGradientRecipe> Ramps => s_ramps;

        internal static IReadOnlyList<TextStudioGradientRecipe> ForKind(
            TextStudio2DPaintKind kind)
        {
            return kind switch
            {
                TextStudio2DPaintKind.LinearGradient => s_linear,
                TextStudio2DPaintKind.RadialGradient => s_radial,
                TextStudio2DPaintKind.ConicGradient => s_conic,
                TextStudio2DPaintKind.FourCornerGradient => s_fourCorner,
                _ => Array.Empty<TextStudioGradientRecipe>()
            };
        }

        internal static TextStudioGradientRecipe GetDefault(TextStudio2DPaintKind kind)
        {
            for (int i = 0; i < s_recipes.Length; i++)
            {
                if (s_recipes[i].Kind == kind)
                    return s_recipes[i];
            }
            return null;
        }

        internal static string FamilyLabel(TextStudio2DPaintKind kind)
        {
            return kind switch
            {
                TextStudio2DPaintKind.LinearGradient => "Linear",
                TextStudio2DPaintKind.RadialGradient => "Radial",
                TextStudio2DPaintKind.ConicGradient => "Conic",
                TextStudio2DPaintKind.FourCornerGradient => "Four Corner",
                TextStudio2DPaintKind.DistanceGradient => "Distance",
                _ => "Gradient"
            };
        }

        internal static string UseCaseLabel(TextStudioGradientUseCase useCase)
        {
            return useCase switch
            {
                TextStudioGradientUseCase.Interface => "Interface",
                TextStudioGradientUseCase.Combat => "Combat & Energy",
                TextStudioGradientUseCase.Fantasy => "Fantasy & Magic",
                TextStudioGradientUseCase.ScienceFiction => "Sci-Fi & Neon",
                TextStudioGradientUseCase.Nature => "Nature & Atmosphere",
                TextStudioGradientUseCase.Luxury => "Luxury & Material",
                _ => useCase.ToString()
            };
        }

        internal static bool IsGradient(TextStudio2DPaintKind kind)
        {
            return kind == TextStudio2DPaintKind.LinearGradient ||
                   kind == TextStudio2DPaintKind.RadialGradient ||
                   kind == TextStudio2DPaintKind.ConicGradient ||
                   kind == TextStudio2DPaintKind.FourCornerGradient ||
                   kind == TextStudio2DPaintKind.DistanceGradient;
        }

        internal static bool IsStopGradient(TextStudio2DPaintKind kind)
        {
            return kind == TextStudio2DPaintKind.LinearGradient ||
                   kind == TextStudio2DPaintKind.RadialGradient ||
                   kind == TextStudio2DPaintKind.ConicGradient ||
                   kind == TextStudio2DPaintKind.DistanceGradient;
        }

        static TextStudioGradientRecipe[] BuildRamps()
        {
            List<TextStudioGradientRecipe> ramps = new List<TextStudioGradientRecipe>(54);
            for (int i = 0; i < s_recipes.Length; i++)
            {
                if (s_recipes[i].Kind != TextStudio2DPaintKind.FourCornerGradient)
                    ramps.Add(s_recipes[i]);
            }
            return ramps.ToArray();
        }

        static TextStudioGradientRecipe[] BuildFamily(TextStudio2DPaintKind kind)
        {
            List<TextStudioGradientRecipe> family = new List<TextStudioGradientRecipe>(18);
            for (int i = 0; i < s_recipes.Length; i++)
            {
                if (s_recipes[i].Kind == kind)
                    family.Add(s_recipes[i]);
            }
            return family.ToArray();
        }

        static TextStudioGradientRecipe[] BuildRecipes()
        {
            List<TextStudioGradientRecipe> recipes = new List<TextStudioGradientRecipe>(72);
            AddLinearRecipes(recipes);
            AddRadialRecipes(recipes);
            AddConicRecipes(recipes);
            AddFourCornerRecipes(recipes);
            return recipes.ToArray();
        }

        static void AddLinearRecipes(List<TextStudioGradientRecipe> recipes)
        {
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.brushed-steel", "Brushed Steel", TextStudioGradientUseCase.Luxury, "Cool brushed-metal bands for premium titles and inventory labels.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xB9C5CF), S(0.42f, 0x687685), S(0.58f, 0xDCE4E9), S(1f, 0x414B57));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.chrome-bands", "Chrome Bands", TextStudioGradientUseCase.Luxury, "Hard mirrored bands for logos, trophies, and metallic display text.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xFFFFFF), S(0.11f, 0x3B4657), S(0.24f, 0xECF6FF), S(0.4f, 0x17202E), S(0.54f, 0x8394A8), S(0.62f, 0xFFFFFF), S(0.75f, 0x293548), S(1f, 0xBFD7EC));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.clean-editorial", "Clean Editorial", TextStudioGradientUseCase.Interface, "Quiet blue-white contrast for readable menus, subtitles, and stat panels.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xF9FBFF), S(0.55f, 0xDCE4F0), S(1f, 0xAAB8CC));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.celestial-gold", "Celestial Gold", TextStudioGradientUseCase.Fantasy, "Layered warm gold for legendary rewards, quest headings, and sacred text.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x4A1B05), S(0.22f, 0xFFD978), S(0.5f, 0x8B3F0C), S(0.72f, 0xFFF1AF), S(1f, 0x5B2106));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.mother-of-pearl", "Mother of Pearl", TextStudioGradientUseCase.Luxury, "Soft iridescent pastels for cosmetics, collectibles, and elegant branding.", 24f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xF8E8FF), S(0.32f, 0xD7FFF5), S(0.64f, 0xFFE7D7), S(1f, 0xB9D8FF));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.studio-light", "Studio Light", TextStudioGradientUseCase.Interface, "Controlled dark-to-light reflections for polished title cards and HUD headers.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x151A23), S(0.2f, 0xF6F8FB), S(0.48f, 0x727B89), S(0.72f, 0xFFFFFF), S(1f, 0x252B36));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.health-bar", "Health Bar", TextStudioGradientUseCase.Combat, "Deep red through hot orange for damage, health, and danger readouts.", 0f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x300509), S(0.34f, 0x9D1522), S(0.7f, 0xFF334C), S(1f, 0xFFB347));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.mana-flow", "Mana Flow", TextStudioGradientUseCase.Fantasy, "Deep blue energy rising into cyan for magic, mana, and ability text.", 0f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x051435), S(0.34f, 0x123A8C), S(0.72f, 0x2F80ED), S(1f, 0x57E6FF));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.toxic-slime", "Toxic Slime", TextStudioGradientUseCase.Combat, "Acid green progression for poison, hazards, mutations, and enemy names.", 0f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x0B2612), S(0.35f, 0x258A36), S(0.72f, 0x8BE33D), S(1f, 0xF3FF6A));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.frost-edge", "Frost Edge", TextStudioGradientUseCase.Nature, "Dark arctic blue into white for ice powers, winter worlds, and frozen UI.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x07142A), S(0.32f, 0x3A78B8), S(0.72f, 0xA7E8FF), S(1f, 0xFFFFFF));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.lava-stripe", "Lava Stripe", TextStudioGradientUseCase.Combat, "Black volcanic rock cut by red, orange, and molten yellow bands.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x090305), S(0.2f, 0x4A0800), S(0.48f, 0xD93600), S(0.75f, 0xFF8A00), S(1f, 0xFFE86B));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.royal-banner", "Royal Banner", TextStudioGradientUseCase.Fantasy, "Regal violet and gold for factions, achievements, and ceremonial titles.", 45f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x190D3A), S(0.35f, 0x5E2E9D), S(0.68f, 0xC24BBE), S(1f, 0xF5C85A));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.cyber-pulse", "Cyber Pulse", TextStudioGradientUseCase.ScienceFiction, "Magenta-to-cyan electric contrast for cyberpunk prompts and neon signage.", 25f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x080912), S(0.35f, 0xEF2DFF), S(0.72f, 0x16E1FF), S(1f, 0xFFFFFF));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.desert-sand", "Desert Sand", TextStudioGradientUseCase.Nature, "Earthy brown through sunlit sand for survival, western, and ruin themes.", 20f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x3B2314), S(0.32f, 0x9F6738), S(0.7f, 0xD9A85B), S(1f, 0xF3DEB0));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.forest-quest", "Forest Quest", TextStudioGradientUseCase.Nature, "Deep forest green, teal, and moss gold for exploration and crafting text.", 15f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x061E19), S(0.35f, 0x156B4A), S(0.72f, 0x48A868), S(1f, 0xD6C75A));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.candy-pop", "Candy Pop", TextStudioGradientUseCase.Interface, "Playful pink, violet, blue, and mint for casual games and celebratory UI.", 30f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xFF5FA2), S(0.34f, 0xBC62FF), S(0.7f, 0x5C7CFA), S(1f, 0x55E8D2));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.ghost-fade", "Ghost Fade", TextStudioGradientUseCase.ScienceFiction, "A transparent spectral sweep for hologram reveals and soft text masks.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xBDEBFF, 0f), S(0.55f, 0xBDEBFF, 0.7f), S(1f, 0xFFFFFF, 1f));
            AddRamp(recipes, TextStudio2DPaintKind.LinearGradient, "linear.victory-shine", "Victory Shine", TextStudioGradientUseCase.ScienceFiction, "Bright gold-white sweep for score bonuses, victory text, and reward banners.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x5B3300), S(0.3f, 0xE9A91D), S(0.55f, 0xFFF4B0), S(0.78f, 0xE9A91D), S(1f, 0x8C4D00));
        }

        static void AddRadialRecipes(List<TextStudioGradientRecipe> recipes)
        {
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.aurora-bloom", "Aurora Bloom", TextStudioGradientUseCase.Nature, "A cool aurora core fading through mint, blue, and deep violet.", 0f, V(0.42f, 0.58f), V(0.92f, 0.72f),
                S(0f, 0xF8FFFF), S(0.32f, 0x5FF4DA), S(0.67f, 0x527DFF), S(1f, 0x5A1B8A));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.bubble-glow", "Bubble Glow", TextStudioGradientUseCase.Interface, "Soft candy light for casual-game titles, bubbles, and collectible labels.", 0f, V(0.42f, 0.62f), V(0.95f, 0.8f),
                S(0f, 0xFFFFFF), S(0.28f, 0xFF7EC8), S(0.62f, 0xA987FF), S(1f, 0x55D7FF));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.inner-light", "Inner Light", TextStudioGradientUseCase.Luxury, "A clean white core falling through ice blue into a dark polished edge.", 0f, V(0.5f, 0.5f), V(0.78f, 0.78f),
                S(0f, 0xFFFFFF), S(0.24f, 0x8DEBFF), S(0.56f, 0x3979A9), S(1f, 0x102541));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.neon-halo", "Neon Halo", TextStudioGradientUseCase.ScienceFiction, "Cyan-white-magenta light for neon badges, energy shields, and sci-fi text.", 0f, V(0.5f, 0.5f), V(1.08f, 0.92f),
                S(0f, 0xFFFFFF), S(0.2f, 0x68FBFF), S(0.58f, 0xC9FFFF), S(0.72f, 0xFFFFFF), S(1f, 0xFF4BC8));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.solar-core", "Solar Core", TextStudioGradientUseCase.Combat, "A hot golden center cooling through orange into a dark ember edge.", 0f, V(0.57f, 0.43f), V(0.82f, 0.82f),
                S(0f, 0xFFF4C2), S(0.3f, 0xFFD46A), S(0.58f, 0xFF721A), S(0.82f, 0xA52205), S(1f, 0x310400));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.pearl-spotlight", "Pearl Spotlight", TextStudioGradientUseCase.Luxury, "A gentle off-center pearl highlight for elegant labels and premium dialog.", 0f, V(0.38f, 0.66f), V(1.1f, 0.88f),
                S(0f, 0xFFFFFF), S(0.38f, 0xF2F8FF), S(0.7f, 0xB9D8F4), S(1f, 0xF4FAFF));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.damage-burst", "Damage Burst", TextStudioGradientUseCase.Combat, "White-hot impact into yellow, red, and a transparent damage edge.", 0f, V(0.5f, 0.5f), V(0.86f, 0.86f),
                S(0f, 0xFFFFFF), S(0.2f, 0xFFF07A), S(0.48f, 0xFF4B2E), S(0.78f, 0x8F0B10, 0.85f), S(1f, 0x260003, 0f));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.healing-pulse", "Healing Pulse", TextStudioGradientUseCase.Interface, "Bright mint healing energy with a soft transparent outer falloff.", 0f, V(0.5f, 0.5f), V(0.95f, 0.95f),
                S(0f, 0xFFFFFF), S(0.25f, 0xB8FFD5), S(0.58f, 0x36D977), S(0.82f, 0x087542, 0.8f), S(1f, 0x052E22, 0f));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.arcane-orb", "Arcane Orb", TextStudioGradientUseCase.Fantasy, "A luminous lavender core descending into royal purple and midnight ink.", 0f, V(0.5f, 0.5f), V(0.9f, 0.9f),
                S(0f, 0xFFFFFF), S(0.22f, 0xE3C5FF), S(0.52f, 0x9456E8), S(0.78f, 0x4B1C8C), S(1f, 0x13072D));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.ice-orb", "Ice Orb", TextStudioGradientUseCase.Nature, "A crisp frozen core with cyan facets and a deep blue perimeter.", 0f, V(0.48f, 0.56f), V(0.88f, 0.82f),
                S(0f, 0xFFFFFF), S(0.24f, 0xC9F8FF), S(0.52f, 0x5BD8F1), S(0.78f, 0x2377B8), S(1f, 0x071C46));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.poison-cloud", "Poison Cloud", TextStudioGradientUseCase.Combat, "Murky chartreuse poison fading into a translucent swamp-green edge.", 0f, V(0.46f, 0.54f), V(1.05f, 0.88f),
                S(0f, 0xEFFF74, 0.9f), S(0.32f, 0x8DD83F, 0.92f), S(0.62f, 0x337B2C, 0.8f), S(1f, 0x102817, 0f));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.holy-aura", "Holy Aura", TextStudioGradientUseCase.Fantasy, "White and sacred gold radiance with a transparent ceremonial edge.", 0f, V(0.5f, 0.55f), V(1.08f, 0.92f),
                S(0f, 0xFFFFFF), S(0.3f, 0xFFF7C4), S(0.6f, 0xF3C85A, 0.85f), S(1f, 0x9B6414, 0f));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.shadow-vignette", "Shadow Vignette", TextStudioGradientUseCase.Interface, "A clear center closing into a strong dark edge for legibility and focus.", 0f, V(0.5f, 0.5f), V(1.15f, 1.05f),
                S(0f, 0x05070C, 0f), S(0.52f, 0x05070C, 0.08f), S(0.78f, 0x05070C, 0.38f), S(1f, 0x05070C, 0.9f));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.portal-core", "Portal Core", TextStudioGradientUseCase.ScienceFiction, "A cyan event-horizon core surrounded by violet and near-black space.", 0f, V(0.5f, 0.5f), V(0.82f, 0.82f),
                S(0f, 0xE8FFFF), S(0.18f, 0x4CF5FF), S(0.46f, 0x6154F5), S(0.72f, 0xA32CDD), S(1f, 0x09031C));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.moonlit-fog", "Moonlit Fog", TextStudioGradientUseCase.Nature, "A cool lunar center diffusing into blue-gray atmospheric haze.", 0f, V(0.38f, 0.64f), V(1.2f, 0.92f),
                S(0f, 0xF8FBFF), S(0.3f, 0xC9DDF4), S(0.62f, 0x7189A6, 0.88f), S(1f, 0x263448, 0.5f));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.ember-bloom", "Ember Bloom", TextStudioGradientUseCase.Luxury, "A polished white spark opening into orange, crimson, and charred black.", 0f, V(0.56f, 0.44f), V(0.86f, 0.86f),
                S(0f, 0xFFF9E8), S(0.2f, 0xFFD36B), S(0.5f, 0xF46B1B), S(0.75f, 0x8B1510), S(1f, 0x170305));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.scan-ping", "Scan Ping", TextStudioGradientUseCase.ScienceFiction, "A transparent cyan ring for radar pings, selections, and detection feedback.", 0f, V(0.5f, 0.5f), V(1.15f, 1.15f),
                S(0f, 0x3FF6FF, 0f), S(0.34f, 0x3FF6FF, 0f), S(0.48f, 0xDFFFFF, 1f), S(0.62f, 0x3FF6FF, 0.2f), S(1f, 0x3FF6FF, 0f));
            AddRamp(recipes, TextStudio2DPaintKind.RadialGradient, "radial.boss-warning", "Boss Warning", TextStudioGradientUseCase.Fantasy, "A severe black-red-white target pulse for bosses, alarms, and critical prompts.", 0f, V(0.5f, 0.5f), V(0.92f, 0.92f),
                S(0f, 0x100003), S(0.28f, 0x8C0715), S(0.5f, 0xFFFFFF), S(0.68f, 0xE21B2D), S(1f, 0x120004));
        }

        static void AddConicRecipes(List<TextStudioGradientRecipe> recipes)
        {
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.spectrum-wheel", "Spectrum Wheel", TextStudioGradientUseCase.Interface, "A balanced full-spectrum wheel for customization, rarity, and creative tools.", 18f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xFF4D8F), S(0.2f, 0xFFCB52), S(0.42f, 0x5CF2D6), S(0.64f, 0x56A0FF), S(0.82f, 0xB76BFF), S(1f, 0xFF4D8F));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.prismatic-twist", "Prismatic Twist", TextStudioGradientUseCase.Luxury, "A pale iridescent rotation for premium borders, gems, and polished text.", 45f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xFFE2FA), S(0.25f, 0xC8FFF4), S(0.5f, 0xB8D5FF), S(0.75f, 0xF5C7FF), S(1f, 0xFFE2FA));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.retro-spin", "Retro Spin", TextStudioGradientUseCase.Interface, "Poster yellow, coral, violet, blue, and teal in a bold arcade rotation.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xFFE66D), S(0.2f, 0xFFB23E), S(0.4f, 0xF15D5D), S(0.6f, 0xC94E99), S(0.8f, 0x4C8EDA), S(1f, 0xFFE66D));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.glitter-orbit", "Glitter Orbit", TextStudioGradientUseCase.Fantasy, "Saturated violet, pink, and blue for magical loot and celebratory effects.", 135f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x7B2EFF), S(0.3f, 0xF04BD8), S(0.58f, 0xFFFFFF), S(0.78f, 0x39BFFF), S(1f, 0x7B2EFF));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.glass-vortex", "Glass Vortex", TextStudioGradientUseCase.Luxury, "Deep glassy navy rotating through cyan highlights and violet shadow.", 225f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x071529), S(0.22f, 0x235F88), S(0.48f, 0x6BD7F0), S(0.7f, 0x204A88), S(1f, 0x071529));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.liquid-prism", "Liquid Prism", TextStudioGradientUseCase.Nature, "Oceanic blues and liquid cyan rolling into a deep violet seam.", 315f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x071633), S(0.26f, 0x176AA0), S(0.52f, 0x73E6F2), S(0.76f, 0x254D9B), S(1f, 0x071633));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.team-clash", "Team Clash", TextStudioGradientUseCase.Combat, "Alternating red and blue sectors for versus screens and competitive titles.", 0f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xE43D4F), S(0.24f, 0x711321), S(0.48f, 0x3478E5), S(0.72f, 0x102C67), S(1f, 0xE43D4F));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.neon-circuit", "Neon Circuit", TextStudioGradientUseCase.ScienceFiction, "Cyan and magenta pulses separated by dark circuit-like sectors.", 30f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x12F7FF), S(0.16f, 0x071018), S(0.34f, 0xFF32DD), S(0.5f, 0x12071A), S(0.68f, 0x7C5CFF), S(0.84f, 0x071018), S(1f, 0x12F7FF));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.gold-dial", "Gold Dial", TextStudioGradientUseCase.Luxury, "Alternating gold reflections for medals, clocks, and prestige indicators.", 60f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x5A3405), S(0.18f, 0xF4C24F), S(0.36f, 0xFFF2AE), S(0.56f, 0x8D530B), S(0.78f, 0xD99819), S(1f, 0x5A3405));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.arctic-compass", "Arctic Compass", TextStudioGradientUseCase.Nature, "White and arctic-blue bearings for navigation, frost magic, and map UI.", 90f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xF8FFFF), S(0.24f, 0x82DFFF), S(0.5f, 0x1C67A5), S(0.74f, 0x0A244F), S(1f, 0xF8FFFF));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.inferno-wheel", "Inferno Wheel", TextStudioGradientUseCase.Combat, "Yellow flame, orange heat, red fire, and black ash in continuous rotation.", 120f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xFFF078), S(0.2f, 0xFF8A18), S(0.45f, 0xD62A0C), S(0.72f, 0x3A0504), S(1f, 0xFFF078));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.toxic-spinner", "Toxic Spinner", TextStudioGradientUseCase.Combat, "Alternating acid lime, swamp green, and toxic shadow for hazards.", 150f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xDFFF4F), S(0.25f, 0x55B833), S(0.5f, 0x133D22), S(0.75f, 0x86E43D), S(1f, 0xDFFF4F));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.arcane-sigil", "Arcane Sigil", TextStudioGradientUseCase.Fantasy, "Purple, blue, and rose energy for spell circles and magical emblems.", 180f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x5422A7), S(0.22f, 0x8C55FF), S(0.48f, 0x3F74E8), S(0.72f, 0xE349B6), S(1f, 0x5422A7));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.candy-pinwheel", "Candy Pinwheel", TextStudioGradientUseCase.Interface, "Bright candy sectors for party games, stickers, and playful headings.", 210f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xFF5D9E), S(0.2f, 0xFFD85C), S(0.4f, 0x68E7D5), S(0.6f, 0x5D8CFF), S(0.8f, 0xB86DFF), S(1f, 0xFF5D9E));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.monochrome-sweep", "Monochrome Sweep", TextStudioGradientUseCase.ScienceFiction, "Graphic white, gray, and black sectors for restrained tech and print looks.", 240f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xF7F8FA), S(0.24f, 0xAEB4BC), S(0.5f, 0x22272E), S(0.74f, 0x656D78), S(1f, 0xF7F8FA));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.sci-fi-radar", "Sci-Fi Radar", TextStudioGradientUseCase.ScienceFiction, "A green scanning wedge with transparent dark sectors for tactical readouts.", 270f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0xB5FFCF, 1f), S(0.1f, 0x39F58A, 0.85f), S(0.28f, 0x0E673B, 0.35f), S(0.62f, 0x071F16, 0.08f), S(1f, 0xB5FFCF, 1f));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.celestial-compass", "Celestial Compass", TextStudioGradientUseCase.Fantasy, "Midnight blue, starlight cream, and gold for maps and celestial UI.", 300f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x111B3E), S(0.22f, 0x36528A), S(0.48f, 0xF8E8B2), S(0.72f, 0xD9A632), S(1f, 0x111B3E));
            AddRamp(recipes, TextStudio2DPaintKind.ConicGradient, "conic.hologram-spin", "Hologram Spin", TextStudioGradientUseCase.Nature, "Translucent cyan, violet, and mint rotation for projections and shields.", 330f, V(0.5f, 0.5f), Vector2.one,
                S(0f, 0x4FF5FF, 0.8f), S(0.24f, 0x7185FF, 0.75f), S(0.5f, 0xC65BFF, 0.82f), S(0.76f, 0x55F0B5, 0.7f), S(1f, 0x4FF5FF, 0.8f));
        }

        static void AddFourCornerRecipes(List<TextStudioGradientRecipe> recipes)
        {
            AddCorner(recipes, "four-corner.horizon-dawn", "Horizon Dawn", TextStudioGradientUseCase.Nature, "Open-sky blues for calm backgrounds, world labels, and loading titles.",
                C(0.24f, 0.46f, 0.86f), C(0.13f, 0.69f, 0.86f), C(0.32f, 0.77f, 0.92f), C(0.56f, 0.83f, 0.95f));
            AddCorner(recipes, "four-corner.coral-sunset", "Coral Sunset", TextStudioGradientUseCase.Nature, "Coral, apricot, and sunlit yellow for warm cinematic cards.",
                C(0.98f, 0.45f, 0.58f), C(0.98f, 0.62f, 0.45f), C(0.98f, 0.82f, 0.53f), C(0.98f, 0.73f, 0.43f));
            AddCorner(recipes, "four-corner.mint-lagoon", "Mint Lagoon", TextStudioGradientUseCase.Nature, "Fresh mint and lagoon teal for relaxed, friendly game UI.",
                C(0.45f, 0.88f, 0.72f), C(0.28f, 0.8f, 0.72f), C(0.7f, 0.94f, 0.72f), C(0.36f, 0.85f, 0.78f));
            AddCorner(recipes, "four-corner.nebula-violet-rose", "Nebula Violet Rose", TextStudioGradientUseCase.Fantasy, "Deep violet and rose nebula colors for magic and cosmic titles.",
                C(0.33f, 0.18f, 0.66f), C(0.56f, 0.27f, 0.83f), C(0.99f, 0.45f, 0.7f), C(0.83f, 0.3f, 0.63f));
            AddCorner(recipes, "four-corner.twilight-ember", "Twilight Ember", TextStudioGradientUseCase.Combat, "Cool twilight above warm embers for dramatic encounter text.",
                C(0.12f, 0.15f, 0.34f), C(0.3f, 0.19f, 0.46f), C(0.94f, 0.55f, 0.25f), C(0.98f, 0.76f, 0.36f));
            AddCorner(recipes, "four-corner.midnight-slate", "Midnight Slate", TextStudioGradientUseCase.Interface, "Subtle midnight slate variation for readable dark UI surfaces.",
                C(0.09f, 0.11f, 0.17f), C(0.14f, 0.18f, 0.26f), C(0.11f, 0.13f, 0.2f), C(0.16f, 0.2f, 0.28f));
            AddCorner(recipes, "four-corner.arcade-neon", "Arcade Neon", TextStudioGradientUseCase.ScienceFiction, "Hot magenta, cyan, violet, and blue for arcade marquees.",
                C(0xFF35D3), C(0x24E8FF), C(0x772EFF), C(0x276CFF));
            AddCorner(recipes, "four-corner.frost-citadel", "Frost Citadel", TextStudioGradientUseCase.Fantasy, "Icy white, cyan, blue, and indigo for frozen fantasy worlds.",
                C(0xE9FCFF), C(0x8BE8FF), C(0x4B9DD8), C(0x223D89));
            AddCorner(recipes, "four-corner.inferno-arena", "Inferno Arena", TextStudioGradientUseCase.Combat, "Molten yellow, orange, crimson, and char for intense battle text.",
                C(0xFFE76A), C(0xFF821E), C(0xD52B0B), C(0x280304));
            AddCorner(recipes, "four-corner.forest-shrine", "Forest Shrine", TextStudioGradientUseCase.Fantasy, "Moss, jade, teal, and sacred gold for forest temples.",
                C(0x85C95A), C(0x2F9C69), C(0x0D4C3C), C(0xD1B54A));
            AddCorner(recipes, "four-corner.royal-tournament", "Royal Tournament", TextStudioGradientUseCase.Combat, "Royal blue, purple, crimson, and gold for competitive banners.",
                C(0x2853B8), C(0x7A3DC0), C(0x9C233F), C(0xE6B84A));
            AddCorner(recipes, "four-corner.ocean-depths", "Ocean Depths", TextStudioGradientUseCase.Nature, "Surface cyan descending into cobalt, teal, and abyssal navy.",
                C(0x46DCEB), C(0x337DE1), C(0x0D7B79), C(0x061A43));
            AddCorner(recipes, "four-corner.candy-kingdom", "Candy Kingdom", TextStudioGradientUseCase.Interface, "Pink, peach, lavender, and mint for playful casual-game panels.",
                C(0xFF79B7), C(0xFFB36E), C(0xB881FF), C(0x69E2CB));
            AddCorner(recipes, "four-corner.tactical-hud", "Tactical HUD", TextStudioGradientUseCase.ScienceFiction, "Dark navy, cyan, green, and slate for restrained tactical interfaces.",
                C(0x0B1E31), C(0x17607A), C(0x12483E), C(0x1B2C45));
            AddCorner(recipes, "four-corner.desert-raid", "Desert Raid", TextStudioGradientUseCase.Combat, "Sunlit sand, rust, leather brown, and dusty gold for survival UI.",
                C(0xE5C073), C(0xB96332), C(0x5D321E), C(0xC79645));
            AddCorner(recipes, "four-corner.healing-sanctuary", "Healing Sanctuary", TextStudioGradientUseCase.Interface, "Soft white, mint, aqua, and green for restorative feedback.",
                C(0xF2FFF8), C(0xA5F3D0), C(0x5FD0C3), C(0x4CB873));
            AddCorner(recipes, "four-corner.shadow-realm", "Shadow Realm", TextStudioGradientUseCase.Luxury, "Near-black violet, smoke, plum, and steel for dark premium themes.",
                C(0x130C20), C(0x29213C), C(0x421A4F), C(0x263044));
            AddCorner(recipes, "four-corner.holographic-shift", "Holographic Shift", TextStudioGradientUseCase.Luxury, "Cyan, lavender, rose, and mint for iridescent collectibles.",
                C(0x8EF4FF), C(0xC6A5FF), C(0xFF9DCC), C(0x8FF2C9));
        }

        static void AddRamp(
            List<TextStudioGradientRecipe> recipes,
            TextStudio2DPaintKind kind,
            string id,
            string name,
            TextStudioGradientUseCase useCase,
            string description,
            float angle,
            Vector2 center,
            Vector2 scale,
            params TextStudio2DGradientStop[] stops)
        {
            recipes.Add(new TextStudioGradientRecipe(
                id,
                name,
                description,
                kind,
                useCase,
                angle,
                center,
                scale,
                Vector2.zero,
                stops));
        }

        static void AddCorner(
            List<TextStudioGradientRecipe> recipes,
            string id,
            string name,
            TextStudioGradientUseCase useCase,
            string description,
            Color topLeft,
            Color topRight,
            Color bottomLeft,
            Color bottomRight)
        {
            recipes.Add(new TextStudioGradientRecipe(
                id,
                name,
                description,
                TextStudio2DPaintKind.FourCornerGradient,
                useCase,
                0f,
                V(0.5f, 0.5f),
                Vector2.one,
                Vector2.zero,
                Array.Empty<TextStudio2DGradientStop>(),
                new TextStudio2DFourCornerGradient(
                    topLeft,
                    topRight,
                    bottomLeft,
                    bottomRight)));
        }

        static TextStudio2DGradientStop S(float position, uint rgb, float alpha = 1f)
        {
            return new TextStudio2DGradientStop(position, C(rgb, alpha));
        }

        static Color C(uint rgb, float alpha = 1f)
        {
            return new Color(
                ((rgb >> 16) & 0xff) / 255f,
                ((rgb >> 8) & 0xff) / 255f,
                (rgb & 0xff) / 255f,
                alpha);
        }

        static Color C(float red, float green, float blue)
        {
            return new Color(red, green, blue, 1f);
        }

        static Vector2 V(float x, float y)
        {
            return new Vector2(x, y);
        }
    }
}
