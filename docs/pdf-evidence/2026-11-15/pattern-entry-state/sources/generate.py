#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 OnePDF contributors
"""Original geometric probes. Expected pixels are fixed before any renderer runs."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CELL = '/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 80 60] /XStep 80 /YStep 60 /Matrix [4 0 0 4 0 0]'
FORM = '/Type /XObject /Subtype /Form /BBox [0 0 320 240]'
LINE = '0.7 0.2 0.4 RG 2 30 m 78 30 l S'
PAINT = '/Pattern cs /P scn 0 0 320 240 re f'
BG = '0.2 0.4 0.6 rg 0 0 320 240 re f '

def stream(dictionary, body):
    return f'<< {dictionary} /Length {len(body.encode("ascii"))} >>\nstream\n{body}\nendstream'

def pdf(content, resources, extras):
    objects = {1: '<< /Type /Catalog /Pages 2 0 R >>', 2: '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
               3: f'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 320 240] /CropBox [0 0 320 240] /Resources << {resources} >> /Contents 4 0 R >>',
               4: stream('', BG + content), **extras}
    output = bytearray(b'%PDF-1.7\n'); offsets = {}
    for number, body in sorted(objects.items()):
        offsets[number] = len(output)
        output.extend(f'{number} 0 obj\n{body}\nendobj\n'.encode('ascii'))
    xref = len(output); count = max(objects) + 1
    output.extend(f'xref\n0 {count}\n0000000000 65535 f \n'.encode('ascii'))
    for number in range(1, count):
        output.extend(f'{offsets.get(number, 0):010} 00000 {"n" if number in offsets else "f"} \n'.encode('ascii'))
    output.extend(f'trailer\n<< /Size {count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode('ascii'))
    return bytes(output)

def regions(width=12, x=20, span=280):
    # Pixel coordinates, y down. Center 120, exact boundaries 120 +/- width/2.
    return [dict(rect=[x, 120-width//2+2, span, width-4], rgb=[179, 51, 102], tolerance=1),
            dict(rect=[x, 96, span, 12], rgb=[51, 102, 153], tolerance=1),
            dict(rect=[x, 132, span, 12], rgb=[51, 102, 153], tolerance=1)]

if __name__ == '__main__':
    out = ROOT / 'inputs'; out.mkdir(exist_ok=False)
    fixtures = []
    for name in ['entry-inherited', 'entry-explicit', 'ordinary-form', 'entry-a-b-a', 'nested-form', 'nested-pattern']:
        page = '3 w [] 0 d /F Do'; resources = '/XObject << /F 5 0 R >>'
        objects = {5: stream(FORM + ' /Resources << /Pattern << /P 6 0 R >> >>', '9 w [1 3] 0 d ' + PAINT),
                   6: stream(CELL + ' /Resources << >>', ('3 w ' if name == 'entry-explicit' else '') + LINE)}
        expected = regions()
        if name == 'ordinary-form':
            objects = {5: stream(FORM + ' /Resources << >>', '4 0 0 4 0 0 cm ' + LINE)}
        elif name == 'entry-a-b-a':
            page = ' '.join(f'q {x} 0 100 240 re W n {w} w [] 0 d /F Do Q' for x, w in [(0,3),(110,5),(220,3)])
            expected = regions(12, 20, 60) + regions(20, 130, 60) + regions(12, 240, 60)
        elif name == 'nested-form':
            objects[5] = stream(FORM + ' /Resources << /XObject << /Inner 7 0 R >> >>', '5 w /Inner Do')
            objects[7] = stream(FORM + ' /Resources << /Pattern << /P 6 0 R >> >>', '9 w [1 3] 0 d ' + PAINT)
            expected = regions(20)
        elif name == 'nested-pattern':
            objects[6] = stream(CELL + ' /Resources << /Pattern << /Inner 7 0 R >> >>', '7 w /Pattern cs /Inner scn 0 0 80 60 re f')
            objects[7] = stream(CELL.replace(' /Matrix [4 0 0 4 0 0]', '') + ' /Resources << >>', LINE)
        data = pdf(page, resources, objects)
        (out / (name + '.pdf')).write_bytes(data)
        fixtures.append(dict(name=name, input=name+'.pdf', sha256=hashlib.sha256(data).hexdigest(), regions=expected))
    (ROOT/'expectations.json').write_text(json.dumps(dict(dpi=72, page=0, width=320, height=240, background=[255]*4, fixtures=fixtures),indent=2)+'\n')
