"""Çalıştırma: python tools/make_brand.py  (Pillow gerekir; yazı tipi Windows Segoe UI, yoksa Calibri/Arial)
EgePharmTech marka işareti: yuvarlatılmış teal kare, beyaz salım eğrisi (kübik Bezier) ve üzerinde üç veri
noktası. Üretilenler (EgePharmTech/wwwroot/img/egepharmtech): favicon.svg, icon-32/192/512.png, apple-touch-icon.png
(180), og-image-tr.png, og-image-en.png; ayrıca NavMenu için tek renkli SVG parçası (stdout)."""
import os, io
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "EgePharmTech", "wwwroot", "img", "egepharmtech")
os.makedirs(OUT, exist_ok=True)
TEAL = (13, 148, 136)      # #0d9488
TEAL_DARK = (15, 118, 110) # #0f766e
DARK = (30, 41, 59)        # #1e293b
GRAY = (100, 116, 139)     # #64748b
WHITE = (255, 255, 255)
BRAND = "EgePharmTech"

# --- Geometri (64 birimlik kutu) ---
P0, P1, P2, P3 = (12, 50), (27, 50), (24, 14), (54, 14)
def bez(t):
    u = 1 - t
    x = u**3*P0[0] + 3*u*u*t*P1[0] + 3*u*t*t*P2[0] + t**3*P3[0]
    y = u**3*P0[1] + 3*u*u*t*P1[1] + 3*u*t*t*P2[1] + t**3*P3[1]
    return x, y
DOT_T = (0.22, 0.5, 0.82)
DOTS = [bez(t) for t in DOT_T]

def draw_mark(size, bg=True, radius_ratio=0.22, color=TEAL, fg=WHITE):
    """size px kare; 4x süper örnekleme ile çizip küçültür."""
    S = 4
    W = size * S
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    k = W / 64.0
    if bg:
        d.rounded_rectangle([0, 0, W - 1, W - 1], radius=int(W * radius_ratio), fill=color + (255,))
    # kalın çizgi: PIL'in width'li polyline'ı eğride boşluk bırakıyor; eğri boyunca yoğun daire damgası
    r = 5.2 * k / 2
    for i in range(1201):
        x, y = bez(i / 1200); x *= k; y *= k
        d.ellipse([x - r, y - r, x + r, y + r], fill=fg + (255,))
    # veri noktaları: beyaz halka + teal göbek
    for (x, y) in DOTS:
        x *= k; y *= k
        r1 = 5.4 * k; r2 = 2.6 * k
        d.ellipse([x - r1, y - r1, x + r1, y + r1], fill=fg + (255,))
        d.ellipse([x - r2, y - r2, x + r2, y + r2], fill=(color if bg else fg) + (255,))
    return img.resize((size, size), Image.LANCZOS)

for s, name in [(32, "icon-32.png"), (192, "icon-192.png"), (512, "icon-512.png"), (180, "apple-touch-icon.png")]:
    draw_mark(s).save(os.path.join(OUT, name), optimize=True)
    print("yazıldı", name)

# --- SVG favicon (aynı geometri) ---
def f(v): return f"{v:.1f}".rstrip("0").rstrip(".")
path = f"M{f(P0[0])} {f(P0[1])} C {f(P1[0])} {f(P1[1])}, {f(P2[0])} {f(P2[1])}, {f(P3[0])} {f(P3[1])}"
dots_svg = "\n".join(
    f'  <circle cx="{f(x)}" cy="{f(y)}" r="5.4" fill="#ffffff"/>\n  <circle cx="{f(x)}" cy="{f(y)}" r="2.6" fill="#0d9488"/>'
    for x, y in DOTS)
svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-label="{BRAND}">
  <rect width="64" height="64" rx="14" fill="#0d9488"/>
  <path d="{path}" fill="none" stroke="#ffffff" stroke-width="5.2" stroke-linecap="round"/>
{dots_svg}
</svg>
'''
io.open(os.path.join(OUT, "favicon.svg"), "w", encoding="utf-8", newline="\n").write(svg)
print("yazıldı favicon.svg")

nav = f'''<svg class="brand-mark brand-mark-pharmtech" viewBox="0 0 64 64" aria-hidden="true" focusable="false">
    <rect width="64" height="64" rx="14" fill="currentColor" />
    <path d="{path}" fill="none" class="brand-mark-stroke" stroke-width="5.2" stroke-linecap="round" />
''' + "\n".join(
    f'    <circle cx="{f(x)}" cy="{f(y)}" r="5.4" class="brand-mark-inner-fill" />\n    <circle cx="{f(x)}" cy="{f(y)}" r="2.6" fill="currentColor" />'
    for x, y in DOTS) + "\n</svg>"
print("\n--- NAV SVG ---\n" + nav + "\n--- /NAV SVG ---")

# --- OG görselleri (1200x630) ---
def font(names, size):
    for n in names:
        p = os.path.join(r"C:\Windows\Fonts", n)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()
BOLD = ["segoeuib.ttf", "SegUIVar.ttf", "calibrib.ttf", "arialbd.ttf"]
REG = ["segoeui.ttf", "SegUIVar.ttf", "calibri.ttf", "arial.ttf"]

def og(subtitle, tagline, domain, name):
    W, H = 1200, 630
    img = Image.new("RGB", (W, H), WHITE)
    mark = draw_mark(260)
    img.paste(mark, (120, (H - 260) // 2), mark)
    d = ImageDraw.Draw(img)
    x0 = 430
    fb = font(BOLD, 96); fs = font(BOLD, 32); ft = font(REG, 28); fd = font(REG, 26)
    d.text((x0 - 4, 178), BRAND, font=fb, fill=DARK)
    d.text((x0, 312), subtitle, font=fs, fill=TEAL)
    words = tagline.split(); lines = []; cur = ""
    for w in words:
        t = (cur + " " + w).strip()
        if d.textlength(t, font=ft) > W - x0 - 60: lines.append(cur); cur = w
        else: cur = t
    lines.append(cur)
    y = 372
    for ln in lines:
        d.text((x0, y), ln, font=ft, fill=GRAY); y += 38
    d.text((x0, 500), domain, font=fd, fill=TEAL_DARK)
    img.save(os.path.join(OUT, name), optimize=True)
    print("yazıldı", name, "font:", getattr(fb, "path", "?"))

og("Farmasötik teknoloji hesaplama araçları",
   "Dissolüsyon kinetiği · f1/f2 · psödo-üçlü faz diyagramı · LD50 · t-testi",
   "egepharmtech.tr", "og-image-tr.png")
og("Pharmaceutical technology calculation tools",
   "Dissolution kinetics · f1/f2 · pseudo-ternary phase diagram · LD50 · t-test",
   "egepharmtech.com", "og-image-en.png")
