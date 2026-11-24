using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Tasks of the dog's tree (AgentScripts/Dogs/Dog.tree.txt), split by area: DogTasks.Fight, .Player, .Follow and
    // .Home. Tuning comes from DogSettings; the world (night, the player) from DogRoster; orders and what happened to it
    // from its State; where to be while following from its Plan.

    // Moving helpers shared by the tasks.
    static class DogWalk
    {
        public const float Timeout = 8f;

        public static bool Near(Dog dog, Vector3 point, float distance) => FlatMath.Length(point - dog.transform.position) <= distance;

        // On its feet and walking toward `point` at `gait`; false while it's still getting up.
        public static bool Go(Dog dog, Vector3 point, DogGait gait)
        {
            if (dog.Pose != DogPose.Stand)
                dog.SetPose(DogPose.Stand);
            if (!dog.Standing)
                return false;
            dog.Legs.SetGait(gait);
            dog.Legs.MoveTo(point, Timeout);
            return true;
        }

        public static bool TryGo(Dog dog, Vector3 center, float radius, DogGait gait, out Vector3 point)
        {
            if (!dog.Legs.TryFindDestination(center, radius, 0.5f, 6, out point))
                return false;
            return Go(dog, point, gait);
        }

        // Stops and settles into `pose` facing `look`. Standing and facing well away from `look`, it first steps round in
        // an arc (kept near `keepNear`, e.g. its stay spot) and settles when the arc ends.
        public static void Settle(Dog dog, DogPose pose, Vector3 look, Vector3? keepNear = null)
        {
            dog.SetSniffing(false);
            if (dog.Pose == DogPose.Stand && dog.Legs.TryArc(look, keepNear))
            {
                if (pose == DogPose.Stand)
                    return;
                dog.PendingPose = pose;
                return;
            }
            dog.Legs.Halt();
            if (pose == DogPose.Stand || dog.Pose == DogPose.Stand)
                dog.Legs.FaceInPlace(look);
            dog.SetPose(pose);
        }

        // A random spot inside the yard (shrunk by `margin`), or near the house when there's no yard.
        public static Vector3 InYard(DogRoster roster, Dog dog, float margin)
        {
            var yard = roster.Yard;
            if (yard == null)
                return roster.Home(dog) + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            Vector3 half = yard.HalfSize;
            var local = new Vector3(Random.Range(-half.x + margin, half.x - margin), 0f, Random.Range(-half.z + margin, half.z - margin));
            Vector3 world = yard.WorldToLocal.inverse.MultiplyPoint3x4(local);
            world.y = dog.transform.position.y;
            return world;
        }
    }

    // Base of the dog's yes/no conditions: Success while Check() holds. Abstract, so Behavior Designer and TreeKit
    // never list it as a task (trees store the concrete task's type name).
    public abstract class DogCondition : ConditionalNode
    {
        protected Dog _dog;

        public override void OnAwake() => _dog = GetComponent<Dog>();
        public override TaskStatus OnUpdate() => Check() ? TaskStatus.Success : TaskStatus.Failure;

        protected abstract bool Check();
    }
}
