using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.Ghosts;
using MB13.Aipocalypse.Core.PlayerSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class GhostTests
    {
        const string FenceLookPath = "Assets/_Game/Content/Structures/WoodFence/WoodFence_Look.prefab";
        const string InteractionTuningPath = "Assets/_Game/Data/Player/PlayerInteractionTuning.asset";

        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Capture_KeepsShownLodZeroMeshes_AtTheirRestPose()
        {
            _root = new GameObject("Root");
            Mesh mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Renderer plain = Part("Plain", _root.transform, mesh, new Vector3(1f, 0f, 0f));
            Part("Hidden", _root.transform, mesh, Vector3.zero).gameObject.SetActive(false);

            var lodRoot = new GameObject("Lods");
            lodRoot.transform.SetParent(_root.transform, false);
            Renderer lod0 = Part("LOD0", lodRoot.transform, mesh, Vector3.zero);
            Renderer lod1 = Part("LOD1", lodRoot.transform, mesh, Vector3.zero);
            lodRoot.AddComponent<LODGroup>().SetLODs(new[] { new LOD(0.5f, new[] { lod0 }), new LOD(0.1f, new[] { lod1 }) });

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(_root.transform, false);
            pivot.localRotation = Quaternion.Euler(80f, 0f, 0f);
            Part("OnPivot", pivot, mesh, new Vector3(0f, 1f, 0f));

            GhostShape shape = GhostShape.Capture(_root.transform, t => t == pivot ? Matrix4x4.identity : null);

            Assert.That(shape.Count, Is.EqualTo(3), "plain, LOD 0 and the pivot's child; not the hidden one or LOD 1");
            Assert.That(shape[0].Local.GetPosition(), Is.EqualTo(plain.transform.localPosition));
            Assert.That(Vector3.Distance(shape[2].Local.GetPosition(), new Vector3(0f, 1f, 0f)), Is.LessThan(0.001f),
                "the pivot at rest, not tipped over");
        }

        [Test]
        public void CaptureLive_FollowsTheModelAsItMoves()
        {
            _root = new GameObject("Root");
            Renderer part = Part("Part", _root.transform, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), Vector3.zero);
            GhostShape shape = GhostShape.CaptureLive(_root.transform);

            part.transform.position = new Vector3(3f, 2f, 1f);
            Matrix4x4 drawn = shape[0].WorldMatrix(Matrix4x4.Translate(new Vector3(100f, 0f, 0f)));

            Assert.That(shape.Count, Is.EqualTo(1));
            Assert.That(Vector3.Distance(drawn.GetPosition(), part.transform.position), Is.LessThan(0.001f),
                "where the renderer is now, whatever the ghost's pose");
        }

        [Test]
        public void FenceLook_CaptureStanding_IgnoresTheFall()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<BarrierLook>(FenceLookPath);
            Assert.That(prefab, Is.Not.Null, FenceLookPath);
            BarrierLook look = Object.Instantiate(prefab);
            _root = look.gameObject;

            GhostShape standing = look.CaptureStanding();
            look.Show(0f, broken: true, instant: true);
            GhostShape fallen = look.CaptureStanding();

            Assert.That(standing.Count, Is.GreaterThan(0));
            Assert.That(fallen.Count, Is.EqualTo(standing.Count));
            for (int i = 0; i < standing.Count; i++)
                Assert.That(Vector3.Distance(fallen[i].Local.GetPosition(), standing[i].Local.GetPosition()), Is.LessThan(0.001f), $"part {i}");
        }

        [Test]
        public void PlayerInteractionTuning_HasAGhostStyle()
        {
            GhostStyle style = AssetDatabase.LoadAssetAtPath<PlayerInteractionTuning>(InteractionTuningPath).Ghost;
            Assert.That(style, Is.Not.Null);
            Assert.That(style.Valid != null && style.Invalid != null && style.DepthOnly != null, Is.True, "valid, invalid and depth materials");
            Assert.That(style.DepthOnly.renderQueue, Is.LessThan(style.Valid.renderQueue), "depth is laid down before the colour");
            Assert.That(style.DepthOnly.renderQueue, Is.LessThan(style.Invalid.renderQueue));
        }

        static Renderer Part(string name, Transform parent, Mesh mesh, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            return go.AddComponent<MeshRenderer>();
        }
    }
}
