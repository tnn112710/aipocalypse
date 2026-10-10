using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if USING_LEGACY
using UnityEngine.Rendering.PostProcessing;
#endif
namespace Rowlan.Genesis.Ambience.Legacy
{
    public class PostProcessingSetup : ScriptableObject
    {
#if USING_LEGACY
        public Camera camera;
        public PostProcessLayer postProcessLayer;
        public PostProcessVolume postProcessVolume;
        public PostProcessProfile postProcessProfile;
#endif

    }
}