using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    // Procedural recoil: each shot kicks the view (aim: moves the crosshair and where shots go) and adds a smaller kick
    // to the gun model, along a random spray pattern; both spring back. The gun is attached to the view, so its offset
    // is only the extra on top. HeldGun applies the gun's, the player's look applies the view's.
    public sealed class GunRecoil
    {
        const float SprayResetDelay = 0.24f;
        const float SprayNoiseStep = 0.347f;
        const float SprayPitchJitter = 0.18f;
        const float SprayYawJitter = 0.72f;
        const float SprayRollFactor = 0.55f;

        readonly RecoilSettings _settings;
        readonly float _instanceNoise = Random.Range(0f, 100f);

        Vector3 _gunTarget;
        Vector3 _gun;
        Vector3 _viewTarget;
        Vector3 _view;
        Vector3 _shakeTarget;
        Vector3 _shake;
        float _kickTarget;
        float _kick;

        int _shotIndex;
        float _sinceShot = float.MaxValue;
        float _sprayNoise;
        float _sprayPhase;
        float _sprayDrift;
        float _sprayClimb;

        public GunRecoil(RecoilSettings settings)
        {
            _settings = settings;
            BeginSpray();
        }

        // Extra euler offset for the gun model on top of the view; negative x lifts the muzzle.
        public Vector3 GunEuler => _gun;
        // Metres the gun is pushed back.
        public float GunKickBack => _kick;
        // Euler offset for the view; negative x looks up.
        public Vector3 ViewEuler => _view;
        public Vector3 ViewShake => _shake;

        // `stateMultiplier`: extra kick from moving or being airborne.
        public void Kick(bool aiming, float stateMultiplier)
        {
            RecoilValues values = _settings.For(aiming);

            if (_sinceShot > SprayResetDelay)
            {
                _shotIndex = 0;
                BeginSpray();
            }
            int shot = _shotIndex++;
            _sinceShot = 0f;

            float sample = _instanceNoise + _sprayNoise + shot * SprayNoiseStep;
            float pitchNoise = SignedNoise(sample, 3.17f);
            float yawNoise = SignedNoise(5.73f, sample);
            float shakeNoise = SignedNoise(sample + 9.41f, 1.19f);
            Pattern(shot, out float pitchScale, out float yawPattern, out float rollPattern);

            float multiplier = AccurateShotsMultiplier(shot) * values.Multiplier * stateMultiplier;
            float pitch = Mathf.Max(0.01f, values.Vertical * pitchScale + pitchNoise * values.NoisePitch);
            float yaw = yawPattern * values.Horizontal + yawNoise * values.NoiseYaw;

            _viewTarget += new Vector3(-pitch, yaw, rollPattern * values.Roll) * multiplier;
            _shakeTarget += new Vector3(yawNoise, pitchNoise + shakeNoise * 0.35f, 0f) * (values.CameraShake * stateMultiplier);

            _gunTarget += new Vector3(-values.GunPitch * pitchScale, values.GunYaw * yawPattern, values.GunRoll * rollPattern) * multiplier;
            _kickTarget += values.KickBack * multiplier;
        }

        public void Tick(float deltaTime, bool aiming)
        {
            if (deltaTime <= 0f)
                return;
            _sinceShot += deltaTime;

            RecoilValues values = _settings.For(aiming);
            float verticalReturn = Damp(values.VerticalReturnSpeed, deltaTime);
            float horizontalReturn = Damp(values.HorizontalReturnSpeed, deltaTime);
            float verticalSnap = Damp(values.VerticalSnappiness, deltaTime);
            float horizontalSnap = Damp(values.HorizontalSnappiness, deltaTime);

            float gunReturn = Damp(values.GunReturnSpeed, deltaTime);
            float gunSnap = Damp(values.GunSnappiness, deltaTime);
            _gunTarget = Vector3.Lerp(_gunTarget, Vector3.zero, gunReturn);
            _gun = Vector3.Lerp(_gun, _gunTarget, gunSnap);
            _viewTarget = DampAxes(_viewTarget, Vector3.zero, verticalReturn, horizontalReturn);
            _view = DampAxes(_view, _viewTarget, verticalSnap, horizontalSnap);
            _shakeTarget = Vector3.Lerp(_shakeTarget, Vector3.zero, horizontalReturn);
            _shake = Vector3.Lerp(_shake, _shakeTarget, horizontalSnap);
            _kickTarget = Mathf.Lerp(_kickTarget, 0f, gunReturn);
            _kick = Mathf.Lerp(_kick, _kickTarget, gunSnap);
        }

        public void Reset()
        {
            _gunTarget = _gun = _viewTarget = _view = _shakeTarget = _shake = Vector3.zero;
            _kickTarget = _kick = 0f;
            _shotIndex = 0;
            _sinceShot = float.MaxValue;
        }

        float AccurateShotsMultiplier(int shot) =>
            _settings.AccurateShots >= 2 && shot < _settings.AccurateShots ? Mathf.Clamp01(_settings.AccurateShotsMultiplier) : 1f;

        // Smooth climb and side-to-side drift that differ every spray.
        void Pattern(int shotIndex, out float pitchScale, out float yawPattern, out float rollPattern)
        {
            float shot = shotIndex + 1f;
            float pitchNoise = SignedNoise(17.13f + _sprayNoise, shot * 0.23f + _sprayPhase);
            float yawNoise = SignedNoise(41.73f + _sprayNoise, shot * 0.31f - _sprayPhase);
            float extraYawNoise = SignedNoise(83.91f + _sprayNoise, shot * 0.47f + _sprayPhase * 0.5f);

            pitchScale = Mathf.Max(0.1f, 1f + shot * _sprayClimb + pitchNoise * SprayPitchJitter);
            float yaw = yawNoise * SprayYawJitter + extraYawNoise * 0.2f + _sprayDrift;
            yawPattern = Mathf.Clamp(yaw, -1f, 1f);
            rollPattern = Mathf.Clamp(yaw * SprayRollFactor, -1f, 1f);
        }

        void BeginSpray()
        {
            _sprayNoise = Random.Range(-1000f, 1000f);
            _sprayPhase = Random.Range(-Mathf.PI, Mathf.PI);
            _sprayDrift = Random.Range(-0.35f, 0.35f);
            _sprayClimb = Random.Range(0.02f, 0.075f);
        }

        static float SignedNoise(float x, float y) => Mathf.PerlinNoise(x, y) * 2f - 1f;

        // x (pitch) uses the vertical rate, y and z the horizontal one.
        static Vector3 DampAxes(Vector3 current, Vector3 target, float verticalT, float horizontalT) => new(
            Mathf.Lerp(current.x, target.x, verticalT),
            Mathf.Lerp(current.y, target.y, horizontalT),
            Mathf.Lerp(current.z, target.z, horizontalT));

        static float Damp(float speed, float deltaTime) => speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * deltaTime);
    }
}
