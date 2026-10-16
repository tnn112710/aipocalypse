using Ami.BroAudio;
using UnityEngine;

namespace Aipocalypse.Core.AudioSystem
{
    // Game code plays sounds through this, never BroAudio's static facade: sounds are BroAudio entities picked as SoundIDs
    // (libraries in Data/Audio), and BroAudio pools the players. Unset ids are silent.
    public sealed class AudioService
    {
        // 2D: the listener's own sounds (held weapon, footsteps, UI).
        public void Play(SoundID id, float volume = 1f)
        {
            if (!id.IsValid())
                return;
            IAudioPlayer player = BroAudio.Play(id);
            if (volume != 1f)
                player.SetVolume(volume);
        }

        // 3D one-shot at a point in the world.
        public void PlayAt(SoundID id, Vector3 position, float volume = 1f)
        {
            if (!id.IsValid())
                return;
            IAudioPlayer player = BroAudio.Play(id, position);
            if (volume != 1f)
                player.SetVolume(volume);
        }
    }
}
