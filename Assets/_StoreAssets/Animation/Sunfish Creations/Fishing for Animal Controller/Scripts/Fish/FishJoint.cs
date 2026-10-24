using System;
using System.Collections;
using UnityEngine;

namespace SunfishCreations
{
    public class FishJoint : MonoBehaviour
    {
        public Vector3 spawnOffset;

        public ConfigurableJoint joint;

        [Tooltip("Local offset applied to this joint's own transform, relative to Fish Holder (its parent once attached to the hand). Applied continuously while a fish is attached, so it is safe to tune live in Play Mode. Do not write to Fish Holder's transform from here — it is driven every frame by its HumanoidParent component.")]
        public Vector3 holderOffset;

        [Tooltip("Seconds to ease the joint into Holder Offset when the fish is transferred to the hand. 0 applies it instantly.")]
        [Min(0f)]
        public float holderOffsetBlendDuration = 0.25f;

        [Tooltip("Easing used while the joint moves into Holder Offset.")]
        public AnimationCurve holderOffsetBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private GameObject currentActiveFish;
        private FishData activeFishData;

        public bool HasActiveFish => currentActiveFish != null;
        public bool IsAttachedToHolder => holderTransform != null;
        public FishData ActiveFishData => activeFishData;
        public Transform HolderTransform => holderTransform;

        private Collider fishCollider;
        private Rigidbody activeFishRigidbody;
        private Vector3 heldFishLocalPosition;
        private Quaternion heldFishLocalRotation;
        private bool fishBodyFollowsHolder;
        private Vector3 localPos;

        private Transform holderTransform;
        private Coroutine holderOffsetBlendRoutine;
        private Vector3 fishDataHolderOffset;

        private Vector3 EffectiveHolderOffset => holderOffset + fishDataHolderOffset;

        private void LateUpdate()
        {
            // Keep enforcing Holder Offset and a fixed hold rotation every frame so it can be
            // tuned live in the Inspector, and the fish's orientation is identical every catch.
            // Skipped while a blend is in-flight so it doesn't fight the easing coroutine.
            if (holderTransform != null)
            {
                if (holderOffsetBlendRoutine == null)
                {
                    transform.localPosition = EffectiveHolderOffset;
                    transform.localRotation = Quaternion.identity;
                }

                FollowFishBodyToHolder();
            }
        }

        public virtual void SpawnFish(GameObject fishPrefab)
        {
            activeFishData = null;
            SpawnFishInternal(fishPrefab, Vector3.zero);
        }

        /// <summary>
        /// Spawns the fish defined by its data and stores its holder-specific offset.
        /// </summary>
        public virtual void SpawnFish(FishData fishData)
        {
            if (fishData == null)
            {
                throw new ArgumentNullException(nameof(fishData));
            }

            activeFishData = fishData;
            SpawnFishInternal(fishData.FishPrefab, fishData.holderOffset);
        }

        /// <summary>
        /// Refreshes the cached fish-data offset after the active FishData asset has been edited.
        /// </summary>
        public void RefreshFishDataHolderOffset()
        {
            if (activeFishData == null)
            {
                return;
            }

            fishDataHolderOffset = activeFishData.holderOffset;
            if (holderTransform != null && holderOffsetBlendRoutine == null)
            {
                transform.localPosition = EffectiveHolderOffset;
            }
        }

        /// <summary>
        /// Instantly moves the live fish Rigidbody to match the current Holder Offset, instead of
        /// waiting for the (unpowered, Limited-motion) ConfigurableJoint to eventually drift there.
        /// Call right after changing holderOffset/fishData.holderOffset so the fish never appears to
        /// teleport or lag behind the newly applied offset.
        /// </summary>
        public void SnapFishToHolderOffset()
        {
            if (holderTransform == null || currentActiveFish == null)
            {
                return;
            }

            FinishHolderOffsetBlend();

            transform.localPosition = EffectiveHolderOffset;
            transform.localRotation = Quaternion.identity;

            if (activeFishRigidbody == null)
            {
                return;
            }

            Vector3 targetPosition;
            Quaternion targetRotation;

            if (fishBodyFollowsHolder)
            {
                // Respect the store-animation lock so the snap doesn't fight it.
                targetPosition = transform.TransformPoint(heldFishLocalPosition);
                targetRotation = transform.rotation * heldFishLocalRotation;
            }
            else
            {
                // Resolve the Rigidbody position that satisfies joint.anchor/connectedAnchor exactly,
                // rather than assuming connectedAnchor is zero (it commonly isn't).
                Vector3 worldAnchor = joint != null ? transform.TransformPoint(joint.anchor) : transform.position;
                Vector3 connectedAnchor = joint != null ? joint.connectedAnchor : Vector3.zero;
                targetRotation = transform.rotation;
                targetPosition = worldAnchor - (targetRotation * connectedAnchor);
            }

            activeFishRigidbody.position = targetPosition;
            activeFishRigidbody.rotation = targetRotation;
            activeFishRigidbody.linearVelocity = Vector3.zero;
            activeFishRigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Locks the active fish at its current pose relative to the holder-attached joint.
        /// Call when the presentation ends and the fish begins its store animation.
        /// </summary>
        public void LockFishBodyToHolder()
        {
            if (holderTransform == null || currentActiveFish == null || fishBodyFollowsHolder)
            {
                return;
            }

            Transform fishTransform = activeFishRigidbody != null
                ? activeFishRigidbody.transform
                : currentActiveFish.transform;

            heldFishLocalPosition = transform.InverseTransformPoint(fishTransform.position);
            heldFishLocalRotation = Quaternion.Inverse(transform.rotation) * fishTransform.rotation;
            fishBodyFollowsHolder = true;

            if (activeFishRigidbody != null)
            {
                activeFishRigidbody.linearVelocity = Vector3.zero;
                activeFishRigidbody.angularVelocity = Vector3.zero;
                activeFishRigidbody.useGravity = false;
                activeFishRigidbody.isKinematic = true;
            }
        }

        private void SpawnFishInternal(GameObject fishPrefab, Vector3 dataHolderOffset)
        {
            fishDataHolderOffset = dataHolderOffset;
            currentActiveFish = Instantiate(fishPrefab, transform.position + spawnOffset, Quaternion.identity);
            activeFishRigidbody = currentActiveFish.GetComponent<Rigidbody>();

            joint.connectedBody = activeFishRigidbody;
            fishCollider = currentActiveFish.GetComponentInChildren<Collider>();
        }

        public virtual void DisableFishCollider()
        {
            if (fishCollider != null)
            {
                fishCollider.enabled = false;
            }
        }

        public virtual void DespawnFish()
        {
            FinishHolderOffsetBlend();

            holderTransform = null;
            fishBodyFollowsHolder = false;
            heldFishLocalPosition = Vector3.zero;
            heldFishLocalRotation = Quaternion.identity;

            transform.localPosition = localPos;
            transform.localRotation = Quaternion.identity;
            Destroy(currentActiveFish.gameObject);
            currentActiveFish = null;
            activeFishRigidbody = null;
            activeFishData = null;
            fishDataHolderOffset = Vector3.zero;
        }

        public virtual void TransferParent(Transform t)
        {
            FinishHolderOffsetBlend();

            localPos = transform.localPosition;
            transform.SetParent(t);

            // Zero out local position/rotation so the combined joint and fish-data offsets are the
            // sole source of the fish's final hand position, rather than preserving SetParent's world position.
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            holderTransform = t;
            fishBodyFollowsHolder = false;

            if (holderOffsetBlendDuration <= 0f)
            {
                transform.localPosition = EffectiveHolderOffset;
                return;
            }

            holderOffsetBlendRoutine = StartCoroutine(BlendHolderOffset());
        }

        private void FollowFishBodyToHolder()
        {
            if (!fishBodyFollowsHolder || currentActiveFish == null)
            {
                return;
            }

            Vector3 targetPosition = transform.TransformPoint(heldFishLocalPosition);
            Quaternion targetRotation = transform.rotation * heldFishLocalRotation;

            if (activeFishRigidbody != null)
            {
                activeFishRigidbody.position = targetPosition;
                activeFishRigidbody.rotation = targetRotation;
            }
            else
            {
                currentActiveFish.transform.SetPositionAndRotation(targetPosition, targetRotation);
            }
        }

        private IEnumerator BlendHolderOffset()
        {
            float elapsed = 0f;

            while (elapsed < holderOffsetBlendDuration)
            {
                elapsed += Time.deltaTime;
                float eased = holderOffsetBlendCurve.Evaluate(Mathf.Clamp01(elapsed / holderOffsetBlendDuration));
                transform.localPosition = Vector3.LerpUnclamped(Vector3.zero, EffectiveHolderOffset, eased);
                yield return null;
            }

            transform.localPosition = EffectiveHolderOffset;
            holderOffsetBlendRoutine = null;
        }

        private void FinishHolderOffsetBlend()
        {
            if (holderOffsetBlendRoutine == null)
            {
                return;
            }

            StopCoroutine(holderOffsetBlendRoutine);
            holderOffsetBlendRoutine = null;

            if (holderTransform != null)
            {
                transform.localPosition = EffectiveHolderOffset;
            }
        }
    }
}
