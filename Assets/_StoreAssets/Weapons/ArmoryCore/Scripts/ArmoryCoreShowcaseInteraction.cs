using System;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ArmoryCoreShowcaseInteraction : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private ArmoryCoreShowcaseFlyCamera flyCamera;
        [SerializeField] private Transform demoSpawn;
        [SerializeField, Min(1f)] private float interactionDistance = 300f;
        [SerializeField, Range(40f, 100f)] private float demoFieldOfView = 65f;
        [SerializeField] private Vector3 heldViewPosition = new(0.23f, -0.24f, 0.96f);
        [SerializeField] private Vector3 heldViewRotation = new(0.5f, 82.5f, 0f);
        [SerializeField, Min(0.1f)] private float heldWeaponLength = 0.88f;

        private ArmoryCoreShowcaseWeaponCard hoveredCard;
        private ArmoryCoreShowcaseWeaponCard selectedCard;
        private GameObject heldRigObject;
        private GameObject heldWeaponObject;
        private ArmoryCoreWeaponController heldWeapon;
        private ArmoryCoreFirstPersonWeaponRig heldRig;
        private Vector3 galleryPosition;
        private Quaternion galleryRotation;
        private float galleryFieldOfView;
        private bool hasGalleryPose;

        public ArmoryCoreWeaponController HeldWeapon => heldWeapon;
        public ArmoryCoreShowcaseWeaponCard HoveredCard => hoveredCard;
        public Transform DemoSpawn => demoSpawn;
        public bool IsInDemoLocation => hasGalleryPose;

        private void Awake()
        {
            UpgradeLegacyHeldPose();
            if (viewCamera == null)
                viewCamera = GetComponent<Camera>();
            if (flyCamera == null)
                flyCamera = GetComponent<ArmoryCoreShowcaseFlyCamera>();
        }

        private void UpgradeLegacyHeldPose()
        {
            // Existing showcase scenes may still contain the older, oversized
            // 76-degree hip pose even after the script defaults have changed.
            if (heldViewRotation.y < 80f || heldWeaponLength > 0.9f || heldViewPosition.z < 0.92f)
            {
                heldViewPosition = new Vector3(0.23f, -0.24f, 0.96f);
                heldViewRotation = new Vector3(0.5f, 82.5f, 0f);
                heldWeaponLength = 0.88f;
            }
        }

        private void Update()
        {
            if (heldWeapon == null)
            {
                UpdateHoveredCard();
                if (InteractPressed() && hoveredCard != null)
                    SelectWeapon(hoveredCard);
                return;
            }

            if (InteractPressed() || EscapePressed())
            {
                ReleaseWeapon();
                return;
            }

            if (SlotPressed(1))
                CycleAttachment(ArmoryCoreAttachmentSlot.Sights);
            if (SlotPressed(2))
                CycleAttachment(ArmoryCoreAttachmentSlot.Barrel);
            if (SlotPressed(3))
                CycleAttachment(ArmoryCoreAttachmentSlot.Grip);
            if (SlotPressed(4))
                CycleAttachment(ArmoryCoreAttachmentSlot.Stock);
        }

        private void OnDisable()
        {
            SetHoveredCard(null);
            ReleaseWeapon();
        }

        public void Configure(
            Camera cameraValue,
            ArmoryCoreShowcaseFlyCamera flyCameraValue,
            Transform demoSpawnValue)
        {
            viewCamera = cameraValue;
            flyCamera = flyCameraValue;
            demoSpawn = demoSpawnValue;
        }

        public void SelectWeapon(ArmoryCoreShowcaseWeaponCard card)
        {
            if (card == null || card.Weapon == null)
                return;
            ReleaseWeapon();
            EnterDemoLocation();

            selectedCard = card;
            heldRigObject = new GameObject("First Person Weapon Rig");
            heldRigObject.transform.SetParent(viewCamera.transform, false);
            var fillLightObject = new GameObject("Weapon Fill Light");
            fillLightObject.transform.SetParent(heldRigObject.transform, false);
            fillLightObject.transform.localPosition = new Vector3(-0.18f, 0.12f, 0.28f);
            fillLightObject.transform.localRotation = Quaternion.identity;
            var fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Spot;
            fillLight.color = new Color(0.68f, 0.8f, 1f);
            fillLight.range = 2.6f;
            fillLight.intensity = 7.5f;
            fillLight.spotAngle = 96f;
            fillLight.innerSpotAngle = 58f;
            fillLight.shadows = LightShadows.None;
            heldWeaponObject = Instantiate(card.Weapon.gameObject);
            heldWeaponObject.name = card.DisplayName + " (Test Weapon)";
            foreach (var cycler in heldWeaponObject.GetComponents<ArmoryCoreRandomAttachmentCycler>())
                cycler.enabled = false;

            heldWeapon = heldWeaponObject.GetComponent<ArmoryCoreWeaponController>();
            if (heldWeapon == null)
            {
                Destroy(heldRigObject);
                Destroy(heldWeaponObject);
                heldRigObject = null;
                heldWeaponObject = null;
                selectedCard = null;
                ExitDemoLocation();
                return;
            }

            heldWeaponObject.transform.SetParent(heldRigObject.transform, false);
            heldWeaponObject.transform.localPosition = Vector3.zero;
            heldWeaponObject.transform.localRotation = Quaternion.Euler(heldViewRotation);
            heldWeaponObject.transform.localScale = Vector3.one;

            var localBounds = CalculateLocalBounds(heldWeaponObject);
            var presentation = ResolveHeldPresentation(heldWeapon.Definition);
            var scale = presentation.length / Mathf.Max(localBounds.size.x, 0.0001f);
            heldWeaponObject.transform.localScale = Vector3.one * scale;
            var worldBounds = CalculateWorldBounds(heldWeaponObject);
            var targetCenter = viewCamera.transform.TransformPoint(presentation.center);
            heldWeaponObject.transform.position += targetCenter - worldBounds.center;

            heldWeapon.SetAimCamera(viewCamera);
            heldWeapon.SetAimReference(viewCamera.transform);
            heldWeapon.SetPlayerInputEnabled(true);
            heldWeapon.InitializeState();
            if (flyCamera != null)
                heldWeapon.RecoilApplied += flyCamera.ApplyWeaponRecoil;
            heldRig = heldRigObject.AddComponent<ArmoryCoreFirstPersonWeaponRig>();
            heldRig.Configure(heldWeapon, flyCamera, viewCamera);
            SetHoveredCard(null);
        }

        public void ReleaseWeapon()
        {
            if (heldWeapon != null)
            {
                heldWeapon.SetPlayerInputEnabled(false);
                if (flyCamera != null)
                    heldWeapon.RecoilApplied -= flyCamera.ApplyWeaponRecoil;
            }
            if (heldRigObject != null)
                Destroy(heldRigObject);
            else if (heldWeaponObject != null)
                Destroy(heldWeaponObject);
            heldRigObject = null;
            heldWeaponObject = null;
            heldWeapon = null;
            heldRig = null;
            selectedCard = null;
            ExitDemoLocation();
        }

        private (float length, Vector3 center) ResolveHeldPresentation(ArmoryCoreWeaponDefinition definition)
        {
            if (definition == null)
                return (heldWeaponLength, heldViewPosition);
            var mass = definition.weaponMassKilograms;
            if (mass <= 1.35f)
                return (0.46f, new Vector3(0.14f, -0.17f, 0.55f));
            if (mass <= 2.35f)
                return (0.68f, new Vector3(0.18f, -0.2f, 0.74f));
            if (mass <= 4.8f)
                return (heldWeaponLength, heldViewPosition);
            return (0.96f, new Vector3(0.26f, -0.29f, 1.05f));
        }

        public void CycleAttachment(ArmoryCoreAttachmentSlot slot)
        {
            if (heldWeapon == null)
                return;
            var slotRoot = heldWeapon.GetAttachmentSlotRoot(slot);
            if (slotRoot == null || slotRoot.childCount == 0)
                return;

            var activeIndex = -1;
            for (var index = 0; index < slotRoot.childCount; index++)
            {
                if (slotRoot.GetChild(index).gameObject.activeSelf)
                {
                    activeIndex = index;
                    break;
                }
            }

            var nextIndex = activeIndex + 1;
            if (nextIndex >= slotRoot.childCount)
            {
                heldWeapon.ClearAttachmentSlot(slot);
                return;
            }

            var next = slotRoot.GetChild(nextIndex);
            var module = next.GetComponent<ArmoryCoreAttachmentModule>();
            var attachmentId = module?.Definition == null ? next.name : module.Definition.id;
            heldWeapon.SelectAttachment(slot, attachmentId);
        }

        private void UpdateHoveredCard()
        {
            if (viewCamera == null)
            {
                SetHoveredCard(null);
                return;
            }
            var pointer = PointerPosition();
            var ray = viewCamera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out var hit, interactionDistance, ~0, QueryTriggerInteraction.Collide))
                SetHoveredCard(hit.collider.GetComponentInParent<ArmoryCoreShowcaseWeaponCard>());
            else
                SetHoveredCard(null);
        }

        private void SetHoveredCard(ArmoryCoreShowcaseWeaponCard value)
        {
            if (hoveredCard == value)
                return;
            hoveredCard?.SetHighlighted(false);
            hoveredCard = value;
            hoveredCard?.SetHighlighted(true);
        }

        private static Bounds CalculateLocalBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer.enabled
                    && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                .ToArray();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);
            var first = root.transform.InverseTransformPoint(renderers[0].bounds.center);
            var bounds = new Bounds(first, Vector3.zero);
            foreach (var renderer in renderers)
            {
                var min = renderer.bounds.min;
                var max = renderer.bounds.max;
                for (var x = 0; x < 2; x++)
                for (var y = 0; y < 2; y++)
                for (var z = 0; z < 2; z++)
                {
                    bounds.Encapsulate(root.transform.InverseTransformPoint(new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z)));
                }
            }
            return bounds;
        }

        private static Bounds CalculateWorldBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer.enabled
                    && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                .ToArray();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private void EnterDemoLocation()
        {
            if (viewCamera == null)
                return;
            galleryPosition = viewCamera.transform.position;
            galleryRotation = viewCamera.transform.rotation;
            galleryFieldOfView = viewCamera.fieldOfView;
            hasGalleryPose = true;

            if (demoSpawn != null)
            {
                if (flyCamera != null)
                    flyCamera.Teleport(demoSpawn.position, demoSpawn.rotation);
                else
                    viewCamera.transform.SetPositionAndRotation(demoSpawn.position, demoSpawn.rotation);
            }
            viewCamera.fieldOfView = demoFieldOfView;
            flyCamera?.SetWeaponTestMode(true);
        }

        private void ExitDemoLocation()
        {
            flyCamera?.SetWeaponTestMode(false);
            if (!hasGalleryPose || viewCamera == null)
                return;

            if (flyCamera != null)
                flyCamera.Teleport(galleryPosition, galleryRotation);
            else
                viewCamera.transform.SetPositionAndRotation(galleryPosition, galleryRotation);
            viewCamera.fieldOfView = galleryFieldOfView;
            hasGalleryPose = false;
        }

        private string ActiveModuleName(ArmoryCoreAttachmentSlot slot)
        {
            if (heldWeapon == null)
                return "—";
            var module = heldWeapon.InstalledModules.FirstOrDefault(value => value?.Definition?.slot == slot);
            return module?.Definition == null ? "None" : module.Definition.displayName;
        }

        private void OnGUI()
        {
            if (heldWeapon == null)
            {
                GUI.Box(new Rect(14, 14, 560, 78), string.Empty);
                GUI.Label(new Rect(28, 22, 530, 22), "WEAPON SHOWCASE — hover a weapon card with the mouse");
                GUI.Label(new Rect(28, 47, 530, 22),
                    hoveredCard == null
                        ? "WASD move | RMB look | Wheel zoom | Space/Ctrl height | R reset"
                        : $"E — enter the range with {hoveredCard.DisplayName}");
                return;
            }

            GUI.Box(new Rect(14, 14, 720, 146), string.Empty);
            GUI.Label(new Rect(28, 22, 690, 22),
                $"TESTING: {selectedCard?.DisplayName ?? heldWeapon.name}   "
                + $"Ammo {heldWeapon.RoundsInMagazine}+{(heldWeapon.ChamberLoaded ? 1 : 0)} / {heldWeapon.ReserveRounds}   "
                + $"Mode {heldWeapon.FireMode}");
            GUI.Label(new Rect(28, 47, 690, 22),
                "WASD move | Shift sprint | LMB fire | RMB aim | Wheel optic zoom | R reload");
            GUI.Label(new Rect(28, 72, 690, 22),
                "B fire mode | C cycle | T clear malfunction | 1 sight | 2 barrel | 3 grip | 4 stock");
            GUI.Label(new Rect(28, 97, 690, 22),
                $"Sight: {ActiveModuleName(ArmoryCoreAttachmentSlot.Sights)} | "
                + $"Barrel: {ActiveModuleName(ArmoryCoreAttachmentSlot.Barrel)} | "
                + $"Grip: {ActiveModuleName(ArmoryCoreAttachmentSlot.Grip)} | "
                + $"Stock: {ActiveModuleName(ArmoryCoreAttachmentSlot.Stock)}");
            GUI.Label(new Rect(28, 122, 690, 22), "E or Esc — return weapon to the showcase");

            if (heldRig == null || !heldRig.IsOpticOverlayVisible)
            {
                var centerX = Screen.width * 0.5f;
                var centerY = Screen.height * 0.5f;
                var previousColor = GUI.color;
                GUI.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(centerX - 1f, centerY - 8f, 2f, 16f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centerX - 8f, centerY - 1f, 16f, 2f), Texture2D.whiteTexture);
                GUI.color = previousColor;
            }
        }

        private static Vector2 PointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : Mouse.current.position.ReadValue();
#else
            return Input.mousePosition;
#endif
        }

        private static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private static bool SlotPressed(int slot)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;
            return slot switch
            {
                1 => keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
                2 => keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
                3 => keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame,
                4 => keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame,
                _ => false
            };
#else
            return slot switch
            {
                1 => Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1),
                2 => Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2),
                3 => Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3),
                4 => Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4),
                _ => false
            };
#endif
        }
    }
}
