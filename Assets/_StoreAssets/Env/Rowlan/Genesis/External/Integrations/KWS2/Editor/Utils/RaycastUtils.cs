using UnityEngine;

namespace Rowlan.Genesis.Integrations.KWS2
{
    public class RaycastUtils
    {
        /// <summary>
        /// Align the transforms to the ground via raycast using the provided paramters.
        /// </summary>
        /// <param name="transforms"></param>
        /// <param name="raycastHeight"></param>
        /// <param name="offsetY"></param>
        /// <param name="alignToNormal"></param>
        /// <param name="layerMask"></param>
        public static void AlignToGround(Transform[] transforms, float raycastHeight, float offsetY, bool alignToNormal, int layerMask)
        {
            foreach (Transform transform in transforms)
            {
                Vector3 position = transform.position;
                Vector3 rayOrigin = new Vector3(position.x, position.y + raycastHeight, position.z);

                Ray ray = new Ray(rayOrigin, Vector3.down);

                if (Physics.Raycast(ray, out RaycastHit hit, raycastHeight * 2f, layerMask))
                {
                    if (!hit.collider.gameObject)
                        continue;

                    // position
                    transform.transform.position = hit.point + Vector3.up * offsetY;

                    // rotation
                    if (alignToNormal)
                    {
                        transform.up = hit.normal;
                    }

                }
            }
        }
    }
}