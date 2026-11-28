namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // A volume a kept audio player follows: sends a new value only when it moved at least a step (or reached silence), so a
    // fade isn't restarted every frame (BroAudio restarts its fade on each SetVolume).
    public struct VolumeStep
    {
        float _sent;
        bool _started;

        public bool Next(float target, float step, out float send)
        {
            send = target;
            bool due = !_started || (target <= 0f && _sent > 0f) || System.Math.Abs(target - _sent) >= step;
            if (!due)
                return false;
            _sent = target;
            _started = true;
            return true;
        }

        public void Reset() => _started = false;
    }
}
