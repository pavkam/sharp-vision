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

    /// <summary>Gets the retained logical divider rectangle before viewport clipping.</summary>
    /// <remarks>This internal seam proves layout geometry and feeds later divider behavior without a synthetic child.</remarks>
    internal Rect LogicalDividerBounds { get; private set; }

    /// <summary>Gets the retained divider rectangle clipped to the committed content viewport.</summary>
    internal Rect VisibleDividerBounds => LogicalDividerBounds == default
        ? default
        : LogicalDividerBounds.Intersect(ViewportBounds);

    /// <summary>Gets the smallest jointly feasible leading-pane border-box extent from the latest arrangement.</summary>
    internal int MinimumFirstPaneExtent { get; private set; }

    /// <summary>Gets the largest jointly feasible leading-pane border-box extent from the latest arrangement.</summary>
    internal int MaximumFirstPaneExtent { get; private set; }

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var count = GetParticipants(out var first, out var second);

        if (count == 0)
        {
            return default;
        }

        var percentageBase = new Constraint(
            ScrollMeasureViewport.Width ?? constraint.Width,
            ScrollMeasureViewport.Height ?? constraint.Height);

        if (count == 1)
        {
            var participant = first!;
            var desired = MeasureSingleParticipant(participant, constraint, percentageBase);
            return new Size(
                desired.Width.Add(participant.Margin.Horizontal),
                desired.Height.Add(participant.Margin.Vertical));
        }

        var primaryConstraint = Primary(constraint);
        var dividerExtent = primaryConstraint.HasValue ? Math.Min(1, primaryConstraint.Value) : 1;
        int? available = ScrollsPrimary()
            ? null
            : primaryConstraint.HasValue ? Math.Max(0, primaryConstraint.Value - dividerExtent) : null;
        var primaryPercentBase = ScrollsPrimary()
            ? Primary(percentageBase).Subtract(dividerExtent)
            : available;
        var firstDesired = MeasureParticipantIntrinsic(first!, constraint, percentageBase, primaryPercentBase);
        var secondDesired = MeasureParticipantIntrinsic(second!, constraint, percentageBase, primaryPercentBase);
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        ResolveAllocation(
            first!,
            second!,
            firstDesired,
            secondDesired,
            available,
            primaryPercentBase,
            extents,
            margins,
            out _,
            out _);

        firstDesired = MeasureParticipantInSlot(
            first!,
            extents[0].Add(margins[0]),
            constraint,
            percentageBase,
            primaryPercentBase);
        secondDesired = MeasureParticipantInSlot(
            second!,
            extents[1].Add(margins[1]),
            constraint,
            percentageBase,
            primaryPercentBase);

        var primary = extents[0]
            .Add(margins[0])
            .Add(dividerExtent)
            .Add(extents[1])
            .Add(margins[1]);
        var cross = Math.Max(
            Cross(firstDesired).Add(CrossMargin(first!)),
            Cross(secondDesired).Add(CrossMargin(second!)));

        return OrientedSize(primary, cross);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var count = GetParticipants(out var first, out var second);

        LogicalDividerBounds = default;
        MinimumFirstPaneExtent = 0;
        MaximumFirstPaneExtent = 0;

        if (count == 0)
        {
            return;
        }

        if (count == 1)
        {
            ArrangeParticipant(first!, bounds, PrimaryContainingBase(bounds), CrossContainingBase(bounds));
            return;
        }

        var primaryBounds = Primary(bounds);
        var dividerExtent = Math.Min(1, primaryBounds);
        int? available = ScrollsPrimary() ? null : Math.Max(0, primaryBounds - dividerExtent);
        var primaryPercentBase = ScrollsPrimary()
            ? Math.Max(0, Primary(Viewport) - Math.Min(1, Primary(Viewport)))
            : available;
        Span<int> extents = stackalloc int[2];
        Span<int> margins = stackalloc int[2];

        ResolveAllocation(
            first!,
            second!,
            first!.DesiredSize,
            second!.DesiredSize,
            available,
            primaryPercentBase,
            extents,
            margins,
            out var minimum,
            out var maximum);

        var firstOuterExtent = extents[0].Add(margins[0]);
        var secondOuterExtent = extents[1].Add(margins[1]);
        var crossContainingBase = CrossContainingBase(bounds);
        var percentageBase = Orientation == Orientation.Horizontal
            ? new Constraint(primaryPercentBase, crossContainingBase)
            : new Constraint(crossContainingBase, primaryPercentBase);
        var constraint = new Constraint(bounds.Width, bounds.Height);

        // Arrange can receive a different slot from the preceding measure. Refresh both panes at
        // their final outer tracks so width-dependent content and nested scroll extents describe
        // the geometry that will actually be arranged, while leaving this resolved allocation fixed.
        _ = MeasureParticipantInSlot(
            first!,
            firstOuterExtent,
            constraint,
            percentageBase,
            primaryPercentBase);
        _ = MeasureParticipantInSlot(
            second!,
            secondOuterExtent,
            constraint,
            percentageBase,
            primaryPercentBase);

        MinimumFirstPaneExtent = minimum;
        MaximumFirstPaneExtent = maximum;

        var dividerOrigin = PrimaryOrigin(bounds).Add(firstOuterExtent);
        var secondOrigin = dividerOrigin.Add(dividerExtent);
        var firstSlot = OrientedRect(bounds, PrimaryOrigin(bounds), firstOuterExtent);
        var secondSlot = OrientedRect(bounds, secondOrigin, secondOuterExtent);

        if (dividerExtent > 0 && Cross(bounds) > 0)
        {
            LogicalDividerBounds = Orientation == Orientation.Horizontal
                ? new Rect(dividerOrigin, bounds.Y, dividerExtent, bounds.Height)
                : new Rect(bounds.X, dividerOrigin, bounds.Width, dividerExtent);
        }

        ArrangeParticipant(first!, firstSlot, primaryPercentBase, crossContainingBase);
        ArrangeParticipant(second!, secondSlot, primaryPercentBase, crossContainingBase);
    }

    private void ArrangeParticipant(
        ControlBase child,
        Rect slot,
        int? primaryContainingBase,
        int? crossContainingBase)
    {
        var widthBase = Orientation == Orientation.Horizontal ? primaryContainingBase : crossContainingBase;
        var heightBase = Orientation == Orientation.Horizontal ? crossContainingBase : primaryContainingBase;

        ArrangeChild(
            child,
            slot,
            ResolvedAxes.Both,
            widthRequestBase: widthBase,
            heightRequestBase: heightBase,
            widthLimitBase: widthBase,
            heightLimitBase: heightBase);
    }

    [Pure]
    private int GetParticipants(out ControlBase? first, out ControlBase? second)
    {
        first = null;
        second = null;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            if (first is null)
            {
                first = child;
            }
            else
            {
                second = child;
                return 2;
            }
        }

        return first is null ? 0 : 1;
    }

    private Size MeasureSingleParticipant(
        ControlBase child,
        Constraint constraint,
        Constraint percentageBase) =>
        MeasureChild(
            child,
            new Constraint(
                ScrollsHorizontally() ? null : constraint.Width,
                ScrollsVertically() ? null : constraint.Height),
            ScrollsHorizontally() ? percentageBase.Width : null,
            ScrollsVertically() ? percentageBase.Height : null,
            percentageBase.Width,
            percentageBase.Height);

    private Size MeasureParticipantIntrinsic(
        ControlBase child,
        Constraint constraint,
        Constraint percentageBase,
        int? primaryPercentBase)
    {
        var crossConstraint = CrossScrolls() ? null : Cross(constraint);
        var primaryRequestBase = ScrollsPrimary() ? primaryPercentBase : null;
        var crossRequestBase = CrossScrolls() ? Cross(percentageBase) : null;
        return MeasureOriented(
            child,
            primaryConstraint: null,
            crossConstraint,
            primaryRequestBase,
            crossRequestBase,
            primaryPercentBase,
            Cross(percentageBase));
    }

    private Size MeasureParticipantInSlot(
        ControlBase child,
        int primaryConstraint,
        Constraint constraint,
        Constraint percentageBase,
        int? primaryPercentBase)
    {
        var crossConstraint = CrossScrolls() ? null : Cross(constraint);
        var primaryRequestBase = ScrollsPrimary() ? primaryPercentBase : null;
        var crossRequestBase = CrossScrolls() ? Cross(percentageBase) : null;
        return MeasureOriented(
            child,
            primaryConstraint,
            crossConstraint,
            primaryRequestBase,
            crossRequestBase,
            primaryPercentBase,
            Cross(percentageBase));
    }

    private Size MeasureOriented(
        ControlBase child,
        int? primaryConstraint,
        int? crossConstraint,
        int? primaryRequestBase,
        int? crossRequestBase,
        int? primaryLimitBase,
        int? crossLimitBase) =>
        Orientation == Orientation.Horizontal
            ? MeasureChild(
                child,
                new Constraint(primaryConstraint, crossConstraint),
                primaryRequestBase,
                crossRequestBase,
                primaryLimitBase,
                crossLimitBase)
            : MeasureChild(
                child,
                new Constraint(crossConstraint, primaryConstraint),
                crossRequestBase,
                primaryRequestBase,
                crossLimitBase,
                primaryLimitBase);

    private void ResolveAllocation(
        ControlBase first,
        ControlBase second,
        Size firstDesired,
        Size secondDesired,
        int? available,
        int? percentBase,
        Span<int> extents,
        Span<int> margins,
        out int minimum,
        out int maximum)
    {
        ResolvePrimaryLimits(first, percentBase, out var firstMinimum, out var firstMaximum);
        ResolvePrimaryLimits(second, percentBase, out var secondMinimum, out var secondMaximum);
        SplitPaneLayout.Resolve(
            FirstPaneLength,
            Primary(firstDesired),
            Primary(secondDesired),
            firstMinimum,
            firstMaximum,
            secondMinimum,
            secondMaximum,
            PrimaryMargin(first),
            PrimaryMargin(second),
            available,
            percentBase,
            extents,
            margins,
            out minimum,
            out maximum);
    }

    private void ResolvePrimaryLimits(ControlBase child, int? containing, out int minimum, out int maximum)
    {
        if (Orientation == Orientation.Horizontal)
        {
            child.ResolveWidthLimits(containing, out minimum, out maximum);
        }
        else
        {
            child.ResolveHeightLimits(containing, out minimum, out maximum);
        }
    }

    [Pure]
    private bool ScrollsHorizontally() => AutoScroll && (ScrollBars & ScrollBars.Horizontal) != 0;

    [Pure]
    private bool ScrollsVertically() => AutoScroll && (ScrollBars & ScrollBars.Vertical) != 0;

    [Pure]
    private bool ScrollsPrimary() =>
        Orientation == Orientation.Horizontal ? ScrollsHorizontally() : ScrollsVertically();

    [Pure]
    private bool CrossScrolls() =>
        Orientation == Orientation.Horizontal ? ScrollsVertically() : ScrollsHorizontally();

    [Pure]
    private int? Primary(Constraint value) =>
        Orientation == Orientation.Horizontal ? value.Width : value.Height;

    [Pure]
    private int? Cross(Constraint value) =>
        Orientation == Orientation.Horizontal ? value.Height : value.Width;

    [Pure]
    private int Primary(Size value) =>
        Orientation == Orientation.Horizontal ? value.Width : value.Height;

    [Pure]
    private int Cross(Size value) =>
        Orientation == Orientation.Horizontal ? value.Height : value.Width;

    [Pure]
    private int Primary(Rect value) =>
        Orientation == Orientation.Horizontal ? value.Width : value.Height;

    [Pure]
    private int Cross(Rect value) =>
        Orientation == Orientation.Horizontal ? value.Height : value.Width;

    [Pure]
    private int PrimaryOrigin(Rect value) =>
        Orientation == Orientation.Horizontal ? value.X : value.Y;

    [Pure]
    private int PrimaryMargin(ControlBase child) =>
        Orientation == Orientation.Horizontal ? child.Margin.Horizontal : child.Margin.Vertical;

    [Pure]
    private int CrossMargin(ControlBase child) =>
        Orientation == Orientation.Horizontal ? child.Margin.Vertical : child.Margin.Horizontal;

    [Pure]
    private int PrimaryContainingBase(Rect bounds) =>
        ScrollsPrimary() ? Primary(Viewport) : Primary(bounds);

    [Pure]
    private int CrossContainingBase(Rect bounds) =>
        CrossScrolls() ? Cross(Viewport) : Cross(bounds);

    [Pure]
    private Size OrientedSize(int primary, int cross) =>
        Orientation == Orientation.Horizontal ? new Size(primary, cross) : new Size(cross, primary);

    [Pure]
    private Rect OrientedRect(Rect bounds, int primaryOrigin, int primaryExtent) =>
        Orientation == Orientation.Horizontal
            ? new Rect(primaryOrigin, bounds.Y, primaryExtent, bounds.Height)
            : new Rect(bounds.X, primaryOrigin, bounds.Width, primaryExtent);

    #endregion

    #region Validation

    private static void ValidateFirstPaneLength(Length length)
    {
        if (length.Kind is not (LengthKind.Cells or LengthKind.Percent))
        {
            throw new ArgumentException(
                "The first pane length must use fixed cells or a percentage.",
                nameof(length));
        }
    }

    #endregion
}
