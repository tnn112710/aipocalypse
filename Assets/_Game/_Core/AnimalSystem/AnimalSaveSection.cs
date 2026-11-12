using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.SaveSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Saves the herd's orders (gate open, bell rung) and each animal by its SaveId: where it stands, the Tokens it holds and
    // whether the player sent it home.
    public sealed class AnimalSaveSection : SaveSection
    {
        const string Key = "animals";

        readonly AnimalHerd _herd;
        readonly AnimalManager _manager;

        public AnimalSaveSection(SaveRegistry registry, AnimalHerd herd, AnimalManager manager) : base(registry)
        {
            _herd = herd;
            _manager = manager;
        }

        public override int Order => SaveOrder.World;

        public override void Capture(SaveData save)
        {
            var saved = new HerdSave { GateOpen = _herd.Orders.GateOpen, Called = _herd.Orders.Called };
            foreach (Animal animal in _herd.Animals)
            {
                if (!animal.TryGetComponent(out SaveId id) || !id.HasId)
                    continue;
                Transform transform = animal.transform;
                saved.Animals.Add(new AnimalSave
                {
                    Id = id.Id,
                    Position = transform.position,
                    Yaw = transform.eulerAngles.y,
                    Held = animal.Earnings.Held,
                    SentHome = animal.SentHome,
                });
            }
            save.Set(Key, saved);
        }

        public override void Restore(SaveData save)
        {
            // The clock was restored before this section: don't earn for the jump.
            _manager.ForgetClock();
            if (!save.TryGet(Key, out HerdSave saved) || saved == null)
                return;
            _herd.Orders.Restore(saved.GateOpen, saved.Called);
            if (_herd.Pen != null && _herd.Pen.Gate != null)
                _herd.Pen.Gate.Restore(saved.GateOpen);
            var byId = new Dictionary<string, AnimalSave>(saved.Animals.Count);
            foreach (AnimalSave entry in saved.Animals)
                if (entry?.Id != null)
                    byId[entry.Id] = entry;
            foreach (Animal animal in _herd.Animals)
                if (animal.TryGetComponent(out SaveId id) && id.HasId && byId.TryGetValue(id.Id, out AnimalSave entry))
                    animal.RestoreSaved(entry.Position, entry.Yaw, entry.Held, entry.SentHome);
        }

        [Serializable]
        public sealed class HerdSave
        {
            public bool GateOpen;
            public bool Called;
            public List<AnimalSave> Animals = new();
        }

        [Serializable]
        public sealed class AnimalSave
        {
            public string Id;
            public Vector3 Position;
            public float Yaw;
            public float Held;
            public bool SentHome;
        }
    }
}
