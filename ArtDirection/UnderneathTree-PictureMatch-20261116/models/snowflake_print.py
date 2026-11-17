"""Draws the gold snowflake wrapping print: gold flakes on plain grey paper, tileable.
Usage: python snowflake_print.py "<output png>"
The gift paper shader tints the grey with each gift's colour and treats the gold as foil."""
import math, random, sys
from PIL import Image, ImageDraw, ImageFilter

SIZE, SS = 2048, 2          # final size, and how much bigger it is drawn before smoothing
W = SIZE * SS
random.seed(1225)
mask = Image.new("L", (W, W), 0)
draw = ImageDraw.Draw(mask)

def line(x0, y0, x1, y1, width):
    # Draw nine copies so anything crossing an edge comes back in on the other side.
    for ox in (-W, 0, W):
        for oy in (-W, 0, W):
            draw.line((x0 + ox, y0 + oy, x1 + ox, y1 + oy), fill=255, width=int(width))
            r = width / 2
            for x, y in ((x0, y0), (x1, y1)):
                draw.ellipse((x + ox - r, y + oy - r, x + ox + r, y + oy + r), fill=255)

def flake(cx, cy, radius, style, turn):
    thick = max(3 * SS, radius * 0.075)
    for arm in range(6):
        a = turn + arm * math.pi / 3
        dx, dy = math.cos(a), math.sin(a)
        line(cx, cy, cx + dx * radius, cy + dy * radius, thick)
        # Side twigs, longest near the middle of the arm.
        steps = (0.34, 0.56, 0.76) if style != 1 else (0.42, 0.68)
        for i, at in enumerate(steps):
            reach = radius * (0.30 - 0.07 * i) * (1.15 if style == 2 else 1.0)
            px, py = cx + dx * radius * at, cy + dy * radius * at
            for side in (-1, 1):
                b = a + side * math.pi / 3
                line(px, py, px + math.cos(b) * reach, py + math.sin(b) * reach, thick * 0.8)
        if style == 0:      # a small diamond on each tip
            tx, ty = cx + dx * radius, cy + dy * radius
            for side in (-1, 1):
                b = a + side * 2.4
                line(tx, ty, tx + math.cos(b) * radius * 0.10, ty + math.sin(b) * radius * 0.10, thick * 0.8)
    # A six-sided ring round the middle.
    ring = radius * (0.20 if style != 1 else 0.28)
    for arm in range(6):
        a0 = turn + arm * math.pi / 3
        a1 = a0 + math.pi / 3
        line(cx + math.cos(a0) * ring, cy + math.sin(a0) * ring,
             cx + math.cos(a1) * ring, cy + math.sin(a1) * ring, thick * 0.75)

def star(cx, cy, radius):
    for i in range(4):
        a = i * math.pi / 2
        line(cx, cy, cx + math.cos(a) * radius, cy + math.sin(a) * radius, max(2 * SS, radius * 0.16))
    for i in range(4):
        a = math.pi / 4 + i * math.pi / 2
        line(cx, cy, cx + math.cos(a) * radius * 0.4, cy + math.sin(a) * radius * 0.4, max(2 * SS, radius * 0.12))

GRID = 5
cell = W / GRID
for gx in range(GRID):
    for gy in range(GRID):
        # Big flakes on a loose grid, every other row shifted half a step.
        cx = (gx + 0.5 + (0.5 if gy % 2 else 0.0) + random.uniform(-0.08, 0.08)) * cell
        cy = (gy + 0.5 + random.uniform(-0.08, 0.08)) * cell
        flake(cx % W, cy % W, cell * random.uniform(0.27, 0.33), random.randrange(3), random.uniform(0, math.pi / 3))
        # A small flake and a few stars in the gaps.
        flake((cx + cell * 0.5) % W, (cy + cell * 0.5 + random.uniform(-0.05, 0.05) * cell) % W,
              cell * random.uniform(0.10, 0.13), 1, random.uniform(0, 1))
        for _ in range(3):
            star(random.uniform(0, W), random.uniform(0, W), cell * random.uniform(0.035, 0.06))
        for _ in range(4):
            x, y, r = random.uniform(0, W), random.uniform(0, W), cell * random.uniform(0.008, 0.014)
            draw.ellipse((x - r, y - r, x + r, y + r), fill=255)

mask = mask.resize((SIZE, SIZE), Image.LANCZOS)
# Paper: plain grey with a faint fibre mottle. Foil: gold, a little lighter in the middle of each stroke.
noise = Image.effect_noise((SIZE, SIZE), 18).filter(ImageFilter.GaussianBlur(1.2))
paper = Image.merge("RGB", [noise.point(lambda v: 176 + (v - 128) * 0.25)] * 3)
core = mask.filter(ImageFilter.GaussianBlur(2.5))
gold = Image.merge("RGB", [core.point(lambda v: 235 + v * 20 // 255),
                           core.point(lambda v: 150 + v * 45 // 255),
                           core.point(lambda v: 28 + v * 30 // 255)])
Image.composite(gold, paper, mask).save(sys.argv[1])
