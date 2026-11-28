namespace MB13.Aipocalypse.Core.Stats
{
    // Any player meter (health, stamina, the needs): read and set the same way by cheats, the HUD and scenarios.
    public interface IMeter
    {
        float Current { get; }
        float Max { get; }
        float Normalized { get; }

        void Set(float current);
    }
}
