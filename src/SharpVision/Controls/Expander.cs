// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Displays a collapsible section with a focusable header toggle and optional content.</summary>
public sealed class Expander: ContentControl
{
    private readonly PressBehavior _interaction;

    /// <summary>Initializes an expanded section with an empty header.</summary>
    public Expander()
    {
        _interaction = new PressBehavior(
            () => new Rect(Bounds.X, Bounds.Y, Bounds.Width, Math.Min(1, Bounds.Height)),
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused, RequestFocus, CapturePointer,
            () => HasPointerCapture, ReleasePointerCapture, SetPressed,
            _ => IsExpanded = !IsExpanded);
        Focusable = true;
        TabStop = true;
    }

    /// <summary>Raised after the expanded state changes.</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Gets or sets the non-null header label.</summary>
    public string Header
    {
        get;
        set { ArgumentNullException.ThrowIfNull(value); _ = SetProperty(ref field, value, ChangeImpact.Measure); }
    } = string.Empty;

    /// <summary>Gets or sets whether the content is visible.</summary>
    public bool IsExpanded
    {
        get;
        set { if (SetProperty(ref field, value, ChangeImpact.Measure)) { ExpandedChanged?.Invoke(this, EventArgs.Empty); } }
    } = true;

    /// <inheritdoc/>

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var hw = (int) Math.Min(int.MaxValue, 2L + Terminal.Unicode.Width.Measure(Header).Cells);
        if (!IsExpanded || Content is not { } child) { return new Size(hw, 1); }
        var d = MeasureChild(child, new Constraint(constraint.Width, constraint.Height.HasValue ? Math.Max(0, constraint.Height.Value - 1) : null));
        var cw = child.Visibility == Visibility.Collapsed ? 0 : (int) Math.Min(int.MaxValue, (long) d.Width + child.Margin.Horizontal);
        var ch = child.Visibility == Visibility.Collapsed ? 0 : (int) Math.Min(int.MaxValue, (long) d.Height + child.Margin.Vertical);
        return new Size(Math.Max(hw, cw), (int) Math.Min(int.MaxValue, 1L + ch));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (IsExpanded && Content is { } c && bounds.Height > 1) { ArrangeChild(c, new Rect(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height - 1), ResolvedAxes.Both); }
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0) { return; }
        var s = ResolvedStyle;
        _ = canvas.Draw((IsExpanded ? "▼ " : "▶ ").AsSpan(), new Point(Bounds.X, Bounds.Y), s, background: BackgroundMode.Transparent);
        if (Header.Length > 0 && Bounds.Width > 2) { _ = canvas.Clip(new Rect(Bounds.X + 2, Bounds.Y, Bounds.Width - 2, 1)).Draw(Header.AsSpan(), new Point(Bounds.X + 2, Bounds.Y), s, background: BackgroundMode.Transparent); }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs) { base.OnEvent(eventArgs); _interaction.Handle(eventArgs); }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused) { base.OnFocusChanged(focused); _interaction.FocusChanged(focused); }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason) { base.OnLostPointerCapture(reason); _interaction.CaptureLost(); }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason) { base.OnUnavailable(reason); _interaction.Unavailable(); if (reason == ReleaseReason.Disposed) { ExpandedChanged = null; } }
}
