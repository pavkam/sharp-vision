# Test Infrastructure

## Load this reference when

Changing xUnit or Microsoft Testing Platform configuration, shared fixtures,
test discovery, filters, minimum counts, randomization, coverage, CI test
wiring, or repository-wide evidence policy.

## Normative documentation

- [Correctness model](../../../../docs/testing/correctness-model.md#overview)
- [Proof levels](../../../../docs/testing/correctness-model.md#proof-levels)
- [Discovery gate](../../../../docs/testing/correctness-model.md#discovery-gate)
- [Continuous integration](../../../../docs/testing/continuous-integration.md#overview)
- [Local commands](../../../../docs/testing/continuous-integration.md#local-command-mapping)
- [Randomized testing](../../../../docs/testing/randomized.md#overview)
- [Shape and reflection](../../../../docs/testing/correctness-model.md#shape-and-reflection)

## Code map

- Test projects: `tests/SharpVision.Terminal.Tests`, `SharpVision.Tests`, and
  `SharpVision.Compatibility.Tests`
- Shared build configuration: `Directory.Build.props`,
  `Directory.Packages.props`
- Local gates: `Makefile` and `package.json`
- CI: `.github/workflows/` and `.github/actions/`
- Coverage result checks: `scripts/validate-*-coverage.mjs`

## Workflow

1. Write a failing fixture for the missing or bypassable gate.
2. Preserve xUnit v3, Shouldly, Arrange/Act/Assert, deterministic fakes, and
   observable behavior.
3. Require `--minimum-expected-tests`; use supported prefix/suffix class or
   namespace filters.
4. Record random seeds and promote failures to named regressions.
5. Keep local and CI project lists, configurations, coverage, and failure
   semantics aligned.

## Project-specific traps

- `*Layout*Tests` is invalid Microsoft Testing Platform filter grammar; use a
  namespace or one leading/trailing wildcard.
- A snapshot is supplemental evidence, never the only oracle.
- Platform skips must be explicit and cannot hide missing portable evidence.
- Do not assert private call graphs, and do not reach production state through
  reflection (`BindingFlags`, `GetField`, `GetMethod`, `GetProperty`,
  `Activator`). Prefer a documented `internal` seam: both test assemblies are
  friend assemblies, and an `internal` member is excluded from the API snapshot,
  so it is not production surface.
- Do not hand-write API-shape assertions. `SharpVision.Compatibility.Tests`
  already freezes both public surfaces; a shape test duplicates it and covers
  less. Assert shape only alongside the behavior it protects, and only for
  accessibility the snapshot cannot express.

## Focused verification

```bash
node --test scripts/*.test.mjs
dotnet test --solution SharpVision.slnx --configuration Release --no-build \
  --minimum-expected-tests 3 --timeout 900s
```
