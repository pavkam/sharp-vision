// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes the protected <c>ArrangeUniformRows</c> contract for behavioral tests.</summary>
/// <remarks>
/// Hosts a fixed number of rows in a plain vertically scrollable <see cref="Stack"/>, each row's
/// <see cref="ControlBase.Height"/> kept equal to whatever this probe's own uniform row height
/// last resolved to - the same shape <see cref="UiListView"/> and
/// <see cref="Table"/> give their own private hosts, just without
/// virtualization.
/// </remarks>
internal sealed class ScrollableItemsControlUniformRowsProbe: ScrollableItemsControl
{
    private readonly Stack _host;
    private int? _rowHeight;

    /// <summary>Initializes a probe with a fixed row count and a uniform row height request.</summary>
    /// <param name="rowCount">The non-negative number of generated rows.</param>
    /// <param name="rowHeightRequest">The fixed or percentage row height request.</param>
    internal ScrollableItemsControlUniformRowsProbe(int rowCount, Length rowHeightRequest)
    {
        RowHeightRequest = rowHeightRequest;
        _host = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded
        };
        InitializeScrollableItemsHost(_host);

        for (var index = 0; index < rowCount; index++)
        {
            InsertItemControl(index, new ControlText($"Row {index}"));
        }
    }

    /// <summary>Gets the uniform row height request resolved against each final viewport.</summary>
    internal Length RowHeightRequest { get; }

    /// <summary>Gets or sets the leading band height forwarded to <c>ArrangeUniformRows</c>.</summary>
    internal int LeadingBandHeight { get; set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => MeasureChild(_host, constraint);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var previousHeight = _rowHeight;
        var previousOffset = VerticalOffset;
        ArrangeChild(_host, bounds, ResolvedAxes.Both);
        ArrangeUniformRows(_host, bounds, previousHeight, previousOffset, LeadingBandHeight, _host.Spacing);
    }

    /// <inheritdoc/>
    protected override bool TryResolveUniformRowHeight(int viewportHeight)
    {
        var resolved = UniformRowHeight.Resolve(RowHeightRequest, viewportHeight);

        if (_rowHeight == resolved)
        {
            return false;
        }

        _rowHeight = resolved;

        for (var index = 0; index < ItemControlCount; index++)
        {
            GetItemControl(index).Height = Length.Cells(resolved);
        }

        return true;
    }

    /// <inheritdoc/>
    protected override int ResolvedUniformRowHeight =>
        _rowHeight ?? throw new InvalidOperationException("No uniform row height has resolved yet.");

    /// <summary>Gets the private presentation host this probe installed, so a test can inspect its
    /// realized children's <see cref="ControlBase.Bounds"/> directly - proving <c>ArrangeUniformRows</c>
    /// re-arranged them in the same pass - instead of only the semantic
    /// <see cref="ScrollableItemsControl.VerticalOffset"/> the owner exposes.</summary>
    internal Stack Host => _host;
}
