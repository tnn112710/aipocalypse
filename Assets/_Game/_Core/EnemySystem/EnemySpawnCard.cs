using Aipocalypse.Core.SpawnSystem;
using Ami.BroAudio;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // One spawnable kind of enemy for raids and the console: a SpawnCard (looks by weight, group size, placement) plus the cue
    // that announces it. Lives with the enemy in Content/Enemies/<Name>/<Name>_Card.asset.
    [CreateAssetMenu(fileName = "Card", menuName = "Aipocalypse/Enemies/Spawn Card")]
    public sealed class EnemySpawnCard : SpawnCard
    {
        [Tooltip("Played once per raid wave this kind is in, at its first group's spawn point (goblins: a war horn)")]
        public SoundID ArrivalCue;
    }
}
