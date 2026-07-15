// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;


using SharpVision.Terminal.Rendering;
using SharpVision.Text;

using TextLayout = SharpVision.Text.Layout;

/// <summary>Displays cached grapheme-safe text through semantic terminal cells.</summary>
public sealed class Text: Control
{
    private const string _ellipsis = "…";
    private string? _cachedContent;
    private int _cachedWidth;
    private Wrapping _cachedWrapping;
    private Trimming _cachedTrimming;
    private Alignment _cachedAlignment;
    private Ambiguous _cachedAmbiguous;
    private bool _hasAmbiguousWidth;
    private Line[] _lines = [];
    private int _lineCount;
    private bool _layoutValid;

    /// <summary>Initializes empty text with documented formatting defaults.</summary>
    public Text()
    {
    }

    /// <summary>Initializes text with non-null immutable-at-render content.</summary>
    /// <param name="content">The non-null UTF-16 content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public Text(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    /// <summary>Gets or sets the non-null UTF-16 content.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string Content
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Set(ref field, value, Invalidation.Measure))
            {
                _layoutValid = false;
            }
        }
    } = string.Empty;

    /// <summary>Gets or sets the logical-line wrapping policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Wrapping Wrapping
    {
        get;
        set
        {
            Validate(value);

            if (Set(ref field, value, Invalidation.Measure))
            {
                _layoutValid = false;
            }
        }
    }

    /// <summary>Gets or sets the unwrapped overflow policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Trimming Trimming
    {
        get;
        set
        {
            Validate(value);

            if (Set(ref field, value, Invalidation.Measure))
            {
                _layoutValid = false;
            }
        }
    }

    /// <summary>Gets or sets horizontal placement within each formatted line.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Alignment TextAlignment
    {
        get;
        set
        {
            Validate(value);
            _ = Set(ref field, value, Invalidation.Arrange);
        }
    }

    /// <summary>Gets or sets the East Asian Ambiguous cell-width policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Ambiguous AmbiguousWidth
    {
        get => _hasAmbiguousWidth ? AmbiguousWidthValue : CellPolicy.AmbiguousWidth;
        set
        {
            Validate(value);
            VerifyMutable();

            if (_hasAmbiguousWidth && AmbiguousWidthValue == value)
            {
                return;
            }

            AmbiguousWidthValue = value;
            _hasAmbiguousWidth = true;
            _layoutValid = false;
            NotifyChanged(nameof(AmbiguousWidth), Invalidation.Measure);
        }
    }

    private Ambiguous AmbiguousWidthValue { get; set; }

    /// <summary>Gets the committed line metrics until the next successful layout.</summary>
    public ReadOnlyMemory<Line> Lines => _lines.AsMemory(0, _lineCount);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureLayout(constraint.Width ?? int.MaxValue);
        var width = 0;

        foreach (var line in Lines.Span)
        {
            width = Math.Max(width, line.Cells);
        }

        return new Size(width, _lineCount);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) => EnsureLayout(bounds.Width);

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;
        EnsureLayout(bounds.Width);
        var style = ResolveStyle();
        var lines = Lines.Span;

        for (var index = 0; index < lines.Length && index < bounds.Height; index++)
        {
            var line = lines[index];
            var origin = new Point(bounds.X + line.Leading, bounds.Y + index);
            var result = canvas.Draw(
                Content.AsSpan(line.Offset, line.Length),
                origin,
                style,
                background: ResolveBackgroundMode());

            if (line.HasEllipsis)
            {
                _ = canvas.Draw(_ellipsis, result.Final, style, background: ResolveBackgroundMode());
            }
        }
    }

    private void EnsureLayout(int width)
    {
        Debug.Assert(width >= 0, "Control layout provides a non-negative content width.");

        if (!_layoutValid ||
            !ReferenceEquals(_cachedContent, Content) ||
            _cachedWidth != width ||
            _cachedWrapping != Wrapping ||
            _cachedTrimming != Trimming ||
            _cachedAmbiguous != AmbiguousWidth)
        {
            Format(width);
            return;
        }

        if (_cachedAlignment != TextAlignment)
        {
            Align(width);
        }
    }

    private void Format(int width)
    {
        var required = TextLayout.Format(
            Content,
            width,
            Wrapping,
            Trimming,
            TextAlignment,
            AmbiguousWidth,
            _lines);

        if (required > _lines.Length)
        {
            Array.Resize(ref _lines, required);
            _ = TextLayout.Format(
                Content,
                width,
                Wrapping,
                Trimming,
                TextAlignment,
                AmbiguousWidth,
                _lines);
        }

        _lineCount = required;
        _cachedContent = Content;
        _cachedWidth = width;
        _cachedWrapping = Wrapping;
        _cachedTrimming = Trimming;
        _cachedAlignment = TextAlignment;
        _cachedAmbiguous = AmbiguousWidth;
        _layoutValid = true;
    }

    private void Align(int width)
    {
        for (var index = 0; index < _lineCount; index++)
        {
            var line = _lines[index];
            var remaining = Math.Max(0, width - line.Cells);
            var leading = TextAlignment switch
            {
                Alignment.Start => 0,
                Alignment.Center => remaining / 2,
                Alignment.End => remaining,
                _ => throw new UnreachableException(),
            };
            _lines[index] = new Line(
                line.Offset,
                line.Length,
                line.Cells,
                leading,
                line.HasEllipsis);
        }

        _cachedAlignment = TextAlignment;
    }

    private TerminalStyle ResolveStyle() => ResolvedStyle;

    private BackgroundMode ResolveBackgroundMode() => ControlAppearance.HasOpaqueFill(this, GetVisualState()) ? BackgroundMode.Opaque : BackgroundMode.Transparent;

    private static void Validate<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
        }
    }
}
