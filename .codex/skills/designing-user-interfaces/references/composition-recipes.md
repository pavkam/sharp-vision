# Composition Recipes

## Application shell

Use Dock for stable outer regions and add the fill workspace last:

```csharp
var shell = new Dock { LastChildFills = true };
Dock.SetSide(menu, Side.Top);
Dock.SetSide(status, Side.Bottom);
Dock.SetSide(sidebar, Side.Left);
shell.Children.Add(menu);
shell.Children.Add(status);
shell.Children.Add(sidebar);
shell.Children.Add(workspace);
```

Keep the menu/status height intrinsic or fixed only when their contract is
genuinely one row. Let the workspace receive the remainder.

## Responsive form or dialog

Use shared Grid tracks rather than independent rows:

```csharp
var form = new Grid { RowSpacing = 1, ColumnSpacing = 1 };
form.Columns.Add(Track.Auto());
form.Columns.Add(Track.Star(1, minimum: 12));
form.Columns.Add(Track.Auto());
form.Rows.Add(Track.Auto());
form.Rows.Add(Track.Auto());

Grid.SetColumn(field, 1);
Grid.SetColumn(action, 2);
Grid.SetRow(message, 1);
Grid.SetColumn(message, 1);
Grid.SetColumnSpan(message, 2);
form.Children.Add(label);
form.Children.Add(field);
form.Children.Add(action);
form.Children.Add(message);
```

Fields added to column 1 share width automatically. Auto actions stay compact;
the field absorbs growth and shrinks to its minimum.

## Split workspace

Use Grid when adjacent regions share the viewport:

```csharp
var workspace = new Grid { ColumnSpacing = 1 };
workspace.Columns.Add(Track.Percent(25, minimum: 16, maximum: 32));
workspace.Columns.Add(Track.Star(1, minimum: 20));
Grid.SetColumn(editor, 1);
workspace.Children.Add(navigation);
workspace.Children.Add(editor);
```

Use a percentage with limits for a responsive sidebar and Star for the primary
workspace. If the sidebar can disappear, collapse it and adjust the track model
as one retained state change.

## Toolbar and action row

Use a horizontal Stack only when actions form one sequence:

```csharp
var actions = new Stack
{
    Orientation = Orientation.Horizontal,
    Spacing = 1,
    Children = { save, cancel }
};
```

For actions that must align with fields or a footer edge, put them directly in
Grid action columns instead.

## Status region

Use a leading flexible message and trailing compact indicators. A Grid with
Star/Auto columns or a dedicated `StatusBar` expresses the relationship more
clearly than padding text with spaces.

## Layered feedback

Use Overlay for content plus non-modal feedback:

```csharp
var overlay = new Overlay { Children = { content, notification } };
Overlay.SetZIndex(notification, 10);
notification.HorizontalAlignment = HorizontalAlignment.Right;
notification.VerticalAlignment = VerticalAlignment.Top;
```

Make decorative feedback pointer-transparent. Use Popup or Window instead when
the layer needs anchored placement, focus transfer, or modality.

## Scrollable content

Scrolling is intrinsic to any `Container`: enable `AutoScroll` and select bars
on the container that owns the viewport. Do not introduce a ScrollView wrapper.
Keep fixed chrome outside that viewport in the surrounding Dock or Grid.
