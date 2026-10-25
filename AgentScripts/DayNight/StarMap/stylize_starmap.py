#!/usr/bin/env python3
"""Stylized version of a star map PNG in our layout, every star kept where it is.

The glow (Milky Way, dust) is separated from the stars on a 1/4-size copy, smoothed, given more
contrast and a palette's colour ramp (--palette) partly mixed with its original hue. Stars are
detected at full size and redrawn at their exact positions as clean round points in a few size tiers
with a compressed brightness range; the brightest get a small 4-point sparkle. The faintest are
dropped, unless --pepper adds the source's own faint star layer back (capped, so the redrawn stars
are not doubled): without it the sky reads empty. Brightness stays on the source's scale (the bright band end
and the median star are kept), so the material's Star Map Intensity still fits.
"""

import argparse
import json
from pathlib import Path
import time

import numpy as np
from PIL import Image

from generate_starmap import DEG, PngWriter, encode, paint_stars, previews, sha256

Image.MAX_IMAGE_PIXELS = None
LUMA = np.array([0.2126, 0.7152, 0.0722], np.float32)
# Hue ramps over the glow's normalised brightness (linear RGB; brightness comes from the source).
PALETTES = {
    # Indigo edges, blue-violet band, pink-mauve clouds, warm cream core.
    "violet": [(0.00, (0.35, 0.42, 1.00)),
               (0.30, (0.50, 0.50, 1.00)),
               (0.55, (0.78, 0.56, 1.00)),
               (0.80, (1.00, 0.62, 0.86)),
               (1.00, (1.00, 0.78, 0.70))],
    # Deep navy edges, teal band, icy cyan clouds, gold core.
    "teal": [(0.00, (0.22, 0.40, 1.00)),
             (0.30, (0.25, 0.62, 1.00)),
             (0.55, (0.30, 0.85, 0.95)),
             (0.80, (0.70, 0.95, 0.95)),
             (1.00, (1.00, 0.84, 0.55))],
}


def to_linear(srgb8):
    p = srgb8.astype(np.float32) / 255
    lin = np.where(p <= 0.04045, p / 12.92, ((p + 0.055) / 1.055) ** 2.4)
    # Undo the encoder's shoulder (mapped = 0.89 (1 - exp(-x / 0.89))).
    return -0.89 * np.log1p(-np.minimum(lin, 0.888) / 0.89)


def shifted(a, d, axis):
    if axis == 1:
        return np.roll(a, d, axis=1)
    return a[np.clip(np.arange(a.shape[0]) - d, 0, a.shape[0] - 1)]


def disc_window(a, radius, op):
    out = a.copy()
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            if (dx or dy) and dx * dx + dy * dy <= radius * radius + radius:
                out = op(out, shifted(shifted(a, dy, 0), dx, 1))
    return out


def box(a, radius):
    for axis in (0, 1):
        out = a.copy()
        for d in range(1, radius + 1):
            out += shifted(a, d, axis) + shifted(a, -d, axis)
        a = out / (2 * radius + 1)
    return a


def glow_quarter(src):
    """The glow on a 1/4-size copy: stars are at most a pixel there, so a small opening removes them."""
    h, w = src.shape[:2]
    small = np.empty((h // 4, w // 4, 3), np.float32)
    for row in range(0, h, 256):
        lin = to_linear(src[row:row + 256])
        small[row // 4:(row + 256) // 4] = lin.reshape(-1, 4, w // 4, 4, 3).mean((1, 3))
    glow = disc_window(disc_window(small, 2, np.minimum), 2, np.maximum)
    return np.minimum(box(box(glow, 2), 2), small)


def upsample(small, rows, width):
    """Bilinear rows of a 1/4-size map at full size (longitude wraps)."""
    hs, ws = small.shape[:2]
    v = (rows + 0.5) / 4 - 0.5
    u = (np.arange(width) + 0.5) / 4 - 0.5
    y0 = np.floor(v).astype(int)
    x0 = np.floor(u).astype(int)
    fy = (v - y0)[:, None, None]
    fx = (u - x0)[None, :, None]
    y1, y0 = np.clip(y0 + 1, 0, hs - 1), np.clip(y0, 0, hs - 1)
    x1, x0 = (x0 + 1) % ws, x0 % ws
    top = small[y0][:, x0] * (1 - fx) + small[y0][:, x1] * fx
    bottom = small[y1][:, x0] * (1 - fx) + small[y1][:, x1] * fx
    return (top * (1 - fy) + bottom * fy).astype(np.float32)


def detect(src, glow, count):
    """Local peaks of the star layer (source minus glow) at full size, strip by strip; the brightest `count`."""
    h, w = src.shape[:2]
    pad = 3
    xs, ys, energy = [], [], []
    for row in range(0, h, 256):
        lo, hi = max(row - pad, 0), min(row + 256 + pad, h)
        rows = np.arange(lo, hi)
        stars = np.maximum(to_linear(src[lo:hi]) - upsample(glow, rows, w), 0)
        lum = stars @ LUMA
        peak = lum > 0.004
        for dy in range(-2, 3):
            for dx in range(-2, 3):
                if dx or dy:
                    peak &= lum >= shifted(shifted(lum, dy, 0), dx, 1)
        py, px = np.nonzero(peak)
        keep = (py + lo >= row) & (py + lo < row + 256)
        py, px = py[keep], px[keep]
        e = np.zeros((len(py), 3), np.float32)
        wx = np.zeros(len(py), np.float32)
        wy = np.zeros(len(py), np.float32)
        for dy in range(-2, 3):
            for dx in range(-2, 3):
                v = stars[np.clip(py + dy, 0, len(rows) - 1), (px + dx) % w]
                e += v
                wx += (v @ LUMA) * dx
                wy += (v @ LUMA) * dy
        total = np.maximum(e @ LUMA, 1e-9)
        xs.append(px + wx / total)
        ys.append(py + lo + wy / total)
        energy.append(e)
    xs, ys, energy = np.concatenate(xs), np.concatenate(ys), np.concatenate(energy)
    order = np.argsort(-(energy @ LUMA))[:count]
    return xs[order], ys[order], energy[order]


def ramp(t, palette):
    stops = np.array([s for s, _ in palette], np.float32)
    colours = np.array([c for _, c in palette], np.float32)
    return np.stack([np.interp(t, stops, colours[:, c]) for c in range(3)], -1)


def unit_luma(c):
    return c / np.maximum(c @ LUMA, 1e-6)[..., None]


def stylize_glow(glow, palette, hue_mix, contrast, saturation):
    """Same brightness scale as the source (its bright band end is kept), more contrast, ramped hue."""
    lum = glow @ LUMA
    floor, top = np.percentile(lum, 30), np.percentile(lum, 99.7)
    t = np.clip(lum / top, 0, None)
    # Power curve: faint sky darker, the bright band kept, so dust lanes and band edges read crisper.
    curved = np.where(t < 1, t ** contrast, t) * top
    position = np.clip((lum - floor) / (top - floor), 0, 1)
    hue = (1 - hue_mix) * unit_luma(ramp(position, palette)) + hue_mix * np.clip(unit_luma(np.maximum(glow, 0)), 0, 3)
    grey = np.ones_like(hue)
    hue = unit_luma(np.maximum(grey + saturation * (hue - grey), 0))
    return (hue * curved[..., None]).astype(np.float32)


def star_catalogue(xs, ys, energy, width, height, gain, saturation):
    lum = np.maximum(energy @ LUMA, 1e-9)
    colour = energy / lum[:, None]
    grey = colour.mean(1, keepdims=True)
    colour = np.clip(grey + saturation * (colour - grey), 0.15, 2.5)
    rank = np.argsort(np.argsort(-lum)) / max(len(lum) - 1, 1)  # 0 brightest .. 1 faintest
    # Clean tiers instead of a photographic range: size and brightness step with rank.
    tiers = np.select([rank < 0.003, rank < 0.03, rank < 0.25], [1.25, 0.9, 0.65], 0.5)
    sigma = tiers * width / 8192
    # The source's energy per star, its range compressed around the median star (same pixel scale in and out).
    median = np.median(lum)
    flux = gain * median * (lum / median) ** 0.6
    peak = np.minimum(flux / (2 * np.pi * sigma ** 2), 2.5).astype(np.float32)
    dec = np.pi / 2 - (ys + 0.5) * np.pi / height
    order = np.argsort(ys, kind="stable")
    stars = dict(x=xs, y=ys, dec=dec, sigma=sigma, flux=peak, color=colour.astype(np.float32),
                 magnitude=np.where(rank < 0.003, 5.0, 12.0))
    return {k: v[order] for k, v in stars.items()}, rank


def paint_sparkles(image, row, stars, width, length, strength):
    """Thin 4-point spikes (along RA and Dec, so they turn with the sky) on the brightest stars."""
    lo, hi = np.searchsorted(stars["y"], [row - length - 2, row + len(image) + length + 2])
    for i in range(lo, hi):
        if stars["magnitude"][i] > 6:
            continue
        x, y, colour = stars["x"][i], stars["y"][i], stars["color"][i]
        reach = np.arange(-length, length + 1)
        fall = (1 - np.abs(reach) / (length + 1)) ** 2.5 * strength * stars["flux"][i]
        stretch = 1 / max(np.cos(stars["dec"][i]), 0.05)
        py = int(round(y)) - row
        if 0 <= py < len(image):
            px = np.round(x + reach * stretch).astype(int) % width
            image[py, px] += fall[:, None] * colour
        rows = np.round(y + reach).astype(int) - row
        ok = (rows >= 0) & (rows < len(image))
        image[rows[ok], int(round(x)) % width] += fall[ok, None] * colour


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="A star map PNG in our layout (e.g. StarMap_NASA2020.png)")
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--stars", type=int, default=60_000, help="How many of the brightest stars to keep")
    parser.add_argument("--star-gain", type=float, default=1.0)
    parser.add_argument("--star-saturation", type=float, default=1.25)
    parser.add_argument("--hue-mix", type=float, default=0.15, help="0 = pure colour ramp, 1 = the glow's own colours")
    parser.add_argument("--contrast", type=float, default=1.2, help="Glow power curve: 1 = source, higher = darker faint sky")
    parser.add_argument("--saturation", type=float, default=1.6, help="Glow colour strength")
    parser.add_argument("--palette", choices=sorted(PALETTES), default="violet")
    parser.add_argument("--pepper", type=float, default=0.0,
                        help="Gain of the source's faint star layer added back (0 = none, as the first version)")
    parser.add_argument("--pepper-cap", type=float, default=0.05,
                        help="Linear cap on that layer: keeps faint stars, leaves bright ones to the redrawn points")
    parser.add_argument("--sparkle", type=float, default=0.25, help="Spike strength on the brightest stars, 0 = none")
    parser.add_argument("--sparkle-length", type=int, default=9)
    parser.add_argument("--seed", type=int, default=271828)
    parser.add_argument("--preview-dir", type=Path)
    parser.add_argument("--preview-gain", type=float, default=4.0)
    args = parser.parse_args()
    start = time.perf_counter()
    with Image.open(args.source) as image:
        src = np.asarray(image.convert("RGB"))
    h, w = src.shape[:2]
    glow = glow_quarter(src)
    xs, ys, energy = detect(src, glow, args.stars)
    styled = stylize_glow(glow, PALETTES[args.palette], args.hue_mix, args.contrast, args.saturation)
    stars, _ = star_catalogue(xs, ys, energy, w, h, args.star_gain, args.star_saturation)
    print(f"{len(xs):,} stars kept", flush=True)
    writer = PngWriter(args.out, w, h)
    for row in range(0, h, 64):
        rows = np.arange(row, min(row + 64, h))
        linear = upsample(styled, rows, w)
        if args.pepper > 0:
            faint = np.maximum(to_linear(src[row:row + len(rows)]) - upsample(glow, rows, w), 0)
            linear += args.pepper * np.minimum(faint, args.pepper_cap)
        paint_stars(linear, row, stars, w, 1.0)
        if args.sparkle > 0:
            paint_sparkles(linear, row, stars, w, args.sparkle_length, args.sparkle)
        writer.rows(encode(np.maximum(linear, 0), row, args.seed))
    writer.close()
    if args.preview_dir:
        previews(args.out, args.preview_dir, args.preview_gain)
    print(json.dumps(dict(out=str(args.out.resolve()), stars=int(len(xs)),
                          seconds=round(time.perf_counter() - start, 1), sha256=sha256(args.out)), indent=2), flush=True)


if __name__ == "__main__":
    main()
