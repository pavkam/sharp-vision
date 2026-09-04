// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Text;

using Document = Controls.Document.Document;

/// <summary>Verifies the internal semantic stream and cell index that protect document selection
/// from visual wrapping, decorative chrome, and retained-control presentation details.</summary>
public sealed class DocumentSelectionTests
{
    /// <summary>Verifies Document exposes its specialized semantic projection through the common control contract.</summary>
    [Fact]
    public void TextSelection_WhenAccessedThroughControlBase_UsesDocumentSelectionState()
    {
        // Arrange
        var document = new Document();
        document.Blocks.Add(new DocumentParagraph("Alpha Beta"));
        ControlBase control = document;

        // Act
        control.SetTextSelection(new Selection(6, 10));

        // Assert
        control.IsTextSelectionEnabled.ShouldBeTrue();
        control.TextSelection.ShouldBe(document.Selection);
        control.SelectedText.ShouldBe("Beta");
        control.CopySelectedText().ShouldBe(document.CopySelection());
    }

    /// <summary>Verifies repeated source identity resolves by exact semantic occurrence and exact
    /// duplicate records choose the first occurrence deterministically.</summary>
    [Fact]
    public void ResolveSourceOccurrence_WhenIdentityRepeats_UsesRangeThenFirstExactOccurrence()
    {
        // Arrange
        var probe = new DocumentSelectionSourceProbe();
        var first = new TextSelectionSource(probe, probe, new Selection(0, 5), "Probe", 0);
        var second = new TextSelectionSource(probe, probe, new Selection(6, 11), "Probe", 0);
        var duplicate = new TextSelectionSource(probe, probe, new Selection(6, 11), "Probe", 0);
        var map = new TextSelectionMap("Probe\nProbe", [], [first, second, duplicate], 0);
        var capturedSecond = new TextSelectionSource(probe, probe, new Selection(6, 11), "Probe", 0);

        // Act
        var resolved = map.ResolveSourceOccurrence(capturedSecond);

        // Assert
        resolved.ShouldBeSameAs(second);
    }

    /// <summary>Verifies the public selection commands preserve direction and expose normalized text.</summary>
    [Fact]
    public void SelectionCommands_WhenRangeIsDirectional_ExposeOwnedSemanticText()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("Alpha Beta") } };
        using var probe = new DocumentRenderProbe(document, new Size(20, 3));

        // Act
        document.SetSelection(new Selection(10, 6));

        // Assert
        document.Selection.ShouldBe(new Selection(10, 6));
        document.SelectedText.ShouldBe("Beta");
        document.CopySelection().ShouldBe("Beta");

        // Act and assert
        document.ClearSelection();
        document.Selection.ShouldBe(new Selection(6, 6));
        document.SelectedText.ShouldBeEmpty();
        document.SelectAll();
        document.Selection.ShouldBe(new Selection(0, 10));
        document.CopySelection().ShouldBe("Alpha Beta");
    }

    /// <summary>Verifies selecting an empty semantic stream and clearing its initial caret are no-ops.</summary>
    [Fact]
    public void SelectAll_WhenDocumentIsEmpty_RemainsSafelyCollapsed()
    {
        // Arrange
        var document = new Document();
        using var probe = new DocumentRenderProbe(document, new Size(5, 2));
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;

        // Act
        document.SelectAll();
        document.ClearSelection();

        // Assert
        document.Selection.ShouldBe(default);
        document.SelectedText.ShouldBeEmpty();
        changes.ShouldBe(0);
    }

    /// <summary>Verifies semantic selection can project an inline control before any layout has
    /// established its one-row geometry.</summary>
    [Fact]
    public void SelectAll_WhenInlineControlHasNotBeenLaidOut_SelectsItsSemanticText()
    {
        // Arrange
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph
                {
                    Inlines = { new DocumentInlineControl(new Button("inline")) }
                }
            }
        };

        // Act
        document.SelectAll();

        // Assert
        document.SelectedText.ShouldBe("inline");
        document.GetSelectableTextSnapshot().Text.ShouldBe("inline");
    }

    /// <summary>Verifies invalid endpoint and grapheme boundaries fail before observable state changes.</summary>
    [Fact]
    public void SetSelection_WhenRangeIsInvalid_ThrowsWithoutMutationOrNotification()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("e\u0301x") } };
        using var probe = new DocumentRenderProbe(document, new Size(10, 2));
        document.SetSelection(new Selection(0, 2));
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => document.SetSelection(new Selection(0, 1)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => document.SetSelection(new Selection(0, 4)));
        document.Selection.ShouldBe(new Selection(0, 2));
        document.SelectedText.ShouldBe("e\u0301");
        changes.ShouldBe(0);
    }

    /// <summary>Verifies notifications occur once per committed directional value, never for repeats.</summary>
    [Fact]
    public void SelectionChanged_WhenSelectionActuallyChanges_RaisesExactlyOncePerCommit()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        using var probe = new DocumentRenderProbe(document, new Size(10, 2));
        var observed = new List<Selection>();
        document.SelectionChanged += (_, _) => observed.Add(document.Selection);

        // Act
        document.SetSelection(new Selection(1, 3));
        document.SetSelection(new Selection(1, 3));
        document.SetSelection(new Selection(3, 1));
        document.ClearSelection();

        // Assert
        observed.ShouldBe([
            new Selection(1, 3),
            new Selection(3, 1),
            new Selection(1, 1)
        ]);
    }

    /// <summary>Verifies reentry from the compatibility event cannot publish an obsolete common
    /// transition after the newer committed selection.</summary>
    [Fact]
    public void TextSelectionChanged_WhenSelectionChangedReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        using var probe = new DocumentRenderProbe(document, new Size(10, 2));
        var observed = new List<(Selection EventSelection, Selection LiveSelection)>();
        document.SelectionChanged += (_, _) =>
        {
            if (document.Selection == new Selection(0, 1))
            {
                document.SetSelection(new Selection(0, 2));
            }
        };
        document.TextSelectionChanged += (_, eventArgs) =>
            observed.Add((eventArgs.Selection, document.Selection));

        // Act
        document.SetSelection(new Selection(0, 1));

        // Assert
        observed.ShouldBe([(new Selection(0, 2), new Selection(0, 2))]);
    }

    /// <summary>Verifies a compatibility subscriber that reenters cannot let a later subscriber on
    /// the same event observe a transition already superseded during delivery.</summary>
    [Fact]
    public void SelectionChanged_WhenSubscriberReenters_DoesNotRedeliverToLaterSubscriber()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("abcd") } };
        using var probe = new DocumentRenderProbe(document, new Size(10, 2));
        var raised = 0;
        document.SelectionChanged += (_, _) =>
        {
            if (document.Selection == new Selection(0, 1))
            {
                document.SetSelection(new Selection(0, 2));
            }
        };
        document.SelectionChanged += (_, _) => raised++;

        // Act
        document.SetSelection(new Selection(0, 1));

        // Assert
        raised.ShouldBe(1);
    }

    /// <summary>Verifies geometry-only rebuilds preserve logical endpoints and selected text.</summary>
    [Fact]
    public void Selection_WhenLayoutReflows_PreservesSemanticRange()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("alpha beta gamma") } };
        using var first = new DocumentRenderProbe(document, new Size(20, 3));
        document.SetSelection(new Selection(6, 10));

        // Act
        using var second = new DocumentRenderProbe(document, new Size(7, 6));

        // Assert
        document.Selection.ShouldBe(new Selection(6, 10));
        document.SelectedText.ShouldBe("beta");
    }

    /// <summary>Verifies semantic mutation clears once while an equivalent reconstruction preserves.</summary>
    [Fact]
    public void Selection_WhenSemanticProjectionChanges_ClearsOnceAfterRebuild()
    {
        // Arrange
        var run = new DocumentTextRun("Before");
        var document = new Document
        {
            Blocks = { new DocumentParagraph { Inlines = { run } } }
        };
        using var first = new DocumentRenderProbe(document, new Size(20, 3));
        document.SelectAll();
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;

        // Act - replacing markup with an equivalent displayed projection must preserve selection.
        run.Text = "<b>Before</b>";
        using var equivalent = new DocumentRenderProbe(document, new Size(20, 3));

        // Assert
        document.Selection.ShouldBe(new Selection(0, 6));
        changes.ShouldBe(0);

        // Act - semantic text now changes.
        run.Text = "After";
        using var changed = new DocumentRenderProbe(document, new Size(20, 3));
        _ = document.MeasureContent(20, force: true);

        // Assert
        document.Selection.ShouldBe(default);
        document.SelectedText.ShouldBeEmpty();
        changes.ShouldBe(1);
    }

    /// <summary>Verifies semantic mutation resets a nonzero collapsed caret before it can become invalid.</summary>
    [Fact]
    public void Selection_WhenCollapsedCaretOutlivesSemanticText_ResetsToDocumentStart()
    {
        // Arrange
        var run = new DocumentTextRun("Before");
        var document = new Document
        {
            Blocks = { new DocumentParagraph { Inlines = { run } } }
        };
        using var first = new DocumentRenderProbe(document, new Size(20, 3));
        document.SetSelection(new Selection(6, 6));
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;

        // Act
        run.Text = "A";
        using var changed = new DocumentRenderProbe(document, new Size(20, 3));

        // Assert
        document.Selection.ShouldBe(default);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies replacing a retained semantic source clears selection even when text is identical.</summary>
    [Fact]
    public void Selection_WhenEmbeddedSourceIdentityChanges_ClearsAfterRebuild()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentBlockControl(new Button("Same")) } };
        using var first = new DocumentRenderProbe(document, new Size(16, 4));
        document.SelectAll();
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;

        // Act
        document.Blocks.Clear();
        document.Blocks.Add(new DocumentBlockControl(new Button("Same")));
        using var changed = new DocumentRenderProbe(document, new Size(16, 4));

        // Assert
        document.Selection.ShouldBe(default);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies a retained source's own semantic mutation is detected before a selection read,
    /// even though it did not invalidate the document's semantic-tree cache directly.</summary>
    [Fact]
    public void SelectedText_WhenEmbeddedSourceMutates_ReconcilesAndClearsExactlyOnce()
    {
        // Arrange
        var button = new Button("before");
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        using var probe = new DocumentRenderProbe(document, new Size(16, 4));
        document.SelectAll();
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;

        // Act
        button.Text = "after!";
        var selected = document.SelectedText;
        var selection = document.Selection;

        // Assert
        selected.ShouldBeEmpty();
        selection.ShouldBe(default);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies pending semantic reconciliation is transactional when a proposed selection
    /// is invalid against the prospective stream.</summary>
    [Fact]
    public void SetSelection_WhenPendingSourceMutationMakesEndpointInvalid_ThrowsBeforeReconciliation()
    {
        // Arrange
        var button = new Button("longer");
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        using var probe = new DocumentRenderProbe(document, new Size(16, 4));
        document.SetSelection(new Selection(5, 6));
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;
        button.Text = "tiny";

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => document.SetSelection(new Selection(5, 6)));

        // Assert - the failed transaction itself publishes nothing. The next read owns reconciliation.
        exception.ParamName.ShouldBe("selection");
        changes.ShouldBe(0);
        document.Selection.ShouldBe(default);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies disposing a retained source is projected as semantic removal rather than
    /// leaking that child's lifetime exception through document selection reads.</summary>
    [Fact]
    public void SelectedText_WhenEmbeddedSourceIsDisposed_ClearsOnceAndRemainsStable()
    {
        // Arrange
        var button = new Button("before");
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        using var probe = new DocumentRenderProbe(document, new Size(16, 4));
        document.SelectAll();
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;

        // Act
        button.Dispose();
        var firstText = document.SelectedText;
        var firstSelection = document.Selection;
        var secondText = document.SelectedText;

        // Assert
        firstText.ShouldBeEmpty();
        firstSelection.ShouldBe(default);
        secondText.ShouldBeEmpty();
        changes.ShouldBe(1);
    }

    /// <summary>Verifies an explicit valid selection after source mutation observes semantic clear
    /// before applying the requested range to the new stream.</summary>
    [Fact]
    public void SetSelection_WhenPendingSourceMutationKeepsRangeValid_PublishesClearThenSelection()
    {
        // Arrange
        var button = new Button("before");
        var document = new Document { Blocks = { new DocumentBlockControl(button) } };
        using var probe = new DocumentRenderProbe(document, new Size(16, 4));
        var range = new Selection(1, 4);
        document.SetSelection(range);
        var observed = new List<Selection>();
        document.SelectionChanged += (_, _) => observed.Add(document.Selection);
        button.Text = "after!";

        // Act
        document.SetSelection(range);

        // Assert
        observed.ShouldBe([default, range]);
        document.Selection.ShouldBe(range);
        document.SelectedText.ShouldBe("fte");
    }

    /// <summary>Verifies semantic mutation discovered while descendants arrange clears an active
    /// selection before the arrange transaction returns, without waiting for a public getter.</summary>
    [Fact]
    public void Arrange_WhenEmbeddedSourceChangesSemanticText_ClearsSelectionImmediately()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe { ChangesTextAfterArrange = true };
        var document = new Document { Blocks = { new DocumentBlockControl(source) } };
        document.Measure(new Constraint(10, 2));
        document.SelectAll();
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;

        // Act
        document.Arrange(new Rect(0, 0, 10, 2));

        // Assert - event count is read before any selection getter can trigger lazy reconciliation.
        changes.ShouldBe(1);
        document.Selection.ShouldBe(default);
        document.SelectionMap.Text.ShouldBe("After");
    }

    /// <summary>Verifies harmless repeated reads compare cheap invalidation versions rather than
    /// materializing a complete selectable snapshot on every access.</summary>
    [Fact]
    public void SelectionReads_WhenEmbeddedSourceIsUnchanged_DoNotRebuildSnapshots()
    {
        // Arrange
        var source = new CountingDocumentSelectionSource(new string('a', 10_000));
        var document = new Document { Blocks = { new DocumentBlockControl(source) } };
        using var probe = new DocumentRenderProbe(document, new Size(20, 2));
        document.SetSelection(new Selection(0, 1));
        var initialCalls = source.SnapshotCalls;

        // Act
        for (var index = 0; index < 100; index++)
        {
            _ = document.Selection;
            _ = document.SelectedText;
        }

        // Assert
        source.SnapshotCalls.ShouldBe(initialCalls);

        // Act - one invalidation causes one exact rebuild, then the new version is cached.
        source.SetText('b' + new string('a', 9_999));
        _ = document.SelectedText;
        var rebuiltCalls = source.SnapshotCalls;
        _ = document.Selection;
        _ = document.SelectedText;

        // Assert
        rebuiltCalls.ShouldBe(initialCalls + 1);
        source.SnapshotCalls.ShouldBe(rebuiltCalls);
    }

    /// <summary>Verifies an attached document rejects off-dispatcher selection before mutation.</summary>
    [Fact]
    public async Task SetSelection_WhenAttachedOffDispatcher_ThrowsBeforeMutationAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var document = new Document { Blocks = { new DocumentParagraph("text") } };
        new LayoutEngine().Layout(document, new Size(10, 2));
        await dispatcher.InvokeAsync(
            () => document.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        // Act
        Action action = () => document.SetSelection(new Selection(0, 2));

        // Assert
        _ = action.ShouldThrow<InvalidOperationException>();
        var selection = await dispatcher.InvokeAsync(
            () => document.Selection,
            TestContext.Current.CancellationToken);
        selection.ShouldBe(default);
    }

    /// <summary>Verifies disposed selection commands reject access without changing retained state.</summary>
    [Fact]
    public void SetSelection_WhenDisposed_ThrowsBeforeMutation()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("text") } };
        using var probe = new DocumentRenderProbe(document, new Size(10, 2));
        var changes = 0;
        document.SelectionChanged += (_, _) => changes++;
        document.Dispose();

        // Act
        Action action = () => document.SetSelection(new Selection(0, 2));

        // Assert
        _ = action.ShouldThrow<ObjectDisposedException>();
        changes.ShouldBe(0);
    }
    /// <summary>Verifies inline breaks, block boundaries, wrapping, markup, and decorative blocks
    /// produce one deterministic semantic stream independent of painted rows.</summary>
    [Fact]
    public void SelectionMap_WhenFlowContainsStructure_UsesSemanticSeparators()
    {
        // Arrange
        var flow = new DocumentParagraph
        {
            Inlines =
            {
                new DocumentTextRun("A <b>soft</b>"),
                new DocumentSoftBreak(),
                new DocumentLink("link"),
                new DocumentLineBreak(),
                new DocumentTextRun("hard")
            }
        };
        var document = new Document
        {
            Blocks =
            {
                flow,
                new DocumentSeparator(),
                new DocumentParagraph("wrapped words")
            }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(7, 8));

        // Assert
        document.SelectionMap.Text.ShouldBe("A soft link\nhard\nwrapped words");
        document.SelectionMap.Glyphs.ShouldContain(glyph => glyph.Range == new Selection(6, 7));
        probe.Rows().ShouldContain("wrapped");
        probe.Rows().ShouldContain("words");
    }

    /// <summary>Verifies list markers, table delimiters, and code line endings use normalized
    /// clipboard-oriented text rather than their painted chrome.</summary>
    [Fact]
    public void SelectionMap_WhenBlocksHavePlainTextConventions_UsesCanonicalValues()
    {
        // Arrange
        var bullets = new DocumentList(DocumentListKind.Bulleted)
        {
            Items = { new DocumentListItem("One"), new DocumentListItem("Two") }
        };
        var numbers = new DocumentList(DocumentListKind.Numbered)
        {
            Start = 4,
            Items = { new DocumentListItem("Four"), new DocumentListItem("Five") }
        };
        var table = new DocumentTable
        {
            Rows =
            {
                new DocumentTableRow { Cells = { new DocumentTableCell("A"), new DocumentTableCell("B") } },
                new DocumentTableRow { Cells = { new DocumentTableCell("1"), new DocumentTableCell("2") } }
            }
        };
        var document = new Document
        {
            Blocks = { bullets, numbers, table, new DocumentCodeBlock("x\tq\r\ny\rz") }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(30, 16));

        // Assert
        document.SelectionMap.Text.ShouldBe("- One\n- Two\n4. Four\n5. Five\nA\tB\n1\t2\nx\tq\ny\nz");
        document.SelectionMap.Text.ShouldNotContain("\r");
        var codeTab = document.SelectionMap.Text.IndexOf("\tq", StringComparison.Ordinal);
        document.SelectionMap.Glyphs.Single(glyph => glyph.Range == new Selection(codeTab, codeTab + 1))
            .Bounds.Width.ShouldBe(3);
        var orderedStart = document.SelectionMap.Text.IndexOf("4. Four", StringComparison.Ordinal);
        document.SelectionMap.Glyphs.ShouldContain(glyph =>
            glyph.Range == new Selection(orderedStart + 1, orderedStart + 2));
    }

    /// <summary>Verifies quote and callout bars remain presentation while their semantic content is
    /// retained, and thematic rules contribute no copied characters.</summary>
    [Fact]
    public void SelectionMap_WhenBlocksHaveChrome_ExcludesDecorativeGlyphs()
    {
        // Arrange
        var callout = new DocumentCallout { Kind = "NOTE", Title = "Title" };
        callout.Blocks.Add(new DocumentParagraph("Body"));
        var document = new Document
        {
            Blocks =
            {
                new DocumentBlockQuote("Quote"),
                callout,
                new DocumentSeparator()
            }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(24, 10));

        // Assert
        document.SelectionMap.Text.ShouldBe("Quote\n[NOTE] Title\nBody");
        document.SelectionMap.Text.ShouldNotContain("│");
        document.SelectionMap.Text.ShouldNotContain("─");
    }

    /// <summary>Verifies embedded control text occupies its exact inline and block positions and
    /// carries translated glyph geometry plus source and viewport identity.</summary>
    [Fact]
    public void SelectionMap_WhenControlsAreEmbedded_PreservesSourceMetadataAndGeometry()
    {
        // Arrange
        var inline = new CheckBox("Choice");
        var block = new Button("Submit");
        var document = new Document
        {
            Blocks =
            {
                new DocumentParagraph
                {
                    Inlines =
                    {
                        new DocumentTextRun("Pick "),
                        new DocumentInlineControl(inline),
                        new DocumentTextRun(" now")
                    }
                },
                new DocumentBlockControl(block)
            }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(30, 8));
        var map = document.SelectionMap;

        // Assert
        map.Text.ShouldBe("Pick Choice now\nSubmit");
        map.Sources.Count.ShouldBe(2);
        map.Sources[0].Source.ShouldBeSameAs(inline);
        map.Sources[0].Viewport.ShouldBeNull();
        map.Sources[1].Source.ShouldBeSameAs(block);
        map.Glyphs.ShouldContain(glyph =>
            glyph.Source != null &&
            ReferenceEquals(glyph.Source.Source, inline) &&
            glyph.Bounds.X >= inline.Bounds.X);
        map.Glyphs.ShouldContain(glyph =>
            glyph.Source != null &&
            ReferenceEquals(glyph.Source.Source, block) &&
            glyph.Bounds.Y >= block.Bounds.Y);
    }

    /// <summary>Verifies row-indexed hit testing returns only grapheme boundaries and uses the wide
    /// grapheme midpoint to choose its before or after endpoint.</summary>
    [Fact]
    public void HitTest_WhenRowContainsWideGrapheme_UsesGraphemeMidpoint()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("A界B") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(10, 3));
        var map = document.SelectionMap;

        // Assert
        map.HitTest(new Point(1, 0)).ShouldBe(1);
        map.HitTest(new Point(2, 0)).ShouldBe(2);
        map.Glyphs.Single(glyph => glyph.Range == new Selection(1, 2)).Bounds.Width.ShouldBe(2);
    }

    /// <summary>Verifies a multi-code-unit grapheme has one indivisible semantic range and one cell
    /// rectangle instead of exposing a caret boundary inside its combining sequence.</summary>
    [Fact]
    public void SelectionMap_WhenTextContainsCombiningGrapheme_KeepsOneRange()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("e\u0301X") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(8, 2));

        // Assert
        document.SelectionMap.Glyphs[0].Range.ShouldBe(new Selection(0, 2));
        document.SelectionMap.Glyphs.ShouldNotContain(glyph => glyph.Range.Start == 1 || glyph.Range.End == 1);
    }

    /// <summary>Verifies a semantic zero-cell grapheme remains copyable without fabricating geometry
    /// or shifting the following painted grapheme away from its layout column.</summary>
    [Fact]
    public void SelectionMap_WhenTextContainsZeroCellGrapheme_DoesNotAdvanceGeometry()
    {
        // Arrange
        var document = new Document { Blocks = { new DocumentParagraph("A\u0001B") } };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(8, 2));
        var map = document.SelectionMap;

        // Assert
        map.Text.ShouldBe("A\u0001B");
        map.Glyphs.ShouldNotContain(glyph => glyph.Range == new Selection(1, 2));
        map.Glyphs.Single(glyph => glyph.Range == new Selection(2, 3)).Bounds.X.ShouldBe(1);
    }

    /// <summary>Verifies an embedded selectable viewport is retained with the same source record
    /// used by its translated glyphs.</summary>
    [Fact]
    public void SelectionMap_WhenEmbeddedSourceHasViewport_PreservesViewportIdentity()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe();
        var document = new Document
        {
            Blocks = { new DocumentBlockControl(source) }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(10, 2));
        var selectionSource = document.SelectionMap.Sources.ShouldHaveSingleItem();

        // Assert
        selectionSource.Source.ShouldBeSameAs(source);
        selectionSource.Viewport.ShouldBeSameAs(source);
        document.SelectionMap.Glyphs.ShouldAllBe(glyph => ReferenceEquals(glyph.Source, selectionSource));
    }

    /// <summary>Verifies a source that changes semantic text between measure and arrange rebuilds
    /// the committed semantic stream and geometry before the arrange transaction completes.</summary>
    [Fact]
    public void SelectionMap_WhenEmbeddedSourceChangesDuringArrange_RebuildsCommittedProjection()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe { ChangesTextAfterArrange = true };
        var document = new Document
        {
            Blocks = { new DocumentBlockControl(source) }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(10, 2));

        // Assert
        document.SelectionMap.Text.ShouldBe("After");
        var selectionSource = document.SelectionMap.Sources.ShouldHaveSingleItem();
        selectionSource.Text.ShouldBe("After");
        document.SelectionMap.Glyphs.ShouldContain(glyph => ReferenceEquals(glyph.Source, selectionSource));
    }

    /// <summary>Verifies embedded glyphs use the committed margin-deflated control origin rather
    /// than the outer placement slot that the document planned during measure.</summary>
    [Fact]
    public void SelectionMap_WhenEmbeddedSourceHasMargin_UsesCommittedPaintOrigin()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe
        {
            Margin = new Thickness(left: 2, top: 0, right: 0, bottom: 0)
        };
        var document = new Document
        {
            Blocks = { new DocumentBlockControl(source) }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 4));
        var first = document.SelectionMap.Glyphs.First(glyph => glyph.Source != null);

        // Assert
        first.Bounds.X.ShouldBe(source.Bounds.X);
        first.Bounds.Y.ShouldBe(source.Bounds.Y);
        probe.Text(first.Bounds.X, first.Bounds.Y).ShouldBe("P");
        document.SelectionMap.HitTest(new Point(first.Bounds.X, first.Bounds.Y)).ShouldBe(0);
    }

    /// <summary>Verifies row indexing sorts arbitrary source order and resolves duplicate and
    /// overlapping rectangles through stable spatial then semantic precedence.</summary>
    [Fact]
    public void HitTest_WhenGlyphOrderAndRectanglesConflict_UsesDeterministicSpatialOrder()
    {
        // Arrange
        var reversed = new TextSelectionMap(
            "ab",
            [
                new TextSelectionGlyph(new Selection(1, 2), new Rect(1, 0, 1, 1)),
                new TextSelectionGlyph(new Selection(0, 1), new Rect(0, 0, 1, 1))
            ],
            [],
            lineCount: 1);
        var overlaps = new TextSelectionMap(
            "abc",
            [
                new TextSelectionGlyph(new Selection(2, 3), new Rect(0, 0, 3, 1)),
                new TextSelectionGlyph(new Selection(1, 2), new Rect(0, 0, 3, 1)),
                new TextSelectionGlyph(new Selection(0, 1), new Rect(1, 0, 1, 1))
            ],
            [],
            lineCount: 1);

        // Act and assert
        reversed.HitTest(new Point(0, 0)).ShouldBe(0);
        reversed.HitTest(new Point(1, 0)).ShouldBe(1);
        overlaps.HitTest(new Point(0, 0)).ShouldBe(1);
        overlaps.HitTest(new Point(1, 0)).ShouldBe(1);
    }

    /// <summary>Verifies a far-right pointer query uses bounded row-index probes rather than
    /// revisiting every preceding glyph on a long non-overlapping visual line.</summary>
    [Fact]
    public void HitTest_WhenRowHasTenThousandGlyphs_ExaminesBoundedIndexEntries()
    {
        // Arrange
        const int count = 10_000;
        var glyphs = new TextSelectionGlyph[count];

        for (var index = 0; index < glyphs.Length; index++)
        {
            glyphs[index] = new TextSelectionGlyph(
                new Selection(index, index + 1),
                new Rect(index * 2, 0, 1, 1));
        }

        var map = new TextSelectionMap(new string('a', count), glyphs, [], lineCount: 1);

        // Act
        var offset = map.HitTest(new Point((count - 1) * 2, 0), out var inspectedEntries);

        // Assert
        offset.ShouldBe(count - 1);
        inspectedEntries.ShouldBeLessThan(40);
    }

    /// <summary>Verifies translated source rectangles and hit-test midpoint or gap arithmetic
    /// saturate instead of wrapping at representable coordinate limits.</summary>
    [Fact]
    public void SelectionMap_WhenCoordinatesAreExtreme_SaturatesAndUsesWideArithmetic()
    {
        // Arrange
        var source = new DocumentSelectionSourceProbe
        {
            Margin = new Thickness(left: 1, top: 0, right: 0, bottom: 0),
            FirstGlyphBoundsOverride = new Rect(int.MaxValue, 0, int.MaxValue, 1)
        };
        var document = new Document
        {
            Blocks = { new DocumentBlockControl(source) }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 2));
        var translated = document.SelectionMap.Glyphs.First(glyph => glyph.Source != null);
        var midpoint = new TextSelectionMap(
            "a",
            [new TextSelectionGlyph(new Selection(0, 1), new Rect(0, 0, int.MaxValue, 1))],
            [],
            lineCount: 1);
        var gap = new TextSelectionMap(
            "aXb",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(int.MinValue, 0, 1, 1)),
                new TextSelectionGlyph(new Selection(2, 3), new Rect(int.MaxValue, 0, 1, 1))
            ],
            [],
            lineCount: 1);

        // Assert
        translated.Bounds.X.ShouldBe(int.MaxValue);
        midpoint.HitTest(new Point(int.MaxValue - 1, 0)).ShouldBe(1);
        gap.HitTest(new Point(0, 0)).ShouldBe(2);
    }

    /// <summary>Verifies a painted blank row resolves to the preceding semantic boundary on an
    /// equidistant tie instead of scanning or inventing a separator offset.</summary>
    [Fact]
    public void HitTest_WhenVisualRowIsEmpty_ReturnsDeterministicNearestOffset()
    {
        // Arrange
        var document = new Document
        {
            Blocks = { new DocumentParagraph("First"), new DocumentParagraph("Second") }
        };

        // Act
        using var probe = new DocumentRenderProbe(document, new Size(12, 4));

        // Assert
        document.SelectionMap.HitTest(new Point(4, 1)).ShouldBe("First".Length);
    }

    /// <summary>Verifies the map's exact source-aware fingerprint remains stable across equivalent
    /// rebuilds and changes when semantic text changes.</summary>
    [Fact]
    public void Fingerprint_WhenProjectionChanges_DistinguishesSemanticMutation()
    {
        // Arrange
        var paragraph = new DocumentParagraph("Before");
        var document = new Document { Blocks = { paragraph } };
        using var first = new DocumentRenderProbe(document, new Size(20, 3));
        var fingerprint = document.SelectionMap.Fingerprint;
        _ = document.MeasureContent(20, force: true);
        document.SelectionMap.Fingerprint.ShouldBe(fingerprint);

        // Act
        paragraph.Inlines[0].ShouldBeOfType<DocumentTextRun>().Text = "After";
        using var second = new DocumentRenderProbe(document, new Size(20, 3));

        // Assert
        document.SelectionMap.Fingerprint.ShouldNotBe(fingerprint);
    }

    /// <summary>Verifies line-end navigation uses the farthest true visual edge rather than the
    /// last glyph origin when mapped glyph rectangles overlap.</summary>
    [Fact]
    public void VisualLineBoundary_WhenGlyphsOverlap_UsesTrueVisualExtents()
    {
        var map = new TextSelectionMap(
            "ab",
            [
                new TextSelectionGlyph(new Selection(0, 1), new Rect(0, 0, 10, 1)),
                new TextSelectionGlyph(new Selection(1, 2), new Rect(5, 0, 1, 1))
            ],
            [],
            1);

        map.VisualLineBoundary(0, end: false).ShouldBe(0);
        map.VisualLineBoundary(0, end: true).ShouldBe(1);
        map.TryGetVisualLineBoundary(0, end: true, out var boundary, out var bounds, out _).ShouldBeTrue();
        boundary.ShouldBe(1);
        bounds.ShouldBe(new Rect(0, 0, 10, 1));
        map.TryGetCaretGeometry(boundary, out var ambiguous, out _).ShouldBeTrue();
        ambiguous.ShouldBe(new Rect(5, 0, 1, 1));
    }

    /// <summary>Verifies large semantic maps use bounded binary indexes for grapheme movement,
    /// vertical lookup, and caret geometry resolution.</summary>
    [Fact]
    public void Navigation_WhenMapIsLarge_InspectsBoundedIndexEntries()
    {
        const int count = 10_000;
        var glyphs = new TextSelectionGlyph[count];
        for (var index = 0; index < count; index++)
        {
            glyphs[index] = new TextSelectionGlyph(new Selection(index, index + 1), new Rect(index, 0, 1, 1));
        }
        var map = new TextSelectionMap(new string('a', count), glyphs, [], 1);

        map.PreviousBoundary(9_999, out var previousInspected).ShouldBe(9_998);
        map.NextBoundary(9_999, out var nextInspected).ShouldBe(10_000);
        map.TryGetVisualPosition(9_999, out _, out _, out var visualInspected).ShouldBeTrue();
        map.TryGetCaretGeometry(9_999, out _, out _, out var revealInspected).ShouldBeTrue();

        previousInspected.ShouldBeLessThan(20);
        nextInspected.ShouldBeLessThan(20);
        visualInspected.ShouldBeLessThan(20);
        revealInspected.ShouldBeLessThan(20);
    }
}
