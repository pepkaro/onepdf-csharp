"""MIT. Direct reference renders of original reviewer fixtures, not upstream expectations."""
from pathlib import Path
import json,hashlib,struct
import fitz
from PIL import ImageCms
root=Path(__file__).resolve().parent
p=(root/'linear-gray.icc').read_bytes()
u=lambda at:struct.unpack_from('>I',p,at)[0]
assert u(0)==len(p) and p[8:12]==bytes([2,0x10,0,0]) and p[12:24]==b'mntrGRAYXYZ ' and p[36:40]==b'acsp'
tags={p[132+i*12:136+i*12].decode():(u(136+i*12),u(140+i*12)) for i in range(u(128))}
assert set(tags)=={'desc','cprt','wtpt','kTRC'}
for o,n in tags.values():assert o%4==0 and o+n<=len(p)
trc,sz=tags['kTRC'];assert p[trc:trc+4]==b'curv' and u(trc+8)==1 and p[trc+12:trc+14]==b'\x01\x00'
profile=ImageCms.getOpenProfile(str(root/'linear-gray.icc'))
report={'pymupdf':fitz.VersionBind,'mupdf':fitz.VersionFitz,'profileName':ImageCms.getProfileName(profile).strip(),'profileSha256':hashlib.sha256(p).hexdigest(),'profileTags':tags,'renders':[]}
for path in sorted(root.glob('*.pdf')):
 with fitz.open(path) as doc:
  assert not doc.is_repaired and doc.page_count==1
  pix=doc[0].get_pixmap(matrix=fitz.Matrix(1,1),alpha=False)
  pix.save(root/(path.stem+'-mupdf.png'))
  data=pix.samples;start=(16*pix.width+16)*pix.n
  report['renders'].append({'name':path.stem,'center':list(data[start:start+3]),'width':pix.width,'height':pix.height,'rgbSha256':hashlib.sha256(data).hexdigest(),'warnings':fitz.TOOLS.mupdf_warnings()})
(root/'mupdf-review.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
