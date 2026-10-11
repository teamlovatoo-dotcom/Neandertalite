"""Tileable falling-water streak texture (RGBA) for waterfall curtains: Assets/Resources/Textures/waterfall_diff.png"""
import numpy as np, sys, os
from PIL import Image

def tile_noise(h, w, fy, fx, octaves, seed):
    r = np.random.default_rng(seed)
    acc = np.zeros((h, w)); amp = 1.0; tot = 0.0
    for o in range(octaves):
        gy, gx = fy * 2 ** o, fx * 2 ** o
        g = r.standard_normal((gy, gx))
        ys = np.arange(h) * gy / h; xs = np.arange(w) * gx / w
        y0 = np.floor(ys).astype(int); x0 = np.floor(xs).astype(int)
        ty = (ys - y0)[:, None]; tx = (xs - x0)[None, :]
        ty = ty * ty * (3 - 2 * ty); tx = tx * tx * (3 - 2 * tx)
        y1 = (y0 + 1) % gy; x1 = (x0 + 1) % gx
        a = g[y0][:, x0]; b = g[y0][:, x1]; c = g[y1][:, x0]; d = g[y1][:, x1]
        acc += amp * ((a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty)
        tot += amp; amp *= 0.55
    acc /= tot
    return (acc - acc.min()) / (acc.max() - acc.min() + 1e-9)

out = sys.argv[1] if len(sys.argv) > 1 else "Assets/Resources/Textures"
H, W = 512, 256
streak = tile_noise(H, W, 2, 20, 4, 1)
fine = tile_noise(H, W, 6, 64, 3, 2)
clump = tile_noise(H, W, 4, 6, 3, 3)
v = np.clip(streak * 0.6 + fine * 0.4, 0, 1)
white = np.clip((v - 0.55) * 3 + (clump - 0.6) * 1.5, 0, 1)
col = np.stack([0.62 + 0.38 * white, 0.74 + 0.26 * white, 0.8 + 0.2 * white], -1)
alpha = np.clip(0.45 + 0.55 * v + 0.3 * white, 0, 1)
img = np.concatenate([col, alpha[..., None]], -1)
Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(os.path.join(out, "waterfall_diff.png"))
print("ok")
