using System.Linq;
using Aipocalypse.Core.EnvironmentSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aipocalypse.Tests
{
    // Stylized Grass wiring that breaks silently: wind masks, the tiled lawn colour map, the player's bender, the crowd bender.
    public class StylizedGrassTests
    {
        const string DetailFolder = "Assets/_Game/Environment/Vegetation/Details";
        const string Colormap = "Assets/_Game/Environment/Vegetation/Textures/BK_Meadow_Colormap";
        const string PlayerPrefab = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";

        static System.Collections.Generic.List<GameObject> StylizedDetails() => AssetDatabase.FindAssets("t:Prefab", new[] { DetailFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p.GetComponent<MeshRenderer>().sharedMaterial.shader.name.StartsWith("Stylized Grass"))
            .ToList();

        [Test]
        public void StylizedDetailPrefabs_HaveAVertexColourWindMask()
        {
            var stylized = StylizedDetails();

            Assert.That(stylized, Is.Not.Empty);
            foreach (var prefab in stylized)
            {
                var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                using var data = Mesh.AcquireReadOnlyMeshData(mesh);
                Assert.That(data[0].HasVertexAttribute(VertexAttribute.Color), Is.True, $"{prefab.name}: Stylized Grass reads wind from vertex colour, roots would slide");
            }
        }

        [Test]
        public void DetailsWithAFlatBendMask_DoNotBend()
        {
            foreach (var prefab in StylizedDetails())
            {
                var colors = prefab.GetComponent<MeshFilter>().sharedMesh.colors;
                if (colors.Length == 0 || colors.Min(c => c.r) < 0.99f)
                    continue;
                var material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(material.GetFloat("_BendPushStrength") + material.GetFloat("_BendFlattenStrength"), Is.Zero,
                    $"{prefab.name}: its roots carry the full bend mask, so a bender would slide or sink the whole card");
            }
        }

        [Test]
        public void LawnColormap_IsTheRepeatingNoiseTile()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Colormap + ".png");
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));

            var map = AssetDatabase.LoadMainAssetAtPath(Colormap + ".asset");
            var so = new SerializedObject(map);
            Assert.That(so.FindProperty("overrideTexture").boolValue, Is.True, "a Render on the colour map renderer replaced the tile");
            Assert.That(so.FindProperty("customTex").objectReferenceValue, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(Colormap + ".png")));
            Assert.That(so.FindProperty("bounds").boundsValue.size.x, Is.EqualTo(25f).Within(0.01f), "the tile repeats every 25 m, like BK's noise");
        }

        [Test]
        public void PlayerPrefab_HasAGrassBenderAtItsFeet()
        {
            var bender = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab).transform.Find("Grass Bender");

            Assert.That(bender, Is.Not.Null);
            Assert.That(bender.GetComponents<MonoBehaviour>().Any(c => c.GetType().Name == "GrassBender"), Is.True);
            Assert.That(bender.GetComponent<MeshRenderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void GrassBendField_AddsEachMoverOnce_AndForgetsRemovedOnes()
        {
            var field = new GameObject("field").AddComponent<GrassBendField>();
            var a = new GameObject("a").transform;
            var b = new GameObject("b").transform;
            try
            {
                field.Add(a, 0.6f);
                field.Add(a, 0.6f);
                field.Add(b, 0f);
                Assert.That(field.Count, Is.EqualTo(1), "a mover joins once; radius 0 bends nothing");

                field.Add(b, 0.4f);
                field.Remove(a);
                field.Remove(a);
                Assert.That(field.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(field.gameObject);
                Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(b.gameObject);
            }
        }
    }
}
