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
    internal NavigationItem(int index, string label) : base(capacity: 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Index = index;
        Label = label;
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
    internal void SetSelected(bool value)
    {
        if (!SetProperty(ref _isSelected, value, ChangeImpact.None, nameof(IsSelected)))
        {
            return;
        }

        InvalidateVisualState();
    }

    #endregion

    #region Input and rendering

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) =>
        Invoked?.Invoke(this, new ActivationEventArgs(cause));

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        var marker = IsSelected || IsHovered ? "›" : "·";
        _ = canvas.Draw($" {marker} {Label}".AsSpan(), new Point(Bounds.X, Bounds.Y), style);
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
}
