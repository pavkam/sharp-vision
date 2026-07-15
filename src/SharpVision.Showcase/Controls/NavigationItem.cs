// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;



/// <summary>Renders one stateful, focusable control-page entry in the dashboard sidebar.</summary>
internal sealed class NavigationItem: Pressable
{
    #region Construction and state

    private bool _isSelected;

    /// <summary>Initializes one page navigation entry with a stable non-negative index and label.</summary>
    /// <param name="index">The non-negative catalog position represented by the entry.</param>
    /// <param name="label">The non-empty exact page label.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty or whitespace.</exception>
    internal NavigationItem(int index, string label)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Index = index;
        Label = label;
        Content = new SharpVision.Controls.Text(label);
        Height = Length.Cells(1);
    }

    /// <summary>Raised after keyboard or primary pointer activation requests page selection.</summary>
    internal event EventHandler<ActivationEventArgs>? Invoked;

    /// <summary>Gets the stable catalog index.</summary>
    internal int Index { get; }

    /// <summary>Gets the exact control-page label.</summary>
    internal string Label { get; }

    /// <summary>Gets whether the page is the gallery's selected page.</summary>
    internal bool IsSelected => _isSelected;

    /// <summary>Commits the visual selected state without changing the page selection itself.</summary>
    /// <param name="value">Whether the entry represents the selected page.</param>
    /// <exception cref="InvalidOperationException">The attached entry is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The entry is disposed.</exception>
    internal void SetSelected(bool value) =>
        _ = SetVisualStateProperty(ref _isSelected, value, nameof(IsSelected));

    #endregion

    #region Input and rendering

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) =>
        Invoked?.Invoke(this, new ActivationEventArgs(cause));

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = Content;
        Debug.Assert(content is not null, "A navigation entry always owns its label content.");
        var desired = MeasureChild(
            content,
            new Constraint(Subtract(constraint.Width, 3), constraint.Height));
        return new Size(Add(3, Add(desired.Width, content.Margin.Horizontal)), 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var content = Content;
        Debug.Assert(content is not null, "A navigation entry always owns its label content.");
        var consumed = Math.Min(3, bounds.Width);
        ArrangeChild(
            content,
            new Rect(bounds.X + consumed, bounds.Y, bounds.Width - consumed, bounds.Height),
            ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        var marker = IsSelected || IsHovered ? "›" : "·";
        _ = canvas.Draw($" {marker} ".AsSpan(), new Point(Bounds.X, Bounds.Y), style);
    }

    /// <inheritdoc/>
    protected override bool IsSelectedState => IsSelected;

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Invoked = null;
        }
    }

    #endregion

    private static int Add(int left, int right) =>
        (int) Math.Min(int.MaxValue, (long) left + right);

    private static int? Subtract(int? value, int extent) =>
        value.HasValue ? Math.Max(0, value.Value - extent) : null;
}
