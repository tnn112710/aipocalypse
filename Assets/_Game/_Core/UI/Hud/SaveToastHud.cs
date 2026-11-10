using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.SaveSystem;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // "Game saved" in a corner for a moment after any save (the morning autosave too), or why it failed; fades in real time.
    public sealed class SaveToastHud : MonoBehaviour
    {
        [SerializeField] TMP_Text _text;
        [SerializeField, Min(0.1f)] float _holdSeconds = 2f;
        [SerializeField, Min(0.05f)] float _fadeSeconds = 0.6f;

        System.IDisposable _subscription;
        float _shownAt = float.NegativeInfinity;

        [Inject]
        void Construct(ISubscriber<GameSaved> saved) => _subscription = saved.Subscribe(OnSaved);

        void Awake()
        {
            _text.enabled = false;
            enabled = false;
        }

        void OnDestroy() => _subscription?.Dispose();

        void Update()
        {
            float age = Time.unscaledTime - _shownAt;
            float alpha = 1f - Mathf.Clamp01((age - _holdSeconds) / _fadeSeconds);
            _text.alpha = alpha;
            if (alpha <= 0f)
            {
                _text.enabled = false;
                enabled = false;
            }
        }

        void OnSaved(GameSaved saved)
        {
            string name = SaveFormat.SlotName(saved.Slot);
            _text.text = !saved.Ok ? saved.Message : saved.Autosave ? $"Autosaved  ·  {name}" : $"Game saved  ·  {name}";
            _text.alpha = 1f;
            _text.enabled = true;
            _shownAt = Time.unscaledTime;
            enabled = true;
        }
    }
}
