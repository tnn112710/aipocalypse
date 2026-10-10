using UnityEngine;

namespace TextStudio
{
    /// <summary>One particle in the active UI renderer's local units relative to an event anchor.</summary>
    public readonly struct TextStudioReactionUIParticle
    {
        public readonly Vector2 Offset;
        public readonly float Size;
        public readonly Color32 Color;

        /// <summary>Creates one UI particle with a local offset, size, and color relative to its event anchor.</summary>
        public TextStudioReactionUIParticle(Vector2 offset, float size, Color32 color)
        {
            Offset = offset;
            Size = size;
            Color = color;
        }
    }

    /// <summary>A versioned output owned by the active UI renderer.</summary>
    public readonly struct TextStudioReactionUIParticleLease
    {
        readonly ITextStudioReactionUIParticleOutput _owner;
        readonly int _slot;
        readonly uint _version;

        /// <summary>Creates a versioned UI particle lease for an output provider slot.</summary>
        public TextStudioReactionUIParticleLease(
            ITextStudioReactionUIParticleOutput owner, int slot, uint version)
        {
            _owner = owner;
            _slot = slot;
            _version = version;
        }

        public bool IsValid => _owner != null && _slot >= 0 && _version != 0;

        /// <summary>Attempts to write a particle at the specified index while this lease is valid.</summary>
        public bool TrySet(int index, in TextStudioReactionUIParticle particle) =>
            IsValid && _owner.TrySet(_slot, _version, index, in particle);

        /// <summary>Publishes the particle values written through this valid lease.</summary>
        public bool Commit() => IsValid && _owner.Commit(_slot, _version);

        /// <summary>Releases the particle output through its owner when this lease is valid.</summary>
        public void Release()
        {
            if (IsValid) _owner.Release(_slot, _version);
        }
    }

    /// <summary>Renderer-neutral commands write particles without owning UI elements.</summary>
    public interface ITextStudioReactionUIParticleOutput
    {
        /// <summary>Attempts to write one particle into the output identified by its slot and version.</summary>
        bool TrySet(int slot, uint version, int index,
            in TextStudioReactionUIParticle particle);
        /// <summary>Publishes the particles written to the specified versioned output.</summary>
        bool Commit(int slot, uint version);
        /// <summary>Releases the UI particle output identified by its slot and version.</summary>
        void Release(int slot, uint version);
    }

    /// <summary>Optional host service for acquiring bounded particle output from the active UI renderer.</summary>
    public interface ITextStudioReactionUIParticleHost
    {
        /// <summary>Reports whether the UI renderer can provide a particle output of the requested size.</summary>
        bool CanAcquireReactionUIParticles(int count);
        /// <summary>Attempts to acquire a bounded UI particle lease anchored to the supplied reaction position.</summary>
        bool TryAcquireReactionUIParticles(in TextStudioReactionAnchor anchor,
            int count, out TextStudioReactionUIParticleLease lease);
    }
}
