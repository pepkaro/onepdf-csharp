# Fractional Indexed image Decode: specification rounding versus native-renderer truncation

Suggested owner: OnePDF image Decode/color conversion. This is an unresolved
compatibility choice, not a new OneImage fork regression or a mandatory change.
Please deduplicate against existing issues before publication.

## Minimal original MIT reproduction

Generate the two PDFs with `python generate.py NEW_DIRECTORY`. The primary image
input is 808 bytes and contains only four 2-bit indices0,1,2,3, a five-entry original
RGB palette, Decode[0.6,3.6], and a320x240 page. It has no external fonts, images,
actions or imported test material. Its vector control selects the same four real
indices directly. MediaBox=CropBox[0,0,320,240], rotation0,72DPI, nearest sampling,
opaque white renderer background (the page paints its own blue background).

All four mapped values0.6,1.6,2.6,3.6 are positive and inside0..hival4. There are
no half ties, negative values, clipping, transparency or color management.

ISO32000-1:2008 §8.9.5.2 maps image integers linearly through Decode. Section
8.6.6.3, printed page157, requires a real Indexed color value to be rounded to its
nearest integer, then limited to0..hival. Thus the fixed numerical oracle selects
palette entries1,2,3,4. Reference image paths select0,1,2,3 instead.
[Primary ISO specification](https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf)

## Actual results and scope

- OneImage with the reviewed source-built OnePDF dependency selects1,2,3,4 and
  passes the unchanged numerical oracle. The unmodified OnePDF1.22 source already
  explicitly rounds this value; no new fork defect or production edit is claimed.
- MuPDF1.26.11/PyMuPDF1.26.6: image selects0,1,2,3; direct vector selects1,2,3,4.
- PDF.js6.5.438 build0a4153349, Node24.22.1/@napi-rs/canvas1.1.1: same image/vector
  split. This is a Node Canvas measurement, not native browser validation.
- PDFium153.0.7999.0/pypdfium2 5.13.0: both tested image and vector paths select
  0,1,2,3. The binary does not expose a commit hash; source inspection below is
  pinned to the matching chromium/7999 branch revision, not asserted bit-for-bit
  source-to-binary provenance.

The six executions of this minimal pair yield two strict oracle matches and four
failures. Each image failure has34,944 mismatched interior pixels; all background
checks pass. The source image/oracle and all outputs are preserved. Earlier
positive-but-clipped, integer Decode and below-half controls confirm that the
mismatch is fractional rounding, not damaged input. Across all six control PDFs,
18 references yield10matches/8failures. The initial larger candidate's3references
all disagree, and it remains uncredited in the corpus.

## Verified source paths, without copied implementation content

- [OnePDF ImageLoader.cs, lines724–738](https://github.com/pepkaro/onepdf-csharp/blob/d813ec6ba9dd138f7d2a6f3b8f5a51dc9c5f4bc8/src/OnePdf/Images/ImageLoader.cs#L724-L738): its image palette conversion rounds the decoded Indexed value before converting color. This is unmodified1.22 source, not proof that a newer release behaves identically.
- [MuPDF draw-unpack.c, lines306–347](https://github.com/ArtifexSoftware/mupdf/blob/d189cc13194a1e32026b6161b543df02c120aac2/source/fitz/draw-unpack.c#L306-L347), reached by [image.c around702](https://github.com/ArtifexSoftware/mupdf/blob/d189cc13194a1e32026b6161b543df02c120aac2/source/fitz/image.c#L690-L705): fixed-point Indexed image remapping discards fractional bits before palette lookup. No upstream expression is reproduced here.
- [PDF.js image.js, lines556–610](https://github.com/mozilla/pdf.js/blob/0a4153349/src/core/image.js#L556-L610): fractional Decode results are stored in an unsigned integer component buffer. [IndexedCS, lines500–526](https://github.com/mozilla/pdf.js/blob/0a4153349/src/core/colorspace.js#L500-L526) rounds its input, but the image fraction has already been lost. The direct vector path reaches rounding without that loss.
- [PDFium CPDF_DIB palette construction, lines957–975](https://pdfium.googlesource.com/pdfium/+/e9fc01804a0c5224ea780ad782abb8cfede628ef/core/fpdfapi/page/cpdf_dib.cpp#957) computes fractional component values, then [CPDF_IndexedCS::GetRGB, lines91–104](https://pdfium.googlesource.com/pdfium/+/e9fc01804a0c5224ea780ad782abb8cfede628ef/core/fpdfapi/page/cpdf_indexedcs.cpp#91) converts the index to an integer by truncation. Both tested paths are consistent with this source behavior.

## Decision requested, not an automatic fix

Maintaining the normative nearest-index behavior preserves OnePDF's existing rule
and image/vector consistency. An explicit native-compatibility mode or changed
rounding would improve image parity with these pinned references, but would diverge
from the cited wording and (for MuPDF/PDF.js) their own vector behavior. Additional
product/reference review should decide whether any change is desirable. Keep the
original numerical oracle separate from compatibility comparison; do not bless an
output by majority vote or count this as a passing three-engine case.

The package includes only original MIT generator/PDFs and newly rendered outputs,
plus factual reports and links. It contains no upstream source or test assets.
