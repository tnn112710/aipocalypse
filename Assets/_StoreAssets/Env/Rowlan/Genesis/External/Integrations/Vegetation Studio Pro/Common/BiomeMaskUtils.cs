#if VEGETATION_STUDIO_PRO

using AwesomeTechnologies;
using AwesomeTechnologies.VegetationSystem.Biomes;
using UnityEngine;

namespace Rowlan.Genesis.Integrations.VegetationStudioPro
{
    public class BiomeMaskUtils
    {
        /// <summary>
        /// Force update on the mask in scene view
        /// </summary>
        /// <param name="mask"></param>
        public static void UpdateMask(BiomeMaskArea mask)
        {
            // TODO: that's just a quick hack to update the mask. mask.PositionNodes(); doesn't seem to work and mask.Update() has an optimization in it
            mask.transform.position = mask.transform.position + new Vector3(0, 1, 0);
            mask.transform.position = mask.transform.position + new Vector3(0, -1, 0);

            // apply the mask to the vegetation
            mask.UpdateBiomeMask();
        }
    }

}
#endif
