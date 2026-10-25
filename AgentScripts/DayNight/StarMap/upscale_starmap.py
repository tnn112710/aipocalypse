#!/usr/bin/env python3
"""Star-aware upscale of a low-res Milky Way panorama to an 8K J2000 star map.

The diffuse glow and dust are separated from the stars and resampled smoothly; the stars are
detected and redrawn as sharp points; leftover fine grain becomes extra faint stars. The band
and core are rotated onto the real galactic plane, so EnvironmentDirector's star settings place
the core as documented. Energy is conserved: the map keeps the source's brightness.
"""

import argparse
import json
from pathlib import Path
import time

import numpy as np
from PIL import Image

from generate_starmap import (DEG, EQ_TO_GAL, PngWriter, encode, fbm, paint_stars, previews,
                              sha256, smoothstep, unit)


# Above this |latitude| a generated panorama's pinched, streaky poles are not trusted: no detected
# stars, and the grain there is replaced by the mid-latitude sky's.
POLAR_CAP = 72.0


def srgb_to_linear(pixels):
    p = pixels.astype(np.float32) / 255
    return np.where(p <= 0.04045, p / 12.92, ((p + 0.055) / 1.055) ** 2.4).astype(np.float32)


def shifted(a, d, axis):
    # Longitude wraps, latitude clamps.
    if axis == 1:
        return np.roll(a, d, axis=1)
    idx = np.clip(np.arange(a.shape[0]) - d, 0, a.shape[0] - 1)
    return a[idx]


def window(a, radius, op):
    for axis in (0, 1):
        out = a.copy()
        for d in range(1, radius + 1):
            out = op(out, op(shifted(a, d, axis), shifted(a, -d, axis)))
        a = out
    return a


def box(a, radius):
    for axis in (0, 1):
        out = a.copy()
        for d in range(1, radius + 1):
            out += shifted(a, d, axis) + shifted(a, -d, axis)
        a = out / (2 * radius + 1)
    return a


def luma(a):
    return a @ np.array([0.2126, 0.7152, 0.0722], np.float32)


def source_dirs(h, w):
    lon = (np.arange(w) + 0.5) * 2 * np.pi / w
    lat = np.pi / 2 - (np.arange(h) + 0.5) * np.pi / h
    lon, lat_grid = np.meshgrid(lon, lat)
    return unit(lon, lat_grid).astype(np.float32), lat


def separate(linear):
    """Opening (min then max) removes stars; blur hides its blockiness."""
    bg = window(window(linear, 3, np.minimum), 3, np.maximum)
    bg = np.minimum(box(box(bg, 2), 2), linear)
    h = linear.shape[0]
    lat = 90 - (np.arange(h) + 0.5) * 180 / h
    # Generated panoramas smear their poles: fade the rows toward their mean.
    polar = smoothstep(POLAR_CAP - 6.0, POLAR_CAP + 8.0, np.abs(lat))[:, None, None]
    bg = bg * (1 - polar) + bg.mean(axis=1, keepdims=True) * polar
    return bg.astype(np.float32), np.maximum(linear - bg, 0.0)


def detect(residual, lat, threshold):
    lum = luma(residual)
    peak = lum >= window(lum, 2, np.maximum)
    keep = peak & (lum > threshold) & (np.abs(lat)[:, None] < POLAR_CAP * DEG)
    ys, xs = np.nonzero(keep)
    energy = np.zeros((len(ys), 3), np.float32)
    weight_x = np.zeros(len(ys), np.float32)
    weight_y = np.zeros(len(ys), np.float32)
    w = residual.shape[1]
    footprint = np.zeros(lum.shape, bool)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            yy = np.clip(ys + dy, 0, lum.shape[0] - 1)
            xx = (xs + dx) % w
            e = residual[yy, xx]
            energy += e
            weight_x += luma(e) * dx
            weight_y += luma(e) * dy
            footprint[yy, xx] = True
    total = np.maximum(luma(energy), 1e-9)
    return xs + weight_x / total, ys + weight_y / total, energy, footprint


def fit_frame(bg, dirs):
    """Rotation source -> galactic: the band's great circle becomes b=0, its brightest part l=0."""
    smooth = luma(box(box(bg, 6), 6))
    cos_lat = np.sqrt(np.maximum(1 - dirs[..., 2] ** 2, 0))
    weight = np.maximum(smooth - np.median(smooth), 0) ** 2 * cos_lat
    v = dirs.reshape(-1, 3).astype(np.float64)
    cov = (v * weight.reshape(-1, 1)).T @ v
    normal = np.linalg.eigh(cov)[1][:, 0]
    if normal[2] < 0:
        normal = -normal
    core_blur = luma(box(box(bg, 20), 20)).reshape(-1)
    top = np.argsort(core_blur)[-max(64, core_blur.size // 2000):]
    core = (v[top] * core_blur[top, None]).sum(0)
    core -= normal * core.dot(normal)
    core /= np.linalg.norm(core)
    return np.stack([core, np.cross(normal, core), normal]).astype(np.float64)


def polar_grain(grain, lat):
    """Uniform sky in the caps: the mid-latitude grain per solid angle, times each row's cos(lat)."""
    cos_lat = np.cos(lat)[:, None, None]
    band = (np.abs(lat) > 45 * DEG) & (np.abs(lat) < (POLAR_CAP - 4) * DEG)
    # Mean, not median: grain is skewed, and only the mean keeps the caps as bright as the sky around them.
    per_area = (grain[band] / cos_lat[band]).mean(axis=(0, 1))
    rng = np.random.Generator(np.random.PCG64(99))
    filler = per_area * cos_lat * rng.exponential(1.0, grain.shape[:2])[..., None]
    cap = smoothstep(POLAR_CAP - 4, POLAR_CAP + 2, np.abs(lat / DEG))[:, None, None]
    return (grain * (1 - cap) + filler * cap).astype(np.float32)


def polar_stars(xs, ys, energy, src_w, src_h, seed):
    """Fills the caps with stars drawn from the mid-latitude ones, at their density per solid angle."""
    rng = np.random.Generator(np.random.PCG64(seed + 7))
    lat = np.pi / 2 - (ys + 0.5) * np.pi / src_h
    lo, hi = 45 * DEG, (POLAR_CAP - 4) * DEG
    pool = np.flatnonzero((np.abs(lat) > lo) & (np.abs(lat) < hi))
    if not len(pool):
        return xs, ys, energy
    band_area = 2 * 2 * np.pi * (np.sin(hi) - np.sin(lo))
    cap_area = 2 * np.pi * (1 - np.sin(POLAR_CAP * DEG))
    count = int(round(len(pool) / band_area * cap_area))
    new_x, new_y, new_e = [], [], []
    for sign in (1, -1):
        # Uniform on the sphere: uniform in sin(latitude).
        cap_lat = sign * np.arcsin(rng.uniform(np.sin(POLAR_CAP * DEG), 1.0, count))
        new_x.append(rng.uniform(0, src_w, count) - 0.5)
        new_y.append((np.pi / 2 - cap_lat) * src_h / np.pi - 0.5)
        new_e.append(energy[rng.choice(pool, count)])
    return (np.concatenate([xs, *new_x]), np.concatenate([ys, *new_y]),
            np.concatenate([energy, *new_e]))


def star_catalogue(xs, ys, energy, src_w, src_h, rotation, width, extra, gain):
    scale = width / src_w
    energy = energy * gain
    lon = (xs + 0.5) * 2 * np.pi / src_w
    lat = np.pi / 2 - (ys + 0.5) * np.pi / src_h
    gal = unit(lon, lat) @ rotation.T
    eq = gal @ EQ_TO_GAL
    ra = np.arctan2(eq[:, 1], eq[:, 0]) % (2 * np.pi)
    dec = np.arcsin(np.clip(eq[:, 2], -1, 1))
    lum = np.maximum(luma(energy), 1e-9)
    color = energy / lum[:, None]
    grey = color.mean(1, keepdims=True)
    color = np.clip(grey + 0.8 * (color - grey), 0.2, 2.0)
    order = np.argsort(-lum)
    ref = lum[order[min(1000, len(lum) - 1)]]
    magnitude = 6.0 - 2.5 * np.log10(lum / ref)
    # Brighter stars get wider cores so energy spreads instead of clipping to one white pixel.
    sigma = np.clip(0.42 + 0.18 * np.log2(1 + lum / np.median(lum)), 0.42, 1.2) * width / 8192
    peak = lum * scale ** 2 / (2 * np.pi * sigma ** 2)
    peak = 2.5 * np.tanh(peak / 2.5)
    x = ra * width / (2 * np.pi) - 0.5
    y = (0.5 - dec / np.pi) * (width // 2) - 0.5
    stars = dict(x=x, y=y, dec=dec, sigma=sigma, flux=peak.astype(np.float32),
                 color=color.astype(np.float32), magnitude=magnitude)
    if extra is not None:
        stars = {k: np.concatenate([stars[k], extra[k]]) for k in stars}
    ordering = np.argsort(stars["y"], kind="stable")
    return {k: v[ordering] for k, v in stars.items()}


def grain_stars(grain, src_w, src_h, rotation, width, count, seed):
    """Unresolved light left after detection, redrawn as many faint stars where it was."""
    rng = np.random.Generator(np.random.PCG64(seed))
    lum = luma(grain)
    p = lum.reshape(-1).astype(np.float64)
    total = p.sum()
    if total <= 0 or count == 0:
        return None
    cells = rng.choice(p.size, size=count, p=p / total)
    ys, xs = np.divmod(cells, src_w)
    color = grain.reshape(-1, 3)[cells] / np.maximum(lum.reshape(-1)[cells, None], 1e-9)
    grey = color.mean(1, keepdims=True)
    color = np.clip(grey + 0.6 * (color - grey), 0.3, 1.8)
    # Power-law fluxes, normalised so the stars carry exactly the grain's energy.
    flux = (1 - rng.random(count)) ** -0.55
    flux *= total / flux.sum()
    xs = xs + rng.random(count) - 0.5
    ys = ys + rng.random(count) - 0.5
    lon = (xs + 0.5) * 2 * np.pi / src_w
    lat = np.pi / 2 - (ys + 0.5) * np.pi / src_h
    eq = (unit(lon, lat) @ rotation.T) @ EQ_TO_GAL
    ra = np.arctan2(eq[:, 1], eq[:, 0]) % (2 * np.pi)
    dec = np.arcsin(np.clip(eq[:, 2], -1, 1))
    sigma = np.full(count, 0.42 * width / 8192)
    peak = flux * (width / src_w) ** 2 / (2 * np.pi * sigma ** 2)
    return dict(x=ra * width / (2 * np.pi) - 0.5, y=(0.5 - dec / np.pi) * (width // 2) - 0.5,
                dec=dec, sigma=sigma, flux=(2.5 * np.tanh(peak / 2.5)).astype(np.float32),
                color=color.astype(np.float32), magnitude=np.full(count, 12.0))


def sample(bg, src_dirs_flat_lonlat):
    lon, lat = src_dirs_flat_lonlat
    h, w = bg.shape[:2]
    u = lon % (2 * np.pi) * w / (2 * np.pi) - 0.5
    v = (0.5 - lat / np.pi) * h - 0.5
    x0, y0 = np.floor(u).astype(int), np.floor(v).astype(int)
    fx, fy = (u - x0)[:, None], (v - y0)[:, None]
    x1, y1 = (x0 + 1) % w, np.clip(y0 + 1, 0, h - 1)
    x0, y0 = x0 % w, np.clip(y0, 0, h - 1)
    return ((1 - fy) * ((1 - fx) * bg[y0, x0] + fx * bg[y0, x1])
            + fy * ((1 - fx) * bg[y1, x0] + fx * bg[y1, x1]))


def render(bg, stars, rotation, width, out, seed, detail, strip_rows=64):
    height = width // 2
    ra = (np.arange(width, dtype=np.float64) + 0.5) * (2 * np.pi / width)
    cosra, sinra = np.cos(ra), np.sin(ra)
    writer = PngWriter(out, width, height)
    for row in range(0, height, strip_rows):
        rows = min(strip_rows, height - row)
        dec = np.pi / 2 - (np.arange(row, row + rows) + 0.5) * np.pi / height
        eq = np.empty((rows * width, 3), np.float32)
        eq[:, 0] = (np.cos(dec)[:, None] * cosra).reshape(-1)
        eq[:, 1] = (np.cos(dec)[:, None] * sinra).reshape(-1)
        eq[:, 2] = np.repeat(np.sin(dec), width)
        src = (eq @ EQ_TO_GAL.astype(np.float32).T) @ rotation.astype(np.float32)
        lon = np.arctan2(src[:, 1], src[:, 0])
        lat = np.arcsin(np.clip(src[:, 2], -1, 1))
        linear = sample(bg, (lon, lat))
        if detail > 0:
            # Fine mottling the low-res source can't hold, strongest where the glow is.
            linear *= np.maximum(1 + detail * 0.18 * fbm(src, 260.0, seed + 401, 3), 0.3)[:, None]
        linear = linear.reshape(rows, width, 3).astype(np.float32)
        paint_stars(linear, row, stars, width, 1.0)
        writer.rows(encode(linear, row, seed))
        if row % (strip_rows * 16) == 0:
            print(f"Rows {row + rows}/{height}", flush=True)
    writer.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--width", type=int, default=8192)
    parser.add_argument("--seed", type=int, default=271828)
    parser.add_argument("--keep-frame", action="store_true",
                        help="Keep the source orientation instead of aligning its band to the galactic plane")
    parser.add_argument("--star-threshold", type=float, default=0.006,
                        help="Linear peak above the glow that counts as a star")
    parser.add_argument("--star-gain", type=float, default=0.5,
                        help="Energy kept by detected stars: the source's blurred stars turn harsh at full energy")
    parser.add_argument("--grain-stars", type=int, default=1_200_000)
    parser.add_argument("--detail", type=float, default=1.0)
    parser.add_argument("--preview-dir", type=Path)
    parser.add_argument("--preview-gain", type=float, default=4.0)
    args = parser.parse_args()
    if args.width < 512 or args.width % 2:
        parser.error("--width must be even and at least 512")
    start = time.perf_counter()
    with Image.open(args.source) as image:
        linear = srgb_to_linear(np.asarray(image.convert("RGB")))
    h, w = linear.shape[:2]
    dirs, lat = source_dirs(h, w)
    bg, residual = separate(linear)
    xs, ys, energy, footprint = detect(residual, lat, args.star_threshold)
    grain = polar_grain(np.where(footprint[..., None], 0.0, residual), lat)
    detected = len(xs)
    xs, ys, energy = polar_stars(xs, ys, energy, w, h, args.seed)
    rotation = np.eye(3) if args.keep_frame else fit_frame(bg, dirs)
    extra = grain_stars(grain, w, h, rotation, args.width, args.grain_stars, args.seed)
    stars = star_catalogue(xs, ys, energy, w, h, rotation, args.width, extra, args.star_gain)
    print(f"Source {w}x{h}: {detected:,} detected stars, {len(xs) - detected:,} polar fill, "
          f"{args.grain_stars:,} grain stars", flush=True)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    render(bg, stars, rotation, args.width, args.out, args.seed, args.detail)
    if args.preview_dir:
        previews(args.out, args.preview_dir, args.preview_gain)
    print(json.dumps(dict(source=str(args.source), out=str(args.out.resolve()), width=args.width,
                          detected_stars=detected, polar_fill=int(len(xs) - detected), rotation=np.round(rotation, 4).tolist(),
                          seconds=round(time.perf_counter() - start, 1), sha256=sha256(args.out)),
                     indent=2), flush=True)


if __name__ == "__main__":
    main()
