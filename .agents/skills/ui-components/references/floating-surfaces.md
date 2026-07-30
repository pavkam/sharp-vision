# Floating Surfaces

## Load this reference when

Changing Menu, ContextMenu, Popup, Flyout, Tooltip, Window, Dialog,
presentation, elevation, dismissal, movement, resize, modality, or focus
restoration.

## Normative documentation

- [Floating surfaces](../../../../docs/concepts/floating-surfaces.md#floating-surface-contract)
- [Modality](../../../../docs/concepts/modality.md#modality-contract)
- [Popup and Window presentation](../../../../docs/concepts/modality.md#popup-and-window-presentations)
- [Window](../../../../docs/controls/windows/window.md#window-contract)
- [Popup](../../../../docs/controls/popups/popup.md#popup-contract)
- [Dialog catalog](../../../../docs/dialogs/index.md#dialog-catalog)

## Code map

- Shared surface identity: `src/SharpVision/Surfaces/`
- Concrete families: `Menus/`, `Popups/`, `Windows/`, `Dialogs/`
- Shared modality and focus policy: `src/SharpVision/Runtime/`
- Tests mirror those families under `tests/SharpVision.Tests/`

## Workflow

1. Identify whether the fault belongs to presentation, visibility, shared modal
   routing, focus restoration, positioning, or disposal.
2. Test mounted ownership, z-order, pointer/keyboard isolation, initial focus,
   nested scopes, unavailable restoration targets, outside dismissal, removal,
   and exception paths.
3. Position movable Windows with Overlay offsets; preserve authored offsets
   while clamping the resolved border box.
4. Update the real showcase interaction, not a static imitation.

## Project-specific traps

- Floating does not imply modal; presentation and modality are separate.
- Popup opening is modal by contract; Window uses explicit modal presentation.
- `ModalityManager` and `FocusManager` own general restoration policy. Change
  Window only for Window-specific presentation ordering.
- There is one mounted surface identity; do not create a detached modal clone.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Windows*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Popups*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Dialogs*" \
  --minimum-expected-tests 1 --timeout 60s
```
