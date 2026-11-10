using System.Collections.Generic;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    public enum SlotsMode
    {
        Save,
        Load,
    }

    // "Select Save Slot": one row per slot (Save 1-5, and the editor's slot in the editor), after Escape from Duckov. Saves
    // to or loads the slot clicked (asking before anything is overwritten or lost), deletes a save, copies the save folder's
    // path. Used by the pause menu and the main menu, which hand it the session (Bind) and close it on Esc (Cancel).
    public sealed class SaveSlotsPanel : MonoBehaviour
    {
        [SerializeField] MenuStyle _style;
        [SerializeField] TMP_Text _title;
        [SerializeField] Button _back;
        [SerializeField] Button _copyPath;
        [SerializeField] TMP_Text _status;
        [SerializeField, Tooltip("Inactive template, cloned once per slot")] SaveSlotRow _rowTemplate;
        [SerializeField] ConfirmDialog _confirm;

        readonly List<SaveSlotRow> _rows = new();
        GameSession _session;
        SaveService _saves;
        SlotsMode _mode;
        bool _hooked;

        public event System.Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        public void Bind(GameSession session, SaveService saves)
        {
            _session = session;
            _saves = saves;
        }

        public void Open(SlotsMode mode)
        {
            Hook();
            _mode = mode;
            _title.text = mode == SlotsMode.Save ? "Save Game" : "Load Game";
            gameObject.SetActive(true);
            _confirm.Close();
            SetStatus(null);
            Refresh();
            if (_mode == SlotsMode.Save && !_session.CanSave(out string reason))
                SetStatus(reason);
            SelectFirst();
        }

        public void Close()
        {
            _confirm.Close();
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        // Esc: the question first, then the panel.
        public void Cancel()
        {
            if (_confirm.IsOpen)
                _confirm.Close();
            else
                Close();
        }

        void Hook()
        {
            if (_hooked)
                return;
            _hooked = true;
            _back.onClick.AddListener(Close);
            _copyPath.onClick.AddListener(CopyPath);
            _rowTemplate.gameObject.SetActive(false);
        }

        void Refresh()
        {
            bool canSave = _mode != SlotsMode.Save || _session.CanSave(out _);
            int index = 0;
            foreach (int slot in _saves.Slots)
            {
                if (index == _rows.Count)
                    _rows.Add(NewRow(slot));
                SaveSlotInfo info = _saves.Info(slot);
                bool clickable = _mode == SlotsMode.Save ? canSave : info.Header != null;
                bool current = _session.InWorld && _session.Slot == slot;
                _rows[index++].Show(info, "Empty", current, clickable, _style);
            }
        }

        SaveSlotRow NewRow(int slot)
        {
            SaveSlotRow row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
            row.name = $"Slot{slot}";
            row.gameObject.SetActive(true);
            row.Button.onClick.AddListener(() => OnSlot(slot));
            row.Delete.onClick.AddListener(() => OnDelete(slot));
            return row;
        }

        // A save finished (here, from the console or the autosave): the rows and the outcome. The owner forwards GameSaved:
        // this panel may never be activated, and then Unity never calls its OnDestroy to drop a subscription.
        public void ShowSaved(GameSaved saved)
        {
            if (!IsOpen)
                return;
            Refresh();
            SetStatus(saved.Message);
        }

        void OnSlot(int slot)
        {
            if (_session.IsLoading)
                return;
            SaveSlotInfo info = _saves.Info(slot);
            string name = SaveFormat.SlotName(slot);
            if (_mode == SlotsMode.Save)
            {
                if (info.IsEmpty || slot == _session.Slot)
                    Save(slot);
                else
                    _confirm.Ask($"Overwrite {name}?\n{SaveFormat.Summary(info)}", "Overwrite", () => Save(slot));
            }
            else if (_session.InWorld)
                _confirm.Ask($"Load {name}?\nProgress since your last save will be lost.", "Load", () => Load(slot));
            else
                Load(slot);
        }

        void Save(int slot)
        {
            _session.Save(slot, out string message);
            Refresh();
            SetStatus(message);
        }

        void Load(int slot)
        {
            if (!_session.Load(slot, out string message))
                SetStatus(message);
        }

        void OnDelete(int slot) =>
            _confirm.Ask($"Delete {SaveFormat.SlotName(slot)}?\nThis can't be undone.", "Delete", () =>
            {
                _session.Delete(slot);
                Refresh();
                SetStatus($"{SaveFormat.SlotName(slot)} deleted");
            });

        void CopyPath()
        {
            GUIUtility.systemCopyBuffer = _saves.FolderPath;
            SetStatus($"Copied: {_saves.FolderPath}");
        }

        void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message ?? string.Empty;
        }

        void SelectFirst()
        {
            foreach (SaveSlotRow row in _rows)
                if (row.Button.interactable)
                {
                    EventSystem.current?.SetSelectedGameObject(row.Button.gameObject);
                    return;
                }
            EventSystem.current?.SetSelectedGameObject(_back.gameObject);
        }
    }
}
