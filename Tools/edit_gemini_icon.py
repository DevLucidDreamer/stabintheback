"""Build the final icon with the project's faceted Frozen_Tuna look."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "Image" / "Gemini_NanoBanana_Icon_Source.png"
# Keep the deterministic offline renderer as a fallback; the shipped icon is
# generated through Gemini Nano Banana and lives at GameIcon.png.
OUTPUT = ROOT / "Assets" / "Sprite" / "GameIcon_Fallback.png"
SIZE = 1024

src = Image.open(SOURCE).convert("RGBA").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
canvas = Image.new("RGBA", (SIZE, SIZE), (8, 18, 40, 255))
pix = canvas.load()
for y in range(SIZE):
    for x in range(SIZE):
        glow = max(0.0, 1.0 - math.hypot(x / SIZE - 0.48, y / SIZE - 0.64) * 1.35)
        pix[x, y] = (int(8 + 18 * glow), int(18 + 26 * glow), int(40 + 24 * glow), 255)
d = ImageDraw.Draw(canvas, "RGBA")
d.polygon([(0, 730), (150, 620), (300, 710), (450, 635), (610, 730),
           (760, 645), (900, 720), (1024, 660), (1024, 1024), (0, 1024)], fill=(18, 48, 51, 255))
for x, h in [(80, 240), (180, 300), (870, 270), (960, 225)]:
    d.polygon([(x, 760), (x - 78, 760), (x - 18, 760 - h),
               (x + 18, 760 - h), (x + 78, 760)], fill=(20, 57, 49, 255))
d.polygon([(350, 760), (395, 650), (440, 760)], fill=(225, 92, 28, 220))
d.polygon([(372, 760), (395, 687), (420, 760)], fill=(255, 185, 48, 235))

# Low-poly tuna using the Frozen_Tuna material palette.
back = (111, 148, 183, 255)
body = (174, 202, 224, 255)
belly = (222, 232, 241, 255)
fin = (219, 204, 133, 255)
edge = (31, 57, 83, 255)
fish = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
fd = ImageDraw.Draw(fish, "RGBA")
fish_body = [(105, 457), (145, 390), (270, 305), (445, 250), (620, 255),
             (735, 292), (795, 335), (750, 382), (630, 425), (475, 500),
             (285, 550), (150, 515)]
fd.polygon(fish_body, fill=body)
fd.polygon([(105, 457), (145, 390), (270, 305), (445, 250), (370, 390),
            (150, 515)], fill=back)
fd.polygon([(150, 515), (370, 390), (630, 425), (475, 500), (285, 550)], fill=belly)
fd.polygon([(370, 390), (445, 250), (620, 255), (630, 425)], fill=(151, 187, 214, 255))
fd.polygon([(620, 255), (735, 292), (795, 335), (750, 382), (630, 425)], fill=(105, 146, 183, 255))
fd.polygon([(742, 298), (835, 188), (805, 330), (920, 438), (790, 392), (742, 350)], fill=fin)
fd.polygon([(405, 270), (470, 148), (520, 275)], fill=back)
fd.polygon([(430, 492), (492, 602), (555, 480)], fill=fin)
fd.polygon([(540, 355), (654, 451), (573, 407)], fill=back)
fd.polygon([(105, 457), (52, 425), (78, 482), (120, 500)], fill=fin)
fd.polygon([(245, 330), (370, 390), (270, 460)], fill=(137, 177, 207, 255))
fd.polygon([(370, 390), (500, 330), (630, 425), (475, 500)], fill=(164, 195, 219, 255))
fd.polygon([(150, 515), (270, 460), (285, 550)], fill=(211, 226, 238, 255))
fd.polygon([(270, 305), (370, 390), (330, 330), (445, 250)], fill=(145, 181, 210, 255))
fd.polygon([(500, 330), (620, 255), (630, 425), (570, 390)], fill=(133, 171, 202, 255))
fd.polygon([(270, 460), (370, 390), (475, 500), (285, 550)], fill=(191, 216, 232, 255))
fd.polygon([(630, 425), (750, 382), (630, 505), (475, 500)], fill=(139, 177, 207, 255))
fd.ellipse((156, 430, 180, 454), fill=(26, 27, 31, 255))
fd.ellipse((162, 434, 169, 441), fill=(93, 111, 124, 180))
fd.line([(218, 414), (198, 470)], fill=(55, 83, 108, 190), width=5)
fd.line([(230, 420), (212, 475)], fill=(75, 103, 129, 150), width=3)
fd.line(fish_body + [fish_body[0]], fill=edge, width=7)
fd.line([(742, 298), (835, 188), (805, 330), (920, 438), (790, 392), (742, 350)], fill=edge, width=6)
# Motion arcs sit behind the tuna so the swing reads at thumbnail size.
d.arc((650, 120, 1080, 620), start=142, end=248, fill=(166, 225, 242, 165), width=6)
d.arc((720, 175, 1090, 570), start=145, end=235, fill=(205, 241, 247, 125), width=3)
canvas.alpha_composite(fish)

# Extract the green character from the Nano Banana source.
char_mask = Image.new("L", (SIZE, SIZE), 0)
cm = ImageDraw.Draw(char_mask)
cm.polygon([(425, 430), (495, 365), (650, 330), (835, 345), (960, 420),
            (995, 700), (1024, 1024), (420, 1024), (420, 700)], fill=255)
sp = src.convert("RGB").load()
mp = char_mask.load()
for y in range(SIZE):
    for x in range(SIZE):
        if mp[x, y]:
            r, g, b = sp[x, y]
            if not (g > 40 and g > r * 1.12 and g > b * 1.08):
                mp[x, y] = 0
char_mask = char_mask.filter(ImageFilter.GaussianBlur(1.2))
canvas.paste(src, (0, 0), char_mask)

# Restore clean black oval eyes where the source fish layer would show through.
d = ImageDraw.Draw(canvas, "RGBA")
d.ellipse((554, 528, 629, 642), fill=(20, 26, 33, 255))
d.ellipse((744, 557, 824, 670), fill=(20, 26, 33, 255))

# Tail-grip and motion: the hand wraps the tail while the tuna swings behind
# the shoulder, rather than appearing as a static horizontal prop.
arm = [(900, 560), (900, 500), (870, 420), (820, 350)]
d.line(arm, fill=(16, 56, 35, 255), width=42, joint="curve")
d.line(arm, fill=(52, 157, 54, 255), width=27, joint="curve")
d.polygon([(792, 330), (815, 305), (850, 310), (872, 335), (858, 365),
           (825, 370), (797, 354)], fill=(16, 56, 35, 255))
d.polygon([(800, 332), (819, 314), (844, 318), (860, 336), (849, 355),
           (827, 359), (805, 349)], fill=(58, 166, 58, 255))
d.polygon([(944, 470), (1000, 445), (975, 490)], fill=(201, 237, 247, 145))
d.polygon([(930, 520), (990, 502), (960, 545)], fill=(201, 237, 247, 120))

# Transparent corners plus a thin rounded-rectangle outline only.
alpha = Image.new("L", (SIZE, SIZE), 0)
ImageDraw.Draw(alpha).rounded_rectangle((0, 0, SIZE - 1, SIZE - 1), radius=142, fill=255)
canvas.putalpha(alpha)
d = ImageDraw.Draw(canvas, "RGBA")
d.rounded_rectangle((18, 18, SIZE - 19, SIZE - 19), radius=126,
                    outline=(57, 94, 123, 245), width=7)
canvas.save(OUTPUT, "PNG", optimize=True)
print(OUTPUT)
