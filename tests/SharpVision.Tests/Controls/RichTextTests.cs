using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;

using Shouldly;

using Wrapping = SharpVision.Text.Wrapping;

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
        using var frame = new Frame(new Size(4, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("H");
        frame.GetCell(new Point(0, 0)).Style.Foreground.ShouldBe(Color.Indexed(2));
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("G");
        frame.GetCell(new Point(0, 1)).Style.Hyperlink.ShouldBe("https://example.test");
    }

    /// <summary>Verifies wrapping never splits a wide grapheme owner.</summary>
    [Fact]
    public void Render_WhenWideGraphemeWraps_MovesCompleteOwnerToNextLine()
    {
        var control = new RichText { Wrapping = Wrapping.Grapheme };
        control.Inlines.Add(new Run("a界"));
        new Engine().Layout(control, new Size(2, 2));
        using var frame = new Frame(new Size(2, 2));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("a");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("界");
        frame.GetCell(new Point(1, 1)).IsContinuation.ShouldBeTrue();
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
