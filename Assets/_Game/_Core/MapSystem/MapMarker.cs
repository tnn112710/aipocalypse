using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.MapSystem
{
    // Puts this object on the minimap as its kind's icon while it is enabled. Needs injection to register: a scene-placed one goes
    // in the scene scope's Auto Inject Game Objects.
    public sealed class MapMarker : MonoBehaviour
    {
        [SerializeField] MapMarkerKind _kind;

        MapMarkerRegistry _registry;

        public MapMarkerKind Kind => _kind;

        [Inject]
        void Construct(MapMarkerRegistry registry)
        {
            _registry = registry;
            // Injected after OnEnable when the scope builds late: register now (the list ignores repeats).
            if (isActiveAndEnabled)
                OnEnable();
        }

        void OnEnable() => _registry?.Add(this);
        void OnDisable() => _registry?.Remove(this);
    }
}
