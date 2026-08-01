# Input, Focus, and Modality

## Load this reference when

Changing routed events, hit testing, pointer capture, keyboard focus, tab or
directional navigation, access keys, modal planes, dismissal, or restoration.

## Normative documentation

- [Input routing](../../../../docs/concepts/input-routing.md#overview)
- [Pointer capture](../../../../docs/concepts/input-routing.md#pointer-capture-and-coordinates)
- [Focus](../../../../docs/concepts/focus.md#overview)
- [Modality](../../../../docs/concepts/modality.md#overview)
- [Modal focus](../../../../docs/concepts/modality.md#modal-focus)
- [Control state-machine evidence](../../../../docs/testing/controls-integration.md#controls-with-state-machines)

## Code map

- Routed input values and services: `src/SharpVision/Input/`
- Control event surface: `src/SharpVision/Controls/`
- Focus and modality integration: `src/SharpVision/Runtime/`
- Tests: `tests/SharpVision.Tests/Input/` and modality/control surface tests

## Workflow

1. Define target selection, capture, modal-plane membership, route snapshot,
   preview/bubble order, handled semantics, and cleanup.
2. Test pointer, keyboard, text, paste, focus, disabled/hidden state, removal,
   disposal, nested scopes, and unavailable restoration targets.
3. Validate saved focus at restoration time and fall back within the surviving
   plane.
4. Keep component presentation separate from shared routing and focus policy.

## Project-specific traps

- Capture wins over hit testing but remains constrained by modality.
- Focus receives keys; pointer coordinates remain in documented cell and pixel
  spaces.
- A Window or Popup owns presentation; `ModalityManager` and `FocusManager` own
  cross-surface isolation and restoration policy.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Input*" \
  --minimum-expected-tests 1 --timeout 60s
```
