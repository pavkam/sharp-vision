# State, input, and events

SharpVision controls are ordinary retained CLR objects. Bind persistent model
state through strongly typed expressions; use typed events for imperative
actions and assign controls on the dispatcher thread.

## Bind persistent model state

```csharp
using SharpVision.DataBinding;

var name = new TextInput();
var enabled = new CheckBox();

name.Bind(viewModel, model => model.Name);
enabled.Bind(viewModel, model => model.Enabled);
```

`TextInput` and `CheckBox` default to two-way synchronization. Nested paths,
observable collections, selection, dispatcher marshaling, and lifetime follow
the [data-binding contract](../concepts/data-binding.md#overview). Use direct
events when an action is not persistent model state.

## Update state from control events

```csharp
var count = 0;
var value = new Text("Count: 0");
var increment = new Button { Content = new Text("Increment") };
var enabled = new CheckBox
{
    Content = new Text("Enable increment"),
    IsChecked = true,
};

increment.Click += (_, _) =>
{
    count++;
    value.Content = $"Count: {count}";
};

enabled.StateChanged += (_, args) =>
{
    increment.IsEnabled = args.Current == true;
};
```

The properties commit before their change events run. `IsEnabled` participates
in inherited effective state, so disabling an ancestor also disables this
button. See the [`CheckBox` event order](../controls/input/check-box.md#api) and
[`Control` inherited-state rules](../controls/control.md#api).

## Handle routed input

Use a routed handler when a component needs preview/bubble behavior rather than
a control-specific event:

```csharp
_ = AddHandler(Events.Key, (_, args) =>
{
    if (args.Phase != RoutingPhase.Preview || args.Handled)
    {
        return;
    }

    if (args.Stroke.Code == Code.Escape)
    {
        Application?.Closed();
        args.Handled = true;
    }
});
```

Preview travels from root toward the target; bubble travels back from target to
root. Marking `Handled` stops ordinary handlers later in the route. Focus,
pointer capture, terminal-focus loss, and control availability are coordinated
with the same route. The complete ordering is in
[input routing](../concepts/input-routing.md#route-construction), while
[focus](../concepts/focus.md#overview) owns navigation and focus transfer.

## Pick the right mechanism

| Requirement                          | Use                                |
| ------------------------------------ | ---------------------------------- |
| A button was activated               | `Button.Click`                     |
| A value or selection changed         | The control's typed changed event  |
| Synchronize persistent model state   | Strongly typed data binding        |
| Observe ordinary property assignment | `Control.PropertyChanged`          |
| Intercept a key before a child       | Preview routed handler             |
| Handle unconsumed input from a child | Bubble routed handler              |
| Perform periodic UI work             | `DispatcherTimer`                  |
| Return from asynchronous work        | `Dispatcher.InvokeAsync` or `Post` |

Next, learn the
[background-work boundary](background-work.md#background-work-and-the-dispatcher).
