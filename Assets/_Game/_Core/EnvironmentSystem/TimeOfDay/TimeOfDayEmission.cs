using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
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
            RuntimeMaterials.CopyUnder(this, source => System.Array.IndexOf(_materials, source) >= 0, source =>
            {
                var copy = new Material(source) { name = source.name + " (Time Of Day)" };
                copy.EnableKeyword("_EMISSION");
                copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                return copy;
            }, _copies);
        }

        protected override void Apply(float weight)
        {
            Color color = _emission * weight;
            foreach (Material copy in _copies)
                copy.SetColor(EmissionId, color);
        }

        protected override void OnDestroy()
        {
            RuntimeMaterials.Destroy(_copies);
            base.OnDestroy();
        }
    }
}
