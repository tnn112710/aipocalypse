using System;
using MB13.Aipocalypse.Core.SaveSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Saves the dog: its order (and where it was told to stay, how long it had been herding), where it stands, its fight
    // state, and the night it last rolled a howl.
    public sealed class DogSaveSection : SaveSection
    {
        public const string Key = "dog";

        readonly DogRoster _roster;

        public DogSaveSection(SaveRegistry registry, DogRoster roster) : base(registry) => _roster = roster;

        public override int Order => SaveOrder.World;

        public override void Capture(SaveData save)
        {
            Dog dog = _roster.Dog;
            if (dog == null || dog.State == null)
                return;
            Transform transform = dog.transform;
            save.Set(Key, new DogSave
            {
                Order = dog.State.Order,
                Position = transform.position,
                Yaw = transform.eulerAngles.y,
                StayPoint = dog.State.StayPoint,
                HerdFor = dog.State.HerdingFor(Time.time),
                HowledNight = dog.State.HowledNight,
                Stance = dog.State.Stance,
                Toughness = dog.State.Toughness,
                Down = dog.State.Down,
                LimpLeft = dog.State.LimpLeft(Time.time),
            });
        }

        public override void Restore(SaveData save)
        {
            Dog dog = _roster.Dog;
            if (dog == null || dog.State == null || !save.TryGet(Key, out DogSave saved) || saved == null)
                return;
            dog.Restore(saved.Position, saved.Yaw, saved.Order, saved.StayPoint, saved.HerdFor, saved.HowledNight);
            dog.State.RestoreFight(saved.Stance, saved.Toughness, saved.Down, saved.LimpLeft, Time.time);
        }

        [Serializable]
        public sealed class DogSave
        {
            public DogOrder Order;
            public Vector3 Position;
            public float Yaw;
            public Vector3 StayPoint;
            public float HerdFor;
            public int HowledNight = -1;
            public DogStance Stance;
            public float Toughness = 100f;
            public bool Down;
            public float LimpLeft;
        }
    }
}
