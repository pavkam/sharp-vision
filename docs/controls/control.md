# Control base API

## Control contract

`Control` is the abstract mutable UI element. It belongs to at most one parent
and, while attached, exactly one
[`Dispatcher`](../concepts/threading.md#threading-contract). Detached trees can
be assembled on any thread. Attached mutation and disposal must run on that
dispatcher.

## Core properties

| Property                                   | Contract                                                |
| ------------------------------------------ | ------------------------------------------------------- |
| `Width`, `Height`                          | Fixed, percentage, automatic, or proportional `Length`. |
| `MinWidth`, `MinHeight`                    | Non-negative cell minimums.                             |
| `MaxWidth`, `MaxHeight`                    | Cell maximums not below the corresponding minimum.      |
| `Margin`                                   | External non-negative `Thickness`.                      |
| `Padding`                                  | Internal non-negative `Thickness`.                      |
| `HorizontalAlignment`, `VerticalAlignment` | Placement within the arranged slot.                     |
| `Visibility`                               | Visible, hidden, or collapsed.                          |
| `IsEnabled`                                | Inherited effective input state.                        |
| `CanFocus`, `TabIndex`                     | Focus participation and deterministic order.            |
| `DesiredSize`                              | Read-only result of the last successful measure.        |
| `Bounds`                                   | Read-only committed arranged rectangle.                 |

Setters validate before mutation, verify dispatcher access while attached, and
raise `PropertyChanged` once after the changed value is committed. Invalid
lengths, negative constraints, inconsistent min/max, invalid enum values, and
disposed access throw documented argument or object-lifetime exceptions.

`EffectiveIsEnabled` and `EffectiveIsVisible` are computed through the complete
ancestor chain. Changing an inherited state invalidates affected descendants.

## Children and ownership

`Container.Children` is the mutable ordered collection for traditional component
composition. `Add`, indexed insert and replacement, `Remove`, and `Clear`
validate the complete operation before changing ownership. A control cannot have
two parents, appear twice, or be inserted beneath one of its own descendants.

Adding below an attached container recursively attaches the subtree. Removing
recursively detaches it and clears its parent. Disposing a container disposes
all owned descendants; repeated disposal is safe.

```csharp
var panel = new StackPanel();
var button = new Button();

panel.Children.Add(button);
Debug.Assert(button.Parent == panel);
```

## Invalidation

Dirty phases form a dependency closure: measure implies arrange and render,
arrange implies render, and render stands alone. Property setters request the
earliest affected phase and coalesce repeated requests while they bubble to the
root.

| Change                                            | Dirty phases                 |
| ------------------------------------------------- | ---------------------------- |
| Width, height, min/max, margin, padding, collapse | Measure, arrange, and render |
| Horizontal or vertical alignment                  | Arrange and render           |
| Enabled state or visible/hidden transition        | Render                       |

## Lifecycle and events

Attachment assigns the same dispatcher recursively. Detachment clears it
recursively. Later infrastructure adds focus/capture cleanup and protected
measure, arrange, and render extension points without changing this ownership
contract.

Routed events will expose `OriginalSource`, `Source`, route phase, handled
state, and typed payload. `PropertyChanged` is available now; layout, focus,
pointer, key, text, and lifecycle notifications arrive with their corresponding
phase.

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
