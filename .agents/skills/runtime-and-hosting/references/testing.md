# Runtime and Hosting Testing

## Load this reference when

Changing tests or claiming dispatcher, hosting, event-loop, terminal-service,
Session, transport, platform, or shutdown behavior complete.

## Normative documentation

- [Correctness model](../../../../docs/testing/correctness-model.md#correctness-model-contract)
- [Pseudoterminal testing](../../../../docs/testing/pseudoterminals.md#pseudoterminal-testing-contract)
- [Continuous integration](../../../../docs/testing/continuous-integration.md#continuous-integration-contract)
- [Error handling](../../../../docs/architecture/error-handling.md#error-handling-contract)
- [Lifecycle ordering](../../../../docs/concepts/lifecycle-events.md#ordering)

## Evidence ladder

- Deterministic unit tests with fake time, transport, restore operations, and
  recorded callbacks.
- Ordered integration tests across Application, Session, renderer, host lease,
  and platform mode boundaries.
- Failure injection after every acquisition and during every cleanup operation.
- Pseudoterminal proof for real Unix process, resize, and restoration behavior.
- Platform-specific console tests where the environment supports them.

Every `dotnet test` command must use supported filter grammar and
`--minimum-expected-tests 1`.

## Completion verification

Run the focused commands from changed references, then:

```bash
make format
make lint
make build
make test
```
