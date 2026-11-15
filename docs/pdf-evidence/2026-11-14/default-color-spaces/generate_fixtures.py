"""MIT-licensed original generated PDF colour-substitution controls. No third-party PDF/profile/font bytes."""
from pathlib import Path
import subprocess, sys
root=Path(__file__).resolve().parent
subprocess.run([sys.executable,str(root/'generate_profile.py'),str(root/'synthetic-cmyk.icc')],check=True)
profile=(root/'synthetic-cmyk.icc').read_bytes()
colors=[(0,0,0,0),(1,0,0,0),(0,1,0,0),(0,0,1,0),(0,0,0,0.5),(.2,.6,.8,.1),(.7,.4,.1,.2),(0,0,0,1)]
samples=bytes(round(v*255) for c in colors for v in c)
def stream(data,entries=''):
 return f'<< {entries} /Length {len(data)} >>\nstream\n'.encode()+data+b'\nendstream'
def pdf(objects,path):
 out=bytearray(b'%PDF-1.7\n%\xe2\xe3\xcf\xd3\n');offsets=[0]
 for n,obj in enumerate(objects,1):
  offsets.append(len(out));out+=f'{n} 0 obj\n'.encode()+obj+b'\nendobj\n'
 xref=len(out);out+=f'xref\n0 {len(objects)+1}\n0000000000 65535 f \n'.encode()
 for o in offsets[1:]:out+=f'{o:010} 00000 n \n'.encode()
 out+=f'trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode();path.write_bytes(out)
def make(mode):
 combined=mode=='comparison';width,height=(568,200) if combined else (256,128)
 cs='/Direct [/ICCBased 5 0 R]';cs+=' /DefaultCMYK [/ICCBased 5 0 R]' if mode!='device' else ''
 content=[]
 def swatches(x,y,direct,image):
  if image:content.append(f'q 256 0 0 56 {x} {y} cm /'+('ImDirect' if direct else 'ImDefault')+' Do Q');return
  for i,c in enumerate(colors):
   content.append(('/Direct cs ' if direct else '')+' '.join(map(str,c))+(' scn ' if direct else ' k ')+f'{x+i*32} {y} 32 56 re f')
 if combined:
  for x,direct in ((12,True),(300,False)):
   swatches(x,120,direct,False)
   swatches(x,20,direct,True)
 else:
  swatches(0,72,mode=='direct',False);swatches(0,0,mode=='direct',True)
 objects=[b'<< /Type /Catalog /Pages 2 0 R >>',b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
 f'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Contents 4 0 R /Resources << /ColorSpace << {cs} >> /XObject << /ImDirect 6 0 R /ImDefault 7 0 R >> >> >>'.encode(),
 stream('\n'.join(content).encode()),stream(profile,'/N 4 /Alternate /DeviceCMYK'),
 stream(samples,'/Type /XObject /Subtype /Image /Width 8 /Height 1 /BitsPerComponent 8 /ColorSpace [/ICCBased 5 0 R]'),
 stream(samples,'/Type /XObject /Subtype /Image /Width 8 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceCMYK')]
 pdf(objects,root/f'{mode}.pdf')
for mode in ('comparison','direct','default','device'):make(mode)
