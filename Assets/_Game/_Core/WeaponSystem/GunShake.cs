using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Plays a GunShakeSettings over a repair's progress as an offset of the gun model (HeldGun adds it on the recoil pivot).
    public sealed class GunShake
    {
        GunShakeSettings _settings;
        bool _stubborn;
        float _progress;
        int _landed;
        // After the repair: the offset it ended on, eased back to rest.
        Vector3 _endPosition;
        Quaternion _endRotation = Quaternion.identity;
        float _returnLeft;
        float _returnTime;

        public bool IsPlaying => _settings != null;

        public void Start(GunShakeSettings settings, bool stubborn)
        {
            _settings = settings;
            _stubborn = stubborn;
            _progress = 0f;
            _landed = 0;
            _returnLeft = 0f;
        }

        // Advances to `progress`; true when a shake landed since the last call (`hard` for a stubborn repair's hard ones).
        // One per call, so a long frame's shakes sound on the following frames instead of all at once.
        public bool Tick(float progress, out bool hard)
        {
            hard = false;
            if (_settings == null)
                return false;
            _progress = Mathf.Clamp01(progress);
            int count = _settings.Shakes(_stubborn);
            if (_landed >= count || _progress < _settings.BeatAt(_landed, count))
                return false;
            hard = _settings.IsHard(_landed, _stubborn);
            _landed++;
            return true;
        }

        // The repair ended (finished, cancelled or cut short by the QTE): ease back from wherever the gun is.
        public void Finish()
        {
            if (_settings == null)
                return;
            Pose(out _endPosition, out _endRotation);
            _returnTime = _returnLeft = _settings.ReturnTime;
            _settings = null;
        }

        public void Stop()
        {
            _settings = null;
            _returnLeft = 0f;
        }

        public void TickReturn(float deltaTime) => _returnLeft = Mathf.Max(0f, _returnLeft - deltaTime);

        public void Apply(ref Vector3 position, ref Quaternion rotation)
        {
            if (_settings != null)
            {
                Pose(out Vector3 offset, out Quaternion turn);
                position += offset;
                rotation *= turn;
            }
            else if (_returnLeft > 0f)
            {
                float weight = Mathf.SmoothStep(0f, 1f, _returnLeft / _returnTime);
                position += _endPosition * weight;
                rotation *= Quaternion.Slerp(Quaternion.identity, _endRotation, weight);
            }
        }

        void Pose(out Vector3 position, out Quaternion rotation)
        {
            GunShakeSettings s = _settings;
            float tilt = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, s.TiltShare, _progress)) *
                         Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 1f - s.TiltShare, _progress));
            float shake = 0f;
            int count = s.Shakes(_stubborn);
            for (int i = 0; i < count; i++)
                shake += Wave((_progress - s.StartAt(i, count)) / s.ShakeShare, s.WindUp, s.Swings) * (s.IsHard(i, _stubborn) ? s.HardScale : 1f);
            position = s.TiltPosition * tilt + s.ShakePosition * shake;
            rotation = Quaternion.Euler(s.TiltEuler * tilt) * Quaternion.Euler(s.ShakeEuler * shake);
        }

        // One shake's signed weight at u (0 = the strike starts, 1 = rung out): a wind-up before it, a snap to the peak,
        // then damped swings.
        public static float Wave(float u, float windUp, float swings)
        {
            const float lead = 0.35f;
            if (u <= -lead || u >= 1f)
                return 0f;
            if (u < 0f)
                return -windUp * Mathf.SmoothStep(0f, 1f, 1f + u / lead);
            if (u < GunShakeSettings.Strike)
                return Mathf.Lerp(-windUp, 1f, Mathf.SmoothStep(0f, 1f, u / GunShakeSettings.Strike));
            float t = (u - GunShakeSettings.Strike) / (1f - GunShakeSettings.Strike);
            float decay = 1f - t;
            return Mathf.Cos(t * Mathf.PI * 2f * swings) * decay * decay;
        }
    }
}
