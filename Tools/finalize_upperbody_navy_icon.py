"""Finalize the generated upper-body icon with an exact flat navy backdrop."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "Image" / "Icon_UpperBody_Generated.png"
OUTPUT = ROOT / "Assets" / "Sprite" / "GameIcon.png"
DOCS_COPY = ROOT / "docs" / "Image" / "Icon_UpperBody_Navy_Final.png"
SIZE = 1024
NAVY = np.array([0x17, 0x2A, 0x46], dtype=np.uint8)
CROP_BOX = (20, 0, 920, 900)

image = Image.open(SOURCE).convert("RGB").resize(
    (SIZE, SIZE), Image.Resampling.LANCZOS
)
original = np.asarray(image).copy()
pixels = original.copy()
r = pixels[:, :, 0].astype(np.int16)
g = pixels[:, :, 1].astype(np.int16)
b = pixels[:, :, 2].astype(np.int16)

# The generated backdrop is exclusively dark blue. This hue/value mask avoids
# the green character, black eyes, gray tuna facets, and red tuna markings.
background = (
    (g < 118)
    & (b < 160)
    & (b > r + 18)
    & (b > g + 7)
)

pixels[background] = NAVY

# Remove the baked checkerboard preview from the generated top corners. These
# pixels are part of the backdrop and must become navy before the final crop.
channel_spread = original.max(axis=2) - original.min(axis=2)
row_index = np.arange(SIZE)[:, None]
fake_checker = (
    (row_index < 150)
    & (original.min(axis=2) > 155)
    & (channel_spread < 38)
)
fake_mask = Image.fromarray((fake_checker * 255).astype(np.uint8), "L")
fake_mask = fake_mask.filter(ImageFilter.MaxFilter(9))
pixels[np.asarray(fake_mask) > 0] = NAVY

# Restore the tuna from the untouched source with a color-derived silhouette.
# This keeps blue-gray facets that share the navy hue without preserving a
# polygon-shaped patch of the original gradient around the fish.
brightness = original.max(axis=2)
fish_roi = np.zeros((SIZE, SIZE), dtype=bool)
fish_roi[135:700, 30:540] = True
fish_seed = fish_roi & (
    (brightness > 102)
    | ((r > 72) & (r > g + 14))
)
fish_mask = Image.fromarray((fish_seed * 255).astype(np.uint8), "L")
fish_mask = fish_mask.filter(ImageFilter.MaxFilter(9))
fish_mask = fish_mask.filter(ImageFilter.MinFilter(7))
fish_mask = fish_mask.filter(ImageFilter.GaussianBlur(0.8))
mix = np.asarray(fish_mask, dtype=np.float32)[:, :, None] / 255.0
pixels = np.clip(pixels * (1.0 - mix) + original * mix, 0, 255).astype(np.uint8)

result = Image.fromarray(pixels, "RGB")
result = result.crop(CROP_BOX).resize((SIZE, SIZE), Image.Resampling.LANCZOS)
result = result.convert("RGBA")
alpha = Image.new("L", (SIZE, SIZE), 0)
ImageDraw.Draw(alpha).rounded_rectangle(
    (0, 0, SIZE - 1, SIZE - 1), radius=132, fill=255
)
result.putalpha(alpha)

result.save(OUTPUT, "PNG", optimize=True)
result.save(DOCS_COPY, "PNG", optimize=True)
print(OUTPUT)
