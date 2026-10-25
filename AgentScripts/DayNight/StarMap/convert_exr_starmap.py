#!/usr/bin/env python3
"""Converts a real equirectangular star map EXR (e.g. NASA SVS Deep Star Maps 2020) into our J2000 PNG.

Reads scanline EXRs (half or float RGB; NONE or ZIP/ZIPS compression) with NumPy and zlib only. The
source's RA origin and direction are not assumed: the galactic plane and centre are fitted from the
image and the layout whose fit matches the real ones (north galactic pole RA 192.86 Dec +27.13,
centre RA 266.40 Dec -29.01) is used, then the map is resampled into ours (RA 0h at the left edge,
growing rightward, +90 deg at the top). Brightness is scaled so the map's median sky matches a
reference map (the one the scene tuned Star Map Intensity for), then encoded like generate_starmap.
"""

import argparse
import json
from pathlib import Path
import struct
import time
import zlib

import numpy as np
from PIL import Image

from generate_starmap import EQ_TO_GAL, PngWriter, encode, previews, sha256

Image.MAX_IMAGE_PIXELS = None
NGP = (192.8595, 27.1283)
CENTRE = (266.4050, -29.0078)
PIXEL_TYPES = {0: np.uint32, 1: np.float16, 2: np.float32}


def read_exr(path):
    data = Path(path).read_bytes()
    magic, version = struct.unpack_from("<ii", data, 0)
    if magic != 20000630 or version & 0x200:
        raise SystemExit("not a scanline OpenEXR file")
    pos, header = 8, {}
    while data[pos] != 0:
        end = data.index(b"\0", pos)
        name = data[pos:end].decode()
        pos = end + 1
        end = data.index(b"\0", pos)
        kind = data[pos:end].decode()
        size = struct.unpack_from("<i", data, end + 1)[0]
        header[name] = (kind, data[end + 5:end + 5 + size])
        pos = end + 5 + size
    pos += 1
    channels, raw, i = [], header["channels"][1], 0
    while raw[i] != 0:
        end = raw.index(b"\0", i)
        channels.append((raw[i:end].decode(), PIXEL_TYPES[struct.unpack_from("<i", raw, end + 1)[0]]))
        i = end + 17
    x0, y0, x1, y1 = struct.unpack("<4i", header["dataWindow"][1])
    width, height = x1 - x0 + 1, y1 - y0 + 1
    compression = header["compression"][1][0]
    lines_per_chunk = {0: 1, 2: 1, 3: 16}.get(compression)
    if lines_per_chunk is None:
        raise SystemExit(f"unsupported EXR compression {compression} (NONE, ZIPS and ZIP only)")
    chunks = (height + lines_per_chunk - 1) // lines_per_chunk
    offsets = struct.unpack_from(f"<{chunks}Q", data, pos)
    planes = {name: np.empty((height, width), np.float32) for name, _ in channels}
    line_bytes = sum(width * np.dtype(t).itemsize for _, t in channels)
    for offset in offsets:
        y, size = struct.unpack_from("<ii", data, offset)
        block = data[offset + 8:offset + 8 + size]
        lines = min(lines_per_chunk, y1 + 1 - y)
        if compression and size < line_bytes * lines:
            # ZIP: deflate, then undo the byte predictor and the even/odd byte split.
            d = np.frombuffer(zlib.decompress(block), np.uint8).astype(np.int64)
            t = np.empty(len(d), np.int64)
            t[0] = d[0]
            t[1:] = d[0] + np.cumsum(d[1:] - 128)  # t[i] = t[i-1] + d[i] - 128, mod 256
            t = (t % 256).astype(np.uint8)
            half = (len(t) + 1) // 2
            out = np.empty(len(t), np.uint8)
            out[0::2], out[1::2] = t[:half], t[half:]
            block = out.tobytes()
        cursor = 0
        for line in range(lines):
            for name, kind in channels:
                n = width * np.dtype(kind).itemsize
                planes[name][y - y0 + line] = np.frombuffer(block, kind, width, cursor).astype(np.float32)
                cursor += n
    names = [n for n, _ in channels]
    rgb = [planes[c] for c in ("R", "G", "B")] if {"R", "G", "B"} <= set(names) else [planes[names[0]]] * 3
    return np.stack(rgb, -1)


def directions(h, w):
    u = (np.arange(w) + 0.5) / w
    v = (np.arange(h) + 0.5) / h
    return u, v


def box(a, r, axis):
    c = np.cumsum(np.pad(a, [(r + 1, r) if i == axis else (0, 0) for i in range(a.ndim)], mode="wrap" if axis == 1 else "edge"), axis=axis)
    hi = np.take(c, np.arange(2 * r + 1, c.shape[axis]), axis=axis)
    lo = np.take(c, np.arange(0, c.shape[axis] - 2 * r - 1), axis=axis)
    return (hi - lo) / (2 * r + 1)


def fit_layout(rgb):
    """Fits the band's plane and its brightest point on a small copy, for each candidate RA layout."""
    small = rgb.reshape(rgb.shape[0] // 8, 8, rgb.shape[1] // 8, 8, 3).mean((1, 3))
    lum = small @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    # Median-filtered glow: stars are rare outliers, the band is everywhere it is.
    glow = box(box(np.minimum(lum, np.percentile(lum, 99)), 6, 0), 6, 1)
    h, w = glow.shape
    u, v = directions(h, w)
    dec = np.radians(90 - 180 * v)[:, None]
    weight = np.maximum(glow - np.median(glow), 0) ** 2 * np.cos(dec)
    core_idx = np.unravel_index(np.argmax(box(box(glow, 10, 0), 10, 1) * (np.cos(dec) > 0.3)), glow.shape)
    results = []
    for origin in (0.0, 0.5):
        for sign in (1, -1):
            ra = np.radians((sign * (u - origin) * 360) % 360)[None, :]
            vec = np.stack([np.cos(dec) * np.cos(ra), np.cos(dec) * np.sin(ra), np.sin(dec) * np.ones_like(ra)], -1).reshape(-1, 3)
            cov = (vec * weight.reshape(-1, 1)).T @ vec
            normal = np.linalg.eigh(cov)[1][:, 0]
            core = vec[np.ravel_multi_index(core_idx, glow.shape)]
            ngp = sky_vector(*NGP)
            err_pole = np.degrees(np.arccos(min(abs(normal @ ngp), 1)))
            err_core = np.degrees(np.arccos(np.clip(core @ sky_vector(*CENTRE), -1, 1)))
            results.append(dict(origin=origin, sign=sign, pole_error=round(float(err_pole), 2), core_error=round(float(err_core), 2)))
    best = min(results, key=lambda r: r["pole_error"] + r["core_error"])
    return best, results


def sky_vector(ra, dec):
    ra, dec = np.radians(ra), np.radians(dec)
    return np.array([np.cos(dec) * np.cos(ra), np.cos(dec) * np.sin(ra), np.sin(dec)])


def reference_median(path):
    with Image.open(path) as image:
        a = np.asarray(image.convert("RGB").reduce(4)).astype(np.float32) / 255
    lin = np.where(a <= 0.04045, a / 12.92, ((a + 0.055) / 1.055) ** 2.4)
    # Undo the encoder's shoulder (mapped = 0.89 * (1 - exp(-x / 0.89))).
    lin = -0.89 * np.log1p(-np.minimum(lin, 0.888) / 0.89)
    return float(np.median(lin @ np.array([0.2126, 0.7152, 0.0722], np.float32)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--reference", type=Path, help="PNG star map whose median sky brightness to match")
    parser.add_argument("--gain", type=float, help="Fixed linear gain instead of matching --reference")
    parser.add_argument("--seed", type=int, default=271828)
    parser.add_argument("--preview-dir", type=Path)
    parser.add_argument("--preview-gain", type=float, default=4.0)
    args = parser.parse_args()
    start = time.perf_counter()
    rgb = read_exr(args.source)
    h, w = rgb.shape[:2]
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    best, tried = fit_layout(rgb)
    print(json.dumps(dict(size=[w, h], min=float(lum.min()), median=float(np.median(lum)),
                          p99=float(np.percentile(lum, 99)), p9999=float(np.percentile(lum, 99.99)),
                          max=float(lum.max()), layouts=tried, chosen=best), indent=2), flush=True)
    if best["pole_error"] > 5 or best["core_error"] > 10:
        raise SystemExit("no RA layout puts the band on the real galactic plane: not an equatorial star map?")
    if args.gain is not None:
        gain = args.gain
    elif args.reference:
        gain = reference_median(args.reference) / max(float(np.median(lum)), 1e-9)
    else:
        gain = 1.0
    # Our column x holds RA 360 (x + 0.5) / w; the source's column for that RA, per the fitted layout.
    ra = (np.arange(w) + 0.5) / w
    src_u = (best["origin"] + best["sign"] * ra) % 1.0
    src_x = np.floor(src_u * w).astype(int) % w
    if src_x.shape[0] == w and h == w // 2:
        remapped = rgb[:, src_x] * np.float32(gain)
    else:
        raise SystemExit("expected a 2:1 equirectangular map")
    writer = PngWriter(args.out, w, h)
    for row in range(0, h, 64):
        writer.rows(encode(np.maximum(remapped[row:row + 64], 0), row, args.seed))
    writer.close()
    if args.preview_dir:
        previews(args.out, args.preview_dir, args.preview_gain)
    print(json.dumps(dict(out=str(args.out.resolve()), gain=round(gain, 5), seconds=round(time.perf_counter() - start, 1),
                          sha256=sha256(args.out)), indent=2), flush=True)


if __name__ == "__main__":
    main()
