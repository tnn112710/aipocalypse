using System;
using MB13.Aipocalypse.Core.SaveSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Saves the dog: its order (and where it was told to stay), where it stands, whether it's in the yard.
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
                InYard = _roster.InYard(transform.position),
                StayPoint = dog.State.StayPoint,
            });
        }

        public override void Restore(SaveData save)
        {
            Dog dog = _roster.Dog;
            if (dog == null || dog.State == null || !save.TryGet(Key, out DogSave saved) || saved == null)
                return;
            dog.Restore(saved.Position, saved.Yaw, saved.Order, saved.StayPoint);
        }

        [Serializable]
        public sealed class DogSave
        {
            public DogOrder Order;
            public Vector3 Position;
            public float Yaw;
            public bool InYard;
            public Vector3 StayPoint;
        }
    }
}
