# Contributing to SharpVision

Thanks for helping make terminal software less surprising. SharpVision values
correctness, clear contracts, and proof over drive-by cleverness.

## Before you change code

Install the prerequisites listed in the
[README](README.md#build-the-repository), then run `make restore`. Read the
relevant document under `docs/`, the nearest tests, and the matching skill in
`.codex/skills/` before changing behavior.

SharpVision is documentation-first: a behavior is complete only when its
specification, public XML documentation, tests, and showcase example agree. Keep
lower terminal layers independent of UI layers; production code never references
tests.

## Development workflow

1. Write one focused failing test named `MethodName_WhenThis_ThatIsExpected`.
2. Confirm it fails for the behavior you intend to add or correct.
3. Implement the smallest correct change.
4. Update the normative document, XML documentation, and showcase example when
   the public behavior changes.
5. Run the focused proof, then run `make format`, `make lint`, `make build`, and
   `make test`.

Use Microsoft Testing Platform filters for focused work, for example:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*DecoderTests" --timeout 60s
```

Public APIs require XML documentation, explicit argument validation, and a
review of ownership, threading, terminal behavior, and compatibility impact.
Keep commits focused and use conventional commit prefixes such as `feat:`,
`fix:`, `test:`, `docs:`, or `chore:`.

## Pull requests

Explain the intent, link the governing specification, summarize the focused
red/green proof, and list every quality gate you ran. Do not fold unrelated
formatting or refactors into behavioral changes. If you found a bug but cannot
complete a correction, open an issue with a reproducible case instead.
