# Images

## Load this reference when

Changing Image ownership, validation, copying, placement, clipping, frame
composition, fallback cells, or graphics-renderer integration.

## Normative documentation

- [Image ownership](../../../../docs/concepts/images.md#imagesource-ownership-contract)
- [Bounds and validation](../../../../docs/concepts/images.md#bounds-and-validation)
- [Copy boundary](../../../../docs/concepts/images.md#copy-boundary)
- [Semantic graphics proof](../../../../docs/testing/rendering.md#semantic-graphics-proof)
- [Graphics backend boundary](../../../../docs/architecture/terminal-backends.md#graphics-backend-boundary)

## Code map

- Image data and placements: `src/SharpVision.Terminal/Graphics/`
- Frame integration: `src/SharpVision.Terminal/Rendering/`
- Public Image control: `src/SharpVision/Controls/Display/Image.cs`
- Tests: terminal `Graphics/` and `Rendering/`; UI `Controls/Display/Image*`

## Workflow

1. Define caller, frame, renderer, and backend ownership at every boundary.
2. Test dimensions, overflow, copy behavior, clipping, repeated frames, backend
   loss, cleanup, and ordinary-cell fallback.
3. Prove semantic placement separately from protocol bytes.
4. Load `terminal-systems` only when capability authorization or encoding
   changes.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Graphics*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*ImageTests" --minimum-expected-tests 1 --timeout 60s
```
