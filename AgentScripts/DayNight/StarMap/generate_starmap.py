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
DEFAULT_BAND_SCALE = 1.0
ROOT = Path(__file__).resolve().parents[3]
DEFAULT_OUT = ROOT / "Assets/_Game/Data/Environment/TimeOfDay/StarMap/StarMap.png"
DEG = np.pi / 180.0
# Equatorial J2000 unit vector -> IAU galactic unit vector.
EQ_TO_GAL = np.array([
    [-0.0548755604, -0.8734370902, -0.4838350155],
    [0.4941094279, -0.4448296300, 0.7469822445],
    [-0.8676661490, -0.1980763734, 0.4559837762],
], dtype=np.float64)


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


def gaussian(x, sigma):
    return np.exp(-0.5 * (x / sigma) ** 2)


def longitude_delta(lon, center):
    return (lon - center + 180.0) % 360.0 - 180.0


def cloud(lon, lat, center, width, b, height):
    return gaussian(longitude_delta(lon, center), width) * gaussian(lat - b, height)


def milky_way(p, seed, band_scale=DEFAULT_BAND_SCALE):
    """Continuous 3D noise on the unit sphere, including at seams and poles."""
    lon = np.arctan2(p[:, 1], p[:, 0]) / DEG
    lat = np.arcsin(np.clip(p[:, 2], -1.0, 1.0)) / DEG
    rgb = np.zeros((len(p), 3), np.float32)
    transmission = np.ones(len(p), np.float32)
    mask = np.abs(lat) < 30.0 * band_scale
    if not np.any(mask):
        return rgb, transmission
    q, l, b = p[mask], lon[mask], lat[mask] / band_scale
    # Stretch noise along the plane so absorption forms long, fine filaments.
    filament_q = q * np.array([0.28, 0.28, 2.4], np.float32)
    along = q.copy()
    along[:, 2] = 0.0
    along[:, :2] /= np.linalg.norm(along[:, :2], axis=1, keepdims=True)
    spine0 = noise(along, 17.0, seed + 131)
    spine1 = noise(along, 51.0, seed + 151)
    n0 = noise(q, 4.0, seed + 11)
    n1 = noise(filament_q, 11.0, seed + 23)
    n2 = noise(filament_q, 29.0, seed + 37)
    n3 = noise(filament_q, 73.0, seed + 51)
    n4 = noise(q, 183.0, seed + 71)
    n5 = noise(q, 457.0, seed + 91)
    n6 = noise(q, 1051.0, seed + 111)
    texture = (1.0 + 0.15 * (n0 - 0.5) + 0.50 * (n1 - 0.5)
               + 0.70 * (n2 - 0.5) + 0.60 * (n3 - 0.5)
               + 0.35 * (n4 - 0.5) + 0.20 * (n5 - 0.5) + 0.10 * (n6 - 0.5))
    texture = np.maximum(texture, 0.25)
    center = gaussian(longitude_delta(l, 0.0), 14.5 * band_scale)
    inner = gaussian(longitude_delta(l, 12.0), 71.0)
    width = (2.6 + 2.9 * inner + 2.0 * center) * (0.48 + 0.12 * inner)
    bend = 0.35 * np.sin(l * DEG * 2.0) + 0.35 * (n0 - 0.5)
    band = gaussian(b - bend, width)
    envelope = gaussian(b, width * 1.9)
    clouds = (0.8 * cloud(l, b, 76, 10, 0.65, 1.5)
              + 1.05 * cloud(l, b, 28, 5.5, -0.5, 1.0)
              + 0.60 * cloud(l, b, 305, 18, -0.4, 1.5)
              + 0.65 * cloud(l, b, 287, 8, -0.35, 1.15)
              + 0.22 * cloud(l, b, 145, 13, 0.4, 1.0))
    cold = (0.006 * envelope * (0.25 + 0.75 * inner)
            + 0.043 * band * (0.28 + 0.72 * inner)
            + 0.012 * clouds) * texture
    bulge = 0.29 * center * gaussian(b + 0.45, 3.5) * texture ** 0.55
    # A ragged, branching absorption lane from Sagittarius through Cygnus.
    rift_extent = (1.0 / (1.0 + np.exp(-(l + 13.0) / 5.0))
                   * 1.0 / (1.0 + np.exp((l - 94.0) / 7.0)))
    lane_center = (0.15 + 0.55 * np.sin((l - 5.0) * DEG * 2.6)
                   + 1.25 * (spine0 - 0.5) + 0.55 * (spine1 - 0.5) + 0.12 * (n2 - 0.5))
    lane_width = 0.24 + 0.70 * spine0 + 0.18 * center
    lane = gaussian(b - lane_center, lane_width) * rift_extent
    branch = gaussian(b - lane_center + 0.60 + 0.90 * spine0, 0.12 + 0.18 * spine1) * inner
    upper_branch = gaussian(b - lane_center - 0.9 - 0.65 * spine1, 0.18 + 0.14 * n2) * inner
    ridges = (1.0 - np.abs(2.0 * n2 - 1.0)) ** 10
    fine_dust = (0.80 * ridges + 0.30 * n3) * gaussian(b - lane_center, 1.25 + 0.65 * center)
    dust = 2.3 * lane * (0.8 + spine1) + 1.3 * branch + 0.65 * upper_branch + fine_dust
    dust += 3.2 * cloud(l, b, 303, 2.8, -0.25, 1.0) * (0.9 + 0.2 * n3)
    trans = np.exp(-dust)
    # Extinction removes blue more strongly, leaving brown/amber lane edges.
    extinction = np.exp(-dust[:, None] * np.array([0.78, 1.0, 1.27], np.float32))
    light = cold[:, None] * np.array([0.69, 0.83, 1.0], np.float32)
    light += bulge[:, None] * np.array([1.0, 0.74, 0.43], np.float32)
    rgb[mask] = light * extinction
    transmission[mask] = trans
    return rgb, transmission


def star_colors(rng, count):
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
    return (0.62 * linear + 0.38).astype(np.float32)


def catalogue(seed, width, band_scale=DEFAULT_BAND_SCALE):
    rng = np.random.Generator(np.random.PCG64(seed))
    # These counts are angular, independent of output resolution.
    field_count, disk_count = 430_000, 1_170_000
    field = unit(rng.uniform(0, 2 * np.pi, field_count),
                 np.arcsin(rng.uniform(-1, 1, field_count)))
    disk_parts = []
    remaining = disk_count
    while remaining:
        n = int(remaining * 2.8) + 64
        l = rng.uniform(-np.pi, np.pi, n)
        b = rng.normal(0.0, (4.7 + 1.8 * np.cos(l)) * DEG, n)
        # cos(b) is the solid-angle Jacobian; the longitudinal factor adds bulge density.
        accept = ((np.abs(b) < np.pi / 2)
                  & (rng.random(n) < np.cos(b) * (0.53 + 0.47 * np.cos(l))))
        part = unit(l[accept][:remaining], b[accept][:remaining])
        disk_parts.append(part)
        remaining -= len(part)
    gal = np.concatenate([field, *disk_parts]).astype(np.float32)
    eq = gal.astype(np.float64) @ EQ_TO_GAL
    ra = np.arctan2(eq[:, 1], eq[:, 0]) % (2 * np.pi)
    dec = np.arcsin(np.clip(eq[:, 2], -1, 1))
    n = len(gal)
    m_max, m_min = 13.0, -1.5
    low = 3.0 ** (m_min - m_max)
    magnitude = m_max + np.log(low + (1 - low) * rng.random(n)) / np.log(3.0)
    color = star_colors(rng, n)
    attenuation = np.empty(n, np.float32)
    for start in range(0, n, 65536):
        _, trans = milky_way(gal[start:start + 65536], seed, band_scale)
        attenuation[start:start + len(trans)] = 0.12 + 0.88 * trans ** 0.65
        foreground_end = min(field_count, start + len(trans))
        if start < foreground_end:
            attenuation[start:foreground_end] = 0.70 + 0.30 * trans[:foreground_end - start]
    flux = (0.0075 * 10.0 ** (0.4 * (m_max - magnitude))).astype(np.float32)
    flux *= attenuation
    flux = 3.0 * np.tanh(flux / 3.0)
    scale = width / 8192.0
    sigma = (0.40 + 0.30 * np.clip((11.5 - magnitude) / 6.5, 0, 1)) * scale
    x = ra * width / (2 * np.pi) - 0.5
    y = (0.5 - dec / np.pi) * (width // 2) - 0.5
    order = np.argsort(y, kind="stable")
    stars = dict(x=x[order], y=y[order], dec=dec[order], sigma=sigma[order],
                 flux=flux[order], color=color[order], magnitude=magnitude[order])
    print(f"Stars: {n:,}; m<6: {np.count_nonzero(magnitude < 6)}; "
          f"m<8: {np.count_nonzero(magnitude < 8)}", flush=True)
    return stars


def paint_stars(image, row, stars, width):
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
        peak = np.minimum(s["flux"], 0.40) * 0.045 if halo else s["flux"]
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


def generate(width, seed, out, strip_rows, band_scale=DEFAULT_BAND_SCALE):
    stars = catalogue(seed, width, band_scale)
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
        linear, _ = milky_way(gal, seed, band_scale)
        linear = linear.reshape(rows, width, 3)
        paint_stars(linear, row, stars, width)
        writer.rows(encode(linear, row, seed))
        if row % (strip_rows * 16) == 0:
            print(f"Rows {row + rows}/{height}", flush=True)
    writer.close()


def srgb_to_linear(pixels):
    p = pixels.astype(np.float32) / 255
    return np.where(p <= 0.04045, p / 12.92, ((p + 0.055) / 1.055) ** 2.4)


def pinhole(pixels, lon, lat, roll=0.0, fov=90, size=(1920, 1080)):
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
        srgb = np.where(linear <= 0.0031308, 12.92 * linear,
                        1.055 * np.maximum(linear, 0) ** (1 / 2.4) - 0.055)
        output[row:row + len(d)] = np.clip(srgb * 255 + 0.5, 0, 255).astype(np.uint8)
    return Image.fromarray(output)


def previews(out, directory):
    directory.mkdir(parents=True, exist_ok=True)
    with Image.open(out) as source:
        pixels = np.asarray(source)
        source.resize((2048, 1024), Image.Resampling.LANCZOS).save(directory / "whole_map_2048.png")
    views = [("galactic_center", 5.0, -1.0, 24.0),
             ("cygnus_great_rift", 65.0, 0.0, -30.0),
             ("high_galactic_latitude", 155.0, 65.0, 0.0)]
    for name, l, b, roll in views:
        pinhole(pixels, l, b, roll).save(directory / f"{name}_1920.png")
    # Both cap views and the RA=0 crossing make projection defects inspectable.
    for name, eq in [("north_pole", np.array([0., 0., 1.])),
                     ("south_pole", np.array([0., 0., -1.])),
                     ("ra_seam", np.array([1., 0., 0.]))]:
        gal = EQ_TO_GAL @ eq
        l, b = np.arctan2(gal[1], gal[0]) / DEG, np.arcsin(gal[2]) / DEG
        pinhole(pixels, l, b, fov=35, size=(960, 960)).save(directory / f"check_{name}.png")


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
    parser.add_argument("--band-scale", type=float, default=DEFAULT_BAND_SCALE,
                        help="Angular scale relative to the NEW narrow baseline (default 1.0); 0.25..2.0")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--preview-dir", type=Path, help="Optional previews outside Assets; omit for minimum memory")
    parser.add_argument("--strip-rows", type=int, default=64, help="Working strip height (default 64)")
    args = parser.parse_args()
    if args.width < 512 or args.width % 2:
        parser.error("--width must be even and at least 512")
    if not 1 <= args.strip_rows <= 256:
        parser.error("--strip-rows must be 1..256")
    if not 0 <= args.seed <= 0xFFFFFFFF:
        parser.error("--seed must be a uint32")
    if not np.isfinite(args.band_scale) or not 0.25 <= args.band_scale <= 2.0:
        parser.error("--band-scale must be finite and within 0.25..2.0")
    args.out.parent.mkdir(parents=True, exist_ok=True)
    start = time.perf_counter()
    generate(args.width, args.seed, args.out, args.strip_rows, args.band_scale)
    generation_seconds = time.perf_counter() - start
    if args.preview_dir:
        previews(args.out, args.preview_dir)
    print(json.dumps(dict(out=str(args.out.resolve()), width=args.width, height=args.width // 2,
                          mode="RGB", seed=args.seed, band_scale=args.band_scale,
                          generation_seconds=round(generation_seconds, 3),
                          total_seconds=round(time.perf_counter() - start, 3),
                          peak_working_set_mib=peak_memory_mib(), sha256=sha256(args.out)), indent=2), flush=True)


if __name__ == "__main__":
    main()
