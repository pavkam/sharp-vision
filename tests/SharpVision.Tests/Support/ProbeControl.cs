// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;



using TerminalCanvas = Terminal.Rendering.Canvas;

/// <summary>Provides a recording leaf for shared control infrastructure tests.</summary>
internal sealed class ProbeControl: Control
{
    private readonly Size _intrinsic;

    /// <summary>Initializes a probe with one validated intrinsic size.</summary>
    /// <param name="intrinsic">The non-negative intrinsic content size.</param>
    internal ProbeControl(Size intrinsic = default) => _intrinsic = intrinsic;

    /// <summary>Gets constraints received by the content measure extension point.</summary>
    internal List<Constraint> MeasureConstraints { get; } = [];

    /// <summary>Gets rectangles received by the content arrange extension point.</summary>
    internal List<Rect> ArrangeBounds { get; } = [];

    /// <summary>Gets or sets work invoked from inside the next measure pass.</summary>
    internal Action<ProbeControl>? Measuring { get; set; }

    /// <summary>Gets the natural content extent captured by the base measure.</summary>
    internal Size ExposedContentExtent => ContentExtent;

    /// <summary>Gets or sets work invoked from inside the next arrange pass.</summary>
    internal Action<ProbeControl>? Arranging { get; set; }

    /// <summary>Gets or sets borrowed text drawn by the render extension point.</summary>
    internal ReadOnlyMemory<char> Content { get; set; }

    /// <summary>Gets the number of render extension-point invocations.</summary>
    internal int RenderCalls { get; private set; }

    /// <summary>Gets or sets work invoked from inside the next render pass.</summary>
    internal Action<ProbeControl>? Rendering { get; set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        MeasureConstraints.Add(constraint);
        Measuring?.Invoke(this);
        return _intrinsic;
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        ArrangeBounds.Add(bounds);
        Arranging?.Invoke(this);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        RenderCalls++;
        Rendering?.Invoke(this);
        _ = canvas.Draw(
            Content.Span,
            new Point(ContentBounds.X, ContentBounds.Y),
            ResolvedStyle);
    }

    /// <summary>Draws one Rune using this control's resolved terminal style.</summary>
    internal void Draw(TerminalCanvas canvas, Rune value)
    {
        Span<char> buffer = stackalloc char[2];
        int length = value.EncodeToUtf16(buffer);
        _ = canvas.Draw(buffer[..length], new Point(Bounds.X, Bounds.Y), ResolvedStyle);
    }
}
