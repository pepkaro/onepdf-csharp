# SPDX-License-Identifier: MIT
"""Limited analytic interior/background probes, fixed before any renderer output exists."""
from PIL import Image

def check_pixels(path,fixture):
    im=Image.open(path).convert('RGBA'); failures=[]; checked=0
    if im.size != (320,160):return dict(status='fail',reason='Unexpected dimensions',actual=list(im.size))
    # Source coordinates are PDF points; PNG rows are top-down at72dpi.
    # Expected pillars span x25..55 and160..190, y40..110.
    # Triangle vertices (80,40),(140,40),(110,110).
    black=[(x,y) for x in [30,40,50,165,175,185] for y in [45,65,85,105]]
    black += [(110,50),(110,70),(110,90),(95,50),(125,50)]
    white=[(x,y) for x in [10,65,150,205,240,300] for y in [20,50,80,120,145]]
    white += [(85,100),(135,100),(90,80),(130,80),(110,120)]
    for expected,probes in [('black',black),('white',white)]:
        for x,y in probes:
            rgb=im.getpixel((x,159-y));checked+=1
            ok=rgb == (0,0,0,255) if expected=='black' else rgb == (255,255,255,255)
            if not ok:failures.append(dict(x=x,y=y,expected=expected,actual=list(rgb)))
    return dict(status='pass' if not failures else 'fail',checked=checked,scope='64 fixed interior/background probes only; not a full geometry or edge mask',failures=failures)
