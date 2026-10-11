using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FayLabs.Kroma
{
    /// <summary>
    /// Forces particle systems to simulate in Edit Mode when the Kroma setting is enabled.
    /// Added dynamically to spawned scene objects by KromaLightingApplier.
    /// Logic is stripped in build, but the component remains (harmless).
    /// </summary>
    [ExecuteAlways]
    public class KromaParticleController : MonoBehaviour
    {
#if UNITY_EDITOR
        private ParticleSystem[] _particles;
        private double _lastTime;
        // Hardcoded key to avoid dependency only Editor assembly (KromaConstants)
        private const string PREF_AUTO_SIMULATE_PARTICLES = "Kroma_AutoSimulateParticles";

        private void OnEnable()
        {
            // Don't interfere in Play Mode
            if (Application.isPlaying) return;

            _particles = GetComponentsInChildren<ParticleSystem>();
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
            PrewarmSystems();
        }

        public void RestartSimulation()
        {
            if (_particles == null) return;
            foreach (var ps in _particles)
            {
                if (ps != null) ps.Clear();
            }
            PrewarmSystems();
        }

        private void PrewarmSystems()
        {
            if (_particles == null) return;
            foreach (var ps in _particles)
            {
                if (ps == null) continue;
                if (ps.main.prewarm && ps.main.loop) // Prewarm usually only valid on looping systems
                {
                    // Fast-forward by duration to simulate "already running" state
                    ps.Simulate(ps.main.duration, true, true, false); 
                }
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
        }

        private void EditorUpdate()
        {
            if (this == null) // Object destroyed check
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }

            // Do nothing in Play Mode (let Unity handle it)
            if (Application.isPlaying) return;

            // Check global setting via EditorPrefs
            if (!EditorPrefs.GetBool(PREF_AUTO_SIMULATE_PARTICLES, true)) return;

            // Throttle? For now, we simulate per frame for smoothness.
            // Using EditorApplication.timeSinceStartup for delta to be robust against pure Editor time.
            float dt = (float)(EditorApplication.timeSinceStartup - _lastTime);
            _lastTime = EditorApplication.timeSinceStartup;
            
            // Clamp DT to avoid huge jumps if editor stalls
            dt = Mathf.Min(dt, 0.1f);

            if (_particles != null)
            {
                bool simulatedAny = false;
                foreach (var ps in _particles)
                {
                    if (ps == null) continue;
                    
                    // Simulate with restart = false, fixedTimeStep = false
                    ps.Simulate(dt, true, false, false);
                    simulatedAny = true;
                }
                
                // Force scene repaint if we simulated, otherwise it won't animate visually
                if (simulatedAny)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                }
            }
        }
        
        // Remove standard Update
        // private void Update() { }
#endif
    }
}
