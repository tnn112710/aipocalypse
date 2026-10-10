using JBooth.MicroVerseCore;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class HeightStampUtils
    {
        public enum ConstrainWorldBounds
        {
            [Tooltip("Constrain the position so that the whole gameobject is within world bounds")]
            Full,
            [Tooltip("Constrain the position so that at least half of the gameobject is within world bounds")]
            Half,
            [Tooltip("Unconstrained positioning. The gameobject is within world bounds, but can be partially outside")]
            Disabled
        }

        public enum StampPosition
        {
            Center
        }

        public enum ScaleOption
        {
            XYZ,
            XZ,
            Y
        }

        public static void SetScale(Transform transform, ScaleOption scaleOption)
        {
            Bounds worldBounds = MicroVerse.instance.GetTerrainWorldBounds();
            switch (scaleOption)
            {
                case ScaleOption.XYZ:
                    {
                        float x = worldBounds.size.x;
                        float y = transform.localScale.y;
                        float z = worldBounds.size.z;
                        y = getReasonableTerrainHeight(y);

                        transform.localScale = new Vector3(x, y, z);
                    }
                    break;

                case ScaleOption.XZ:
                    {
                        float x = worldBounds.size.x;
                        float y = transform.localScale.y;
                        float z = worldBounds.size.z;
                        y = getReasonableTerrainHeight(y);

                        transform.localScale = new Vector3(x, y, z);
                    }

                    break;

                case ScaleOption.Y:
                    {
                        float x = transform.localScale.x;
                        float y = worldBounds.size.y / 2f;
                        float z = transform.localScale.z;
                        y = getReasonableTerrainHeight(y);

                        transform.localScale = new Vector3(x, y, z);
                    }
                    break;
            }
        }

        /// <summary>
        /// If terrain height is very low we use a custom calculation:
        /// 
        /// * get the maximum height of the terrain size (instead of the bounds)
        /// * multiply it by an arbitrary factor (eg a third)
        /// 
        /// This is just to see anything instead of flat terrain. Problem could occur if the terrain is new and the first stamp is being fit to the terrain.
        /// 
        /// </summary>
        /// <param name="originalHeight"></param>
        /// <returns></returns>
        private static float getReasonableTerrainHeight(float originalHeight)
        {
            float threshold = 10f;

            if (originalHeight >= threshold)
                return originalHeight;

            // use terrain height
            float height = MicroVerse.instance.GetTerrainMeshHeight();

            height *= 0.3f;

            return height;
        }

        public static void SetPosition(Transform transform, StampPosition stampPosition)
        {
            Bounds worldBounds = MicroVerse.instance.GetTerrainWorldBounds();

            float y = worldBounds.center.y - worldBounds.size.y / 2f;

            transform.position = new Vector3(worldBounds.center.x, y, worldBounds.center.z);
        }

        public static void ResetRotation(Transform transform)
        {
            transform.rotation = Quaternion.identity;

        }

        public static void Rotate(Transform transform, float angle)
        {
            transform.Rotate(Vector3.up, angle);

        }

        public static void IncreaseSize(Transform transform, float scaleChange)
        {
            float scaleLimit = 4000f;

            Vector3 scale = transform.localScale *= (1 + scaleChange);

            if (scale.magnitude > scaleLimit)
            {
                Debug.LogError($"Scale limit of {scaleLimit} reached");
                return;
            }

            transform.localScale = scale;
        }

        public static void DecreaseSize(Transform transform, float scaleChange)
        {
            float scaleLimit = 100;

            Vector3 scale = transform.localScale *= (1 - scaleChange);

            if (scale.magnitude < scaleLimit)
            {
                Debug.LogError($"Internal scale limit of {scaleLimit} reached");
                return;
            }

            transform.localScale = scale;
        }

        public static void SetRandomPosition(Transform[] transforms, ConstrainWorldBounds constrainWorldBounds)
        {
            foreach( Transform transform in transforms)
            {
                SetRandomPosition(transform, constrainWorldBounds);
            }
        }

        public static void SetRandomPosition(Transform transform, ConstrainWorldBounds constrainWorldBounds)
        {
            Bounds worldBounds = MicroVerse.instance.GetTerrainWorldBounds();

            Vector3 localScale = transform.localScale;

            float xRange = worldBounds.size.x;
            float zRange = worldBounds.size.z;

            if (xRange < 0)
                xRange = 0;

            if (zRange < 0)
                zRange = 0;

            float x = Random.Range(0f, xRange);
            float y = transform.position.y;
            float z = Random.Range(0f, zRange);

            float factor;
            switch (constrainWorldBounds)
            {
                case ConstrainWorldBounds.Full:
                    factor = 0.5f; // half of the scale since the pivot is in the center
                    x = Mathf.Clamp(x, localScale.x * factor, worldBounds.size.x - localScale.x * factor);
                    z = Mathf.Clamp(z, localScale.z * factor, worldBounds.size.z - localScale.z * factor);
                    break;

                case ConstrainWorldBounds.Half:
                    factor = 0.25f; // quarter of the scale since the pivot is in the center
                    x = Mathf.Clamp(x, localScale.x * factor, worldBounds.size.x - localScale.x * factor);
                    z = Mathf.Clamp(z, localScale.z * factor, worldBounds.size.z - localScale.z * factor);
                    break;

                case ConstrainWorldBounds.Disabled:
                    // do nothing
                    break;
            }

            transform.position = new Vector3(x, y, z);
        }

        public static void SetRandomRotation(Transform[] transforms)
        {
            foreach (Transform transform in transforms)
            {
                SetRandomRotation(transform);
            }
        }

        public static void SetRandomRotation(Transform transform)
        {
            float angle = Random.Range(0f, 360f);

            transform.Rotate(Vector3.up, angle);
        }

        /// <summary>
        /// Get the world bounds of all the terrain which are used by MicroVerse
        /// </summary>
        public static Bounds GetTerrainWorldBounds()
        {
            Terrain[] terrains = MicroVerse.instance.GetComponentsInChildren<Terrain>();

            Bounds worldBounds = TerrainUtil.ComputeTerrainBounds(terrains);
            
            return worldBounds;

        }
    }
}