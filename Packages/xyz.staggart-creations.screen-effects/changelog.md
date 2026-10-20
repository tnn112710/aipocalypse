3.0.2 (October 16th 2026)

Added:
- Kaleidoscope: Border reflection option.
- Example HLSL shader illustrating how the Fog effect can be integrated into a transparent material.

Fixed:
- Runtime error when using the Blur effect in a build
- Migration Utility, not transfering the "amount" parameter value onto the new "Intensity" parameter (Radial Blur, Refraction & Tilt Shift)
- Condition, RGB colors of input textures not being used (preset textures are now imported as RGB textures as well).

3.0.1 (October 15th 2026)

Fixed:
- Fog: Start distance parameter not behaving consistently between cameras with different Far Clipping plane values
- Fog: Skybox influence parameter creating a small unfogged gap just before the camera's far clipping plane.

3.0.0 (October 12th 2026)
This is a major revision of the asset for Unity 6, and a separate asset altogether.

See the Upgrade Guide in the documentation on how to migrate from v2.4.1!

New effects:
- Motion Trails, creates a smooth visual lag trails from pixels beyond the set luminance threshold. Commonly used as a fast motion blur alternative.
- Anaglyph 3D, use color glasses to add 3D depth perception to a scene.
- Condition, blends in a (animated) texture/color at the screen edges. Includes 8 prefabs.

New components:
- Post Effect Volume Renderer, grabs all the effects from the attached Volume and injects them into rendering. No render features needed!
- Post Effect Mask, converts and Mesh into a Rendering Mask for effects.
- Custom Effect Renderer, allows you to create your own effects using a custom Shader Graph.

New functionality:
- Rendering Layer Mask support, allows effects to be limited to specific rendering layers.
- Timeline playables, allows you to active Profiles in a timeline.

Added:
- Sunshafts, support for a light's color temperature + a temperature shift parameter.
- Sunshafts, option to cast from a point rather than the skybox's brightest areas.
- Scanlines, option to use a resolution independent line count.
- Fog: Gradient texture has been converted to a regular Gradient field
- Fog: Toggle to invert the height fog.
- Ripples: Spherical Projection mode, keeps the displacement consistent regardless of the camera's rotation.
- Transition: Softness parameter
- Transition: Additional types: Linear, Shutters, Star and Circular.
- Double Vision, angle parameter.
- Colorize, range parameter. Allows remapping the input to ensure the entire gradient is used.
- Edge Detection, 3D noise to alter the edge thickness, offset and clipping (animation possible).
- Radial Blur, "CenterOnWorldPosition" helper function

Core changes:
- Script namespace has changed from “SCPE” to “sc.screeneffects.runtime”
- Main parameter name changed to "Intensity" for all effects (if needed). This is for consistency and to simplify volume blending.
- Effects no longer require an intermediate copy-pass first, so render in one pass instead of two.
- Animation speed parameters can now be smoothly changed.
- Shaders are now bundled into a ScriptableObject that's added to the "Pre-loaded assets" list on install (required for builds).

Effect changes:
- Consistency between 720p and 2160p resolutions for Blur, Edge Detection and Tilt Shift.
- Sunshafts, sizeable performance optimizations
- Sunshafts, toggles for using a lights color/intensity are now linear sliders
- Fog, now uses a 3D noise texture for height fog, eliminating any streaking on vertical surfaces.
- Fog, option on the render feature to apply fog to the Opaque texture (required for effects such as water shading/refraction).
- Fog, option to invert the height fog.
- Fog, directional light color/intensity influence is now a linear slider, for optimal use with time of day systems.
- Radial Blur, now uses a configurable "Sample Distance" that auto-calculates the number of itterations based on the blur length.
- Edge Detection, now offers control over the influence of Depth, Normals and Color difference
- Ripples: Now uses a displacement texture rather than procedural noise. Faster performance with no decernable visual difference.
- Kuwahara, optimized performance. Depth fade option now reduces impact to 0 when completely faded out.
- Sketch effect has been reworked, now offers control over the light- and dark threshold.
- Double Vision, now has an "Edge Mask" parameter instead of a dedicated "Edge" mode.
- Transition, now also has a "softness" parameter + additional styles: Shutters, Linear and Circular.

Fixed:
- Sunshafts, now fully VR compatible.
- Sketch, fixed the texture projection being rotated 90 degrees on the YZ axis.

Removed:
- Lens Flares effect, a native varient was added to URP in Unity 2022.3
- Danger effect, now replaced by the Condition effect
- Speed Lines effect, now replaced by the Condition effect
- Caustics effect, the same effect can already be achieved using Decals, Light Cookies or Overlay materials with much greater performance efficiency.*
- Cloud Shadows, better utilized using an animated light cookie (native shadow integration).*
- Ambient Occlusion 2D, didn't see any use or mention.

[The change log for older versions can be found in the SC Post Effects Pack asset]