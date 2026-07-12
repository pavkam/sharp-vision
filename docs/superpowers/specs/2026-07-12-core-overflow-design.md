# Core overflow and scrollbar design

## Goal

Unify every terminal scrollbar behind the public `ScrollBar` control and expose
one reusable overflow policy. The library must not maintain a private viewport
rail configuration that differs from standalone scrollbars.

## Public API

```csharp
[Flags]
public enum ScrollBars
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    Both = Horizontal | Vertical,
}

public enum ShowScrollBars
{
    Never,
    WhenNeeded,
    Always,
}

public enum ScrollBarStyle
{
    Thin,
    Full,
}

public enum ScrollBarFill
{
    Line,
    Block,
}
```

A scroll host accepts:

- `ScrollBars`, defaulting to `Both`, which enables horizontal and/or vertical
  scrolling.
- `ShowScrollBars`, defaulting to `WhenNeeded`, which controls whether
  enabled-axis chrome is hidden, automatic, or permanently reserved.
- `ScrollBarStyle` and `ScrollBarFill`, defaulting to `Full` and `Block`, which
  configure owned bars through the public ScrollBar API.

`Never` suppresses chrome while retaining wheel, keyboard, programmatic, and
bring-into-view scrolling on enabled axes. `None` disables scrolling on both
axes and clips overflow.

## Compatibility

`ScrollView` remains a public thin viewport façade during the migration. Its
obsolete `HorizontalBarVisibility` and `VerticalBarVisibility` properties
forward to the new common policy without changing behavior. New code uses the
common properties. `List` and the Showcase migrate immediately.

## Ownership and rendering

A scroll host owns two ordinary `ScrollBar` children. It configures them only
through `Orientation`, `Style`, and `Fill`; it never writes private glyph sets.
The bars retain their ordinary hit testing, focus, capture, keyboard, wheel, and
drag behavior.

`Thin` omits arrow-button cells and uses the complete bounds as track. `Full`
retains decrement and increment cells. `Line` renders a light line thumb and
track; `Block` renders shaded block-glyph chrome. Explicit glyph properties
remain supported and override the generated defaults.

## Verification

Tests prove exact glyph geometry for all four style/fill combinations, thin
thumb dragging, two-axis automatic reservation, `Never` chrome suppression with
retained scrolling, `None` axis suppression, legacy-property forwarding, List
migration, and Showcase virtual/tmux output.
