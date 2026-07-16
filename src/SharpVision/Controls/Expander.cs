// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Owns one retained focusable header that expands or collapses caller-replaceable content.</summary>
public sealed class Expander: ContentControl, IStyleScope
{
    private readonly Text _headerText;
    private readonly OwnedControlSlot _headerSlot;

    /// <summary>Initializes an expanded Expander with one retained borderless header button.</summary>
    public Expander()
    {
        _headerText = new Text();
        HeaderPart = new Button
        {
            Content = _headerText,
            BorderThickness = default,
            HasShadow = false,
            Padding = default,
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
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
        UpdateHeader();
    }

    /// <summary>Raised after a changed expansion state and retained header text commit.</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Gets or sets the non-null single-line text rendered after the directional glyph.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control.</exception>
    /// <exception cref="InvalidOperationException">The attached Expander is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Expander is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Terminal.Unicode.Width.Measure(value).Controls > 0)
            {
                throw new ArgumentException("An Expander header cannot contain terminal controls.", nameof(value));
            }

            if (SetProperty(ref field, value, ChangeImpact.Measure))
            {
                UpdateHeader();
            }
        }
    } = string.Empty;

    /// <summary>Gets or sets whether caller content participates below the retained header.</summary>
    /// <exception cref="InvalidOperationException">The attached Expander is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The Expander is disposed.</exception>
    public bool IsExpanded
    {
        get;
        set
        {
            if (!SetProperty(ref field, value, ChangeImpact.Measure))
            {
                return;
            }

            UpdateHeader();
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    /// <summary>Gets the retained focusable header framework part for internal proof and composition.</summary>
    internal Button HeaderPart { get; }

    /// <inheritdoc/>
    public override Control? HitTest(Point point)
    {
        var contains = Bounds.Contains(point);

        return !CanHitTestSelf(point, requireContainment: false)
            ? null
            : HitTestPopup(point) ??
                (contains ? HeaderPart.HitTest(point) : null) ??
                (IsExpanded && contains ? Content?.HitTest(point) : null) ??
                (contains ? this : null);
    }

    /// <inheritdoc/>
    internal override int NavigationCount => IsExpanded && Content is not null ? 2 : 1;

    /// <inheritdoc/>
    internal override Control NavigationAt(int index) => index switch
    {
        0 => HeaderPart,
        1 when IsExpanded && Content is not null => Content,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "The navigation index is outside the Expander."),
    };

    /// <inheritdoc/>
    internal override Control? HitTestPopupCore(Point point) =>
        HeaderPart.HitTestPopupBranch(point, OwnedControlLayer.Normal) ??
        (IsExpanded ? Content?.HitTestPopupBranch(point, OwnedControlLayer.Normal) : null);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var header = MeasureChild(HeaderPart, new Constraint(constraint.Width, height: 1));
        var content = Content;

        if (!IsExpanded || content is null || content.Visibility == Visibility.Collapsed)
        {
            return header;
        }

        var desired = MeasureChild(
            content,
            new Constraint(constraint.Width, Subtract(constraint.Height, header.Height)));
        return new Size(
            Math.Max(header.Width, Add(desired.Width, content.Margin.Horizontal)),
            Add(header.Height, Add(desired.Height, content.Margin.Vertical)));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var headerHeight = Math.Min(1, bounds.Height);
        ArrangeChild(
            HeaderPart,
            new Rect(bounds.X, bounds.Y, bounds.Width, headerHeight),
            ResolvedAxes.Both);

        if (Content is not { } content)
        {
            return;
        }

        var slot = IsExpanded
            ? new Rect(bounds.X, Add(bounds.Y, headerHeight), bounds.Width, Math.Max(0, bounds.Height - headerHeight))
            : default;
        ArrangeChild(content, slot, ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        if (HeaderPart.RendersInNormalLayer)
        {
            HeaderPart.Render(canvas);
        }

        if (IsExpanded && Content is { RendersInNormalLayer: true } content)
        {
            content.Render(canvas);
        }
    }

    /// <inheritdoc/>
    internal override void RenderOwnedPopupDescendants(TerminalCanvas canvas)
    {
        HeaderPart.RenderPopupBranch(canvas, OwnedControlLayer.Normal);

        if (IsExpanded && Content is { } content)
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
            ExpandedChanged = null;
        }
    }

    private void OnHeaderClick(object? sender, ActivationEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        IsExpanded = !IsExpanded;
    }

    private void UpdateHeader()
    {
        var glyph = IsExpanded ? "▼" : "▶";
        _headerText.Content = Header.Length == 0 ? glyph : $"{glyph} {Header}";
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(right >= 0, "Expander layout adds non-negative extents.");
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int? Subtract(int? value, int amount)
    {
        Debug.Assert(amount >= 0, "Expander constraints subtract non-negative header height.");
        return value.HasValue ? Math.Max(0, value.Value - amount) : null;
    }
}
