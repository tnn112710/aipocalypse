
using System.Collections.Generic;

namespace DistantLands.Cozy.Horizon
{
    public struct HorizonLayerInstance
    {
        public float blend;
        public CozyHorizonProfile.HorizonLayerReference layer;
        public HorizonLayerInstance(CozyHorizonProfile.HorizonLayerReference layer, float blend)
        {
            this.layer = layer;
            this.blend = blend;
        }
    }
}