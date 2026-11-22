using UnityEngine;

namespace MB13.Aipocalypse.Core.Interaction
{
    // Who answers a plain whistle when nothing callable is under the aim or heard near it (the dog, when it's in range).
    public interface IWhistleFallback
    {
        bool TryAnswer(GameObject actor, Vector3 eye, Vector3 aim, out IWhistleTarget target);
    }
}
