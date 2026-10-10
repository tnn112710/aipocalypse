using UnityEngine;
using UnityEngine.EventSystems;

namespace TextStudio.Examples.Whisperwood
{
    [AddComponentMenu("")]
    public sealed class TextStudioWhisperwoodDialogueClick : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] TextStudioWhisperwoodDemo _demo;
        public void Configure(TextStudioWhisperwoodDemo demo) => _demo = demo;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && _demo != null && !_demo.IsJournalOpen) _demo.Advance();
        }
    }
}
