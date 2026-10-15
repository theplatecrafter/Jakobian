"""Reproducible original Jakobian fixtures, no external images or game assets.
PNG scanlines are north-to-south; Unity/CPU maps expose south-to-north rows.
"""
import math
from pathlib import Path
import struct
import zlib

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'GameData/JakobianTestBodies/Maps'
OUT.mkdir(parents=True, exist_ok=True)
W, H = 256, 129
PALETTE = [(158, 94, 56), (224, 188, 124), (62, 76, 85), (216, 224, 225)]

def png(name, rows):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    raw = b''.join(b'\0' + bytes(row) for row in reversed(rows))
    image = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0))
    image += chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')
    (OUT / name).write_bytes(image)

height_rows, color_rows, biome_rows = [], [], []
for y in range(H):
    height, color, biome = [], [], []
    lat = -math.pi / 2 + math.pi * y / (H - 1)
    for x in range(W):
        # Exact pole direction avoids quantization differences along the pole row.
        lon = 2 * math.pi * x / W if y not in (0, H-1) else 0
        dx, dy, dz = math.cos(lat)*math.cos(lon), math.sin(lat), math.cos(lat)*math.sin(lon)
        value = .45 + .17 * math.sin(dx*9 + dz*3)*math.cos(dy*8-dz*5)
        value += .07 * math.sin(dx*23+dy*19)*math.cos(dz*17-dy*13)
        # Smooth crater bowl, raised rim and a smaller crater on a different side.
        for centre, radius in [((1,0,0), .32), ((-.6,.48,.64), .23), ((.1,-.6,-.794), .19)]:
            separation = math.acos(max(-1,min(1, dx*centre[0]+dy*centre[1]+dz*centre[2]))) / radius
            value -= .31 * math.exp(-(separation/.75)**4)
            value += .26 * math.exp(-((separation-1)/.16)**2)
        value = max(0,min(1,value))
        identity = 3 if abs(dy)>.87 else 2 if value<.30 else 1 if value>.53 else 0
        shade = .78 + .32*value + .04*math.sin(dx*107+dz*61)*math.sin(dy*89-dz*47)
        h = round(255*value)
        height.extend((h,h,h))
        biome.extend(PALETTE[identity])
        color.extend(max(0,min(255,round(c*shade))) for c in PALETTE[identity])
    height_rows.append(height); color_rows.append(color); biome_rows.append(biome)
png('Atlas-height.png', height_rows)
png('Atlas-color.png', color_rows)
png('Atlas-biomes.png', biome_rows)
print('Generated 3 original 256x129 PNG8 maps in', OUT)
