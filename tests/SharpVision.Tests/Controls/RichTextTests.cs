// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;




/// <summary>Verifies RichText inline ownership, layout, styling, and cells.</summary>
public sealed class RichTextTests
{
    #region Ownership

    /// <summary>Verifies one inline cannot be duplicated or shared across documents.</summary>
    [Fact]
    public void Add_WhenInlineIsAlreadyOwned_ThrowsBeforeMutation()
    {
        var first = new RichText();
        var second = new RichText();
        var run = new Run("hello");
        first.Inlines.Add(run);

        _ = Should.Throw<ArgumentException>(() => first.Inlines.Add(run));
        _ = Should.Throw<ArgumentException>(() => second.Inlines.Add(run));

        first.Inlines.Count.ShouldBe(1);
        second.Inlines.Count.ShouldBe(0);
    }

    /// <summary>Verifies removal releases ownership for another document.</summary>
    [Fact]
    public void Remove_WhenInlineIsOwned_ReleasesItForReuse()
    {
        var first = new RichText();
        var second = new RichText();
        var run = new Run("hello");
        first.Inlines.Add(run);

        first.Inlines.Remove(run).ShouldBeTrue();
        Should.NotThrow(() => second.Inlines.Add(run));

        second.Inlines.ShouldContain(run);
    }

    /// <summary>Verifies assigning an inline back to its current slot is a no-op.</summary>
    [Fact]
    public void Indexer_WhenValueIsCurrentInline_DoesNotRejectOwnership()
    {
        var control = new RichText();
        var run = new Run("hello");
        control.Inlines.Add(run);

        _ = Should.NotThrow(() => control.Inlines[0] = run);

        control.Inlines.Count.ShouldBe(1);
        control.Inlines[0].ShouldBeSameAs(run);
    }

    /// <summary>Verifies disposal releases inline ownership for later reuse.</summary>
    [Fact]
    public void Dispose_WhenDocumentOwnsInline_ReleasesOwnership()
    {
        var first = new RichText();
        var second = new RichText();
        var run = new Run("hello");
        first.Inlines.Add(run);

        first.Dispose();

        Should.NotThrow(() => second.Inlines.Add(run));
        second.Inlines.ShouldContain(run);
    }

    #endregion

    #region Layout and rendering

    /// <summary>Verifies runs and explicit breaks produce exact styled cells.</summary>
    [Fact]
    public void Render_WhenRunsHaveStylesAndBreaks_WritesExactCells()
    {
        var control = new RichText();
        control.Inlines.Add(new Run("Hi") { Foreground = Color.Indexed(2) });
        control.Inlines.Add(new LineBreak());
        control.Inlines.Add(new Hyperlink("Go", "https://example.test"));
        new Engine().Layout(control, new Size(4, 2));
        using Frame frame = new(new Size(4, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("H");
        frame.GetCell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(2));
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("G");
        frame.GetCell(new Point(0, 1)).Style.Hyperlink.ShouldBe("https://example.test");
    }

    /// <summary>Verifies inline modern decorations reach exact semantic cells.</summary>
    [Fact]
    public void Render_WhenRunHasModernDecorations_WritesCompleteStyle()
    {
        var run = new Run("x")
        {
            Attributes = Attributes.RapidBlink | Attributes.Overline,
            Underline = Underline.Curly,
            UnderlineColor = Color.Rgb(1, 2, 3),
        };
        var control = new RichText();
        control.Inlines.Add(run);
        new Engine().Layout(control, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        control.Render(frame.Canvas);

        var style = frame.GetCell(default).Style;
        style.Attributes.ShouldBe(Attributes.RapidBlink | Attributes.Overline);
        style.Underline.ShouldBe(Underline.Curly);
        style.UnderlineColor.ShouldBe(Color.Rgb(1, 2, 3));
    }

    /// <summary>Verifies invalid inline decoration changes preserve previous state.</summary>
    [Fact]
    public void Decorations_WhenCombinationIsInvalid_ThrowBeforeMutation()
    {
        var run = new Run("x");

        _ = Should.Throw<ArgumentException>(() =>
            run.UnderlineColor = Color.Indexed(1));
        run.UnderlineColor.ShouldBeNull();
        run.Underline = Underline.Curly;
        run.UnderlineColor = Color.Indexed(1);

        _ = Should.Throw<ArgumentException>(() =>
            run.Attributes = Attributes.Underline);

        run.Attributes.ShouldBeNull();
        run.Underline.ShouldBe(Underline.Curly);
        run.UnderlineColor.ShouldBe(Color.Indexed(1));
    }

    /// <summary>Verifies wrapping never splits a wide grapheme owner.</summary>
    [Fact]
    public void Render_WhenWideGraphemeWraps_MovesCompleteOwnerToNextLine()
    {
        var control = new RichText() { Wrapping = Wrapping.Grapheme };
        control.Inlines.Add(new Run("a界"));
        new Engine().Layout(control, new Size(2, 2));
        using Frame frame = new(new Size(2, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("界");
        frame.GetCell(new Point(1, 1)).IsContinuation.ShouldBeTrue();
    }

    /// <summary>Verifies word wrapping preserves a complete following word when a whitespace boundary fits.</summary>
    [Fact]
    public void Render_WhenWordWrappingFindsWhitespaceBoundary_MovesWholeWordToNextLine()
    {
        var control = new RichText() { Wrapping = Wrapping.Word };
        control.Inlines.Add(new Run("one two"));
        new Engine().Layout(control, new Size(5, 2));
        using Frame frame = new(new Size(5, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("o");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("t");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("o");
    }

    /// <summary>Verifies inline mutation invalidates and changes desired size.</summary>
    [Fact]
    public void Content_WhenRunChanges_RecomputesDocumentLayout()
    {
        var run = new Run("a");
        var control = new RichText();
        control.Inlines.Add(run);
        var engine = new Engine();
        engine.Layout(control, new Size(10, 1));

        run.Content = "abcd";
        engine.Layout(control, new Size(10, 1));

        control.DesiredSize.ShouldBe(new Size(4, 1));
    }

    #endregion
}
