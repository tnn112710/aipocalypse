using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.TimeSystem;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.VFX;
using VContainer;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // What a tap of the interact key does at a campfire.
    public enum CampfireAction
    {
        None,
        AddWood,
        Light,
    }

    // A fire pit the player tends (docs/technicals/CAMPFIRE.md). Tap E: put a log from the inventory on it (when one fits),
    // else light it; hold E: put it out (the fuel stays). Wood thrown into it burns. While lit it burns fuel (CampfireTicker),
    // lights the area, keeps the player sane within its SafeLight and crackles; below ShrinkBelow of the max all of that
    // shrinks with the fuel; the logs in the pit show the fuel left. Without the scene's CampfireInstaller it's a prop: nothing
    // ticks or injects it, and wood thrown in just lands.
    public sealed class Campfire : MonoBehaviour, IInteractable, IHoldInteractable
    {
        static readonly LocalizedString AddWoodVerb = Loc.Interaction("verb.add_wood");
        static readonly LocalizedString LightVerb = Loc.Interaction("verb.light_fire");
        static readonly LocalizedString PutOutVerb = Loc.Interaction("verb.put_out");
        static readonly LocalizedString FireVerb = Loc.Interaction("verb.fire");
        static readonly LocalizedString FireTarget = Loc.Interaction("target.fire");
        static readonly LocalizedString FireFuelTarget = Loc.Interaction("target.fire_fuel");
        static readonly LocalizedString FuelTarget = Loc.Interaction("target.fuel");
        static readonly LocalizedString BurningTarget = Loc.Interaction("target.fire_burning");
        static readonly LocalizedString NeedsWoodTarget = Loc.Interaction("target.fire_needs_wood");
        const string EmbersRate = "AmbersRate";   // the GA flame's exposed name

        [SerializeField] CampfireSettings _settings;
        [SerializeField, Tooltip("The sanity safe zone; enabled only while lit, its radius set from the settings")] SafeLight _safeLight;
        [SerializeField, Tooltip("Warm light near the flame: the hot pool around the pit")] Light _core;
        [SerializeField, Tooltip("Wide light high above the fire: lights the yard evenly (~20 m)")] Light _fill;
        [SerializeField, Tooltip("Flame effect: plays while lit, stops (its particles die out) when out")] VisualEffect _flame;
        [SerializeField, Tooltip("Scaled with the fire's strength (the flame's parent)")] Transform _flameRoot;
        [SerializeField, Tooltip("Loop while burning, at the pit (3D)")] SoundID _crackle;
        [SerializeField] SoundID _ignite;
        [SerializeField] SoundID _addWood;
        [SerializeField] SoundID _putOut;
        [SerializeField, Tooltip("Now and then while a lit fire is below ShrinkBelow: it's dying")] SoundID _sputter;
        [SerializeField, Tooltip("Log models in the pit: one shows for every started share of the max (4 logs: a quarter each)")]
        GameObject[] _logs;

        CampfireFuel _fuel;
        CampfireRegistry _registry;
        AudioService _audio;
        TimeService _time;
        IAudioPlayer _crackleLoop;
        bool _cracklePaused;
        int _logsShown = -1;
        // A log just went on: 1, settling to 0 over FlareSeconds.
        float _flare;
        float _embersRate = -1f;
        float _sputterIn = -1f;
        LightShadows _fillShadows;
        bool _fillShadowsRead;
        // Eased strength the looks follow (gameplay reads the fuel's own Strength).
        float _shown;
        float _noiseSeed;
        bool _playing;
        // Prompt cache: rebuilt only when what it shows changes.
        string _promptText;
        int _promptKey = int.MinValue;
        Locale _promptLocale;

        // New game: full and unlit (user 2026-11-28); a save restores its own state.
        public CampfireFuel Fuel => _fuel ??= CreateFuel();
        public CampfireSettings Settings => _settings;

        CampfireFuel CreateFuel()
        {
            float max = _settings != null ? _settings.MaxFuel : 8f;
            var fuel = new CampfireFuel(max, _settings != null ? _settings.ShrinkBelow : 0.25f, max);
            fuel.WentOut += OnWentOut;
            return fuel;
        }

        [Inject]
        void Construct(CampfireRegistry registry, AudioService audio, TimeService time)
        {
            _registry = registry;
            _audio = audio;
            _time = time;
            registry.Add(this);
        }

        void Awake()
        {
            _noiseSeed = Random.value * 100f;
            _shown = Fuel.Strength;
            Refresh(0f, snap: true);
        }

        void OnDestroy()
        {
            _registry?.Remove(this);
            _audio?.Stop(_crackleLoop);
        }

        void Update() => Refresh(Time.deltaTime);

        // ---------------------------------------------------------------- rules

        // Burns `hours` of game time × burnScale (rain); called by CampfireTicker.
        public void Burn(float hours, float burnScale) => Fuel.Advance(hours, burnScale);

        public CampfireAction ActionFor(Inventory inventory)
        {
            if (CarriedFuel(inventory) != null)
                return CampfireAction.AddWood;
            return !Fuel.Lit && Fuel.Fuel > 0f ? CampfireAction.Light : CampfireAction.None;
        }

        // What a tap does: a log from the inventory, else light it.
        public void Tap(Inventory inventory)
        {
            switch (ActionFor(inventory))
            {
                case CampfireAction.AddWood:
                    MaterialDefinition wood = CarriedFuel(inventory);
                    if (Fuel.Add(wood.FuelHours) && inventory.Take(wood, 1) > 0)
                        Flare();
                    break;
                case CampfireAction.Light:
                    if (Fuel.Light())
                        PlayAtPit(_ignite);
                    break;
            }
        }

        public void PutOut()
        {
            if (!Fuel.Lit)
                return;
            Fuel.PutOut();
            PlayAtPit(_putOut);
        }

        // Burns the whole units of a fuel item that fit (wood thrown in); takes them off the item. How many burned.
        public int Feed(ItemInstance item)
        {
            if (item == null || item.IsEmpty || item.Definition is not MaterialDefinition { IsFuel: true } fuel)
                return 0;
            int burned = 0;
            while (burned < item.Count && Fuel.Add(fuel.FuelHours))
                burned++;
            if (burned > 0)
            {
                item.Consume(burned);
                Flare();
            }
            return burned;
        }

        // A save's state: no sounds, the looks jump to it.
        public void RestoreSaved(float fuel, bool lit)
        {
            Fuel.Restore(fuel, lit);
            Refresh(0f, snap: true);
        }

        // The first fuel stack in the inventory that fits whole.
        MaterialDefinition CarriedFuel(Inventory inventory)
        {
            if (inventory == null)
                return null;
            return FindFuel(inventory.Hotbar) ?? FindFuel(inventory.Backpack);
        }

        MaterialDefinition FindFuel(ItemContainer container)
        {
            for (int i = 0; i < container.Count; i++)
                if (container[i] is { IsEmpty: false, Definition: MaterialDefinition { IsFuel: true } fuel } && Fuel.CanAdd(fuel.FuelHours))
                    return fuel;
            return null;
        }

        void OnWentOut() => PlayAtPit(_putOut);

        void Flare()
        {
            _flare = 1f;
            PlayAtPit(_addWood);
        }

        void PlayAtPit(SoundID sound) => _audio?.PlayAt(sound, transform.position);

        // Wood thrown or dropped into the pit burns (only a working fire: without the installer the pit is a prop).
        void OnTriggerEnter(Collider other)
        {
            if (_registry == null)
                return;
            var item = other.GetComponentInParent<WorldItem>();
            if (item == null || item.IsTaken || !item.IsFlying)
                return;
            Feed(item.Item);
            if (item.Item.IsEmpty)
                Destroy(item.gameObject);
        }

        // ---------------------------------------------------------------- looks

        // Lights, flame, logs, sound and the safe zone follow the fuel. `snap` jumps the looks to the state (a load); a paused
        // frame (deltaTime 0) holds them.
        public void Refresh(float deltaTime, bool snap = false)
        {
            float strength = Fuel.Strength;
            ShowLogs();
            if (_safeLight != null)
            {
                _safeLight.enabled = Fuel.Lit;
                _safeLight.SetRadius((_settings != null ? _settings.SafeRadius : 30f) * strength);
            }
            float fade = snap || _settings == null || _settings.FadeSeconds <= 0f ? 1f : Mathf.Max(0f, deltaTime) / _settings.FadeSeconds;
            _shown = Mathf.MoveTowards(_shown, strength, fade);
            if (_settings == null)
                return;

            // The core flickers fully; the fill only by FillFlicker of it, or the whole yard pulses like a strobe.
            float dip = 0f;
            if (_shown > 0f && _settings.FlickerAmount > 0f)
            {
                float t = Time.time * _settings.FlickerSpeed;
                float noise = Mathf.PerlinNoise(t, _noiseSeed) * 0.6f + Mathf.PerlinNoise(t * 2.8f, _noiseSeed + 7.1f) * 0.4f;
                dip = _settings.FlickerAmount * noise;
            }
            _flare = Mathf.Max(0f, _flare - Mathf.Max(0f, deltaTime) / _settings.FlareSeconds);
            float flare = 1f + _settings.FlareBoost * _flare;
            SetLight(_core, _settings.CoreIntensity * _shown * (1f - dip) * flare, _settings.CoreRange * _shown);
            SetLight(_fill, _settings.FillIntensity * _shown * (1f - dip * _settings.FillFlicker) * flare, _settings.FillRange * _shown);
            ShadowOnlyAtHome();
            Embers();

            if (_flameRoot != null)
                _flameRoot.localScale = Vector3.one * Mathf.Lerp(_settings.MinFlameScale, 1f, _shown);
            bool burning = Fuel.Lit;
            if (burning != _playing)
            {
                _playing = burning;
                if (_flame != null)
                {
                    if (burning)
                        _flame.Play();
                    else
                        _flame.Stop();
                }
            }

            if (_audio == null)
                return;
            // Paused (the pause menu): the crackle pauses with the game instead of playing on.
            bool paused = _time != null && _time.IsPaused;
            if (paused != _cracklePaused)
            {
                _cracklePaused = paused;
                if (paused)
                    _audio.Pause(_crackleLoop);
                else
                    _audio.UnPause(_crackleLoop);
            }
            if (paused)
                return;
            Sputter(deltaTime);
            if (_shown > 0.01f)
            {
                if (_crackleLoop == null || !_crackleLoop.IsActive)
                    _crackleLoop = _audio.StartAt(_crackle, transform.position, _shown);
                else
                    _audio.SetVolume(_crackleLoop, _shown);
            }
            else if (_crackleLoop != null)
            {
                _audio.Stop(_crackleLoop, 0.5f);
                _crackleLoop = null;
            }
        }

        // Sparks fly up while the flare lasts: the flame's embers rate, × FlareEmbers at its top.
        void Embers()
        {
            if (_flame == null || !_flame.HasFloat(EmbersRate))
                return;
            if (_embersRate < 0f)
                _embersRate = _flame.GetFloat(EmbersRate);
            float rate = _embersRate * Mathf.Lerp(1f, _settings.FlareEmbers, _flare);
            if (!Mathf.Approximately(rate, _flame.GetFloat(EmbersRate)))
                _flame.SetFloat(EmbersRate, rate);
        }

        // Each shadowed fill light costs ~0.4 ms of GPU: only the home fire (the first) casts them.
        void ShadowOnlyAtHome()
        {
            if (_fill == null)
                return;
            if (!_fillShadowsRead)
            {
                _fillShadowsRead = true;
                _fillShadows = _fill.shadows;
            }
            LightShadows shadows = _registry == null || _registry.Home == this ? _fillShadows : LightShadows.None;
            if (_fill.shadows != shadows)
                _fill.shadows = shadows;
        }

        // A dying fire (lit, below ShrinkBelow) sputters now and then.
        void Sputter(float deltaTime)
        {
            if (!Fuel.Lit || Fuel.Share >= _settings.ShrinkBelow)
            {
                _sputterIn = -1f;
                return;
            }
            if (_sputterIn >= 0f)
            {
                _sputterIn -= deltaTime;
                if (_sputterIn > 0f)
                    return;
                PlayAtPit(_sputter);
            }
            _sputterIn = Random.Range(_settings.SputterEveryMin, Mathf.Max(_settings.SputterEveryMin, _settings.SputterEveryMax));
        }

        // One log for every started share of the max: a glance at the pit says how much is left.
        void ShowLogs()
        {
            if (_logs == null || _logs.Length == 0)
                return;
            int shown = Mathf.Clamp(Mathf.CeilToInt(Fuel.Share * _logs.Length - 1e-3f), 0, _logs.Length);
            if (shown == _logsShown)
                return;
            _logsShown = shown;
            for (int i = 0; i < _logs.Length; i++)
                if (_logs[i] != null)
                    _logs[i].SetActive(i < shown);
        }

        static void SetLight(Light light, float intensity, float range)
        {
            if (light == null)
                return;
            bool on = intensity > 0.001f && range > 0.01f;
            if (light.enabled != on)
                light.enabled = on;
            if (!on)
                return;
            light.intensity = intensity;
            light.range = range;
        }

        // ---------------------------------------------------------------- interaction

        public bool TryGetPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            Inventory inventory = actor.TryGetComponent(out IEquipmentOwner owner) ? owner.Equipment?.Inventory : null;
            CampfireAction action = ActionFor(inventory);
            LocalizedString verb;
            LocalizedString target;
            switch (action)
            {
                case CampfireAction.AddWood:
                    verb = AddWoodVerb;
                    target = FuelTarget;
                    break;
                case CampfireAction.Light:
                    verb = LightVerb;
                    target = FireFuelTarget;
                    break;
                default:
                    verb = FireVerb;
                    target = Fuel.Lit ? BurningTarget : Fuel.Fuel > 0f ? FireFuelTarget : NeedsWoodTarget;
                    break;
            }
            prompt = new InteractionPrompt(verb.Text(), PromptTarget(target, (int)action));
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (actor.TryGetComponent(out IEquipmentOwner owner) && owner.Equipment != null)
                Tap(owner.Equipment.Inventory);
        }

        public bool TryGetHoldPrompt(GameObject actor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!Fuel.Lit)
                return false;
            prompt = new InteractionPrompt(PutOutVerb.Text(), FireTarget.Text());
            return true;
        }

        public void InteractHold(GameObject actor) => PutOut();

        // "(3.0 / 8 h)": rebuilt when the tenths shown, the action or the language change, not every frame.
        string PromptTarget(LocalizedString target, int action)
        {
            int tenths = Mathf.CeilToInt(Fuel.Fuel * 10f);
            int key = (tenths << 4) | (action << 2) | (Fuel.Lit ? 1 : 0) | (ReferenceEquals(target, NeedsWoodTarget) ? 2 : 0);
            if (key == _promptKey && _promptLocale == Loc.Current && _promptText != null)
                return _promptText;
            _promptKey = key;
            _promptLocale = Loc.Current;
            _promptText = string.Format(target.Text(), (tenths / 10f).ToString("0.0"), Fuel.Max.ToString("0.#"));
            return _promptText;
        }
    }
}
