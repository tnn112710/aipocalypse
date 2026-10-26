namespace Aipocalypse.Core.Debugging
{
    // Held-key repeat for the console's editing keys: fires on the press, then after delay every interval, at most once a frame.
    public sealed class KeyRepeat
    {
        float _held;
        float _next;

        public bool Step(bool pressed, bool held, float deltaTime, float delay, float interval)
        {
            if (pressed)
            {
                _held = 0f;
                _next = delay;
                return true;
            }
            if (!held)
                return false;
            _held += deltaTime;
            if (_held < _next)
                return false;
            _next += interval;
            if (_next < _held)
                _next = _held;
            return true;
        }
    }
}
