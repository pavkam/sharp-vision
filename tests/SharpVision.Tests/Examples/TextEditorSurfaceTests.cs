// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Examples;

/// <summary>Proves the TextEditor example connects menus to attached presentations and commands.</summary>
public sealed class TextEditorSurfaceTests
{
    /// <summary>Verifies the example uses the public status control for document, position, and encoding context.</summary>
    [Fact]
    public async Task Constructor_WhenMounted_UsesStatusBarForDocumentContextAsync()
    {
        // Arrange
        var screen = new TextEditor.EditorScreen();

        // Act
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(90, 26),
            TestContext.Current.CancellationToken);
        var status = FindAll<StatusBar>(screen).Single();

        // Assert
        status.Items.Select(static item => item.Alignment).ShouldBe(
        [
            StatusBarItemAlignment.Left,
            StatusBarItemAlignment.Right,
            StatusBarItemAlignment.Right,
            StatusBarItemAlignment.Right,
            StatusBarItemAlignment.Right
        ]);
        var content = status.Items
            .Select(static item => item.Content.ShouldBeOfType<ControlText>().Content)
            .ToArray();
        content[0].ShouldBe("Ready");
        content[1].ShouldStartWith("Ln ");
        content[1].ShouldContain(", Col ");
        content[2].ShouldBe("<d>Wrap</d>");
        content[3].ShouldBe("<d>UTF-8</d>");
        content[4].ShouldBe("<d>Untitled</d>");
        status.Bounds.Bottom.ShouldBe(26);
    }

    /// <summary>
    /// Verifies a save failure (writing to a path that resolves to an existing
    /// directory) reports the error and keeps the document dirty, rather than
    /// silently treating the failed write as a successful save. Before the
    /// fix, WriteFileAsync swallowed the exception and the discard-gate always
    /// returned true regardless of write outcome.
    /// </summary>
    [Fact]
    public async Task Pointer_WhenSaveAsWritesToADirectoryPath_ReportsErrorAndKeepsDocumentDirtyAsync()
    {
        // Arrange: a real subdirectory of the dialog's default browse directory
        // (Environment.CurrentDirectory) stands in for an unwritable target.
        var directoryName = $"save-failure-{Guid.NewGuid():N}";
        var directoryPath = Path.Combine(Environment.CurrentDirectory, directoryName);
        _ = Directory.CreateDirectory(directoryPath);

        try
        {
            var screen = new TextEditor.EditorScreen();
            await using var surface = await ComponentSurface.MountScreenAsync(
                screen,
                new Size(90, 26),
                TestContext.Current.CancellationToken);
            var editor = Editor(screen);
            await surface.UpdateAsync(() => editor.Text = "unsaved content", "type content");

            var file = FindItem(screen, "File");
            var saveAs = FindItem(file.Submenu.ShouldNotBeNull(), "Save As...");
            await surface.Pointer.ClickAsync(file);
            await surface.Pointer.ClickAsync(saveAs);
            await surface.UpdateAsync(static () => { }, "show Save As dialog");

            // The filename input receives initial load focus, so typed text and
            // Enter route directly to it without locating the private field.
            await surface.Keyboard.TypeAsync(directoryName);
            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "attempt the failing write");

            // Assert: the write failure surfaces as an error dialog.
            var error = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
            error.Title.ShouldBe("Error");
            await surface.Keyboard.PressAsync(Code.Enter);
            await surface.UpdateAsync(static () => { }, "dismiss error dialog");

            // Assert: the document is still considered dirty. Quitting must
            // still prompt to discard rather than closing silently.
            var quitItem = FindItem(file.Submenu.ShouldNotBeNull(), "Quit");
            await surface.Pointer.ClickAsync(file);
            await surface.Pointer.ClickAsync(quitItem);
            await surface.UpdateAsync(static () => { }, "attempt to quit");

            _ = OwnedTree.Find<MessageBox>(surface.Application.Root).ShouldNotBeNull();
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    /// <summary>Verifies a clicked Search command closes the menu plane and shows an attached modeless find Window.</summary>
    [Fact]
    public async Task Pointer_WhenSearchFindIsClicked_ShowsModelessFindWindowAfterMenuClosesAsync()
    {
        // Arrange
        var screen = new TextEditor.EditorScreen();
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(90, 26),
            TestContext.Current.CancellationToken);
        var search = FindItem(screen, "Search");
        var find = FindItem(search.Submenu.ShouldNotBeNull(), "Find");

        // Act
        await surface.Pointer.ClickAsync(search);
        var menuScope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.ClickAsync(find);
        await surface.UpdateAsync(static () => { }, "drain queued TextEditor find command");

        // Assert
        var window = FindAll<Window>(screen).Single();
        window.Visibility.ShouldBe(Visibility.Visible);
        FindAll<Grid>(window).Single(grid => grid.Rows.Count > 0).Rows.Count.ShouldBe(2);
        _ = window.Parent.ShouldBeOfType<Overlay>();
        window.Bounds.Y.ShouldBe(2);
        window.Bounds.Right.ShouldBe(88);
        (window.Bounds.Right + window.Shadow.Offset.X).ShouldBeLessThanOrEqualTo(89);
        menuScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.Application.Focus.Focused.ShouldBeOfType<TextInput>().ShouldNotBeSameAs(Editor(screen));

        // Act background interaction
        var editor = Editor(screen);
        await surface.Pointer.ClickAsync(editor, new Point(1, 1));

        // Assert modeless interaction
        window.Visibility.ShouldBe(Visibility.Visible);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(editor);
    }

    /// <summary>Verifies the TextEditor find Window follows ordinary Overlay title-bar dragging.</summary>
    [Fact]
    public async Task Pointer_WhenFindWindowTitleIsDragged_MovesWindowWithinEditorOverlayAsync()
    {
        // Arrange
        var screen = new TextEditor.EditorScreen();
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(90, 26),
            TestContext.Current.CancellationToken);
        var search = FindItem(screen, "Search");
        var find = FindItem(search.Submenu.ShouldNotBeNull(), "Find");
        await surface.Pointer.ClickAsync(search);
        await surface.Pointer.ClickAsync(find);
        await surface.UpdateAsync(static () => { }, "show draggable TextEditor find Window");
        var window = FindAll<Window>(screen).Single();
        var origin = window.Bounds;

        // Act
        await surface.Pointer.DragAsync(window, new Point(10, 0), new Point(8, 2));

        // Assert
        window.Bounds.X.ShouldBe(origin.X - 2);
        window.Bounds.Y.ShouldBe(origin.Y + 2);
        surface.ShouldHaveCapture(null);

        // Act resize after movement
        await surface.ResizeAsync(new Size(65, 18));

        // Assert resized containment
        window.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        window.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0);
        window.Bounds.Right.ShouldBeLessThanOrEqualTo(65);
        window.Bounds.Bottom.ShouldBeLessThanOrEqualTo(18);
    }

    /// <summary>Verifies the TextEditor replace Window uses aligned fields and restrained action chrome.</summary>
    [Fact]
    public async Task Pointer_WhenReplaceWindowOpens_UsesAlignedCompactDialogLayoutAsync()
    {
        // Arrange
        var screen = new TextEditor.EditorScreen();
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(90, 26),
            TestContext.Current.CancellationToken);
        var search = FindItem(screen, "Search");
        var replace = FindItem(search.Submenu.ShouldNotBeNull(), "Replace");

        // Act
        await surface.Pointer.ClickAsync(search);
        await surface.Pointer.ClickAsync(replace);
        await surface.UpdateAsync(static () => { }, "show polished TextEditor replace Window");

        // Assert
        var window = FindAll<Window>(screen).Single();
        FindAll<Grid>(window).Single(grid => grid.Rows.Count > 0).Rows.Count.ShouldBe(2);
        var fields = FindAll<TextInput>(window).Where(input => !input.AcceptsReturn).ToArray();
        fields.Length.ShouldBe(2);
        var findLabel = FindText(window, "Find:");
        var replaceLabel = FindText(window, "Replace:");
        findLabel.Bounds.X.ShouldBe(replaceLabel.Bounds.X);
        findLabel.Bounds.Width.ShouldBe(replaceLabel.Bounds.Width);
        findLabel.TextAlignment.ShouldBe(Alignment.End);
        replaceLabel.TextAlignment.ShouldBe(Alignment.End);
        fields[0].Bounds.X.ShouldBe(fields[1].Bounds.X);
        fields[0].Bounds.Width.ShouldBe(fields[1].Bounds.Width);
        fields[0].Bounds.Width.ShouldBeGreaterThan(30);
        var initialFieldWidth = fields[0].Bounds.Width;
        var findNext = FindButton(window, "Find Next");
        var replaceButton = FindButton(window, "Replace");
        var replaceAll = FindButton(window, "Replace All");
        var close = FindButton(window, "Close");
        var actions = new[] { findNext, replaceButton, replaceAll, close };
        _ = actions.Select(static button => button.Bounds.Y).Distinct().ShouldHaveSingleItem();
        findNext.Bounds.X.ShouldBeLessThan(replaceButton.Bounds.X);
        replaceButton.Bounds.X.ShouldBeLessThan(replaceAll.Bounds.X);
        replaceAll.Bounds.X.ShouldBeLessThan(close.Bounds.X);
        findNext.Bounds.Y.ShouldBeGreaterThan(fields[1].Bounds.Bottom);
        close.Bounds.Right.ShouldBe(fields[0].Bounds.Right);
        window.Bounds.Width.ShouldBe(54);
        window.Bounds.Height.ShouldBeLessThanOrEqualTo(13);
        FindAll<Button>(window).ShouldAllBe(button => !button.ActualShadow.IsVisible);
        foreach (var button in FindAll<Button>(window))
        {
            button.Content.ShouldBeOfType<ControlText>().TextAlignment.ShouldBe(Alignment.Center);
        }

        var status = FindAll<ControlText>(window).Single(text => text.Content == "<d>Enter a search term</d>");
        status.Bounds.Y.ShouldBeGreaterThanOrEqualTo(fields[1].Bounds.Bottom);
        status.Bounds.Y.ShouldBeLessThan(findNext.Bounds.Y);

        // Act resize wider
        await surface.ResizeAsync(new Size(110, 26));

        // Assert proportional field growth
        window.Bounds.Width.ShouldBe(58);
        fields[0].Bounds.Width.ShouldBeGreaterThan(initialFieldWidth);
        fields[0].Bounds.Width.ShouldBe(fields[1].Bounds.Width);
        _ = actions.Select(static button => button.Bounds.Y).Distinct().ShouldHaveSingleItem();
        close.Bounds.Right.ShouldBe(fields[0].Bounds.Right);
    }

    /// <summary>Verifies clicked Copy and Paste commands share the Application clipboard path.</summary>
    [Fact]
    public async Task Pointer_WhenEditCopyAndPasteAreClicked_UsesApplicationClipboardAsync()
    {
        // Arrange
        var screen = new TextEditor.EditorScreen();
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(90, 26),
            TestContext.Current.CancellationToken);
        var editor = Editor(screen);
        var edit = FindItem(screen, "Edit");
        var editMenu = edit.Submenu.ShouldNotBeNull();
        var copy = FindItem(editMenu, "Copy");
        var paste = FindItem(editMenu, "Paste");
        const string copied = "Welcome";
        await surface.UpdateAsync(
            () =>
            {
                editor.Text = "Welcome to SharpVision";
                editor.Select(0, copied.Length);
            },
            "select TextEditor text for menu copy");

        // Act copy
        await surface.Pointer.ClickAsync(edit);
        await surface.Pointer.ClickAsync(copy);
        await surface.UpdateAsync(static () => { }, "drain queued TextEditor copy command");
        await surface.UpdateAsync(
            () => editor.Select(editor.Text.Length, 0),
            "move TextEditor caret before menu paste");

        // Act paste
        await surface.Pointer.ClickAsync(edit);
        await surface.Pointer.ClickAsync(paste);
        await surface.UpdateAsync(static () => { }, "drain queued TextEditor paste command");

        // Assert
        editor.Text.ShouldEndWith(copied);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(editor);
    }

    /// <summary>Verifies a secondary editor press opens the attached context Popup as a dismissing modal plane.</summary>
    [Fact]
    public async Task Pointer_WhenEditorIsSecondaryPressed_OpensModalContextPopupAsync()
    {
        // Arrange
        var screen = new TextEditor.EditorScreen();
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(90, 26),
            TestContext.Current.CancellationToken);
        var editor = Editor(screen);
        var popup = FindAll<Popup>(screen).Single(candidate => ReferenceEquals(candidate.Anchor, editor));
        var menu = popup.Content.ShouldBeOfType<Menu>();
        var selectAll = FindItem(menu, "Select All");
        var point = await surface.ResolvePointAsync(editor, new Point(1, 1));

        // Act
        await surface.SendAsync(
            EncodePointer(button: 2, point, final: 'M'),
            "press secondary pointer in TextEditor");

        // Assert
        popup.IsOpen.ShouldBeTrue();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        scope.Root.ShouldBeSameAs(popup);
        scope.OutsideInteraction.ShouldBe(OutsideInteraction.Dismiss);

        // Act command
        await surface.Pointer.ClickAsync(selectAll);
        await surface.UpdateAsync(static () => { }, "drain queued TextEditor context command");

        // Assert command
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        editor.SelectionLength.ShouldBe(editor.Text.Length);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(editor);
    }

    private static TextInput Editor(Control root) =>
        FindAll<TextInput>(root).Single(input => input.AcceptsReturn);

    private static MenuItem FindItem(Control root, string label) =>
        FindAll<MenuItem>(root).Single(item =>
            item.Content is ControlText text &&
            text.Content.Replace("&", "", StringComparison.Ordinal) == label);

    private static Button FindButton(Control root, string label) =>
        FindAll<Button>(root).Single(button => button.Content is ControlText text && text.Content == label);

    private static ControlText FindText(Control root, string content) =>
        FindAll<ControlText>(root).Single(text => text.Content == content);

    private static List<T> FindAll<T>(Control root) where T : Control
    {
        var matches = new List<T>();
        Visit(root);
        return matches;

        void Visit(Control control)
        {
            if (control is T match)
            {
                matches.Add(match);
            }

            for (var index = 0; index < control.OwnedControlCount; index++)
            {
                Visit(control.OwnedControlAt(index));
            }
        }
    }

    private static byte[] EncodePointer(int button, Point point, char final) =>
        Encoding.ASCII.GetBytes(
            FormattableString.Invariant($"\u001b[<{button};{point.X + 1};{point.Y + 1}{final}"));
}
