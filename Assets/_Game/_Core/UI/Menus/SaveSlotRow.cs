using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // One slot in the save list: "Save 2" (or "Editor Save") with its day, hour and play time and when it was saved, and a delete button.
    public sealed class SaveSlotRow : MonoBehaviour
    {
        const string EmptyDate = "----/--/-- --:--";
        static readonly LocalizedString Backup = Loc.UI("slots.backup");

        [SerializeField] Button _button;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _progress;
        [SerializeField] TMP_Text _date;
        [SerializeField] Button _delete;

        public Button Button => _button;
        public Button Delete => _delete;

        // `emptyText` is what an empty slot offers ("New Game", "Empty"); `current` marks the game being played.
        public void Show(SaveSlotInfo info, string emptyText, bool current, bool clickable, MenuStyle style)
        {
            _title.text = SaveFormat.SlotName(info.Slot);
            if (info.Header != null)
            {
                _progress.text = SaveSlotProgress(info);
                _date.text = SaveFormat.SavedAt(info.Header);
            }
            else
            {
                _progress.text = info.Problem ?? emptyText;
                _date.text = EmptyDate;
            }
            _button.interactable = clickable;
            if (style != null)
                _button.colors = style.Colors(current ? style.Accent : null);
            _delete.gameObject.SetActive(!info.IsEmpty);
        }

        static string SaveSlotProgress(SaveSlotInfo info) =>
            info.FromBackup ? SaveFormat.Progress(info.Header) + Backup.Text() : SaveFormat.Progress(info.Header);
    }
}
