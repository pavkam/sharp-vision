// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using SharpVision.Tests.Controls;

/// <summary>Exposes the protected projection-surface and width-dependent-projection authoring
/// contract for behavioral tests.</summary>
internal sealed class ScrollableCompositeControlProjectionProbe: ScrollableCompositeControlBase, IStyled<SemanticColorStyle>
{
    // Deliberately much larger than any viewport width this probe is laid out against, so a
    // narrower or wider settled width always produces a different row count - the same reflow
    // shape JsonView's and CodeView's own width-dependent projections use, just reduced to one
    // arithmetic line instead of real line wrapping.
    private const int _totalCells = 400;

    private readonly StyleSlot<SemanticColorStyle> _style;
    private int? _projectionWidth;

    /// <summary>Initializes a probe hosting one projection surface inside a private scrolling host.</summary>
    /// <param name="forwardsScrollEvent">
    /// Forwarded to the surface overload of <c>InitializeScrollableContent</c>.
    /// </param>
    /// <param name="installWidthDependentProjection">
    /// Whether the constructor installs width-dependent projection immediately. A test that means
    /// to observe the guard <c>InitializeWidthDependentProjection</c> raises against an
    /// incompatible host passes false and calls <see cref="InstallWidthDependentProjection"/> itself.
    /// </param>
    internal ScrollableCompositeControlProjectionProbe(
        bool forwardsScrollEvent = false,
        bool installWidthDependentProjection = true)
    {
        _style = InitializeStyle(SemanticColorStyle.Definition);
        Surface = new ProjectionSurface(this, Measure, RenderSurface);
        ScrollableHost = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Children = { Surface },
        };
        InitializeContent(ScrollableHost);
        InitializeScrollableContent(ScrollableHost, Surface, forwardsScrollEvent);

        if (installWidthDependentProjection)
        {
            InstallWidthDependentProjection();
        }
    }

    /// <inheritdoc/>
    public SemanticColorStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <inheritdoc/>
    public SemanticColorStyle ActualStyle => _style.Actual;

    /// <summary>Gets the private tall content host installed by the constructor.</summary>
    internal Stack ScrollableHost { get; }

    /// <summary>Gets the shared projection surface installed by the constructor.</summary>
    internal ProjectionSurface Surface { get; }

    /// <summary>Gets how many times the reproject callback ran.</summary>
    internal int ReprojectCount { get; private set; }

    /// <summary>Propagates an inherited Theme into this probe and its retained descendants for
    /// focused theme-impact tests, bypassing the routed ancestry a real Theme assignment would
    /// otherwise require.</summary>
    /// <param name="theme">The prospective Theme.</param>
    internal void ApplyTheme(Theme theme) => PropagateTheme(theme);

    /// <summary>Installs width-dependent projection through the protected owner seam, exercising the
    /// guard from outside the class.</summary>
    internal void InstallWidthDependentProjection() =>
        InitializeWidthDependentProjection(static () => true, () => _projectionWidth, Reproject);

    private Size Measure(int? width)
    {
        if (width is { } bounded && bounded > 0 && _projectionWidth != bounded)
        {
            _projectionWidth = bounded;
        }

        var effectiveWidth = _projectionWidth ?? _totalCells;
        return new Size(effectiveWidth, Math.Max(1, _totalCells / effectiveWidth));
    }

    private static void RenderSurface(TerminalCanvas canvas, Rect bounds)
    {
        _ = canvas;
        _ = bounds;
    }

    private void Reproject(int width)
    {
        ReprojectCount++;
        _projectionWidth = width;
    }
}
