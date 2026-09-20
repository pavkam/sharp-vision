// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Controls.Collections;
using SharpVision.Documents.Markdown;

// The project's own namespace, SharpVision.Document.Tests, nests textually under the SharpVision.Document
// segment, so an unqualified "Document" would otherwise resolve to that segment (as a namespace)
// rather than the Document control - this in-namespace alias, unlike a global one, takes priority
// over that enclosing-segment lookup in every position, including local-variable and return types.
using Document = Controls.Document.Document;

/// <summary>Verifies a mounted <see cref="Document"/> realized inside a <see cref="ListView"/> row
/// contributes its parsed prose to the list's own projected text-selection stream.</summary>
public sealed class ListViewMarkdownSelectionSurfaceTests
{
    /// <summary>Verifies enabling text selection on the owning ListView - not on the row's own
    /// <see cref="Document"/> - lets Ctrl+A on the focused list select the complete Markdown-parsed
    /// prose realized inside its one row, proving the list's projection walks all the way through
    /// its realized item wrapper and the item's own template content into a real leaf source.</summary>
    [Fact]
    public async Task Keyboard_WhenListTextSelectionEnabledProjectsMountedMarkdownRow_ControlACopiesTheProseAsync()
    {
        // Arrange
        var document = new Document();
        _ = document.Load("Some selectable prose.", new MarkdownDocumentReader());
        var list = new ListView
        {
            Items = ["row"],
            SelectionMode = ListSelectionMode.None,
            RowHeight = Length.Auto,
            IsTextSelectionEnabled = true,
            ScrollBars = ScrollBars.None,
            ItemTemplate = _ => new Stack { Children = { document } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(24, 3),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => list.Focus().ShouldBeTrue(), "focus the list");

        // Act
        var controlA = new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune('a'),
            nativeCode: 0,
            Modifiers.Control,
            KeyAction.Press));
        await surface.UpdateAsync(
            () => _ = Router.Route(list, Events.Key, controlA),
            "press Ctrl+A on the list");

        // Assert
        var copied = await surface.Application.Dispatcher.InvokeAsync(
            () => list.CopySelectedText(),
            TestContext.Current.CancellationToken);
        copied.ShouldContain("Some selectable prose.");
    }
}
