//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    public interface IVFXProvider
    {
        public void Center();
        public Transform FXParent { get; }

        public void Initialize();
        public void Deinitialize();

        public bool GetFXParent<T>(out Transform parent);
        public T GetFXRuntimeRef<T>(string name) where T : Component;
    }
}