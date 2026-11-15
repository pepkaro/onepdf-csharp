"""MIT: compare original paired controls without altering their pixels."""
from pathlib import Path
from PIL import Image
import json,numpy as np
root=Path(__file__).resolve().parent
pairs=[('implicit-sc','explicit-g'),('implicit-scn','explicit-g'),('implicit-SC','explicit-G'),('form-implicit-sc','form-explicit-g'),('initial-device-cmyk','explicit-black-cmyk')]
report={}
for engine in ('baseline','prereview','fixed','mupdf'):
 result=[]
 for left,right in pairs:
  def path(n):return root/(f'{n}-mupdf.png' if engine=='mupdf' else f'{engine}-{n}.png')
  a=np.array(Image.open(path(left)).convert('RGB'));b=np.array(Image.open(path(right)).convert('RGB'))
  d=np.abs(a.astype(int)-b.astype(int))
  result.append({'left':left,'right':right,'different_pixels':int(np.any(d,axis=2).sum()),'total_pixels':int(a.shape[0]*a.shape[1]),'maximum_channel_difference':int(d.max()),'left_rgb':a[16,16].tolist(),'right_rgb':b[16,16].tolist()})
 report[engine]=result
(root/'pair-metrics.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
