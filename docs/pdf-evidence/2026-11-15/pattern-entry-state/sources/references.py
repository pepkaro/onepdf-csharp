#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 OnePDF contributors
"""Run installed reference engines as black boxes on the new independent PDFs."""
import json
import subprocess
import sys
from pathlib import Path
import fitz
import pypdfium2

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'expectations.json').read_text())
for engine in ['mupdf', 'pdfium', 'pdfjs']:
    folder = root / engine
    folder.mkdir(exist_ok=False)
    for fixture in manifest['fixtures']:
        source = root / 'inputs' / fixture['input']
        target = folder / (fixture['name'] + '.png')
        if engine == 'mupdf':
            with fitz.open(source) as doc:
                doc[0].get_pixmap(matrix=fitz.Matrix(1,1),alpha=False).save(target)
        elif engine == 'pdfium':
            with pypdfium2.PdfDocument(source) as doc:
                doc[0].render(scale=1,fill_color=(255,255,255,255)).to_pil().convert('RGB').save(target)
        else:
            run = subprocess.run(['node', sys.argv[1], sys.argv[2], str(source), str(target), '72'], capture_output=True, text=True)
            (folder / (fixture['name']+'.log')).write_text(run.stdout+run.stderr)
            run.check_returncode()
        print(engine, fixture['name'], flush=True)
print('MuPDF', fitz.VersionBind, 'PDFium', pypdfium2.PDFIUM_INFO, 'pypdfium2', pypdfium2.PYPDFIUM_INFO)
