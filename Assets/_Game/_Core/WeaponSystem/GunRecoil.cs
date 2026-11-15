using MB13.Aipocalypse.Core.RandomSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Procedural recoil: each shot kicks the view (crosshair and shots) and, less, the gun model along a random spray pattern.
    // KeptShare of the aim kick turns the look for good (LookTurn), the rest springs back; hip/aim values blend by aim weight.
    public sealed class GunRecoil
    {
        const float SprayNoiseStep = 0.347f;

        readonly RecoilSettings _settings;
        readonly RecoilValues _blend = new();
        readonly RandomStream _random;
        readonly float _instanceNoise;

        Vector3 _gunTarget;
        Vector3 _gun;
        Vector3 _viewTarget;
        Vector3 _view;
        Vector3 _shakeTarget;
        Vector3 _shake;
        float _kickTarget;
        float _kick;
        float _fov;
        // Kept aim kick not yet turned into the look (x = pitch, y = yaw).
        Vector2 _keptLeft;

        int _shotIndex;
        float _sinceShot = float.MaxValue;
        float _sprayNoise;
        float _sprayPhase;
        float _sprayDrift;
        float _sprayClimb;

        // `random` rolls each spray's pattern (a seeded stream, so tests can fix it).
        public GunRecoil(RecoilSettings settings, RandomStream random)
        {
            _random = random;
            _instanceNoise = random.Range(0f, 100f);
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
        // Field of view multiplier from the shots (1 = none, above 1 = wider).
        public float FovScale => 1f + _fov;
        // This frame's share of the kept kick, for the look to add: x = pitch (negative = up), y = yaw.
        public Vector2 LookTurn { get; private set; }

        // `aimWeight`: 0 hip .. 1 aiming down sights. `stateMultiplier`: extra kick from moving, being airborne or tired.
        // `verticalScale` / `horizontalScale`: the gun's parts (GunStats), on the up and sideways kick of view and model.
        public void Kick(float aimWeight, float stateMultiplier, float verticalScale = 1f, float horizontalScale = 1f)
        {
            RecoilValues values = Values(aimWeight);

            if (_sinceShot > _settings.SprayResetDelay)
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
            float pitch = Mathf.Max(0.01f, values.Vertical * pitchScale + pitchNoise * values.NoisePitch) * verticalScale;
            float yaw = (yawPattern * values.Horizontal + yawNoise * values.NoiseYaw) * horizontalScale;

            float kept = Mathf.Clamp01(_settings.KeptShare);
            _viewTarget += new Vector3(-pitch * (1f - kept), yaw * (1f - kept), rollPattern * values.Roll) * multiplier;
            _keptLeft += new Vector2(-pitch, yaw) * (kept * multiplier);
            _shakeTarget += new Vector3(yawNoise, pitchNoise + shakeNoise * 0.35f, 0f) * (values.CameraShake * stateMultiplier);

            _gunTarget += new Vector3(-values.GunPitch * pitchScale * verticalScale, values.GunYaw * yawPattern * horizontalScale,
                values.GunRoll * rollPattern) * multiplier;
            _kickTarget += values.KickBack * multiplier;
            _fov = Mathf.Min(_fov + _settings.FovKick * stateMultiplier, Mathf.Max(_settings.FovKick, _settings.FovKickMax));
        }

        public void Tick(float deltaTime, float aimWeight)
        {
            LookTurn = default;
            if (deltaTime <= 0f)
                return;
            _sinceShot += deltaTime;

            RecoilValues values = Values(aimWeight);
            float verticalReturn = Damping.ByRate(values.VerticalReturnSpeed, deltaTime);
            float horizontalReturn = Damping.ByRate(values.HorizontalReturnSpeed, deltaTime);
            float verticalSnap = Damping.ByRate(values.VerticalSnappiness, deltaTime);
            float horizontalSnap = Damping.ByRate(values.HorizontalSnappiness, deltaTime);

            float gunReturn = Damping.ByRate(values.GunReturnSpeed, deltaTime);
            float gunSnap = Damping.ByRate(values.GunSnappiness, deltaTime);
            _gunTarget = Vector3.Lerp(_gunTarget, Vector3.zero, gunReturn);
            _gun = Vector3.Lerp(_gun, _gunTarget, gunSnap);
            _viewTarget = DampAxes(_viewTarget, Vector3.zero, verticalReturn, horizontalReturn);
            _view = DampAxes(_view, _viewTarget, verticalSnap, horizontalSnap);
            _shakeTarget = Vector3.Lerp(_shakeTarget, Vector3.zero, horizontalReturn);
            _shake = Vector3.Lerp(_shake, _shakeTarget, horizontalSnap);
            _kickTarget = Mathf.Lerp(_kickTarget, 0f, gunReturn);
            _kick = Mathf.Lerp(_kick, _kickTarget, gunSnap);
            _fov = Mathf.Lerp(_fov, 0f, Damping.ByRate(_settings.FovReturnSpeed, deltaTime));

            // The kept part rises at the snappiness rates, like the springing part.
            LookTurn = new Vector2(_keptLeft.x * verticalSnap, _keptLeft.y * horizontalSnap);
            _keptLeft -= LookTurn;
        }

        public void Reset()
        {
            _gunTarget = _gun = _viewTarget = _view = _shakeTarget = _shake = Vector3.zero;
            _kickTarget = _kick = _fov = 0f;
            _keptLeft = LookTurn = Vector2.zero;
            _shotIndex = 0;
            _sinceShot = float.MaxValue;
        }

        RecoilValues Values(float aimWeight)
        {
            float t = Mathf.Clamp01(aimWeight);
            if (t <= 0f)
                return _settings.Hip;
            if (t >= 1f)
                return _settings.Aim;
            RecoilValues.Lerp(_settings.Hip, _settings.Aim, t, _blend);
            return _blend;
        }

        float AccurateShotsMultiplier(int shot) =>
            shot < _settings.AccurateShots ? Mathf.Clamp01(_settings.AccurateShotsMultiplier) : 1f;

        // Smooth climb and side-to-side drift that differ every spray.
        void Pattern(int shotIndex, out float pitchScale, out float yawPattern, out float rollPattern)
        {
            float shot = shotIndex + 1f;
            float pitchNoise = SignedNoise(17.13f + _sprayNoise, shot * 0.23f + _sprayPhase);
            float yawNoise = SignedNoise(41.73f + _sprayNoise, shot * 0.31f - _sprayPhase);
            float extraYawNoise = SignedNoise(83.91f + _sprayNoise, shot * 0.47f + _sprayPhase * 0.5f);

            pitchScale = Mathf.Max(0.1f, 1f + shot * _sprayClimb + pitchNoise * _settings.SprayPitchJitter);
            float yaw = yawNoise * _settings.SprayYawJitter + extraYawNoise * 0.2f + _sprayDrift;
            yawPattern = Mathf.Clamp(yaw, -1f, 1f);
            rollPattern = Mathf.Clamp(yaw * _settings.SprayRollFromYaw, -1f, 1f);
        }

        void BeginSpray()
        {
            _sprayNoise = _random.Range(-1000f, 1000f);
            _sprayPhase = _random.Range(-Mathf.PI, Mathf.PI);
            _sprayDrift = _random.Range(-_settings.SprayDrift, _settings.SprayDrift);
            _sprayClimb = _random.Range(_settings.SprayClimb.x, _settings.SprayClimb.y);
        }

        static float SignedNoise(float x, float y) => Mathf.PerlinNoise(x, y) * 2f - 1f;

        // x (pitch) uses the vertical rate, y and z the horizontal one.
        static Vector3 DampAxes(Vector3 current, Vector3 target, float verticalT, float horizontalT) => new(
            Mathf.Lerp(current.x, target.x, verticalT),
            Mathf.Lerp(current.y, target.y, horizontalT),
            Mathf.Lerp(current.z, target.z, horizontalT));
    }
}
