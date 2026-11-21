"""Bake a seamless crystal relief field. Requires numpy and Pillow; never runs in Unity."""
from pathlib import Path
import numpy as np
from PIL import Image

size = 2048
rng = np.random.default_rng(41371)
height = np.zeros((size, size), np.float32)
shade = np.zeros_like(height)
# A 64 mm tile: mostly 0.4-1.2 mm grains, with irregular chipped facets.
for _ in range(6400):
    x, y = rng.uniform(0, size, 2)
    radius = rng.uniform(7, 22)
    extent = int(radius * 1.6) + 2
    xx = np.arange(int(x)-extent, int(x)+extent+1)
    yy = np.arange(int(y)-extent, int(y)+extent+1)
    dx, dy = np.meshgrid((xx-x)/radius, (yy-y)/radius)
    angle = rng.uniform(0, np.pi*2)
    u = dx*np.cos(angle) - dy*np.sin(angle)
    v = (dx*np.sin(angle) + dy*np.cos(angle)) * rng.uniform(.7, 1.3)
    distance = np.maximum.reduce([abs(u), abs(v), (abs(u)+abs(v))*.68])
    grain = np.maximum(0, np.minimum(1-distance, .52+u*.15-v*.08)) * rng.uniform(.6, 1)
    region = np.ix_(yy % size, xx % size)
    winning = grain > height[region]
    shade[region] = np.where(winning, rng.uniform(.65, 1), shade[region])
    height[region] = np.maximum(height[region], grain)
dx = (np.roll(height, -1, 1)-np.roll(height, 1, 1)) * 9
dy = (np.roll(height, -1, 0)-np.roll(height, 1, 0)) * 9
mask = np.clip(height*28, 0, 1)
# RG = signed slopes, B = grain brightness, A = grain coverage. Linear, mipmapped.
packed = np.stack([np.clip(.5-dx*.5,0,1), np.clip(.5-dy*.5,0,1), shade, mask], -1)
target = Path(__file__).resolve().parents[2] / 'Assets/_DeadlyJenga/Presentation/Resources/CandyFineSugar.png'
Image.fromarray(np.uint8(packed*255+.5), 'RGBA').save(target)
print(target)
