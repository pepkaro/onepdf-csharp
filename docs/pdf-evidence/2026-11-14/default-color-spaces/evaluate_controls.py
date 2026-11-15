"""Render the independent MuPDF reference and compare unaltered renderer pixels. MIT."""
from pathlib import Path
import hashlib, json
import fitz
import numpy as np
from PIL import Image, ImageCms
root=Path(__file__).resolve().parent
profile=ImageCms.getOpenProfile(str(root/'synthetic-cmyk.icc'))
reference={'pymupdf':fitz.VersionBind,'mupdf':fitz.VersionFitz,'profile_name':ImageCms.getProfileName(profile).strip(),'renders':[]}
for name in ('comparison','direct','default','device'):
 with fitz.open(root/f'{name}.pdf') as doc:
  pix=doc[0].get_pixmap(matrix=fitz.Matrix(2,2),alpha=False)
  pix.save(root/f'mupdf-{name}.png')
  reference['renders'].append({'name':name,'width':pix.width,'height':pix.height,'rgb_sha256':hashlib.sha256(pix.samples).hexdigest()})
reference['warnings']=fitz.TOOLS.mupdf_warnings()
(root/'mupdf-report.json').write_text(json.dumps(reference,indent=2)+'\n')
def pixels(name):return np.array(Image.open(root/name).convert('RGB'))
def difference(a,b):
 d=np.abs(a.astype(int)-b.astype(int))
 return {'different_pixels':int(np.any(d,axis=2).sum()),'total_pixels':int(a.shape[0]*a.shape[1]),'mean_absolute_channel_difference':float(d.mean()),'maximum_channel_difference':int(d.max())}
result={p:difference(pixels(f'{p}-direct.png'),pixels(f'{p}-default.png')) for p in ('baseline','fixed','mupdf')}
result['explicit_unchanged']=bool(np.array_equal(pixels('baseline-direct.png'),pixels('fixed-direct.png')))
result['unprofiled_unchanged']=bool(np.array_equal(pixels('baseline-device.png'),pixels('fixed-device.png')))
result['fixed_vs_mupdf_direct']=difference(pixels('fixed-direct.png'),pixels('mupdf-direct.png'))
(root/'pixel-comparison.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
