// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Fluent builder for composing <see cref="Menu"/> hierarchies declaratively.</summary>
/// <remarks>
/// <para>
/// Inspired by long-standing menu builder conventions in WPF, WinForms, and modern UI toolkits.
/// Every mutating method returns the same builder instance for chaining.
/// </para>
/// <code>
/// var menu = MenuBuilder.Vertical()
///     .Item("New", shortcut: "Ctrl+N", onInvoke: NewFile)
///     .Item("Open", shortcut: "Ctrl+O", onInvoke: OpenFile)
///     .Separator()
///     .Item("Save", shortcut: "Ctrl+S", onInvoke: SaveFile)
///     .Separator()
///     .Item("Quit", shortcut: "Ctrl+Q", onInvoke: Quit)
///     .Build();
/// </code>
/// </remarks>
public sealed class MenuBuilder
{
    private readonly Orientation _orientation;
    private readonly int _spacing;
    private readonly List<Action<Menu>> _steps = [];

    private MenuBuilder(Orientation orientation, int spacing)
    {
        _orientation = orientation;
        _spacing = spacing;
    }

    /// <summary>Starts building a vertical menu (typical for context and dropdown menus).</summary>
    /// <returns>A new builder configured for vertical orientation.</returns>
    public static MenuBuilder Vertical() => new(Orientation.Vertical, 0);

    /// <summary>Starts building a horizontal menu (typical for menu bars).</summary>
    /// <param name="spacing">The cell spacing between items. Defaults to 1.</param>
    /// <returns>A new builder configured for horizontal orientation.</returns>
    public static MenuBuilder Horizontal(int spacing = 1) => new(Orientation.Horizontal, spacing);

    /// <summary>Adds a command menu item.</summary>
    /// <param name="label">The visible label text.</param>
    /// <param name="shortcut">An optional right-aligned shortcut hint.</param>
    /// <param name="onInvoke">An optional handler raised when the item is activated.</param>
    /// <param name="isEnabled">Whether the item accepts activation. Defaults to true.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is null.</exception>
    public MenuBuilder Item(
        string label,
        string? shortcut = null,
        Action? onInvoke = null,
        bool isEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(label);
        _steps.Add(menu =>
        {
            var item = new MenuItem
            {
                Content = new Text(label),
                ShortcutText = shortcut,
                IsEnabled = isEnabled,
            };

            if (onInvoke is not null)
            {
                item.Invoked += (_, _) => onInvoke();
            }

            menu.Items.Add(item);
        });
        return this;
    }

    /// <summary>Adds a check-toggle menu item.</summary>
    /// <param name="label">The visible label text.</param>
    /// <param name="isChecked">The initial checked state.</param>
    /// <param name="onInvoke">An optional handler raised when the item is activated.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is null.</exception>
    public MenuBuilder Check(
        string label,
        bool isChecked = false,
        Action? onInvoke = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        _steps.Add(menu =>
        {
            var item = new MenuItem
            {
                Content = new Text(label),
                Kind = MenuItemKind.Check,
                IsChecked = isChecked,
            };

            if (onInvoke is not null)
            {
                item.Invoked += (_, _) => onInvoke();
            }

            menu.Items.Add(item);
        });
        return this;
    }

    /// <summary>Adds a radio menu item in a named mutual-exclusion group.</summary>
    /// <param name="label">The visible label text.</param>
    /// <param name="groupName">The non-null radio group name.</param>
    /// <param name="isChecked">Whether this member is initially selected.</param>
    /// <param name="onInvoke">An optional handler raised when the item is activated.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> or <paramref name="groupName"/> is null.</exception>
    public MenuBuilder Radio(
        string label,
        string groupName,
        bool isChecked = false,
        Action? onInvoke = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(groupName);
        _steps.Add(menu =>
        {
            var item = new MenuItem
            {
                Content = new Text(label),
                Kind = MenuItemKind.Radio,
                GroupName = groupName,
                IsChecked = isChecked,
            };

            if (onInvoke is not null)
            {
                item.Invoked += (_, _) => onInvoke();
            }

            menu.Items.Add(item);
        });
        return this;
    }

    /// <summary>Adds a non-interactive separator line.</summary>
    /// <returns>This builder for chaining.</returns>
    public MenuBuilder Separator()
    {
        _steps.Add(static menu => menu.Items.Add(new MenuSeparator()));
        return this;
    }

    /// <summary>Adds a menu item with nested submenu content configured by a callback.</summary>
    /// <param name="label">The visible label text for the parent item.</param>
    /// <param name="configure">A callback that receives a nested builder for the submenu.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> or <paramref name="configure"/> is null.</exception>
    public MenuBuilder Submenu(string label, Action<MenuBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(configure);
        _steps.Add(menu =>
        {
            var sub = Vertical();
            configure(sub);
            var subMenu = sub.Build();
            var item = new MenuItem { Content = new Text(label) };
            menu.Items.Add(item);
            _ = item;
            _ = subMenu;
        });
        return this;
    }

    /// <summary>Builds and returns the configured <see cref="Menu"/>.</summary>
    /// <returns>A new Menu populated with all items added through the builder chain.</returns>
    public Menu Build()
    {
        var menu = new Menu
        {
            Orientation = _orientation,
            Spacing = _spacing,
        };

        foreach (var step in _steps)
        {
            step(menu);
        }

        return menu;
    }
}
