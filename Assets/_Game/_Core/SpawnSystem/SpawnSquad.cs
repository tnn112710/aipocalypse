using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.SpawnSystem
{
    // A mixed group spawned together: members in slot order (the first entry leads), standing in a formation. The first
    // member's card decides where the group can stand.
    [CreateAssetMenu(fileName = "Squad", menuName = "Aipocalypse/Spawning/Spawn Squad")]
    public sealed class SpawnSquad : ScriptableObject
    {
        public List<SquadMember> Members = new();
        [Tooltip("How the squad stands; none = a scatter")] public Formation Formation;

        public int Count
        {
            get
            {
                int count = 0;
                foreach (SquadMember member in Members)
                    count += Mathf.Max(0, member.Count);
                return count;
            }
        }

        // The card of slot `slot` (members fill slots in order); null past the end.
        public SpawnCard CardAt(int slot)
        {
            foreach (SquadMember member in Members)
            {
                if (slot < member.Count)
                    return member.Card;
                slot -= Mathf.Max(0, member.Count);
            }
            return null;
        }

        public SpawnCard Lead
        {
            get
            {
                foreach (SquadMember member in Members)
                    if (member.Card != null && member.Count > 0)
                        return member.Card;
                return null;
            }
        }
    }

    [Serializable]
    public struct SquadMember
    {
        public SpawnCard Card;
        [Min(0)] public int Count;

        public SquadMember(SpawnCard card, int count)
        {
            Card = card;
            Count = count;
        }
    }
}
