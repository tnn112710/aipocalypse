using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Input;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // Shows what the active player can use right now ("[E] Pick up Rifle", and a hold use under it), plus, above it, what the
    // hammer in hand would repair while the attack button is held ("[LMB] Repair Fence 63%"), and below it what the focus offers to collect ("[C] Collect
    // Tokens"). Polled: the focus changes as the player looks around.
    public sealed class InteractPromptHud : PlayerHudView
    {
        [SerializeField] InteractPromptView _view;
        [SerializeField, Tooltip("Optional: the repair tool's prompt, on its own key")] InteractPromptView _toolView;
        [SerializeField, Tooltip("Optional: what the focus offers to collect, on the Collect key")] InteractPromptView _collectView;

        InputService _input;

        [Inject]
        void Construct(InputService input) => _input = input;

        protected override void Start()
        {
            _view.Hide();
            _view.SetKey(_input?.GetInteractKeyLabel() ?? "E");
            if (_toolView != null)
            {
                _toolView.Hide();
                _toolView.SetKey(_input?.GetKeyLabel(GameButton.Attack) ?? "LMB");
            }
            if (_collectView != null)
            {
                _collectView.Hide();
                _collectView.SetKey(_input?.GetKeyLabel(GameButton.Collect) ?? "C");
            }
            base.Start();
        }

        protected override void Unbind(IPlayer player)
        {
            _view.Hide();
            if (_toolView != null)
                _toolView.Hide();
            if (_collectView != null)
                _collectView.Hide();
        }

        // Hidden while a menu (the inventory) holds the UI: the player isn't looking around then.
        void LateUpdate()
        {
            bool menu = _input != null && _input.IsUIRequested;
            ShowInteract(Player != null && !menu ? Player.Interactor : null);
            ShowTool(Player != null && !menu ? Player.Repair : null);
            ShowCollect(Player != null && !menu ? Player.Interactor : null);
        }

        void ShowCollect(PlayerInteractor interactor)
        {
            if (_collectView == null)
                return;
            if (interactor == null || !interactor.HasCollectPrompt)
                _collectView.Hide();
            else
                _collectView.Show(interactor.CollectPrompt.Verb, interactor.CollectPrompt.Target);
        }

        void ShowInteract(PlayerInteractor interactor)
        {
            if (interactor == null || (!interactor.HasPrompt && !interactor.HasHoldPrompt))
            {
                _view.Hide();
                return;
            }

            InteractionPrompt prompt = interactor.HasPrompt ? interactor.Prompt : default;
            InteractionPrompt hold = interactor.HasHoldPrompt ? interactor.HoldPrompt : default;
            _view.Show(prompt.Verb, prompt.Target, hold.Verb, hold.Target);
        }

        void ShowTool(PlayerRepair repair)
        {
            if (_toolView == null)
                return;
            if (repair == null || !repair.HasPrompt)
                _toolView.Hide();
            else
                _toolView.Show(repair.Prompt.Verb, repair.Prompt.Target);
        }
    }
}
