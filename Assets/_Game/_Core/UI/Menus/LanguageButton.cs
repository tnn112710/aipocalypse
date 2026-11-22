using System;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // A menu button that switches the game's language: each press moves to the next one, and the label names the current
    // one in itself ("LANGUAGE: TIẾNG VIỆT"). The choice is kept for the next launch (LanguageService). Hidden while only
    // one language is offered.
    [RequireComponent(typeof(Button))]
    public sealed class LanguageButton : MonoBehaviour
    {
        static readonly LocalizedString LabelFormat = Loc.UI("menu.language");

        [SerializeField] TMP_Text _label;

        LanguageService _language;
        IDisposable _subscription;

        [Inject]
        void Construct(LanguageService language, ISubscriber<LanguageChanged> changed)
        {
            _language = language;
            if (language.Languages.Count < 2)
                gameObject.SetActive(false);
            _subscription = changed.Subscribe(_ => Refresh());
            Refresh();
        }

        void Awake() => GetComponent<Button>().onClick.AddListener(() => _language?.Step(1));

        void OnDestroy() => _subscription?.Dispose();

        void Refresh()
        {
            if (_label != null && _language != null)
                _label.text = LabelFormat.Format(_language.NameOf(_language.Current).ToUpperInvariant());
        }
    }
}
