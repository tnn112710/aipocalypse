using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Entities
{
    // A character walking away from a one-off clip (a dog's act, a robot's emote): the legs take over instead of the pose
    // sliding along. Only once the clip has played `settle` seconds, so stopping into it (still slowing down) doesn't cut it.
    public static class WalkOff
    {
        const float Blend = 0.2f;

        public static bool Due(float moving, float leaveSpeed, float secondsIn, float settle) => moving > leaveSpeed && secondsIn >= settle;

        // Cross-fades to `locomotion` when the current state is one of `states` and it's Due; true when it did.
        public static bool Apply(Animator animator, HashSet<int> states, int locomotion, float moving, float leaveSpeed, float settle)
        {
            if (moving <= leaveSpeed || animator.IsInTransition(0))
                return false;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (!states.Contains(info.shortNameHash) || !Due(moving, leaveSpeed, info.normalizedTime * info.length, settle))
                return false;
            animator.CrossFadeInFixedTime(locomotion, Blend);
            return true;
        }
    }
}
