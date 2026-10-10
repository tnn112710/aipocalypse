# Renderer comparison

Open [Renderer Comparison](../Renderer%20Comparison.unity). TextMeshPro and legacy UI Text appear beside three UI Toolkit source routes. The Message field updates all five outputs; no route selector is needed.

The UI Toolkit specimens use direct Label text, another element's value and a typed data property. Their Default Effects, Reward Impact Reaction, and `enable-in-edit-mode` setting are saved in `B14_Renderer.uxml`. The two Canvas hosts store the same Default Effects and Reaction in their native text Inspectors. The controller connects sources and responsive layout in Edit and Play Mode without replacing these settings or independently edited messages when re-enabled. Edit Mode shows the ongoing wave; the completion Reaction plays only in Play Mode when each message finishes revealing.

Legacy UI Text supports reveal, hide, placement, geometry, alpha and color, but not UV offset, TMP/SDF material effects or the complete 2D Appearance stack. Its native rich-text subset is bold, italic, color and size. Use a separate presentation Text object for editable legacy InputField content. UI Toolkit uses full rendering parity on Unity 6.2 and newer; older supported versions use its native Basic mode.

Inspect `TextStudioRendererLesson.SetDataMessage` for a typed property update. Programmatic Label Text writes and the Element source remain independent until the shared Message input is edited again. Native binding Inspectors and the original markup remain available for further experiments.
