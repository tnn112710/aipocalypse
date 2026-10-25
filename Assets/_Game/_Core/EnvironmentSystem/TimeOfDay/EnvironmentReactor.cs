using Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // Something in the scene that follows the environment, ticked by the EnvironmentDirector in Play mode (no Update of its own).
    // Placed in a scene: the director collects it on save / Play. Spawned at runtime: inject it (resolver.Instantiate).
    public abstract class EnvironmentReactor : MonoBehaviour
    {
        EnvironmentDirector _director;

        [Inject]
        void Construct(IObjectResolver resolver)
        {
            if (resolver.TryResolve(out _director))
                _director.Register(this);
        }

        protected virtual void OnDestroy()
        {
            if (_director != null)
                _director.Unregister(this);
        }

        public abstract void React(EnvironmentDirector director, float deltaTime);
    }

    public enum ReactorSource
    {
        [Tooltip("On between two hours")] Hours,
        [Tooltip("Follows the sun: 1 by day, 0 at night")] Daylight,
        [Tooltip("1 at night, 0 by day")] Darkness,
    }

    // Turns something on and off over the day with a 0–1 weight. Subclass and implement Apply for a new kind of object.
    public abstract class TimeOfDayReactor : EnvironmentReactor
    {
        [SerializeField] ReactorSource _source = ReactorSource.Hours;
        [SerializeField, ShowIf(nameof(_source), ReactorSource.Hours)] TimeWindow _hours = new(18.5f, 6f, 0.5f);
        [SerializeField, Min(0f), Tooltip("Real seconds to fade, so clock jumps (N / M cheats) don't pop")]
        float _response = 1.5f;

        float _weight = -1f;
        float _applied = -1f;

        public float Weight => _weight;

        public override void React(EnvironmentDirector director, float deltaTime)
        {
            float target = _source switch
            {
                ReactorSource.Daylight => director.Daylight,
                ReactorSource.Darkness => 1f - director.Daylight,
                _ => _hours.Weight(director.Hour),
            };

            // The fade always advances; only Apply is skipped for tiny changes, or high frame rates would never move it.
            _weight = _weight < 0f || _response <= 0f ? target : Mathf.MoveTowards(_weight, target, deltaTime / _response);
            bool settled = _weight == 0f || _weight == 1f;
            if (Mathf.Abs(_weight - _applied) < 0.002f && !(settled && _weight != _applied))
                return;

            _applied = _weight;
            Apply(_weight);
        }

        protected abstract void Apply(float weight);
    }
}
