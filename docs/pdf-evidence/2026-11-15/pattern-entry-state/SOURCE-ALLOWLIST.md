# Source allowlist

One logical patch against OnePDF d813ec6ba9dd138f7d2a6f3b8f5a51dc9c5f4bc8. No other source files are proposed.

- src/OnePdf/Content/ContentInterpreter.Forms.cs
- src/OnePdf/Content/ContentInterpreter.Patterns.cs
- src/OnePdf/Content/ContentInterpreter.Type3.cs
- src/OnePdf/Content/ContentInterpreter.cs
- src/OnePdf/Content/GState.cs
- src/OnePdf/Content/PatternEntryState.cs
- src/OnePdf/Rendering/PdfPatterns.cs
- tests/OnePdf.Tests/Shading/PatternEntryStateTests.cs

The original test file is MIT licensed and builds every PDF and tiny Type3 glyph procedure in memory. It reads no fixture, image or font asset. The new source helper and modifications use the repository’s MIT-or-Apache license.
