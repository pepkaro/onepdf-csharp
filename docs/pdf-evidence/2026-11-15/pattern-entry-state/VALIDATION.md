# Validation

## Passed

- Release build of the full OnePdf solution, with repository analyzers and warnings-as-errors: zero warnings and errors.
- Focused tests: all 23 pass, no skips. The 22 behavior checks fail 17/pass 5 with unchanged OnePDF. The additional helper-specific structural check proves lazy key construction and immutable capture.
- Full OnePdf suite: 3,904 total, zero failures/errors, 281 existing skips. This is 3,623 executed passes, not an all-tests-executed claim.
- Full layout suite: 465 total, zero failures/errors, 37 existing skips (428 executed passes).
- OneImage PDF integration with the final OnePDF loaded: 635 of 635 pass, no skips.
- Both smoke programs pass when executed as managed applications from the repository. This does not establish native-AOT publication.
- Local package/API compatibility validation: no breaking changes, zero warnings/errors. All eight generated package files pass the no-test-data check. Packages were not published or included here.
- Patch applies cleanly to the stated baseline; whitespace check is clean.
- Six final numeric visual probes: zero mismatches and zero maximum channel error across 53,280 checked pixels.
- Equal-entry cache control: 40 identical form invocations make one cell. The retained initial identity-key implementation makes 40. A/B/A makes two cells, with the first reused. Different outer alpha/blend alone does not rebuild a cell.

The existing full-suite skips involve missing external font/corpus/validator assets and existing documented limitations. None of the new 23 checks is skipped. No external asset was copied into the new tests to make these skips disappear.

## Remaining limits and retained failures

- The normal `dotnet test` launcher cannot create its IPC socket in this environment (permission denied). The supported direct xUnit executables completed the suites above.
- Both native-AOT publish attempts stop in `ComputeManagedAssemblies`: MSBuild cannot create/connect the NET x64 task host (MSB4216 followed by MSB4027). Native executables were not produced or run. This remains a CI check to complete on a host where that task can run.
- No performance benchmark was run. Structural reuse and lazy-key checks establish allocation/work avoidance, not wall-clock performance equivalence.
- Nine of 18 independent reference renders fail their fixed numeric oracles. These disagreements remain in `reference-results.json`. The nested-pattern MuPDF mismatch is not hidden.
- The nonconforming cross-context-pattern unit test is robustness coverage, not a conformance claim. The omitted-resources case is obsolete-reader compatibility coverage.
- No corpus milestone credit or fix for Multiply backdrop compositing or shading-pattern ExtGState is claimed.

Development attempts are retained separately: the first rendering adapter omitted OnePdf.Fonts; cached reference resolution required a clean adapter rebuild; a standalone PDF.js cleanup call used the wrong API after producing pixels; an xUnit assertion triggered the collection-size analyzer; a guessed exclusion flag was rejected; a managed layout smoke run started from the wrong directory; and the first parallel pack invocation exited without diagnostics. Corrected runs completed. Failed attempts and partial outputs were not counted as passing checks.
