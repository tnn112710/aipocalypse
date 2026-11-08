using UnityEngine;
using UnityEngine.Rendering;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // A lightning flash: a global volume of its own (post exposure, above the grades) pulsed along a curve by Strike.
    // The volume and this component are off between strikes.
    [RequireComponent(typeof(Volume))]
    public sealed class LightningFlash : MonoBehaviour
    {
        [SerializeField, Tooltip("Volume weight over seconds since the strike: the flash ends at the last key")]
        AnimationCurve _weight = new(
            new Keyframe(0f, 0f), new Keyframe(0.03f, 1f), new Keyframe(0.09f, 0.15f), new Keyframe(0.14f, 0.8f), new Keyframe(0.4f, 0f));

        Volume _volume;
        float _time;
        float _end;

        void Awake()
        {
            _volume = GetComponent<Volume>();
            _end = _weight.length > 0 ? _weight[_weight.length - 1].time : 0f;
            Stop();
        }

        public void Strike()
        {
            if (_end <= 0f)
                return;
            _time = 0f;
            _volume.weight = 0f;
            _volume.enabled = true;
            enabled = true;
        }

        void Update()
        {
            _time += Time.deltaTime;
            if (_time >= _end)
            {
                Stop();
                return;
            }
            _volume.weight = Mathf.Clamp01(_weight.Evaluate(_time));
        }

        void Stop()
        {
            _volume.weight = 0f;
            _volume.enabled = false;
            enabled = false;
        }
    }
}
