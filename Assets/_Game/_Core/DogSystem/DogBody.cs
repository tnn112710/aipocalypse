using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // On the dog's model, after the Animator each frame: paws on the ground stay planted while it stands still (a two-bone
    // solve per leg to where and how each paw touched down, a quick step when the pose pulls one too far), the neck and
    // head turn to what it watches (DogLook, through the model's Look rig), and panting goes on top (DogPant: jaw and
    // tongue). The legs are solved here, not in the RigBuilder: the paws must be read as animated, before any IK, and a
    // rig attached to the Animator has already run by LateUpdate.
    [DisallowMultipleComponent]
    public sealed class DogBody : MonoBehaviour
    {
        [Serializable]
        public struct Leg
        {
            [Tooltip("Upper bone, the joint that bends, and the end the paw hangs from")] public Transform Root, Mid, Tip;
            [Tooltip("The bone that touches the ground")] public Transform Contact;
            [Tooltip("Its height above the ground standing, at the model's 1×")] public float Rest;
        }

        // At the model's 1×: a paw within PlantSlack of its rest height plants, one lifted past LiftSlack lets go (the gap
        // keeps a paw near the line from flickering); one pulled MaxSlip from where it was planted steps over (lifted
        // StepLift, in StepTime). Still: slower than StillSpeed and turning slower than StillTurn for StillTime, moving
        // again past twice those.
        const float PlantSlack = 0.025f;
        const float LiftSlack = 0.06f;
        const float StillTime = 0.15f;
        const float MaxSlip = 0.1f;
        const float StepLift = 0.04f;
        const float StepTime = 0.22f;
        const float StillSpeed = 0.15f;
        const float StillTurn = 25f;
        const float PlantBlend = 0.12f;
        const float LookBlend = 0.35f;
        const float LookSmooth = 0.2f;
        const float PantBlend = 0.4f;
        const float PantSoundGap = 3.5f;
        const float PlayerHeadHeight = 1.6f;
        const float ProbeUp = 0.3f, ProbeDown = 0.6f;

        [SerializeField] Leg[] _legs = Array.Empty<Leg>();
        [SerializeField, Tooltip("Neck then head, aimed at the look target")] MultiAimConstraint[] _look = Array.Empty<MultiAimConstraint>();
        [SerializeField, Tooltip("Each look constraint's weight at a full look")] float[] _lookShare = Array.Empty<float>();
        [SerializeField] Transform _lookTarget;
        [SerializeField] Transform _head;
        [SerializeField] Transform _jaw;
        [SerializeField, Tooltip("Root to tip")] Transform[] _tongue = Array.Empty<Transform>();

        enum Plant { Free, Held, Stepping }

        Dog _dog;
        readonly DogPant _pant = new();
        Plant[] _plant;
        Vector3[] _heldAt, _stepFrom;
        Quaternion[] _heldRot, _stepFromRot;
        float[] _stepT, _weight;
        Renderer[] _renderers;
        float _lastYaw;
        float _stillFor;
        float _lookWeight;
        Vector3 _lookAt, _lookVelocity;
        float _pantWeight, _breath, _nextPantSound;

        public void Setup(Leg[] legs, MultiAimConstraint[] look, float[] lookShare, Transform lookTarget, Transform head, Transform jaw,
            Transform[] tongue)
        {
            _legs = legs;
            _look = look;
            _lookShare = lookShare;
            _lookTarget = lookTarget;
            _head = head;
            _jaw = jaw;
            _tongue = tongue;
        }

        void OnEnable()
        {
            _dog = GetComponentInParent<Dog>();
            int count = _legs.Length;
            _plant = new Plant[count];
            _heldAt = new Vector3[count];
            _stepFrom = new Vector3[count];
            _heldRot = new Quaternion[count];
            _stepFromRot = new Quaternion[count];
            _stepT = new float[count];
            _weight = new float[count];
            _renderers = GetComponentsInChildren<Renderer>();
        }

        void LateUpdate()
        {
            DogSettings settings = _dog != null && _dog.Active ? _dog.Settings : null;
            if (settings == null)
                return;
            // Off-screen the Animator stops posing the bones (CullUpdateTransforms): nothing to plant on or add to, and the
            // paws would lock onto last seen positions.
            if (!Seen())
            {
                for (int i = 0; i < _plant.Length; i++)
                {
                    _plant[i] = Plant.Free;
                    _weight[i] = 0f;
                }
                _stillFor = 0f;
                return;
            }
            float dt = Time.deltaTime;
            Paws(settings, dt);
            Look(settings, dt);
            Pant(settings, dt);
        }

        bool Seen()
        {
            foreach (Renderer r in _renderers)
                if (r != null && r.isVisible)
                    return true;
            return false;
        }

        float FlatSpeed
        {
            get
            {
                Vector3 velocity = _dog.Legs.Velocity;
                return new Vector2(velocity.x, velocity.z).magnitude;
            }
        }

        void Paws(DogSettings settings, float dt)
        {
            float scale = transform.lossyScale.y;
            float yaw = transform.eulerAngles.y;
            float turn = dt > 0f ? Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) / dt : 0f;
            _lastYaw = yaw;
            bool allowed = settings.PlantPaws && !_dog.Scrambling && !_dog.InHouse && !_dog.Swimming;
            float speed = FlatSpeed;
            if (!allowed || speed > StillSpeed * 2f || turn > StillTurn * 2f)
                _stillFor = 0f;
            else if (speed < StillSpeed && turn < StillTurn)
                _stillFor += dt;
            bool still = _stillFor >= StillTime;
            for (int i = 0; i < _legs.Length; i++)
            {
                Leg leg = _legs[i];
                Transform tip = leg.Tip;
                Vector3 animated = tip.position;
                Quaternion animatedRot = tip.rotation;
                float height = still ? GroundHeight(leg.Contact.position, settings.PawGround) / scale - leg.Rest : float.MaxValue;
                bool down = height < PlantSlack;
                bool up = height > LiftSlack;
                switch (_plant[i])
                {
                    // Planting while still fading out of the last hold: glide there instead of snapping.
                    case Plant.Free when down && _weight[i] > 0.01f:
                        _plant[i] = Plant.Stepping;
                        _stepFrom[i] = _heldAt[i];
                        _stepFromRot[i] = _heldRot[i];
                        _stepT[i] = 0f;
                        break;
                    case Plant.Free when down:
                        _plant[i] = Plant.Held;
                        _heldAt[i] = animated;
                        _heldRot[i] = animatedRot;
                        break;
                    case Plant.Held when up:
                        _plant[i] = Plant.Free;
                        break;
                    case Plant.Held when (animated - _heldAt[i]).sqrMagnitude > MaxSlip * MaxSlip * scale * scale:
                        _plant[i] = Plant.Stepping;
                        _stepFrom[i] = _heldAt[i];
                        _stepFromRot[i] = _heldRot[i];
                        _stepT[i] = 0f;
                        break;
                    case Plant.Stepping:
                        _stepT[i] += dt / StepTime;
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_stepT[i]));
                        _heldAt[i] = Vector3.Lerp(_stepFrom[i], animated, k) + Vector3.up * (StepLift * scale * Mathf.Sin(k * Mathf.PI));
                        _heldRot[i] = Quaternion.Slerp(_stepFromRot[i], animatedRot, k);
                        if (_stepT[i] >= 1f)
                        {
                            _plant[i] = up ? Plant.Free : Plant.Held;
                            _heldAt[i] = animated;
                            _heldRot[i] = animatedRot;
                        }
                        break;
                }
                if (!still)
                    _plant[i] = Plant.Free;
                _weight[i] = Mathf.MoveTowards(_weight[i], _plant[i] == Plant.Free ? 0f : 1f, dt / PlantBlend);
                if (_weight[i] > 0f)
                    Solve(leg, _heldAt[i], _heldRot[i], _weight[i]);
            }
        }

        // Two-bone IK: bends the middle joint to the reach the target needs, in the plane the leg already bends in, turns the
        // upper bone to point the end at it, then the end bone to the target's rotation. `weight` blends from the animation.
        static void Solve(Leg leg, Vector3 target, Quaternion rotation, float weight)
        {
            Vector3 a = leg.Root.position, b = leg.Mid.position, c = leg.Tip.position;
            Vector3 goal = Vector3.Lerp(c, target, weight);
            float upper = (b - a).magnitude, lower = (c - b).magnitude;
            float reach = Mathf.Clamp((goal - a).magnitude, Mathf.Abs(upper - lower) + 1e-4f, upper + lower - 1e-4f);
            float bend = Vector3.Angle(a - b, c - b);
            float wanted = Mathf.Acos(Mathf.Clamp((upper * upper + lower * lower - reach * reach) / (2f * upper * lower), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 axis = Vector3.Cross(a - b, c - b);
            if (axis.sqrMagnitude > 1e-10f)
            {
                Quaternion turn = Quaternion.AngleAxis(wanted - bend, axis);
                if (Mathf.Abs(Vector3.Angle(a - b, turn * (c - b)) - wanted) > 0.5f)
                    turn = Quaternion.AngleAxis(bend - wanted, axis);
                leg.Mid.rotation = turn * leg.Mid.rotation;
            }
            leg.Root.rotation = Quaternion.FromToRotation(leg.Tip.position - a, goal - a) * leg.Root.rotation;
            leg.Tip.rotation = Quaternion.Slerp(leg.Tip.rotation, rotation, weight);
        }

        // The contact's height above the ground under it (far above when there's none within reach).
        static float GroundHeight(Vector3 contact, LayerMask ground) =>
            Physics.Raycast(contact + Vector3.up * ProbeUp, Vector3.down, out RaycastHit hit, ProbeUp + ProbeDown, ground, QueryTriggerInteraction.Ignore)
                ? contact.y - hit.point.y
                : float.MaxValue;

        void Look(DogSettings settings, float dt)
        {
            if (_lookTarget == null || _head == null)
                return;
            DogRoster roster = _dog.Roster;
            DogManager manager = _dog.Manager;
            bool calm = _dog.Standing || _dog.Pose == DogPose.Sit || _dog.Pose == DogPose.Lie;
            Vector3 point = default;
            bool want = calm && !_dog.Scrambling && !_dog.InHouse && !_dog.State.Down
                && DogLook.Pick(_head.position, transform.forward, manager.TargetId, manager.Foes, roster.HasPlayer,
                    roster.PlayerPosition + Vector3.up * PlayerHeadHeight, FlatSpeed, out point, settings.HeadLookRange, settings.LookCalmSpeed,
                    settings.LookMaxAngle);
            if (want)
                _lookAt = _lookWeight <= 0f ? point : Vector3.SmoothDamp(_lookAt, point, ref _lookVelocity, LookSmooth, Mathf.Infinity, dt);
            _lookWeight = Mathf.MoveTowards(_lookWeight, want ? settings.LookWeight : 0f, dt / LookBlend);
            _lookTarget.position = _lookAt;
            for (int i = 0; i < _look.Length; i++)
                _look[i].weight = _lookWeight * (i < _lookShare.Length ? _lookShare[i] : 1f);
        }

        // Jaw open on a quick rhythm, the tongue drooping out further; added on top of whatever the clip does.
        void Pant(DogSettings settings, float dt)
        {
            _pant.Tick(FlatSpeed, Time.time, dt, settings.PantRunSpeed, settings.PantAfter, settings.PantFor);
            // Not in the fighting stance: its growl works the jaw already.
            bool free = (_dog.Standing || _dog.Pose == DogPose.Sit || _dog.Pose == DogPose.Lie) && !_dog.Swimming && !_dog.State.Down
                && !_dog.CombatStance;
            _pantWeight = Mathf.MoveTowards(_pantWeight, free ? _pant.Level : 0f, dt / PantBlend);
            if (_pantWeight <= 0f || _jaw == null)
                return;
            // Heard too while it's well into it (the voice gap keeps it from crowding barks).
            if (_pantWeight > 0.5f && Time.time >= _nextPantSound)
            {
                _nextPantSound = Time.time + PantSoundGap;
                _dog.Manager.TryPlay(_dog, DogSound.Pant);
            }
            _breath += dt * settings.PantRate;
            float wave = 0.5f + 0.5f * Mathf.Sin(_breath * 2f * Mathf.PI);
            float open = settings.PantJaw * _pantWeight * (0.65f + 0.35f * wave);
            Vector3 side = transform.right;
            _jaw.rotation = Quaternion.AngleAxis(open, side) * _jaw.rotation;
            for (int i = 0; i < _tongue.Length; i++)
                _tongue[i].rotation = Quaternion.AngleAxis(open * (0.5f + 0.4f * i), side) * _tongue[i].rotation;
        }
    }
}
