# Public API Compatibility

## Load this reference when

Changing exported public/protected API, compatibility snapshots, baseline
review, assembly selection, or breaking-change evidence.

## Normative documentation

- [Public API compatibility](../../../../docs/testing/correctness-model.md#public-api-compatibility)
- [Project boundaries](../../../../docs/architecture/project-structure.md#overview)
- [Change rule](../../../../docs/architecture/project-structure.md#change-rule)
- [CI required evidence](../../../../docs/testing/continuous-integration.md#required-evidence)

## Code map

- Gate project: `tests/SharpVision.Compatibility.Tests/`
- Test owner: `PublicApiCompatibilityTests.cs`
- Accepted baselines: `Snapshots/*.verified.txt` (one file per assembly, not
  versioned - there is no `Snapshots/<version>/` subfolder)
- Solution and CI wiring: `SharpVision.slnx`, `Makefile`, `.github/workflows/`

## Workflow

1. Run the gate before changing API and capture the expected baseline failure.
2. Inspect every received difference for intended additions, removals, namespace
   changes, signatures, visibility, defaults represented by types, and protected
   extensibility. Attribute-only lines never appear in the compared text (see
   below), so every surviving diff line is a genuine signature or shape change.
3. Snapshot the three bundled libraries: `SharpVision.Terminal`, `SharpVision`,
   and `SharpVision.FigletFonts`.
4. Decide, from the diff, whether the change is the kind that warrants bumping
   `OverallVersion` in `Directory.Build.props` before or alongside accepting the
   new baseline - this gate is the maintainer's own signal for that decision,
   not an automated release gate keyed to the version number.
5. Accept a baseline manually after review by overwriting the `.verified.txt`
   file with its paired `.received.txt` file; CI never rewrites verified files.

## Project-specific traps

- Green project compilation does not prove API compatibility.
- Project-reference API snapshots and packed-package consumer tests prove
  different contracts; retain both.
- Do not report a design or received file as an accepted compatibility gate.
- The compared text has every attribute-application line stripped before the
  diff (`PublicApiCompatibilityTests.RemoveAttributeLines`): attributes are
  metadata annotations, not binary-breaking surface, so adding, removing, or
  editing one is never itself a reason to flag or fail this gate.

## Focused verification

```bash
DiffEngine_Disabled=true dotnet test \
  --project tests/SharpVision.Compatibility.Tests \
  --minimum-expected-tests 1 --timeout 60s
```
