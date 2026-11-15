"""MIT-licensed original reviewer controls. No private or third-party asset bytes."""
from pathlib import Path
import struct
root=Path(__file__).resolve().parent
U=lambda x:struct.pack('>I',x)
H=lambda x:struct.pack('>H',x)
S=lambda x:struct.pack('>i',round(x*65536))
def xyz(x,y,z):return b'XYZ '+b'\0'*4+S(x)+S(y)+S(z)
def desc(t):
 b=t.encode()+b'\0';return b'desc'+b'\0'*4+U(len(b))+b+U(0)+U(0)+H(0)+b'\0'+b'\0'*67
tags={'desc':desc('Original linear Gray reviewer control'),'cprt':b'text'+b'\0'*4+b'MIT original mathematical control\0','wtpt':xyz(.9642,1,.8249),'kTRC':b'curv'+b'\0'*4+U(1)+H(256)}
head=bytearray(128);head[8:12]=bytes([2,0x10,0,0]);head[12:24]=b'mntrGRAYXYZ ';head[24:36]=struct.pack('>6H',2026,11,14,0,0,0);head[36:40]=b'acsp';head[40:44]=b'APPL';head[64:68]=U(1);head[68:80]=S(.9642)+S(1)+S(.8249)
offset=132+12*len(tags);table=bytearray();body=bytearray()
for sig,t in tags.items():
 table+=sig.encode()+U(offset)+U(len(t));pad=(-len(t))%4;body+=t+b'\0'*pad;offset+=len(t)+pad
profile=head+U(len(tags))+table+body;profile[:4]=U(len(profile))
(root/'linear-gray.icc').write_bytes(profile)
def stream(data,entries=''):
 return f'<< {entries} /Length {len(data)} >>\nstream\n'.encode()+data+b'\nendstream'
def pdf(objects,path):
 out=bytearray(b'%PDF-1.7\n%\xe2\xe3\xcf\xd3\n');offsets=[0]
 for n,obj in enumerate(objects,1):offsets.append(len(out));out+=f'{n} 0 obj\n'.encode()+obj+b'\nendobj\n'
 xref=len(out);out+=f'xref\n0 {len(objects)+1}\n0000000000 65535 f \n'.encode()
 for o in offsets[1:]:out+=f'{o:010} 00000 n \n'.encode()
 out+=f'trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode();path.write_bytes(out)
def make(name,content,resources,extras):
 pdf([b'<< /Type /Catalog /Pages 2 0 R >>',b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',f'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 32 32] /Contents 4 0 R /Resources << {resources} >> >>'.encode(),stream(content.encode()),*extras],root/(name+'.pdf'))
res='/ColorSpace << /DefaultGray [/ICCBased 5 0 R] /Direct [/ICCBased 5 0 R] >>'
for name,prefix in [('implicit-sc','0.25 sc'),('implicit-scn','0.25 scn'),('explicit-g','0.25 g'),('explicit-device','/DeviceGray cs 0.25 sc'),('explicit-icc','/Direct cs 0.25 scn')]:
 make(name,prefix+' 0 0 32 32 re f',res,[stream(profile,'/N 1 /Alternate /DeviceGray')])
for name,prefix in [('implicit-SC','0.25 SC'),('explicit-G','0.25 G')]:
 make(name,prefix+' 32 w 16 0 m 16 32 l S',res,[stream(profile,'/N 1 /Alternate /DeviceGray')])
# Legal Separation default, useful for initial-value callback semantics; MuPDF may not support this replacement family.
sep='/ColorSpace << /DefaultGray [/Separation /ReviewInk /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [0.2 0.4 0.6] /C1 [0.8 0.7 0.5] /N 1 >>] >>'
for name,prefix in [('sep-implicit',''),('sep-explicit-g','0 g'),('sep-explicit-device','/DeviceGray cs')]:make(name,prefix+' 0 0 32 32 re f',sep,[])
# Reuse only the session's original mathematical CMYK profile, not upstream or private data.
cmyk=(root.parent/'synthetic-cmyk.icc').read_bytes()
cmykres='/ColorSpace << /DefaultCMYK [/ICCBased 5 0 R] /Direct [/ICCBased 5 0 R] >>'
for name,prefix in [('initial-device-cmyk','/DeviceCMYK cs'),('explicit-black-cmyk','0 0 0 1 k'),('initial-direct-icc','/Direct cs')]:
 make(name,prefix+' 0 0 32 32 re f',cmykres,[stream(cmyk,'/N 4 /Alternate /DeviceCMYK /Range [0 1 0 1 0 1 0 1]')])
for name,prefix in [('form-implicit-sc','0.25 sc'),('form-explicit-g','0.25 g')]:
 make(name,'/F Do','/XObject << /F 6 0 R >>',[stream(profile,'/N 1 /Alternate /DeviceGray'),stream((prefix+' 0 0 32 32 re f').encode(),'/Type /XObject /Subtype /Form /BBox [0 0 32 32] /Resources << '+res+' >>')])
# Font-free, original full-page visual: implicit/initial selection on the left, explicit operator control on the right.
# Rows, top to bottom: page Gray, form Gray, initial CMYK black. Captions live in README, never in the renderer pixels.
contents=(
 'q 0.25 sc 0 128 112 48 re f Q q 0.25 g 144 128 112 48 re f Q '
 'q 3.5 0 0 1.5 0 64 cm /Implicit Do Q q 3.5 0 0 1.5 144 64 cm /Explicit Do Q '
 'q /DeviceCMYK cs 0 0 112 48 re f Q q 0 0 0 1 k 144 0 112 48 re f Q')
pdf([b'<< /Type /Catalog /Pages 2 0 R >>',b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
 b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 256 192] /Contents 4 0 R /Resources << /ColorSpace << /DefaultGray [/ICCBased 5 0 R] /DefaultCMYK [/ICCBased 6 0 R] >> /XObject << /Implicit 7 0 R /Explicit 8 0 R >> >> >>',
 stream(contents.encode()),stream(profile,'/N 1 /Alternate /DeviceGray'),stream(cmyk,'/N 4 /Alternate /DeviceCMYK /Range [0 1 0 1 0 1 0 1]'),
 stream(b'0.25 sc 0 0 32 32 re f','/Type /XObject /Subtype /Form /BBox [0 0 32 32] /Resources << /ColorSpace << /DefaultGray [/ICCBased 5 0 R] >> >>'),
 stream(b'0.25 g 0 0 32 32 re f','/Type /XObject /Subtype /Form /BBox [0 0 32 32] /Resources << /ColorSpace << /DefaultGray [/ICCBased 5 0 R] >> >>')],root/'initial-comparison.pdf')
