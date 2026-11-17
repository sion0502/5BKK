"""기 13종 아이콘 생성 (뼈색 문양 + 붉은 포인트, 투명 배경). 1024로 그려 256으로 축소."""
import math
import sys
from pathlib import Path
from PIL import Image, ImageDraw

S = 1024
OUT = 256
BONE = (232, 224, 208, 255)
RED = (205, 42, 36, 255)
CLEAR = (0, 0, 0, 0)


def canvas():
    img = Image.new("RGBA", (S, S), CLEAR)
    return img, ImageDraw.Draw(img)


def poly(d, pts, color, w):
    d.line(pts, fill=color, width=w, joint="curve")
    r = w // 2
    for x, y in (pts[0], pts[-1]):
        d.ellipse((x - r, y - r, x + r, y + r), fill=color)


def rotated_layer(draw_fn, angle, center=(S // 2, S // 2)):
    layer, d = canvas()
    draw_fn(d)
    return layer.rotate(angle, resample=Image.BICUBIC, center=center)


def drop(d, cx, cy, r, color):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color)
    d.polygon([(cx, cy - r * 2.1), (cx - r * 0.93, cy - r * 0.35), (cx + r * 0.93, cy - r * 0.35)], fill=color)


def reckless_sprint():
    img, d = canvas()
    for i in range(3):
        x0 = 140 + i * 175
        poly(d, [(x0, 300), (x0 + 175, 512), (x0, 724)], BONE if i < 2 else RED, 74)
    drop(d, 840, 330, 72, RED)
    return img


def strongman():
    img, _ = canvas()

    def link(cx, cy):
        def fn(d):
            d.rounded_rectangle((cx - 190, cy - 105, cx + 190, cy + 105), radius=105, outline=BONE, width=58)
        return fn

    img.alpha_composite(rotated_layer(link(330, 650), 40, (330, 650)))
    img.alpha_composite(rotated_layer(link(694, 374), 40, (694, 374)))
    d = ImageDraw.Draw(img)
    # 끊어진 틈의 균열·불꽃
    for ang in (20, 65, 110, 155, 200, 245, 290, 335):
        a = math.radians(ang)
        cx, cy = 512, 512
        poly(d, [(cx + math.cos(a) * 60, cy + math.sin(a) * 60), (cx + math.cos(a) * 125, cy + math.sin(a) * 125)], RED, 26)
    return img


def starter_pack():
    img, d = canvas()
    d.rectangle((260, 470, 764, 840), outline=BONE, width=52)
    d.rectangle((220, 360, 804, 480), fill=BONE)
    d.rectangle((482, 360, 542, 840), fill=RED)
    d.ellipse((330, 210, 512, 360), outline=RED, width=48)
    d.ellipse((512, 210, 694, 360), outline=RED, width=48)
    d.ellipse((470, 300, 554, 384), fill=RED)
    return img


def born_strong():
    img, d = canvas()
    d.rectangle((290, 170, 734, 880), outline=BONE, width=52)
    d.rectangle((370, 250, 654, 470), outline=BONE, width=30)
    d.ellipse((610, 540, 670, 600), fill=BONE)
    poly(d, [(480, 170), (550, 320), (468, 450), (590, 600), (500, 730), (606, 880)], RED, 36)
    poly(d, [(468, 450), (380, 520)], RED, 24)
    poly(d, [(590, 600), (690, 650)], RED, 24)
    return img


def heavy_pack():
    img, d = canvas()
    d.arc((330, 170, 530, 370), 180, 360, fill=BONE, width=46)
    d.rounded_rectangle((200, 270, 660, 870), radius=110, outline=BONE, width=52)
    d.line((200, 470, 660, 470), fill=BONE, width=40)
    d.rounded_rectangle((290, 590, 570, 790), radius=40, outline=BONE, width=36)
    d.rectangle((790, 380, 870, 690), fill=RED)
    d.polygon([(720, 670), (940, 670), (830, 850)], fill=RED)
    return img


def running_man():
    img, d = canvas()
    d.rounded_rectangle((320, 230, 704, 860), radius=60, outline=BONE, width=50)
    d.line((350, 330, 674, 330), fill=BONE, width=34)
    d.rounded_rectangle((440, 150, 584, 220), radius=20, fill=BONE)
    d.polygon([(560, 380), (410, 600), (505, 600), (455, 790), (620, 540), (520, 540)], fill=RED)
    return img


def narrow_escape():
    img, d = canvas()
    pts = []
    for i in range(0, 361, 3):
        t = math.radians(i)
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((512 + x * 22, 470 - y * 22))
    d.line(pts + [pts[0]], fill=BONE, width=50, joint="curve")
    poly(d, [(110, 520), (320, 520), (390, 390), (465, 690), (545, 300), (615, 570), (665, 520), (914, 520)], RED, 40)
    return img


def adrenaline():
    def fn(d):
        d.rectangle((360, 430, 760, 594), outline=BONE, width=40)
        d.rectangle((388, 458, 600, 566), fill=RED)
        for x in (470, 560, 650):
            d.line((x, 430, x, 480), fill=BONE, width=18)
        d.line((760, 512, 880, 512), fill=BONE, width=40)
        d.line((880, 430, 880, 594), fill=BONE, width=46)
        d.line((330, 470, 330, 554), fill=BONE, width=40)
        d.line((330, 512, 150, 512), fill=BONE, width=20)

    img, _ = canvas()
    img.alpha_composite(rotated_layer(fn, 40))
    return img


def ordeal():
    img, d = canvas()
    cx, cy, r = 512, 470, 255
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=BONE, width=46)
    for k in range(12):
        a = math.radians(k * 30 + 15)
        out = 1 if k % 2 == 0 else -1
        base = r + out * 10
        tip = r + out * (120 if out > 0 else 70)
        ax, ay = math.cos(a), math.sin(a)
        px, py = -ay, ax
        bx, by = cx + ax * base, cy + ay * base
        d.polygon([(bx + px * 34, by + py * 34), (bx - px * 34, by - py * 34), (cx + ax * tip, cy + ay * tip)], fill=BONE)
    drop(d, 512, 900, 50, RED)
    drop(d, 600, 790, 30, RED)
    return img


def thorn_wings():
    img, d = canvas()
    spar = [(140, 520), (260, 360), (470, 250), (700, 200), (890, 170)]
    edge = [(890, 170), (820, 470), (720, 390), (660, 650), (560, 470), (480, 760), (400, 520), (310, 690), (250, 530)]
    d.polygon(spar + edge, fill=BONE)
    d.line(spar, fill=BONE, width=60, joint="curve")
    d.ellipse((90, 470, 200, 580), fill=BONE)
    # 날개 위 가장자리의 붉은 가시
    for (x0, y0), (x1, y1) in zip(spar[1:], spar[2:]):
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        dx, dy = x1 - x0, y1 - y0
        ln = (dx * dx + dy * dy) ** 0.5
        nx, ny = dy / ln, -dx / ln  # 위쪽 법선
        d.polygon([(mx - dx / ln * 40, my - dy / ln * 40), (mx + dx / ln * 40, my + dy / ln * 40),
                   (mx + nx * 130 + dx / ln * 30, my + ny * 130 + dy / ln * 30)], fill=RED)
    d.polygon([(860, 150), (920, 190), (990, 90)], fill=RED)
    return img


def k2c1():
    img, d = canvas()
    w = 130
    for i, x in enumerate((240, 447, 654)):
        d.ellipse((x, 250, x + w, 560), fill=RED if i == 1 else BONE)
        d.rectangle((x, 420, x + w, 850), fill=BONE)
        d.rectangle((x - 12, 800, x + w + 12, 850), fill=BONE)
        d.line((x, 470, x + w, 470), fill=CLEAR, width=14)
    return img


def map_icon():
    img, d = canvas()
    outline = [(170, 300), (395, 220), (629, 300), (854, 220), (854, 770), (629, 850), (395, 770), (170, 850), (170, 300)]
    d.line(outline, fill=BONE, width=44, joint="curve")
    d.line((395, 220, 395, 770), fill=BONE, width=30)
    d.line((629, 300, 629, 850), fill=BONE, width=30)
    for x in range(250, 520, 70):
        d.line((x, 690 - (x - 250) * 0.5, x + 34, 690 - (x + 34 - 250) * 0.5), fill=BONE, width=22)
    cx, cy, r = 600, 430, 96
    d.polygon([(cx - r * 0.92, cy + r * 0.38), (cx + r * 0.92, cy + r * 0.38), (cx, cy + r * 2.2)], fill=RED)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=RED)
    d.ellipse((cx - 36, cy - 36, cx + 36, cy + 36), fill=CLEAR)
    return img


def lockpick():
    img, d = canvas()
    d.arc((330, 160, 694, 560), 180, 360, fill=BONE, width=62)
    d.rectangle((330, 350, 392, 470), fill=BONE)
    d.rectangle((632, 350, 694, 470), fill=BONE)
    d.rounded_rectangle((250, 440, 774, 870), radius=50, fill=BONE)
    d.ellipse((462, 560, 562, 660), fill=CLEAR)
    d.polygon([(482, 630), (542, 630), (566, 770), (458, 770)], fill=CLEAR)
    poly(d, [(512, 640), (900, 470)], RED, 30)
    poly(d, [(512, 640), (470, 600)], RED, 30)
    d.ellipse((850, 400, 970, 520), outline=RED, width=30)
    return img


ICONS = {
    "RecklessSprint": reckless_sprint,
    "Strongman": strongman,
    "StarterPack": starter_pack,
    "BornStrong": born_strong,
    "HeavyPack": heavy_pack,
    "RunningMan": running_man,
    "NarrowEscape": narrow_escape,
    "Adrenaline": adrenaline,
    "Ordeal": ordeal,
    "ThornWings": thorn_wings,
    "K2C1": k2c1,
    "Map": map_icon,
    "Lockpick": lockpick,
}


def main(out_dir, preview_path):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    tiles = []
    for name, fn in ICONS.items():
        icon = fn().resize((OUT, OUT), Image.LANCZOS)
        icon.save(out / f"{name}.png")
        tiles.append(icon)

    cols = 5
    rows = math.ceil(len(tiles) / cols)
    sheet = Image.new("RGBA", (cols * (OUT + 16) + 16, rows * (OUT + 16) + 16), (26, 26, 28, 255))
    for i, tile in enumerate(tiles):
        x = 16 + (i % cols) * (OUT + 16)
        y = 16 + (i // cols) * (OUT + 16)
        sheet.alpha_composite(tile, (x, y))
    sheet.save(preview_path)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
