using System;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Starts authored specimens through the native preview lifecycle without building or replacing their content.</summary>
    [ExecuteAlways, AddComponentMenu("")]
    // Native text hosts must finish OnEnable before this controller starts their playback.
    [DefaultExecutionOrder(100)]
    public sealed class TextStudioSamplePlayback : MonoBehaviour
    {
        [Serializable]
        public struct Specimen
        {
            public TextStudioText Text;
            public TextStudioPreviewSegment Segments;
            public bool Repeat;
            public bool HoldSnapshot;
            [Range(0, 1)] public float SnapshotProgress;
            public int ActiveDurationMultiplier;
        }

        [SerializeField] Specimen[] _specimens = Array.Empty<Specimen>();
        [SerializeField] TextStudioText _eventText;
        [SerializeField] Graphic _lantern;
        [SerializeField] string _marker = "sample_lantern";
        [SerializeField] Color _unlit = new Color(.35f, .39f, .30f, 1);
        [SerializeField] Color _lit = new Color(1, .70f, .15f, 1);
        public Specimen[] Specimens => _specimens;
        public bool LanternLit { get; private set; }

        public void Configure(Specimen[] specimens, TextStudioText eventText = null, Graphic lantern = null)
        {
            Unsubscribe();
            _specimens = specimens ?? Array.Empty<Specimen>();
            _eventText = eventText;
            _lantern = lantern;
            if (isActiveAndEnabled) { Subscribe(); Restart(); }
        }

        void OnEnable() { Subscribe(); Restart(); }
        void OnDisable()
        {
            Unsubscribe();
            foreach (Specimen specimen in _specimens)
                if (specimen.Text != null) specimen.Text.Preview.Pause();
        }

        void Subscribe() { if (_eventText != null) _eventText.OnMarkerReached += OnMarker; }
        void Unsubscribe() { if (_eventText != null) _eventText.OnMarkerReached -= OnMarker; }
        void OnMarker(string id, string[] parameters)
        {
            if (id != TextStudioBuiltInActionIds.Event || parameters == null || parameters.Length == 0 || parameters[0] != _marker) return;
            LanternLit = true;
            if (_lantern != null) _lantern.color = _lit;
        }

        public void Restart()
        {
            LanternLit = false;
            if (_lantern != null) _lantern.color = _unlit;
            foreach (Specimen specimen in _specimens)
            {
                if (specimen.Text == null) continue;
                if (specimen.ActiveDurationMultiplier > 0)
                    specimen.Text.Preview.ActiveDurationMultiplier = specimen.ActiveDurationMultiplier;
                specimen.Text.Preview.SetSegments(specimen.Segments);
                specimen.Text.Preview.Repeat = specimen.Repeat;
                if (specimen.HoldSnapshot) specimen.Text.Preview.Scrub(specimen.SnapshotProgress);
                else specimen.Text.Preview.Restart();
            }
        }
    }
}
