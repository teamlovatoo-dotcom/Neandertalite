"""Looping fire flipbook (8x8 frames, premultiplied for additive blending) and a 2x2 smoke puff sheet."""
import numpy as np, sys, os
from PIL import Image
out = sys.argv[1]

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

S, G = 128, 8
F = G * G
P = S * 4                      # vertical period of the scrolling noise (pixels)
n1 = tile_noise(P, S, 8, 2, 5, 1)    # main turbulence (tileable vertically and horizontally)
n2 = tile_noise(P, S, 4, 1, 4, 2)    # sway
n3 = tile_noise(P, S, 16, 4, 3, 3)   # fine licks
ys = (np.arange(S)[::-1] + 0.5) / S  # v: 0 bottom .. 1 top (image row 0 is top)
xs = (np.arange(S) + 0.5) / S * 2 - 1
V, X = np.meshgrid(ys, xs, indexing="ij")
ramp_t = np.array([0.0, 0.18, 0.4, 0.62, 0.82, 1.0])
ramp_c = np.array([[0, 0, 0], [0.5, 0.06, 0.01], [0.95, 0.28, 0.03], [1.0, 0.52, 0.1], [1.0, 0.76, 0.32], [1.0, 0.92, 0.7]])
sheet = np.zeros((S * G, S * G, 4))
rows = np.arange(S)
for f in range(F):
    shift = int(round(f * P / F))           # one full period per loop -> seamless
    r1 = (rows + shift * 1) % P
    r2 = (rows + shift // 2 * 2) % P
    r3 = (rows + shift * 2) % P
    N1 = n1[r1][:, :]; N2 = n2[r2]; N3 = n3[r3]
    sway = (N2 - 0.5) * 0.7 * V ** 1.1 + (N3 - 0.5) * 0.25 * V
    xx = X + sway
    width = 0.5 * (1 - V) ** 0.9 * (0.75 + 0.6 * np.sqrt(V)) + 0.01
    shape = np.clip(1 - np.abs(xx) / width, 0, 1) ** 0.7
    base = np.clip(V / 0.1, 0, 1) ** 0.7       # soft bottom edge
    turb = 0.65 * N1 + 0.35 * N3
    I = shape * (0.8 + 0.5 * (1 - V)) - (1 - turb) ** 1.3 * (0.25 + 1.6 * V) - 0.04
    I = np.clip(I, 0, 1) * base
    I = np.clip(I * 1.15, 0, 1) ** 1.1
    col = np.stack([np.interp(I, ramp_t, ramp_c[:, k]) for k in range(3)], -1)
    a = np.clip(I * 2.0, 0, 1)
    rgba = np.concatenate([col * a[..., None], a[..., None]], -1)  # premultiplied
    gy, gx = f // G, f % G
    sheet[gy * S:(gy + 1) * S, gx * S:(gx + 1) * S] = rgba
Image.fromarray((np.clip(sheet, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(out, "fx_fire.png"))

# smoke: 2x2 puffs
T = 256
sm = np.zeros((T * 2, T * 2, 4))
yy, xx = np.mgrid[0:T, 0:T]
d = np.sqrt(((xx + 0.5) / T * 2 - 1) ** 2 + ((yy + 0.5) / T * 2 - 1) ** 2)
for k in range(4):
    n = tile_noise(T, T, 3, 3, 6, 10 + k)
    m = np.clip(1 - d * (1.0 + 0.6 * (n - 0.5)), 0, 1) ** 1.5
    a = np.clip(m * (0.35 + 0.9 * n) - 0.08, 0, 1)
    shade = 0.75 + 0.25 * n
    rgba = np.stack([shade, shade, shade, a], -1)
    sm[(k // 2) * T:(k // 2 + 1) * T, (k % 2) * T:(k % 2 + 1) * T] = rgba
Image.fromarray((np.clip(sm, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(out, "fx_smoke.png"))
print("ok")
