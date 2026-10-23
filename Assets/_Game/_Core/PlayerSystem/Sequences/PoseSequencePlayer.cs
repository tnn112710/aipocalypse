using System;
using Aipocalypse.Core.AudioSystem;
using Ami.BroAudio;
using DG.Tweening;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // Plays a PoseSequence on the player's camera: moves the camera root through the beats (the look pitch is folded into
    // it), then holds the last pose with free look and drift until Stop; the handheld wander runs throughout. Beats are
    // placed once, at Play.
    public sealed class PoseSequencePlayer
    {
        const float GroundProbeUp = 1.5f;
        const float GroundProbeDown = 4f;
        const float SettleFadeIn = 2f;
        const float HandheldFadeIn = 0.25f;
        // Position xyz, euler xyz, field of view: what a pose interpolates.
        const int Channels = 7;

        readonly Transform _body;
        readonly Transform _root;
        readonly Transform _pitchPivot;
        readonly AudioService _audio;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        PoseSequence _sequence;
        Segment[] _segments = Array.Empty<Segment>();
        int _segmentCount;
        // Continuous path: one key per beat arrival (plus the start), monotone cubic tangents per channel.
        float[] _keyTimes = Array.Empty<float>();
        float[] _keyValues = Array.Empty<float>();
        float[] _keyTangents = Array.Empty<float>();
        int _keyCount;
        Pose _settled;
        float _settleTime;
        float _driftStart;
        float _musicTime;
        float _time;
        // Cues up to here have played. Starts below 0 so cues at 0 play once, even if the first ticks don't advance time.
        float _cuedTo;
        float _handheldSeed;
        Vector2 _look;
        bool _ended;
        bool _paused;
        IAudioPlayer _music;

        struct Pose
        {
            public Vector3 Position;
            public Vector3 Euler;
            public float Fov;
        }

        // A move (or a hold: From = To) and its cues.
        struct Segment
        {
            public Pose From;
            public Pose To;
            public float Start;
            public float Duration;
            public Ease Ease;
            public SoundID StartSound;
            public SoundID EndSound;
        }

        // Raised once, the sequence's EndAfter seconds after settling.
        public event Action<PoseSequence> Ended;

        public PoseSequence Current => _sequence;
        public bool IsPlaying => _sequence != null;
        public bool IsSettled => IsPlaying && _time >= _settleTime;
        // Field of view multiplier for the world camera (1 = normal).
        public float FovScale { get; private set; } = 1f;

        public PoseSequencePlayer(Transform body, Transform cameraRoot, Transform cameraPitch, AudioService audio)
        {
            _body = body;
            _root = cameraRoot;
            _pitchPivot = cameraPitch;
            _audio = audio;
        }

        // `pitch` is where the player was looking (degrees, positive = down). `anchor` is needed for PoseSpace.Anchor.
        public void Play(PoseSequence sequence, float pitch, Transform anchor = null)
        {
            Stop();
            if (sequence == null)
                return;
            if (sequence.Space == PoseSpace.Anchor && anchor == null)
            {
                Debug.LogError($"{sequence.name} is placed on an anchor, but none was given.", sequence);
                return;
            }

            _sequence = sequence;
            _time = 0f;
            _cuedTo = -1f;
            _look = Vector2.zero;
            _ended = false;
            _musicTime = float.MaxValue;
            _handheldSeed = UnityEngine.Random.value * 100f;

            PoseBeat[] beats = sequence.Beats;
            if (_segments.Length < beats.Length * 2)
                _segments = new Segment[beats.Length * 2];
            if (_keyTimes.Length < beats.Length + 1)
            {
                _keyTimes = new float[beats.Length + 1];
                _keyValues = new float[(beats.Length + 1) * Channels];
                _keyTangents = new float[(beats.Length + 1) * Channels];
            }
            _segmentCount = 0;
            _keyCount = 0;

            var pose = new Pose { Position = _root.localPosition, Euler = new Vector3(pitch, 0f, 0f), Fov = 1f };
            AddKey(0f, pose);
            float time = 0f;
            _driftStart = 0f;
            for (int i = 0; i < beats.Length; i++)
            {
                PoseBeat beat = beats[i];
                Pose next = sequence.Space == PoseSpace.Anchor ? PlaceOnAnchor(beat, anchor) : PlaceOnGround(beat, pose);
                if (i == sequence.MusicBeat)
                    _musicTime = time + sequence.MusicDelay;
                if (i == beats.Length - 1)
                    _driftStart = time;
                Add(pose, next, beat.Duration, beat.Ease, beat.StartSound, beat.ArriveSound, ref time);
                AddKey(time, next);
                if (beat.Hold > 0f)
                    Add(next, next, beat.Hold, Ease.Linear, default, default, ref time);
                pose = next;
            }
            _settled = pose;
            _settleTime = time;
            // The path ends on the last arrival; a trailing hold just waits there.
            if (_keyCount > 1)
                SetTangents();
            Apply(Evaluate(0f));
        }

        // `lookDegrees` turns the view inside the sequence's LookRange once settled (mouse x = yaw, y = pitch up).
        public void Tick(float deltaTime, Vector2 lookDegrees)
        {
            if (_sequence == null)
                return;

            _time += deltaTime;
            PlayCues(_cuedTo, _time);
            _cuedTo = _time;

            if (_time < _settleTime)
            {
                Apply(Evaluate(_time));
                return;
            }

            Vector2 range = _sequence.LookRange;
            _look.x = Mathf.Clamp(_look.x - lookDegrees.y, -range.x, range.x);
            _look.y = Mathf.Clamp(_look.y + lookDegrees.x, -range.y, range.y);
            Pose pose = _settled;
            pose.Euler.x += _look.x;
            pose.Euler.y += _look.y;
            Apply(pose);

            float settledFor = _time - _settleTime;
            if (!_ended && _sequence.EndAfter >= 0f && settledFor >= _sequence.EndAfter)
            {
                _ended = true;
                Ended?.Invoke(_sequence);
            }
        }

        // Pauses the music while time is stopped.
        public void SetPaused(bool paused)
        {
            if (paused == _paused)
                return;
            _paused = paused;
            if (paused)
                _audio?.Pause(_music);
            else
                _audio?.UnPause(_music);
        }

        // Ends the sequence; the caller gives the camera back (look pitch, head bob).
        public void Stop()
        {
            if (_music != null)
                _audio?.Stop(_music, _sequence != null ? _sequence.MusicFadeOut : 0f);
            _music = null;
            _sequence = null;
            FovScale = 1f;
        }

        void Add(in Pose from, in Pose to, float duration, Ease ease, SoundID startSound, SoundID endSound, ref float time)
        {
            _segments[_segmentCount++] = new Segment
            {
                From = from,
                To = to,
                Start = time,
                Duration = Mathf.Max(0f, duration),
                Ease = ease,
                StartSound = startSound,
                EndSound = endSound,
            };
            time += Mathf.Max(0f, duration);
        }

        void AddKey(float time, in Pose pose)
        {
            // A zero-length move lands on the previous key's time: it replaces that key.
            if (_keyCount > 0 && time <= _keyTimes[_keyCount - 1])
                _keyCount--;
            _keyTimes[_keyCount] = time;
            int at = _keyCount * Channels;
            _keyValues[at] = pose.Position.x;
            _keyValues[at + 1] = pose.Position.y;
            _keyValues[at + 2] = pose.Position.z;
            _keyValues[at + 3] = pose.Euler.x;
            _keyValues[at + 4] = pose.Euler.y;
            _keyValues[at + 5] = pose.Euler.z;
            _keyValues[at + 6] = pose.Fov;
            _keyCount++;
        }

        // Monotone cubic (Fritsch–Butland) tangents: the path never overshoots a beat, so the eye can't dip into the
        // ground between two low beats. It starts and ends at rest.
        void SetTangents()
        {
            for (int c = 0; c < Channels; c++)
            {
                _keyTangents[c] = 0f;
                _keyTangents[(_keyCount - 1) * Channels + c] = 0f;
                for (int k = 1; k < _keyCount - 1; k++)
                {
                    float h0 = _keyTimes[k] - _keyTimes[k - 1];
                    float h1 = _keyTimes[k + 1] - _keyTimes[k];
                    float d0 = (_keyValues[k * Channels + c] - _keyValues[(k - 1) * Channels + c]) / h0;
                    float d1 = (_keyValues[(k + 1) * Channels + c] - _keyValues[k * Channels + c]) / h1;
                    _keyTangents[k * Channels + c] = d0 * d1 <= 0f ? 0f : 3f * (h0 + h1) / ((2f * h1 + h0) / d0 + (h1 + 2f * h0) / d1);
                }
            }
        }

        Pose Evaluate(float time) => _sequence.Continuous ? EvaluatePath(time) : EvaluateSegments(time);

        Pose EvaluateSegments(float time)
        {
            for (int i = 0; i < _segmentCount; i++)
            {
                Segment segment = _segments[i];
                if (time >= segment.Start + segment.Duration)
                    continue;
                float eased = DOVirtual.EasedValue(0f, 1f, (time - segment.Start) / segment.Duration, segment.Ease);
                return new Pose
                {
                    Position = Vector3.LerpUnclamped(segment.From.Position, segment.To.Position, eased),
                    Euler = Vector3.LerpUnclamped(segment.From.Euler, segment.To.Euler, eased),
                    Fov = Mathf.LerpUnclamped(segment.From.Fov, segment.To.Fov, eased),
                };
            }
            return _settled;
        }

        Pose EvaluatePath(float time)
        {
            if (_keyCount < 2 || time >= _keyTimes[_keyCount - 1])
                return _settled;
            int k = 0;
            while (k < _keyCount - 2 && time >= _keyTimes[k + 1])
                k++;

            float h = _keyTimes[k + 1] - _keyTimes[k];
            float s = Mathf.Clamp01((time - _keyTimes[k]) / h);
            float s2 = s * s;
            float s3 = s2 * s;
            float h00 = 2f * s3 - 3f * s2 + 1f;
            float h10 = s3 - 2f * s2 + s;
            float h01 = -2f * s3 + 3f * s2;
            float h11 = s3 - s2;

            int a = k * Channels;
            int b = a + Channels;
            float Channel(int c) => h00 * _keyValues[a + c] + h10 * h * _keyTangents[a + c] + h01 * _keyValues[b + c] + h11 * h * _keyTangents[b + c];
            return new Pose
            {
                Position = new Vector3(Channel(0), Channel(1), Channel(2)),
                Euler = new Vector3(Channel(3), Channel(4), Channel(5)),
                Fov = Channel(6),
            };
        }

        void PlayCues(float from, float to)
        {
            if (_audio == null)
                return;
            for (int i = 0; i < _segmentCount; i++)
            {
                Segment segment = _segments[i];
                if (Crossed(segment.Start, from, to))
                    _audio.Play(segment.StartSound);
                if (Crossed(segment.Start + segment.Duration, from, to))
                    _audio.Play(segment.EndSound);
            }
            if (Crossed(_musicTime, from, to))
                _music = _audio.Start(_sequence.Music, 1f, _sequence.MusicFadeIn);
        }

        static bool Crossed(float time, float from, float to) => from < time && to >= time;

        void Apply(Pose pose)
        {
            AddDrift(ref pose);
            AddHandheld(ref pose);
            _root.localPosition = pose.Position;
            _root.localRotation = Quaternion.Euler(pose.Euler);
            _pitchPivot.localRotation = Quaternion.identity;
            FovScale = pose.Fov;
        }

        // An ellipse (pitch sin, yaw cos − 1, so it starts at zero offset): pitch and yaw never rest at the same time, so the
        // view keeps moving. Fades in from the start of the last beat's move, overlapping its arrival.
        void AddDrift(ref Pose pose)
        {
            Vector2 amplitude = _sequence.DriftAmplitude;
            float drifting = _time - _driftStart;
            if (amplitude == Vector2.zero || drifting <= 0f)
                return;
            float phase = drifting / _sequence.DriftPeriod * 2f * Mathf.PI;
            float weight = Mathf.SmoothStep(0f, 1f, drifting / SettleFadeIn);
            pose.Euler.x += Mathf.Sin(phase) * amplitude.x * weight;
            pose.Euler.y += (Mathf.Cos(phase) - 1f) * amplitude.y * weight;
        }

        // Perlin wander per channel, from a fresh seed each play so no two deaths look the same.
        void AddHandheld(ref Pose pose)
        {
            Vector3 angles = _sequence.HandheldAngles;
            float offset = _sequence.HandheldOffset;
            if (angles == Vector3.zero && offset <= 0f)
                return;
            float t = _time * _sequence.HandheldSpeed;
            float weight = Mathf.Clamp01(_time / HandheldFadeIn);
            pose.Euler.x += Noise(0, t) * angles.x * weight;
            pose.Euler.y += Noise(1, t) * angles.y * weight;
            pose.Euler.z += Noise(2, t) * angles.z * weight;
            pose.Position.x += Noise(3, t) * offset * weight;
            pose.Position.y += Noise(4, t) * offset * weight;
            pose.Position.z += Noise(5, t) * offset * weight;
        }

        // -1..1
        float Noise(int channel, float t) => (Mathf.PerlinNoise(_handheldSeed + channel * 17.3f, t) - 0.5f) * 2f;

        static float Fov(PoseBeat beat) => beat.FovScale > 0f ? beat.FovScale : 1f;

        // Exact, in the anchor's space; the anchor's facing turns the yaw.
        Pose PlaceOnAnchor(PoseBeat beat, Transform anchor)
        {
            Vector3 world = anchor.TransformPoint(new Vector3(beat.Offset.x, beat.Height, beat.Offset.y));
            Vector3 euler = beat.Euler;
            euler.y += Mathf.DeltaAngle(_body.eulerAngles.y, anchor.eulerAngles.y);
            return new Pose { Position = _body.InverseTransformPoint(world), Euler = euler, Fov = Fov(beat) };
        }

        // On the ground under the beat's offset (the feet's level when none is found), pulled back from walls between the
        // previous eye position and this one.
        Pose PlaceOnGround(PoseBeat beat, in Pose previous)
        {
            Vector3 local = new(beat.Offset.x, 0f, beat.Offset.y);
            local.y = GroundHeight(local) + beat.Height;

            Vector3 from = _body.TransformPoint(previous.Position);
            Vector3 path = _body.TransformPoint(local) - from;
            float distance = path.magnitude;
            if (distance > 0.001f)
            {
                int count = Physics.SphereCastNonAlloc(from, _sequence.HeadRadius, path / distance, _hits, distance, _sequence.GroundMask,
                    QueryTriggerInteraction.Ignore);
                // Casts that start inside a collider report distance 0; the player's own capsule is skipped.
                if (PhysicsQueries.Nearest(_hits, count, _body, out RaycastHit wall) && wall.distance > 0f)
                    local = _body.InverseTransformPoint(from + path / distance * wall.distance);
            }
            return new Pose { Position = local, Euler = beat.Euler, Fov = Fov(beat) };
        }

        float GroundHeight(Vector3 local)
        {
            Vector3 origin = _body.TransformPoint(local + Vector3.up * GroundProbeUp);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, GroundProbeUp + GroundProbeDown, _sequence.GroundMask,
                QueryTriggerInteraction.Ignore);
            return PhysicsQueries.Nearest(_hits, count, _body, out RaycastHit ground) ? _body.InverseTransformPoint(ground.point).y : 0f;
        }
    }
}
