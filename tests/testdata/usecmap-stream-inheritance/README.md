# Original stream-parent CMap visual pair, revision 2

All glyph contours, CMap programs, PDF objects, analytic oracle and generator code in this package were newly authored for this regression. No third-party font or unpublished expansion fixture was copied. The generated TrueType font, PDFs and their rendered evidence images are original test artifacts licensed under MIT, together with these scripts. FontTools is an installed build dependency, not an outline source. Repository LICENSE-MIT supplies the license terms.

Reproduce into a new temporary directory (never over frozen inputs):

    out=$(mktemp -d)
    python generate.py --output "$out"

The font uses clockwise outer contours, fixed head timestamps and disabled timestamp recalculation. Two independent temporary generations must match byte for byte before freezing/rendering. Structural validation checks contours, font table checksum, required metadata, page boxes and CMap dictionaries. The inheritance PDF has parent codespace, inherited CID2 and child CID8 override. The control changes only child CMap contents/dictionary to flatten the mappings; same font, content, widths and expected geometry.

The oracle deliberately claims only 64 fixed solid interior/background probes, requiring exact opaque RGBA black/white. It does not claim exhaustive pixel geometry or antialiased-edge comparison. Expected pillars: x25..55 and160..190,y40..110. Expected triangle vertices:(80,40),(140,40),(110,110). It is frozen before any render and is not tuned against engine output. Reference disagreements remain reported.

Revision2 is separately frozen: it disables FontBuilder timestamp recalculation, reverses contour winding to TrueType clockwise convention, tightens oracle opacity/color checks and provides this provenance/reproduction note. Revision1 remains unmodified and was never rendered. Control and safety guards receive zero extra corpus credit.
