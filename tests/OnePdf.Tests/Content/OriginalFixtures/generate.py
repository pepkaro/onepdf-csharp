#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 OneImage contributors
"""Regenerate only the original B04 PDF 2.0 q/Q fixture from numeric data.
No font, PDF, expected image, or external example is read.
Install fontTools==4.61.1; pass --output to avoid overwriting the frozen input.
The font and PDF serializers are the independently authored B04 functions.
"""
from pathlib import Path
import argparse, hashlib, io, struct
from fontTools import __version__ as FONTTOOLS_VERSION
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
NAME = 'B04OriginalRectangles'

def require(condition, message):
    if not condition:
        raise ValueError(message)

def font():
 require(FONTTOOLS_VERSION=='4.61.1','fontTools version must be 4.61.1 for pinned serialization')
 fb=FontBuilder(1000,isTTF=True);order=['.notdef','A','B','C'];fb.setupGlyphOrder(order)
 fb.setupCharacterMap({65:'A',66:'B',67:'C'})
 glyphs={}
 # Clockwise rectangles, all on-curve; empty .notdef. No hint instructions.
 for name,extent in [('.notdef',None),('A',(750,900)),('B',(500,600)),('C',(300,400))]:
  pen=TTGlyphPen(None)
  if extent:
   w,h=extent;pen.moveTo((0,0));pen.lineTo((0,h));pen.lineTo((w,h));pen.lineTo((w,0));pen.closePath()
  glyphs[name]=pen.glyph()
 fb.setupGlyf(glyphs);fb.setupHorizontalMetrics({'.notdef':(1000,0),'A':(1000,0),'B':(800,0),'C':(600,0)})
 fb.setupHorizontalHeader(ascent=900,descent=0,lineGap=100)
 fb.setupNameTable({'familyName':NAME,'styleName':'Regular','uniqueFontIdentifier':NAME+'-1','fullName':NAME,'psName':NAME,'version':'Version 1.000','copyright':'Copyright (c) 2026 OneImage contributors; MIT License'})
 fb.setupOS2(sTypoAscender=900,sTypoDescender=0,sTypoLineGap=100,usWinAscent=900,usWinDescent=0,fsType=0,sxHeight=400,sCapHeight=900)
 fb.setupPost(keepGlyphNames=True);fb.setupMaxp();fb.font['head'].created=2082844800;fb.font['head'].modified=2082844800;fb.font.recalcTimestamp=False
 buf=io.BytesIO();fb.save(buf);data=buf.getvalue();f=TTFont(io.BytesIO(data),checkChecksums=2)
 require(f['head'].unitsPerEm==1000 and f.getGlyphOrder()==order,'Font table contract')
 require(all(f['glyf'][n].numberOfContours==1 for n in order[1:]),'Rectangle contour contract')
 require(not any(t in f for t in ['fpgm','prep','cvt ']),'No hinting programs')
 padded=data+b'\0'*((-len(data))%4);require(sum(struct.unpack('>'+str(len(padded)//4)+'I',padded))&0xffffffff==0xB1B0AFBA,'TTF checksum adjustment')
 return data

def stream(data,extra=''):
 if isinstance(data,str):data=data.encode('ascii')
 return f'<< /Length {len(data)} {extra} >>\nstream\n'.encode()+data+b'\nendstream'

def pdf(content,ttf,version):
 objects=[b'<< /Type /Catalog /Pages 2 0 R >>',b'<< /Type /Pages /Count 1 /Kids [3 0 R] >>',b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 240 180] /CropBox [0 0 240 180] /Rotate 0 /UserUnit 1 /Resources << /Font << /F 5 0 R >> /ExtGState << /Zero << /Type /ExtGState /ca 0 /CA 0 >> /Opaque << /Type /ExtGState /ca 1 /CA 1 >> >> >> /Contents 4 0 R >>',stream(content),f'<< /Type /Font /Subtype /TrueType /BaseFont /{NAME} /FirstChar 65 /LastChar 67 /Widths [1000 800 600] /Encoding /WinAnsiEncoding /FontDescriptor 6 0 R >>'.encode(),f'<< /Type /FontDescriptor /FontName /{NAME} /Flags 32 /FontBBox [0 0 750 900] /ItalicAngle 0 /Ascent 900 /Descent 0 /CapHeight 900 /StemV 80 /MissingWidth 1000 /FontFile2 7 0 R >>'.encode(),stream(ttf,f'/Length1 {len(ttf)}')]
 result=bytearray(('%PDF-'+version+'\n%\xe2\xe3\xcf\xd3\n').encode('latin1'));offsets=[]
 for i,obj in enumerate(objects,1):offsets.append(len(result));result.extend(f'{i} 0 obj\n'.encode()+obj+b'\nendobj\n')
 start=len(result);result.extend(f'xref\n0 {len(objects)+1}\n0000000000 65535 f \n'.encode())
 for pos in offsets:result.extend(f'{pos:010d} 00000 n \n'.encode())
 result.extend(f'trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n'.encode());return bytes(result)

CONTENT = '1 0 0 rg\nBT /F 40 Tf 0 Tr 34 TL 1 0 0 1 24 120 Tm (A) Tj\nq 0 0 1 rg 1 0 0 1 146 68 Tm (B) Tj 10 -20 Td (C) Tj Q\n0 0 0 rg (B) Tj T* (C) Tj ET\n'
EXPECTED_SHA256 = '41b815e8cbb46212e3a22ba6f330fa5cb985d8371235c2b5b6bbe4c105f90e9e'

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    data = pdf(CONTENT, font(), '2.0')
    actual = hashlib.sha256(data).hexdigest()
    require(actual == EXPECTED_SHA256, 'Frozen fixture bytes changed: ' + actual)
    require(not args.output.exists(), 'Refusing to overwrite an existing file')
    args.output.write_bytes(data)
    print(actual)
