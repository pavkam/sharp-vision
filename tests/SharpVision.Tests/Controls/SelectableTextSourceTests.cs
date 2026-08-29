// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using Moq;

/// <summary>Verifies selectable-text projections through real mounted layout.</summary>
public sealed class SelectableTextSourceTests
{
    /// <summary>Verifies markup is removed while wide and atomic graphemes retain cell geometry.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenTextContainsMarkupAndUnicode_ProjectsSemanticGlyphsAsync()
    {
        var text = new ControlText("A <b>\u754c</b> e\u0301 \ud83d\udc69\u200d\ud83d\udcbb")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(() => snapshot = text.GetSelectableTextSnapshot(), "project selectable text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("A \u754c e\u0301 \ud83d\udc69\u200d\ud83d\udcbb");
        snapshot.IsAuthoritative.ShouldBeTrue();
        snapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe([
            new Selection(0, 1),
            new Selection(1, 2),
            new Selection(2, 3),
            new Selection(3, 4),
            new Selection(4, 6),
            new Selection(6, 7),
            new Selection(7, 12)
        ]);
        snapshot.Glyphs[2].Bounds.ShouldBe(new Rect(2, 0, 2, 1));
        snapshot.Glyphs[4].Bounds.Width.ShouldBe(1);
        snapshot.Glyphs[6].Bounds.Width.ShouldBe(2);
    }

    /// <summary>Verifies wrapping and ellipsis preserve full semantic text but project only source glyphs.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenTextWrapsOrEllipsizes_PreservesSemanticTextAsync()
    {
        var wrapped = new ControlText("abcd")
        {
            Overflow = Overflow.WrapAnywhere,
            Width = Length.Cells(2),
            Height = Length.Cells(2)
        };
        await using var wrappedSurface = await ComponentSurface.MountAsync(
            wrapped,
            new Size(2, 2),
            TestContext.Current.CancellationToken);

        SelectableTextSnapshot? wrappedSnapshot = null;
        await wrappedSurface.UpdateAsync(
            () => wrappedSnapshot = wrapped.GetSelectableTextSnapshot(),
            "project wrapped selectable text");
        wrappedSnapshot = wrappedSnapshot.ShouldNotBeNull();

        wrappedSnapshot.Text.ShouldBe("abcd");
        wrappedSnapshot.Glyphs.Select(glyph => glyph.Bounds).ShouldBe([
            new Rect(0, 0, 1, 1), new Rect(1, 0, 1, 1),
            new Rect(0, 1, 1, 1), new Rect(1, 1, 1, 1)
        ]);

        var clipped = new ControlText("abcd")
        {
            Overflow = Overflow.Ellipsis,
            Width = Length.Cells(3),
            Height = Length.Cells(1)
        };
        await using var clippedSurface = await ComponentSurface.MountAsync(
            clipped,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        SelectableTextSnapshot? clippedSnapshot = null;
        await clippedSurface.UpdateAsync(
            () => clippedSnapshot = clipped.GetSelectableTextSnapshot(),
            "project clipped selectable text");
        clippedSnapshot = clippedSnapshot.ShouldNotBeNull();

        clippedSnapshot.Text.ShouldBe("abcd");
        clippedSnapshot.Glyphs.Count.ShouldBe(2);
        clippedSnapshot.Glyphs.All(glyph => glyph.Range.End <= 2).ShouldBeTrue();
    }

    /// <summary>Verifies ordered containers translate and offset visible child projections.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenContainerHasVisibleAndCollapsedChildren_AggregatesVisibleChildrenAsync()
    {
        var first = new ControlText("A");
        var collapsed = new ControlText("X") { Visibility = Visibility.Collapsed };
        var hidden = new ControlText("Y") { Visibility = Visibility.Hidden };
        var second = new ControlText("\u754c");
        var stack = new Stack { Children = { first, collapsed, hidden, second } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(() => snapshot = stack.GetSelectableTextSnapshot(), "project child text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("A\u754c");
        snapshot.IsAuthoritative.ShouldBeFalse();
        snapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe([
            new Selection(0, 1), new Selection(1, 2)
        ]);
        snapshot.Glyphs.Select(glyph => glyph.Bounds).ShouldBe([
            new Rect(first.Bounds.X - stack.Bounds.X, first.Bounds.Y - stack.Bounds.Y, 1, 1),
            new Rect(second.Bounds.X - stack.Bounds.X, second.Bounds.Y - stack.Bounds.Y, 2, 1)
        ]);
    }

    /// <summary>Verifies a reversed stack aggregates semantic text in its visual reading order.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenStackIsReversed_AggregatesVisualOrderAsync()
    {
        // Arrange
        var stack = new Stack
        {
            Reverse = true,
            Children = { new ControlText("AAAA"), new ControlText("BBBB"), new ControlText("CCCC") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act
        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(() => snapshot = stack.GetSelectableTextSnapshot(), "project reversed text");

        // Assert
        snapshot.ShouldNotBeNull().Text.ShouldBe("CCCCBBBBAAAA");
    }

    /// <summary>Verifies direct hidden sources retain semantics without claiming visible geometry.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenDirectSourceIsNotEffectivelyVisible_RetainsOnlySemanticsAsync()
    {
        var hiddenText = new ControlText("Hidden") { Visibility = Visibility.Hidden };
        var collapsedText = new ControlText("Collapsed") { Visibility = Visibility.Collapsed };
        var hiddenInput = new TextInput { Text = "Input", Visibility = Visibility.Hidden };
        var collapsedInput = new TextInput { Text = "Editor", Visibility = Visibility.Collapsed };
        var ancestorHiddenText = new ControlText("AncestorText");
        var ancestorHiddenInput = new TextInput { Text = "AncestorInput" };
        var hiddenAncestor = new Stack
        {
            Visibility = Visibility.Hidden,
            Children = { ancestorHiddenText, ancestorHiddenInput }
        };
        var root = new Stack
        {
            Children = { hiddenText, collapsedText, hiddenInput, collapsedInput, hiddenAncestor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot[] snapshots = [];

        await surface.UpdateAsync(
            () => snapshots = [
                hiddenText.GetSelectableTextSnapshot(),
                collapsedText.GetSelectableTextSnapshot(),
                hiddenInput.GetSelectableTextSnapshot(),
                collapsedInput.GetSelectableTextSnapshot(),
                ancestorHiddenText.GetSelectableTextSnapshot(),
                ancestorHiddenInput.GetSelectableTextSnapshot()
            ],
            "project hidden direct sources");

        snapshots.Select(snapshot => snapshot.Text).ShouldBe([
            "Hidden", "Collapsed", "Input", "Editor", "AncestorText", "AncestorInput"
        ]);
        snapshots.All(snapshot => snapshot.Glyphs.Count == 0).ShouldBeTrue();
        snapshots.All(snapshot => snapshot.IsAuthoritative).ShouldBeTrue();
    }

    /// <summary>Verifies zero-cell controls remain semantic without invalid geometry.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenTextContainsZeroCellControl_OmitsOnlyItsGeometryAsync()
    {
        var text = new ControlText("\0A e\u0301");
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(5, 1),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(() => snapshot = text.GetSelectableTextSnapshot(), "project zero-cell text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("\0A e\u0301");
        snapshot.Glyphs.Any(glyph => glyph.Range == new Selection(0, 1)).ShouldBeFalse();
        snapshot.Glyphs.Single(glyph => glyph.Range == new Selection(3, 5)).Bounds.Width.ShouldBe(1);
    }

    /// <summary>Verifies line alignment and clipped overflow use rendered source positions only.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenTextIsCenteredOrClipped_UsesFormattedGeometryAsync()
    {
        var centered = new ControlText("A")
        {
            TextAlignment = Alignment.Center,
            Width = Length.Cells(5),
            Height = Length.Cells(1)
        };
        await using var centeredSurface = await ComponentSurface.MountAsync(
            centered,
            new Size(5, 1),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? centeredSnapshot = null;
        await centeredSurface.UpdateAsync(
            () => centeredSnapshot = centered.GetSelectableTextSnapshot(),
            "project centered text");
        centeredSnapshot = centeredSnapshot.ShouldNotBeNull();

        centeredSnapshot.Glyphs.Single().Bounds.ShouldBe(new Rect(2, 0, 1, 1));

        var clipped = new ControlText("abcd")
        {
            Overflow = Overflow.Clip,
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        await using var clippedSurface = await ComponentSurface.MountAsync(
            clipped,
            new Size(2, 1),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? clippedSnapshot = null;
        await clippedSurface.UpdateAsync(
            () => clippedSnapshot = clipped.GetSelectableTextSnapshot(),
            "project clipped text");
        clippedSnapshot = clippedSnapshot.ShouldNotBeNull();

        clippedSnapshot.Text.ShouldBe("abcd");
        clippedSnapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe([
            new Selection(0, 1), new Selection(1, 2)
        ]);
        clippedSnapshot.Glyphs.Select(glyph => glyph.Bounds).ShouldBe([
            new Rect(0, 0, 1, 1), new Rect(1, 0, 1, 1)
        ]);
    }

    /// <summary>Verifies single-content owners aggregate their retained content exactly once.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenContentControlOwnsText_AggregatesOnceAsync()
    {
        var owner = new StatusBarItem { Content = new ControlText("Only") };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(() => snapshot = owner.GetSelectableTextSnapshot(), "project content text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("Only");
        snapshot.Glyphs.Count.ShouldBe(4);
        snapshot.IsAuthoritative.ShouldBeFalse();
    }

    /// <summary>Verifies a composite's private retained root is traversed exactly once.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenCompositeOwnsTextRoot_AggregatesOnceAsync()
    {
        var owner = new ProbeCompositeControl(new ControlText("Root"));
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(() => snapshot = owner.GetSelectableTextSnapshot(), "project composite text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("Root");
        snapshot.Glyphs.Count.ShouldBe(4);
        snapshot.IsAuthoritative.ShouldBeFalse();
    }

    /// <summary>Verifies caption owners expose only their displayed caption geometry.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenInputHasCaption_ExcludesInputChromeAsync()
    {
        var button = new Button { Text = "&Go", UseMnemonic = true };
        var checkBox = new CheckBox { Text = "Pick" };
        var root = new Stack { Children = { button, checkBox } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(10, 4),
            TestContext.Current.CancellationToken);

        SelectableTextSnapshot? buttonSnapshot = null;
        SelectableTextSnapshot? checkSnapshot = null;
        await surface.UpdateAsync(
            () =>
            {
                buttonSnapshot = button.GetSelectableTextSnapshot();
                checkSnapshot = checkBox.GetSelectableTextSnapshot();
            },
            "project input captions");
        buttonSnapshot = buttonSnapshot.ShouldNotBeNull();
        checkSnapshot = checkSnapshot.ShouldNotBeNull();

        buttonSnapshot.Text.ShouldBe("Go");
        buttonSnapshot.Glyphs.Count.ShouldBe(2);
        buttonSnapshot.Glyphs[0].Bounds.X.ShouldBe(button.TextControl!.Bounds.X - button.Bounds.X);
        checkSnapshot.Text.ShouldBe("Pick");
        checkSnapshot.Glyphs.Count.ShouldBe(4);
        checkSnapshot.Glyphs[0].Bounds.X.ShouldBe(checkBox.TextControl!.Bounds.X - checkBox.Bounds.X);
    }

    /// <summary>Verifies editors expose visible source text without placeholder or password disclosure.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenTextInputScrollsOrMasks_ProjectsSafelyAsync()
    {
        var input = new TextInput
        {
            Text = "abcd",
            Placeholder = "secret",
            Width = Length.Cells(4),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(() => snapshot = input.GetSelectableTextSnapshot(), "project editor text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("abcd");
        snapshot.IsAuthoritative.ShouldBeTrue();
        snapshot.Glyphs.Count.ShouldBeLessThan(4);

        SelectableTextSnapshot? masked = null;
        await surface.UpdateAsync(
            () =>
            {
                input.PasswordCharacter = new Rune('*');
                masked = input.GetSelectableTextSnapshot();
            },
            "mask and project editor text");
        masked = masked.ShouldNotBeNull();

        masked.Text.ShouldBeEmpty();
        masked.Glyphs.ShouldBeEmpty();
        masked.IsAuthoritative.ShouldBeTrue();
    }

    /// <summary>Verifies editor projections follow public viewport scrolling.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenTextInputViewportScrolls_ProjectsVisibleRangesAsync()
    {
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "abcdef\none\ntwo\nthree",
            CaretIndex = 0,
            Width = Length.Cells(4),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(4, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);

        await surface.Pointer.WheelAsync(input, default, wheelY: -1);
        await surface.Pointer.WheelAsync(input, default, wheelX: 1);
        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(() => snapshot = input.GetSelectableTextSnapshot(), "project scrolled editor");
        snapshot = snapshot.ShouldNotBeNull();

        input.HorizontalOffset.ShouldBe(1);
        input.VerticalOffset.ShouldBe(1);
        snapshot.Text.ShouldBe("abcdef\none\ntwo\nthree");
        snapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe([
            new Selection(8, 9), new Selection(9, 10)
        ]);
        snapshot.Glyphs.Select(glyph => glyph.Bounds).ShouldBe([
            new Rect(0, 0, 1, 1), new Rect(1, 0, 1, 1)
        ]);
    }

    /// <summary>Verifies wrapped editor lines preserve semantics and omit newline geometry.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenTextInputWrapsMultilineText_ProjectsVisualLinesAsync()
    {
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "ab\ncdef",
            CaretIndex = 0,
            WordWrap = true,
            ScrollBars = ScrollBars.None,
            Width = Length.Cells(2),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(2, 3),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(() => snapshot = input.GetSelectableTextSnapshot(), "project wrapped editor");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("ab\ncdef");
        snapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe([
            new Selection(0, 1), new Selection(1, 2),
            new Selection(3, 4), new Selection(4, 5),
            new Selection(5, 6), new Selection(6, 7)
        ]);
        snapshot.Glyphs.Select(glyph => glyph.Bounds).ShouldBe([
            new Rect(0, 0, 1, 1), new Rect(1, 0, 1, 1),
            new Rect(0, 1, 1, 1), new Rect(1, 1, 1, 1),
            new Rect(0, 2, 1, 1), new Rect(1, 2, 1, 1)
        ]);
    }

    /// <summary>Verifies a leading control grapheme remains semantic without glyph geometry.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenTextInputContainsLeadingControlGrapheme_OmitsGeometryAsync()
    {
        var input = new TextInput
        {
            AcceptsReturn = true,
            Text = "\nA",
            Width = Length.Cells(2),
            Height = Length.Cells(2)
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(2, 2),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(() => snapshot = input.GetSelectableTextSnapshot(), "project combining editor text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("\nA");
        snapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe([new Selection(1, 2)]);
        snapshot.Glyphs.Single().Bounds.ShouldBe(new Rect(0, 1, 1, 1));
    }

    /// <summary>Verifies scrolling apertures exclude fully and partially clipped child glyphs atomically.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenAutoScrollClipsChildren_OmitsClippedGeometryAsync()
    {
        var first = new ControlText("A") { Height = Length.Cells(1), MinHeight = Length.Cells(1) };
        var outside = new ControlText("B") { Height = Length.Cells(1), MinHeight = Length.Cells(1) };
        var vertical = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { first, outside }
        };
        await using var verticalSurface = await ComponentSurface.MountAsync(
            vertical,
            new Size(2, 1),
            TestContext.Current.CancellationToken);
        outside.Bounds.Height.ShouldBe(1);
        outside.Bounds.Y.ShouldBeGreaterThanOrEqualTo(vertical.Bounds.Bottom);
        SelectableTextSnapshot? verticalSnapshot = null;
        await verticalSurface.UpdateAsync(
            () => verticalSnapshot = vertical.GetSelectableTextSnapshot(),
            "project vertically clipped aggregate");
        verticalSnapshot = verticalSnapshot.ShouldNotBeNull();

        verticalSnapshot.Text.ShouldBe("AB");
        verticalSnapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe([new Selection(0, 1)]);

        var wide = new ControlText("\u754c")
        {
            Width = Length.Cells(2),
            MinWidth = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var horizontal = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            Orientation = Orientation.Horizontal,
            Children = { wide }
        };
        await using var horizontalSurface = await ComponentSurface.MountAsync(
            horizontal,
            new Size(1, 1),
            TestContext.Current.CancellationToken);
        wide.Bounds.Width.ShouldBe(2);
        SelectableTextSnapshot? horizontalSnapshot = null;
        await horizontalSurface.UpdateAsync(
            () => horizontalSnapshot = horizontal.GetSelectableTextSnapshot(),
            "project partially clipped wide aggregate");
        horizontalSnapshot = horizontalSnapshot.ShouldNotBeNull();

        horizontalSnapshot.Text.ShouldBe("\u754c");
        horizontalSnapshot.Glyphs.ShouldBeEmpty();
    }

    /// <summary>Verifies a queried descendant aggregate honors clipping inherited from its ancestor.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenAggregateAncestorClips_OmitsDescendantOverflowGeometryAsync()
    {
        var first = new ControlText("A") { Height = Length.Cells(1), MinHeight = Length.Cells(1) };
        var outside = new ControlText("B") { Height = Length.Cells(1), MinHeight = Length.Cells(1) };
        var inner = new Stack
        {
            Height = Length.Cells(2),
            MinHeight = Length.Cells(2),
            Children = { first, outside }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { inner }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(2, 1),
            TestContext.Current.CancellationToken);
        outside.Bounds.Height.ShouldBe(1);
        outside.Bounds.Y.ShouldBeGreaterThanOrEqualTo(outer.Bounds.Bottom);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(() => snapshot = inner.GetSelectableTextSnapshot(), "project ancestor-clipped text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("AB");
        snapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe([new Selection(0, 1)]);
    }

    /// <summary>Verifies headered owners aggregate header then content exactly once.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenHeaderedControlsAreMounted_AggregatesHeaderThenContentAsync()
    {
        var group = new GroupBox
        {
            HeaderText = "Head",
            Content = new ControlText("Body"),
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        await using var groupSurface = await ComponentSurface.MountAsync(
            group,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? groupSnapshot = null;
        await groupSurface.UpdateAsync(
            () => groupSnapshot = group.GetSelectableTextSnapshot(),
            "project group header and content");
        groupSnapshot = groupSnapshot.ShouldNotBeNull();

        groupSnapshot.Text.ShouldBe("HeadBody");
        groupSnapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe(Enumerable.Range(0, 8)
            .Select(index => new Selection(index, index + 1)));

        var expander = new Expander
        {
            HeaderText = "Top",
            Content = new ControlText("Leaf"),
            IsExpanded = true,
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        await using var expanderSurface = await ComponentSurface.MountAsync(
            expander,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? expanderSnapshot = null;
        await expanderSurface.UpdateAsync(
            () => expanderSnapshot = expander.GetSelectableTextSnapshot(),
            "project expander header and content");
        expanderSnapshot = expanderSnapshot.ShouldNotBeNull();

        expanderSnapshot.Text.ShouldBe("TopLeaf");
        expanderSnapshot.Glyphs.Select(glyph => glyph.Range).ShouldBe(Enumerable.Range(0, 7)
            .Select(index => new Selection(index, index + 1)));
        expanderSnapshot.Glyphs[3].Bounds.Y.ShouldBeGreaterThan(expanderSnapshot.Glyphs[0].Bounds.Y);
    }

    /// <summary>Verifies deep aggregate traversal captures its authoritative leaf once.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenAggregateDepthIsLarge_CapturesLeafOnceAsync()
    {
        var captures = 0;
        var leaf = new Mock<ControlBase> { CallBase = true };
        _ = leaf.As<ISelectableTextSource>()
            .Setup(source => source.GetSelectableTextSnapshot())
            .Returns(() =>
            {
                captures++;
                return new SelectableTextSnapshot(
                    "L",
                    [new SelectableTextGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1))],
                    isAuthoritative: true);
            });
        leaf.Object.Width = Length.Cells(1);
        leaf.Object.Height = Length.Cells(1);
        var root = leaf.Object;

        for (var depth = 0; depth < 64; depth++)
        {
            root = new ProbeCompositeControl(root);
        }

        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(1, 1),
            TestContext.Current.CancellationToken);
        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(
            () => snapshot = ((ISelectableTextSource) root).GetSelectableTextSnapshot(),
            "project deep aggregate text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("L");
        snapshot.Glyphs.Count.ShouldBe(1);
        captures.ShouldBe(1);
    }

    /// <summary>Verifies direct text sources honor an ancestor AutoScroll viewport.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenDirectTextIsOutsideAncestorViewport_OmitsGeometryAsync()
    {
        var target = new ControlText("Off") { Height = Length.Cells(1), MinHeight = Length.Cells(1) };
        var root = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Children =
            {
                new ControlText("A") { Height = Length.Cells(1), MinHeight = Length.Cells(1) },
                target
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(3, 1),
            TestContext.Current.CancellationToken);
        target.Bounds.Height.ShouldBe(1);
        target.Bounds.Y.ShouldBeGreaterThanOrEqualTo(root.Bounds.Bottom);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(() => snapshot = target.GetSelectableTextSnapshot(), "project off-viewport text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("Off");
        snapshot.Glyphs.ShouldBeEmpty();
        snapshot.IsAuthoritative.ShouldBeTrue();
    }

    /// <summary>Verifies direct editors honor an ancestor AutoScroll viewport.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenDirectTextInputIsOutsideAncestorViewport_OmitsGeometryAsync()
    {
        var target = new TextInput
        {
            Text = "Edit",
            Height = Length.Cells(1),
            MinHeight = Length.Cells(1)
        };
        var root = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Children =
            {
                new ControlText("A") { Height = Length.Cells(1), MinHeight = Length.Cells(1) },
                target
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(4, 1),
            TestThemes.BorderlessInput,
            TestContext.Current.CancellationToken);
        target.Bounds.Height.ShouldBe(1);
        target.Bounds.Y.ShouldBeGreaterThanOrEqualTo(root.Bounds.Bottom);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(
            () => snapshot = target.GetSelectableTextSnapshot(),
            "project off-viewport editor");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("Edit");
        snapshot.Glyphs.ShouldBeEmpty();
        snapshot.IsAuthoritative.ShouldBeTrue();
    }

    /// <summary>Verifies a direct wide text glyph is omitted when an ancestor clips half of it.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenDirectWideTextIsPartiallyClipped_OmitsGlyphAtomicallyAsync()
    {
        var target = new ControlText("\u754c")
        {
            Width = Length.Cells(2),
            MinWidth = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var root = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            Orientation = Orientation.Horizontal,
            Children = { target }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(1, 1),
            TestContext.Current.CancellationToken);
        target.Bounds.Width.ShouldBe(2);
        SelectableTextSnapshot? snapshot = null;

        await surface.UpdateAsync(
            () => snapshot = target.GetSelectableTextSnapshot(),
            "project partially clipped direct text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("\u754c");
        snapshot.Glyphs.ShouldBeEmpty();
        snapshot.IsAuthoritative.ShouldBeTrue();
    }

    /// <summary>Verifies intrinsic popup content resets clipping from its small logical owner.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenComboBoxPopupIsOpen_UsesRootRenderPlaneAsync()
    {
        var combo = new ComboBox
        {
            Items = ["Alpha", "Beta"],
            SelectedIndex = 0,
            Width = Length.Cells(8),
            Height = Length.Cells(3),
            DropDownHeight = Length.Cells(2)
        };
        var root = new Overlay { Children = { combo } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 7),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => combo.IsOpen = true, "open selectable ComboBox popup");
        var popup = OwnedTree.Find<Popup>(combo).ShouldNotBeNull();
        popup.StartsPopupRenderBranch.ShouldBeTrue();
        var popupText = OwnedTree.FindAll<ControlText>(combo)
            .Single(text => text.Content == "Beta");
        popupText.Bounds.Y.ShouldBeGreaterThanOrEqualTo(combo.Bounds.Bottom);
        SelectableTextSnapshot? snapshot = null;
        await surface.UpdateAsync(
            () => snapshot = popupText.GetSelectableTextSnapshot(),
            "project intrinsic popup text");
        snapshot = snapshot.ShouldNotBeNull();

        snapshot.Text.ShouldBe("Beta");
        snapshot.Glyphs.Count.ShouldBe(4);
    }

    /// <summary>Verifies slot-only popup promotion resets to, but never escapes, the root plane.</summary>
    [Fact]
    public async Task GetSelectableTextSnapshot_WhenNormalTextUsesPopupSlot_ResetsAndClampsClipAsync()
    {
        var visible = new ControlText("OK") { Bounds = new Rect(3, 1, 2, 1) };
        var rootClipped = new ControlText("\u754c") { Bounds = new Rect(5, 2, 2, 1) };
        var owner = new TraversalOwner
        {
            Width = Length.Cells(2),
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        owner.AddPopup(visible);
        owner.AddPopup(rootClipped);
        var root = new Overlay { Children = { owner } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(6, 3),
            TestContext.Current.CancellationToken);
        visible.ResolveOwnedLayer(visible.OwningSlot.ShouldNotBeNull().Options.Layer)
            .ShouldBe(OwnedControlLayer.Popup);
        visible.IntrinsicLayer.ShouldBe(OwnedControlLayer.Normal);
        visible.StartsPopupRenderBranch.ShouldBeTrue();
        SelectableTextSnapshot? visibleSnapshot = null;
        SelectableTextSnapshot? clippedSnapshot = null;

        await surface.UpdateAsync(
            () =>
            {
                visibleSnapshot = visible.GetSelectableTextSnapshot();
                clippedSnapshot = rootClipped.GetSelectableTextSnapshot();
            },
            "project slot-promoted popup text");
        visibleSnapshot = visibleSnapshot.ShouldNotBeNull();
        clippedSnapshot = clippedSnapshot.ShouldNotBeNull();

        visibleSnapshot.Text.ShouldBe("OK");
        visibleSnapshot.Glyphs.Count.ShouldBe(2);
        clippedSnapshot.Text.ShouldBe("\u754c");
        clippedSnapshot.Glyphs.ShouldBeEmpty();
    }
}
