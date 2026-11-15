# Default color spaces: correctness-only draft evidence

This is an explicitly narrower publication of the original MIT correctness
evidence. CORRECTNESS-ALLOWLIST.json describes the selected stored original files.
Those files are byte-for-byte unchanged, including complete raw before/final and
reference images, original PDFs/profiles and selected generators/checks.
This is not the complete frozen evidence package or its original full manifest.

The complete final raw timing dataset `performance/slowpath-results.json` and
its four transport chunks are NOT included. All performance reports, numerical
performance conclusions and transport representations are also excluded from
this draft. Performance review is incomplete; no speedup, performance-pass or
no-regression claim is made. No summary substitutes for the unpublished raw data.
The complete frozen package and contrary historical results remain preserved
separately for later authorized review.

Build/runtime records with absolute local workspace paths are deferred as well.
No denied payload has been transformed, redacted into a substitute, or sent by an
alternate route. The original full manifests are not presented as describing this
narrower tree. The allowlist identifies every omitted original publication path.

Correctness is tied to source tree d027a1d812f0150edfe027708bad144ec0322b21,
base d813ec6ba9dd138f7d2a6f3b8f5a51dc9c5f4bc8 and source patch SHA-256
4def99e9d68119d6ed5589a6a818b167e2c323c245454a8c4757fe0e69dade70.
All seven source file hashes were independently checked against the frozen
validated worktree. Full suite results: OnePDF 3,638 passed/281 existing skips;
layout 428 passed/37 existing skips; source-integrated OneImage PDF 635/635.
The 38 new regressions pass; the same tests fail 31/38 on baseline and 11/38 on the
first candidate. All 20 final raw visual controls match the corrected candidate.
These are correctness results, not a combined-stack or package-release claim.

In the 1136x400 comparison, explicit ICC is left, DefaultCMYK right, vectors top,
images bottom. In the 256x192 initial-state comparison, implicit is left and
explicit right. First-candidate failures remain visible. MuPDF corroborates the
within-engine equivalence; cross-engine byte identity is not claimed. PDFium and
PDF.js Node Canvas disagreements remain in the references directory and are not
counted as support. Node Canvas is not a native browser/WebView.

The source tests and included numeric generators use original MIT material.
Existing project/dependency licenses and OneImage's ordinary OnePDF 1.18.0 pin
are unchanged. Local OnePDF 1.22 source integration does not release a package.
