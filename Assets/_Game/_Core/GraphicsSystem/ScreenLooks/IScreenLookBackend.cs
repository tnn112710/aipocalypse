using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks
{
    // Draws screen looks with one tool (URP volumes, SC Screen Effects, anything else). Strength 0 = off, 1 = full; called
    // every frame for every look so pulses can animate.
    public interface IScreenLookBackend : IDisposable
    {
        void Apply(ScreenLookId look, float strength);
    }

    // The factory: a settings field names one of these assets, and the asset builds its backend. A new tool is a new subclass
    // and a new asset; game code never changes.
    public abstract class ScreenLookBackendAsset : ScriptableObject
    {
        // `parent`: where scene objects the backend needs (volumes) live; destroyed with it.
        public abstract IScreenLookBackend Create(Transform parent);
    }
}
