#!/usr/bin/env python3
"""Deterministic procedural J2000 emission sky; no downloaded catalogues."""

import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import time
import zlib

import numpy as np
from PIL import Image


DEFAULT_SEED = 271828
DEFAULT_STYLE = "natural"
ROOT = Path(__file__).resolve().parents[3]
DEFAULT_OUT = ROOT / "Assets/_Game/Data/Environment/TimeOfDay/StarMap/StarMap.png"
DEG = np.pi / 180.0
# Equatorial J2000 unit vector -> IAU galactic unit vector.
EQ_TO_GAL = np.array([
    [-0.0548755604, -0.8734370902, -0.4838350155],
    [0.4941094279, -0.4448296300, 0.7469822445],
    [-0.8676661490, -0.1980763734, 0.4559837762],
], dtype=np.float64)

# Looks, all linear light before the encoder's shoulder. size scales every angular extent of the
# Milky Way (band, bulge, nebulae, dust); band / wing / bulge / nebula are emission levels; the
# star values set count, brightness and how strongly stars crowd into the band.
STYLES = {
    # Neutral silver band, soft warm core, mottled dust: refs with desert / lake skies.
    "natural": dict(size=0.62, band=0.016, wing=0.005, bulge=0.075, clouds=1.0, grain=0.55,
                    dust=1.0, rift=1.0, nebula=0.25, band_color=(0.82, 0.87, 1.0),
                    core_color=(1.0, 0.80, 0.60), nebula_color=(1.0, 0.42, 0.55),
                    field_stars=1_100_000, disk_stars=1_500_000, star_flux=1.0, star_band=1.0,
                    star_saturation=0.40, halo=1.0, saturation=0.85),
    # Colourful astrophoto: rosy core, pink emission, violet band edges.
    "vivid": dict(size=0.66, band=0.018, wing=0.006, bulge=0.095, clouds=1.15, grain=0.6,
                  dust=1.25, rift=1.2, nebula=1.0, band_color=(0.80, 0.80, 1.0),
                  core_color=(1.0, 0.74, 0.62), nebula_color=(1.0, 0.36, 0.62),
                  field_stars=1_100_000, disk_stars=1_600_000, star_flux=1.0, star_band=1.0,
                  star_saturation=0.55, halo=1.1, saturation=1.15),
    # Mostly a star field: a faint, narrow, cool band that blends in.
    "subtle": dict(size=0.52, band=0.010, wing=0.0035, bulge=0.045, clouds=0.9, grain=0.7,
                   dust=0.8, rift=0.8, nebula=0.15, band_color=(0.78, 0.85, 1.0),
                   core_color=(0.98, 0.86, 0.72), nebula_color=(1.0, 0.45, 0.6),
                   field_stars=1_400_000, disk_stars=1_700_000, star_flux=1.15, star_band=1.25,
                   star_saturation=0.35, halo=1.0, saturation=0.75),
    # Dramatic dust: a darker, branching Great Rift through a warm band.
    "rift": dict(size=0.6, band=0.017, wing=0.0045, bulge=0.085, clouds=1.25, grain=0.5,
                 dust=1.6, rift=1.6, nebula=0.45, band_color=(0.86, 0.86, 0.96),
                 core_color=(1.0, 0.76, 0.54), nebula_color=(1.0, 0.40, 0.52),
                 field_stars=1_100_000, disk_stars=1_500_000, star_flux=1.0, star_band=0.9,
                 star_saturation=0.45, halo=1.0, saturation=0.95),
}


def unit(lon, lat):
    c = np.cos(lat)
    return np.stack((c * np.cos(lon), c * np.sin(lon), np.sin(lat)), axis=-1)


def gal_to_eq(lon, lat):
    return unit(lon, lat) @ EQ_TO_GAL


def hash_grid(x, y, z, seed):
    with np.errstate(over="ignore"):
        h = (x.astype(np.uint32) * np.uint32(374761393)
             + y.astype(np.uint32) * np.uint32(668265263)
             + z.astype(np.uint32) * np.uint32(2246822519)
             + np.uint32(seed & 0xFFFFFFFF))
        h = (h ^ (h >> 13)) * np.uint32(1274126177)
        h ^= h >> 16
    return (h >> 8).astype(np.float32) * np.float32(1.0 / 16777216.0)


def noise(p, frequency, seed):
    q = p * np.float32(frequency) + np.float32(137.25)
    cell = np.floor(q).astype(np.int32)
    t = q - cell
    t = (t * t * (3.0 - 2.0 * t)).astype(np.float32)
    x, y, z = cell.T
    tx, ty, tz = t.T
    result = np.zeros(len(p), np.float32)
    for iz in range(2):
        wz = tz if iz else 1.0 - tz
        for iy in range(2):
            wy = ty if iy else 1.0 - ty
            for ix in range(2):
                wx = tx if ix else 1.0 - tx
                result += hash_grid(x + ix, y + iy, z + iz, seed) * wx * wy * wz
    return result


_FBM_STD = {}


def fbm(p, frequency, seed, octaves, gain=0.55):
    """Zero-mean fractal noise scaled to unit standard deviation."""
    total = np.zeros(len(p), np.float32)
    amplitude = 1.0
    for i in range(octaves):
        total += amplitude * (noise(p, frequency * 2.03 ** i, seed + 97 * i) - 0.5)
        amplitude *= gain
    key = (octaves, gain)
    if key not in _FBM_STD:
        probe = np.random.Generator(np.random.PCG64(7)).normal(size=(65536, 3)).astype(np.float32)
        probe /= np.linalg.norm(probe, axis=1, keepdims=True)
        sample = np.zeros(len(probe), np.float32)
        amplitude = 1.0
        for i in range(octaves):
            sample += amplitude * (noise(probe, 40.0 * 2.03 ** i, 1 + 97 * i) - 0.5)
            amplitude *= gain
        _FBM_STD[key] = float(sample.std())
    return total / np.float32(_FBM_STD[key])


def gaussian(x, sigma):
    return np.exp(-0.5 * (x / sigma) ** 2)


def sigmoid(x):
    return 1.0 / (1.0 + np.exp(-x))


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def longitude_delta(lon, center):
    return (lon - center + 180.0) % 360.0 - 180.0


def blob(lon, lat, center, b, width, height):
    return gaussian(longitude_delta(lon, center), width) * gaussian(lat - b, height)


def milky_way(p, seed, s):
    """Galactic unit vectors -> (linear RGB, dust transmission, star crowding 0..1).

    3D noise on the unit sphere keeps it continuous at seams and poles.
    """
    k = s["size"]
    lon = np.arctan2(p[:, 1], p[:, 0]) / DEG
    lat = np.arcsin(np.clip(p[:, 2], -1.0, 1.0)) / DEG
    n = len(p)
    rgb = np.zeros((n, 3), np.float32)
    transmission = np.ones(n, np.float32)
    crowding = np.zeros(n, np.float32)
    mask = np.abs(lat) < 34.0 * k
    if not np.any(mask):
        return rgb, transmission, crowding
    q, l = p[mask], lon[mask]
    b = lat[mask] / k  # latitude in the style's scaled degrees
    inv = 1.0 / k
    # Noise squeezed across the plane makes features stretch along it, like the real band.
    flat = q * np.array([1.0, 1.0, 2.2], np.float32)
    thin = q * np.array([1.0, 1.0, 3.4], np.float32)
    along = q.copy()
    along[:, 2] = 0.0
    along[:, :2] /= np.linalg.norm(along[:, :2], axis=1, keepdims=True)

    inner = gaussian(longitude_delta(l, 10.0), 62.0)
    center = gaussian(longitude_delta(l, 0.0), 13.0 * k)
    warp = fbm(along, 2.6, seed + 3, 3) * 1.1 + fbm(q, 5.0 * inv, seed + 5, 3) * 0.45
    width = 3.2 + 2.2 * inner + 2.4 * center
    bb = b - warp - 0.3 * np.sin(l * DEG * 2.0)
    core = gaussian(bb, width)
    # The taper keeps the wide glow off the noise mask's edge.
    wing = gaussian(b - 0.5 * warp, width * 2.2) * smoothstep(33.0, 22.0, np.abs(b))

    clouds = fbm(flat, 5.5 * inv, seed + 11, 5)
    patch = np.maximum(1.0 + 0.42 * s["clouds"] * clouds, 0.08) ** 1.6
    grain = np.maximum(1.0 + 0.30 * s["grain"] * fbm(q, 120.0 * inv, seed + 13, 3), 0.2)
    # Bright star clouds and the fainter anticenter.
    along_light = (0.30 + 0.70 * inner
                   + 0.55 * blob(l, b, 28, -0.6, 6.0 * k, 2.2)     # Scutum
                   + 0.45 * blob(l, b, 76, 0.8, 10.0 * k, 2.6)     # Cygnus
                   + 0.40 * blob(l, b, 305, -0.4, 14.0 * k, 2.4)   # Crux / Centaurus
                   + 0.35 * blob(l, b, 287, -0.4, 7.0 * k, 2.0)    # Carina
                   + 0.30 * blob(l, b, 333, -0.5, 9.0 * k, 2.0))   # Norma
    band = (s["band"] * core * patch + s["wing"] * wing * (0.6 + 0.4 * patch)) * along_light * grain
    bulge = (s["bulge"] * gaussian(longitude_delta(l, 0.0), 8.5 * k)
             * gaussian(b + 1.6 - 0.4 * warp, 4.6) * patch ** 0.7 * grain)
    crowding[mask] = np.clip((core * patch * along_light + 0.25 * wing
                              + 1.5 * bulge / max(s["bulge"], 1e-6)) / 2.2, 0.0, 1.0)

    # Dust: blotchy lanes along a wandering spine, darkest and branching through the Great Rift.
    spine = fbm(along, 8.0, seed + 131, 4) * 0.9 + fbm(thin, 9.0 * inv, seed + 137, 3) * 0.35
    rift = sigmoid((l + 14.0) / 4.0) * sigmoid((92.0 - l) / 6.0)
    blotch = smoothstep(-0.9, 1.6, fbm(flat, 9.0 * inv, seed + 201, 7, gain=0.62)) ** 1.4
    lane = gaussian(b - spine, width * (0.30 + 0.18 * center))
    branch = gaussian(b - spine + 1.3 + 0.8 * fbm(along, 13.0, seed + 211, 2), 0.55) * inner
    # Thin dark veins, wispy rather than solid.
    ridged = (1.0 - np.abs(np.tanh(0.7 * fbm(thin, 22.0 * inv, seed + 223, 5)))) ** 9
    dust = (1.3 * s["dust"] * lane * blotch * (0.35 + 1.1 * s["rift"] * rift)
            + 0.6 * s["rift"] * branch * blotch * rift
            + 0.7 * s["dust"] * ridged * (0.4 + blotch) * gaussian(b - spine, width * 0.75) * inner)
    dust += 2.2 * blob(l, b, 303, -0.5, 2.6 * k, 1.1 / k ** 0.3)                   # Coalsack
    dust += 1.6 * blob(l, b, 2.0, 5.5, 2.2 * k, 1.3) * (0.7 + 0.6 * blotch)        # Pipe Nebula
    dust += 1.0 * blob(l, b, 356, 13.0, 6.0 * k, 1.4) * blotch     # Ophiuchus streamers
    trans = np.exp(-dust)
    # Dust reddens what it lets through: brown lane edges.
    extinction = np.exp(-dust[:, None] * np.array([0.80, 1.0, 1.25], np.float32))

    wisps = fbm(q, 16.0 * inv, seed + 301, 5)
    hii = np.maximum(wisps - 0.9, 0.0)
    nebula = 0.025 * gaussian(b - 0.3 * warp, width * 0.35) * inner * hii + np.maximum(0.55 + 0.45 * wisps, 0.0) * (
              0.10 * blob(l, b, 6.0, -1.2, 1.1 * k, 0.9)    # Lagoon
              + 0.05 * blob(l, b, 7.0, -0.3, 0.6 * k, 0.6)    # Trifid
              + 0.05 * blob(l, b, 16.0, 0.6, 1.0 * k, 0.8)    # Eagle / Omega
              + 0.06 * blob(l, b, 352.0, 16.5, 2.5 * k, 2.0)  # Rho Ophiuchi
              + 0.08 * blob(l, b, 287.6, -0.6, 1.4 * k, 1.0)  # Eta Carinae
              + 0.05 * blob(l, b, 85.0, -1.0, 2.2 * k, 1.6)   # North America
              + 0.04 * blob(l, b, 79.0, 1.5, 4.0 * k, 2.0)    # Sadr region
              + 0.03 * blob(l, b, 134.0, 0.8, 2.0 * k, 1.2))  # Heart and Soul
    nebula = s["nebula"] * nebula * (0.5 + 0.5 * patch) * grain

    light = (band[:, None] * np.array(s["band_color"], np.float32)
             + bulge[:, None] * np.array(s["core_color"], np.float32)
             + nebula[:, None] * np.array(s["nebula_color"], np.float32))
    light *= extinction
    luma = light @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    light = luma[:, None] + s["saturation"] * (light - luma[:, None])
    rgb[mask] = np.maximum(light, 0.0)
    transmission[mask] = trans
    return rgb, transmission, crowding


def star_colors(rng, count, saturation):
    # Approximate blackbody colours with a mostly F/G/A stellar population.
    bv = np.clip(rng.normal(0.48, 0.43, count), -0.30, 1.9)
    temp = 4600.0 * (1.0 / (0.92 * bv + 1.7) + 1.0 / (0.92 * bv + 0.62)) / 100.0
    red = np.where(temp <= 66, 255.0, 329.6987 * np.maximum(temp - 60, 1) ** -0.1332)
    green = np.where(temp <= 66, 99.4708 * np.log(temp) - 161.1196,
                     288.1222 * np.maximum(temp - 60, 1) ** -0.07551)
    blue = np.where(temp >= 66, 255.0,
                    np.where(temp <= 19, 0.0, 138.5177 * np.log(np.maximum(temp - 10, 1)) - 305.0448))
    srgb = np.clip(np.stack((red, green, blue), axis=1) / 255.0, 0, 1)
    linear = np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)
    return (saturation * linear + (1.0 - saturation)).astype(np.float32)


def catalogue(seed, width, s):
    rng = np.random.Generator(np.random.PCG64(seed))
    # These counts are angular, independent of output resolution.
    field_count, disk_count = s["field_stars"], s["disk_stars"]
    field = unit(rng.uniform(0, 2 * np.pi, field_count),
                 np.arcsin(rng.uniform(-1, 1, field_count))).astype(np.float32)
    # Disk stars crowd where the band is bright, so the star clouds read as made of stars.
    disk_parts, transmissions = [], []
    remaining = disk_count
    while remaining:
        n = int(remaining * 3.0) + 4096
        l = rng.uniform(-np.pi, np.pi, n)
        b = rng.normal(0.0, (5.0 + 2.0 * np.cos(l)) * s["size"] * DEG, n)
        b = np.clip(b, -np.pi / 2, np.pi / 2)
        candidates = unit(l, b).astype(np.float32)
        crowd = np.empty(n, np.float32)
        trans = np.empty(n, np.float32)
        for start in range(0, n, 65536):
            _, t, c = milky_way(candidates[start:start + 65536], seed, s)
            crowd[start:start + len(c)], trans[start:start + len(t)] = c, t
        weight = (0.18 + 0.82 * crowd ** s["star_band"]) * np.cos(b)
        accept = np.flatnonzero(rng.random(n) < weight)[:remaining]
        disk_parts.append(candidates[accept])
        transmissions.append(trans[accept])
        remaining -= len(accept)
    field_trans = np.empty(field_count, np.float32)
    for start in range(0, field_count, 65536):
        _, t, _ = milky_way(field[start:start + 65536], seed, s)
        field_trans[start:start + len(t)] = t
    gal = np.concatenate([field, *disk_parts])
    # Field stars are mostly nearby, in front of the dust; disk stars sit in or behind it.
    attenuation = np.concatenate([0.75 + 0.25 * field_trans,
                                  0.08 + 0.92 * np.concatenate(transmissions) ** 0.8])
    eq = gal.astype(np.float64) @ EQ_TO_GAL
    ra = np.arctan2(eq[:, 1], eq[:, 0]) % (2 * np.pi)
    dec = np.arcsin(np.clip(eq[:, 2], -1, 1))
    n = len(gal)
    m_max, m_min = 13.5, -1.4
    growth = 2.75
    low = growth ** (m_min - m_max)
    magnitude = m_max + np.log(low + (1 - low) * rng.random(n)) / np.log(growth)
    color = star_colors(rng, n, s["star_saturation"])
    # Flatter than the real 0.4 slope: faint stars stay visible after mipmapping and the night grade.
    flux = (0.011 * s["star_flux"] * 10.0 ** (0.32 * (m_max - magnitude))).astype(np.float32)
    flux *= attenuation
    flux = 2.5 * np.tanh(flux / 2.5)
    scale = width / 8192.0
    sigma = (0.42 + 0.55 * np.clip((9.5 - magnitude) / 8.0, 0, 1)) * scale
    x = ra * width / (2 * np.pi) - 0.5
    y = (0.5 - dec / np.pi) * (width // 2) - 0.5
    order = np.argsort(y, kind="stable")
    stars = dict(x=x[order], y=y[order], dec=dec[order], sigma=sigma[order],
                 flux=flux[order], color=color[order], magnitude=magnitude[order])
    print(f"Stars: {n:,}; m<6: {np.count_nonzero(magnitude < 6)}; "
          f"m<9: {np.count_nonzero(magnitude < 9)}", flush=True)
    return stars


def paint_stars(image, row, stars, width, halo_strength):
    """Spherical Gaussian PSFs: local 1/cos(dec), exact distance in polar caps."""
    height, scale = width // 2, width / 8192.0
    margin = int(math.ceil(10 * scale)) + 2
    lo, hi = np.searchsorted(stars["y"], [row - margin, row + len(image) + margin])
    subset = {k: v[lo:hi] for k, v in stars.items()}
    for halo in (False, True):
        select = subset["magnitude"] < 6.0 if halo else np.ones(hi - lo, bool)
        s = {k: v[select] for k, v in subset.items()}
        if not len(s["x"]):
            continue
        sigma = np.full(len(s["x"]), 2.4 * scale) if halo else s["sigma"]
        peak = np.minimum(s["flux"], 0.40) * 0.05 * halo_strength if halo else s["flux"]
        # Binning avoids giving all faint stars the support of a bright halo.
        radius_y = np.ceil(3.5 * sigma).astype(int)
        cosdec = np.cos(s["dec"])
        radius_x = np.minimum(np.ceil(3.5 * sigma / np.maximum(cosdec, 1e-9)), width // 2).astype(int)
        regular = np.abs(s["dec"]) < 86 * DEG
        for ry in np.unique(radius_y[regular]):
            for rx in np.unique(radius_x[regular & (radius_y == ry)]):
                use = np.flatnonzero(regular & (radius_y == ry) & (radius_x == rx))
                if not len(use):
                    continue
                cx = np.floor(s["x"][use] + 0.5).astype(int)
                cy = np.floor(s["y"][use] + 0.5).astype(int)
                for dy in range(-int(ry), int(ry) + 1):
                    py = cy + dy
                    valid = (py >= row) & (py < row + len(image))
                    ids = use[valid]
                    if not len(ids):
                        continue
                    yy = py[valid] - row
                    gy = ((py[valid] - s["y"][ids]) / sigma[ids]) ** 2
                    for dx in range(-int(rx), int(rx) + 1):
                        px = cx[valid] + dx
                        gx = ((px - s["x"][ids]) * cosdec[ids] / sigma[ids]) ** 2
                        weight = np.exp(-0.5 * (gx + gy)) * peak[ids]
                        for channel in range(3):
                            np.add.at(image[:, :, channel], (yy, px % width),
                                      weight * s["color"][ids, channel])
        # Great-circle haversine distances remain well conditioned near either pole.
        for i in np.flatnonzero(~regular):
            angular_sigma = sigma[i] * (2 * np.pi / width)
            dec0 = s["dec"][i]
            y0 = max(row, int(np.floor(s["y"][i] - 3.5 * sigma[i])))
            y1 = min(row + len(image), int(np.ceil(s["y"][i] + 3.5 * sigma[i])) + 1)
            if y1 <= y0:
                continue
            reaches_pole = np.pi / 2 - abs(dec0) < 3.5 * angular_sigma
            rx = width // 2 if reaches_pole else min(width // 2, int(radius_x[i] * 1.3) + 2)
            px = (np.arange(-rx, rx + 1) + int(round(s["x"][i]))) % width
            px = np.unique(px)
            dra = (px - s["x"][i]) * 2 * np.pi / width
            dec = np.pi / 2 - (np.arange(y0, y1) + 0.5) * np.pi / height
            hav = (np.sin((dec[:, None] - dec0) / 2) ** 2
                   + np.cos(dec[:, None]) * np.cos(dec0) * np.sin(dra[None, :] / 2) ** 2)
            angle = 2 * np.arcsin(np.sqrt(np.clip(hav, 0, 1)))
            weight = np.exp(-0.5 * (angle / angular_sigma) ** 2) * peak[i]
            image[y0 - row:y1 - row, px, :] += weight[:, :, None] * s["color"][i]


class PngWriter:
    """Streaming RGB PNG with explicit sRGB transfer metadata; no full-frame buffer."""

    def __init__(self, path, width, height):
        self.file = path.open("wb")
        self.file.write(b"\x89PNG\r\n\x1a\n")
        self.chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        self.chunk(b"sRGB", b"\x00")
        self.chunk(b"gAMA", struct.pack(">I", 45455))
        self.compressor = zlib.compressobj(6)

    def chunk(self, tag, data):
        self.file.write(struct.pack(">I", len(data)))
        self.file.write(tag)
        self.file.write(data)
        self.file.write(struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    def rows(self, rgb):
        # PNG Sub filter, computed in uint8 arithmetic before DEFLATE.
        filtered = rgb.copy()
        filtered[:, 1:, :] -= rgb[:, :-1, :]
        data = np.empty((len(rgb), rgb.shape[1] * 3 + 1), np.uint8)
        data[:, 0] = 1
        data[:, 1:] = filtered.reshape(len(rgb), -1)
        compressed = self.compressor.compress(data.tobytes())
        if compressed:
            self.chunk(b"IDAT", compressed)

    def close(self):
        self.chunk(b"IDAT", self.compressor.flush())
        self.chunk(b"IEND", b"")
        self.file.close()


def encode(linear, row, seed):
    # Gentle shoulder, 0.89 linear ceiling (242 sRGB); no large hard-clipped areas.
    mapped = 0.89 * (-np.expm1(-linear / 0.89))
    srgb = np.where(mapped <= 0.0031308, 12.92 * mapped,
                    1.055 * np.maximum(mapped, 0) ** (1 / 2.4) - 0.055)
    yy = np.arange(row, row + len(linear), dtype=np.uint32)[:, None]
    xx = np.arange(linear.shape[1], dtype=np.uint32)[None, :]
    dither = hash_grid(xx, yy, np.zeros_like(xx), seed + 12345) - 0.5
    return np.clip(np.floor(srgb * 255 + 0.5 + dither[:, :, None]), 0, 255).astype(np.uint8)


def generate(width, seed, out, strip_rows, s):
    stars = catalogue(seed, width, s)
    height = width // 2
    ra = (np.arange(width, dtype=np.float64) + 0.5) * (2 * np.pi / width)
    cosra, sinra = np.cos(ra), np.sin(ra)
    writer = PngWriter(out, width, height)
    for row in range(0, height, strip_rows):
        rows = min(strip_rows, height - row)
        dec = np.pi / 2 - (np.arange(row, row + rows) + 0.5) * np.pi / height
        eq = np.empty((rows, width, 3), np.float32)
        eq[:, :, 0] = np.cos(dec)[:, None] * cosra
        eq[:, :, 1] = np.cos(dec)[:, None] * sinra
        eq[:, :, 2] = np.sin(dec)[:, None]
        gal = eq.reshape(-1, 3) @ EQ_TO_GAL.astype(np.float32).T
        linear, _, _ = milky_way(gal, seed, s)
        linear = linear.reshape(rows, width, 3)
        paint_stars(linear, row, stars, width, s["halo"])
        writer.rows(encode(linear, row, seed))
        if row % (strip_rows * 16) == 0:
            print(f"Rows {row + rows}/{height}", flush=True)
    writer.close()


def srgb_to_linear(pixels):
    p = pixels.astype(np.float32) / 255
    return np.where(p <= 0.04045, p / 12.92, ((p + 0.055) / 1.055) ** 2.4)


def pinhole(pixels, lon, lat, roll=0.0, fov=90, size=(1920, 1080), gain=1.0):
    w, h = size
    forward = gal_to_eq(lon * DEG, lat * DEG)
    north = gal_to_eq(lon * DEG, (lat + 0.01) * DEG) - forward
    north -= forward * np.dot(north, forward)
    north /= np.linalg.norm(north)
    right = np.cross(north, forward)
    right, north = (right * np.cos(roll * DEG) + north * np.sin(roll * DEG),
                    north * np.cos(roll * DEG) - right * np.sin(roll * DEG))
    xx = ((np.arange(w) + 0.5) / w * 2 - 1) * np.tan(fov * DEG / 2)
    yy = (1 - (np.arange(h) + 0.5) / h * 2) * np.tan(fov * DEG / 2) * h / w
    output = np.empty((h, w, 3), np.uint8)
    mh, mw = pixels.shape[:2]
    for row in range(0, h, 64):
        d = forward + xx[None, :, None] * right + yy[row:row + 64, None, None] * north
        d /= np.linalg.norm(d, axis=-1, keepdims=True)
        u = np.arctan2(d[:, :, 1], d[:, :, 0]) % (2 * np.pi) * mw / (2 * np.pi) - 0.5
        v = (0.5 - np.arcsin(d[:, :, 2]) / np.pi) * mh - 0.5
        x0, y0 = np.floor(u).astype(int), np.floor(v).astype(int)
        fx, fy = (u - x0)[:, :, None], (v - y0)[:, :, None]
        x1, y1 = (x0 + 1) % mw, np.clip(y0 + 1, 0, mh - 1)
        x0, y0 = x0 % mw, np.clip(y0, 0, mh - 1)
        linear = ((1 - fy) * ((1 - fx) * srgb_to_linear(pixels[y0, x0])
                             + fx * srgb_to_linear(pixels[y0, x1]))
                  + fy * ((1 - fx) * srgb_to_linear(pixels[y1, x0])
                          + fx * srgb_to_linear(pixels[y1, x1])))
        linear = np.minimum(linear * gain, 1.0)
        srgb = np.where(linear <= 0.0031308, 12.92 * linear,
                        1.055 * np.maximum(linear, 0) ** (1 / 2.4) - 0.055)
        output[row:row + len(d)] = np.clip(srgb * 255 + 0.5, 0, 255).astype(np.uint8)
    return Image.fromarray(output)


def previews(out, directory, gain):
    directory.mkdir(parents=True, exist_ok=True)
    with Image.open(out) as source:
        pixels = np.asarray(source)
        source.resize((2048, 1024), Image.Resampling.LANCZOS).save(directory / "whole_map_2048.png")
    # Gain approximates the material's Star Map Intensity, so previews read like the game.
    views = [("galactic_center", 5.0, -1.0, 24.0, 90),
             ("cygnus_great_rift", 65.0, 0.0, -30.0, 90),
             ("high_galactic_latitude", 155.0, 65.0, 0.0, 90),
             ("core_closeup", 0.0, 0.0, 30.0, 40)]
    for name, l, b, roll, fov in views:
        pinhole(pixels, l, b, roll, fov, gain=gain).save(directory / f"{name}_1920.png")
    # Both cap views and the RA=0 crossing make projection defects inspectable.
    for name, eq in [("north_pole", np.array([0., 0., 1.])),
                     ("south_pole", np.array([0., 0., -1.])),
                     ("ra_seam", np.array([1., 0., 0.]))]:
        gal = EQ_TO_GAL @ eq
        l, b = np.arctan2(gal[1], gal[0]) / DEG, np.arcsin(gal[2]) / DEG
        pinhole(pixels, l, b, fov=35, size=(960, 960), gain=gain).save(directory / f"check_{name}.png")


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(data)
    return digest.hexdigest()


def peak_memory_mib():
    if sys.platform == "win32":
        import ctypes
        from ctypes import wintypes

        class Counters(ctypes.Structure):
            _fields_ = [("cb", wintypes.DWORD), ("PageFaultCount", wintypes.DWORD)] + [
                (name, ctypes.c_size_t) for name in (
                    "PeakWorkingSetSize", "WorkingSetSize", "QuotaPeakPagedPoolUsage",
                    "QuotaPagedPoolUsage", "QuotaPeakNonPagedPoolUsage", "QuotaNonPagedPoolUsage",
                    "PagefileUsage", "PeakPagefileUsage")]

        counters = Counters()
        counters.cb = ctypes.sizeof(counters)
        handle = ctypes.windll.kernel32.GetCurrentProcess
        handle.restype = wintypes.HANDLE
        query = ctypes.windll.psapi.GetProcessMemoryInfo
        query.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD]
        query.restype = wintypes.BOOL
        if query(handle(), ctypes.byref(counters), counters.cb):
            return round(counters.PeakWorkingSetSize / 1024 ** 2, 1)
    return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--width", type=int, default=8192)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--style", choices=sorted(STYLES), default=DEFAULT_STYLE)
    parser.add_argument("--size", type=float, help="Overrides the style's Milky Way angular size (0.3..1.5)")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--preview-dir", type=Path, help="Optional previews outside Assets; omit for minimum memory")
    parser.add_argument("--preview-gain", type=float, default=4.0, help="Preview exposure, ~ Star Map Intensity")
    parser.add_argument("--strip-rows", type=int, default=64, help="Working strip height (default 64)")
    args = parser.parse_args()
    if args.width < 512 or args.width % 2:
        parser.error("--width must be even and at least 512")
    if not 1 <= args.strip_rows <= 256:
        parser.error("--strip-rows must be 1..256")
    if not 0 <= args.seed <= 0xFFFFFFFF:
        parser.error("--seed must be a uint32")
    style = dict(STYLES[args.style])
    if args.size is not None:
        if not np.isfinite(args.size) or not 0.3 <= args.size <= 1.5:
            parser.error("--size must be finite and within 0.3..1.5")
        style["size"] = args.size
    args.out.parent.mkdir(parents=True, exist_ok=True)
    start = time.perf_counter()
    generate(args.width, args.seed, args.out, args.strip_rows, style)
    generation_seconds = time.perf_counter() - start
    if args.preview_dir:
        previews(args.out, args.preview_dir, args.preview_gain)
    print(json.dumps(dict(out=str(args.out.resolve()), width=args.width, height=args.width // 2,
                          mode="RGB", seed=args.seed, style=args.style, size=style["size"],
                          generation_seconds=round(generation_seconds, 3),
                          total_seconds=round(time.perf_counter() - start, 3),
                          peak_working_set_mib=peak_memory_mib(), sha256=sha256(args.out)), indent=2), flush=True)


if __name__ == "__main__":
    main()
