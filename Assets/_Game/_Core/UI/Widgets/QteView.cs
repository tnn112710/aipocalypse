using Aipocalypse.Core.QuickTimeSystem;
using UnityEngine;

namespace Aipocalypse.Core.UI
{
    public abstract class QteView : MonoBehaviour
    {
        [SerializeField] string _presentationId;
        public string PresentationId => _presentationId;
        public abstract bool CanDisplay(IQteSession session);
        public abstract void Display(IQteSession session);
    }
}
