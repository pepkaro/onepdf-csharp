#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 OnePDF contributors
"""Compare fixed interior/exterior rectangles, without consensus-driven oracle changes."""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'expectations.json').read_text())
results = []
for directory in sys.argv[1:]:
    for fixture in manifest['fixtures']:
        source = root / 'inputs' / fixture['input']
        assert hashlib.sha256(source.read_bytes()).hexdigest() == fixture['sha256']
        path = root / directory / (fixture['name'] + '.png')
        picture = Image.open(path).convert('RGB')
        assert picture.size == (manifest['width'], manifest['height'])
        bad = checked = maximum = 0
        for region in fixture['regions']:
            x, y, width, height = region['rect']
            for pixel in picture.crop((x,y,x+width,y+height)).getdata():
                error = max(abs(a-b) for a,b in zip(pixel,region['rgb']))
                bad += error > region['tolerance']
                maximum = max(maximum, error)
                checked += 1
        results.append(dict(renderer=directory, case=fixture['name'], checked=checked, mismatched=bad,
                            max_channel_error=maximum, passed=bad == 0))
        print(directory, fixture['name'], 'PASS' if bad == 0 else 'FAIL', f'{bad}/{checked}', f'max={maximum}')
(root / ('results-' + '-'.join(sys.argv[1:]) + '.json')).write_text(json.dumps(results,indent=2)+'\n')
raise SystemExit(any(not row['passed'] for row in results))
