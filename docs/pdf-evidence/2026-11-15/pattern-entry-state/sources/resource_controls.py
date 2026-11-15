#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 OnePDF contributors
"""Freeze separate hypotheses about shared and omitted form resource dictionaries."""
import importlib.util, json, hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[1]
s=importlib.util.spec_from_file_location('original',Path(__file__).with_name('generate.py'))
g=importlib.util.module_from_spec(s);s.loader.exec_module(g)
out=root/'resource-inputs';out.mkdir(exist_ok=False)
rows=[]
for name in ['omitted-resources','shared-resources','explicit-resources']:
    resources='/XObject << /F 5 0 R >> /Pattern << /P 6 0 R >>'
    form_resources='' if name=='omitted-resources' else (' /Resources 9 0 R' if name=='shared-resources' else ' /Resources << /Pattern << /P 6 0 R >> >>')
    objects={5:g.stream(g.FORM+form_resources,'9 w [1 3] 0 d '+g.PAINT),6:g.stream(g.CELL+' /Resources << >>',g.LINE)}
    if name=='shared-resources':objects[9]='<< '+resources+' >>'
    data=g.pdf('3 w [] 0 d /F Do',resources,objects)
    if name=='shared-resources':
        # Share the actual dictionary object rather than two identical dictionaries.
        objects[3]='<< /Type /Page /Parent 2 0 R /MediaBox [0 0 320 240] /CropBox [0 0 320 240] /Resources 9 0 R /Contents 4 0 R >>'
        data=g.pdf('3 w [] 0 d /F Do',resources,objects)
    (out/(name+'.pdf')).write_bytes(data)
    rows.append(dict(name=name,sha256=hashlib.sha256(data).hexdigest(),hypotheses={'invocation_entry_width':3,'page_resource_owner_width':1,'current_width':9},resolved_expectation=None))
(root/'resource-hypotheses.json').write_text(json.dumps(rows,indent=2)+'\n')
