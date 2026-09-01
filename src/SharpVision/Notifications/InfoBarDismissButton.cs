// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

/// <summary>Owns the retained keyboard, pointer, focus, and pressed state for an InfoBar dismiss action.</summary>
internal sealed class InfoBarDismissButton: ControlBase
{
    private readonly InfoBar _owner;
    private readonly PressBehavior _interaction;

    /// <summary>Initializes a dismiss part bound to one non-null owner.</summary>
    /// <param name="owner">The InfoBar that owns this part for its lifetime.</param>
    internal InfoBarDismissButton(InfoBar owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        _interaction = new PressBehavior(
            () => Bounds,
            IsAvailable,
            () => FocusOwner is null || IsFocused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            _ => _owner.Dismiss(),
            () => Capabilities.KeyReleaseEvents.Authoritative);
        RegisterLifecycleParticipant(_interaction);
        IsFocusable = true;
        IsTabStop = true;
    }

    /// <summary>Cancels transient press and capture state before owner availability changes publish.</summary>
    internal void CancelInteraction()
    {
        _interaction.Unavailable();

        if (HasPointerCapture)
        {
            ReleasePointerCapture();
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => _ = bounds;

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => new()
    {
        SkipBodyFill = true,
        SkipBorder = true,
        SkipShadow = true
    };

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (!IsAvailable())
        {
            return;
        }

        var style = _owner.ActualStyle;
        var cellStyle = ResolvedStyle.WithForeground(ResolveColor(style.DismissColor));
        canvas.DrawRune(
            ResolveControlGlyph(style.DismissGlyph),
            new Point(Bounds.X, Bounds.Y),
            cellStyle,
            BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        base.OnEvent(eventArgs);
        _interaction.Handle(eventArgs);
    }

    private bool IsAvailable() =>
        !IsDisposed && _owner.IsOpen && _owner.IsDismissible &&
        EffectiveIsEnabled && EffectiveIsVisible && Bounds.Width > 0 && Bounds.Height > 0;
}
