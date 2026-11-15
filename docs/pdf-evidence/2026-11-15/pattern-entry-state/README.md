# Preserve tiling-pattern parent entry state

One logical OnePDF patch against `d813ec6ba9dd138f7d2a6f3b8f5a51dc9c5f4bc8`. No remote publication has occurred. See `SOURCE-ALLOWLIST.md` for the eight changed files.

## Minimal visible defect

An ordinary form starts with line width 3, changes its current width to 9, then fills with a tiling pattern. The cell never sets its own width. Its matrix scales by 4. The correct solid line is therefore 12 points wide. Unchanged OnePDF initializes it to width 1, producing a 4-point line instead.

- `visuals/entry-inherited.pdf`: original minimal input.
- `visuals/before.png`: unchanged parser, incorrect 4-point line.
- `visuals/after.png`: final patched parser, correct 12-point line.
- `visuals/entry-explicit-control.pdf` and `explicit-control.png`: the cell explicitly sets width 3.
- `mupdf.png`, `pdfium.png`, `pdfjs.png`: observed independent outputs, including disagreements.

Every render is page zero, 72 dpi, 320 by 240 pixels, zero rotation, full MediaBox/CropBox and opaque white display background. The PDF paints its own opaque blue backdrop. Stroke RGB is (.7,.2,.4); backdrop is (.2,.4,.6). Interior and exterior rectangles are fixed in `expectations.json` before rendering. Tolerance is one RGB level; no boundary pixels are relied upon.

The six original probes cover the minimal case, explicit-width and ordinary-form controls, same-matrix A/B/A reuse, nested forms and nested patterns. Final output has zero mismatches and zero maximum channel error across 53,280 checked pixels. Unchanged output fails four probes with 6,800 mismatched pixels. The 18 independent reference renders yield nine strict passes and nine failures. MuPDF passes the minimal and resource controls but fails the nested-pattern probe; PDFium and PDF.js retain default-width mismatches. No oracle was changed to obtain consensus.

## Change

Capture parent entry values separately from mutable q/Q state. Preserve those values with the selected pattern for deferred interpretation. Initialize each cell from that state, with its own coordinate space and the required default alpha, blend and soft mask. Restore entry ownership after nested forms and Type3 procedures.

The complete immutable cache key includes drawing state, selected colour spaces, text state and dash values. It is constructed only when a pattern is actually selected. Equivalent form entries share cells; different entry states do not. The first reviewed implementation made 40 cells for 40 equal invocations; the final implementation makes one. No performance timing claim is made.

## Provenance

All new test programs, geometric inputs, oracle rectangles and adapters are independently authored and MIT licensed. No private document or derived image was used. No upstream fixture, artwork, glyph outline or third-party font was copied into the new source or this selection. The tiny Type3 procedures in the unit tests are original line geometry written in memory. Existing runtime packages are external dependencies and are not included.

Primary authority: [ISO 32000-1:2008](https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf), sections 7.8.3, 8.7.2, 8.7.3.1 and 11.6.7. `RESOURCE-REVIEW.md` distinguishes obsolete resource fallback and deliberately nonconforming robustness coverage from conformance claims.

References: MuPDF 1.26.11 through PyMuPDF 1.26.6; PDFium 153.0.7999.0 through pypdfium2 5.13.0; PDF.js 6.5.438 build 0a4153349 through Node 24.22.1 and @napi-rs/canvas 1.1.1. PDF.js uses a custom Node Canvas adapter, not a browser. The original and standalone adapters produce identical pixels. OneImage is unchanged at `e43ee264954a177ff01a16584625a3dea79244fa` for before/after renders.

See `VALIDATION.md` for checks and remaining limits, and `REPRODUCE.md` for commands. Raw failure logs and intermediate attempts are retained separately; none is counted as a passing check.
