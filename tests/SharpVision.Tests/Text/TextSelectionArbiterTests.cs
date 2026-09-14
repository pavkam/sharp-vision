// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using SharpVision.Tests.Support;

/// <summary>Proves the dispatcher-owned <see cref="TextSelectionArbiter"/> keeps at most one
/// non-empty text selection per application: the moment any selection owner commits a range, every
/// other owner under the same application collapses its own, whether the new range came from the
/// pointer, the keyboard, or code, and a detached owner releases its claim.</summary>
public sealed class TextSelectionArbiterTests
{
    private static Task<ComponentSurface> MountAsync(ControlBase control, int width, int height) =>
        ComponentSurface.MountAsync(
            control,
            new Size(width, height),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

    private static (Stack Root, TextInput First, TextInput Second, ControlText Label) CreateRoot()
    {
        var first = new TextInput { Text = "alpha bravo", ScrollBars = ScrollBars.None, Width = Length.Cells(14), Height = Length.Cells(1) };
        var second = new TextInput { Text = "charlie delta", ScrollBars = ScrollBars.None, Width = Length.Cells(14), Height = Length.Cells(1) };
        var label = new ControlText("echo foxtrot") { IsTextSelectionEnabled = true, IsFocusable = true };
        var root = new Stack { Children = { first, second, label } };
        return (root, first, second, label);
    }

    /// <summary>Verifies a pointer drag that selects in one input collapses a keyboard selection
    /// another input still held, and the collapsed input keeps its caret where it was.</summary>
    [Fact]
    public async Task PointerDrag_WhenAnotherInputHoldsASelection_CollapsesThatSelectionAsync()
    {
        // Arrange
        var (root, first, second, _) = CreateRoot();
        await using var surface = await MountAsync(root, 14, 3);
        await surface.FocusAsync(first);
        await surface.UpdateAsync(() => first.Select(0, 5), "select 'alpha' in the first input");
        first.SelectionLength.ShouldBe(5);

        // Act
        await surface.Pointer.MoveToAsync(second, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(second, new Point(7, 0));
        await surface.Pointer.ReleaseAsync();

        // Assert
        var (secondLength, firstLength, firstCaret) = await surface.ReadAsync(
            () => (second.SelectionLength, first.SelectionLength, first.CaretIndex));
        secondLength.ShouldBeGreaterThan(0);
        firstLength.ShouldBe(0);
        firstCaret.ShouldBe(5);
    }

    /// <summary>Verifies Control+A in a selectable label collapses the selection an input held,
    /// and a later programmatic selection in the input collapses the label's in turn.</summary>
    [Fact]
    public async Task SelectAll_WhenAnInputHoldsASelection_CollapsesItAndIsCollapsedByTheNextOwnerAsync()
    {
        // Arrange
        var (root, first, _, label) = CreateRoot();
        await using var surface = await MountAsync(root, 14, 3);
        await surface.FocusAsync(first);
        await surface.UpdateAsync(() => first.Select(6, 5), "select 'bravo' in the first input");

        // Act
        await surface.FocusAsync(label);
        await surface.ControlAsync('a');

        // Assert
        var (labelText, firstLength) = await surface.ReadAsync(() => (label.SelectedText, first.SelectionLength));
        labelText.ShouldBe("echo foxtrot");
        firstLength.ShouldBe(0);

        // Act
        await surface.UpdateAsync(() => first.Select(0, 5), "select 'alpha' programmatically");

        // Assert
        var (labelLength, firstText) = await surface.ReadAsync(() => (label.TextSelection.Length, first.SelectedText));
        labelLength.ShouldBe(0);
        firstText.ShouldBe("alpha");
    }

    /// <summary>Verifies moving the caret without selecting does not disturb another owner's
    /// selection: only a non-empty range claims exclusivity.</summary>
    [Fact]
    public async Task CaretMove_WhenAnotherInputHoldsASelection_LeavesThatSelectionAloneAsync()
    {
        // Arrange
        var (root, first, second, _) = CreateRoot();
        await using var surface = await MountAsync(root, 14, 3);
        await surface.FocusAsync(first);
        await surface.UpdateAsync(() => first.Select(0, 5), "select 'alpha' in the first input");

        // Act
        await surface.FocusAsync(second);
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.Left);

        // Assert
        var (secondLength, secondCaret, firstLength) = await surface.ReadAsync(
            () => (second.SelectionLength, second.CaretIndex, first.SelectionLength));
        secondLength.ShouldBe(0);
        secondCaret.ShouldBe(second.Text.Length - 2);
        firstLength.ShouldBe(5);
    }

    /// <summary>Verifies the collapsed owner publishes its change through its own selection
    /// events, so an application observing either control sees a consistent picture.</summary>
    [Fact]
    public async Task Claim_WhenAnotherOwnerCollapses_RaisesThatOwnerSelectionChangedAsync()
    {
        // Arrange
        var (root, first, second, _) = CreateRoot();
        await using var surface = await MountAsync(root, 14, 3);
        await surface.FocusAsync(first);
        await surface.UpdateAsync(() => first.Select(0, 5), "select 'alpha' in the first input");
        var changes = new List<int>();
        first.SelectionChanged += (_, _) => changes.Add(first.SelectionLength);

        // Act
        await surface.UpdateAsync(() => second.Select(0, 3), "select in the second input");

        // Assert
        changes.ShouldBe([0]);
    }

    /// <summary>Verifies an owner detached while holding the application's selection releases its
    /// claim, so a later selection elsewhere never reaches back into a detached control.</summary>
    [Fact]
    public async Task Detach_WhenOwnerHoldsTheSelection_ReleasesTheClaimAsync()
    {
        // Arrange
        var (root, first, second, _) = CreateRoot();
        await using var surface = await MountAsync(root, 14, 3);
        await surface.FocusAsync(first);
        await surface.UpdateAsync(() => first.Select(0, 5), "select 'alpha' in the first input");

        (await surface.ReadAsync(() => surface.Application.Dispatcher.TextSelectionArbiter.Owner)).ShouldBeSameAs(first);

        // Act
        await surface.UpdateAsync(() => root.Children.Remove(first), "detach the selecting input");
        (await surface.ReadAsync(() => surface.Application.Dispatcher.TextSelectionArbiter.Owner)).ShouldBeNull();
        await surface.UpdateAsync(() => second.Select(0, 3), "select in the second input");

        // Assert
        first.SelectionLength.ShouldBe(5);
        second.SelectionLength.ShouldBe(3);
        (await surface.ReadAsync(() => surface.Application.Dispatcher.TextSelectionArbiter.Owner)).ShouldBeSameAs(second);
    }
}
