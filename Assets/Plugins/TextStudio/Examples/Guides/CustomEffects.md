# Text Studio Custom Effect Sample

Open `../02_Effects/Custom Extensions.unity` to see a code effect on the gate inscription. The referenced assets preserve both custom-code routes: `Orbit Code Effect.asset` stores typed defaults, and `Runtime Orbit Composed.asset` and `Asset Orbit Composed.asset` place the runtime factory and asset evaluator inside native Composed Effects. Apply these named assets or edit the specimen's inline markup in the Inspector to compare parameters, aliases, semantic areas and combinations with built-in effects.

Open `../Scripts/Core/CustomEffects/TextStudioCustomEffectSampleHost.cs` beside the scene. `TextStudioOrbitEffectSampleFactory` implements `ITextStudioEffectFactory` and publishes metadata for the picker, diagnostics, direct parameter controls, aliases, and ranges. Every `CreateEffect` call returns a detached evaluator with resolved parameters. The host registers the factory when enabled and unregisters it when disabled, which keeps Fast Enter Play Mode and repeated scene loads predictable.

Open `TextStudioOrbitCodeEffectAsset.cs` for the asset-backed route. Its parameter struct supplies the same metadata through fields and attributes, while `Orbit Code Effect.asset` supplies serialized defaults and catalog identity. `Runtime Orbit Composed.asset` and `Asset Orbit Composed.asset` show that lifecycle, playback, curves, sequencing, and reusable authoring belong to Composed Effects for both routes.

Copy a runtime factory when behavior needs scene objects, services, or live game state. Copy a Code Effect asset when projects should serialize the primitive and its defaults. Prefer channel layers when existing Composed controls can express the visual without new code.
