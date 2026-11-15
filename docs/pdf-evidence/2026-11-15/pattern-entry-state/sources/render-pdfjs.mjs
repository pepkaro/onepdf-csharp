// SPDX-License-Identifier: MIT
// Copyright (c) 2026 OnePDF contributors
// Standalone black-box adapter; the installed reference packages are never shipped.
import { readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';

const [runtime, input, output, resolution = '72'] = process.argv.slice(2);
if (!runtime || !input || !output) throw new Error('Expected: PDF.js package directory, input PDF, output PNG, DPI.');
const root = resolve(runtime);
const require = createRequire(join(root, 'package.json'));
const graphics = require('@napi-rs/canvas');
for (const name of ['DOMMatrix', 'ImageData', 'Path2D']) globalThis[name] = graphics[name];
const pdfjs = await import(pathToFileURL(join(root, 'legacy/build/pdf.mjs')).href);
const job = pdfjs.getDocument({
    data: new Uint8Array(await readFile(input)),
    useWorkerFetch: false,
    isEvalSupported: false,
    disableFontFace: true,
    useSystemFonts: false,
    isOffscreenCanvasSupported: false,
    enableXfa: false,
});
const doc = await job.promise;
try {
    const page = await doc.getPage(1);
    const viewport = page.getViewport({ scale: Number(resolution) / 72 });
    const canvas = graphics.createCanvas(Math.ceil(viewport.width), Math.ceil(viewport.height));
    await page.render({ canvasContext: canvas.getContext('2d'), viewport, background: 'rgb(255,255,255)' }).promise;
    await writeFile(output, canvas.toBuffer('image/png'));
    console.log(JSON.stringify({ engine: 'PDF.js', version: pdfjs.version, build: pdfjs.build, page: 1, dpi: Number(resolution) }));
} finally {
    await job.destroy();
}
