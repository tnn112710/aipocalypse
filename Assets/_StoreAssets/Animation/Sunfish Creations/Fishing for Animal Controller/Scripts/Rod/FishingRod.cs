using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.Items;
using SunfishCreations.Controller;
using SunfishCreations.Animations;
using UnityEngine;
using UnityEngine.Events;

namespace SunfishCreations
{
    /// <summary>
    /// Bridges Super Item actions to the Simple fishing state and fishing caster.
    /// </summary>
    [DisallowMultipleComponent]
    public class FishingRod : MonoBehaviour, IHandSwapper
    {
        private const float DefaultCastCooldown = 1f;

        [Tooltip("Minimum time in seconds between fishing casts.")]
        [SerializeField, Min(0f)] private float castCooldown = DefaultCastCooldown;

        [Header("Hand Swap")]
        [Tooltip("Absolute local position, rotation, and scale applied relative to the original Super Item equip point when returning with the rod centered in both hands.")]
        [SerializeField] private TransformOffset middleRodOffset = new(1);

        private SuperItem superItem;
        private SuperItemManager cachedUser;
        private MAnimal animal;
        private Fishing fishingState;
        private FishingCaster fishingCaster;
        private FishingRodBendController bendController;
        private SuperItem cachedSwapItem;
        private SuperItemManager cachedSwapUser;
        private SEquipPoint cachedSwapEquipPoint;
        private Transform cachedEquipPointTransform;
        private Vector3 cachedEquipLocalPosition;
        private Quaternion cachedEquipLocalRotation;
        private Vector3 cachedEquipLocalScale;
        private bool hasCachedEquipTransform;
        private bool isOnSecondaryHand;
        private float lastCastTime;
        private bool hasCast;

        private void OnEnable()
        {
            BindSuperItem(GetComponent<SuperItem>());
            bendController = GetComponent<FishingRodBendController>();
        }

        private void OnDisable()
        {
            if (superItem != null)
                superItem.OnUnequip.RemoveListener(HandleItemUnequipped);

            superItem = null;
            ClearCachedOwner();
        }

        /// <summary>
        /// Executes a Super Item fishing action, either casting or issuing the active fishing-mode command.
        /// </summary>
        /// <param name="item">The Super Item currently invoking this adapter.</param>
        /// <param name="castMode">The animal mode used to play the cast ability.</param>
        /// <param name="castAbilityIndex">The configured cast ability index.</param>
        /// <returns>True when the action was handled; otherwise, false.</returns>
        public bool ExecuteItemAction(SuperItem item, ModeID castMode, int castAbilityIndex)
        {
            if (item == null)
            {
                Debug.LogWarning("Fishing Rod cannot execute an action without a Super Item context.", this);
                return false;
            }

            BindSuperItem(item);
            ResolveOwner();

            if (animal == null || fishingState == null)
            {
                Debug.LogWarning("Fishing Rod could not resolve the Super Item user's animal or Fishing state.", this);
                return false;
            }

            if (fishingState.IsActiveState)
            {
                fishingState.FishingRodCommand();
                return true;
            }

            if (castMode == null)
            {
                Debug.LogWarning("Fishing Rod cannot cast because its cast Mode ID is missing.", this);
                return false;
            }

            if (fishingCaster == null)
            {
                Debug.LogWarning("Fishing Rod could not find a FishingCaster under the Super Item user's animal.", this);
                return false;
            }

            if (hasCast && Time.time - lastCastTime < castCooldown)
                return false;

            if (!animal.Mode_TryActivate(castMode.ID, castAbilityIndex, AbilityStatus.PlayOnce))
            {
                Debug.LogWarning($"Fishing Rod could not activate cast ability {castAbilityIndex} on mode '{castMode.name}'.", this);
                return false;
            }

            lastCastTime = Time.time;
            hasCast = true;

            if (!fishingCaster.TryCastToWater(out Vector3 waterHitPoint, out GameObject hitWaterObject))
            {
                bendController?.InformWaterHit(false);
                return false;
            }

            fishingCaster.InformWaterHit(waterHitPoint);

            FishableArea fishableArea = hitWaterObject != null ? hitWaterObject.GetComponent<FishableArea>() : null;
            if (fishableArea == null)
            {
                Debug.LogWarning("Fishing Rod hit water, but the hit object has no FishableArea. The Fishing state was not activated.", hitWaterObject != null ? hitWaterObject : this);
                bendController?.InformWaterHit(false);
                return false;
            }

            fishingState.SetCurrentWaterSpot(fishableArea);
            animal.State_Activate(fishingState.ID);
            bendController?.InformWaterHit(true);
            return true;
        }

        /// <summary>
        /// Moves the rod to the owning animal's opposite hand while preserving its current world pose.
        /// </summary>
        public void OnSwapToSecondaryHand()
        {
            if (!TryResolveHandSwapContext(out SuperItem item, out SuperItemManager user, out SEquipPoint equipPoint, out Transform equipPointTransform, out _, out Transform oppositeHand))
                return;

            if (hasCachedEquipTransform && isOnSecondaryHand)
                return;

            if (!hasCachedEquipTransform)
            {
                if (transform.parent != equipPointTransform)
                {
                    Debug.LogWarning("Fishing Rod hand swap could not capture its Super Item equip offset because the rod is not parented to its current equip-point transform; the rod was left in place.", this);
                    return;
                }

                cachedSwapItem = item;
                cachedSwapUser = user;
                cachedSwapEquipPoint = equipPoint;
                cachedEquipPointTransform = equipPointTransform;
                cachedEquipLocalPosition = transform.localPosition;
                cachedEquipLocalRotation = transform.localRotation;
                cachedEquipLocalScale = transform.localScale;
                hasCachedEquipTransform = true;
            }

            transform.SetParent(oppositeHand, true);
            isOnSecondaryHand = true;
        }

        /// <summary>
        /// Restores the rod to its original Super Item equip point and reapplies its captured equip offset.
        /// </summary>
        /// <param name="middleRod">Whether to apply the configured centered-grip adjustment after restoring the equip offset.</param>
        public void OnSwapToOriginalHand(bool middleRod)
        {
            if (!hasCachedEquipTransform)
                return;

            if (!TryResolveCurrentEquipPoint(out SuperItem item, out SuperItemManager user, out SEquipPoint equipPoint, out Transform equipPointTransform))
                return;

            if (item != cachedSwapItem || user != cachedSwapUser || equipPoint != cachedSwapEquipPoint || equipPointTransform != cachedEquipPointTransform)
            {
                ClearHandSwapCache();
                return;
            }

            transform.SetParent(cachedEquipPointTransform, false);
            transform.localPosition = cachedEquipLocalPosition;
            transform.localRotation = cachedEquipLocalRotation;
            transform.localScale = cachedEquipLocalScale;

            // When fish spawn directly on the water (bobber-side catch), the rod must stay in its
            // original single-hand grip for the presentation regardless of the Animator's requested
            // middle-rod pose, since that pose is only meant for the hand-off/intercept variants.
            if (middleRod && !FishingGlobalSettings.SpawnFishOnWater)
            {
                middleRodOffset.SetOffset(transform);
            }

            isOnSecondaryHand = false;
        }

        private bool TryResolveHandSwapContext(out SuperItem item, out SuperItemManager user, out SEquipPoint equipPoint, out Transform equipPointTransform, out Transform sourceHand, out Transform oppositeHand)
        {
            sourceHand = null;
            oppositeHand = null;
            if (!TryResolveCurrentEquipPoint(out item, out user, out equipPoint, out equipPointTransform))
                return false;

            MAnimal ownerAnimal = user.Animal;
            Animator ownerAnimator = ownerAnimal.GetComponent<Animator>();
            if (ownerAnimator == null)
                ownerAnimator = ownerAnimal.GetComponentInChildren<Animator>();

            if (ownerAnimator == null || !ownerAnimator.isHuman || ownerAnimator.avatar == null)
            {
                Debug.LogWarning("Fishing Rod hand swap could not resolve a humanoid Animator on the Super Item user's animal; the rod was left in place.", this);
                return false;
            }

            WeaponSide side = equipPoint.ID != null ? equipPoint.ID.Side : WeaponSide.None;
            if (side == WeaponSide.Right)
            {
                sourceHand = ownerAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                oppositeHand = ownerAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
            else if (side == WeaponSide.Left)
            {
                sourceHand = ownerAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                oppositeHand = ownerAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            else
            {
                Debug.LogWarning("Fishing Rod hand swap requires the current Super Item equip-point ID to specify a left or right side; the rod was left in place.", this);
                return false;
            }

            if (sourceHand == null || oppositeHand == null)
            {
                Debug.LogWarning("Fishing Rod hand swap could not resolve both humanoid hand bones from the Super Item user's Animator; the rod was left in place.", this);
                return false;
            }

            return true;
        }

        private bool TryResolveCurrentEquipPoint(out SuperItem item, out SuperItemManager user, out SEquipPoint equipPoint, out Transform equipPointTransform)
        {
            item = superItem != null ? superItem : GetComponent<SuperItem>();
            user = item != null ? item.User : null;
            equipPoint = item != null ? item.CurrentEquipPoint : null;
            equipPointTransform = equipPoint?.Point != null ? equipPoint.Point.Value : null;

            if (superItem != item)
                BindSuperItem(item);

            ResolveOwner();

            if (hasCachedEquipTransform && (item != cachedSwapItem || user != cachedSwapUser || equipPoint != cachedSwapEquipPoint || equipPointTransform != cachedEquipPointTransform))
                ClearHandSwapCache();

            return item != null && user != null && user.Animal != null && equipPoint != null && equipPointTransform != null;
        }

        private void ClearHandSwapCache()
        {
            cachedSwapItem = null;
            cachedSwapUser = null;
            cachedSwapEquipPoint = null;
            cachedEquipPointTransform = null;
            cachedEquipLocalPosition = Vector3.zero;
            cachedEquipLocalRotation = Quaternion.identity;
            cachedEquipLocalScale = Vector3.one;
            hasCachedEquipTransform = false;
            isOnSecondaryHand = false;
        }

        private void BindSuperItem(SuperItem item)
        {
            if (superItem == item)
                return;

            if (superItem != null)
                superItem.OnUnequip.RemoveListener(HandleItemUnequipped);

            superItem = item;

            if (superItem != null)
                superItem.OnUnequip.AddListener(HandleItemUnequipped);

            ClearCachedOwner();
        }

        private void ResolveOwner()
        {
            SuperItemManager currentUser = superItem != null ? superItem.User : null;
            MAnimal currentAnimal = currentUser != null ? currentUser.Animal : null;

            if (cachedUser != currentUser || animal != currentAnimal)
            {
                ClearHandSwapCache();
                cachedUser = currentUser;
                animal = currentAnimal;
                fishingState = animal != null ? animal.State_Get<Fishing>() : null;
                fishingCaster = animal != null ? animal.GetComponentInChildren<FishingCaster>() : null;
            }
        }

        private void HandleItemUnequipped(GameObject ownerObject)
        {
            if (ownerObject == null)
                return;

            SuperItemManager ownerManager = ownerObject.GetComponent<SuperItemManager>();
            MAnimal ownerAnimal = ownerManager != null ? ownerManager.Animal : ownerObject.GetComponent<MAnimal>();
            Fishing ownerFishingState = ownerAnimal != null ? ownerAnimal.State_Get<Fishing>() : null;

            if (ownerFishingState != null && ownerFishingState.IsActiveState)
                ownerFishingState.AllowExit();

            if (cachedUser == ownerManager)
                ClearCachedOwner();
        }

        private void ClearCachedOwner()
        {
            cachedUser = null;
            animal = null;
            fishingState = null;
            fishingCaster = null;
            ClearHandSwapCache();
        }
    }
}
