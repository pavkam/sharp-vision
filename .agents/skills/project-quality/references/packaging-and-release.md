# Packaging and Release

## Load this reference when

Changing package metadata, versioning, packability, symbols, package consumers,
release notes, tags, NuGet publication, workflow permissions, or release gates.

## Normative documentation

- [Package publication](../../../../docs/testing/continuous-integration.md#package-publication)
- [Failure handling](../../../../docs/testing/continuous-integration.md#failure-handling)
- [Project structure](../../../../docs/architecture/project-structure.md#overview)
- [Public API compatibility](../../../../docs/testing/correctness-model.md#public-api-compatibility)

## Code map

- Version and package defaults: `Directory.Build.props` - each library owns an
  independent version property (`SharpVisionTerminalVersion`,
  `SharpVisionVersion`, `SharpVisionFigletFontsVersion`); they are not required
  to agree
- Library metadata: `src/SharpVision/SharpVision.csproj`,
  `src/SharpVision.Terminal/SharpVision.Terminal.csproj`, and
  `src/SharpVision.FigletFonts/SharpVision.FigletFonts.csproj`
- Packed consumer proof:
  `tests/SharpVision.Tests/Compatibility/FloatingSurfaceBaseConsumerTests.cs`
  and `DialogConsumerTests.cs`
- External specimen: `tests/SharpVision.Tests/Compatibility/PackageConsumers/`
- Publish workflow: `.github/workflows/sharpvision-publish.yml`

## Workflow

1. Define version, package IDs, dependency version, symbols, readme, icon,
   license, and release-note expectations.
2. Pack all three libraries as the workflow does and inspect archive contents.
3. Build and run an unfriended specimen against the produced packages.
4. Run API compatibility independently.
5. Prove duplicate-version, authentication, partial publish, symbols, summary,
   and failure paths in the workflow.

## Project-specific traps

- Publish `SharpVision.Terminal`, then `SharpVision`, then
  `SharpVision.FigletFonts`; an existing package must not suppress either
  missing sibling.
- The three libraries do not need to agree on a version; never reintroduce a
  check that requires them to. Only `SharpVision.FigletFonts`'s own dependency
  floor on `SharpVision` (`Directory.Packages.props`) ties two of their numbers
  together, and it must always reference `SharpVisionVersion` specifically, not
  a shared/overall version property.
- `SharpVision.FigletFonts` restores `SharpVision` from the ignored bootstrap
  feed so its production project never needs a project reference.
- Project references can see repository internals and do not prove package
  usability.
- Never allow one package or symbol failure to produce a successful release.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*FloatingSurfaceBaseConsumerTests;*DialogConsumerTests" \
  --minimum-expected-tests 1 --timeout 180s
```
