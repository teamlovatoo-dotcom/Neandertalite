"""Procedural textures: bone, hide (from leather_white), feather, plus copies/tints of downloaded ones."""
import numpy as np, sys, os
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
rng = np.random.default_rng(7)

def tile_noise(n, freq, octaves=5, seed=0, stretch=(1, 1)):
    r = np.random.default_rng(seed)
    acc = np.zeros((n, n)); amp = 1; tot = 0
    for o in range(octaves):
        fx, fy = int(freq * stretch[0] * 2 ** o), int(freq * stretch[1] * 2 ** o)
        g = r.standard_normal((fy, fx))
        # bilinear tileable upsample
        ys = np.arange(n) * fy / n; xs = np.arange(n) * fx / n
        y0 = np.floor(ys).astype(int); x0 = np.floor(xs).astype(int)
        ty = (ys - y0)[:, None]; tx = (xs - x0)[None, :]
        ty = ty * ty * (3 - 2 * ty); tx = tx * tx * (3 - 2 * tx)
        y1 = (y0 + 1) % fy; x1 = (x0 + 1) % fx
        a = g[y0][:, x0]; b = g[y0][:, x1]; c = g[y1][:, x0]; d = g[y1][:, x1]
        acc += amp * ((a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty)
        tot += amp; amp *= 0.5
    acc /= tot
    return (acc - acc.min()) / (acc.max() - acc.min() + 1e-9)

def normal_from_height(h, strength):
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    nz = np.ones_like(h)
    l = np.sqrt(gx * gx + gy * gy + 1)
    n = np.stack([-gx / l, gy / l, nz / l], -1)   # OpenGL convention (green up)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)

def save(name, rgb, nrm):
    Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).save(os.path.join(out, name + "_diff.jpg"), quality=90)
    Image.fromarray(nrm).save(os.path.join(out, name + "_nor_gl.jpg"), quality=92)

N = 1024
# ---------- bone: ivory with long fibres, pores and stains ----------
fib = tile_noise(N, 3, 6, 1, (14, 1))          # stretched along v -> fibres along length
blot = tile_noise(N, 3, 5, 2)
pores = tile_noise(N, 48, 2, 3)
pores = np.clip((pores - 0.8) * 5, 0, 1)
base = np.array([214, 200, 172], float)
stain = np.array([150, 120, 82], float)
t = np.clip((blot - 0.45) * 1.8, 0, 1)[..., None]
rgb = base * (1 - t) + stain * t
rgb *= (0.86 + 0.22 * fib)[..., None]
rgb *= (1 - 0.2 * pores)[..., None]
h = fib * 0.6 + blot * 0.3 - pores * 0.5
save("bone", rgb, normal_from_height(h, 4.0))

# ---------- hide: rawhide from leather_white normal + tan colour blotches ----------
lw = np.asarray(Image.open(os.path.join(src, "leather_white/diff.jpg")).convert("RGB").resize((N, N)), float)
lum = lw.mean(-1) / 255.0
blot = tile_noise(N, 2, 6, 4)
blot2 = tile_noise(N, 6, 4, 5)
light = np.array([196, 160, 112], float); dark = np.array([112, 78, 48], float)
t = np.clip(blot * 0.8 + blot2 * 0.35 - 0.2, 0, 1)[..., None]
rgb = light * (1 - t) + dark * t
rgb *= (0.65 + 0.6 * (lum / lum.mean()) * 0.6)[..., None]
scrape = tile_noise(N, 2, 6, 6, (1, 9))
rgb *= (0.88 + 0.24 * scrape)[..., None]
fine = tile_noise(N, 40, 3, 10)
rgb *= (0.92 + 0.16 * fine)[..., None]
Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).save(os.path.join(out, "hide_diff.jpg"), quality=90)
Image.open(os.path.join(src, "leather_white/nor_gl.jpg")).resize((N, N)).save(os.path.join(out, "hide_nor_gl.jpg"), quality=92)

# ---------- feather: barbs along the vane ----------
M = 512
y, x = np.mgrid[0:M, 0:M] / M
barb = 0.5 + 0.5 * np.sin((y * 90 + x * 25) * np.pi * 2 + tile_noise(M, 8, 3, 8) * 6)
band = tile_noise(M, 2, 4, 9, (1, 3))
col = np.array([120, 110, 96], float) * (1 - band[..., None] * 0.6) + np.array([235, 228, 214], float) * band[..., None] * 0.6
col *= (0.75 + 0.35 * barb)[..., None]
save("feather", col, normal_from_height(barb * 0.4, 2.0))
print("ok")

# ---------- flint: grey-brown chert with mottling and pale cortex patches ----------
dr = np.asarray(Image.open(os.path.join(src, "dark_rock/diff.jpg")).convert("RGB").resize((N, N)), float).mean(-1)
dr = (dr - dr.min()) / (dr.max() - dr.min() + 1e-6)
mott = tile_noise(N, 5, 6, 21)
cort = np.clip((tile_noise(N, 2, 5, 22) - 0.68) * 5, 0, 1)
dark = np.array([78, 70, 62], float); mid = np.array([132, 118, 98], float); pale = np.array([196, 184, 160], float)
t = np.clip(mott * 0.8 + dr * 0.4 - 0.2, 0, 1)[..., None]
rgb = dark * (1 - t) + mid * t
rgb = rgb * (1 - cort[..., None]) + pale * cort[..., None]
Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).save(os.path.join(out, "flint_diff.jpg"), quality=90)
Image.open(os.path.join(src, "dark_rock/nor_gl.jpg")).resize((N, N)).save(os.path.join(out, "flint_nor_gl.jpg"), quality=92)
print("flint")
