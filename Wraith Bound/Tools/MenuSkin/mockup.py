"""새 메뉴 스킨 목업 (일시정지 메뉴 + 설정 패널) — 같은 텍스처로 대략 합성."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

skin = Path(sys.argv[1])
font_path = sys.argv[2]
out = sys.argv[3]

W, H = 1920, 1080
TEXT = (214, 209, 199)
HOVER = (250, 163, 148)
ORN = (219, 214, 204)
RED = (199, 36, 31)


def tex(name, size=None, tint=(255, 255, 255), alpha=1.0, flip=False):
    img = Image.open(skin / f"{name}.png").convert("RGBA")
    if size:
        img = img.resize(size, Image.LANCZOS)
    if flip:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    r, g, b, a = img.split()
    a = a.point(lambda v: int(v * alpha))
    solid = Image.new("RGBA", img.size, tint + (255,))
    solid.putalpha(a)
    if tint == (255, 255, 255):
        img.putalpha(a)
        return img
    return solid


def nine(name, size, border, alpha):
    src = Image.open(skin / f"{name}.png").convert("RGBA")
    w, h = size
    s = src.width
    b = border
    dst = Image.new("RGBA", size, (0, 0, 0, 0))
    xs = [(0, b, 0, b), (b, s - b, b, w - b), (s - b, s, w - b, w)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in xs if True else []:
            pass
    rows = [(0, b, 0, b), (b, s - b, b, h - b), (s - b, s, h - b, h)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in rows:
            piece = src.crop((sx0, sy0, sx1, sy1)).resize((max(1, dx1 - dx0), max(1, dy1 - dy0)), Image.LANCZOS)
            dst.paste(piece, (dx0, dy0))
    r, g, bb, a = dst.split()
    dst.putalpha(a.point(lambda v: int(v * alpha)))
    return dst


def paste_center(base, img, cx, cy):
    base.alpha_composite(img, (int(cx - img.width / 2), int(cy - img.height / 2)))


def text(d, cx, cy, s, size, color, spacing=0):
    f = ImageFont.truetype(font_path, size)
    widths = [d.textlength(ch, font=f) for ch in s]
    total = sum(widths) + spacing * (len(s) - 1)
    x = cx - total / 2
    for ch, w in zip(s, widths):
        d.text((x, cy), ch, font=f, fill=color, anchor="lm")
        x += w + spacing


# 배경: 흐릿한 회녹색 장면 느낌
bg = Image.new("RGBA", (W, H), (58, 62, 58, 255))
d = ImageDraw.Draw(bg)
for i in range(18):
    d.rectangle((i * 120, 200 + (i % 3) * 60, i * 120 + 70, 900), fill=(48 + i % 4 * 6, 52, 49, 255))
bg = bg.filter(ImageFilter.GaussianBlur(24))
vig = Image.new("L", (W, H), 0)
ImageDraw.Draw(vig).ellipse((-300, -250, W + 300, H + 250), fill=255)
vig = vig.filter(ImageFilter.GaussianBlur(160))
dark = Image.new("RGBA", (W, H), (10, 10, 10, 255))
bg = Image.composite(bg, dark, vig)

# ── 왼쪽: 일시정지 메뉴 ──
cx = 560
paste_center(bg, nine("PanelBg", (780, 646), 80, 0.6), cx, 520)
paste_center(bg, tex("BrushBand", (464, 84), alpha=0.85), cx, 300)
d = ImageDraw.Draw(bg)
text(d, cx, 300, "일시정지", 34, TEXT, spacing=16)
paste_center(bg, tex("Divider", (580, 23), ORN, 0.6), cx, 336)
labels = ["계속하기", "다시하기", "메인메뉴로", "바탕화면으로"]
for i, lab in enumerate(labels):
    y = 410 + i * 82
    hover = i == 0
    if hover:
        paste_center(bg, tex("BrushBand", (504, 83), alpha=0.92), cx, y)
        paste_center(bg, tex("LineFade", (342, 3), RED, 0.85), cx, y - 32)
        paste_center(bg, tex("LineFade", (342, 3), RED, 0.85), cx, y + 32)
    pa = 1.0 if hover else 0.45
    paste_center(bg, tex("LinePin", (79, 14), ORN, 0.6 * pa), cx - 180 + 40, y)
    paste_center(bg, tex("LinePin", (79, 14), ORN, 0.6 * pa, flip=True), cx + 180 - 40, y)
    d = ImageDraw.Draw(bg)
    text(d, cx, y, lab, 33 if hover else 32, HOVER if hover else TEXT, spacing=6)
paste_center(bg, tex("Divider", (580, 23), ORN, 0.6), cx, 410 + 3 * 82 + 70)
d = ImageDraw.Draw(bg)
text(d, cx, 410 + 3 * 82 + 105, "ESC   계속하기", 20, (140, 135, 128), spacing=4)

# ── 오른쪽: 설정 패널 ──
px, py, pw, ph = 1390, 540, 760, 470
paste_center(bg, nine("PanelBg", (pw + 140, ph + 140), 80, 0.88), px, py)
paste_center(bg, nine("Frame", (pw, ph), 32, 0.6), px, py)
paste_center(bg, tex("BrushBand", (448, 84), alpha=0.85), px, py - 175)
d = ImageDraw.Draw(bg)
text(d, px, py - 175, "설정", 34, TEXT, spacing=16)
paste_center(bg, tex("Divider", (560, 22), ORN, 0.6), px, py - 141)
rows = [("전체 음량", 0.8, "80%"), ("마우스 감도", 0.3, "1.0x")]
f28 = ImageFont.truetype(font_path, 28)
for k, (name, v, val) in enumerate(rows):
    y = py - 90 + k * 80
    d.text((px - 330, y), name, font=f28, fill=TEXT, anchor="lm")
    x0, x1 = px - 100, px + 220
    d.rectangle((x0, y - 1, x1, y + 1), fill=ORN + (90,))
    d.rectangle((x0, y - 2, x0 + (x1 - x0) * v, y + 2), fill=RED + (255,))
    paste_center(bg, tex("Diamond", (26, 26), TEXT), x0 + (x1 - x0) * v, y)
    d = ImageDraw.Draw(bg)
    d.text((px + 345, y), val, font=ImageFont.truetype(font_path, 26), fill=TEXT, anchor="rm")
y = py + 70
d.text((px - 330, y), "전체 화면", font=f28, fill=TEXT, anchor="lm")
paste_center(bg, tex("LinePin", (40, 14), ORN, 0.27), px + 80, y)
paste_center(bg, tex("LinePin", (40, 14), ORN, 0.27, flip=True), px + 200, y)
d = ImageDraw.Draw(bg)
text(d, px + 140, y, "켜짐", 24, TEXT, spacing=6)
paste_center(bg, tex("LinePin", (49, 14), ORN, 0.27), px - 112 + 25, py + 175)
paste_center(bg, tex("LinePin", (49, 14), ORN, 0.27, flip=True), px + 112 - 25, py + 175)
d = ImageDraw.Draw(bg)
text(d, px, py + 175, "뒤로", 27, TEXT, spacing=6)

bg.convert("RGB").resize((1440, 810), Image.LANCZOS).save(out)
