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

        // 2D sound kept by the caller (loops): fade its volume with SetVolume and end it with Stop. Null when unset.
        public IAudioPlayer Start(SoundID id, float volume = 1f, float fadeIn = 0f)
        {
            if (!id.IsValid())
                return null;
            IAudioPlayer player = fadeIn > 0f ? BroAudio.Play(id, fadeIn) : BroAudio.Play(id);
            if (volume != 1f)
                player.SetVolume(volume);
            return player;
        }

        public void SetVolume(IAudioPlayer player, float volume, float fadeTime = 0f)
        {
            if (player != null && player.IsActive)
                player.SetVolume(volume, fadeTime);
        }

        public void Stop(IAudioPlayer player, float fadeOut = 0f)
        {
            if (player != null && player.IsActive)
                player.Stop(fadeOut);
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
