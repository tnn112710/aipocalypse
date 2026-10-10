# Custom actions

Open `../02_Effects/Custom Extensions.unity`. The text opens a gate and awards coins through the native `<courseGate reward=25>` action.

`../Scripts/Core/TextStudioCourseGateAction.cs` implements a scene-local action beside its `TextStudioText`. `TextStudioExtensionLesson.cs` also includes a code-registered action with the same definition. The lesson component's `NextAction` method switches ownership for Inspector or scripted experiments; the native action definition keeps parameter metadata available to markup diagnostics in both routes.

The visible result is the gate and reward, with no sample-management menu. The action and effect recipes remain inspectable in the hierarchy and neighboring assets. Edit Mode does not run action tags by default. Enable Run Actions in the native Preview panel when intentionally testing their consequences in the editor.
