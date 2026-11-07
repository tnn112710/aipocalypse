//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    public interface IVFXBlockingProvider
    {
        public void UpdateVFXBlocking();
        public void Initialize();
        public void Deinitialize();
    }
}