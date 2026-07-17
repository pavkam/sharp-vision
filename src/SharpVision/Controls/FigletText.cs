// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Fonts;

/// <summary>Displays cached FIGlet output through grapheme-safe semantic cells.</summary>
public sealed class FigletText: Control
{
    private string? _cachedContent;
    private FigletFont? _cachedFont;
    private FigletOptions _cachedOptions;
    private string[] _lines = [];

    #region Construction and properties

    /// <summary>Initializes an empty display using a non-null immutable font.</summary>
    /// <param name="font">The non-null parsed font.</param>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is null.</exception>
    public FigletText(FigletFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        Font = font;
    }

    /// <summary>Gets or sets the non-null Unicode source text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Content
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets the non-null immutable FIGfont.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public FigletFont Font
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    }

    /// <summary>Gets or sets direction and layout overrides.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public FigletOptions Options
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Measure);
    }

    #endregion

    #region Layout and rendering

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        EnsureOutput();
        var width = 0;

        foreach (var line in _lines)
        {
            width = Math.Max(
                width,
                Terminal.Unicode.Width.Measure(line, CellPolicy.AmbiguousWidth).Cells);
        }

        return new Size(width, _lines.Length);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        EnsureOutput();
        var bounds = ContentBounds;
        var style = ResolveTextStyle();

        for (var row = 0; row < _lines.Length && row < bounds.Height; row++)
        {
            _ = canvas.Draw(_lines[row], new Point(bounds.X, bounds.Y + row), style);
        }
    }

    #endregion

    private void EnsureOutput()
    {
        if (ReferenceEquals(_cachedContent, Content) &&
            ReferenceEquals(_cachedFont, Font) &&
            _cachedOptions == Options)
        {
            return;
        }

        _lines = Content.Length == 0
            ? []
            : Font.Render(Content, Options).Split('\n');
        _cachedContent = Content;
        _cachedFont = Font;
        _cachedOptions = Options;
    }

    private TerminalStyle ResolveTextStyle() => ResolvedStyle;
}
