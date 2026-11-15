#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Fresh hand-authored polygon font and paired stream-inheritance PDFs."""
from pathlib import Path
import argparse,hashlib,json
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
ROOT=None

def font():
    names=['.notdef','pillar','bar','triangle']; glyphs={}
    points=[[],[(0,0),(300,0),(300,700),(0,700)],[(0,0),(500,0),(500,200),(0,200)],[(0,0),(600,0),(300,700)]]
    for name,vertices in zip(names,points):
        pen=TTGlyphPen(None)
        if vertices:
            vertices=list(reversed(vertices))  # TrueType outer contours are clockwise.
            pen.moveTo(vertices[0])
            for v in vertices[1:]: pen.lineTo(v)
            pen.closePath()
        glyphs[name]=pen.glyph()
    fb=FontBuilder(1000,isTTF=True);fb.setupGlyphOrder(names);fb.setupCharacterMap({65:'pillar',66:'bar',67:'triangle'});fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics({name:(800,0) for name in names});fb.setupHorizontalHeader(ascent=800,descent=-200)
    fb.setupNameTable({'familyName':'StreamParentOriginal','styleName':'Regular','uniqueFontIdentifier':'StreamParentOriginal-1','fullName':'StreamParentOriginal Regular','psName':'StreamParentOriginal-Regular','version':'Version 1.0'})
    fb.setupOS2(sTypoAscender=800,sTypoDescender=-200,usWinAscent=800,usWinDescent=200);fb.setupPost();fb.setupMaxp()
    fb.font.recalcTimestamp=False
    fb.font['head'].created=fb.font['head'].modified=2082844800
    fb.save(ROOT/'original.ttf');return (ROOT/'original.ttf').read_bytes()

def stream(data,extra=''):
    return f'<< /Length {len(data)} {extra} >>\nstream\n'.encode()+data+b'\nendstream'

def cmap(name,body):
    return (f'/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> def\n/CMapName /{name} def\n/CMapType 1 def\n/WMode 0 def\n'+body+'\nendcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n').encode()

def pdf(ttf,flat):
    parent=cmap('OriginalParent','1 begincodespacerange\n<00> <FF>\nendcodespacerange\n2 begincidchar\n<41> 2\n<42> 5\nendcidchar')
    body='1 begincodespacerange\n<00> <FF>\nendcodespacerange\n2 begincidchar\n<41> 2\n<42> 8\nendcidchar' if flat else '/OriginalParent usecmap\n1 begincidchar\n<42> 8\nendcidchar'
    meta='/Type /CMap /CMapName /OriginalChild /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /WMode 0'
    objs={1:b'<< /Type /Catalog /Pages 2 0 R >>',2:b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',3:b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 320 160] /CropBox [0 0 320 160] /Rotate 0 /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>',4:stream(b'0 g BT /F1 100 Tf 1 0 0 1 25 40 Tm <414241> Tj ET'),5:b'<< /Type /Font /Subtype /Type0 /BaseFont /StreamParentOriginal-Regular /Encoding 7 0 R /DescendantFonts [6 0 R] >>',6:b'<< /Type /Font /Subtype /CIDFontType2 /BaseFont /StreamParentOriginal-Regular /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 9 0 R /DW 800 /W [2 [550] 5 [650] 8 [800]] /CIDToGIDMap 11 0 R >>',7:stream(cmap('OriginalChild',body),meta+('' if flat else ' /UseCMap 8 0 R')),8:stream(parent,meta.replace('OriginalChild','OriginalParent')),9:b'<< /Type /FontDescriptor /FontName /StreamParentOriginal-Regular /Flags 4 /FontBBox [0 0 600 700] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 10 0 R >>',10:stream(ttf,f'/Length1 {len(ttf)}'),11:stream(bytes([0,0,0,0,0,1,0,0,0,0,0,2,0,0,0,0,0,3]))}
    out=bytearray(b'%PDF-1.7\n%\xe2\xe3\xcf\xd3\n');offsets=[0]
    for i,obj in objs.items(): offsets.append(len(out));out+=f'{i} 0 obj\n'.encode()+obj+b'\nendobj\n'
    xref=len(out);out+=f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode()
    for offset in offsets[1:]:out+=f'{offset:010d} 00000 n \n'.encode()
    out+=f'trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode();return out

def main():
    global ROOT
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    ROOT=args.output.resolve();ROOT.mkdir(parents=True,exist_ok=True)
    if any(ROOT.iterdir()):raise ValueError('Output directory must be empty; frozen inputs are never overwritten')
    ttf=font();(ROOT/'pdf').mkdir(exist_ok=True);rows=[]
    for name,flat in [('stream-parent',False),('flattened-control',True)]:
        data=pdf(ttf,flat);(ROOT/'pdf'/f'{name}.pdf').write_bytes(data)
        rows.append(dict(id=name,file=f'pdf/{name}.pdf',sha256=hashlib.sha256(data).hexdigest(),mediaBox=[0,0,320,160],cropBox=[0,0,320,160],rotation=0,renderWidth=320,renderHeight=160))
    (ROOT/'manifest.json').write_text(json.dumps(dict(schemaVersion=1,dpi=72,background=[255,255,255,255],fixtures=rows),indent=2)+'\n')
if __name__=='__main__':main()
