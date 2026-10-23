# Generates Data/UI/DeathScreen/Eyelid.png (upper eyelid: black above an arched soft edge) and Glow.png (white, soft radial alpha).
# Usage: python AgentScripts/DeathScreen/gen_textures.py Assets/_Game/Data/UI/DeathScreen  (needs numpy). Keep DeathScreenStyle.EyelidSolidAt = arch + feather.
import numpy as np, zlib, struct, sys
out = sys.argv[1]
def png(path, rgba):
    h, w, _ = rgba.shape
    raw = b''.join(b'\x00' + rgba[y].tobytes() for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')
    open(path, 'wb').write(data)
def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
# Upper eyelid: black above an arched edge (highest in the middle), soft edge above it. Row 0 of the PNG is the top.
W, H = 512, 512
u = np.linspace(-1, 1, W)[None, :]
v = np.linspace(1, 0, H)[:, None]          # 1 at the top row, 0 at the bottom row
arch, feather = 0.22, 0.08
edge = arch * (1 - u ** 2)
a = smooth(edge, edge + feather, v)
lid = np.zeros((H, W, 4), np.uint8); lid[..., 3] = (a * 255).astype(np.uint8)
png(out + '/Eyelid.png', lid)
# Glow: white, alpha falling off from the middle (elliptical), for the menu background.
W, H = 256, 256
x = np.linspace(-1, 1, W)[None, :]; y = np.linspace(-1, 1, H)[:, None]
r = np.sqrt(x ** 2 + (y * 1.15) ** 2)
a = np.clip(1 - smooth(0.0, 1.05, r), 0, 1) ** 1.3
glow = np.full((H, W, 4), 255, np.uint8); glow[..., 3] = (a * 255).astype(np.uint8)
png(out + '/Glow.png', glow)
print('ok')
