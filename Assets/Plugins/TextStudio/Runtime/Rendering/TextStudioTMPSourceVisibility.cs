using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Owns suppression of every native TMP draw while a complete replacement is visible.</summary>
    internal sealed class TextStudioTMPSourceVisibility
    {
        readonly List<TMP_SubMesh> _subMeshes = new List<TMP_SubMesh>();
        readonly List<TMP_SubMeshUI> _subGraphics = new List<TMP_SubMeshUI>();
        readonly List<MeshRenderer> _suppressedMeshes = new List<MeshRenderer>();
        readonly List<bool> _meshForceRenderingOff = new List<bool>();
        readonly List<CanvasRenderer> _hiddenCanvases = new List<CanvasRenderer>();
        readonly List<float> _canvasAlphas = new List<float>();

        internal void Synchronize(TMP_Text text, bool hidden)
        {
            if (!hidden || text == null) { Restore(); return; }
            if (text is TextMeshProUGUI)
            {
                Hide(text.GetComponent<CanvasRenderer>());
                text.GetComponentsInChildren(true, _subGraphics);
                foreach (TMP_SubMeshUI sub in _subGraphics) Hide(sub.canvasRenderer);
            }
            else
            {
                Hide(text.GetComponent<MeshRenderer>());
                text.GetComponentsInChildren(true, _subMeshes);
                foreach (TMP_SubMesh sub in _subMeshes) Hide(sub.GetComponent<MeshRenderer>());
            }
        }

        void Hide(MeshRenderer renderer)
        {
            if (renderer == null) return;
            int index = _suppressedMeshes.IndexOf(renderer);
            if (index < 0)
            {
                _suppressedMeshes.Add(renderer);
                _meshForceRenderingOff.Add(renderer.forceRenderingOff);
            }
            renderer.forceRenderingOff = true;
        }

        void Hide(CanvasRenderer renderer)
        {
            if (renderer == null) return;
            if (!_hiddenCanvases.Contains(renderer))
            {
                _hiddenCanvases.Add(renderer);
                _canvasAlphas.Add(renderer.GetAlpha());
            }
            // Canvas clipping can overwrite cull after LateUpdate. Alpha suppresses only this draw, not its children.
            renderer.SetAlpha(0);
        }

        internal void Restore()
        {
            for (int index = 0; index < _suppressedMeshes.Count; index++)
                if (_suppressedMeshes[index] != null)
                    _suppressedMeshes[index].forceRenderingOff = _meshForceRenderingOff[index];
            for (int index = 0; index < _hiddenCanvases.Count; index++)
                if (_hiddenCanvases[index] != null) _hiddenCanvases[index].SetAlpha(_canvasAlphas[index]);
            _suppressedMeshes.Clear(); _meshForceRenderingOff.Clear();
            _hiddenCanvases.Clear(); _canvasAlphas.Clear();
            _subMeshes.Clear(); _subGraphics.Clear();
        }
    }
}
