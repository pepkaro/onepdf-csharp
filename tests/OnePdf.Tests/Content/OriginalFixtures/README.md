# Original text-matrix restore fixture

`text-q-restore-both-matrices-pdf20.pdf` is an independently authored MIT PDF 2.0 numeric program with an independently authored, embedded, unhinted rectangle TrueType font. No upstream example PDF, third-party font, private document, reference image, or content stream was copied.

The fixture is the frozen B04 `text-q-restore-both-matrices-pdf20` case. SHA-256: `41b815e8cbb46212e3a22ba6f330fa5cb985d8371235c2b5b6bbe4c105f90e9e`.

The font's units-per-em are 1000. A, B, and C have advance widths 1000, 800, and 600 and rectangular outlines 750x900, 500x600, and 300x400, respectively. At size 40, the text matrix at `q` is translated to (64,120), while the line matrix remains at (24,120). The inner text ends at (180,48), with its line matrix at (156,48). After `Q`, B must start at (64,120); `T*` with leading 34 then starts C at (24,86).

This expectation follows corrected ISO 32000-2:2020 clause 9.4.1:
https://pdf-issues.pdfa.org/32000-2-2020/clause09.html#9.4.1
The ISO-approved clarification is tracked at:
https://github.com/pdf-association/pdf-issues/issues/368

The fixture deliberately uses properly nested `q`/`Q` within one `BT`/`ET` object. It does not establish normative output for improperly nested objects, older PDF-version semantics, or unrelated text clipping cases. The interpreter applies the clarified behavior without a version gate, consistent with its existing acceptance of `q`/`Q` inside text objects.

To reproduce the exact PDF without overwriting it, run `python generate.py --output /tmp/text-matrix-reproduced.pdf` with `fontTools==4.61.1`. The self-contained generator uses the independently authored B04 numeric font and PDF serializers and checks the frozen SHA-256 before writing. No existing font or PDF is read.

All newly authored fixture material is MIT licensed; see LICENSE. The regression test asserts emitted glyph-run matrices directly. Pixel evidence uses the frozen, independently computed rectangle geometry rather than treating any renderer as an unconditional oracle.
