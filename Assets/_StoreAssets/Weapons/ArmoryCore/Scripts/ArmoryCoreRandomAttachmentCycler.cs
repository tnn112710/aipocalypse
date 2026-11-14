using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArmoryCoreWeaponController))]
    public sealed class ArmoryCoreRandomAttachmentCycler : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float intervalSeconds = 1f;
        [SerializeField, Range(0f, 1f)] private float completeClearChance = 0.2f;
        [SerializeField] private bool useUnscaledTime = true;

        private ArmoryCoreWeaponController controller;
        private float nextChangeTime;

        public float IntervalSeconds => intervalSeconds;
        public float CompleteClearChance => completeClearChance;

        private void Awake()
        {
            controller = GetComponent<ArmoryCoreWeaponController>();
        }

        private void OnEnable()
        {
            ScheduleNextChange();
        }

        private void Update()
        {
            if (CurrentTime() < nextChangeTime)
                return;
            RandomizeNow();
            ScheduleNextChange();
        }

        public void Configure(float interval, float clearChance, bool unscaledTime = true)
        {
            intervalSeconds = Mathf.Max(0.05f, interval);
            completeClearChance = Mathf.Clamp01(clearChance);
            useUnscaledTime = unscaledTime;
        }

        [ContextMenu("Randomize Attachments Now")]
        public void RandomizeNow()
        {
            var clearEverything = Random.value < completeClearChance;
            for (var slotIndex = 0; slotIndex < transform.childCount; slotIndex++)
            {
                var slotRoot = transform.GetChild(slotIndex);
                if (!slotRoot.name.EndsWith("_All", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var selectedIndex = clearEverything || slotRoot.childCount == 0
                    ? -1
                    : Random.Range(-1, slotRoot.childCount);
                for (var optionIndex = 0; optionIndex < slotRoot.childCount; optionIndex++)
                    slotRoot.GetChild(optionIndex).gameObject.SetActive(optionIndex == selectedIndex);
            }

            if (controller == null)
                controller = GetComponent<ArmoryCoreWeaponController>();
            controller?.RebuildInstalledAttachments();
        }

        private void ScheduleNextChange()
        {
            nextChangeTime = CurrentTime() + intervalSeconds;
        }

        private float CurrentTime()
        {
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }

        private void OnValidate()
        {
            intervalSeconds = Mathf.Max(0.05f, intervalSeconds);
            completeClearChance = Mathf.Clamp01(completeClearChance);
        }
    }
}
