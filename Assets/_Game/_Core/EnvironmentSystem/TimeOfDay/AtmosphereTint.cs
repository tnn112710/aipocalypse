using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    public enum AtmosphereChannel
    {
        [Tooltip("Unlit effects: mist, haze banks, clouds, god rays, the waterfall")] UnlitTint,
        [Tooltip("Far mountains' own height haze")] Haze,
        Fog,
        Sun,
        AmbientSky,
    }

    // Pushes an atmosphere colour into materials the director can't reach: vendor shaders with their own haze, unlit effects
    // that would glow at night. Works on runtime copies only, so shared and vendor materials are never written.
    public sealed class AtmosphereTint : EnvironmentReactor
    {
        struct Slot
        {
            public Material Copy;
            public int Id;
            public Color Original;
        }

        [SerializeField, Tooltip("Empty: every material under this object that has one of the properties")]
        Material[] _materials = System.Array.Empty<Material>();
        [SerializeField, Tooltip("Colour properties to tint; each material uses the ones it has")]
        string[] _properties = { "_BaseColor" };
        [SerializeField] AtmosphereChannel _channel;
        [SerializeField, Tooltip("On: material colour × channel (keeps each material's own tint). Off: the channel colour")]
        bool _multiply = true;
        [SerializeField] bool _keepAlpha = true;

        readonly List<Material> _copies = new();
        readonly List<Slot> _slots = new();
        Color _last = new(-1f, -1f, -1f, -1f);

        void Awake()
        {
            var ids = new int[_properties.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = Shader.PropertyToID(_properties[i]);

            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !HasAny(source, ids))
                        continue;
                    if (_materials.Length > 0 && System.Array.IndexOf(_materials, source) < 0)
                        continue;
                    if (!copies.TryGetValue(source, out Material copy))
                    {
                        copy = new Material(source) { name = source.name + " (Atmosphere)" };
                        copies.Add(source, copy);
                        _copies.Add(copy);
                        foreach (int id in ids)
                            if (source.HasProperty(id))
                                _slots.Add(new Slot { Copy = copy, Id = id, Original = source.GetColor(id) });
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        static bool HasAny(Material material, int[] ids)
        {
            foreach (int id in ids)
                if (material.HasProperty(id))
                    return true;
            return false;
        }

        public override void React(EnvironmentDirector director, float deltaTime)
        {
            ref readonly AtmosphereState state = ref director.State;
            Color color = _channel switch
            {
                AtmosphereChannel.Haze => state.HazeColor,
                AtmosphereChannel.Fog => state.FogColor,
                AtmosphereChannel.Sun => state.SunColor,
                AtmosphereChannel.AmbientSky => state.AmbientSky,
                _ => state.UnlitTint,
            };
            if (color == _last)
                return;
            _last = color;

            foreach (Slot slot in _slots)
            {
                Color value = _multiply ? slot.Original * color : color;
                if (_keepAlpha)
                    value.a = slot.Original.a;
                slot.Copy.SetColor(slot.Id, value);
            }
        }

        protected override void OnDestroy()
        {
            foreach (Material copy in _copies)
                Destroy(copy);
            base.OnDestroy();
        }
    }
}
