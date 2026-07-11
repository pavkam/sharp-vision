# Window

## Window contract

`Window` is a top-level focus scope with optional title, border, content,
commands, activation, z-order, modal ownership, and move/resize behavior.

## API

- `Content` uses managed ownership; `Title` is non-null text content.
- `State` is normal, maximized, or minimized when the host supports it.
- `Left`, `Top`, `Width`, and `Height` validate against sizing policy;
  `MinWidth/Height` and `MaxWidth/Height` apply during interactive resize.
- `CanMove`, `CanResize`, `IsModal`, and `Owner` define interaction/ownership.
- `Opening`/`Closing` are cancellable; `Opened`, `Closed`, `Activated`,
  `Deactivated`, `Moved`, and `Resized` report committed state.

## Lifecycle and interaction

Open attaches the subtree, establishes scope, measures/arranges, activates, then
renders before `Opened`. Close validates modal/owner rules, releases capture and
focus, detaches, activates the next eligible window, and raises `Closed`.

Move/resize use keyboard or pixel/cell pointer capture, remain within documented
viewport policy, respect min/max, and coalesce events per committed frame.

## Example

```csharp
var window = new Window
{
    Title = "SharpVision Showcase",
    Content = root,
    Width = Length.Percent(100),
    Height = Length.Percent(100),
};
```

## Test obligations

Cover open/close order/cancellation, owner/modal behavior, activation/z-order,
focus restore, default/cancel buttons, move/resize/capture/min/max, terminal
resize, maximize/minimize fallback, tiny bounds, content ownership, and frames.
