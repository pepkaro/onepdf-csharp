# MIT-only original evidence provenance

All new test/evidence inputs in this directory, their generators, harness source and generated outputs are independently authored for this investigation and licensed under the accompanying MIT LICENSE. No upstream corpus/test expression, PDF stream, font, artwork, private document or pre-existing rendered page is an input.

- `generate_profile.py` encodes a mathematical CMYK/Lab LUT16 ICC v2.1 profile from specification primitives. It does not characterize an actual printer. The original author/coordinator authorizes MIT licensing of this new work; its unshipped draft licensing label was replaced only in the newly authored generator and profile metadata.
- `generate_fixtures.py` writes every PDF object, stream and xref byte from original geometry/sample values. The current comparison has no font dictionary, text operator, font name or embedded font. Captions belong to README rather than renderer pixels.
- `initial-state/generate_review_fixtures.py` is the independent reviewer's original MIT generator for a linear Gray ICC and initial-colour controls. It is internal independently authored work, not copied upstream test source. Its CMYK controls use the original mathematical profile above.
- The raw PNGs are full-frame outputs produced by the named renderer from only those original inputs. They are never used as renderer inputs and are not adapted upstream expectations. No retouching, compositing, AI image generation or crops are involved.
- `harness/` and evaluation scripts are independently authored MIT tools. Rendering dependencies remain dependencies with their original licenses; none of their source/assets is relicensed or bundled as new fixtures.
- The new `DefaultColorSpaceTests.cs` in the patch is independently authored and has an MIT SPDX header. Existing production repository files and all repository/dependency license files retain their original licenses.

The original clean baseline, first reviewed candidate and revised candidate are distinct binaries. Each render run writes a `*-provenance.json` with actual loaded assembly hashes, versions, paths, runtime, DPI and worker count. First-candidate failures are retained as `prereview-*` controls rather than presented as a passed result. PyMuPDF binding and MuPDF engine versions are recorded separately.

Only explicit entries in the final publication manifest are cleared for inclusion. Harness build directories, logs with local runtime paths, scratch clones and historical draft evidence are excluded unless individually reviewed.
