"""
Generates document.ico from document.svg.
Requires Node + sharp (from the mdview packaging node_modules).
"""
import os, sys, io, subprocess, struct, zlib

DIR    = os.path.dirname(os.path.abspath(__file__))
SVG    = os.path.join(DIR, 'document.svg')
OUT    = os.path.join(DIR, '..', 'document.ico')
SHARP  = os.path.join(DIR, '..', '..', 'mdview', 'src', 'main', 'packaging', 'node_modules', 'sharp')
SIZES  = [16, 32, 48, 64, 128, 256]

def render_png(size):
    script = (
        f"const s=require({repr(SHARP)}),fs=require('fs');"
        f"s(fs.readFileSync({repr(SVG)})).resize({size},{size}).png().toBuffer()"
        ".then(b=>process.stdout.buffer?process.stdout.buffer.write(b):process.stdout.write(b))"
    )
    r = subprocess.run(['node', '-e', script], capture_output=True, cwd=DIR)
    if r.returncode != 0 or not r.stdout:
        print(f'sharp error at {size}px:', r.stderr.decode()); sys.exit(1)
    return r.stdout

def png_to_rgba(png_bytes):
    sig = png_bytes[:8]
    assert sig == b'\x89PNG\r\n\x1a\n'
    pos, chunks = 8, {}
    while pos < len(png_bytes):
        length = struct.unpack('>I', png_bytes[pos:pos+4])[0]
        ctype  = png_bytes[pos+4:pos+8]
        data   = png_bytes[pos+8:pos+8+length]
        chunks.setdefault(ctype, []).append(data)
        pos += 12 + length
    ihdr = chunks[b'IHDR'][0]
    w, h, bit_depth, colour_type = struct.unpack('>IIBB', ihdr[:10])
    assert bit_depth == 8 and colour_type == 6
    raw  = zlib.decompress(b''.join(chunks[b'IDAT']))
    stride, rows, i = w * 4, [], 0
    for _ in range(h):
        f = raw[i]; i += 1
        row = bytearray(raw[i:i+stride]); i += stride
        if f == 1:
            for x in range(4, len(row)): row[x] = (row[x] + row[x-4]) & 0xFF
        elif f == 2:
            if rows:
                prev = rows[-1]
                for x in range(len(row)): row[x] = (row[x] + prev[x]) & 0xFF
        elif f == 3:
            prev = rows[-1] if rows else bytes(stride)
            for x in range(len(row)):
                a = row[x-4] if x >= 4 else 0
                row[x] = (row[x] + (a + prev[x]) // 2) & 0xFF
        elif f == 4:
            prev = rows[-1] if rows else bytes(stride)
            for x in range(len(row)):
                a = row[x-4] if x >= 4 else 0
                b2 = prev[x]; c = prev[x-4] if x >= 4 else 0
                p  = a + b2 - c
                pa, pb, pc = abs(p-a), abs(p-b2), abs(p-c)
                pr = a if pa <= pb and pa <= pc else (b2 if pb <= pc else c)
                row[x] = (row[x] + pr) & 0xFF
        rows.append(bytes(row))
    return w, h, rows

def make_bmp(w, h, rows):
    bih = struct.pack('<IiiHHIIiiII', 40, w, h*2, 1, 32, 0, w*h*4, 0, 0, 0, 0)
    pixels = bytearray()
    for row in reversed(rows):
        for x in range(0, len(row), 4):
            r, g, b, a = row[x], row[x+1], row[x+2], row[x+3]
            pixels += bytes([b, g, r, a])
    mask_row = ((w + 31) // 32) * 4
    return bih + bytes(pixels) + bytes(mask_row * h)

entries = []
for size in SIZES:
    png = render_png(size)
    w, h, rows = png_to_rgba(png)
    entries.append((size, make_bmp(w, h, rows)))
    print(f'  rendered {size}x{size}')

n = len(entries)
header = struct.pack('<HHH', 0, 1, n)
offset = 6 + n * 16
directory = data = b''
for size, bmp in entries:
    sz = 0 if size == 256 else size
    directory += struct.pack('<BBBBHHII', sz, sz, 0, 0, 1, 32, len(bmp), offset)
    offset += len(bmp)
    data += bmp

out_path = os.path.normpath(OUT)
with open(out_path, 'wb') as f:
    f.write(header + directory + data)
print(f'Written: {out_path}  ({os.path.getsize(out_path):,} bytes)')
