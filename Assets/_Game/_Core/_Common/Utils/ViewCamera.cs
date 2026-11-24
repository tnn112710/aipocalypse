using Unity.Cinemachine;
using UnityEngine;

namespace MB13.Aipocalypse.Core
{
    // The camera the player sees through: the output of the active Cinemachine brain (on the player prefab), so screen
    // projections and camera-following effects don't depend on the MainCamera tag. Null when no brain is active.
    public static class ViewCamera
    {
        public static Camera Current
        {
            get
            {
                for (int i = 0; i < CinemachineBrain.ActiveBrainCount; i++)
                {
                    Camera output = CinemachineBrain.GetActiveBrain(i).OutputCamera;
                    if (output != null && output.isActiveAndEnabled)
                        return output;
                }
                return null;
            }
        }
    }
}
