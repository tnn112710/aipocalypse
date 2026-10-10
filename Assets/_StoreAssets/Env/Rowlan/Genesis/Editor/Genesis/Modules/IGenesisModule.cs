using UnityEngine;

namespace Rowlan.Genesis
{
    public interface IGenesisModule
    {
        public void OnInspectorGUI();

        public void Create(GameObject parent);

        public void Destroy();

    }
}