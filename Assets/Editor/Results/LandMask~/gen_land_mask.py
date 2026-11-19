# Generates the coarse 1-degree land/ocean mask used by MTS.Results.LandMask from Assets/Art/Results/WorldMap.jpg.
# (The folder name ends in "~" so Unity ignores it.)
# Usage: python -I gen_land_mask.py <WorldMap.jpg> <out.b64> [preview.png]
# Pixels are classified as sea (blue), land (green/brown) or unknown (white cloud / ice); each 1x1 degree cell takes the
# majority of its classified pixels, and cloud-covered cells are filled from their neighbours. The ice sheets are set by
# hand: everything south of 70 S is land (Antarctica), north of 84 N is sea (Arctic Ocean), and a rough Greenland outline
# is land. Bit k = row * 360 + col (row 0 = 90..89 S, col 0 = 180..179 W), set = land, little-endian bit order in bytes.
import sys, base64
from PIL import Image

img = Image.open(sys.argv[1]).convert('RGB')
W, H = img.size
px = img.load()
LAND, SEA, UNK = 1, 0, -1


def classify(r, g, b):
    mn, mx = min(r, g, b), max(r, g, b)
    if mn > 150 and mx - mn < 60: return UNK          # cloud / ice
    if b > r + 25 and b >= g: return SEA
    if mx - mn < 25 and mn > 110: return UNK           # grey haze
    return LAND


grid = [[UNK] * 360 for _ in range(180)]
for j in range(180):
    for i in range(360):
        x0 = int(i * W / 360); x1 = max(x0 + 1, int((i + 1) * W / 360))
        y1 = H - int(j * H / 180); y0 = max(0, H - int((j + 1) * H / 180)); y1 = max(y0 + 1, y1)
        land = sea = 0
        for y in range(y0, y1):
            for x in range(x0, x1):
                c = classify(*px[x, y])
                if c == LAND: land += 1
                elif c == SEA: sea += 1
        if land + sea >= 2: grid[j][i] = LAND if land > sea else SEA

changed = True
while changed:
    changed = False
    new = [row[:] for row in grid]
    for j in range(180):
        for i in range(360):
            if grid[j][i] != UNK: continue
            l = s = 0
            for dj in (-1, 0, 1):
                for di in (-1, 0, 1):
                    jj = j + dj
                    if 0 <= jj < 180:
                        v = grid[jj][(i + di) % 360]
                        if v == LAND: l += 1
                        elif v == SEA: s += 1
            if l + s:
                new[j][i] = LAND if l > s else SEA
                changed = True
    grid = new


def inside(lat, lon, poly):
    c = False
    n = len(poly)
    for k in range(n):
        y1, x1 = poly[k]; y2, x2 = poly[(k + 1) % n]
        if (y1 > lat) != (y2 > lat) and lon < (x2 - x1) * (lat - y1) / (y2 - y1) + x1: c = not c
    return c


greenland = [(60, -44), (65, -40), (68, -31), (70, -22), (76, -18), (81, -12), (83.5, -30), (82, -60), (78, -73),
             (76, -68), (70, -54), (66, -53), (61, -49)]
for j in range(180):
    lat = -90 + j + 0.5
    for i in range(360):
        lon = -180 + i + 0.5
        if lat < -70: grid[j][i] = LAND
        if lat > 81.5 and not (-95 < lon < -60 and lat < 83.5): grid[j][i] = SEA   # Arctic Ocean, not Ellesmere
        if -70 <= lat < -48:
            # Southern Ocean: cloud bands read as land; keep only Tierra del Fuego and the Antarctic Peninsula.
            keep = (-56 < lat and -76 < lon < -63) or (lat < -62 and -68 < lon < -55)
            if not keep: grid[j][i] = SEA
        if inside(lat, lon, greenland): grid[j][i] = LAND
        if grid[j][i] == UNK: grid[j][i] = SEA

# Drop specks of one or two cells (mostly cloud edges; real islands that small are lost, which is acceptable here).
seen = [[False] * 360 for _ in range(180)]
for j in range(180):
    for i in range(360):
        if grid[j][i] != LAND or seen[j][i]: continue
        stack = [(j, i)]; comp = []; seen[j][i] = True
        while stack:
            a, b = stack.pop(); comp.append((a, b))
            for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                aa, bb = a + da, (b + db) % 360
                if 0 <= aa < 180 and not seen[aa][bb] and grid[aa][bb] == LAND:
                    seen[aa][bb] = True; stack.append((aa, bb))
        if len(comp) <= 2:
            for a, b in comp: grid[a][b] = SEA

bits = bytearray(180 * 360 // 8)
for j in range(180):
    for i in range(360):
        k = j * 360 + i
        if grid[j][i] == LAND: bits[k >> 3] |= 1 << (k & 7)
open(sys.argv[2], 'w').write(base64.b64encode(bytes(bits)).decode())
if len(sys.argv) > 3:
    prev = Image.new('RGB', (360, 180))
    for j in range(180):
        for i in range(360): prev.putpixel((i, 179 - j), (60, 160, 60) if grid[j][i] == LAND else (20, 40, 120))
    prev.resize((1080, 540), Image.NEAREST).save(sys.argv[3])
print('land fraction', sum(bin(b).count('1') for b in bits) / 64800)
