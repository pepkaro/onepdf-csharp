"""Original synthetic ICC v2.1 CMYK profile, reconstructed from the Nov13 diagnostic generator.
No third-party profile bytes; not a characterization of an actual printer. MIT license (see LICENSE).
Usage: python generated-cmyk-profile-20261114.py output.icc
"""
import sys, struct, itertools
from pathlib import Path
U=lambda x:struct.pack('>I',x)
H=lambda x:struct.pack('>H',x)
S=lambda x:struct.pack('>i',round(x*65536))
def xyz(x,y,z):return b'XYZ '+b'\0'*4+S(x)+S(y)+S(z)
def desc(t):
 b=t.encode()+b'\0';return b'desc'+b'\0'*4+U(len(b))+b+U(0)+U(0)+H(0)+b'\0'+b'\0'*67
def txt(t):return b'text'+b'\0'*4+t.encode()+b'\0'
def lut(ni,no,grid,func):
 b=b'mft2'+b'\0'*4+bytes([ni,no,grid,0])+b''.join(S(float(i==j)) for i in range(3) for j in range(3))+H(2)+H(2)
 b+=b''.join(H(v) for _ in range(ni) for v in (0,65535))
 for v in itertools.product(range(grid),repeat=ni):b+=b''.join(H(max(0,min(65535,round(x)))) for x in func(*(x/(grid-1) for x in v)))
 return b+b''.join(H(v) for _ in range(no) for v in (0,65535))
def color(c,m,y,k):
 l=max(0.,(1-k)*(1-.22*c-.30*m-.12*y));a=28*(m-c)*(1-k);b=28*(y-.5*(c+m))*(1-k)
 return l*65280,(a+128)*256,(b+128)*256
a2b=lut(4,3,5,color)
b2a=lut(3,4,5,lambda l,a,b:(0,0,0,(1-min(1,l*65535/65280))*65535))
tags={'desc':desc('Generated CMYK diagnostic - not a printing profile'),'cprt':txt('Original mathematical test data. MIT license; see LICENSE.'),'wtpt':xyz(.9642,1,.8249),'bkpt':xyz(0,0,0),'gamt':lut(3,1,5,lambda l,a,b:(0,))}
for i in range(3):tags[f'A2B{i}']=a2b;tags[f'B2A{i}']=b2a
head=bytearray(128);head[8:12]=bytes([2,0x10,0,0]);head[12:24]=b'prtrCMYKLab ';head[24:36]=struct.pack('>6H',2026,11,13,0,0,0);head[36:40]=b'acsp';head[40:44]=b'APPL';head[64:68]=U(1);head[68:80]=S(.9642)+S(1)+S(.8249)
offset=132+12*len(tags);table=bytearray();body=bytearray()
for sig,t in tags.items():
 table+=sig.encode()+U(offset)+U(len(t));pad=(-len(t))%4;body+=t+b'\0'*pad;offset+=len(t)+pad
out=head+U(len(tags))+table+body;out[:4]=U(len(out));Path(sys.argv[1]).write_bytes(out)
print(len(out))
