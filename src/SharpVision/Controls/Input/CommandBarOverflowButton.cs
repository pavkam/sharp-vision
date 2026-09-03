// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Renders and routes the stable private overflow trigger retained by a command bar.</summary>
internal sealed class CommandBarOverflowButton: InputBase
{
    private readonly CommandBar _owner;

    /// <summary>Initializes one non-tab-stop owner-focused trigger.</summary>
    /// <param name="owner">The non-null command bar that owns activation and presentation.</param>
    internal CommandBarOverflowButton(CommandBar owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        EnablePressActivation();
        IsFocusable = false;
        IsTabStop = false;
        Width = Length.Cells(1);
        Height = Length.Cells(1);
    }

    /// <inheritdoc/>
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        BarAppearance.Rebase((theme ?? ThemeCatalog.Dark).GetInteractiveControlStyleSet());

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => _ = bounds;

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (ContentBounds.Width == 0 || ContentBounds.Height == 0)
        {
            return;
        }

        var style = _owner.ActualStyle;
        var foreground = _owner.Style is null && GetAppearanceState() != VisualState.Normal
            ? ResolvedStyle.Foreground
            : _owner.ResolveOverflowColor(style.OverflowColor);
        var resolved = style.OverflowGlyph.Value.Resolve(
            style.OverflowGlyph.Fallback,
            CellPolicy.AmbiguousWidth);
        var glyphStyle = new TerminalStyle(
            foreground,
            ResolvedStyle.Background,
            ResolvedStyle.Attributes,
            ResolvedStyle.Hyperlink,
            ResolvedStyle.Underline,
            ResolvedStyle.UnderlineColor);
        _ = canvas.Draw(
            resolved.ToString().AsSpan(),
            new Point(ContentBounds.X, ContentBounds.Y),
            glyphStyle,
            background: BackgroundMode.Transparent);
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) => _owner.ToggleOverflow(cause);

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        _owner.FocusFromOverflowPointer(eventArgs);
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    /// <summary>Cancels any armed press when layout removes or repositions the trigger.</summary>
    internal void CancelPress() => SetPressed(false);

    /// <summary>Commits owner-driven roving appearance without becoming independently focusable.</summary>
    /// <param name="value">Whether the owner currently selects this trigger.</param>
    internal void CommitSelection(bool value) => SetSelectedState(value);
}
