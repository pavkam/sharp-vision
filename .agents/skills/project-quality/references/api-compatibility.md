# Public API Compatibility

## Load this reference when

Changing exported public/protected API, compatibility snapshots, accepted
versions, baseline review, assembly selection, or breaking-change evidence.

## Normative documentation

- [Public API compatibility](../../../../docs/testing/correctness-model.md#public-api-compatibility)
- [Project boundaries](../../../../docs/architecture/project-structure.md#overview)
- [Change rule](../../../../docs/architecture/project-structure.md#change-rule)
- [CI required evidence](../../../../docs/testing/continuous-integration.md#required-evidence)

## Code map

- Gate project: `tests/SharpVision.Compatibility.Tests/`
- Test owner: `PublicApiCompatibilityTests.cs`
- Accepted baselines: `Snapshots/<version>/*.verified.txt`
- Version source: `Directory.Build.props`
- Solution and CI wiring: `SharpVision.slnx`, `Makefile`, `.github/workflows/`

## Workflow

1. Run the gate before changing API and capture the expected baseline failure.
2. Inspect every received difference for intended additions, removals, namespace
   changes, signatures, visibility, defaults represented by types, and protected
   extensibility.
3. Snapshot the three bundled libraries: `SharpVision.Terminal`, `SharpVision`,
   and `SharpVision.FigletFonts`.
4. Accept a baseline manually after review; CI never rewrites verified files.
5. Keep baseline version selection deterministic and test failure diagnostics.

## Project-specific traps

- Green project compilation does not prove API compatibility.
- Project-reference API snapshots and packed-package consumer tests prove
  different contracts; retain both.
- Do not report a design or received file as an accepted compatibility gate.

## Focused verification

```bash
DiffEngine_Disabled=true dotnet test \
  --project tests/SharpVision.Compatibility.Tests \
  --minimum-expected-tests 1 --timeout 60s
```
