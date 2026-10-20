using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreShowcaseImpactSurface : MonoBehaviour, IArmoryCoreBallisticTarget
    {
        [SerializeField] private Color markColor = new(0.015f, 0.012f, 0.01f, 1f);
        [SerializeField] private Color sparkColor = new(1f, 0.56f, 0.12f, 1f);
        [SerializeField, Range(0.01f, 0.1f)] private float markRadius = 0.028f;
        [SerializeField, Min(1f)] private float markLifetime = 24f;

        private static Material markMaterial;
        private static Material sparkMaterial;

        public void ReceiveBallisticHit(ArmoryCoreBallisticHit hit)
        {
            CreateImpactMark(hit);
            CreateSparks(hit);
        }

        private void CreateImpactMark(ArmoryCoreBallisticHit hit)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mark.name = "Bullet Impact";
            var collider = mark.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            mark.transform.SetParent(hit.Collider == null ? transform : hit.Collider.transform, true);
            mark.transform.position = hit.Point + hit.Normal * 0.0025f;
            mark.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.Normal);
            var energyScale = Mathf.Lerp(0.82f, 1.35f,
                Mathf.InverseLerp(100f, 2600f, hit.KineticEnergyJoules));
            mark.transform.localScale = new Vector3(
                markRadius * energyScale,
                0.0012f,
                markRadius * energyScale);
            mark.GetComponent<Renderer>().sharedMaterial = GetMarkMaterial();
            Destroy(mark, markLifetime);
        }

        private void CreateSparks(ArmoryCoreBallisticHit hit)
        {
            var sparkObject = new GameObject("Impact Sparks");
            sparkObject.SetActive(false);
            sparkObject.transform.position = hit.Point + hit.Normal * 0.01f;
            sparkObject.transform.rotation = Quaternion.LookRotation(hit.Normal);
            var particles = sparkObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.022f);
            main.startColor = sparkColor;
            main.gravityModifier = 0.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 16;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 11) });
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.012f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.4f;
            renderer.velocityScale = 0.12f;
            renderer.sharedMaterial = GetSparkMaterial();
            sparkObject.SetActive(true);
            particles.Play(true);
            Destroy(sparkObject, 0.55f);
        }

        private Material GetMarkMaterial()
        {
            if (markMaterial != null)
                return markMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            markMaterial = new Material(shader) { name = "Runtime Bullet Impact" };
            markMaterial.color = markColor;
            if (markMaterial.HasProperty("_BaseColor"))
                markMaterial.SetColor("_BaseColor", markColor);
            if (markMaterial.HasProperty("_Smoothness"))
                markMaterial.SetFloat("_Smoothness", 0.12f);
            return markMaterial;
        }

        private Material GetSparkMaterial()
        {
            if (sparkMaterial != null)
                return sparkMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Additive")
                ?? Shader.Find("Sprites/Default");
            sparkMaterial = new Material(shader) { name = "Runtime Impact Spark" };
            sparkMaterial.color = sparkColor;
            if (sparkMaterial.HasProperty("_BaseColor"))
                sparkMaterial.SetColor("_BaseColor", sparkColor);
            return sparkMaterial;
        }
    }
}
