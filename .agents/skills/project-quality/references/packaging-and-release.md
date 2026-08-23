# Packaging and Release

## Load this reference when

Changing package metadata, versioning, packability, symbols, release notes,
tags, NuGet publication, workflow permissions, or release gates.

## Normative documentation

- [Package publication](../../../../docs/testing/continuous-integration.md#package-publication)
- [Failure handling](../../../../docs/testing/continuous-integration.md#failure-handling)
- [Project structure](../../../../docs/architecture/project-structure.md#overview)
- [Public API compatibility](../../../../docs/testing/correctness-model.md#public-api-compatibility)

## Code map

- Version and package defaults: `Directory.Build.props` - each library owns an
  independent version property (`SharpVisionTerminalVersion`,
  `SharpVisionVersion`, `SharpVisionDocumentVersion`,
  `SharpVisionFigletFontsVersion`, `SharpVisionSyntaxHighlightingVersion`); they
  are not required to agree
- Library metadata: `src/SharpVision/SharpVision.csproj`,
  `src/SharpVision.Terminal/SharpVision.Terminal.csproj`,
  `src/SharpVision.Document/SharpVision.Document.csproj`,
  `src/SharpVision.FigletFonts/SharpVision.FigletFonts.csproj`, and
  `src/SharpVision.SyntaxHighlighting/SharpVision.SyntaxHighlighting.csproj`
- First-party version-derivation check:
  `tests/SharpVision.Compatibility.Tests/FirstPartyPackageVersionTests.cs` - the
  one package-consumption proof this repository keeps; see
  [Tests](../../../../AGENTS.md#tests) for why a packed-consumer mini-project
  per control was retired
- Publish workflow: `.github/workflows/sharpvision-publish.yml`

## Workflow

1. Define version, package IDs, dependency version, symbols, readme, icon,
   license, and release-note expectations.
2. Pack every library as the workflow does and inspect archive contents.
3. Run `FirstPartyPackageVersionTests` and API compatibility independently.
4. Prove duplicate-version, authentication, partial publish, symbols, summary,
   and failure paths in the workflow.

## Project-specific traps

- Publish `SharpVision.Terminal` before `SharpVision`, and `SharpVision` before
  its optional leaves (`SharpVision.Document`, `SharpVision.FigletFonts`,
  `SharpVision.SyntaxHighlighting`); an existing package must not suppress
  either missing sibling.
- The libraries do not need to agree on a version; never reintroduce a check
  that requires them to. Only an optional leaf's own dependency floor on
  `SharpVision` (`Directory.Packages.props`) ties two of their numbers together,
  and it must always reference `SharpVisionVersion` specifically, not a
  shared/overall version property.
- `SharpVision.FigletFonts` restores `SharpVision` from the ignored bootstrap
  feed so its production project never needs a project reference.
- Project references can see repository internals and do not prove package
  usability - `FirstPartyPackageVersionTests` proves the version-derivation
  contract specifically, not general consumability.
- Never allow one package or symbol failure to produce a successful release.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Compatibility.Tests \
  --filter-class "*FirstPartyPackageVersionTests" \
  --minimum-expected-tests 1 --timeout 60s
```
