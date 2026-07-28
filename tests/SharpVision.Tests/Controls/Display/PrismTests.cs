// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies Prism validation, content ownership, layout, and deterministic foreground effects.</summary>
public sealed class PrismTests
{
    /// <summary>Verifies a new Prism has stable effect defaults and no required content.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var prism = new Prism();

        prism.Phase.ShouldBe(0d);
        prism.CycleLength.ShouldBe(18);
        prism.Direction.ShouldBe(PrismDirection.Diagonal);
        prism.Content.ShouldBeNull();
    }

    /// <summary>Verifies rejected effect values preserve committed state and publish no property change.</summary>
    [Fact]
    public void EffectProperties_WhenValuesAreInvalid_ThrowBeforeMutationOrNotification()
    {
        var prism = new Prism { Phase = 0.25d, CycleLength = 7, Direction = PrismDirection.Horizontal };
        var notifications = new List<string?>();
        prism.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        var invalidPhases = new[]
        {
            double.NaN, double.NegativeInfinity, double.PositiveInfinity, -double.Epsilon, 1d, 2d
        };

        foreach (var invalid in invalidPhases)
        {
            _ = Should.Throw<ArgumentOutOfRangeException>(() => prism.Phase = invalid);
        }

        _ = Should.Throw<ArgumentOutOfRangeException>(() => prism.CycleLength = 0);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => prism.CycleLength = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => prism.Direction = (PrismDirection) 99);

        prism.Phase.ShouldBe(0.25d);
        prism.CycleLength.ShouldBe(7);
        prism.Direction.ShouldBe(PrismDirection.Horizontal);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies effect changes publish once and preserve cached measure and arrangement.</summary>
    [Fact]
    public void EffectProperties_WhenChanged_InvalidateRenderingWithoutRelayout()
    {
        var content = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var prism = new Prism { Content = content };
        var engine = new Engine();
        var notifications = new List<string?>();
        prism.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        engine.Layout(prism, new Size(4, 2));
        using (Frame frame = new(new Size(4, 2)))
        {
            prism.Render(frame.Canvas);
        }

        var desiredSize = prism.DesiredSize;
        var prismBounds = prism.Bounds;
        var contentBounds = content.Bounds;
        var measureCalls = content.MeasureConstraints.Count;
        var arrangeCalls = content.ArrangeBounds.Count;
        var renderCalls = content.RenderCalls;

        prism.Phase = 0.5d;
        prism.CycleLength = 6;
        prism.Direction = PrismDirection.Vertical;
        prism.Pending.ShouldBe(Invalidation.Render);
        content.Pending.ShouldBe(Invalidation.None);
        engine.Layout(prism, new Size(4, 2));
        prism.Pending.ShouldBe(Invalidation.Render);
        using Frame updated = new(new Size(4, 2));
        prism.Render(updated.Canvas);

        notifications.ShouldBe([
            nameof(Prism.Phase),
            nameof(Prism.CycleLength),
            nameof(Prism.Direction)
        ]);
        prism.DesiredSize.ShouldBe(desiredSize);
        prism.Bounds.ShouldBe(prismBounds);
        content.Bounds.ShouldBe(contentBounds);
        content.MeasureConstraints.Count.ShouldBe(measureCalls);
        content.ArrangeBounds.Count.ShouldBe(arrangeCalls);
        content.RenderCalls.ShouldBe(renderCalls + 1);
    }

    /// <summary>Verifies child foregrounds change after rendering without altering rich semantics.</summary>
    [Fact]
    public void Render_WhenContentIsRich_RecolorsOnlyChildForegroundsDeterministically()
    {
        ControlText content = new(
            "<bg=blue><u=curly><uc=magenta><link=https://example.test><b>ABC</b></link></uc></u></bg>");
        var prism = new Prism
        {
            Content = content,
            Direction = PrismDirection.Horizontal,
            CycleLength = 6
        };
        var engine = new Engine();
        engine.Layout(prism, new Size(5, 3));
        using Frame first = new(new Size(5, 3));

        prism.Render(first.Canvas);

        prism.Bounds.ShouldBe(new Rect(0, 0, 3, 3));
        content.Bounds.ShouldBe(new Rect(0, 0, 3, 3));
        AssertRichStyle(first.GetCell(default).Style, Color.Rgb(255, 0, 0));
        AssertRichStyle(first.GetCell(new Point(1, 0)).Style, Color.Rgb(255, 255, 0));
        AssertRichStyle(first.GetCell(new Point(2, 0)).Style, Color.Rgb(0, 255, 0));

        var previousContentBounds = content.Bounds;
        prism.Phase = 0.5d;
        engine.Layout(prism, new Size(5, 3));
        using Frame second = new(new Size(5, 3));
        prism.Render(second.Canvas);

        content.Bounds.ShouldBe(previousContentBounds);
        AssertRichStyle(second.GetCell(default).Style, Color.Rgb(0, 255, 255));
    }

    /// <summary>Verifies each direction uses content-relative coordinates and wraps exact phase offsets.</summary>
    [Fact]
    public void Render_WhenDirectionChanges_UsesDocumentedCoordinateAndHueWrap()
    {
        RenderColors(PrismDirection.Horizontal).ShouldBe([
            Color.Rgb(0, 255, 255), Color.Rgb(255, 0, 0),
            Color.Rgb(0, 255, 255), Color.Rgb(255, 0, 0)
        ]);
        RenderColors(PrismDirection.Vertical).ShouldBe([
            Color.Rgb(0, 255, 255), Color.Rgb(0, 255, 255),
            Color.Rgb(255, 0, 0), Color.Rgb(255, 0, 0)
        ]);
        RenderColors(PrismDirection.Diagonal).ShouldBe([
            Color.Rgb(0, 255, 255), Color.Rgb(255, 0, 0),
            Color.Rgb(255, 0, 0), Color.Rgb(0, 255, 255)
        ]);
    }

    /// <summary>Verifies Prism post-processing does not recolor unrelated frame cells.</summary>
    [Fact]
    public void Render_WhenFrameHasUnrelatedCells_TransformsOnlyStoredContentInsideContentBounds()
    {
        var prism = new Prism
        {
            Content = new ControlText("A")
        };
        new Engine().Layout(prism, new Size(3, 3));
        using Frame frame = new(new Size(5, 3));
        var outsideStyle = new TerminalStyle(ReferenceColors.Get(9), ReferenceColors.Get(2));
        _ = frame.Canvas.Draw("Z", new Point(4, 2), outsideStyle);

        prism.Render(frame.Canvas);

        frame.GetCell(default).Style.Foreground.ShouldBe(Color.Rgb(255, 0, 0));
        frame.GetCell(new Point(4, 2)).Style.ShouldBe(outsideStyle);
    }

    /// <summary>Verifies an empty Prism does not transform stored cells already present in its content box.</summary>
    [Fact]
    public void Render_WhenContentIsNull_PreservesStoredCellsInsideContentBounds()
    {
        var prism = new Prism { Width = Length.Cells(2), Height = Length.Cells(1) };
        new Engine().Layout(prism, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));
        var original = new TerminalStyle(
            ReferenceColors.Get(9),
            ReferenceColors.Get(4),
            Attributes.Bold | Attributes.Italic,
            "https://example.test/underlay",
            Underline.Curly,
            ReferenceColors.Get(5));
        _ = frame.Canvas.Draw("Z", new Point(1, 0), original);

        prism.Render(frame.Canvas);

        AssertStoredCellUnchanged(frame, new Point(1, 0), "Z", original);
    }

    /// <summary>Verifies a child transforms only cells it writes within its complete arranged bounds.</summary>
    [Fact]
    public void Render_WhenChildWritesSubsetOfArrangedBounds_PreservesUnwrittenStoredCells()
    {
        var content = new ControlText("A")
        {
            Face = AppearanceTestValues.Face(background: Color.Transparent)
        };
        var prism = new Prism { Content = content, Width = Length.Cells(5), Height = Length.Cells(1) };
        new Engine().Layout(prism, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));
        var original = new TerminalStyle(
            ReferenceColors.Get(9),
            ReferenceColors.Get(4),
            Attributes.Bold | Attributes.Italic,
            "https://example.test/underlay",
            Underline.Curly,
            ReferenceColors.Get(5));
        _ = frame.Canvas.Draw("ZZZZZ", default, original);

        prism.Render(frame.Canvas);

        content.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
        FrameOracle.Get(frame, default).ShouldBe("A");
        frame.GetCell(default).Style.Foreground.ShouldBe(Color.Rgb(255, 0, 0));

        for (var x = 1; x < 5; x++)
        {
            AssertStoredCellUnchanged(frame, new Point(x, 0), "Z", original);
        }
    }

    /// <summary>Verifies a wide grapheme receives one lead-derived color across its complete owner.</summary>
    [Fact]
    public void Render_WhenContentContainsWideGrapheme_RecolorsOwnerAtomically()
    {
        var prism = new Prism
        {
            Content = new ControlText("界"),
            Direction = PrismDirection.Horizontal,
            CycleLength = 6
        };
        new Engine().Layout(prism, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));

        prism.Render(frame.Canvas);

        var lead = frame.GetCell(default);
        var continuation = frame.GetCell(new Point(1, 0));
        lead.Width.ShouldBe(2);
        lead.Style.Foreground.ShouldBe(Color.Rgb(255, 0, 0));
        continuation.IsContinuation.ShouldBeTrue();
        continuation.Lead.ShouldBe(default);
        continuation.Style.ShouldBe(lead.Style);
    }

    /// <summary>Verifies stored spaces participate while untouched cells remain semantic blanks.</summary>
    [Fact]
    public void Render_WhenContentStoresSpace_RecolorsSpaceButNotUntouchedBlank()
    {
        var prism = new Prism { Content = new ControlText(" "), HorizontalAlignment = HorizontalAlignment.Stretch };
        new Engine().Layout(prism, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));

        prism.Render(frame.Canvas);

        frame.GetCell(default).Style.Foreground.ShouldBe(Color.Rgb(255, 0, 0));
        frame.GetCell(new Point(1, 0)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies null and empty content bounds render without an effect failure.</summary>
    [Fact]
    public void Render_WhenContentOrContentBoundsAreEmpty_HasNoForegroundEffect()
    {
        var empty = new Prism();
        new Engine().Layout(empty, new Size(0, 0));
        using Frame emptyFrame = new(new Size(0, 0));
        Should.NotThrow(() => empty.Render(emptyFrame.Canvas));

        var nullContent = new Prism { Width = Length.Cells(1), Height = Length.Cells(1) };
        new Engine().Layout(nullContent, new Size(1, 1));
        using Frame painted = new(new Size(1, 1));
        nullContent.Render(painted.Canvas);
        painted.GetCell(default).ShouldBe(CellInfo.Blank);

        var tiny = new Prism
        {
            Content = new ControlText("X"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new Engine().Layout(tiny, new Size(1, 1));
        using Frame tinyFrame = new(new Size(1, 1));
        Should.NotThrow(() => tiny.Render(tinyFrame.Canvas));
        tiny.Content.Bounds.ShouldBe(new Rect(0, 0, 1, 1));
        tinyFrame.GetCell(default).Style.Foreground.ShouldBe(Color.Rgb(255, 0, 0));
    }

    /// <summary>Verifies Prism retains ordinary replaceable ContentControl ownership semantics.</summary>
    [Fact]
    public void Content_WhenReplacedAndCleared_UsesOrdinaryContentControlOwnership()
    {
        var prism = new Prism();
        var first = new ProbeControl();
        var second = new ProbeControl();
        ContentControl owner = prism;

        owner.Content = first;
        owner.Content.ShouldBeSameAs(first);
        first.Parent.ShouldBeSameAs(prism);

        owner.Content = second;
        owner.Content.ShouldBeSameAs(second);
        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(prism);

        owner.Content = null;

        typeof(Prism).BaseType.ShouldBe(typeof(ContentControl));
        typeof(Prism).GetProperty(nameof(Container.Children)).ShouldBeNull();
        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeNull();
        owner.Content.ShouldBeNull();
        first.IsDisposed.ShouldBeFalse();
        second.IsDisposed.ShouldBeFalse();
    }

    private static Color[] RenderColors(PrismDirection direction)
    {
        var prism = new Prism
        {
            Content = new ControlText("AB\nCD"),
            Phase = 0.5d,
            CycleLength = 2,
            Direction = direction
        };
        new Engine().Layout(prism, new Size(2, 2));
        using Frame frame = new(new Size(2, 2));

        prism.Render(frame.Canvas);

        return
        [
            frame.GetCell(new Point(0, 0)).Style.Foreground,
            frame.GetCell(new Point(1, 0)).Style.Foreground,
            frame.GetCell(new Point(0, 1)).Style.Foreground,
            frame.GetCell(new Point(1, 1)).Style.Foreground
        ];
    }

    private static void AssertRichStyle(TerminalStyle style, Color foreground)
    {
        style.Foreground.ShouldBe(foreground);
        style.Background.ShouldBe(ReferenceColors.Get(4));
        style.Attributes.ShouldBe(Attributes.Bold);
        style.Hyperlink.ShouldBe("https://example.test");
        style.Underline.ShouldBe(Underline.Curly);
        style.UnderlineColor.ShouldBe(ReferenceColors.Get(5));
    }

    private static void AssertStoredCellUnchanged(
        Frame frame,
        Point point,
        string grapheme,
        TerminalStyle original)
    {
        FrameOracle.Get(frame, point).ShouldBe(grapheme);
        var actual = frame.GetCell(point).Style;
        actual.Foreground.IsRgb.ShouldBe(original.Foreground.IsRgb);
        actual.ShouldBe(original);
    }
}
