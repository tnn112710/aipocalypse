using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.Effects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MB13.Aipocalypse.Core.SurfaceSystem
{
    // Root service: what a collider is made of at a point (terrain: its strongest layer there; anything else: the first
    // rule keyword in its name, its parents' or its material's, remembered per collider), and that surface's sounds.
    public sealed class SurfaceService : IDisposable
    {
        // Parents whose names are read with the collider's own.
        const int ParentDepth = 2;
        // Alphamap texels per side of a terrain tile read in one GetAlphamaps call.
        const int TileSize = 64;

        // Strongest layer per alphamap texel of one TerrainData, a tile filled on its first query.
        sealed class TerrainLookup
        {
            public TerrainLayer[] Layers;
            public int Width;
            public int Height;
            public int TilesX;
            public byte[][] Tiles;
        }

        readonly SurfaceSettings _settings;
        readonly AudioService _audio;
        readonly EffectPool _effects;
        readonly Dictionary<Collider, SurfaceKind> _colliders = new();
        readonly Dictionary<TerrainLayer, SurfaceKind> _layers = new();
        readonly Dictionary<TerrainData, TerrainLookup> _terrains = new();
        int _impactFrame = -1;
        int _impactsThisFrame;

        public SurfaceService(SurfaceSettings settings, AudioService audio, EffectPool effects)
        {
            _settings = settings;
            _audio = audio;
            _effects = effects;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            if (!Enabled || _settings.Looks == null)
                return;
            foreach (SurfaceLook look in _settings.Looks.Values)
                if (look != null)
                    _effects.Prewarm(look.ImpactEffect, _settings.ImpactPrewarm);
        }

        public bool Enabled => _settings != null && _settings.Enabled;

        public void Dispose() => SceneManager.sceneUnloaded -= OnSceneUnloaded;

        // Root service outliving scenes: forget the unloaded scene's colliders and terrains.
        void OnSceneUnloaded(Scene scene)
        {
            _colliders.Clear();
            _terrains.Clear();
        }

        public SurfaceKind KindOf(Collider collider, Vector3 point)
        {
            if (!Enabled || collider == null)
                return _settings != null ? _settings.Default : SurfaceKind.Dirt;
            if (collider is TerrainCollider terrain)
                return TerrainKind(terrain, point);
            if (_colliders.TryGetValue(collider, out SurfaceKind known))
                return known;
            SurfaceKind kind = Classify(collider);
            _colliders[collider] = kind;
            return kind;
        }

        // The surface's look, or null when the system is off or the kind has none.
        public SurfaceLook LookAt(Collider collider, Vector3 point) => Enabled ? _settings.Look(KindOf(collider, point)) : null;
        public SurfaceLook Look(SurfaceKind kind) => Enabled ? _settings.Look(kind) : null;

        // A bullet landed on a surface: its impact effect (or `fallback`) and its sound.
        public void BulletImpact(Collider collider, Vector3 point, Vector3 normal, PooledEffect fallback)
        {
            SurfaceLook look = LookAt(collider, point);
            PooledEffect effect = look != null && look.ImpactEffect != null ? look.ImpactEffect : fallback;
            _effects.Spawn(effect, point, Quaternion.LookRotation(normal));
            if (look != null && TakeImpactSound())
                _audio.PlayAt(look.BulletImpact, point);
        }

        // A bullet landed on something that bleeds.
        public void FleshImpact(Vector3 point)
        {
            if (Enabled && TakeImpactSound())
                _audio.PlayAt(_settings.FleshImpact, point);
        }

        bool TakeImpactSound()
        {
            if (_impactFrame != Time.frameCount)
            {
                _impactFrame = Time.frameCount;
                _impactsThisFrame = 0;
            }
            return _impactsThisFrame++ < _settings.MaxImpactSoundsPerFrame;
        }

        SurfaceKind Classify(Collider collider)
        {
            Transform t = collider.transform;
            for (int depth = 0; t != null && depth <= ParentDepth; depth++, t = t.parent)
                if (_settings.TryMatch(t.name, out SurfaceKind byName))
                    return byName;

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null && collider.transform.parent != null)
                renderer = collider.transform.parent.GetComponentInChildren<Renderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material != null && _settings.TryMatch(material.name, out SurfaceKind byMaterial))
                return byMaterial;
            return _settings.Default;
        }

        SurfaceKind TerrainKind(TerrainCollider collider, Vector3 point)
        {
            TerrainData data = collider.terrainData;
            if (data == null)
                return _settings.DefaultTerrain;
            TerrainLookup lookup = LookupFor(data);
            if (lookup.Layers.Length == 0)
                return _settings.DefaultTerrain;

            Vector3 local = point - collider.transform.position;
            int x = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * (lookup.Width - 1)), 0, lookup.Width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * (lookup.Height - 1)), 0, lookup.Height - 1);
            TerrainLayer layer = lookup.Layers[StrongestAt(data, lookup, x, y)];
            if (layer == null)
                return _settings.DefaultTerrain;
            if (_layers.TryGetValue(layer, out SurfaceKind known))
                return known;
            SurfaceKind kind = _settings.TryMatch(layer.name, out SurfaceKind byName) ? byName
                : layer.diffuseTexture != null && _settings.TryMatch(layer.diffuseTexture.name, out SurfaceKind byTexture) ? byTexture
                : _settings.DefaultTerrain;
            _layers[layer] = kind;
            return kind;
        }

        TerrainLookup LookupFor(TerrainData data)
        {
            if (_terrains.TryGetValue(data, out TerrainLookup lookup))
                return lookup;
            int width = data.alphamapWidth;
            int height = data.alphamapHeight;
            int tilesX = (width + TileSize - 1) / TileSize;
            lookup = new TerrainLookup
            {
                Layers = data.terrainLayers ?? Array.Empty<TerrainLayer>(),
                Width = width,
                Height = height,
                TilesX = tilesX,
                Tiles = new byte[tilesX * ((height + TileSize - 1) / TileSize)][],
            };
            _terrains[data] = lookup;
            return lookup;
        }

        // The index of the strongest layer at texel (x, y); the first of equal weights wins.
        static int StrongestAt(TerrainData data, TerrainLookup lookup, int x, int y)
        {
            int tileX = x / TileSize;
            int tileY = y / TileSize;
            int x0 = tileX * TileSize;
            int y0 = tileY * TileSize;
            int width = Mathf.Min(TileSize, lookup.Width - x0);
            ref byte[] tile = ref lookup.Tiles[tileY * lookup.TilesX + tileX];
            if (tile == null)
            {
                int height = Mathf.Min(TileSize, lookup.Height - y0);
                float[,,] weights = data.GetAlphamaps(x0, y0, width, height);
                int count = Mathf.Min(lookup.Layers.Length, weights.GetLength(2));
                tile = new byte[width * height];
                for (int j = 0; j < height; j++)
                    for (int i = 0; i < width; i++)
                    {
                        int strongest = 0;
                        for (int k = 1; k < count; k++)
                            if (weights[j, i, k] > weights[j, i, strongest])
                                strongest = k;
                        tile[j * width + i] = (byte)strongest;
                    }
            }
            return tile[(y - y0) * width + (x - x0)];
        }
    }
}
