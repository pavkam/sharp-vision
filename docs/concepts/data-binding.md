# Data binding

## Data-binding contract

SharpVision binds retained control properties to ordinary .NET model properties
through strongly typed expressions. Models remain caller-owned CLR objects;
binding adds no `DataContext`, virtual tree, reconciliation, polling, or string
property paths.

```csharp
using SharpVision.DataBinding;

input.Bind(customer, model => model.Address.City);
active.Bind(settings, model => model.Enabled);
```

The returned `Binding` keeps the relationship alive and implements
`IDisposable`. The target also owns it, so retain the return value only when the
application must stop synchronization early.

## Notification model

Initial synchronization works with any non-null reference-type model. Live
source updates use `INotifyPropertyChanged`. Every reachable object that owns a
replaceable segment implements that interface when its changes must remain
observable.

An exact property name refreshes only the affected path. A null or empty
property name refreshes every segment owned by the publisher. Unrelated names
are ignored. Binding never polls a plain object or rewrites it into a proxy.

## Modes and natural values

| Mode             | Initial value    | Source changes | Target changes |
| ---------------- | ---------------- | -------------- | -------------- |
| `OneWay`         | Source to target | Applied        | Ignored        |
| `TwoWay`         | Source to target | Applied        | Applied        |
| `OneWayToSource` | Target to source | Ignored        | Applied        |

Natural adapters choose a concrete default:

| Control                                          | Property        | Default mode |
| ------------------------------------------------ | --------------- | ------------ |
| `Text`                                           | `Content`       | `OneWay`     |
| `TextInput`                                      | `Text`          | `TwoWay`     |
| `CheckBox`, `RadioButton`                        | `IsChecked`     | `TwoWay`     |
| `Slider`, `ScrollBar`                            | `Value`         | `TwoWay`     |
| `ProgressBar`                                    | `Value`         | `OneWay`     |
| `ColorPicker`                                    | `Value`         | `TwoWay`     |
| `ListView`, `ComboBox`, `TabControl`, and `Menu` | `SelectedIndex` | `TwoWay`     |

`BindCommand` and `BindCommandParameter` bind `Button.Command` and its borrowed
parameter one-way. Existing `ICommand.CanExecuteChanged`, click ordering, and
execution remain owned by `Button`.

The generic escape hatch names both properties and supplies typed conversion:

```csharp
label.BindProperty(
    control => control.Content,
    viewModel,
    model => model.Count,
    count => count.ToString(CultureInfo.InvariantCulture),
    convertBack: null,
    BindingMode.OneWay,
    fallbackValue: "0");
```

`TwoWay` and `OneWayToSource` converted bindings require a reverse converter.
Converters execute once per destination update and never hide endpoint errors.

## Paths, nulls, and ordering

Paths contain public instance properties rooted in the expression parameter.
Fields, methods, indexers, static properties, captured roots, conditionals, and
arithmetic are rejected before subscription or mutation. Expressions compile
once; updates call cached accessors without reflection or string lookup.

For `model => model.Address.City`, binding observes both `Address` and the
current address's `City`. Replacing `Address` unsubscribes the old branch and
observes the replacement.

A null intermediate makes the path unavailable. Natural adapters project a safe
fallback: empty text, null check state, current range minimum, or selection
index `-1`. Observation resumes when the branch becomes reachable. A null leaf
is an ordinary value.

A reverse update cannot write through a null intermediate. It throws
`InvalidOperationException` after target commit and before model mutation;
binding never constructs model objects or silently discards input.

Binding compares the converted destination before invoking its setter. Equal
values do not assign. Direction guards suppress only the binding's own echo.
Because target properties commit before `Control.PropertyChanged`, two-way model
updates occur before later typed events such as `TextInput.TextChanged`.

Invalid declarations fail before registration. Failed initialization removes its
subscriptions. Runtime getter, converter, setter, validation, and subscriber
exceptions preserve their identity. Only one live binding may author a target
property.

## Dispatcher and responsiveness

Attached targets remain dispatcher-affine. Source notifications already on the
dispatcher apply inline. A worker notification marks the binding dirty and posts
at most one callback. Notifications coalesce, and the callback reads the latest
complete path instead of replaying values.

A notification arriving during execution requests one additional latest-value
pass. Sustained publication yields through another single callback. Queue
saturation propagates, clears scheduled state, and permits a later retry.
Binding retains no unbounded event history.

Detached targets update synchronously; concurrent mutation of one detached
control remains unsupported. Explicit disposal of an attached binding is
dispatcher-affine.

Tests send 10,000 worker notifications while the dispatcher is occupied and
require one latest target assignment. Another 10,000-update test limits managed
allocation to 256 bytes per scalar update and proves reverse updates do not
recurse.

## Observable items and selection

```csharp
list.BindItems(viewModel, model => model.Results);
list.BindSelection(viewModel, model => model.SelectedResult);
```

`BindItems` connects a finite `IReadOnlyList<T>` to `ListView` or `ComboBox`.
Property replacement uses `INotifyPropertyChanged`; a current
`INotifyCollectionChanged` collection observes add, remove, replace, move, and
reset. Each action requests one latest complete snapshot. Null projects empty,
and replacement detaches the old collection.

Snapshot construction is `O(n)` because `ListView` currently realizes every
item. The collection remains stable while `Count` and its indexer are read on
the target dispatcher. Worker mutation of a thread-unsafe
`ObservableCollection<T>` still requires application dispatcher marshaling.

`BindSelection` supports reference-type values and single-selection `ListView`
or `ComboBox`. Matching uses `EqualityComparer<T>.Default` and the first equal
item. Null or no match selects `-1`; clearing selection writes null.

Items commits suppress transient reverse selection writes, then re-read model
selection. Replacing items cannot overwrite the model with a temporary empty or
first-item selection. Multiple selection remains explicit application logic
because it requires collection-diff and cancellation semantics.

## Test obligations

The target owns each binding, and a binding owns its model. `Binding.Dispose`
removes source, target, nested-path, and collection subscriptions. Target
disposal releases bindings before `OnDisposing` while preserving first-failure
cleanup. Detach, hide, and disable do not end data lifetime.

Tests cover every mode and adapter; nested replacement/null recovery; equality,
ordering, conversion, exceptions, and disposal; dispatcher coalescing and queue
retry; every collection action; coordinated selection; responsiveness; and the
consumer-facing compatibility surface.

`make test-binding-coverage` requires at least 95% line and 90% branch coverage
across binding production files. Missing binding files fail the gate rather than
producing a vacuous percentage.
