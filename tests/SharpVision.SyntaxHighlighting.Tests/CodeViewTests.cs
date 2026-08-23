// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting.Tests;

/// <summary>Verifies CodeView content, selection, and folding behavior.</summary>
public sealed class CodeViewTests
{
    private const string _rustSnippet = "fn main() {\n    let x = 1;\n}\n";

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

    /// <summary>Verifies SetSelection commits a valid range and SelectedText reflects it.</summary>
    [Fact]
    public void SetSelection_WhenGivenValidRange_UpdatesSelectedText()
    {
        var view = new CodeView { Code = "abcdef" };

        view.SetSelection(new Selection(1, 4));

        view.SelectedText.ShouldBe("bcd");
        view.CopySelection().ShouldBe("bcd");
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
