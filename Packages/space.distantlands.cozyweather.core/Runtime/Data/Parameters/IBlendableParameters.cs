namespace DistantLands.Cozy
{
    /// <summary>
    /// Implement on parameters structs that need to be able to be blended between biomes
    /// </summary>
    public interface IBlendableParameters<T>
    {
        public void Blend(T newValue, float weight);
    }
}