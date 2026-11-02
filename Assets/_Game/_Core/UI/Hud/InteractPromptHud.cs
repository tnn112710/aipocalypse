using Aipocalypse.Core.Interaction;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Input;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // Shows what the active player can use right now ("[E] Pick up Rifle", and a hold use under it). Polled: the focus changes
    // as the player looks around.
    public sealed class InteractPromptHud : PlayerHudView
    {
        [SerializeField] InteractPromptView _view;

        InputService _input;

        [Inject]
        void Construct(InputService input) => _input = input;

        protected override void Start()
        {
            _view.Hide();
            _view.SetKey(_input?.GetInteractKeyLabel() ?? "E");
            base.Start();
        }

        protected override void Unbind(IPlayer player) => _view.Hide();

        void LateUpdate()
        {
            PlayerInteractor interactor = Player != null ? Player.Interactor : null;
            if (interactor == null || (!interactor.HasPrompt && !interactor.HasHoldPrompt))
            {
                _view.Hide();
                return;
            }

            InteractionPrompt prompt = interactor.HasPrompt ? interactor.Prompt : default;
            InteractionPrompt hold = interactor.HasHoldPrompt ? interactor.HoldPrompt : default;
            _view.Show(prompt.Verb, prompt.Target, hold.Verb, hold.Target);
        }
    }
}
