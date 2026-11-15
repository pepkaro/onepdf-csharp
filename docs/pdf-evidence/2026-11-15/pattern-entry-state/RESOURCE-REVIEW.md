# Resource and cache review

Primary source: ISO 32000-1:2008, sections 7.8.3, 8.7.2, 8.7.3.1, Table 95, and 11.6.7.
https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf

Section 7.8.3 makes resource names local to a content stream. A resource dictionary is associated with that stream, including the obsolete fallback from forms to page resources. Section 8.7.2 note 1 anchors a pattern used inside a form to the form's initial coordinate system. Thus two streams sharing one dictionary object do not thereby share an entry graphics state. The executing stream's entry is the relevant context. The omitted-resource control is compatibility coverage for an obsolete construct, not recommended conforming-writer practice.

The three independently written controls were frozen in resource-hypotheses.json before rendering. With explicit resources, a shared indirect dictionary, and omitted form resources, MuPDF renders exactly 12 painted rows (114 through 125 at x=100): inherited width 3 times pattern scale 4. PDFium and PDF.js render four rows (118 through 121) in all three cases, retaining their previously observed default-width disagreement. No reference result was reclassified to force consensus.

Section 8.7.2 note 2 prohibits patterns being defined in one context but used in another. The inherited-pattern selection test therefore remains explicitly identified as nonconforming-input robustness. It is not a conformance claim or corpus credit.

The first implementation used entry-object identity, which caused 40 equal form invocations to construct 40 cells. The revised cache key includes the complete immutable inherited state: the public graphics-state snapshot, both selected colour spaces, all eight text-state fields, and dash values by sequence. CTM is represented by the separate pattern-base key. Alpha constants, blend and soft mask are normalized to their required implicit-group defaults. Clip ownership is excluded because it belongs to the parent paint. Equal invocations now share one cell, including independently allocated but equal dash arrays; unequal A/B/A entry states yield two cells with the first reused for the final A. Changes to outer alpha/blend alone reuse the cell while preserving different outer compositing state. These are structural counts, not performance timings.

The final key construction is lazy. Each stream entry stores a private normalized GState copy, but the complete drawing/text/color/dash key and its hash are created only for an actual cache lookup. The new structural test verifies that capture and state creation leave the key absent, live-state mutation cannot alter the saved values, and hashing materializes the key. The pre-optimization 22-test candidate and its DLL are retained separately.
