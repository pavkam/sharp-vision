// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Text;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Documents.Document;

/// <summary>Verifies a mounted <see cref="Document"/>'s interaction contract: link navigation and
/// release, keyboard and pointer activation, disabled links, keyboard scrolling, hover, and the
/// disabled state cascade.</summary>
public sealed class DocumentLinkSurfaceTests
{
    /// <summary>Verifies disposal clears the selected link and rejects subsequent access instead of
    /// exposing stale state from an unavailable control.</summary>
    [Fact]
    public void ActiveLink_WhenDocumentIsDisposed_ClearsStateAndThrows()
    {
        // Arrange
        var link = new DocumentLink("link");
        var document = LinkDocument(link);
        using var probe = new DocumentRenderProbe(document, new Size(12, 2));
        document.ActiveLink = link;
        document.ActiveLinkIndex.ShouldBe(0);

        // Act
        document.Dispose();

        // Assert
        document.ActiveLinkIndex.ShouldBe(-1);
        _ = Should.Throw<ObjectDisposedException>(() => document.ActiveLink);
    }

    /// <summary>Verifies disposal rejects active-link access even when no selection needed clearing.</summary>
    [Fact]
    public void ActiveLink_WhenDisposedWithoutSelection_Throws()
    {
        // Arrange
        var document = new Document();

        // Act
        document.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() => document.ActiveLink);
    }

    /// <summary>Verifies links with no painted cells are omitted from active-link navigation.</summary>
    [Fact]
    public void ActiveLink_WhenLinkHasNoVisibleRegion_CannotSelectTheInvisibleLink()
    {
        // Arrange
        var empty = new DocumentLink();
        var visible = new DocumentLink("visible");
        var document = LinkDocument(empty, visible);
        using var probe = new DocumentRenderProbe(document, new Size(12, 3));

        // Act and assert
        document.ActiveLink = empty;
        document.ActiveLink.ShouldBeNull();
        document.ActiveLink = visible;
        document.ActiveLink.ShouldBeSameAs(visible);
    }

    /// <summary>Verifies a mounted document renders its content and observes pointer hover without
    /// claiming press state, which it has none of.</summary>
    [Fact]
    public async Task Pointer_WhenDocumentIsHovered_TracksHoverWithoutPressStateAsync()
    {
        // Arrange
        var document = new Document
        {
            Blocks = { new DocumentHeading(1, "Title"), new DocumentParagraph("Body") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(document);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("T");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("B");
        surface.ShouldHaveState(document, VisualState.IsPointerOver);
        document.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies Tab and Shift+Tab step through the document's links while they have somewhere
    /// to go, and release focus to the surrounding tab order at either end exactly as a browser does
    /// after the last link on a page.</summary>
    [Fact]
    public async Task Keyboard_WhenTabReachesEitherEnd_StepsThroughLinksThenReleasesFocusAsync()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        var before = new Button { Text = "B" };
        var after = new Button { Text = "A" };
        var host = new Stack { Children = { before, document, after } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(20, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");

        // Act and assert - forward through every link, then out of the document.
        await surface.Keyboard.PressAsync(Code.Tab);
        document.ActiveLink.ShouldBeSameAs(first);
        surface.ShouldHaveFocus(document);

        await surface.Keyboard.PressAsync(Code.Tab);
        document.ActiveLink.ShouldBeSameAs(second);
        surface.ShouldHaveFocus(document);

        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(after);
        document.ActiveLink.ShouldBeSameAs(second);

        // Act and assert - backward is symmetric.
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(document);

        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        document.ActiveLink.ShouldBeSameAs(first);
        surface.ShouldHaveFocus(document);

        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(before);
    }

    /// <summary>Verifies moving the active link with Tab alone - with no click, scroll, or resize in
    /// between to force an incidental full repaint - actually repaints the highlight on the very next
    /// frame.</summary>
    /// <remarks>
    /// Regression test: moving the active link previously invalidated only the <see cref="Document"/>
    /// control's own render bit. The cells a document paints belong to a private surface several
    /// levels below it in the retained tree, so that surface's own render bit stayed clean, and the
    /// renderer's clean-subtree fast path kept reusing the previous frame's cells - the internal
    /// <see cref="Document.ActiveLink"/> state moved correctly, but the highlight never visibly
    /// followed it until an unrelated event elsewhere happened to force a full repaint. A test that
    /// only asserts <see cref="Document.ActiveLink"/> - as the Tab-stepping test above does - cannot
    /// catch this: the state was always correct, only the paint was stuck.
    /// </remarks>
    [Fact]
    public async Task Keyboard_WhenTabMovesTheActiveLinkAlone_RepaintsTheHighlightWithoutAnyOtherEventAsync()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        await surface.Keyboard.PressAsync(Code.Tab);
        var firstActiveStyle = surface.Cell(new Point(0, 0)).Style;

        // Act - nothing but a second Tab happens between the two style captures.
        await surface.Keyboard.PressAsync(Code.Tab);
        var firstAfterMovingOnStyle = surface.Cell(new Point(0, 0)).Style;
        var secondActiveStyle = surface.Cell(new Point(0, 2)).Style;

        // Assert - the highlight actually moved, rather than staying stuck on the first link.
        document.ActiveLink.ShouldBeSameAs(second);
        firstAfterMovingOnStyle.ShouldNotBe(firstActiveStyle);
        secondActiveStyle.ShouldBe(firstActiveStyle);
    }

    /// <summary>Verifies an active link immediately returns to its resting face when keyboard focus
    /// leaves the document, without relying on a later resize or pointer event to repaint it.</summary>
    [Fact]
    public async Task Focus_WhenFocusLeavesTheDocument_ClearsTheActiveLinkAppearanceAsync()
    {
        // Arrange
        var link = new DocumentLink("link");
        var document = LinkDocument(link);
        var after = new Button("After");
        var host = new Stack { Children = { document, after } };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        var restingStyle = surface.Cell(new Point(0, 0)).Style;
        await surface.Keyboard.PressAsync(Code.Tab);
        var activeStyle = surface.Cell(new Point(0, 0)).Style;

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(after);
        activeStyle.ShouldNotBe(restingStyle);
        surface.Cell(new Point(0, 0)).Style.ShouldBe(restingStyle);
        document.ActiveLink.ShouldBeSameAs(link);
    }

    /// <summary>Verifies Enter and Space both activate the focused link, raising the link's own
    /// notification before the document's central one.</summary>
    [Fact]
    public async Task Keyboard_WhenEnterOrSpaceActivatesTheFocusedLink_RaisesBothNotificationsAsync()
    {
        // Arrange
        var link = new DocumentLink("only");
        var document = LinkDocument(link);
        var order = new List<string>();
        link.Clicked += (_, _) => order.Add("link");
        document.LinkClicked += (_, eventArgs) =>
        {
            eventArgs.Link.ShouldBeSameAs(link);
            order.Add("document");
        };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.TypeAsync(" ");

        // Assert
        order.ShouldBe(["link", "document", "link", "document"]);
    }

    /// <summary>Verifies removing the active link and routing activation in the same dispatcher
    /// turn cannot publish either link notification for the detached node.</summary>
    [Fact]
    public async Task Keyboard_WhenActiveLinkIsRemovedBeforeImmediateActivation_DoesNotActivateItAsync()
    {
        // Arrange
        var link = new DocumentLink("only");
        var document = LinkDocument(link);
        var activations = 0;
        link.Clicked += (_, _) => activations++;
        document.LinkClicked += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        await surface.Keyboard.PressAsync(Code.Tab);
        var enter = new KeyEventArgs(new Stroke(Code.Enter, null, 0, Modifiers.None, KeyAction.Press));

        // Act
        await surface.UpdateAsync(
            () =>
            {
                document.Blocks.RemoveAt(0);
                _ = Router.Route(document, Events.Key, enter);
            },
            "remove active link and route Enter");

        // Assert
        activations.ShouldBe(0);
        enter.IsHandled.ShouldBeFalse();
        document.ActiveLink.ShouldBeNull();
    }

    /// <summary>Verifies same-turn Tab navigation skips a detached link still present in the stale
    /// projection and reaches the next owned link.</summary>
    [Fact]
    public async Task Keyboard_WhenEarlierLinkIsRemovedBeforeImmediateTab_SelectsTheNextOwnedLinkAsync()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        var tab = new KeyEventArgs(new Stroke(Code.Tab, null, 0, Modifiers.None, KeyAction.Press));

        // Act
        await surface.UpdateAsync(
            () =>
            {
                document.Blocks.RemoveAt(0);
                _ = Router.Route(document, Events.Key, tab);
            },
            "remove earlier link and route Tab");

        // Assert
        tab.IsHandled.ShouldBeTrue();
        document.ActiveLink.ShouldBeSameAs(second);
    }

    /// <summary>Verifies stale projected geometry cannot hit a detached link before the next layout
    /// rebuilds the document.</summary>
    [Fact]
    public void Pointer_WhenLinkedParagraphIsRemoved_StopsHittingItsOldCellSynchronously()
    {
        // Arrange
        var link = new DocumentLink("only");
        var paragraph = new DocumentParagraph { Inlines = { link } };
        var document = new Document { Blocks = { paragraph } };
        using var probe = new DocumentRenderProbe(document, new Size(12, 2));
        document.LinkAt(new Point(0, 0)).ShouldBeSameAs(link);

        // Act
        document.Blocks.Remove(paragraph).ShouldBeTrue();

        // Assert
        document.LinkAt(new Point(0, 0)).ShouldBeNull();
    }

    /// <summary>Verifies a disabled link is skipped by navigation, refused as an explicit selection,
    /// and never activated.</summary>
    [Fact]
    public async Task Keyboard_WhenALinkIsDisabled_SkipsItAndNeverActivatesItAsync()
    {
        // Arrange
        var first = new DocumentLink("first");
        var disabled = new DocumentLink("middle") { IsEnabled = false };
        var last = new DocumentLink("last");
        var document = LinkDocument(first, disabled, last);
        var activations = 0;
        disabled.Clicked += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        document.ActiveLink.ShouldBeSameAs(last);
        document.ActiveLinkIndex.ShouldBe(2);

        // Act - an explicit selection of the disabled link clears instead of selecting it.
        await surface.UpdateAsync(() => document.ActiveLink = disabled, "select disabled link");
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        document.ActiveLink.ShouldBeNull();
        activations.ShouldBe(0);
    }

    /// <summary>Verifies a disabled link keeps painting with the disabled link face rather than the
    /// ordinary one, so it reads as unavailable before anyone tries to use it.</summary>
    [Fact]
    public async Task Render_WhenALinkIsDisabled_PaintsItWithTheDisabledLinkFaceAsync()
    {
        // Arrange
        var enabled = new DocumentLink("aa");
        var disabled = new DocumentLink("bb") { IsEnabled = false };
        var document = LinkDocument(enabled, disabled);
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        var enabledStyle = surface.Cell(new Point(0, 0)).Style;
        var disabledStyle = surface.Cell(new Point(0, 2)).Style;

        // Assert
        disabledStyle.ShouldNotBe(enabledStyle);

        // Act - re-enabling repaints it with the ordinary link face.
        await surface.UpdateAsync(() => disabled.IsEnabled = true, "enable link");

        // Assert
        surface.Cell(new Point(0, 2)).Style.ShouldBe(enabledStyle);
    }

    /// <summary>Verifies an eligible primary release on a link focuses the document, selects that
    /// link, and activates it, while a click on ordinary text does neither.</summary>
    [Fact]
    public async Task Pointer_WhenLinkCellsArePressed_FocusesSelectsAndActivatesAsync()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        var activations = 0;
        second.Clicked += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(document, new Point(1, 2));

        // Assert
        activations.ShouldBe(1);
        document.ActiveLink.ShouldBeSameAs(second);
        surface.ShouldHaveFocus(document);

        // Act - ordinary content is not an activation target.
        await surface.Pointer.ClickAsync(document, new Point(0, 1));

        // Assert
        activations.ShouldBe(1);
        document.ActiveLink.ShouldBeSameAs(second);
    }

    /// <summary>Verifies a buttonless legacy X10 release completes the primary potential gesture
    /// that began on a link and activates it exactly once.</summary>
    [Fact]
    public async Task Pointer_WhenLegacyReleaseCompletesPotentialLink_ActivatesAsync()
    {
        // Arrange
        var link = new DocumentLink("link");
        var document = LinkDocument(link);
        var activations = 0;
        link.Clicked += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(document.SelectAll, "select before legacy link release");
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();

        // Act - X10 selector three is an unqualified release at cell zero.
        await surface.SendAsync("\u001b[M#!!"u8.ToArray(), "legacy release over potential link");

        // Assert
        activations.ShouldBe(1);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 0));
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies link activation requires release over the same enabled link and never
    /// occurs for an outside release or a selection drag.</summary>
    [Fact]
    public async Task Pointer_WhenLinkReleaseIsIneligible_DoesNotActivateAsync()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        var firstActivations = 0;
        var secondActivations = 0;
        first.Clicked += (_, _) => firstActivations++;
        second.Clicked += (_, _) => secondActivations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Act - release outside every link.
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(8, 1));
        await surface.Pointer.ReleaseAsync();

        // Act - release over a different link.
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(document, new Point(0, 2));
        await surface.Pointer.ReleaseAsync();

        // Assert - both movements became selection drags, so neither link activates.
        firstActivations.ShouldBe(0);
        secondActivations.ShouldBe(0);
    }

    /// <summary>Verifies disabling a link between press and release cancels pointer activation.</summary>
    [Fact]
    public async Task Pointer_WhenPressedLinkIsDisabledBeforeRelease_DoesNotActivateAsync()
    {
        // Arrange
        var link = new DocumentLink("only");
        var document = LinkDocument(link);
        var activations = 0;
        link.Clicked += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();
        activations.ShouldBe(0);

        // Act
        await surface.UpdateAsync(() => link.IsEnabled = false, "disable pressed link");
        await surface.Pointer.ReleaseAsync();

        // Assert
        activations.ShouldBe(0);
    }

    /// <summary>Verifies an ancestor-consumed preview press never arms document selection or link
    /// activation when later motion and release records remain unhandled.</summary>
    [Fact]
    public async Task Pointer_WhenAncestorConsumesPreviewPress_DoesNotSelectCollapseOrActivateAsync()
    {
        // Arrange
        var link = new DocumentLink("link");
        var document = LinkDocument(link);
        var host = new Stack { Children = { document } };
        var activations = 0;
        link.Clicked += (_, _) => activations++;
        _ = host.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs is
                {
                    Phase: RoutingPhase.Preview,
                    Pointer.Action: PointerAction.Press
                })
            {
                eventArgs.IsHandled = true;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(document.SelectAll, "select document before consumed press");

        // Act
        await surface.Pointer.DragAsync(document, new Point(0, 0), new Point(1, 0));

        // Assert
        activations.ShouldBe(0);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 4));
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies releasing a secondary button cannot complete a primary potential link
    /// click after its press collapses selection, while the later primary release activates exactly once.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryReleaseOccursDuringPotentialLink_WaitsForPrimaryReleaseAsync()
    {
        // Arrange
        var link = new DocumentLink("link");
        var document = LinkDocument(link);
        var activations = 0;
        link.Clicked += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(document.SelectAll, "select before multi-button link click");
        await surface.Pointer.MoveToAsync(document, new Point(0, 0));
        await surface.Pointer.PressAsync();

        // Act - SGR secondary release at the held primary point.
        await surface.SendAsync("\u001b[<2;1;1m"u8.ToArray(), "release secondary during primary link click");

        // Assert
        activations.ShouldBe(0);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Potential);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 0));

        // Act and assert - the primary release remains the sole completing transition.
        await surface.Pointer.ReleaseAsync();
        activations.ShouldBe(1);
        document.SelectionGesturePhase.ShouldBe(TextSelectionGesturePhase.Idle);
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 0));
    }

    /// <summary>Verifies release hit testing refreshes a mutated layout before deciding whether the
    /// pressed link identity still occupies the release cell.</summary>
    [Fact]
    public async Task Pointer_WhenLinkReflowsDuringRelease_DoesNotActivateStaleRegionAsync()
    {
        // Arrange
        var link = new DocumentLink("link");
        var paragraph = new DocumentParagraph { Inlines = { link } };
        var document = new Document { Blocks = { paragraph } };
        var host = new Stack { Children = { document } };
        var activations = 0;
        var mutated = false;
        link.Clicked += (_, _) => activations++;
        _ = host.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (!mutated &&
                eventArgs is
                {
                    Phase: RoutingPhase.Preview,
                    Pointer.Action: PointerAction.Release,
                    Pointer.Buttons: var buttons
                } &&
                (buttons & Buttons.Primary) != 0)
            {
                mutated = true;
                paragraph.Inlines.Insert(0, new DocumentTextRun("xx"));
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(document, new Point(0, 0));

        // Assert
        mutated.ShouldBeTrue();
        activations.ShouldBe(0);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("x");
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken)).ShouldBe(new Selection(0, 0));
    }

    /// <summary>Verifies a link that wraps stays one logical link and remains activatable on every
    /// line it occupies.</summary>
    [Fact]
    public async Task Pointer_WhenAWrappedLinkIsClickedOnEitherLine_ActivatesTheSameLinkAsync()
    {
        // Arrange
        var link = new DocumentLink("alpha beta");
        var document = LinkDocument(link);
        var activations = 0;
        link.Clicked += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("a");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("b");

        // Act
        await surface.Pointer.ClickAsync(document, new Point(0, 0));
        await surface.Pointer.ClickAsync(document, new Point(0, 1));

        // Assert
        activations.ShouldBe(2);
        document.ActiveLink.ShouldBeSameAs(link);
    }

    /// <summary>Verifies arrow, page, and endpoint keys scroll the focused document and are reported
    /// handled, so the keystroke can never escape and page an enclosing scroller out from under the
    /// still-focused document.</summary>
    [Fact]
    public async Task Keyboard_WhenScrollKeysArePressed_ScrollsTheDocumentAndNeverTheEnclosingScrollerAsync()
    {
        // Arrange
        var document = new Document { Height = Length.Cells(4), LineSize = 2, PageOverlap = 1 };

        for (var index = 0; index < 20; index++)
        {
            document.Blocks.Add(new DocumentParagraph(FormattableString.Invariant($"P{index}")));
        }

        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { document, new ControlText("tail") { Height = Length.Cells(20) } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        outer.Extent.Height.ShouldBeGreaterThan(outer.Viewport.Height);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        var page = document.Viewport.Height - document.PageOverlap;

        // Act and assert - one line step.
        await surface.Keyboard.PressAsync(Code.Down);
        document.VerticalOffset.ShouldBe(document.LineSize);

        await surface.Keyboard.PressAsync(Code.Up);
        document.VerticalOffset.ShouldBe(0);

        // Act and assert - one page step.
        await surface.Keyboard.PressAsync(Code.PageDown);
        document.VerticalOffset.ShouldBe(page);

        await surface.Keyboard.PressAsync(Code.PageUp);
        document.VerticalOffset.ShouldBe(0);

        // Act and assert - endpoints, including a saturating press at the end.
        await surface.Keyboard.PressAsync(Code.End);
        var maximum = document.Extent.Height - document.Viewport.Height;
        document.VerticalOffset.ShouldBe(maximum);

        await surface.Keyboard.PressAsync(Code.Down);
        document.VerticalOffset.ShouldBe(maximum);

        await surface.Keyboard.PressAsync(Code.Home);
        document.VerticalOffset.ShouldBe(0);

        // Assert - the enclosing scroller never moved for any of them.
        outer.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies moving to a link below the viewport reveals it by scrolling, so keyboard
    /// navigation never selects something the reader cannot see.</summary>
    [Fact]
    public async Task Keyboard_WhenTabReachesALinkBelowTheViewport_ScrollsToRevealItAsync()
    {
        // Arrange
        var document = new Document();

        for (var index = 0; index < 10; index++)
        {
            document.Blocks.Add(new DocumentParagraph(FormattableString.Invariant($"P{index}")));
        }

        var deep = new DocumentLink("deep");
        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(deep);
        document.Blocks.Add(paragraph);
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(20, 5),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        document.VerticalOffset.ShouldBe(0);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        document.ActiveLink.ShouldBeSameAs(deep);
        document.VerticalOffset.ShouldBeGreaterThan(0);
        surface.Cell(new Point(0, 4)).Text.ShouldBe("d");
    }

    /// <summary>Verifies moving to a link in an intrinsic table column beyond the right edge reveals
    /// it through the document's horizontal viewport.</summary>
    [Fact]
    public async Task Keyboard_WhenTabReachesALinkBeyondTheRightEdge_ScrollsToRevealItAsync()
    {
        // Arrange
        var link = new DocumentLink("link");
        var linkCell = new DocumentTableCell { Inlines = { link } };
        var table = new DocumentTable
        {
            Rows =
            {
                new DocumentTableRow
                {
                    Cells = { new DocumentTableCell("abcdefghij"), linkCell }
                }
            }
        };
        var document = new Document { Blocks = { table } };
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(5, 3),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        document.ActiveLink.ShouldBeSameAs(link);
        var snapshot = await surface.Application.Dispatcher.InvokeAsync(
            document.GetSelectableTextSnapshot,
            TestContext.Current.CancellationToken);
        snapshot.Glyphs.ShouldContain(glyph => glyph.Range.Length == 1 &&
            document.SelectionMap.Text[glyph.Range.Start] == 'l');
        (await surface.Application.Dispatcher.InvokeAsync(
            () => document.ScrollSelectableTextViewport(-100, 0),
            TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    /// <summary>Verifies disabling a mounted document cascades disabled visual state, refuses link
    /// activation, holds geometry stable across a genuine resize, and recovers on re-enable.</summary>
    [Fact]
    public async Task Enabled_WhenDocumentIsDisabledAndReenabled_CascadesStateAndPreservesGeometryAsync()
    {
        // Arrange
        var link = new DocumentLink("only");
        var document = LinkDocument(link);
        var activations = 0;
        link.Clicked += (_, _) => activations++;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => document.IsEnabled = false, "disable document");

        // Assert
        surface.ShouldHaveState(document, VisualState.Disabled);
        document.EffectiveIsEnabled.ShouldBeFalse();

        // Act - a disabled document routes nothing to its links.
        await surface.Pointer.ClickAsync(document, new Point(0, 0));

        // Assert
        activations.ShouldBe(0);

        // Act resize while disabled
        await surface.ResizeAsync(new Size(8, 2));

        // Assert
        document.Bounds.Width.ShouldBe(8);

        // Act re-enable
        await surface.UpdateAsync(() => document.IsEnabled = true, "re-enable document");

        // Assert
        surface.ShouldHaveState(document, VisualState.Normal);
        await surface.Pointer.ClickAsync(document, new Point(0, 0));
        activations.ShouldBe(1);
    }

    /// <summary>Verifies a link carrying a target stamps an OSC 8 hyperlink onto every cell it
    /// paints, and that a link without one leaves the cells unlinked.</summary>
    [Fact]
    public void Render_WhenALinkCarriesATarget_StampsTheHyperlinkOnEveryCellItPaints()
    {
        // Arrange
        var targeted = new DocumentLink("docs", "https://example.test/docs");
        var plain = new DocumentLink("plain");
        var document = LinkDocument(targeted, plain);

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 4));

        // Assert - the target covers the link's whole width, not just its first cell.
        probe.Row(0).ShouldBe("docs");
        probe.Cell(0, 0).Style.Hyperlink.ShouldBe("https://example.test/docs");
        probe.Cell(3, 0).Style.Hyperlink.ShouldBe("https://example.test/docs");
        probe.Row(2).ShouldBe("plain");
        probe.Cell(0, 2).Style.Hyperlink.ShouldBeNull();
    }

    /// <summary>Verifies a resting <see cref="DocumentLinkEmphasis.Action"/> link paints a solid,
    /// opaque chip distinct from an ordinary <see cref="DocumentLinkEmphasis.Standard"/> link's
    /// transparent inline look, with no focus involved.</summary>
    /// <remarks>
    /// Mounted rather than a detached <see cref="DocumentRenderProbe"/>: without a real theme every
    /// semantic color falls back to the same sentinel value, which would make an opaque chip and a
    /// transparent inline link resolve to identical background colors by coincidence and hide the
    /// very distinction this test exists to prove.
    /// </remarks>
    [Fact]
    public async Task Render_WhenEmphasisIsAction_PaintsASolidChipDistinctFromAStandardLinkAsync()
    {
        // Arrange
        var standard = new DocumentLink("plain");
        var action = new DocumentLink("chip") { Emphasis = DocumentLinkEmphasis.Action };
        var document = LinkDocument(standard, action);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 4),
            TestContext.Current.CancellationToken);

        // Assert - Standard stays transparent, matching ordinary body text; Action fills a solid,
        // distinct background - the built-in call-to-action chip look - entirely without focus.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("p");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("c");
        surface.Cell(new Point(0, 2)).Style.Background.ShouldNotBe(surface.Cell(new Point(0, 0)).Style.Background);
    }

    /// <summary>Verifies the focused-link face also differs by emphasis: moving focus to an
    /// <see cref="DocumentLinkEmphasis.Action"/> link paints a face distinct from a focused
    /// <see cref="DocumentLinkEmphasis.Standard"/> link's, and the standard link's highlight clears
    /// once focus moves on.</summary>
    [Fact]
    public async Task Render_WhenFocusMovesToAnActionLink_UsesADistinctActiveFaceFromAStandardLinkAsync()
    {
        // Arrange
        var standard = new DocumentLink("plain");
        var action = new DocumentLink("chip") { Emphasis = DocumentLinkEmphasis.Action };
        var document = LinkDocument(standard, action);
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");

        // Act - Tab to the standard link first, then on to the action link.
        await surface.Keyboard.PressAsync(Code.Tab);
        var standardActiveStyle = surface.Cell(new Point(0, 0)).Style;
        await surface.Keyboard.PressAsync(Code.Tab);
        var actionActiveStyle = surface.Cell(new Point(0, 2)).Style;
        var standardAfterMovingOnStyle = surface.Cell(new Point(0, 0)).Style;

        // Assert
        document.ActiveLink.ShouldBeSameAs(action);
        actionActiveStyle.ShouldNotBe(standardActiveStyle);
        standardAfterMovingOnStyle.ShouldNotBe(standardActiveStyle);
    }

    /// <summary>Verifies a disabled link resolves to the one shared disabled face regardless of
    /// emphasis: disabling already communicates the link's state, so an action-emphasis link needs
    /// no separate grayed-out chip variant.</summary>
    [Fact]
    public void Render_WhenAnActionLinkIsDisabled_UsesTheSameDisabledFaceAsAStandardDisabledLink()
    {
        // Arrange
        var disabledStandard = new DocumentLink("plain") { IsEnabled = false };
        var disabledAction = new DocumentLink("chip")
        {
            Emphasis = DocumentLinkEmphasis.Action,
            IsEnabled = false
        };
        var document = LinkDocument(disabledStandard, disabledAction);

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 4));

        // Assert
        probe.Cell(0, 0).Style.ShouldBe(probe.Cell(0, 2).Style);
    }

    /// <summary>Verifies a <c>Clicked</c> handler that removes the just-activated link's own
    /// paragraph from <c>Blocks</c>, still inside the same activation dispatch, does not corrupt
    /// state or throw: the reconciliation the removal triggers must tolerate the active link no
    /// longer existing in the freshly rebuilt link sequence.</summary>
    [Fact]
    public async Task Keyboard_WhenClickedHandlerRemovesItsOwnParagraphDuringActivation_DoesNotThrowAsync()
    {
        // Arrange
        var link = new DocumentLink("only");
        var paragraph = new DocumentParagraph();
        paragraph.Inlines.Add(link);
        var document = new Document { Blocks = { paragraph } };
        link.Clicked += (_, _) => document.Blocks.Remove(paragraph);
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        document.Blocks.ShouldBeEmpty();
        document.ActiveLink.ShouldBeNull();
    }

    /// <summary>Verifies a <c>Clicked</c> handler that reassigns <c>ActiveLink</c> to a different,
    /// still-attached link, still inside the same activation dispatch, leaves that reassignment in
    /// effect rather than being silently overwritten by the activation path it interrupted.</summary>
    [Fact]
    public async Task Keyboard_WhenClickedHandlerReassignsActiveLinkDuringActivation_LeavesTheReassignmentInEffectAsync()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        first.Clicked += (_, _) => document.ActiveLink = second;
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        document.ActiveLink.ShouldBeSameAs(second);
    }

    /// <summary>Verifies a <c>Clicked</c> handler that calls <c>Load</c> again, replacing the whole
    /// tree, still inside the same activation dispatch, does not throw and leaves the document
    /// showing only the freshly loaded content.</summary>
    [Fact]
    public async Task Keyboard_WhenClickedHandlerReloadsTheWholeTreeDuringActivation_DoesNotThrowAsync()
    {
        // Arrange
        var link = new DocumentLink("only");
        var document = LinkDocument(link);
        var reader = new PlainTextDocumentReaderProbe();
        link.Clicked += (_, _) => document.Load("replacement", reader);
        await using var surface = await ComponentSurface.MountAsync(
            document,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => document.Focus().ShouldBeTrue(), "focus document");
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        document.Blocks.ShouldHaveSingleItem().ShouldBeOfType<DocumentParagraph>().Inlines
            .ShouldHaveSingleItem().ShouldBeOfType<DocumentTextRun>().Text.ShouldBe("replacement");
        document.ActiveLink.ShouldBeNull();
    }

    private static Document LinkDocument(params DocumentLink[] links)
    {
        var document = new Document();

        foreach (var link in links)
        {
            var paragraph = new DocumentParagraph();
            paragraph.Inlines.Add(link);
            document.Blocks.Add(paragraph);
        }

        return document;
    }
}
