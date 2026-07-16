// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using Label = Text;

/// <summary>Owns one retained pressable tab header and one caller-replaceable page content control.</summary>
public sealed class TabItem: ContentControl
{
    private readonly Label _headerText;
    private readonly OwnedControlSlot _headerSlot;
    private Rect _contentBounds;
    private Rect _headerBounds;

    /// <summary>Initializes an unselected page with one retained borderless header button.</summary>
    public TabItem()
    {
        _headerText = new Label();
        HeaderPart = new Button
        {
            Content = _headerText,
            BorderThickness = default,
            HasShadow = false,
            Padding = new Thickness(1, 0),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        HeaderPart.Click += OnHeaderClick;
        _headerSlot = RegisterOwnedSlot(
            new OwnedControlOptions(
                OwnedControlRole.FrameworkPart,
                OwnedControlLayer.Normal,
                participatesInHitTesting: true,
                participatesInNavigation: true,
                partKey: "header",
                ChangeImpact.Measure),
            capacity: 1);
        _headerSlot.Add(HeaderPart);
    }

    /// <summary>Gets or sets the non-null single-line text rendered in the retained header.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Terminal.Unicode.Width.Measure(value).Controls > 0)
            {
                throw new ArgumentException("A tab header cannot contain terminal controls.", nameof(value));
            }

            if (SetProperty(ref field, value, ChangeImpact.Measure))
            {
                _headerText.Content = value;
            }
        }
    } = string.Empty;

    /// <summary>Gets whether the owning TabControl has committed this page as selected.</summary>
    public bool IsSelected { get; private set; }

    /// <summary>Gets the retained pressable header framework part for internal composition and proof.</summary>
    internal Button HeaderPart { get; }

    /// <summary>Raised after eligible pointer or keyboard activation of the retained header.</summary>
    internal event EventHandler<ActivationEventArgs>? HeaderActivated;

    /// <summary>Gets the measured terminal-cell width of the retained header.</summary>
    internal int HeaderWidth { get; private set; }

    /// <summary>Commits owner-controlled selected state before publication.</summary>
    /// <param name="value">Whether this page is selected.</param>
    internal void CommitSelection(bool value)
    {
        VerifyMutable();

        if (IsSelected == value)
        {
            return;
        }

        IsSelected = value;
        HeaderPart.SetSelectedState(value);
        NotifyPropertyChanged(nameof(IsSelected), ChangeImpact.Measure);
    }

    /// <summary>Supplies the committed header and selected-content rectangles for one arrange pass.</summary>
    /// <param name="headerBounds">The absolute header rectangle, which may be clipped by the strip.</param>
    /// <param name="contentBounds">The absolute selected-content rectangle below the separator.</param>
    internal void SetPresentationBounds(Rect headerBounds, Rect contentBounds)
    {
        if (_headerBounds == headerBounds && _contentBounds == contentBounds)
        {
            return;
        }

        _headerBounds = headerBounds;
        _contentBounds = contentBounds;
        Invalidate(Invalidation.Arrange);
    }

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        return !CanHitTestSelf(point, requireContainment: false) || !Bounds.Contains(point)
            ? null
            : HitTestPopup(point) ??
                HeaderPart.HitTest(point) ??
                (IsSelected ? Content?.HitTest(point) : null);
    }

    /// <inheritdoc/>
    internal override int NavigationCount => IsSelected && Content is not null ? 2 : 1;

    /// <inheritdoc/>
    internal override Control NavigationAt(int index) => index switch
    {
        0 => HeaderPart,
        1 when IsSelected && Content is not null => Content,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "The navigation index is outside the TabItem."),
    };

    /// <inheritdoc/>
    internal override Control? HitTestPopupCore(Point point) =>
        HeaderPart.HitTestPopupBranch(point, OwnedControlLayer.Normal) ??
        (IsSelected ? Content?.HitTestPopupBranch(point, OwnedControlLayer.Normal) : null);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var header = MeasureChild(HeaderPart, new Constraint(width: null, height: 1));
        HeaderWidth = header.Width;
        var content = Content;

        if (!IsSelected || content is null || content.Visibility == Visibility.Collapsed)
        {
            return new Size(HeaderWidth, Math.Min(2, constraint.Height ?? 2));
        }

        var desired = MeasureChild(content, new Constraint(constraint.Width, Subtract(constraint.Height, 2)));
        return new Size(
            Math.Max(HeaderWidth, Add(desired.Width, content.Margin.Horizontal)),
            Add(2, Add(desired.Height, content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        _ = bounds;
        ArrangeChild(HeaderPart, _headerBounds, ResolvedAxes.Both);

        if (Content is { } content)
        {
            ArrangeChild(content, IsSelected ? _contentBounds : default, ResolvedAxes.Both);
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas) =>
        _ = canvas.Bounds;

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        if (HeaderPart.RendersInNormalLayer)
        {
            HeaderPart.Render(canvas);
        }

        if (IsSelected && Content is { RendersInNormalLayer: true } content)
        {
            content.Render(canvas);
        }
    }

    /// <inheritdoc/>
    internal override void RenderOwnedPopupDescendants(TerminalCanvas canvas)
    {
        HeaderPart.RenderPopupBranch(canvas, OwnedControlLayer.Normal);

        if (IsSelected && Content is { } content)
        {
            content.RenderPopupBranch(canvas, OwnedControlLayer.Normal);
        }
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            HeaderPart.Click -= OnHeaderClick;
            HeaderActivated = null;
        }
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "TabItem layout adds non-negative extents.");
        Debug.Assert(right >= 0, "TabItem layout adds non-negative extents.");
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int? Subtract(int? value, int amount) =>
        value.HasValue ? Math.Max(0, value.Value - amount) : null;

    private void OnHeaderClick(object? sender, ActivationEventArgs eventArgs)
    {
        _ = sender;
        HeaderActivated?.Invoke(this, eventArgs);
    }
}
