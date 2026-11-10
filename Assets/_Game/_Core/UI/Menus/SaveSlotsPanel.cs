using System.Collections.Generic;
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
        NewGame,
    }

    // "Select Save Slot": one row per slot, after Escape from Duckov. Saves to, loads or starts a new game in the slot
    // clicked (asking before anything is overwritten or lost), deletes a save, copies the save folder's path. Used by the
    // pause menu and the main menu, which hand it the session (Bind) and close it on Esc (Cancel).
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
            _title.text = mode switch
            {
                SlotsMode.Save => "Save Game",
                SlotsMode.Load => "Load Game",
                _ => "New Game",
            };
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
            while (_rows.Count < _saves.SlotCount)
            {
                int slot = _rows.Count + 1;
                SaveSlotRow row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
                row.name = $"Slot{slot}";
                row.gameObject.SetActive(true);
                row.Button.onClick.AddListener(() => OnSlot(slot));
                row.Delete.onClick.AddListener(() => OnDelete(slot));
                _rows.Add(row);
            }
            bool canSave = _mode != SlotsMode.Save || _session.CanSave(out _);
            string emptyText = _mode == SlotsMode.NewGame ? "New Game" : "Empty";
            for (int i = 0; i < _rows.Count; i++)
            {
                SaveSlotInfo info = _saves.Info(i + 1);
                bool clickable = _mode switch
                {
                    SlotsMode.Save => canSave,
                    SlotsMode.Load => info.Header != null,
                    _ => true,
                };
                bool current = _session.InWorld && _session.Slot == i + 1;
                _rows[i].Show(info, emptyText, current, clickable, _style);
            }
        }

        void OnSlot(int slot)
        {
            if (_session.IsLoading)
                return;
            SaveSlotInfo info = _saves.Info(slot);
            switch (_mode)
            {
                case SlotsMode.Save:
                    if (info.IsEmpty || slot == _session.Slot)
                        Save(slot);
                    else
                        _confirm.Ask($"Overwrite Save {slot}?\n{SaveFormat.Summary(info)}", "Overwrite", () => Save(slot));
                    break;
                case SlotsMode.Load:
                    if (_session.InWorld)
                        _confirm.Ask($"Load Save {slot}?\nProgress since your last save will be lost.", "Load", () => Load(slot));
                    else
                        Load(slot);
                    break;
                case SlotsMode.NewGame:
                    if (info.IsEmpty)
                        NewGame(slot);
                    else
                        _confirm.Ask($"Start a new game in Save {slot}?\nIts save is overwritten when the new game first saves.", "New Game", () => NewGame(slot));
                    break;
            }
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

        void NewGame(int slot)
        {
            if (!_session.NewGame(slot, out string message))
                SetStatus(message);
        }

        void OnDelete(int slot) =>
            _confirm.Ask($"Delete Save {slot}?\nThis can't be undone.", "Delete", () =>
            {
                _session.Delete(slot);
                Refresh();
                SetStatus($"Save {slot} deleted");
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
