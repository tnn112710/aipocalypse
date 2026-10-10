using System.Collections;
using UnityEngine;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Sequences gameplay values while the native text host owns their presentation.</summary>
    [RequireComponent(typeof(TextStudioText)), DefaultExecutionOrder(100), AddComponentMenu("")]
    public sealed class TextStudioCountdownRecipe : MonoBehaviour
    {
        Coroutine _sequence;
        TextStudioText _text;
        public bool IsRunning => _sequence != null;

        void OnEnable()
        {
            _text = GetComponent<TextStudioText>();
            if (Application.isPlaying) Restart();
        }

        void OnDisable()
        {
            StopSequence();
            if (_text != null) _text.PauseReveal();
        }

        [ContextMenu("Restart countdown")]
        public void Restart()
        {
            StopSequence();
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            _text = GetComponent<TextStudioText>();
            _sequence = StartCoroutine(CountDown());
        }

        void StopSequence()
        {
            if (_sequence != null) StopCoroutine(_sequence);
            _sequence = null;
        }

        IEnumerator CountDown()
        {
            WaitForSecondsRealtime second = new WaitForSecondsRealtime(1);
            for (int value = 3; value > 0; value--)
            {
                _text.ShowText("<pop>" + value + "</pop>");
                yield return second;
            }
            _text.ShowText("<pop><color=#C75543>GO!</color></pop>");
            _sequence = null;
        }
    }
}
