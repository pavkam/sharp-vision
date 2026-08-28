// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Documents.Document;

/// <summary>Verifies <see cref="Document"/>'s detached contract: documented defaults, block
/// ownership, scrolling state and commands, and focused-link selection.</summary>
public sealed class DocumentTests
{
    /// <summary>Verifies non-structural node mutations never rescan embedded-control membership,
    /// while a collection edit reconciles it exactly once.</summary>
    [Fact]
    public void NodeMutation_WhenStructureIsUnchanged_DoesNotReconcileEmbeddedControls()
    {
        // Arrange
        var text = new DocumentTextRun("before");
        var link = new DocumentLink("docs", "https://example.test/one");
        var paragraph = new DocumentParagraph { Inlines = { text, link } };
        var code = new DocumentCodeBlock("code") { Language = "csharp" };
        var document = new Document { Blocks = { paragraph, code } };
        var reconciliations = document.ControlReconciliationCount;

        // Act
        text.Text = "after";
        link.Target = "https://example.test/two";
        link.Emphasis = DocumentLinkEmphasis.Action;
        code.Language = "fsharp";

        // Assert
        document.ControlReconciliationCount.ShouldBe(reconciliations);

        paragraph.Inlines.Add(new DocumentInlineControl(new CheckBox("new")));
        document.ControlReconciliationCount.ShouldBe(reconciliations + 1);
    }

    /// <summary>Verifies a new document is empty, stretches, and takes part in tab navigation as a
    /// single focusable stop.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var document = new Document();

        // Assert
        document.Blocks.Count.ShouldBe(0);
        document.IsFocusable.ShouldBeTrue();
        document.IsTabStop.ShouldBeTrue();
        document.TabNavigation.ShouldBe(TabNavigation.None);
        document.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        document.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        document.VerticalOffset.ShouldBe(0);
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);
    }

    /// <summary>Verifies direct and ancestor-inherited IsEnabled changes compute the effective
    /// disabled state the document's uniform dimming depends on.</summary>
    [Fact]
    public void IsEnabled_WhenDisabledDirectlyOrByAncestor_ComputesEffectiveState()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("Body") } };
        var host = new Stack { Children = { document } };

        // Act and assert
        document.EffectiveIsEnabled.ShouldBeTrue();
        document.IsEnabled = false;
        document.EffectiveIsEnabled.ShouldBeFalse();

        // Act and assert re-enable
        document.IsEnabled = true;
        document.EffectiveIsEnabled.ShouldBeTrue();

        // Act and assert ancestor
        host.IsEnabled = false;
        document.EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies content taller than the viewport reports a scrollable extent.</summary>
    [Fact]
    public void Extent_WhenContentExceedsTheViewport_ExceedsTheViewportHeight()
    {
        // Arrange
        var document = Filled(20);

        // Act
        new LayoutEngine().Layout(document, new Size(40, 5));

        // Assert
        document.Viewport.Height.ShouldBe(5);
        document.Extent.Height.ShouldBeGreaterThan(document.Viewport.Height);
    }

    /// <summary>Verifies a resize that makes the current scroll position unreachable clamps the
    /// offset to the new endpoint.</summary>
    [Fact]
    public void VerticalOffset_WhenViewportGrows_ClampsToTheNewExtent()
    {
        // Arrange
        var document = Filled(8);
        var engine = new LayoutEngine();
        engine.Layout(document, new Size(20, 3));
        _ = document.ScrollToEnd();
        document.VerticalOffset.ShouldBeGreaterThan(0);

        // Act
        engine.Layout(document, new Size(20, 20));

        // Assert
        document.VerticalOffset.ShouldBe(0);
        document.Viewport.Height.ShouldBe(20);
    }

    /// <summary>Verifies a signed line delta moves the vertical offset and reports the change.</summary>
    [Fact]
    public void ScrollBy_WhenContentExceedsTheViewport_MovesTheVerticalOffset()
    {
        // Arrange
        var document = Filled(20);
        new LayoutEngine().Layout(document, new Size(40, 5));

        // Act
        var moved = document.ScrollBy(3);

        // Assert
        moved.ShouldBeTrue();
        document.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies a delta past either endpoint saturates instead of overscrolling, and that a
    /// request that cannot move the offset reports no change.</summary>
    [Fact]
    public void ScrollBy_WhenDeltaPassesAnEndpoint_ClampsAndReportsNoFurtherChange()
    {
        // Arrange
        var document = Filled(20);
        new LayoutEngine().Layout(document, new Size(40, 5));
        var maximum = document.Extent.Height - document.Viewport.Height;

        // Act
        var moved = document.ScrollBy(1000);

        // Assert
        moved.ShouldBeTrue();
        document.VerticalOffset.ShouldBe(maximum);
        document.ScrollBy(1000).ShouldBeFalse();
    }

    /// <summary>Verifies an undefined scroll cause is rejected.</summary>
    [Fact]
    public void ScrollBy_WhenCauseIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var document = Filled(20);
        new LayoutEngine().Layout(document, new Size(40, 5));

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.ScrollBy(1, (ScrollCause) 99));
    }

    /// <summary>Verifies the endpoint commands move to the first and last lines and report whether
    /// they changed anything.</summary>
    [Fact]
    public void ScrollToEnd_WhenContentExceedsTheViewport_MovesToTheEndpointsAndBack()
    {
        // Arrange
        var document = Filled(20);
        new LayoutEngine().Layout(document, new Size(40, 5));
        var maximum = document.Extent.Height - document.Viewport.Height;

        // Act
        var toEnd = document.ScrollToEnd();

        // Assert
        toEnd.ShouldBeTrue();
        document.VerticalOffset.ShouldBe(maximum);
        document.ScrollToEnd().ShouldBeFalse();

        // Act
        var toTop = document.ScrollToTop();

        // Assert
        toTop.ShouldBeTrue();
        document.VerticalOffset.ShouldBe(0);
        document.ScrollToTop().ShouldBeFalse();
    }

    /// <summary>Verifies a document whose content fits reports no movement from either endpoint
    /// command.</summary>
    [Fact]
    public void ScrollToEnd_WhenContentFits_ReportsNoChange()
    {
        // Arrange
        var document = Filled(1);
        new LayoutEngine().Layout(document, new Size(40, 10));

        // Act and assert
        document.ScrollToEnd().ShouldBeFalse();
        document.ScrollToTop().ShouldBeFalse();
        document.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies the vertical offset is settable within the extent and rejected outside it.</summary>
    [Fact]
    public void VerticalOffset_WhenAssigned_AcceptsValidOffsetsAndRejectsOthers()
    {
        // Arrange
        var document = Filled(20);
        new LayoutEngine().Layout(document, new Size(40, 5));
        var maximum = document.Extent.Height - document.Viewport.Height;

        // Act
        document.VerticalOffset = 4;

        // Assert
        document.VerticalOffset.ShouldBe(4);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.VerticalOffset = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.VerticalOffset = maximum + 1);
        document.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies the line and page metrics are settable and validated.</summary>
    [Fact]
    public void LineSize_WhenAssigned_ValidatesTheScrollingMetrics()
    {
        // Arrange
        var document = new Document { LineSize = 3, PageOverlap = 2 };

        // Act and assert
        document.LineSize.ShouldBe(3);
        document.PageOverlap.ShouldBe(2);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.LineSize = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.PageOverlap = -1);
        document.LineSize.ShouldBe(3);
        document.PageOverlap.ShouldBe(2);
    }

    /// <summary>Verifies the generated bar's visibility policy is settable and validated.</summary>
    [Fact]
    public void ShowScrollBars_WhenAssigned_AcceptsDefinedPoliciesOnly()
    {
        // Arrange
        var document = new Document { ShowScrollBars = ShowScrollBars.Always };

        // Act and assert
        document.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.ShowScrollBars = (ShowScrollBars) 99);
        document.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
    }

    /// <summary>Verifies the document forwards its private host's scroll notification.</summary>
    [Fact]
    public void ScrollChanged_WhenOffsetCommits_RaisesWithTheDocumentAsSender()
    {
        // Arrange
        var document = Filled(20);
        new LayoutEngine().Layout(document, new Size(40, 5));
        object? sender = null;
        ScrollChangedEventArgs? observed = null;
        document.ScrollChanged += (raiser, eventArgs) =>
        {
            sender = raiser;
            observed = eventArgs;
        };

        // Act
        _ = document.ScrollBy(2);

        // Assert
        sender.ShouldBeSameAs(document);
        _ = observed.ShouldNotBeNull();
    }

    /// <summary>Verifies reentry from an earlier ScrollChanged subscriber, triggered through the
    /// stack-forwarding path (<c>OnStackScrollChanged</c>), prevents later subscribers from receiving
    /// the obsolete outer transition.</summary>
    [Fact]
    public void ScrollChanged_WhenStackForwardingSubscriberReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var document = Filled(20);
        new LayoutEngine().Layout(document, new Size(40, 5));
        var observed = new List<Point>();
        document.ScrollChanged += (_, eventArgs) =>
        {
            if (eventArgs.Offset == new Point(0, 2))
            {
                _ = document.ScrollBy(1);
            }
        };
        document.ScrollChanged += (_, eventArgs) => observed.Add(eventArgs.Offset);

        // Act
        _ = document.ScrollBy(2);

        // Assert
        observed.ShouldBe([new Point(0, 3)]);
    }

    /// <summary>Verifies reentry from an earlier ScrollChanged subscriber, triggered through the
    /// document's own horizontal-scroll path (<c>ApplyHorizontal</c>), prevents later subscribers
    /// from receiving the obsolete outer transition.</summary>
    [Fact]
    public void ScrollChanged_WhenHorizontalSubscriberReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentCodeBlock(new string('x', 40)) } };
        new LayoutEngine().Layout(document, new Size(10, 5));
        var observed = new List<Point>();
        document.ScrollChanged += (_, eventArgs) =>
        {
            if (eventArgs.Offset == new Point(2, 0))
            {
                _ = document.ScrollSelectableTextViewport(1, 0);
            }
        };
        document.ScrollChanged += (_, eventArgs) => observed.Add(eventArgs.Offset);

        // Act
        _ = document.ScrollSelectableTextViewport(2, 0);

        // Assert
        observed.ShouldBe([new Point(3, 0)]);
    }

    /// <summary>Verifies assigning an enabled link that belongs to the laid-out document selects it,
    /// and that clearing the selection reports no active link.</summary>
    [Fact]
    public void ActiveLink_WhenAssignedAnOwnedEnabledLink_SelectsIt()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        new LayoutEngine().Layout(document, new Size(40, 5));

        // Act
        document.ActiveLink = second;

        // Assert
        document.ActiveLink.ShouldBeSameAs(second);
        document.ActiveLinkIndex.ShouldBe(1);

        // Act clear
        document.ActiveLink = null;

        // Assert
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);
    }

    /// <summary>Verifies a disabled or foreign link clears the selection instead of selecting
    /// something the document cannot activate.</summary>
    [Fact]
    public void ActiveLink_WhenLinkIsDisabledOrForeign_ClearsTheSelection()
    {
        // Arrange
        var first = new DocumentLink("first");
        var disabled = new DocumentLink("second") { IsEnabled = false };
        var document = LinkDocument(first, disabled);
        new LayoutEngine().Layout(document, new Size(40, 5));
        document.ActiveLink = first;
        document.ActiveLinkIndex.ShouldBe(0);

        // Act
        document.ActiveLink = disabled;

        // Assert
        document.ActiveLinkIndex.ShouldBe(-1);

        // Act foreign
        document.ActiveLink = first;
        document.ActiveLink = new DocumentLink("foreign");

        // Assert
        document.ActiveLinkIndex.ShouldBe(-1);
    }

    /// <summary>Verifies disabling the selected link clears selection synchronously, before another
    /// layout pass has an opportunity to rebuild link regions.</summary>
    [Fact]
    public void ActiveLink_WhenSelectedLinkIsDisabled_ClearsImmediately()
    {
        // Arrange
        var link = new DocumentLink("link");
        var document = LinkDocument(link);
        new LayoutEngine().Layout(document, new Size(20, 2));
        document.ActiveLink = link;

        // Act
        link.IsEnabled = false;

        // Assert
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);
    }

    /// <summary>Verifies owned links cannot be selected before the first projection and no latent
    /// selection appears when layout later includes a visible link but omits an empty one.</summary>
    [Fact]
    public void ActiveLink_WhenOwnedLinksAreAssignedBeforeLayout_LeavesSelectionEmptyUntilProjected()
    {
        // Arrange
        var empty = new DocumentLink();
        var visible = new DocumentLink("visible");
        var document = LinkDocument(empty, visible);

        // Act and assert: no projection exists yet.
        document.ActiveLink = visible;
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);
        document.ActiveLink = empty;
        document.ActiveLink.ShouldBeNull();

        // Act and assert: first layout does not resurrect either rejected assignment.
        new LayoutEngine().Layout(document, new Size(20, 2));
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);

        // Act and assert: projection membership now distinguishes the two owned links.
        document.ActiveLink = empty;
        document.ActiveLink.ShouldBeNull();
        document.ActiveLink = visible;
        document.ActiveLink.ShouldBeSameAs(visible);
        document.ActiveLinkIndex.ShouldBe(0);
    }

    /// <summary>Verifies an owned link added after layout cannot be selected from a stale
    /// projection, then becomes selectable after the next layout includes it.</summary>
    [Fact]
    public void ActiveLink_WhenOwnedLinkIsAbsentFromLatestProjection_CannotSelectUntilReprojected()
    {
        // Arrange
        var first = new DocumentLink("first");
        var added = new DocumentLink("added");
        var document = LinkDocument(first);
        new LayoutEngine().Layout(document, new Size(20, 2));
        var addedParagraph = new DocumentParagraph();
        addedParagraph.Inlines.Add(added);
        document.Blocks.Add(addedParagraph);

        // Act
        document.ActiveLink = added;

        // Assert
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);

        // Act
        new LayoutEngine().Layout(document, new Size(20, 2));
        document.ActiveLink = added;

        // Assert
        document.ActiveLink.ShouldBeSameAs(added);
        document.ActiveLinkIndex.ShouldBe(1);
    }

    /// <summary>Verifies removing the selected link's paragraph by position clears selection before
    /// another layout can observe the detached node.</summary>
    [Fact]
    public void ActiveLink_WhenSelectedLinkIsRemovedAt_ClearsSynchronously()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        new LayoutEngine().Layout(document, new Size(40, 5));
        document.ActiveLink = second;
        document.ActiveLinkIndex.ShouldBe(1);

        // Act
        document.Blocks.RemoveAt(1);

        // Assert
        document.ActiveLinkIndex.ShouldBe(-1);
        document.ActiveLink.ShouldBeNull();
    }

    /// <summary>Verifies removing the selected link's paragraph by identity clears selection before
    /// another layout can observe the detached node.</summary>
    [Fact]
    public void ActiveLink_WhenSelectedLinkIsRemoved_ClearsSynchronously()
    {
        // Arrange
        var link = new DocumentLink("only");
        var document = LinkDocument(link);
        new LayoutEngine().Layout(document, new Size(20, 2));
        document.ActiveLink = link;

        // Act
        document.Blocks.Remove(document.Blocks[0]).ShouldBeTrue();

        // Assert
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);
    }

    /// <summary>Verifies clearing a tree that contains the selected link clears selection before
    /// another layout can observe the detached node.</summary>
    [Fact]
    public void ActiveLink_WhenBlocksAreCleared_ClearsSynchronously()
    {
        // Arrange
        var link = new DocumentLink("only");
        var document = LinkDocument(link);
        new LayoutEngine().Layout(document, new Size(20, 2));
        document.ActiveLink = link;

        // Act
        document.Blocks.Clear();

        // Assert
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);
    }

    /// <summary>Verifies loading a replacement tree clears a selected link from the previous tree
    /// before the replacement is laid out.</summary>
    [Fact]
    public void ActiveLink_WhenLoadReplacesTheTree_ClearsSynchronously()
    {
        // Arrange
        var link = new DocumentLink("only");
        var document = LinkDocument(link);
        new LayoutEngine().Layout(document, new Size(20, 2));
        document.ActiveLink = link;

        // Act
        _ = document.Load("replacement", new PlainTextDocumentReaderProbe());

        // Assert
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);
    }

    /// <summary>Verifies rebuilding after an earlier sibling is removed preserves the selected link
    /// by identity instead of clearing it because its ordinal changed.</summary>
    [Fact]
    public void ActiveLink_WhenAnEarlierLinkLeavesTheTree_PreservesTheSelectedLink()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        var engine = new LayoutEngine();
        engine.Layout(document, new Size(40, 5));
        document.ActiveLink = second;

        // Act
        document.Blocks.RemoveAt(0);
        engine.Layout(document, new Size(40, 5));

        // Assert
        document.ActiveLink.ShouldBeSameAs(second);
        document.ActiveLinkIndex.ShouldBe(0);
    }

    /// <summary>Verifies removing the selected first link does not silently transfer selection to
    /// the next link that inherits its old ordinal.</summary>
    [Fact]
    public void ActiveLink_WhenSelectedFirstLinkLeavesTheTree_DoesNotSelectItsSuccessor()
    {
        // Arrange
        var first = new DocumentLink("first");
        var second = new DocumentLink("second");
        var document = LinkDocument(first, second);
        var engine = new LayoutEngine();
        engine.Layout(document, new Size(40, 5));
        document.ActiveLink = first;

        // Act
        document.Blocks.RemoveAt(0);
        engine.Layout(document, new Size(40, 5));

        // Assert
        document.ActiveLink.ShouldBeNull();
        document.ActiveLinkIndex.ShouldBe(-1);
    }

    /// <summary>Verifies public active-link mutation observes the document's dispatcher affinity.</summary>
    [Fact]
    public async Task ActiveLink_WhenAssignedOffDispatcher_ThrowsBeforeSelectionChangesAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var link = new DocumentLink("link");
        var document = LinkDocument(link);
        new LayoutEngine().Layout(document, new Size(20, 2));
        await dispatcher.InvokeAsync(
            () => document.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        var action = () => document.ActiveLink = link;

        // Assert
        _ = action.ShouldThrow<InvalidOperationException>();
        document.ActiveLink.ShouldBeNull();
    }

    private static Document Filled(int paragraphs)
    {
        var document = new Document();

        for (var index = 0; index < paragraphs; index++)
        {
            document.Blocks.Add(new DocumentParagraph(FormattableString.Invariant($"Paragraph {index}")));
        }

        return document;
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
