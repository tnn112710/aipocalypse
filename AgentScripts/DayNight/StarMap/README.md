# Procedural emission star map

Run from the repository root with Python 3.12, NumPy and Pillow. No astronomy
catalogues, photographs or other data are downloaded. Only the output PNG and
optional preview PNGs are written; this tool never controls Unity or writes metas.

```powershell
python -m venv "$env:LOCALAPPDATA\aipocalypse-tools\starmap-venv"
$starPython = "$env:LOCALAPPDATA\aipocalypse-tools\starmap-venv\Scripts\python.exe"
& $starPython -m pip install numpy==2.3.5 Pillow==12.3.0
& $starPython AgentScripts/DayNight/StarMap/generate_starmap.py `
  --width 8192 --seed 271828 --band-scale 1.0 `
  --preview-dir "$env:TEMP\claude\d--Projects-indie-aipocalypse\1f5caf11-af8b-4655-bd37-6727032765a9\scratchpad\starmap"
```

This session's sandbox denied the requested LOCALAPPDATA venv location and pip
could not install packages. The working isolated venv is instead
`C:/Users/NhNg/AppData/Local/Temp/aipocalypse-tools/starmap-venv`.
NumPy 2.3.5 and Pillow 12.3.0 were copied there from the bundled Codex Python
runtime; global Python was not modified. Substitute that venv's `Scripts/python.exe`
for `$starPython` to reproduce this session's run.

Arguments:

| Argument | Default / meaning |
| --- | --- |
| `--width` | `8192`; even, >=512; height is exactly width/2. Supports `16384`. |
| `--seed` | `271828`; unsigned 32-bit integer. |
| `--band-scale` | `1.0`; multiplier of the **new narrow baseline**, finite and within `0.25..2.0`. Smaller values narrow it further. `1.0` does not restore the original broad version. |
| `--out` | `Assets/_Game/Data/Environment/TimeOfDay/StarMap/StarMap.png`, relative to the repository containing the script. Explicit relative paths use the working directory. |
| `--preview-dir` | Omitted: no previews. Set to an external scratch directory. |
| `--strip-rows` | `64`; 1..256; lower for less working memory. |

Example full 16K run outside the repository:

```powershell
& $starPython AgentScripts/DayNight/StarMap/generate_starmap.py `
  --width 16384 --out "$env:TEMP\claude\d--Projects-indie-aipocalypse\1f5caf11-af8b-4655-bd37-6727032765a9\scratchpad\starmap\test_16384.png"
```

## Coordinates and appearance

At `--band-scale 1.0`, diffuse latitude widths are about 60% of the previous
generator near the inner galaxy and approach 48% toward the anticenter. The
warm bulge has half the previous longitude and latitude extent, with a brighter
peak. The multiplier scales diffuse/dust latitude extents and both bulge axes;
longitude positions of named features stay fixed. Star counts, directions,
magnitudes, colours and PSF sizes stay unchanged; dust attenuation is recomputed
for the narrower lanes. No scale value restores the old blotchy texture.

The 2:1 equirectangular image uses **equatorial J2000** coordinates. Right ascension
increases to the right: 0h (0 degrees) at the left boundary, 24h (360 degrees) at
the right boundary. Declination runs from +90 degrees at the top boundary to
-90 degrees at the bottom boundary. Pixel centers are
`RA = 360 * (x + 0.5) / width`, `Dec = 90 - 180 * (y + 0.5) / height`.
There is no duplicated seam column or pole row. A viewer looking outward may
need the appropriate horizontal orientation in its shader; the file itself
always follows the stated increasing-RA convention.

The standard orthonormal equatorial-to-galactic matrix puts the north galactic
pole at RA 192.859, Dec +27.128 degrees. The brighter, wider Sagittarius bulge
surrounds the actual galactic center, RA 266.405, Dec -29.008 degrees. The IAU
coordinate origin l=0, b=0 is slightly offset (Dec -28.936 degrees); the actual
center is within 0.1 degree of that origin. The structured extinction lane runs
from the center toward Cygnus, with
clouds at Scutum (l=28), Cygnus (l=76), Carina (l=287), Crux (l=305), and a dark
Coalsack (l=303). The anticenter is thinner and fainter. Structure is a procedural
interpretation at the correct large-scale positions, not a measured survey;
individual stars are synthetic, without real constellation positions.

1.6 million stars use solid-angle directions, a cos(latitude)-weighted disk,
a magnitude CDF growing x3 per magnitude, modest blackbody colours and extinction.
Faint cores are about one pixel at 8K; brighter stars widen and roughly 700 get
soft halos. PSFs stretch by 1/cos(declination); polar caps use exact spherical
distances, including pole-crossing footprints. Angular sizes stay fixed at 16K.

Low-contrast emission provides a soft stellar glow. Anisotropic 3D noise shapes
fine brown dust filaments and crisp central lanes without coordinate seams;
large high-contrast cloud blobs have been removed.
Off-band emission is black. Output is **8-bit RGB sRGB PNG, no alpha**, with
sRGB/gAMA metadata, a gentle tone shoulder, sub-code deterministic dither and
highlight headroom. No atmosphere, ground, spikes or horizon glow.

## Memory, determinism and previews

Rendering and PNG compression stream strips without a full-frame float buffer.
Omit previews for minimum memory at 16K; previews decode the RGB PNG.

Same seed, width, band scale and strip height give identical bytes in the same
Python/NumPy/zlib environment. Logs report SHA-256, time and Windows peak RAM.
Dependency or strip-height changes can affect exact bytes.

Previews include a 2048x1024 full-map downscale and three 1920x1080 pinhole views,
all sampled from the actual PNG, with no exposure lift:

- Galactic center: l=5, b=-1, roll=24 degrees, horizontal FOV 90 degrees.
- Cygnus / Great Rift: l=65, b=0, roll=-30 degrees, horizontal FOV 90 degrees.
- Sparse high latitude: l=155, b=65, roll=0, horizontal FOV 90 degrees.

Three 960x960, 35-degree views inspect both celestial poles and the RA=0 seam.
Pinhole interpolation is performed in linear light. The whole-map downscale uses
Pillow LANCZOS. These are emission-only previews against black, not Unity captures.

The streaming writer and spherical PSF renderer also support 16K (the previous
version completed a full 16K run in 288 s at 449 MiB peak RAM). Current validation
results are in the external preview directory (`verification.json`).

Narrow revision at 8K, seed 271828 and band scale 1.0: generation 66.3 s,
71.2 s including previews, 449.2 MiB peak RAM. Repeat generation was identical:
`c9edcbe18aa5b16a891f9492aa756c5de4e271ecd62e092151293bc52b862e83`.
RGB/sRGB encoding, seam continuity, both polar PSFs and unchanged stellar
directions/magnitudes/colours/counts were checked; all previews were inspected.

## Upscaling a generated panorama (`upscale_starmap.py`)

Turns a low-res Milky Way panorama (e.g. an AI-generated 1774x887 equirectangular image) into an
8K map in the same J2000 layout as above. Same Python environment; it imports helpers from
`generate_starmap.py`, so run it as a script (not with `-I`).

```powershell
& $starPython AgentScripts/DayNight/StarMap/upscale_starmap.py `
  Assets/_Game/Data/Environment/TimeOfDay/StarMap/StarMap4.png `
  --out Assets/_Game/Data/Environment/TimeOfDay/StarMap/StarMap4_8K.png --preview-dir <scratch dir>
```

How it works: an opening with a round radius-3 window separates the diffuse glow and dust from the
stars (a square window leaves square contours around bright stars); the glow is resampled smoothly
with a little fine mottling (`--detail`). Stars brighter than `--star-threshold` are detected; each
core (3x3, measured above the star's own halo level) is redrawn as a sharp spherical point with
`--star-gain` of its energy (the source's blurred stars look harsh at full energy). Bright stars'
halos (up to 6 source pixels) become a smooth glow with `--halo-gain` of their energy, and the grain
under them continues from the surroundings. Zeroing the grain under each star instead left a dark
square with a speckled ring around every bright star. Unresolved grain left over becomes
`--grain-stars` faint stars where it was, carrying exactly its energy. Above |latitude| 72 deg the
source's pinched poles are not trusted: their stars are copies of the 45-68 deg ones at the same
density per solid angle, and their grain is the mid-latitude mean (not the median: grain is skewed,
and a median left an 18-deg dim hole around each galactic pole). The band's great circle is fitted and its brightest point taken as the core, then the whole
panorama is rotated (a proper rotation, never mirrored) onto the real galactic plane, so
`EnvironmentDirector`'s Star Azimuth / Latitude / Galactic Core Transit place it as documented.
`--keep-frame` skips that rotation. 8K takes about 90 s.
