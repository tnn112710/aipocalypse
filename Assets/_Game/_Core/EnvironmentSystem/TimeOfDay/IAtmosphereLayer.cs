namespace Aipocalypse.Core.EnvironmentSystem
{
    // Changes the time-of-day look before it's applied (weather, local zones, special nights). Added with
    // EnvironmentDirector.AddLayer, run by ascending Order; prefer relative changes so a layer works at any hour.
    public interface IAtmosphereLayer
    {
        int Order { get; }
        void Modify(ref AtmosphereState state, float hour);
    }
}
