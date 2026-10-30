using UnityEngine;
using UnityEngine.Rendering;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreExplosionEffect : MonoBehaviour
    {
        private const float Lifetime = 4.5f;
        private const float LightDuration = 0.35f;

        private Light flashLight;
        private float startTime;
        private float peakIntensity;

        public static ArmoryCoreExplosionEffect Spawn(Vector3 position, Vector3 normal, float radius)
        {
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.up;
            var root = new GameObject("ArmoryCore Explosion");
            root.SetActive(false);
            root.transform.SetPositionAndRotation(position + normal * 0.1f, Quaternion.LookRotation(normal));
            var effect = root.AddComponent<ArmoryCoreExplosionEffect>();
            var scale = Mathf.Clamp(radius / 5f, 0.4f, 3f);

            CreateFireball(root.transform, scale);
            CreateSmoke(root.transform, scale);
            CreateDebris(root.transform, scale);

            effect.flashLight = root.AddComponent<Light>();
            effect.flashLight.type = LightType.Point;
            effect.flashLight.color = new Color(1f, 0.62f, 0.28f);
            effect.flashLight.range = radius * 3f;
            effect.flashLight.shadows = LightShadows.None;
            effect.peakIntensity = 8f * scale;
            effect.flashLight.intensity = effect.peakIntensity;
            effect.startTime = Time.time;

            root.SetActive(true);
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>())
                particles.Play(true);
            Destroy(root, Lifetime);
            return effect;
        }

        private void Update()
        {
            if (flashLight == null)
                return;
            var t = (Time.time - startTime) / LightDuration;
            if (t >= 1f)
            {
                Destroy(flashLight);
                flashLight = null;
                return;
            }
            flashLight.intensity = peakIntensity * (1f - t) * (1f - t);
        }

        private static void CreateFireball(Transform parent, float scale)
        {
            var particles = CreateParticleObject("Fireball", parent, ArmoryCoreEffectMaterials.Fireball);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * scale, 9f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.6f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.45f, 1f),
                new Color(1f, 0.45f, 0.12f, 1f));
            main.gravityModifier = -0.15f;
            main.maxParticles = 40;

            var emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22, 30) });
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.35f * scale;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 1.15f)));
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient(
                new Color(1f, 0.95f, 0.7f),
                new Color(0.9f, 0.3f, 0.08f),
                1f);
        }

        private static void CreateSmoke(Transform parent, float scale)
        {
            var particles = CreateParticleObject("Smoke", parent, ArmoryCoreEffectMaterials.Smoke);
            var main = particles.main;
            main.startDelay = 0.05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * scale, 4.5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f * scale, 3.2f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.22f, 0.21f, 0.2f, 0.85f),
                new Color(0.42f, 0.4f, 0.38f, 0.75f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 40;

            var emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18, 26) });
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.6f * scale;

            var velocity = particles.limitVelocityOverLifetime;
            velocity.enabled = true;
            velocity.limit = 0.6f * scale;
            velocity.dampen = 0.08f;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(1f, 2.2f)));
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient(Color.white, Color.white, 0.85f);
        }

        private static void CreateDebris(Transform parent, float scale)
        {
            var particles = CreateParticleObject("Debris", parent, ArmoryCoreEffectMaterials.Spark);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f * scale, 22f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new Color(1f, 0.62f, 0.2f, 1f);
            main.gravityModifier = 1f;
            main.maxParticles = 60;

            var emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 50) });
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = 0.2f * scale;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.04f;
        }

        private static ParticleSystem CreateParticleObject(string name, Transform parent, Material material)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static Gradient FadeGradient(Color start, Color end, float startAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[]
                {
                    new GradientAlphaKey(startAlpha, 0f),
                    new GradientAlphaKey(startAlpha * 0.8f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }

    internal static class ArmoryCoreEffectMaterials
    {
        private static Material rocketBody;
        private static Material motorFlame;
        private static Material smokeTrail;
        private static Material fireball;
        private static Material smoke;
        private static Material spark;
        private static Texture2D softParticleTexture;
        private static Texture2D softTrailTexture;

        public static Material RocketBody
        {
            get
            {
                if (rocketBody != null)
                    return rocketBody;
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                rocketBody = new Material(shader) { name = "Runtime Rocket Body" };
                SetColor(rocketBody, new Color(0.2f, 0.23f, 0.17f, 1f));
                if (rocketBody.HasProperty("_Smoothness"))
                    rocketBody.SetFloat("_Smoothness", 0.35f);
                return rocketBody;
            }
        }

        public static Material MotorFlame
        {
            get
            {
                if (motorFlame != null)
                    return motorFlame;
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                motorFlame = new Material(shader) { name = "Runtime Rocket Motor Flame" };
                SetColor(motorFlame, new Color(1f, 0.72f, 0.3f, 1f));
                return motorFlame;
            }
        }

        public static Material SmokeTrail =>
            smokeTrail != null ? smokeTrail : smokeTrail = CreateParticleMaterial("Runtime Rocket Smoke Trail", SoftTrailTexture, false);

        public static Material Fireball =>
            fireball != null ? fireball : fireball = CreateParticleMaterial("Runtime Explosion Fireball", SoftParticleTexture, true);

        public static Material Smoke =>
            smoke != null ? smoke : smoke = CreateParticleMaterial("Runtime Explosion Smoke", SoftParticleTexture, false);

        public static Material Spark =>
            spark != null ? spark : spark = CreateParticleMaterial("Runtime Explosion Debris", null, true);

        private static Texture2D SoftParticleTexture =>
            softParticleTexture != null ? softParticleTexture : softParticleTexture = CreateSoftTexture(64, 64, true);

        private static Texture2D SoftTrailTexture =>
            softTrailTexture != null ? softTrailTexture : softTrailTexture = CreateSoftTexture(4, 64, false);

        private static Material CreateParticleMaterial(string name, Texture texture, bool additive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = name };
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
            }
            SetColor(material, Color.white);

            // Configure the URP particle shader for transparency the way its material inspector would.
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", additive ? 2f : 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_SrcBlendAlpha"))
                    material.SetFloat("_SrcBlendAlpha", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                if (material.HasProperty("_DstBlendAlpha"))
                    material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (additive)
                    material.EnableKeyword("_BLENDMODE_ADD");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            return material;
        }

        private static void SetColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
        }

        private static Texture2D CreateSoftTexture(int width, int height, bool radial)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = radial ? "Runtime Soft Particle" : "Runtime Soft Trail",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var v = (y + 0.5f) / height * 2f - 1f;
                    var u = radial ? (x + 0.5f) / width * 2f - 1f : 0f;
                    var distance = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                    var alpha = Mathf.SmoothStep(1f, 0f, distance);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
