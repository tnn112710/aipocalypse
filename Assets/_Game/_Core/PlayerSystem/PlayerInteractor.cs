using Aipocalypse.Core.Interaction;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // What the player is looking at that can be used, and using it. A short sphere cast from the eye each frame;
    // walls block it, trigger colliders (pickups) are hit.
    public sealed class PlayerInteractor
    {
        readonly Transform _eye;
        readonly GameObject _actor;
        readonly PlayerInteractionTuning _tuning;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        Collider _focusCollider;
        IInteractable _focus;

        public PlayerInteractor(Transform eye, GameObject actor, PlayerInteractionTuning tuning)
        {
            _eye = eye;
            _actor = actor;
            _tuning = tuning;
        }

        public bool HasPrompt { get; private set; }
        public InteractionPrompt Prompt { get; private set; }

        public void Tick(bool interactPressed)
        {
            Refocus();

            InteractionPrompt prompt = default;
            HasPrompt = _focus != null && _focus.TryGetPrompt(_actor, out prompt);
            Prompt = prompt;
            if (!HasPrompt || !interactPressed)
                return;

            _focus.Interact(_actor);
            // The target may have been consumed; look again next frame.
            _focusCollider = null;
            _focus = null;
            HasPrompt = false;
        }

        public void Clear()
        {
            _focusCollider = null;
            _focus = null;
            HasPrompt = false;
        }

        void Refocus()
        {
            Collider nearest = FindNearestHit();
            if (nearest == _focusCollider)
                return;
            _focusCollider = nearest;
            _focus = nearest != null ? nearest.GetComponentInParent<IInteractable>() : null;
        }

        // Ground in front of the player, short of any wall, for dropping an item.
        public Vector3 FindDropPoint()
        {
            Vector3 forward = Vector3.ProjectOnPlane(_eye.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = _actor.transform.forward;
            forward.Normalize();

            float wallGap = _tuning.DropWallGap;
            float distance = _tuning.DropDistance;
            if (Physics.Raycast(_eye.position, forward, out RaycastHit wall, distance + wallGap, _tuning.InteractMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0f, wall.distance - wallGap);

            Vector3 point = _eye.position + forward * distance;
            return Physics.Raycast(point, Vector3.down, out RaycastHit ground, _tuning.DropGroundProbe, _tuning.InteractMask, QueryTriggerInteraction.Ignore)
                ? ground.point
                : _actor.transform.position + forward * distance;
        }

        // A sphere cast also reports colliders it starts inside (the player's own capsule), so those are skipped.
        Collider FindNearestHit()
        {
            Ray ray = new(_eye.position, _eye.forward);
            int count = Physics.SphereCastNonAlloc(ray, _tuning.InteractRadius, _hits, _tuning.InteractRange, _tuning.InteractMask, QueryTriggerInteraction.Collide);
            return PhysicsQueries.Nearest(_hits, count, _actor.transform, out RaycastHit nearest) ? nearest.collider : null;
        }
    }
}
