namespace Aipocalypse.Core.EnvironmentSystem
{
    // Changes the time-of-day look before it is applied: weather, local zones (caves, swamp), special nights.
    // Add with EnvironmentDirector.AddLayer; layers run from low Order to high. Prefer relative changes (×, lerp toward)
    // so one layer works at any hour.
    public interface IAtmosphereLayer
    {
        int Order { get; }
        void Modify(ref AtmosphereState state, float hour);
    }
}
