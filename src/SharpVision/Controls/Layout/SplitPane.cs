// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Owns two panes separated by one keyboard- and pointer-resizable cell divider.</summary>
[PublicAPI]
public sealed class SplitPane: Container
{
    private readonly CallbackTransitionStream _splitTransitions = new();

    /// <summary>Initializes an empty horizontal split with capacity for two panes.</summary>
    public SplitPane() : base(capacity: 2)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        EnableChromeAuthoring();
        IsFocusable = true;
        IsTabStop = true;
        TabNavigation = TabNavigation.Continue;
    }

    /// <summary>Raised after a changed authored leading-pane length commits.</summary>
    public event EventHandler<SplitChangedEventArgs>? SplitChanged;

    /// <summary>Gets or sets whether the first pane is left of or above the second pane.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached split pane is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The split pane is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The split orientation is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets the leading pane's border-box request in cells or percentage.</summary>
    /// <remarks>The request excludes the divider and the leading pane's margin.</remarks>
    /// <exception cref="ArgumentException">The value is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached split pane is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The split pane is disposed.</exception>
    public Length FirstPaneLength
    {
        get;
        set
        {
            ValidateFirstPaneLength(value);
            var previous = field;

            if (!SetTransitionProperty(
                    ref field,
                    value,
                    InvalidationImpact.Measure,
                    _splitTransitions,
                    out var transition))
            {
                return;
            }

            transition.PublishCurrent(
                SplitChanged,
                this,
                new SplitChangedEventArgs(previous, value));
            transition.ThrowIfFailed();
        }
    } = Length.Percent(50);

    /// <summary>Gets or sets whether divider keyboard and pointer interaction is enabled.</summary>
    /// <exception cref="InvalidOperationException">The attached split pane is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The split pane is disposed.</exception>
    public bool IsResizable
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    } = true;

    /// <summary>Gets or sets the non-negative arrow-key change in terminal cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached split pane is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The split pane is disposed.</exception>
    [NonNegativeValue]
    public int SmallChange
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = 1;

    /// <summary>Gets or sets the non-negative Page Up and Page Down change in terminal cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached split pane is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The split pane is disposed.</exception>
    [NonNegativeValue]
    public int LargeChange
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.None);
        }
    } = 10;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return default;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => _ = bounds;

    private static void ValidateFirstPaneLength(Length length)
    {
        if (length.Kind is not (LengthKind.Cells or LengthKind.Percent))
        {
            throw new ArgumentException(
                "The first pane length must use fixed cells or a percentage.",
                nameof(length));
        }
    }
}
