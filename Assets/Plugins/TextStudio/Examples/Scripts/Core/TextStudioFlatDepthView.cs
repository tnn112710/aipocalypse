using TMPro;
using UnityEngine;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Turns the existing flat specimen to expose its actual output plane.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioFlatDepthView : MonoBehaviour
    {
        [SerializeField] RectTransform[] _subjects;
        [SerializeField] TMP_Text _buttonLabel;
        bool _side;
        public bool ShowsEdge => _side;
        public void Configure(RectTransform[] subjects, TMP_Text buttonLabel)
        { _subjects = subjects; _buttonLabel = buttonLabel; ResetView(); }
        public void ToggleView() { _side = !_side; Apply(); }
        public void ResetView() { _side = false; Apply(); }
        void Apply()
        {
            if (_subjects != null)
                foreach (RectTransform subject in _subjects)
                    if (subject != null) subject.localRotation = Quaternion.Euler(0, _side ? 86 : 0, 0);
            if (_buttonLabel != null) _buttonLabel.text = _side ? "View face" : "View edge";
        }
    }
}
