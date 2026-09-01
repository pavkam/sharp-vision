// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines one passive semantic divider between command-bar items.</summary>
[PublicAPI]
public sealed class CommandBarSeparator: ControlBase, IStyled<CommandBarSeparatorStyle>
{
    private readonly StyleSlot<CommandBarSeparatorStyle> _style;

    /// <summary>Initializes a non-focusable, non-hit-testable one-cell separator.</summary>
    public CommandBarSeparator()
    {
        _style = InitializeStyle(CommandBarSeparatorStyle.Definition);
        IsFocusable = false;
        IsTabStop = false;
        IsHitTestVisible = false;
    }

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached separator is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The separator is disposed.</exception>
    public CommandBarSeparatorStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public CommandBarSeparatorStyle ActualStyle => _style.Actual;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var glyph = ActualStyle.Glyph.Value.Resolve(
            ActualStyle.Glyph.Fallback,
            CellPolicy.AmbiguousWidth);
        canvas.DrawRune(glyph, new Point(ContentBounds.X, ContentBounds.Y), ResolvedStyle, BackgroundMode.Transparent);
    }
}
