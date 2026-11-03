using System;
using Aipocalypse.Core.Ghosts;
using DG.Tweening;
using UnityEngine;

namespace Aipocalypse.Core.DefenseSystem
{
    // A barrier's model and how it shows damage: it leans further at each damage stage, away from the blows, shakes when hit,
    // and falls over when broken. Of several model variants one is shown, picked (and maybe turned round) from where it
    // stands, so a fence line doesn't repeat. A gate's model also names the hinges its leaves swing round (one, or two for a
    // double gate). Every pivot (a panel, or each leaf) leans and falls the same way.
    public sealed class BarrierLook : MonoBehaviour
    {
        [Serializable]
        public struct Stage
        {
            [Range(0f, 1f), Tooltip("Applies below this share of health")] public float Below;
            [Tooltip("Degrees it leans")] public float Lean;
        }

        [SerializeField, Tooltip("At the base, local X along the barrier: lean and fall round their X axis (a panel, or each leaf)")]
        Transform[] _pivots = Array.Empty<Transform>();
        [SerializeField, Tooltip("Children of the pivots, punched on each blow")] Transform[] _shakes = Array.Empty<Transform>();
        [SerializeField] Stage[] _stages = { new() { Below = 0.67f, Lean = 4f }, new() { Below = 0.34f, Lean = 10f } };
        [SerializeField, Tooltip("Degrees it lies at when broken")] float _fallenAngle = 82f;
        [SerializeField, Tooltip("Metres it sinks when broken (so it lies on the ground, not on its edge)")] float _fallenDrop = 0.08f;
        [SerializeField, Min(0.01f)] float _moveTime = 0.35f;
        [SerializeField, Tooltip("Degrees round X, Y, Z of the shake on a blow")] Vector3 _punch = new(4f, 0f, 2f);
        [SerializeField, Min(0.01f)] float _punchTime = 0.3f;
        [SerializeField, Tooltip("Colliders turned off while it lies broken (so nothing snags on the fallen boards)")] Collider[] _solid = Array.Empty<Collider>();
        [SerializeField, Tooltip("Gates only: what each leaf swings round. A hinge left of centre has its leaf along +X, right of it along -X")]
        Transform[] _hinges = Array.Empty<Transform>();
        [SerializeField, Tooltip("Optional: models of which one is shown, picked by position")] GameObject[] _variants = Array.Empty<GameObject>();
        [SerializeField, Tooltip("The picked variant may also be turned 180° round Y")] bool _flipVariants = true;

        Quaternion[] _baseRotations;
        Vector3[] _basePositions;
        Tween[] _moves;
        Tween[] _drops;
        Tween[] _punchTweens;
        float _side = 1f;

        public Transform[] Hinges => _hinges;

        void Awake()
        {
            Cache();
            PickVariant();
        }

        void OnDestroy()
        {
            if (_moves == null)
                return;
            for (int i = 0; i < _moves.Length; i++)
            {
                _moves[i]?.Kill();
                _drops[i]?.Kill();
            }
            foreach (Tween punch in _punchTweens)
                punch?.Kill();
        }

        // Which side blows come from (world direction of the push): it leans and falls that way.
        public void PushedToward(Vector3 direction)
        {
            if (_pivots.Length == 0 || _pivots[0] == null)
                return;
            float along = Vector3.Dot(direction, _pivots[0].forward);
            if (Mathf.Abs(along) > 0.01f)
                _side = Mathf.Sign(along);
        }

        public void Show(float share, bool broken, bool instant = false)
        {
            Cache();
            float angle = broken ? _fallenAngle : LeanFor(share);
            foreach (Collider solid in _solid)
                if (solid != null)
                    solid.enabled = !broken;

            for (int i = 0; i < _pivots.Length; i++)
            {
                Transform pivot = _pivots[i];
                if (pivot == null)
                    continue;
                Quaternion rotation = _baseRotations[i] * Quaternion.Euler(angle * _side, 0f, 0f);
                Vector3 position = _basePositions[i] + (broken ? Vector3.down * _fallenDrop : Vector3.zero);
                _moves[i]?.Kill();
                _drops[i]?.Kill();
                if (instant)
                {
                    pivot.SetLocalPositionAndRotation(position, rotation);
                    continue;
                }
                _moves[i] = pivot.DOLocalRotateQuaternion(rotation, _moveTime).SetEase(broken ? Ease.InQuad : Ease.OutBack).SetLink(gameObject);
                _drops[i] = pivot.DOLocalMove(position, _moveTime).SetLink(gameObject);
            }
        }

        public void Punch()
        {
            Cache();
            for (int i = 0; i < _shakes.Length; i++)
            {
                if (_shakes[i] == null)
                    continue;
                _punchTweens[i]?.Complete();
                _punchTweens[i] = _shakes[i].DOPunchRotation(_punch, _punchTime, 8, 0.6f).SetLink(gameObject);
            }
        }

        // Also run by builders, so the scene shows the variant play will.
        public void PickVariant()
        {
            if (_variants.Length == 0)
                return;
            Vector3 position = transform.position;
            int hash = (Mathf.RoundToInt(position.x * 7.31f) * 73856093 ^ Mathf.RoundToInt(position.z * 5.17f) * 19349663) & int.MaxValue;
            int pick = hash % _variants.Length;
            for (int i = 0; i < _variants.Length; i++)
            {
                if (_variants[i] == null)
                    continue;
                _variants[i].SetActive(i == pick);
                if (i == pick && _flipVariants)
                    _variants[i].transform.localRotation = Quaternion.Euler(0f, (hash / _variants.Length) % 2 == 0 ? 0f : 180f, 0f);
            }
        }

        // Its model standing whole (pivots at rest, the shown variant only), for the ghost of a broken barrier.
        public GhostShape CaptureStanding()
        {
            Cache();
            return GhostShape.Capture(transform, RestLocal);
        }

        Matrix4x4? RestLocal(Transform t)
        {
            int i = Array.IndexOf(_pivots, t);
            return i >= 0 ? Matrix4x4.TRS(_basePositions[i], _baseRotations[i], t.localScale) : null;
        }

        float LeanFor(float share)
        {
            float lean = 0f;
            foreach (Stage stage in _stages)
                if (share < stage.Below)
                    lean = Mathf.Max(lean, stage.Lean);
            return lean;
        }

        void Cache()
        {
            if (_baseRotations != null)
                return;
            _baseRotations = new Quaternion[_pivots.Length];
            _basePositions = new Vector3[_pivots.Length];
            _moves = new Tween[_pivots.Length];
            _drops = new Tween[_pivots.Length];
            _punchTweens = new Tween[_shakes.Length];
            for (int i = 0; i < _pivots.Length; i++)
            {
                if (_pivots[i] == null)
                    continue;
                _baseRotations[i] = _pivots[i].localRotation;
                _basePositions[i] = _pivots[i].localPosition;
            }
        }
    }
}
