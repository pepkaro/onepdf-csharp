#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 OneImage contributors
"""Original four-sample Indexed controls; no external source/font/art/input."""
from pathlib import Path
import json,hashlib,sys
def stream(data, attrs=''):
    if isinstance(data, str): data = data.encode('ascii')
    return f'<< {attrs} /Length {len(data)} >>\nstream\n'.encode() + data + b'\nendstream'

class Pdf:
    def __init__(self):
        self.objects = [b'', b'', b'']
        self.resources = ''; self.page = ''; self.catalog = ''; self.parent = ''
        self.media = [0, 0, 320, 240]; self.inherit_media = False; self.inherit_resources = False
    def add(self, value):
        self.objects.append(value.encode('ascii') if isinstance(value, str) else value)
        return len(self.objects)
    def finish(self, content):
        content_ref = self.add(stream(content))
        self.objects[0] = f'<< /Type /Catalog /Pages 2 0 R {self.catalog} >>'.encode()
        box = '/MediaBox ['+' '.join(map(str,self.media))+']'
        resources = '/Resources << '+self.resources+' >>'
        self.objects[1] = f'<< /Type /Pages /Count 1 /Kids [3 0 R] {self.parent} {box if self.inherit_media else ""} {resources if self.inherit_resources else ""} >>'.encode()
        self.objects[2] = f'<< /Type /Page /Parent 2 0 R {"" if self.inherit_media else box} {"" if self.inherit_resources else resources} /Contents {content_ref} 0 R {self.page} >>'.encode()
        data = bytearray(b'%PDF-1.7\n% original MIT feature case\n'); offsets = []
        for i, value in enumerate(self.objects, 1):
            offsets.append(len(data)); data.extend(f'{i} 0 obj\n'.encode()+value+b'\nendobj\n')
        xref = len(data); data.extend(f'xref\n0 {len(self.objects)+1}\n0000000000 65535 f \n'.encode())
        data.extend(''.join(f'{x:010d} 00000 n \n' for x in offsets).encode())
        data.extend(f'trailer\n<< /Size {len(self.objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode())
        return bytes(data)

def image_object(doc, data, width, height, cs='/DeviceRGB', bits=8, attrs=''):
    return doc.add(stream(data,f'/Type /XObject /Subtype /Image /Width {width} /Height {height} /BitsPerComponent {bits} /ColorSpace {cs} '+attrs))

D=Path(sys.argv[1]);D.mkdir(exist_ok=False);(D/"pdf").mkdir();fixtures=[]
for vector in [False,True]:
 name=('vector' if vector else 'image')+'-positive-no-clipping';doc=Pdf();palette=[[230,45,80],[58,200,123],[46,97,223],[231,190,40],[30,210,220]];cs='[/Indexed /DeviceRGB 4 <e62d503ac87b2e61dfe7be281ed2dc>]';body='0.2 0.4 0.6 rg 0 0 320 240 re f '
 if vector:doc.resources='/ColorSpace << /Palette '+cs+' >>';body+='/Palette cs '+' '.join(f'{.6+x:.1f} scn {40+x*60} 40 60 160 re f' for x in range(4))
 else:
  obj=image_object(doc,b'\x1b',4,1,cs,2,'/Decode [0.6 3.6] /Interpolate false');doc.resources=f'/XObject << /Raster {obj} 0 R >>';body+='q 240 0 0 160 40 40 cm /Raster Do Q'
 raw=doc.finish(body);p=D/'pdf'/(name+'.pdf');p.write_bytes(raw);regions=[{'rect':[42+60*x,42,56,156],'rgb':palette[x+1],'tolerance':1} for x in range(4)];fixtures.append(dict(id=name,file='pdf/'+p.name,sha256=hashlib.sha256(raw).hexdigest(),bytes=len(raw),expectedRegions=regions,imageRect=[40,40,240,160],backgroundRgb=[51,102,153],renderWidth=320,renderHeight=240,mediaBox=[0,0,320,240],cropBox=[0,0,320,240],rotation=0,derivation='0.6,1.6,2.6,3.6 are positive and all within0..hival4; nearest integers1,2,3,4. No clipping, negative numbers or exact-half ties.'))

(D/"manifest.json").write_text(json.dumps({"fixtures":fixtures},indent=2)+"\n")
