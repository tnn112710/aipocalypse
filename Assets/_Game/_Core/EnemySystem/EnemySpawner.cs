using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Aipocalypse.Core.EnemySystem
{
    // Puts enemies into play from code (waves, events, a pool later): spawned under it through the scene's resolver, so each is
    // injected before it wakes. Itself injected by the scene's EnemyInstaller, which lists it on save like placed enemies.
    public sealed class EnemySpawner : MonoBehaviour
    {
        IObjectResolver _resolver;

        [Inject]
        void Construct(IObjectResolver resolver) => _resolver = resolver;

        public EnemyBrain Spawn(EnemyBrain prefab, Vector3 position, Quaternion rotation)
        {
            if (_resolver == null)
            {
                Debug.LogError($"{name}: not injected; save the scene so its EnemyInstaller lists it.", this);
                return null;
            }
            return _resolver.Instantiate(prefab, position, rotation, transform);
        }
    }
}
