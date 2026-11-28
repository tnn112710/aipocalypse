using System;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // The yard's firewood: E takes a log, hold E takes as many as fit (a pickup: feed and sounds); it fills up to PileSize
    // again each morning (Dawn), a stand-in for chopping (user 2026-11-28). The model hides while it's empty.
    public sealed class WoodPile : MonoBehaviour, IInteractable, IHoldInteractable
    {
        static readonly LocalizedString TakeVerb = Loc.Interaction("verb.take");
        static readonly LocalizedString TakeAllVerb = Loc.Interaction("verb.take_all");

        [SerializeField] CampfireSettings _settings;
        [SerializeField, Tooltip("What it gives")] MaterialDefinition _wood;
        [SerializeField, Tooltip("Hidden while the pile is empty")] GameObject _model;

        int _stock = -1;
        CampfireRegistry _registry;
        IDisposable _subscription;
        string _promptTarget;
        int _promptStock = -1;
        Locale _promptLocale;

        public int Stock => _stock >= 0 ? _stock : _stock = Full;
        public MaterialDefinition Wood => _wood;
        int Full => _settings != null ? _settings.PileSize : 8;

        [Inject]
        void Construct(CampfireRegistry registry, ISubscriber<DayPhaseChanged> phases)
        {
            _registry = registry;
            registry.Add(this);
            _subscription = phases.Subscribe(change => OnDayPhase(change.Current));
        }

        void Awake() => ShowModel();

        void OnDestroy()
        {
            _registry?.Remove(this);
            _subscription?.Dispose();
        }

        public void OnDayPhase(DayPhase phase)
        {
            if (phase == DayPhase.Dawn)
                SetStock(Full);
        }

        // Gives up to `count` logs, as many as fit; how many were taken.
        public int Take(Equipment equipment, int count)
        {
            count = Mathf.Min(count, Stock);
            if (equipment == null || _wood == null || count <= 0)
                return 0;
            ItemInstance logs = _wood.CreateInstance(count);
            equipment.TryPickUp(logs, out int added);
            SetStock(Stock - added);
            return added;
        }

        // A save's stock, silently.
        public void SetStock(int stock)
        {
            _stock = Mathf.Max(0, stock);
            ShowModel();
        }

        void ShowModel()
        {
            if (_model != null && _model.activeSelf != Stock > 0)
                _model.SetActive(Stock > 0);
        }

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (Stock <= 0 || _wood == null || !actor.TryGetComponent(out IEquipmentOwner owner) || owner.Equipment == null)
                return false;
            if (_promptStock != Stock || _promptLocale != Loc.Current)
            {
                _promptStock = Stock;
                _promptLocale = Loc.Current;
                _promptTarget = $"{_wood.DisplayName} ({Stock})";
            }
            prompt = new InteractionPrompt(TakeVerb.Text(), _promptTarget);
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (actor.TryGetComponent(out IEquipmentOwner owner))
                Take(owner.Equipment, 1);
        }

        // Hold: the whole pile (what fits). Nothing to hold for with one log left: the tap takes it.
        public bool TryGetHoldPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (Stock <= 1 || !TryGetPrompt(actor, out InteractionPrompt tap))
                return false;
            prompt = new InteractionPrompt(TakeAllVerb.Text(), tap.Target);
            return true;
        }

        public void InteractHold(GameObject actor)
        {
            if (actor.TryGetComponent(out IEquipmentOwner owner))
                Take(owner.Equipment, Stock);
        }
    }
}
