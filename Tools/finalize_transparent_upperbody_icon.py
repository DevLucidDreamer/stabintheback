"""Create a tighter upper-body icon with a genuine transparent background."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "Image" / "Icon_UpperBody_Navy_Final.png"
OUTPUT = ROOT / "Assets" / "Sprite" / "GameIcon_Transparent_Fallback.png"
DOCS_COPY = ROOT / "docs" / "Image" / "Icon_UpperBody_Transparent_Fallback.png"
SIZE = 1024
NAVY = np.array([0x17, 0x2A, 0x46], dtype=np.int16)
CROP_BOX = (20, 0, 920, 900)

source = Image.open(SOURCE).convert("RGBA")
rgba = np.asarray(source).copy()
rgb = rgba[:, :, :3].astype(np.int16)
old_alpha = rgba[:, :, 3].astype(np.float32)

# Convert the exact flat navy backdrop into real alpha. The short transition
# removes dark-blue edge halos while retaining antialiasing around the subject.
delta = rgb.astype(np.float32) - NAVY.astype(np.float32)
distance = np.sqrt(np.sum(delta * delta, axis=2))
extracted_alpha = np.clip((distance - 5.0) / 28.0, 0.0, 1.0) * 255.0
new_alpha = np.minimum(old_alpha, extracted_alpha)

# Preserve all tuna facets, including dark blue-gray faces close to the navy
# key color, by restoring opacity through a tight color-derived fish mask.
brightness = rgb.max(axis=2)
r = rgb[:, :, 0]
g = rgb[:, :, 1]
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
fish_opacity = np.asarray(fish_mask, dtype=np.float32)
new_alpha = np.maximum(new_alpha, fish_opacity)

# The generator baked a checkerboard preview into the extreme top corners.
# Remove those neutral gray squares (and their thin antialiased rim) from alpha.
channel_spread = rgb.max(axis=2) - rgb.min(axis=2)
row_index = np.arange(SIZE)[:, None]
fake_checker = (
    (row_index < 150)
    & (rgb.min(axis=2) > 155)
    & (channel_spread < 38)
)
fake_mask = Image.fromarray((fake_checker * 255).astype(np.uint8), "L")
fake_mask = fake_mask.filter(ImageFilter.MaxFilter(9))
new_alpha[np.asarray(fake_mask) > 0] = 0

rgba[:, :, 3] = np.clip(new_alpha, 0, 255).astype(np.uint8)
cutout = Image.fromarray(rgba, "RGBA")

# A tighter square crop shows the head and upper torso while keeping the tuna.
cutout = cutout.crop(CROP_BOX).resize((SIZE, SIZE), Image.Resampling.LANCZOS)
cutout.save(OUTPUT, "PNG", optimize=True)
cutout.save(DOCS_COPY, "PNG", optimize=True)
print(OUTPUT)
