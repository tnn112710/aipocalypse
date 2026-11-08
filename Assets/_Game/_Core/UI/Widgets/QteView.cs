using MB13.Aipocalypse.Core.QuickTimeSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    public abstract class QteView : MonoBehaviour
    {
        [SerializeField] string _presentationId;
        public string PresentationId => _presentationId;
        public abstract bool CanDisplay(IQteSession session);
        public abstract void Display(IQteSession session);
        public virtual void Flash(Color color, float amount) { }
        public virtual void DisplaySuccess(IQteSession session) => Display(session);
    }
}
