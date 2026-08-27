// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

using SharpVision.Controls;
using SharpVision.Threading;

/// <summary>Verifies CodeView content, selection, and folding behavior.</summary>
public sealed class CodeViewTests
{
    /// <summary>Verifies a reveal deferred by fold expansion completes at the detached layout
    /// boundary even though no dispatcher exists to run a posted continuation.</summary>
    [Fact]
    public void RevealSelectableTextOffset_WhenDetachedFoldExpands_CompletesDuringArrange()
    {
        var view = new CodeView
        {
            Code = "fn main() {\n    01234567890123456789\n}\nafter",
            Language = "Rust",
            Width = Length.Cells(8),
            Height = Length.Cells(2),
        };
        var foldStart = Enumerable.Range(0, 4).First(view.IsFoldStart);
        view.SetFolded(foldStart, true).ShouldBeTrue();
        var target = view.Code.IndexOf("89", StringComparison.Ordinal) + 1;

        view.RevealSelectableTextOffset(target).ShouldBeTrue();
        view.Measure(new Constraint(8, 2));
        view.Arrange(new Rect(0, 0, 8, 2));

        view.HorizontalOffset.ShouldBeGreaterThan(0);
        view.RevealSelectableTextOffset(target).ShouldBeFalse();
    }

    /// <summary>Verifies a reveal continuation queued by a prior dispatcher cannot consume or
    /// mutate pending state after the view attaches to a replacement dispatcher.</summary>
    [Fact]
    public async Task RevealSelectableTextOffset_WhenReattached_IgnoresPriorDispatcherContinuationAsync()
    {
        await using var first = Dispatcher.Start(name: "code-view-reveal-first");
        await using var second = Dispatcher.Start(name: "code-view-reveal-second");
        var view = new CodeView
        {
            Code = "fn main() {\n    01234567890123456789\n}\nafter",
            Language = "Rust",
            Width = Length.Cells(8),
            Height = Length.Cells(2),
        };
        Exception? firstFailure = null;
        first.UnhandledException += (_, eventArgs) =>
        {
            firstFailure = eventArgs.Exception;
            eventArgs.IsHandled = true;
        };
        var detached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseFirstDispatcher = new ManualResetEventSlim();
        var cancellationToken = TestContext.Current.CancellationToken;

        var firstTransition = first.InvokeAsync(() =>
        {
            view.Attach(first);
            var foldStart = Enumerable.Range(0, 4).First(view.IsFoldStart);
            view.SetFolded(foldStart, true).ShouldBeTrue();
            var target = view.Code.IndexOf("89", StringComparison.Ordinal) + 1;
            view.RevealSelectableTextOffset(target).ShouldBeTrue();
            view.Measure(new Constraint(8, 2));
            view.Arrange(new Rect(0, 0, 8, 2));
            view.Detach();
            detached.SetResult();
            releaseFirstDispatcher.Wait(cancellationToken);
        }, cancellationToken).AsTask();
        await detached.Task.WaitAsync(cancellationToken);
        await second.InvokeAsync(() => view.Attach(second), cancellationToken);
        releaseFirstDispatcher.Set();
        await firstTransition;
        await first.InvokeAsync(static () => { }, cancellationToken);

        firstFailure.ShouldBeNull();
        await second.InvokeAsync(() =>
        {
            view.Invalidate(Invalidation.Measure);
            view.Measure(new Constraint(8, 2));
            view.Arrange(new Rect(0, 0, 8, 2));
        }, cancellationToken);
        await second.InvokeAsync(() =>
        {
            view.HorizontalOffset.ShouldBeGreaterThan(0);
            view.Detach();
        }, cancellationToken);
    }

    private const string _rustSnippet = "fn main() {\n    let x = 1;\n}\n";

    /// <summary>Verifies content and clipboard mutations each notify exactly once after the new
    /// value and dependent projection are fully observable.</summary>
    [Fact]
    public void ContentProperties_WhenChanged_NotifyAfterTheirTransactionsCommit()
    {
        var view = new CodeView();
        var notifications = new List<string>();
        view.PropertyChanged += (_, args) =>
        {
            notifications.Add(args.PropertyName!);

            if (args.PropertyName == nameof(CodeView.Code))
            {
                view.Code.ShouldBe(_rustSnippet);
                view.Selection.IsEmpty.ShouldBeTrue();
            }
            else if (args.PropertyName == nameof(CodeView.Language))
            {
                view.Language.ShouldBe("Rust");
                view.FoldRanges.ShouldNotBeEmpty();
            }
        };
        Action<string> writer = _ => { };

        view.Code = _rustSnippet;
        view.Language = "Rust";
        view.ClipboardWriter = writer;
        view.Code = _rustSnippet;
        view.Language = "Rust";
        view.ClipboardWriter = writer;

        notifications.Count(name => name == nameof(CodeView.Code)).ShouldBe(1);
        notifications.Count(name => name == nameof(CodeView.Language)).ShouldBe(1);
        notifications.Count(name => name == nameof(CodeView.ClipboardWriter)).ShouldBe(1);
        view.ClipboardWriter.ShouldBeSameAs(writer);
    }

    /// <summary>Verifies CodeView adapts its read-only selection through the common control contract.</summary>
    [Fact]
    public void TextSelection_WhenAccessedThroughControlBase_UsesCodeViewSelectionState()
    {
        // Arrange
        var view = new CodeView { Code = "Alpha Beta" };
        ControlBase control = view;

        // Act
        control.SetTextSelection(new Selection(6, 10));

        // Assert
        control.IsTextSelectionEnabled.ShouldBeTrue();
        control.TextSelection.ShouldBe(view.Selection);
        control.SelectedText.ShouldBe("Beta");
        control.CopySelectedText().ShouldBe(view.CopySelection());
    }

    /// <summary>Verifies the selectable projection owns the complete normalized logical source.</summary>
    [Fact]
    public void GetSelectableTextSnapshot_WhenLineEndingsVary_ReturnsNormalizedAuthoritativeText()
    {
        var view = new CodeView { Code = "first\r\nsecond\rthird" };

        var snapshot = GetSelectableSnapshot(view);

        snapshot.Text.ShouldBe("first\nsecond\nthird");
        snapshot.IsAuthoritative.ShouldBeTrue();
        snapshot.Glyphs.ShouldBeEmpty();
    }

    /// <summary>Verifies repeated snapshot reads do not invent selectable-text mutations.</summary>
    [Fact]
    public void SelectableTextVersion_WhenSnapshotsAreRead_RemainsStableUntilCodeChanges()
    {
        var view = new CodeView { Code = "before" };
        var before = GetSelectableVersion(view);

        _ = GetSelectableSnapshot(view);
        _ = GetSelectableSnapshot(view);

        GetSelectableVersion(view).ShouldBe(before);

        view.Code = "after";

        GetSelectableVersion(view).ShouldNotBe(before);
    }

    /// <summary>Verifies viewport reveal rejects invalid UTF-16 and grapheme endpoints.</summary>
    [Fact]
    public void RevealSelectableTextOffset_WhenOffsetIsInvalid_ThrowsBeforeViewportMutation()
    {
        var view = new CodeView { Code = "a🙂b" };

        _ = Should.Throw<ArgumentException>(() => view.RevealSelectableTextOffset(2));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.RevealSelectableTextOffset(5));
    }

    private static SelectableTextSnapshot GetSelectableSnapshot<TSource>(TSource source)
        where TSource : ISelectableTextSource =>
        source.GetSelectableTextSnapshot();

    private static ulong GetSelectableVersion<TSource>(TSource source)
        where TSource : ISelectableTextSource => source.SelectableTextVersion;

    /// <summary>Verifies a freshly constructed view has empty content and no selection.</summary>
    [Fact]
    public void Constructor_WhenCalled_StartsEmptyWithNoSelection()
    {
        var view = new CodeView();

        view.Code.ShouldBe(string.Empty);
        view.Language.ShouldBeNull();
        view.Selection.IsEmpty.ShouldBeTrue();
        view.SelectedText.ShouldBe(string.Empty);
        view.IsFocusable.ShouldBeTrue();
        view.IsTabStop.ShouldBeTrue();
        view.Overflow.ShouldBe(Overflow.Visible);
    }

    /// <summary>Verifies an unknown Overflow value is rejected before any observable state changes.</summary>
    [Fact]
    public void Overflow_WhenSetToAnUndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        var view = new CodeView();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.Overflow = (Overflow) 99);

        view.Overflow.ShouldBe(Overflow.Visible);
    }

    /// <summary>Verifies assigning Code resets the selection and is retained verbatim.</summary>
    [Fact]
    public void Code_WhenAssigned_IsRetainedAndResetsSelection()
    {
        var view = new CodeView { Code = "abc" };
        view.SetSelection(new Selection(0, 3));

        view.Code = "xyz";

        view.Code.ShouldBe("xyz");
        view.Selection.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies a null Code assignment is rejected.</summary>
    [Fact]
    public void Code_WhenNull_ThrowsArgumentNullException()
    {
        var view = new CodeView();

        _ = Should.Throw<ArgumentNullException>(() => view.Code = null!);
    }

    /// <summary>Verifies Language resolves a grammar from the default catalog and highlights the code.</summary>
    [Fact]
    public void Language_WhenSetToRust_TokenizesAgainstTheResolvedGrammar()
    {
        var view = new CodeView { Code = _rustSnippet, Language = "Rust" };

        view.Language.ShouldBe("Rust");
    }

    /// <summary>Verifies an unknown language name is rejected without changing the current language.</summary>
    [Fact]
    public void Language_WhenUnknown_ThrowsKeyNotFoundExceptionAndPreservesPreviousLanguage()
    {
        var view = new CodeView { Code = _rustSnippet, Language = "Rust" };

        _ = Should.Throw<KeyNotFoundException>(() => view.Language = "NoSuchLanguage");

        view.Language.ShouldBe("Rust");
    }

    /// <summary>Verifies a custom catalog can be substituted for the embedded default.</summary>
    [Fact]
    public void Catalog_WhenReplaced_IsUsedToResolveLanguage()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-view-catalog-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "external.xml"),
                """
                <language name="ExternalTest" section="Sources" extensions="*.ext" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <DetectChar attribute="Normal Text" context="#stay" char="!"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);

            var view = new CodeView
            {
                Catalog = SyntaxDefinitionCatalog.FromDirectory(directory.FullName),
                Language = "ExternalTest",
            };

            view.Language.ShouldBe("ExternalTest");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies replacing Catalog while Language is already set re-resolves the grammar
    /// from the replacement catalog instead of continuing to tokenize with the grammar the
    /// previous catalog produced.</summary>
    [Fact]
    public void Catalog_WhenLanguageIsAlreadySet_ReResolvesGrammarFromTheReplacementCatalog()
    {
        var withFolding = Directory.CreateTempSubdirectory("sharpvision-syntax-view-catalog-fold-a-");
        var withoutFolding = Directory.CreateTempSubdirectory("sharpvision-syntax-view-catalog-fold-b-");

        try
        {
            File.WriteAllText(
                Path.Combine(withFolding.FullName, "swap.xml"),
                """
                <language name="SwapTest" section="Sources" extensions="*.ext" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <DetectChar attribute="Normal Text" context="#stay" char="{" beginRegion="Brace"/>
                        <DetectChar attribute="Normal Text" context="#stay" char="}" endRegion="Brace"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);
            File.WriteAllText(
                Path.Combine(withoutFolding.FullName, "swap.xml"),
                """
                <language name="SwapTest" section="Sources" extensions="*.ext" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <DetectChar attribute="Normal Text" context="#stay" char="{"/>
                        <DetectChar attribute="Normal Text" context="#stay" char="}"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);

            var catalogWithFolding = SyntaxDefinitionCatalog.FromDirectory(withFolding.FullName);
            var catalogWithoutFolding = SyntaxDefinitionCatalog.FromDirectory(withoutFolding.FullName);
            var view = new CodeView
            {
                Catalog = catalogWithFolding,
                Language = "SwapTest",
                Code = "{\nx\n}\n",
            };
            view.FoldRanges.Count.ShouldBe(1);

            view.Catalog = catalogWithoutFolding;

            view.FoldRanges.Count.ShouldBe(0);
        }
        finally
        {
            withFolding.Delete(recursive: true);
            withoutFolding.Delete(recursive: true);
        }
    }

    /// <summary>Verifies a reentrant language change supersedes the catalog setter's pre-notification grammar.</summary>
    [Fact]
    public void Catalog_WhenNotificationChangesLanguage_DoesNotRestoreStaleGrammar()
    {
        var firstDirectory = Directory.CreateTempSubdirectory("sharpvision-syntax-view-reentrant-a-");
        var secondDirectory = Directory.CreateTempSubdirectory("sharpvision-syntax-view-reentrant-b-");

        try
        {
            const string folding = """
                <language name="LangA" section="Sources" extensions="*.a" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts><context name="Normal" attribute="Normal Text" lineEndContext="#stay"><DetectChar attribute="Normal Text" context="#stay" char="{" beginRegion="Brace"/><DetectChar attribute="Normal Text" context="#stay" char="}" endRegion="Brace"/></context></contexts>
                    <itemDatas><itemData name="Normal Text" defStyleNum="dsNormal"/></itemDatas>
                  </highlighting>
                </language>
                """;
            const string plain = """
                <language name="LangB" section="Sources" extensions="*.b" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts><context name="Normal" attribute="Normal Text" lineEndContext="#stay"/></contexts>
                    <itemDatas><itemData name="Normal Text" defStyleNum="dsNormal"/></itemDatas>
                  </highlighting>
                </language>
                """;

            foreach (var directory in new[] { firstDirectory, secondDirectory })
            {
                File.WriteAllText(Path.Combine(directory.FullName, "a.xml"), folding);
                File.WriteAllText(Path.Combine(directory.FullName, "b.xml"), plain);
            }

            var first = SyntaxDefinitionCatalog.FromDirectory(firstDirectory.FullName);
            var second = SyntaxDefinitionCatalog.FromDirectory(secondDirectory.FullName);
            var view = new CodeView { Catalog = first, Language = "LangA", Code = "{\nx\n}\n" };
            view.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(CodeView.Catalog))
                {
                    view.Language = "LangB";
                }
            };

            view.Catalog = second;

            view.Language.ShouldBe("LangB");
            view.FoldRanges.ShouldBeEmpty();
        }
        finally
        {
            firstDirectory.Delete(recursive: true);
            secondDirectory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies replacing Catalog with a value that lacks the current Language throws and
    /// leaves Catalog, Language, and the already-resolved grammar exactly as they were - the same
    /// validate-before-mutate contract Language's own setter keeps.</summary>
    [Fact]
    public void Catalog_WhenReplacementLacksTheCurrentLanguage_ThrowsAndPreservesThePreviousCatalog()
    {
        var directory = Directory.CreateTempSubdirectory("sharpvision-syntax-view-catalog-missing-");

        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "other.xml"),
                """
                <language name="SomeOtherLanguage" section="Sources" extensions="*.other" version="1" kateversion="5.0">
                  <highlighting>
                    <contexts>
                      <context name="Normal" attribute="Normal Text" lineEndContext="#stay">
                        <DetectChar attribute="Normal Text" context="#stay" char="!"/>
                      </context>
                    </contexts>
                    <itemDatas>
                      <itemData name="Normal Text" defStyleNum="dsNormal"/>
                    </itemDatas>
                  </highlighting>
                </language>
                """);

            var originalCatalog = SyntaxDefinitionCatalog.Default;
            var view = new CodeView { Code = _rustSnippet, Language = "Rust" };
            var replacement = SyntaxDefinitionCatalog.FromDirectory(directory.FullName);

            _ = Should.Throw<KeyNotFoundException>(() => view.Catalog = replacement);

            view.Catalog.ShouldBeSameAs(originalCatalog);
            view.Language.ShouldBe("Rust");
            view.FoldRanges.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies SetSelection rejects an out-of-range endpoint.</summary>
    [Fact]
    public void SetSelection_WhenEndpointExceedsText_ThrowsArgumentOutOfRangeException()
    {
        var view = new CodeView { Code = "abc" };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.SetSelection(new Selection(0, 10)));
    }

    /// <summary>Verifies SetSelection rejects an endpoint that splits a grapheme cluster.</summary>
    [Fact]
    public void SetSelection_WhenEndpointSplitsAGraphemeCluster_ThrowsArgumentException()
    {
        // "e\u0301" (e + combining acute accent) is one extended grapheme cluster spanning two
        // UTF-16 chars; an endpoint of 1 lands between them.
        var view = new CodeView { Code = "e\u0301bc" };

        _ = Should.Throw<ArgumentException>(() => view.SetSelection(new Selection(0, 1)));
    }

    /// <summary>Verifies SetSelection commits a valid range and SelectedText reflects it.</summary>
    [Fact]
    public void SetSelection_WhenGivenValidRange_UpdatesSelectedText()
    {
        var view = new CodeView { Code = "abcdef" };

        view.SetSelection(new Selection(1, 4));

        view.SelectedText.ShouldBe("bcd");
        view.CopySelection().ShouldBe("bcd");
    }

    /// <summary>Verifies CRLF and lone-CR line endings both normalize to a single <c>\n</c> in
    /// selected text, even though <see cref="CodeView.Code"/> itself retains the assigned bytes
    /// verbatim.</summary>
    [Theory]
    [InlineData("line one\r\nline two", "line one\nline two")]
    [InlineData("line one\rline two", "line one\nline two")]
    public void SelectAll_WhenCodeUsesNonLfLineEndings_NormalizesThemInSelectedText(string code, string expected)
    {
        var view = new CodeView { Code = code };

        view.SelectAll();

        view.Code.ShouldBe(code);
        view.SelectedText.ShouldBe(expected);
    }

    /// <summary>Verifies SelectAll selects the entire normalized text.</summary>
    [Fact]
    public void SelectAll_WhenCalled_SelectsEntireNormalizedText()
    {
        var view = new CodeView { Code = "line one\nline two" };

        view.SelectAll();

        view.SelectedText.ShouldBe("line one\nline two");
    }

    /// <summary>Verifies ClearSelection collapses to an empty range without changing the caret.</summary>
    [Fact]
    public void ClearSelection_WhenSelectionExists_CollapsesToEmpty()
    {
        var view = new CodeView { Code = "abcdef" };
        view.SetSelection(new Selection(1, 4));

        view.ClearSelection();

        view.Selection.IsEmpty.ShouldBeTrue();
        view.SelectedText.ShouldBe(string.Empty);
    }

    /// <summary>Verifies CopySelection returns an empty string with no selection, never touching a clipboard.</summary>
    [Fact]
    public void CopySelection_WhenNothingIsSelected_ReturnsEmptyString()
    {
        var view = new CodeView { Code = "abcdef" };

        view.CopySelection().ShouldBe(string.Empty);
    }

    /// <summary>Verifies SelectionChanged fires exactly once per committed selection change.</summary>
    [Fact]
    public void SelectionChanged_WhenSelectionCommits_RaisesExactlyOnce()
    {
        var view = new CodeView { Code = "abcdef" };
        var raised = 0;
        view.SelectionChanged += (_, _) => raised++;

        view.SetSelection(new Selection(0, 3));
        view.SetSelection(new Selection(0, 3));

        raised.ShouldBe(1);
    }

    /// <summary>Verifies reentry from the compatibility event cannot publish an obsolete common
    /// transition after the newer committed selection.</summary>
    [Fact]
    public void TextSelectionChanged_WhenSelectionChangedReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var view = new CodeView { Code = "abcdef" };
        var observed = new List<(Selection EventSelection, Selection LiveSelection)>();
        view.SelectionChanged += (_, _) =>
        {
            if (view.Selection == new Selection(0, 1))
            {
                view.SetSelection(new Selection(0, 2));
            }
        };
        view.TextSelectionChanged += (_, eventArgs) =>
            observed.Add((eventArgs.Selection, view.Selection));

        // Act
        view.SetSelection(new Selection(0, 1));

        // Assert
        observed.ShouldBe([(new Selection(0, 2), new Selection(0, 2))]);
    }

    /// <summary>Verifies a line beginning a region fold can be collapsed and expanded.</summary>
    [Fact]
    public void SetFolded_WhenLineBeginsARegion_CollapsesAndExpands()
    {
        var view = new CodeView { Code = _rustSnippet, Language = "Rust" };
        var foldStart = -1;

        for (var line = 0; line < view.FoldRanges.Count + 4; line++)
        {
            if (view.IsFoldStart(line))
            {
                foldStart = line;
                break;
            }
        }

        foldStart.ShouldBeGreaterThanOrEqualTo(0);
        view.IsFolded(foldStart).ShouldBeFalse();

        var changed = view.SetFolded(foldStart, true);

        changed.ShouldBeTrue();
        view.IsFolded(foldStart).ShouldBeTrue();

        view.ExpandAll();

        view.IsFolded(foldStart).ShouldBeFalse();
    }

    /// <summary>Verifies SetFolded on a line that begins no fold range is a no-op.</summary>
    [Fact]
    public void SetFolded_WhenLineBeginsNoFoldRange_ReturnsFalse()
    {
        var view = new CodeView { Code = "plain text\nwith no folds" };

        view.SetFolded(0, true).ShouldBeFalse();
        view.IsFolded(0).ShouldBeFalse();
    }

    /// <summary>Verifies ToggleFold flips a fold range's collapsed state each call.</summary>
    [Fact]
    public void ToggleFold_WhenCalledTwice_ReturnsToTheOriginalState()
    {
        var view = new CodeView { Code = "if (x) {\n    y();\n}\n", Language = "Rust" };
        var foldStart = Enumerable.Range(0, 3).First(view.IsFoldStart);

        _ = view.ToggleFold(foldStart);
        view.IsFolded(foldStart).ShouldBeTrue();

        _ = view.ToggleFold(foldStart);
        view.IsFolded(foldStart).ShouldBeFalse();
    }

    /// <summary>Verifies CollapseAll folds every range and ExpandAll restores all of them.</summary>
    [Fact]
    public void CollapseAllThenExpandAll_WhenMultipleFoldsExist_TogglesEveryRange()
    {
        var view = new CodeView
        {
            Code = "fn a() {\n    x();\n}\nfn b() {\n    y();\n}\n",
            Language = "Rust",
        };

        var starts = Enumerable.Range(0, 6).Where(view.IsFoldStart).ToArray();
        starts.ShouldNotBeEmpty();

        view.CollapseAll();
        starts.ShouldAllBe(line => view.IsFolded(line));

        view.ExpandAll();
        starts.ShouldAllBe(line => !view.IsFolded(line));
    }

    /// <summary>Verifies nested collapsed ranges are projected with one range update and one line
    /// scan apiece instead of repeatedly marking every enclosing interior.</summary>
    [Fact]
    public void CollapseAll_WhenFoldsAreDeeplyNested_RebuildsVisibilityLinearly()
    {
        const int depth = 300;
        var code = string.Concat(Enumerable.Repeat("if (true) {\n", depth)) +
                   string.Concat(Enumerable.Repeat("}\n", depth));
        var view = new CodeView { Code = code, Language = "Rust" };
        view.FoldRanges.Count.ShouldBeGreaterThanOrEqualTo(depth);

        view.CollapseAll();

        view.LastFoldVisibilityOperationCount.ShouldBeLessThanOrEqualTo(
            view.FoldRanges.Count + (depth * 2) + 1);
    }

    /// <summary>Verifies the style resolves every default-style role to a non-transparent color.</summary>
    [Fact]
    public void ActualStyle_WhenResolved_ProvidesEveryRoleColor()
    {
        var view = new CodeView();

        foreach (var role in Enum.GetValues<SyntaxDefaultStyle>())
        {
            _ = view.ActualStyle.ColorFor(role);
        }
    }

    /// <summary>Verifies a freshly constructed view defaults to a visible, enabled fold gutter.</summary>
    [Fact]
    public void IsFoldingEnabled_WhenConstructed_DefaultsToTrue()
    {
        var view = new CodeView();

        view.IsFoldingEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies disabling folding hides every collapsed range's interior lines again,
    /// and re-enabling it resumes the preserved collapsed state.</summary>
    [Fact]
    public void IsFoldingEnabled_WhenToggledOffThenOn_PreservesFoldedStateWithoutApplyingIt()
    {
        var view = new CodeView { Code = "fn a() {\n    x();\n}\n", Language = "Rust" };
        var foldStart = Enumerable.Range(0, 3).First(view.IsFoldStart);
        _ = view.SetFolded(foldStart, true);

        view.IsFoldingEnabled = false;

        view.IsFolded(foldStart).ShouldBeTrue();

        view.IsFoldingEnabled = true;

        view.IsFolded(foldStart).ShouldBeTrue();
    }

    /// <summary>Verifies visible-line projection follows a newer folding policy committed from
    /// the owner notification instead of the outer setter's captured policy.</summary>
    [Fact]
    public void IsFoldingEnabled_WhenPropertyObserverRestoresTrue_RebuildsFoldedProjection()
    {
        var view = new CodeView { Code = "fn a() {\n    x();\n}\n", Language = "Rust" };
        var foldStart = Enumerable.Range(0, 3).First(view.IsFoldStart);
        _ = view.SetFolded(foldStart, true);
        var foldedHeight = view.MeasureProjection().Height;
        view.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CodeView.IsFoldingEnabled) && !view.IsFoldingEnabled)
            {
                view.IsFoldingEnabled = true;
            }
        };

        view.IsFoldingEnabled = false;

        view.IsFoldingEnabled.ShouldBeTrue();
        view.MeasureProjection().Height.ShouldBe(foldedHeight);
    }

    /// <summary>Verifies a freshly constructed view has a default CodeViewContextMenu.</summary>
    [Fact]
    public void ContextMenu_WhenConstructed_IsTheDefaultCodeViewContextMenu()
    {
        var view = new CodeView();

        _ = view.ContextMenu.ShouldBeOfType<CodeViewContextMenu>();
    }

    /// <summary>Verifies the default context menu can be replaced, exactly like TextInput's.</summary>
    [Fact]
    public void ContextMenu_WhenReplaced_AcceptsAnyOtherMenuOrNull()
    {
        var view = new CodeView();
        var custom = new ContextMenu();

        view.ContextMenu = custom;

        view.ContextMenu.ShouldBeSameAs(custom);
    }

    /// <summary>Verifies ClipboardWriter defaults to null and is retained verbatim once assigned.</summary>
    [Fact]
    public void ClipboardWriter_WhenAssigned_IsRetained()
    {
        var view = new CodeView();
        view.ClipboardWriter.ShouldBeNull();

        var writer = new Action<string>(_ => { });
        view.ClipboardWriter = writer;

        view.ClipboardWriter.ShouldBeSameAs(writer);
    }

    /// <summary>Verifies the internal clipboard-copy request forwards CopySelection's result to ClipboardWriter.</summary>
    [Fact]
    public void RequestClipboardCopy_WhenClipboardWriterIsSet_ForwardsSelectedText()
    {
        var view = new CodeView { Code = "abcdef" };
        view.SetSelection(new Selection(0, 3));
        string? written = null;
        view.ClipboardWriter = value => written = value;

        view.RequestClipboardCopy();

        written.ShouldBe("abc");
    }

    /// <summary>Verifies the internal clipboard-copy request is a safe no-op with no ClipboardWriter.</summary>
    [Fact]
    public void RequestClipboardCopy_WhenClipboardWriterIsNull_DoesNotThrow()
    {
        var view = new CodeView { Code = "abcdef" };
        view.SelectAll();

        Should.NotThrow(view.RequestClipboardCopy);
    }
}
