using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aipocalypse.Tests
{
    // Stylized Grass wiring that breaks silently: wind masks, the tiled lawn colour map, the player's bender.
    public class StylizedGrassTests
    {
        const string DetailFolder = "Assets/_Game/Environment/Vegetation/Details";
        const string Colormap = "Assets/_Game/Environment/Vegetation/Textures/BK_Meadow_Colormap";
        const string PlayerPrefab = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";

        [Test]
        public void StylizedDetailPrefabs_HaveAVertexColourWindMask()
        {
            var stylized = AssetDatabase.FindAssets("t:Prefab", new[] { DetailFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p.GetComponent<MeshRenderer>().sharedMaterial.shader.name.StartsWith("Stylized Grass"))
                .ToList();

            Assert.That(stylized, Is.Not.Empty);
            foreach (var prefab in stylized)
            {
                var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                using var data = Mesh.AcquireReadOnlyMeshData(mesh);
                Assert.That(data[0].HasVertexAttribute(VertexAttribute.Color), Is.True, $"{prefab.name}: Stylized Grass reads wind from vertex colour, roots would slide");
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
    }
}
