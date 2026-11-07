//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    public class DraggableElement
    {
        public float pixelThreshold = 0;
        public Action OnStartDrag;
        public Action<float> OnDrag;
        public Action OnEndDrag;
        public VisualElement NewContainer()
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("");
            container.AddToClassList("unity-base-field__label--with-dragger");

            float accumulatorX = 0f;
            bool dragging = false;

            container.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    dragging = true;
                    accumulatorX = 0f;
                    container.CapturePointer(0);
                    evt.StopPropagation();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                    OnStartDrag?.Invoke();
                }
            });

            container.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (dragging && container.HasMouseCapture())
                {
                    accumulatorX += evt.deltaPosition.x * ((evt.modifiers & EventModifiers.Shift) != 0 ? 4f : 1f);

                    if (Mathf.Abs(accumulatorX) >= pixelThreshold)
                    {
                        if (accumulatorX != 0)
                        {
                            OnDrag?.Invoke(accumulatorX / pixelThreshold);

                            accumulatorX = 0;

                            evt.StopPropagation();
                        }
                    }
                }
            });

            container.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (dragging && evt.button == 0)
                {
                    dragging = false;
                    container.ReleaseMouse();
                    evt.StopPropagation();
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    OnEndDrag?.Invoke();
                }
            });

            return container;
        }
    }
}