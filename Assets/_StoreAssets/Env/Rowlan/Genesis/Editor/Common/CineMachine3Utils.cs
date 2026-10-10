#if USING_CINEMACHINE3
using Unity.Cinemachine;
#endif

namespace Rowlan.Genesis.Gameplay
{
    public class CineMachine3Utils
    {
        public static bool IsCineMachineInstalled()
        {
            bool installed = false;
#if USING_CINEMACHINE3
            installed = true;
#endif
            return installed;
        }
    }
}