# Control base API

## Control contract

`Control` is the abstract mutable UI element. It belongs to at most one parent,
is owned by one dispatcher, participates in measure/arrange/render, and receives
routed events.

## Core properties

| Property                                   | Contract                                                |
| ------------------------------------------ | ------------------------------------------------------- |
| `Width`, `Height`                          | Fixed, percentage, automatic, or proportional `Length`. |
| `MinWidth`, `MinHeight`                    | Non-negative cell minimums.                             |
| `MaxWidth`, `MaxHeight`                    | Cell maximums not below the corresponding minimum.      |
| `Margin`                                   | External non-negative `Thickness`.                      |
| `HorizontalAlignment`, `VerticalAlignment` | Placement within the arranged slot.                     |
| `Visibility`                               | Visible, hidden, or collapsed.                          |
| `IsEnabled`                                | Inherited effective input state.                        |
| `CanFocus`, `TabIndex`                     | Focus participation and deterministic order.            |
| `Style`                                    | Optional direct style resource.                         |
| `Bounds`                                   | Read-only committed arranged rectangle.                 |

Setters verify dispatcher access and validate before mutation. Invalid lengths,
negative constraints, inconsistent min/max, invalid enum values, and disposed
access throw documented argument or object-lifetime exceptions.

## Lifecycle and events

Attachment assigns parent and dispatcher atomically. Detachment releases focus,
capture, resources, and inherited subscriptions. Measure, arrange, and render
are protected extension points with debug assertions over their phase
invariants.

Routed events expose `OriginalSource`, `Source`, route phase, handled state, and
typed payload. `PropertyChanged`, `LayoutUpdated`, focus, pointer, key, text,
and lifecycle notifications follow the shared event ordering.

## Example

```csharp
var button = new Button
{
    Width = Length.Cells(14),
    Margin = new Thickness(horizontal: 1, vertical: 0),
    IsEnabled = true,
};
```

## Test obligations

Every concrete control tests validation-before-mutation, phase-specific
invalidation, dispatcher affinity, attach/detach ownership, visibility, enabled
inheritance, focus/capture cleanup, zero/tiny bounds, and final cells.
