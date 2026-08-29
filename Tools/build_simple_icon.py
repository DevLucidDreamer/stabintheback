"""Build a clean, static-preparation game icon from the Gemini character reference."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "Image" / "Gemini_NanoBanana_Icon_Source.png"
OUTPUT = ROOT / "Assets" / "Sprite" / "GameIcon_Fallback.png"
SIZE = 1024

src = Image.open(SOURCE).convert("RGBA").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
canvas = Image.new("RGBA", (SIZE, SIZE), (35, 57, 72, 255))
d = ImageDraw.Draw(canvas, "RGBA")

# Medium-facet Frozen Tuna silhouette, held by the tail behind the shoulder.
edge = (31, 57, 83, 255)
back = (103, 140, 175, 255)
body = (166, 195, 218, 255)
belly = (222, 232, 241, 255)
fin = (214, 198, 126, 255)
fish = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
fd = ImageDraw.Draw(fish, "RGBA")
outline = [(100, 455), (148, 390), (270, 305), (445, 250), (620, 255),
           (735, 292), (795, 335), (750, 382), (630, 425), (475, 500),
           (285, 550), (150, 515)]
fd.polygon(outline, fill=body)
fd.polygon([(100,455),(148,390),(270,305),(445,250),(370,390),(150,515)], fill=back)
fd.polygon([(150,515),(370,390),(630,425),(475,500),(285,550)], fill=belly)
fd.polygon([(370,390),(445,250),(620,255),(630,425)], fill=(144,181,209,255))
fd.polygon([(620,255),(735,292),(795,335),(750,382),(630,425)], fill=(98,137,173,255))
fd.polygon([(742,298),(835,188),(805,330),(920,438),(790,392),(742,350)], fill=fin)
fd.polygon([(405,270),(470,148),(520,275)], fill=back)
fd.polygon([(430,492),(492,602),(555,480)], fill=fin)
fd.polygon([(540,355),(654,451),(573,407)], fill=back)
fd.polygon([(100,455),(52,425),(78,482),(120,500)], fill=fin)
fd.polygon([(245,330),(370,390),(270,460)], fill=(137,177,207,255))
fd.polygon([(370,390),(500,330),(630,425),(475,500)], fill=(164,195,219,255))
fd.polygon([(150,515),(270,460),(285,550)], fill=(211,226,238,255))
fd.polygon([(270,305),(370,390),(330,330),(445,250)], fill=(145,181,210,255))
fd.polygon([(500,330),(620,255),(630,425),(570,390)], fill=(133,171,202,255))
fd.polygon([(270,460),(370,390),(475,500),(285,550)], fill=(191,216,232,255))
fd.ellipse((156,430,180,454), fill=(26,27,31,255))
fd.line([(218,414),(198,470)], fill=(55,83,108,190), width=5)
fd.line([(230,420),(212,475)], fill=(75,103,129,150), width=3)
fd.line(outline + [outline[0]], fill=edge, width=7)
fd.line([(742,298),(835,188),(805,330),(920,438),(790,392),(742,350)], fill=edge, width=6)
# Turn the fish so its head clearly points toward the upper-left.
fish = fish.rotate(15, resample=Image.Resampling.BICUBIC, center=(500, 400))
canvas.alpha_composite(fish)

# Isolate only the green character, then lean it back slightly as if bracing to swing.
mask = Image.new("L", (SIZE, SIZE), 0)
md = ImageDraw.Draw(mask)
md.polygon([(425,430),(495,365),(650,330),(835,345),(960,420),(995,700),
            (1024,1024),(420,1024),(420,700)], fill=255)
sp = src.convert("RGB").load(); mp = mask.load()
for y in range(SIZE):
    for x in range(SIZE):
        if mp[x,y]:
            r,g,b = sp[x,y]
            if not (g > 40 and g > r * 1.12 and g > b * 1.08):
                mp[x,y] = 0
mask = mask.filter(ImageFilter.GaussianBlur(1.2))
char = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
char.paste(src, (0,0), mask)
char = char.rotate(7, resample=Image.Resampling.BICUBIC, center=(700,760))
# Enlarge the character for a stronger face-first icon silhouette.
char = char.resize((1126, 1126), Image.Resampling.LANCZOS)
enlarged = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
enlarged.alpha_composite(char, (-65, -65))
canvas.alpha_composite(enlarged)

d = ImageDraw.Draw(canvas, "RGBA")
# Clean isolated color flecks inherited from the reference cutout.
d.polygon([(930, 475), (962, 470), (960, 530), (930, 530)], fill=(35, 57, 72, 255))
d.polygon([(944, 600), (980, 590), (978, 665), (944, 665)], fill=(35, 57, 72, 255))
# Tail grip: one clear arm and hand, with no speed lines or blur.
arm = [(898, 565), (900, 500), (870, 420), (820, 350)]
d.line(arm, fill=(16,56,35,255), width=42, joint="curve")
d.line(arm, fill=(52,157,54,255), width=27, joint="curve")
d.polygon([(792,330),(815,305),(850,310),(872,335),(858,365),(825,370),(797,354)], fill=(16,56,35,255))
d.polygon([(800,332),(819,314),(844,318),(860,336),(849,355),(827,359),(805,349)], fill=(58,166,58,255))
# Pure black eyes only; cover the source eye rims so no white/gray iris remains.
d.ellipse((510, 514, 660, 680), fill=(8, 12, 16, 255))
d.ellipse((695, 530, 855, 708), fill=(8, 12, 16, 255))

# Rounded-square outline only; outside corners are transparent.
alpha = Image.new("L", (SIZE, SIZE), 0)
ImageDraw.Draw(alpha).rounded_rectangle((0,0,SIZE-1,SIZE-1), radius=142, fill=255)
canvas.putalpha(alpha)
d = ImageDraw.Draw(canvas, "RGBA")
canvas.save(OUTPUT, "PNG", optimize=True)
print(OUTPUT)
