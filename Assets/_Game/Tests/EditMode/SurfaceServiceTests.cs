using MB13.Aipocalypse.Core.SurfaceSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class SurfaceServiceTests
    {
        // 130 texels: two full 64-texel tiles and a 2-texel edge tile per side.
        const int Resolution = 130;

        SurfaceSettings _settings;
        TerrainData _data;
        TerrainLayer _grass;
        TerrainLayer _rock;
        GameObject _terrain;
        SurfaceService _surfaces;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SurfaceSettings>();
            _settings.Rules.Add(new SurfaceRule { Keyword = "Rock", Kind = SurfaceKind.Stone });
            _settings.Rules.Add(new SurfaceRule { Keyword = "Grass", Kind = SurfaceKind.Grass });
            _grass = new TerrainLayer { name = "GrassLayer" };
            _rock = new TerrainLayer { name = "RockLayer" };
            _data = new TerrainData { alphamapResolution = Resolution, terrainLayers = new[] { _grass, _rock } };
            _data.size = new Vector3(Resolution - 1, 10f, Resolution - 1);
            // Grass west of x = 100, rock from there on; a near tie (0.5 / 0.5) at x = 50 goes to the first layer.
            var weights = new float[Resolution, Resolution, 2];
            for (int y = 0; y < Resolution; y++)
                for (int x = 0; x < Resolution; x++)
                {
                    float rock = x >= 100 ? 1f : x == 50 ? 0.5f : 0f;
                    weights[y, x, 0] = 1f - rock;
                    weights[y, x, 1] = rock;
                }
            _data.SetAlphamaps(0, 0, weights);
            _terrain = new GameObject("Terrain", typeof(TerrainCollider));
            _terrain.GetComponent<TerrainCollider>().terrainData = _data;
            _surfaces = new SurfaceService(_settings, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            _surfaces.Dispose();
            Object.DestroyImmediate(_terrain);
            Object.DestroyImmediate(_data);
            Object.DestroyImmediate(_grass);
            Object.DestroyImmediate(_rock);
            Object.DestroyImmediate(_settings);
        }

        SurfaceKind At(float x, float z) => _surfaces.KindOf(_terrain.GetComponent<TerrainCollider>(), new Vector3(x, 0f, z));

        [Test]
        public void KindOf_Terrain_IsTheStrongestLayerAtThePoint_InEveryTile()
        {
            Assert.AreEqual(SurfaceKind.Grass, At(10f, 10f));
            Assert.AreEqual(SurfaceKind.Grass, At(99f, 70f), "second tile, last grass texel");
            Assert.AreEqual(SurfaceKind.Stone, At(100f, 70f));
            Assert.AreEqual(SurfaceKind.Stone, At(129f, 129f), "the far corner sits in the small edge tile");
            Assert.AreEqual(SurfaceKind.Grass, At(50f, 3f), "equal weights: the first layer wins");
            Assert.AreEqual(SurfaceKind.Grass, At(-20f, 500f), "points off the terrain clamp to its edge");
        }

        [Test]
        public void KindOf_Terrain_AnswersAgainWithoutReadingTheAlphamapEachTime()
        {
            Assert.AreEqual(SurfaceKind.Stone, At(120f, 5f));
            // The tile is read once: changing the alphamap afterwards doesn't show (it never changes in play).
            var grass = new float[1, 1, 2];
            grass[0, 0, 0] = 1f;
            _data.SetAlphamaps(120, 5, grass);
            Assert.AreEqual(SurfaceKind.Stone, At(120f, 5f));
        }
    }
}
