using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Makes chosen materials glow over their hours (house windows at night). Renderers under this object that use them
    // switch to one runtime copy per material with emission on; the material assets are never written.
    public sealed class TimeOfDayEmission : TimeOfDayReactor
    {
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Tooltip("Materials to light up, e.g. Window_Glass")]
        Material[] _materials = System.Array.Empty<Material>();
        [SerializeField, ColorUsage(false, true)] Color _emission = new(2.4f, 1.3f, 0.5f);

        readonly List<Material> _copies = new();

        void Awake()
        {
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || System.Array.IndexOf(_materials, source) < 0)
                        continue;
                    if (!copies.TryGetValue(source, out Material copy))
                    {
                        copy = new Material(source) { name = source.name + " (Time Of Day)" };
                        copy.EnableKeyword("_EMISSION");
                        copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                        copies.Add(source, copy);
                        _copies.Add(copy);
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        protected override void Apply(float weight)
        {
            Color color = _emission * weight;
            foreach (Material copy in _copies)
                copy.SetColor(EmissionId, color);
        }

        protected override void OnDestroy()
        {
            foreach (Material copy in _copies)
                Destroy(copy);
            base.OnDestroy();
        }
    }
}
