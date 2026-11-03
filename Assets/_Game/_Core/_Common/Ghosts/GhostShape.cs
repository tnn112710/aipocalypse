using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aipocalypse.Core.Ghosts
{
    // A model's meshes and where each sits under its root, captured once, so a see-through ghost of it can be drawn anywhere
    // (a broken fence standing again, a building or item before it's placed). Only shown renderers count; of a LOD group, LOD 0.
    public sealed class GhostShape
    {
        public readonly struct Part
        {
            public readonly Mesh Mesh;
            public readonly Matrix4x4 Local;

            public Part(Mesh mesh, Matrix4x4 local)
            {
                Mesh = mesh;
                Local = local;
            }
        }

        static readonly List<Renderer> Renderers = new();
        static readonly List<LODGroup> LodGroups = new();
        static readonly HashSet<Renderer> LowerLods = new();
        static readonly HashSet<Renderer> FirstLods = new();

        readonly Part[] _parts;

        GhostShape(Part[] parts) => _parts = parts;

        public int Count => _parts.Length;
        public Part this[int index] => _parts[index];

        // `restLocal` gives a transform's local matrix at rest when it's moved now (a fallen panel's pivot); null keeps its own.
        // Works on scene objects and prefab assets alike.
        public static GhostShape Capture(Transform root, Func<Transform, Matrix4x4?> restLocal = null)
        {
            CollectLowerLods(root);
            root.GetComponentsInChildren(true, Renderers);
            var parts = new List<Part>(Renderers.Count);
            foreach (Renderer renderer in Renderers)
            {
                if (!renderer.enabled || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || LowerLods.Contains(renderer)
                    || !IsShown(renderer.transform, root))
                    continue;
                Mesh mesh = renderer switch
                {
                    MeshRenderer when renderer.TryGetComponent(out MeshFilter filter) => filter.sharedMesh,
                    // Its bind pose: close enough for a preview.
                    SkinnedMeshRenderer skinned => skinned.sharedMesh,
                    _ => null,
                };
                if (mesh != null)
                    parts.Add(new Part(mesh, ToRoot(renderer.transform, root, restLocal)));
            }
            Renderers.Clear();
            LowerLods.Clear();
            return new GhostShape(parts.ToArray());
        }

        // Renderers only in LOD 1 and up (some packs share a renderer between levels: that one stays).
        static void CollectLowerLods(Transform root)
        {
            LowerLods.Clear();
            FirstLods.Clear();
            root.GetComponentsInChildren(true, LodGroups);
            foreach (LODGroup group in LodGroups)
            {
                LOD[] lods = group.GetLODs();
                for (int level = 0; level < lods.Length; level++)
                    foreach (Renderer renderer in lods[level].renderers)
                        if (renderer != null)
                            (level == 0 ? FirstLods : LowerLods).Add(renderer);
            }
            LowerLods.ExceptWith(FirstLods);
            LodGroups.Clear();
            FirstLods.Clear();
        }

        // activeSelf up to the root: a prefab asset isn't in a scene, so activeInHierarchy can't be used.
        static bool IsShown(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (!t.gameObject.activeSelf)
                    return false;
            return true;
        }

        static Matrix4x4 ToRoot(Transform t, Transform root, Func<Transform, Matrix4x4?> restLocal)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            for (; t != null && t != root; t = t.parent)
                matrix = (restLocal?.Invoke(t) ?? Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale)) * matrix;
            return matrix;
        }
    }
}
