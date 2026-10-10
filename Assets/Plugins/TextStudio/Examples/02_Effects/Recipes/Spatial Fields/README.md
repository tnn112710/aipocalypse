# Spatial field recipes

Each prefab contains four native text hosts, their saved effect references and a shared movable target. Place a group under a Canvas. The main Spatial Fields scene provides the same original four behaviors with keyboard and pointer interaction.

- **Original four**: Scale and color, local motion, inverted visibility, and target distance multiplied by noise. Each panel uses its own saved Composed Effect.
- **Shape / Linear**: The linear field is centered on each text. Only Target Distance follows the compass; compare with the Original four recipe.
- **Shape / Sphere**: The sphere field is centered on each text. Only Target Distance follows the compass; compare with the Original four recipe.
- **Shape / Box**: The box field is centered on each text. Only Target Distance follows the compass; compare with the Original four recipe.
- **Shape / Cylinder**: The cylinder field is centered on each text. Only Target Distance follows the compass; compare with the Original four recipe.
- **Shape / Torus**: The torus field is centered on each text. Only Target Distance follows the compass; compare with the Original four recipe.
- **Shape / Noise**: Noise uses a stable spatial pattern. It does not follow the compass. The four original channels remain unchanged.
- **Space / SourceLayout**: The compass is evaluated in SourceLayout. The radius is authored in that space's units; inspect the asset to compare coordinates.
- **Space / SourceTextNormalized**: The compass is evaluated in SourceTextNormalized. The radius is authored in that space's units; inspect the asset to compare coordinates.
- **Space / World**: The compass is evaluated in World. The radius is authored in that space's units; inspect the asset to compare coordinates.
- **Blend / Add**: The fourth panel combines Target Distance and Noise with Add. The first three panels remain fixed references.
- **Blend / Maximum**: The fourth panel combines Target Distance and Noise with Maximum. The first three panels remain fixed references.
- **Blend / Minimum**: The fourth panel combines Target Distance and Noise with Minimum. The first three panels remain fixed references.
