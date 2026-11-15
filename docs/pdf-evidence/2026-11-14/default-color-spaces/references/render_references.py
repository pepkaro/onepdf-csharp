# SPDX-License-Identifier: MIT
# Copyright (c) 2026 Original PDF color-control contributors
"""Render only our generated, bounded inputs with two external reference runtimes."""
from pathlib import Path
import hashlib,json,subprocess,sys
import numpy as np
from PIL import Image
import pypdfium2 as p
import pypdfium2.version as v
root=Path(__file__).resolve().parent.parent
out=root/'references'; runtime=Path(sys.argv[1]).resolve()
pairs=[('implicit-sc','explicit-g'),('implicit-scn','explicit-g'),('implicit-SC','explicit-G'),('form-implicit-sc','form-explicit-g'),('initial-device-cmyk','explicit-black-cmyk')]
inputs=[root/(x+'.pdf') for x in ('direct','default','device','comparison')]+[root/'initial-state'/(x+'.pdf') for x in sorted({x for pair in pairs for x in pair}|{'initial-comparison','explicit-icc','explicit-device'})]
reports={'pdfium':{'binding':str(v.PYPDFIUM_INFO),'engine':str(v.PDFIUM_INFO),'dpi':72,'draw_annots':True,'xfa_disabled':True,'files':[]},'pdfjs':{'files':[]}}
for source in inputs:
 assert source.stat().st_size<1048576
 name=source.stem
 with p.PdfDocument(source) as doc:
  doc.init_forms(config=p.raw.FPDF_FORMFILLINFO(version=2,xfa_disabled=True))
  page=doc[0]
  assert page.get_width()*page.get_height()<=512*512
  bitmap=page.render(scale=1,draw_annots=True)
  bitmap.to_pil().convert('RGB').save(out/f'pdfium-{name}.png')
  reports['pdfium']['files'].append({'name':name,'width':bitmap.width,'height':bitmap.height})
  bitmap.close(); page.close()
 run=subprocess.run(['node',str(out/'render-pdfjs-local-icc.mjs'),str(runtime),str(source),str(out/f'pdfjs-{name}.png'),'72'],capture_output=True,text=True,check=True)
 reports['pdfjs']['files'].append({'name':name,**json.loads(run.stdout),'stderr':run.stderr})
for engine in reports:
 result=[]
 for left,right in [('direct','default'),*pairs]:
  a=np.array(Image.open(out/f'{engine}-{left}.png').convert('RGB')); b=np.array(Image.open(out/f'{engine}-{right}.png').convert('RGB'))
  d=np.abs(a.astype(int)-b.astype(int))
  result.append({'left':left,'right':right,'different_pixels':int(np.any(d,axis=2).sum()),'total_pixels':int(a.shape[0]*a.shape[1]),'maximum_channel_difference':int(d.max()),'mean_absolute_channel_difference':float(d.mean())})
 reports[engine]['pairs']=result
 for entry in reports[engine]['files']:
  file=out/f'{engine}-{entry["name"]}.png'
  entry['sha256']=hashlib.sha256(file.read_bytes()).hexdigest()
reports['pdfjs']['runtime_sha256']={str(p.relative_to(runtime)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [runtime/'legacy/build/pdf.mjs',runtime/'legacy/build/pdf.worker.mjs',runtime/'wasm/qcms_bg.wasm']}
(out/'reference-report.json').write_text(json.dumps(reports,indent=2)+'\n')
print(json.dumps({k:v['pairs'] for k,v in reports.items()},indent=2))
