using UnityEngine;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UI.Button;
using UITKButton = UnityEngine.UIElements.Button;

namespace TextStudio.Examples.Whisperwood
{
    public sealed partial class TextStudioWhisperwoodDemo
    {
        [Header("Moon Gate")]
        [SerializeField] RectTransform _canvasFinalePanel;
        [SerializeField] TextStudioText _canvasInscription;
        [SerializeField] TextStudioText _canvasFinaleReward;
        [SerializeField] UIButton _canvasFinaleContinueButton;
        VisualElement _uitkFinalePanel;
        TextStudioBinding _uitkInscription;
        TextStudioBinding _uitkFinaleReward;
        UITKButton _uitkFinaleContinueButton;
        bool _finaleCompleted;
        bool _inscriptionOverlayPauseOwned;

        public bool FinaleCompleted => _finaleCompleted;
        public bool IsShowingFinale => _mode == DemoMode.Finale;

        public void ConfigureFinale(RectTransform panel, TextStudioText inscription, TextStudioText reward, UIButton button)
        {
            _canvasFinalePanel = panel;
            _canvasInscription = inscription;
            _canvasFinaleReward = reward;
            _canvasFinaleContinueButton = button;
        }

        void BeginGateFinale()
        {
            _mode = DemoMode.Finale;
            _finaleCompleted = false;
            SetHudVisible(true);
            SetNearby(string.Empty);
            SetWorldOverlaysVisible(false);
            SetFinaleVisible(true);
            ShowInstant(_canvasFinaleReward, _uitkFinaleReward, "");
            string[] collected = new string[NpcCount];
            for (int i = 0; i < collected.Length; i++) collected[i] = NpcAt(i).Word;
            string words = string.Join("   ", collected);
            if (_backend == Backend.Canvas) _canvasInscription?.ShowText(words);
            else _uitkInscription?.ShowText(words);
            SetFinaleButtonLabel("Skip inscription");
            SetPrompt("The words settle into the moonstone.");
        }

        void UpdateGateFinale()
        {
            if (_mode != DemoMode.Finale || _finaleCompleted) return;
            bool revealing = _backend == Backend.Canvas ? _canvasInscription != null && _canvasInscription.IsRevealing
                : _uitkInscription != null && _uitkInscription.IsRevealing;
            if (!revealing) FinishGateFinale();
        }

        void FinishGateFinale()
        {
            if (_finaleCompleted) return;
            _finaleCompleted = true;
            const string reward = "<pop>The path is yours.</pop>";
            if (_backend == Backend.Canvas) _canvasFinaleReward?.ShowText(reward);
            else _uitkFinaleReward?.ShowText(reward);
            SetFinaleButtonLabel("Step through");
            SetPrompt("Mara, Luma and Rowan have shown you the way.");
        }

        public void ContinueFinale()
        {
            if (_mode != DemoMode.Finale) return;
            if (_finaleCompleted) { ShowCredits(); return; }
            if (_backend == Backend.Canvas) _canvasInscription?.SkipReveal();
            else _uitkInscription?.SkipReveal();
            FinishGateFinale();
        }

        void SetFinaleVisible(bool visible)
        {
            if (_canvasFinalePanel != null) _canvasFinalePanel.gameObject.SetActive(visible);
            if (_uitkFinalePanel != null) _uitkFinalePanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) SetPrompt(_finaleCompleted ? "Mara, Luma and Rowan have shown you the way." : "The words settle into the moonstone.");
        }

        void SetFinaleButtonLabel(string label)
        {
            _canvasFinaleContinueButton?.GetComponentInChildren<TextStudioText>(true)?.ShowTextInstant(label);
            if (_uitkFinaleContinueButton != null) _uitkFinaleContinueButton.text = label;
        }

        void PauseInscription()
        {
            if (_inscriptionOverlayPauseOwned) return;
            _inscriptionOverlayPauseOwned = _backend == Backend.Canvas ? _canvasInscription != null && _canvasInscription.IsRevealing
                : _uitkInscription != null && _uitkInscription.IsRevealing;
            if (!_inscriptionOverlayPauseOwned) return;
            if (_backend == Backend.Canvas) _canvasInscription.PauseReveal();
            else _uitkInscription.PauseReveal();
        }

        void ResumeInscription()
        {
            if (!_inscriptionOverlayPauseOwned) return;
            _inscriptionOverlayPauseOwned = false;
            if (_backend == Backend.Canvas) _canvasInscription?.ResumeReveal();
            else _uitkInscription?.ResumeReveal();
        }

        void BindFinale()
        {
            _uitkFinalePanel = _uitkRoot.Q<VisualElement>("finale-panel");
            _uitkInscription = QueryBinding("inscription", RevealMode.ByLetter);
            _uitkFinaleReward = QueryBinding("finale-reward", RevealMode.ByWord);
            _uitkFinaleContinueButton = _uitkRoot.Q<UITKButton>("finale-continue-button");
            if (_uitkFinaleContinueButton != null) _uitkFinaleContinueButton.clicked += ContinueFinale;
        }

        void UnbindFinale()
        {
            if (_uitkFinaleContinueButton != null) _uitkFinaleContinueButton.clicked -= ContinueFinale;
            _uitkFinaleContinueButton = null;
        }
    }
}
