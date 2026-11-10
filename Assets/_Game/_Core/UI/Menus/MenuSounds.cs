using MB13.Aipocalypse.Core.AudioSystem;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // On a menu's root (pause menu, main menu): its sounds, from MenuStyle, through AudioService. MenuButtons below find it.
    public sealed class MenuSounds : MonoBehaviour
    {
        [SerializeField] MenuStyle _style;

        AudioService _audio;

        [Inject]
        void Construct(AudioService audio) => _audio = audio;

        public void Hover() => _audio?.Play(_style.HoverSound);
        public void Click() => _audio?.Play(_style.ClickSound);
        public void Back() => _audio?.Play(_style.BackSound);
    }
}
