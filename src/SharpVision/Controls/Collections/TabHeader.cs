// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Provides one retained pressable header for a semantic tab page.</summary>
internal sealed class TabHeader: Pressable
{
    private const int _headerRowHeight = 1;
    private const int _headerHorizontalPadding = 1;
    private bool _isSelected;

    /// <summary>Initializes one non-focusable header with validated text.</summary>
    /// <param name="header">The non-null header text without terminal controls.</param>
    public TabHeader(string header)
    {
        Header = header;
        Height = Length.Cells(_headerRowHeight);
        Focusable = false;
        TabStop = false;
    }

    /// <summary>Raised after primary pointer activation completes inside this header.</summary>
    public event EventHandler<ActivationEventArgs>? Activated;

    /// <summary>Gets or sets the validated label rendered with one cell of horizontal padding.</summary>
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

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override string? AccessKeyText => Header;

    /// <summary>Commits whether this retained header represents the selected page.</summary>
    /// <param name="value">Whether selected appearance is active.</param>
    public void CommitSelection(bool value) =>
        _ = SetVisualStateProperty(ref _isSelected, value, nameof(IsSelectedState));

    /// <inheritdoc/>
    protected override bool IsSelectedState => _isSelected;

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) =>
        Activated?.Invoke(this, new ActivationEventArgs(cause));

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var cells = SharpVision.Input.AccessKeyText.Measure(Header, CellPolicy.AmbiguousWidth, UseMnemonic);
        return new Size(
            (int) Math.Min(int.MaxValue, ((long) _headerHorizontalPadding * 2) + cells),
            _headerRowHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => _ = bounds;

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var appearance = GetResolvedAppearance(GetAppearanceState());

        if (appearance.BackgroundMode == BackgroundMode.Opaque)
        {
            canvas.Clear(Bounds, appearance.Style);
        }

        var leading = canvas.Draw(
            " ".AsSpan(),
            new Point(Bounds.X, Bounds.Y),
            appearance.Style,
            background: BackgroundMode.Transparent);
        var cells = SharpVision.Input.AccessKeyText.Draw(
            canvas,
            Header,
            leading.Final,
            appearance.Style,
            BackgroundMode.Transparent,
            CellPolicy.AmbiguousWidth,
            UseMnemonic,
            EffectiveIsEnabled ? Theme?.Hotkey ?? Color.Default : null);
        _ = canvas.Draw(
            " ".AsSpan(),
            new Point(leading.Final.X + cells, leading.Final.Y),
            appearance.Style,
            background: BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override bool OnAccessKey(Rune key)
    {
        _ = key;

        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current is TabControl tabs)
            {
                _ = tabs.Focus();
                Activate(ActivationCause.Keyboard);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Activated = null;
        }
    }
}
