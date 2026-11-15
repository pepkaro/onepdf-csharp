# Original MIT before/after evidence

These unaltered 320×160 PNGs render the freshly authored polygon-font PDFs from the standalone v2 freeze. No private document or third-party font outlines are included. MIT provenance and reproduction instructions are in [fixture README](../../../tests/testdata/usecmap-stream-inheritance/README.md) and [MIT license](../../../tests/testdata/usecmap-stream-inheritance/LICENSE-MIT).

Before: exact main d813ec6… parser. After: that main plus only the UseCMap fix. The renderer/remaining dependencies are identical. Flattened control uses the baseline parser.

![Before: inheritance omitted](before.png)

![After: inherited pillar, overridden triangle, inherited pillar](after.png)

![Flattened baseline control](flattened-control.png)

The after and control images are byte-identical. Fixed oracle scope is 64 opaque interior/background probes, not an exhaustive edge mask. All three reference engines fail the inheritance PDF while passing the flattened control; their raw outputs and versions are retained separately. No reference-parity claim is made.
