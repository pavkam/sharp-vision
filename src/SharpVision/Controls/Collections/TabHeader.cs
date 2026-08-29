// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Provides one retained pressable header for a semantic tab page.</summary>
internal sealed class TabHeader: InputBase
{
    private const int _headerRowHeight = 1;
    private const int _headerHorizontalPadding = 1;
    private bool _isSelected;

    /// <summary>Initializes one non-focusable header with validated text.</summary>
    /// <param name="header">The non-null header text without terminal controls.</param>
    public TabHeader(string header)
    {
        EnablePressActivation();
        Text = header;
        Height = Length.Cells(_headerRowHeight);
        IsFocusable = false;
        IsTabStop = false;
    }

    /// <summary>Raised after primary pointer activation completes inside this header.</summary>
    public event EventHandler<ActivationEventArgs>? Activated;

    /// <summary>Gets or sets the validated label rendered with one cell of horizontal padding.</summary>
    public override string Text
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentException.ThrowIfContainsControls(value, nameof(value), "A tab header cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override string? AccessKeyText => Text;

    /// <summary>Commits whether this retained header represents the selected page.</summary>
    /// <param name="value">Whether selected appearance is active.</param>
    public void CommitSelection(bool value) =>
        _ = SetVisualStateProperty(ref _isSelected, value, nameof(IsSelectedState));

    /// <inheritdoc/>
    protected override bool IsSelectedState => _isSelected;

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).GetInteractiveRowStyleSet().ToAppearanceStates();

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) =>
        Activated?.Invoke(this, new ActivationEventArgs(cause));

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var cells = Text.Measure(CellPolicy.AmbiguousWidth, UseMnemonic);
        return new Size(
            (int) Math.Min(int.MaxValue, ((long) _headerHorizontalPadding * 2) + cells),
            _headerRowHeight);
    }

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
        var cells = Text.Draw(
            canvas,
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
                if (!tabs.Focus() || !ReferenceEquals(FindTabControl(), tabs))
                {
                    return false;
                }

                Activate(ActivationCause.Keyboard);
                return true;
            }
        }

        return false;
    }

    private TabControl? FindTabControl()
    {
        for (var current = Parent; current is not null; current = current.Parent)
        {
            if (current is TabControl tabs)
            {
                return tabs;
            }
        }

        return null;
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

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }
}
