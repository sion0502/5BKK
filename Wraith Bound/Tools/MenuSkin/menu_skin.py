"""메뉴 UI 질감 텍스처 생성 (붓질 띠·흐려지는 선·핀 장식·구분선·거친 테두리·연기 패널·마름모).
사용: python menu_skin.py <출력 폴더> [미리보기 png]
출력 폴더 = Assets/Resources/UI/MenuSkin  (MenuSkin.cs가 Resources.Load로 읽음)
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

RNG = np.random.default_rng(7)


def smooth1d(values, k):
    kernel = np.ones(k) / k
    return np.convolve(np.pad(values, k, mode="edge"), kernel, mode="same")[k:-k]


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def rgba(color, alpha):
    h, w = alpha.shape
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[..., 0], out[..., 1], out[..., 2] = color
    out[..., 3] = np.clip(alpha * 255, 0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def brush_band(w=1024, h=160):
    """마우스를 올린 버튼 뒤에 깔리는 거친 붓질 띠 (어두운 색)."""
    y = np.arange(h)[:, None]
    x = np.arange(w)[None, :]
    # 줄마다 시작·끝 위치가 들쭉날쭉 → 마른 붓 끝
    left = (0.04 + smooth1d(RNG.random(h), 9) * 0.16)[:, None] * w
    right = (0.96 - smooth1d(RNG.random(h), 9) * 0.16)[:, None] * w
    ramp = 0.14 * w
    hx = smoothstep(left, left + ramp, x) * (1 - smoothstep(right - ramp, right, x))
    vy = np.clip(1 - np.abs((y - h / 2) / (h / 2)) ** 2.2, 0, 1)
    streak = (0.7 + 0.3 * smooth1d(RNG.random(h), 3))[:, None]
    grain = 0.82 + 0.18 * RNG.random((h, w))
    alpha = hx * vy * streak * grain * 0.95
    return rgba((14, 10, 10), alpha).filter(ImageFilter.GaussianBlur(0.6))


def line_fade(w=512, h=6):
    """가운데가 진하고 양끝이 사라지는 얇은 선 (흰색, 코드에서 색을 입힘)."""
    x = np.linspace(-1, 1, w)[None, :]
    profile = np.array([0.0, 0.35, 1.0, 1.0, 0.35, 0.0])[:, None]
    alpha = (1 - np.abs(x) ** 1.8) * profile
    return rgba((255, 255, 255), alpha)


def line_pin(w=256, h=32):
    """버튼 글자 옆 장식: 왼쪽에서 흐려지며 들어와 오른쪽 끝에 작은 핀 머리 (오른쪽 것은 코드에서 좌우 반전)."""
    s = 4
    img = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cy = h * s // 2
    end = int(w * s * 0.82)
    for xi in range(0, end, s):
        a = int(255 * (xi / end) ** 1.4)
        d.rectangle((xi, cy - s, xi + s, cy + s), fill=(255, 255, 255, a))
    d.rectangle((end, cy - 6 * s, end + 2 * s, cy + 6 * s), fill=(255, 255, 255, 255))
    r = 5 * s
    knob = end + 12 * s
    d.ellipse((knob - r, cy - r, knob + r, cy + r), fill=(255, 255, 255, 255))
    d.rectangle((knob, cy - s // 2, int(w * s * 0.98), cy + s // 2), fill=(255, 255, 255, 200))
    return img.resize((w, h), Image.LANCZOS)


def divider(w=1024, h=40):
    """제목 아래 장식 구분선: 양끝이 흐린 선 + 가운데 마름모 + 덩굴 장식 + 점."""
    s = 4
    img = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cy = h * s // 2
    W = w * s
    for xi in range(0, W, s):
        t = abs(xi / W * 2 - 1)
        a = int(255 * max(0.0, 1 - t ** 2.2) * 0.9)
        d.rectangle((xi, cy - s // 2 - 1, xi + s, cy + s // 2 + 1), fill=(255, 255, 255, a))
    c = W // 2
    r = 9 * s
    d.polygon([(c, cy - r), (c + r, cy), (c, cy + r), (c - r, cy)], fill=(255, 255, 255, 255))
    for side in (-1, 1):
        x0 = c + side * 22 * s
        box = (min(x0, x0 + side * 40 * s), cy - 12 * s, max(x0, x0 + side * 40 * s), cy + 2 * s)
        d.arc(box, 180, 360, fill=(255, 255, 255, 230), width=2 * s)
        px = int(W * (0.5 + side * 0.22))
        d.ellipse((px - 3 * s, cy - 3 * s, px + 3 * s, cy + 3 * s), fill=(255, 255, 255, 170))
    return img.resize((w, h), Image.LANCZOS)


def frame(size=256, inset=12):
    """패널 테두리 (9-slice, 테두리 32px): 손으로 그은 듯 흔들리는 얇은 선, 모서리에서 선이 교차해 삐져나옴."""
    s = 4
    S = size * s
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    i0, i1 = inset * s, S - inset * s
    over = 9 * s

    def rough(p0, p1):
        n = 40
        pts = []
        for k in range(n + 1):
            t = k / n
            x = p0[0] + (p1[0] - p0[0]) * t
            y = p0[1] + (p1[1] - p0[1]) * t
            j = RNG.normal(0, 0.35 * s)
            if p0[0] == p1[0]:
                x += j
            else:
                y += j
            pts.append((x, y))
        for a, b in zip(pts, pts[1:]):
            if RNG.random() < 0.015:
                continue  # 군데군데 끊김
            alpha = int(150 + RNG.random() * 105)
            d.line((a, b), fill=(255, 255, 255, alpha), width=int(1.6 * s))

    rough((i0 - over, i0), (i1 + over, i0))
    rough((i0 - over, i1), (i1 + over, i1))
    rough((i0, i0 - over), (i0, i1 + over))
    rough((i1, i0 - over), (i1, i1 + over))
    # 모서리 근처 긁힌 자국
    for cx, cy in ((i0, i0), (i1, i0), (i0, i1), (i1, i1)):
        for _ in range(10):
            px = cx + RNG.normal(0, 7 * s)
            py = cy + RNG.normal(0, 7 * s)
            r = RNG.random() * 1.6 * s + 0.5 * s
            d.ellipse((px - r, py - r, px + r, py + r), fill=(255, 255, 255, int(80 + RNG.random() * 140)))
    return img.resize((size, size), Image.LANCZOS)


def panel_bg(size=256, edge=80):
    """연기처럼 가장자리가 흐려지는 어두운 패널 바탕 (9-slice, 테두리 edge px)."""
    yy, xx = np.mgrid[0:size, 0:size]
    dist = np.minimum.reduce([xx, yy, size - 1 - xx, size - 1 - yy]).astype(float)
    noise = np.array(Image.fromarray((RNG.random((size, size)) * 255).astype(np.uint8))
                     .filter(ImageFilter.GaussianBlur(7))).astype(float)
    noise = (noise - noise.min()) / max(1.0, noise.max() - noise.min())
    alpha = smoothstep(0, edge, dist + (noise - 0.5) * 40) * (0.9 + 0.1 * RNG.random((size, size))) * 0.82
    return rgba((8, 8, 10), alpha)


def diamond(size=64):
    s = 4
    S = size * s
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c, r = S // 2, S // 2 - 6 * s
    d.polygon([(c, c - r), (c + r, c), (c, c + r), (c - r, c)], fill=(255, 255, 255, 255))
    return img.resize((size, size), Image.LANCZOS)


TEXTURES = {
    "BrushBand": brush_band,
    "LineFade": line_fade,
    "LinePin": line_pin,
    "Divider": divider,
    "Frame": frame,
    "PanelBg": panel_bg,
    "Diamond": diamond,
}


def main(out_dir, preview=None):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    images = {}
    for name, fn in TEXTURES.items():
        img = fn()
        img.save(out / f"{name}.png")
        images[name] = img

    if preview:
        sheet = Image.new("RGBA", (1100, 760), (70, 72, 70, 255))
        y = 20
        for name in ("BrushBand", "LineFade", "LinePin", "Divider"):
            img = images[name]
            sheet.alpha_composite(img, (20, y))
            y += img.height + 30
        sheet.alpha_composite(images["Frame"], (20, y))
        sheet.alpha_composite(images["PanelBg"], (300, y))
        sheet.alpha_composite(images["Diamond"], (580, y))
        sheet.save(preview)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
