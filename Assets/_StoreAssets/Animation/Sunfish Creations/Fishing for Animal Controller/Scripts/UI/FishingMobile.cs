using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.Items;
using MalbersAnimations.Utilities;
using SunfishCreations.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace SunfishCreations
{
    /// <summary>
    /// Routes mobile fishing controls to the active Super Item player in this scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingMobile : MonoBehaviour
    {
        private const string AttackButtonName = "Mobile B Attack";
        private const string AttackIconName = "Icon";
        private const string PickupButtonName = "Mobile B PickUp";
        private const string BackButtonName = "Mobile B Back";
        private const string ReloadButtonName = "Mobile B Reload";
        private const int MobileReelModeId = 901102;

        [Tooltip("Super Item ID identifying the fishing rod.")]
        [SerializeField] private SuperItemID fishingRodItemType;

        [Tooltip("Primary item action used by the mobile attack control.")]
        [SerializeField] private ItemActionID attackActionId;

        [Tooltip("Additional unarmed item action used by the mobile attack control.")]
        [SerializeField] private ItemActionID alternateAttackActionId;

        [Tooltip("Item action invoked by the mobile Reload control.")]
        [SerializeField] private ItemActionID reloadActionId;

        [Tooltip("Holster selected by the mobile Back control.")]
        [SerializeField] private HolsterID backHolsterId;

        [Tooltip("Icon shown by the mobile attack control while the fishing rod is equipped.")]
        [SerializeField] private Sprite fishingRodIcon;

        private SuperItemManager superItemManager;
        private MAnimal animal;
        private Fishing fishingState;
        private MInteractor interactor;
        private Button attackButton;
        private Button pickupButton;
        private Button backButton;
        private Button reloadButton;
        private Image attackButtonIcon;
        private Image backButtonIcon;
        private Sprite defaultAttackIcon;
        private Sprite defaultBackButtonIcon;
        private bool referencesResolved;
        private bool reloadButtonOriginallyEnabled;
        private bool hasCapturedReloadButtonState;

        private void OnEnable()
        {
            ResolveSceneReferences();
            if (interactor != null)
            {
                interactor.OnFocusing += HandleInteractableFocused;
                interactor.OnUnFocusing += HandleInteractableUnfocused;
            }

            UpdatePickupButtonVisibility();
            UpdateAttackIcon();
            UpdateReloadButtonVisibility();
            UpdateBackButtonVisibility();
        }

        private void OnDisable()
        {
            if (interactor != null)
            {
                interactor.OnFocusing -= HandleInteractableFocused;
                interactor.OnUnFocusing -= HandleInteractableUnfocused;
            }

            UnbindFishingStateEvents();

            if (pickupButton != null)
                pickupButton.gameObject.SetActive(false);

            if (reloadButton != null)
                reloadButton.gameObject.SetActive(false);

            if (backButton != null)
                backButton.gameObject.SetActive(false);

            if (attackButtonIcon != null)
                attackButtonIcon.sprite = defaultAttackIcon;

            if (backButtonIcon != null)
                backButtonIcon.sprite = defaultBackButtonIcon;

            if (reloadButton != null && hasCapturedReloadButtonState)
                reloadButton.enabled = reloadButtonOriginallyEnabled;
        }

        private void Update()
        {
            if (!referencesResolved)
                ResolveSceneReferences();

            UpdateAttackIcon();
            UpdateReloadButtonVisibility();
            UpdateBackButtonVisibility();
        }

        /// <summary>
        /// Interacts with the active player's currently focused interactable.
        /// </summary>
        public void PickupFocusedItem()
        {
            if (interactor == null || interactor.FocusedInteractables == null || interactor.FocusedInteractables.Count == 0)
                return;

            interactor.Interact();
        }

        /// <summary>
        /// Starts the configured primary and alternate mobile attack actions.
        /// </summary>
        public void PressAttack()
        {
            if (superItemManager == null)
                return;

            if (IsFishingRodEquipped())
            {
                if (attackActionId != null && !superItemManager.Action_Press(attackActionId, fishingRodItemType))
                    Debug.LogWarning("Fishing Mobile: the equipped fishing rod did not accept its Attack action. Check the rod's action conditions and active action set.", this);

                return;
            }

            if (attackActionId != null)
                superItemManager.Action_Press(attackActionId);

            if (alternateAttackActionId != null)
                superItemManager.Action_Press(alternateAttackActionId);
        }

        /// <summary>
        /// Releases the configured primary and alternate mobile attack actions.
        /// </summary>
        public void ReleaseAttack()
        {
            if (superItemManager == null)
                return;

            if (IsFishingRodEquipped())
            {
                if (attackActionId != null)
                    superItemManager.Action_Release(attackActionId, fishingRodItemType);

                return;
            }

            if (attackActionId != null)
                superItemManager.Action_Release(attackActionId);

            if (alternateAttackActionId != null)
                superItemManager.Action_Release(alternateAttackActionId);
        }

        /// <summary>
        /// Taps the configured Super Item Reload action once.
        /// </summary>
        public void ReloadTapped()
        {
            if (superItemManager != null && reloadActionId != null)
                superItemManager.Action_Tap(reloadActionId);
        }

        /// <summary>
        /// Activates the fishing reel mode used by the existing mobile Reload control.
        /// </summary>
        public void BeginReel()
        {
            if (animal != null)
                animal.Mode_Activate(MobileReelModeId);
        }

        /// <summary>
        /// Ends the held fishing reel mode when the mobile Reload control is released.
        /// </summary>
        public void EndReel()
        {
            if (animal != null)
                animal.Mode_Interrupt_Forced();
        }

        /// <summary>
        /// Uses the configured holster through the Super Item manager's general holster behavior.
        /// </summary>
        public void UseBackHolster()
        {
            if (superItemManager != null && backHolsterId != null)
                superItemManager.Item_Equip_From_Holster(backHolsterId);
        }

        private void ResolveSceneReferences()
        {
            SuperItemManager[] managers = FindObjectsByType<SuperItemManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            SuperItemManager resolvedManager = null;
            int activeManagerCount = 0;

            foreach (SuperItemManager candidate in managers)
            {
                if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.gameObject.scene != gameObject.scene)
                    continue;

                resolvedManager = candidate;
                activeManagerCount++;
            }

            if (activeManagerCount != 1)
            {
                UnbindFishingStateEvents();
                SetReloadButtonVisible(false);
                referencesResolved = false;
                superItemManager = null;
                animal = null;
                interactor = null;
                if (activeManagerCount > 1)
                    Debug.LogError("Fishing Mobile found multiple active Super Item managers in this scene; mobile fishing inputs were not bound.", this);
                return;
            }

            superItemManager = resolvedManager;
            animal = superItemManager.GetComponent<MAnimal>();
            BindFishingStateEvents();
            interactor = superItemManager.GetComponentInChildren<MInteractor>(true);

            attackButton = FindUniqueSceneComponent<Button>(AttackButtonName);
            pickupButton = FindUniqueSceneComponent<Button>(PickupButtonName);
            backButton = FindUniqueSceneComponent<Button>(BackButtonName);
            backButtonIcon = FindButtonIcon(backButton, BackButtonName);
            if (backButtonIcon != null && defaultBackButtonIcon == null)
                defaultBackButtonIcon = backButtonIcon.sprite;
            reloadButton = FindUniqueSceneComponent<Button>(ReloadButtonName);
            if (reloadButton != null)
            {
                if (!hasCapturedReloadButtonState)
                {
                    reloadButtonOriginallyEnabled = reloadButton.enabled;
                    hasCapturedReloadButtonState = true;
                }

                // The custom UIButton fires WM Reload on press; disable Button.onClick to prevent a duplicate Reload tap.
                reloadButton.enabled = false;
                SetReloadButtonVisible(fishingState != null && fishingState._fishOnHook && !fishingState._fishProcessed);
            }

            attackButtonIcon = FindAttackButtonIcon();

            if (attackButtonIcon != null && defaultAttackIcon == null)
                defaultAttackIcon = attackButtonIcon.sprite;

            referencesResolved = animal != null && interactor != null && attackButton != null && pickupButton != null && backButton != null && reloadButton != null && attackButtonIcon != null;
            if (!referencesResolved)
                Debug.LogWarning("Fishing Mobile could not resolve the active Super Item player's animal, interactor, or one or more uniquely named mobile controls.", this);
        }

        private Button FindUniqueSceneComponent<Button>(string objectName) where Button : Component
        {
            Button[] candidates = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Button match = null;

            foreach (Button candidate in candidates)
            {
                if (candidate == null || candidate.gameObject.name != objectName || candidate.gameObject.scene != gameObject.scene)
                    continue;

                if (match != null)
                {
                    Debug.LogError($"Fishing Mobile found multiple scene objects named '{objectName}'; that control was not bound.", this);
                    return null;
                }

                match = candidate;
            }

            return match;
        }

        private Image FindAttackButtonIcon()
        {
            Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Image match = null;

            foreach (Image candidate in images)
            {
                if (candidate == null || candidate.gameObject.name != AttackIconName || candidate.transform.parent == null)
                    continue;

                if (candidate.transform.parent.name != AttackButtonName || candidate.gameObject.scene != gameObject.scene)
                    continue;

                if (match != null)
                {
                    Debug.LogError("Fishing Mobile found multiple attack-button icons in this scene; the rod icon was not bound.", this);
                    return null;
                }

                match = candidate;
            }

            return match;
        }

        private Image FindButtonIcon(Button button, string buttonName)
        {
            if (button == null)
                return null;

            Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Image candidate in images)
            {
                if (candidate != null && candidate.gameObject.scene == gameObject.scene && candidate.gameObject.name == "Icon" && candidate.transform.parent == button.transform)
                    return candidate;
            }

            Debug.LogWarning($"Fishing Mobile could not find the Icon image under '{buttonName}'.", this);
            return null;
        }

        private void UpdateBackButtonVisibility()
        {
            if (backButton == null)
                return;

            SuperItem displayedItem = null;
            if (superItemManager != null && backHolsterId != null)
            {
                foreach (SItemHolster holster in superItemManager.holsters)
                {
                    if (holster != null && holster.ID == backHolsterId && holster.Item != null)
                    {
                        displayedItem = holster.Item;
                        break;
                    }
                }

                if (displayedItem == null)
                {
                    foreach (SEquipPoint equipPoint in superItemManager.EquipPoints)
                    {
                        if (equipPoint?.Item != null && equipPoint.Item.IsAllowedHolster(backHolsterId))
                        {
                            displayedItem = equipPoint.Item;
                            break;
                        }
                    }
                }
            }

            bool shouldShowBackButton = displayedItem != null;
            if (backButton.gameObject.activeSelf != shouldShowBackButton)
                backButton.gameObject.SetActive(shouldShowBackButton);

            if (backButtonIcon != null)
            {
                Sprite itemIcon = displayedItem != null ? displayedItem.UIIcon : null;
                if (itemIcon == null && displayedItem != null && displayedItem.ItemType == fishingRodItemType)
                    itemIcon = fishingRodIcon;

                backButtonIcon.sprite = itemIcon != null ? itemIcon : defaultBackButtonIcon;
            }
        }

        private void UpdateReloadButtonVisibility()
        {
            if (animal == null)
            {
                SetReloadButtonVisible(false);
                return;
            }

            Fishing activeFishingState = animal.State_Get<Fishing>();
            bool shouldShowReload = activeFishingState != null && animal.ActiveState == activeFishingState && activeFishingState._fishOnHook && !activeFishingState._fishProcessed;
            SetReloadButtonVisible(shouldShowReload);
        }

        private void BindFishingStateEvents()
        {
            Fishing resolvedFishingState = animal != null ? animal.State_Get<Fishing>() : null;
            if (resolvedFishingState == fishingState)
                return;

            UnbindFishingStateEvents();
            fishingState = resolvedFishingState;
            if (fishingState == null)
                return;

            fishingState.OnFishingStart.AddListener(HideReloadButton);
            fishingState.OnFishBiting.AddListener(ShowReloadButton);
            fishingState.OnFishMissed.AddListener(HideReloadButton);
            fishingState.OnFishGotAway.AddListener(HideReloadButton);
            fishingState.OnFishHooked.AddListener(HandleFishHooked);
            fishingState.OnFishingEnd.AddListener(HideReloadButton);

            if (fishingState._fishOnHook && !fishingState._fishProcessed)
                ShowReloadButton();
        }

        private void UnbindFishingStateEvents()
        {
            if (fishingState == null)
                return;

            fishingState.OnFishingStart.RemoveListener(HideReloadButton);
            fishingState.OnFishBiting.RemoveListener(ShowReloadButton);
            fishingState.OnFishMissed.RemoveListener(HideReloadButton);
            fishingState.OnFishGotAway.RemoveListener(HideReloadButton);
            fishingState.OnFishHooked.RemoveListener(HandleFishHooked);
            fishingState.OnFishingEnd.RemoveListener(HideReloadButton);
            fishingState = null;
        }

        private void ShowReloadButton()
        {
            SetReloadButtonVisible(true);
        }

        private void HideReloadButton()
        {
            SetReloadButtonVisible(false);
        }

        private void HandleFishHooked(FishData fishData)
        {
            SetReloadButtonVisible(false);
        }

        private void SetReloadButtonVisible(bool visible)
        {
            if (reloadButton != null && reloadButton.gameObject.activeSelf != visible)
                reloadButton.gameObject.SetActive(visible);
        }

        private void HandleInteractableFocused(IInteractable interactable)
        {
            if (interactable is SuperItem)
                UpdatePickupButtonVisibility();
        }

        private void HandleInteractableUnfocused(IInteractable interactable)
        {
            if (interactable is SuperItem)
                UpdatePickupButtonVisibility();
        }

        private void UpdatePickupButtonVisibility()
        {
            if (pickupButton == null || interactor == null || interactor.FocusedInteractables == null)
                return;

            bool hasFocusedSuperItem = false;
            foreach (IInteractable focusedInteractable in interactor.FocusedInteractables)
            {
                if (focusedInteractable is SuperItem)
                {
                    hasFocusedSuperItem = true;
                    break;
                }
            }

            pickupButton.gameObject.SetActive(hasFocusedSuperItem);
        }

        private void UpdateAttackIcon()
        {
            if (superItemManager == null || attackButtonIcon == null || fishingRodItemType == null)
                return;

            attackButtonIcon.sprite = IsFishingRodEquipped() ? fishingRodIcon : defaultAttackIcon;
        }

        private bool IsFishingRodEquipped()
        {
            if (superItemManager == null || fishingRodItemType == null)
                return false;

            foreach (SEquipPoint equipPoint in superItemManager.EquipPoints)
            {
                if (equipPoint != null && equipPoint.Item != null && equipPoint.Item.ItemType == fishingRodItemType)
                    return true;
            }

            return false;
        }
    }
}
