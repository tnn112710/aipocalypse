using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Production
{
    /// <summary>A small tile rack demonstrating Text Studio text changes and native backplates.</summary>
    [AddComponentMenu("")]
    public sealed class TextStudioScrabbleExample : TextStudioProductionExampleBase
    {
        [Serializable]
        public sealed class TileEntry
        {
            public string Letter;
            public int Score;
            public Button Button;
            public TextStudioText LetterText;
            public TextStudioText ScoreText;
        }

        [SerializeField] TextStudioText _word;
        [SerializeField] TileEntry[] _tiles = Array.Empty<TileEntry>();
        [SerializeField] TextStudio2DStyleAsset _normalTileStyle;
        [SerializeField] TextStudio2DStyleAsset _selectedTileStyle;
        [SerializeField] Button _undoButton;
        [SerializeField] Button _clearButton;

        readonly List<int> _selection = new List<int>(5);
        readonly HashSet<int> _used = new HashSet<int>();
        readonly List<UnityEngine.Events.UnityAction> _tileHandlers = new List<UnityEngine.Events.UnityAction>(5);

        public int SelectionCount => _selection.Count;

        public void Configure(TextStudioText word, TileEntry[] tiles, TextStudio2DStyleAsset normalStyle,
            TextStudio2DStyleAsset selectedStyle, Button undoButton, Button clearButton)
        {
            Unbind();
            _word = word;
            _tiles = tiles ?? Array.Empty<TileEntry>();
            _normalTileStyle = normalStyle;
            _selectedTileStyle = selectedStyle;
            _undoButton = undoButton;
            _clearButton = clearButton;
            if (isActiveAndEnabled) Bind();
            ResetSelection();
        }

        void OnEnable()
        {
            Bind();
            ResetSelection();
        }

        void OnDisable()
        {
            Unbind();
            ResetSelection();
        }

        void Bind()
        {
            if (_tileHandlers.Count != 0) return;
            for (int index = 0; index < _tiles.Length; index++)
            {
                int tileIndex = index;
                UnityEngine.Events.UnityAction handler = () => SelectTile(tileIndex);
                _tileHandlers.Add(handler);
                if (_tiles[index]?.Button != null) _tiles[index].Button.onClick.AddListener(handler);
            }
            if (_undoButton != null) _undoButton.onClick.AddListener(UndoSelection);
            if (_clearButton != null) _clearButton.onClick.AddListener(ClearSelection);
        }

        void Unbind()
        {
            for (int index = 0; index < _tileHandlers.Count; index++)
                if (index < _tiles.Length && _tiles[index]?.Button != null)
                    _tiles[index].Button.onClick.RemoveListener(_tileHandlers[index]);
            _tileHandlers.Clear();
            if (_undoButton != null) _undoButton.onClick.RemoveListener(UndoSelection);
            if (_clearButton != null) _clearButton.onClick.RemoveListener(ClearSelection);
        }

        public void SelectTile(int index)
        {
            if (index < 0 || index >= _tiles.Length || _used.Contains(index) || _word == null) return;
            TileEntry tile = _tiles[index];
            if (tile == null || string.IsNullOrEmpty(tile.Letter)) return;
            _selection.Add(index);
            _used.Add(index);
            SetTileState(index, true);
            _word.AppendText(FormatTile(tile));
            RefreshControls();
        }

        public void UndoSelection()
        {
            if (_selection.Count == 0 || _word == null) return;
            int last = _selection[_selection.Count - 1];
            _selection.RemoveAt(_selection.Count - 1);
            _used.Remove(last);
            SetTileState(last, false);
            StringBuilder markup = new StringBuilder(_selection.Count * 56);
            foreach (int index in _selection) markup.Append(FormatTile(_tiles[index]));
            _word.SetText(markup.ToString());
            RefreshControls();
        }

        public void ClearSelection()
        {
            if (_selection.Count == 0 || _word == null) return;
            ResetSelection();
        }

        void ResetSelection()
        {
            _selection.Clear();
            _used.Clear();
            for (int index = 0; index < _tiles.Length; index++) SetTileState(index, false);
            if (_word != null) _word.ClearText();
            RefreshControls();
        }

        void SetTileState(int index, bool selected)
        {
            TileEntry tile = _tiles[index];
            if (tile == null) return;
            if (tile.LetterText != null)
                tile.LetterText.SetAppearanceStyle(selected ? _selectedTileStyle : _normalTileStyle);
            if (tile.Button != null) tile.Button.interactable = !selected;
        }

        void RefreshControls()
        {
            bool hasSelection = _selection.Count > 0;
            if (_undoButton != null) _undoButton.interactable = hasSelection;
            if (_clearButton != null) _clearButton.interactable = hasSelection;
        }

        static string FormatTile(TileEntry tile)
        {
            return tile.Letter + "<sub><appear size start=2 end=1>" + tile.Score + "</appear></sub>";
        }
    }
}
