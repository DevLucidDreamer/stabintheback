"""Create the shipping icon by cropping the original Gemini reference image."""
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "Image" / "Gemini_Generated_Image_.png"
OUTPUT = ROOT / "Assets" / "Sprite" / "GameIcon_ReferenceCrop.png"
DOCS_COPY = ROOT / "docs" / "Image" / "Icon_ReferenceCrop_Final.png"
SIZE = 1024

# Preserve the original green character and the frozen-tuna swing at icon scale.
# The box tightly frames both subjects without redrawing or restyling the scene.
CROP_BOX = (200, 470, 700, 970)

source = Image.open(SOURCE).convert("RGBA")
cropped = source.crop(CROP_BOX).resize((SIZE, SIZE), Image.Resampling.LANCZOS)

# Rounded-square silhouette only: transparent corners and no visible border line.
alpha = Image.new("L", (SIZE, SIZE), 0)
ImageDraw.Draw(alpha).rounded_rectangle(
    (0, 0, SIZE - 1, SIZE - 1), radius=132, fill=255
)
cropped.putalpha(alpha)

cropped.save(OUTPUT, "PNG", optimize=True)
cropped.save(DOCS_COPY, "PNG", optimize=True)
print(OUTPUT)
