# Reproduce

Use a checkout of OnePDF at d813ec6ba9dd138f7d2a6f3b8f5a51dc9c5f4bc8 and the repository's .NET SDK configuration.

```sh
git apply /path/to/pattern-entry-state.patch
dotnet build OnePdf.slnx -c Release -m:1
dotnet test -c Release --no-build
```

Where the test launcher cannot create its IPC socket, the project's supported direct xUnit executables run the same tests:

```sh
dotnet artifacts/bin/OnePdf.Tests/release/OnePdf.Tests.dll -class '*PatternEntryStateTests' -noLogo
dotnet artifacts/bin/OnePdf.Tests/release/OnePdf.Tests.dll -noLogo
dotnet artifacts/bin/OnePdf.Layout.Tests/release/OnePdf.Layout.Tests.dll -noLogo
```

The 23 focused checks consist of 22 behavior checks and one test of the new lazy key helper. To compare the unchanged parser, use the same compiled test assembly with the unchanged OnePDF DLL and exclude only the new helper-specific test:

```sh
dotnet OnePdf.Tests.dll -class '*PatternEntryStateTests' -method- '*UnusedEntryDefersCacheKeyAndKeepsACopyOfTheOriginalState' -noLogo
```

The unchanged parser fails 17 of those 22 behavior checks. This is not a skipped rendering case: the excluded test references a newly introduced internal helper absent from the old assembly.

## Original pixels

From this selection directory, generate all six PDFs into a fresh `inputs` directory. Generation refuses to overwrite inputs.

```sh
python3 sources/generate.py
```

Build the adapter with the directory containing externally built OneImage, OneImage.Pdf and OnePdf.Fonts DLLs, and select the exact baseline or patched OnePDF DLL. Use separate output directories and a rebuild so cached reference resolution cannot select an earlier dependency set.

```sh
dotnet build sources/Render.csproj -c Release -t:Rebuild -p:RendererDir=/external/renderer -p:ParserFile=/external/baseline/OnePdf.dll -o renderer-before
dotnet renderer-before/Render.dll inputs before
dotnet build sources/Render.csproj -c Release -t:Rebuild -p:RendererDir=/external/renderer -p:ParserFile=/external/patched/OnePdf.dll -o renderer-after
dotnet renderer-after/Render.dll inputs after
python3 sources/check.py before after
```

The checker deliberately exits nonzero if any retained result fails. Its fixed expectations and input hashes are not inferred from renderer agreement.

The external reference adapter requires Pillow, PyMuPDF and pypdfium2, with PDF.js and @napi-rs/canvas installed outside this selection:

```sh
python3 sources/references.py sources/render-pdfjs.mjs /external/node_modules/pdfjs-dist
python3 sources/check.py mupdf pdfium pdfjs
```

The independent reference comparison is expected to exit nonzero because disagreements remain.

`resource_controls.py` separately freezes three resource-scope hypotheses; `resource-observations.json` retains the observed painted rows. Its obsolete missing-resource case and the unit test explicitly named `NonconformingCrossContext...` must not be represented as clean conforming-writer examples.
