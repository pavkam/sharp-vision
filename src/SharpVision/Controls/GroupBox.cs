// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Frames one caller-replaceable content control beneath an optional title in the top border edge.</summary>
public sealed class GroupBox: ContentControl, IStyleScope
{
    static GroupBox()
    {
        _ = BorderThicknessProperty.RegisterClassDefault<GroupBox>(new Thickness(1));
        _ = BorderGlyphsProperty.RegisterClassDefault<GroupBox>(Glyphs.Rounded);
    }

    /// <summary>Initializes an empty rounded GroupBox with no header.</summary>
    public GroupBox()
    {
    }

    /// <summary>Gets or sets the non-null single-line header rendered in the top border edge.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control.</exception>
    /// <exception cref="InvalidOperationException">The attached GroupBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The GroupBox is disposed.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Terminal.Unicode.Width.Measure(value).Controls > 0)
            {
                throw new ArgumentException("A GroupBox header cannot contain terminal controls.", nameof(value));
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets the validated physical glyph family used by the frame.</summary>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached GroupBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The GroupBox is disposed.</exception>
    public Glyphs Glyphs
    {
        get => BorderGlyphs;
        set => BorderGlyphs = value;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var content = base.MeasureOverride(constraint);
        var header = Header.Length == 0
            ? 0
            : Add(Terminal.Unicode.Width.Measure(Header).Cells, 2);
        return new Size(Math.Max(content.Width, header), content.Height);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        RenderChrome(canvas);

        if (Header.Length == 0 || Bounds.Width <= 2 || Bounds.Height == 0)
        {
            return;
        }

        var state = GetVisualState();
        var opaque = ControlAppearance.HasOpaqueFill(this, state);
        var style = ControlAppearance.ResolveBorderStyle(this, state);
        var title = canvas.Clip(new Rect(Bounds.X + 1, Bounds.Y, Bounds.Width - 2, 1));
        var text = $" {Header} ";
        _ = title.Draw(
            text.AsSpan(),
            new Point(Bounds.X + 1, Bounds.Y),
            style,
            background: opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent);
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0 && right >= 0, "GroupBox measurement adds non-negative cell extents.");
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }
}
