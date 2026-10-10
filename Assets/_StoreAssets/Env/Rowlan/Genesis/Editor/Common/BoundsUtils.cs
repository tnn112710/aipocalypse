using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class BoundsUtils
    {

        private static Bounds zeroBounds = new Bounds(Vector3.zero, Vector3.zero);

        /// <summary>
        /// Get the local and world bounds from a gameobject.
        /// Note: This doesn't work with LODs.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="localBounds"></param>
        /// <param name="worldBounds"></param>
        /// <returns></returns>
        public static bool GetBounds( Transform transform, out Bounds localBounds, out Bounds worldBounds)
        {
            MeshFilter meshFilter = transform.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = transform.GetComponent<MeshRenderer>();

            if (meshFilter && meshRenderer)
            {
                localBounds = meshFilter.sharedMesh.bounds;
                worldBounds = meshRenderer.bounds;

                return true;
            }
            else
            {

                SkinnedMeshRenderer skinnedMeshRenderer = transform.GetComponent<SkinnedMeshRenderer>();

                if (skinnedMeshRenderer)
                {
                    localBounds = skinnedMeshRenderer.sharedMesh.bounds;
                    worldBounds = skinnedMeshRenderer.bounds;

                    return true;
                }

            }

            localBounds = zeroBounds;
            worldBounds = zeroBounds;

            return false;
        }

        /// <summary>
        /// Get the bounds of a gameobject considering LOD.
        /// Thanks Lennart!
        /// </summary>
        /// <param name="gameObject">The gameobject with renderers including children</param>
        /// <param name="combinedWorldBounds">The combined bounds of the gameobject and its children in world space</param>
        /// <returns></returns>
        public static bool CalculateBounds(GameObject gameObject, out Bounds combinedWorldBounds)
        {
            // assignment in case there are no renderers; also used for initial assignment of out parameter, but will be created new if there are renderers
            combinedWorldBounds = new Bounds(gameObject.transform.position, Vector3.zero);

            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();

            // if there are no renderers return empty bounds
            if( renderers.Length == 0)
            {
                return false;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Bounds currentBounds;

                if (renderer is SkinnedMeshRenderer)
                {
                    SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;

                    Mesh mesh = new Mesh();

                    skinnedMeshRenderer.BakeMesh(mesh);
                    Vector3[] vertices = mesh.vertices;

                    for (int vertexIndex = 0; vertexIndex <= vertices.Length - 1; vertexIndex++)
                    {
                        vertices[vertexIndex] = skinnedMeshRenderer.transform.TransformPoint(vertices[vertexIndex]);
                    }
                    mesh.vertices = vertices;
                    mesh.RecalculateBounds();

                    Bounds meshBounds = mesh.bounds;
                    currentBounds = meshBounds;
                }
                else if( renderer is ParticleSystemRenderer)
                {
                    // basically skip the particle system renderer
                    // it would resoult in the point being at 0/0/0 which would result in a very large bounds object
                    currentBounds = new Bounds(gameObject.transform.position, Vector3.zero);
                }
                else
                {
                    currentBounds = renderer.bounds;
                }

                if( rendererIndex == 0)
                {
                    combinedWorldBounds = new Bounds(currentBounds.center, currentBounds.size);
                }
                else
                {
                    combinedWorldBounds.Encapsulate(currentBounds);
                }
            }

            return true;
        }
    }
}
