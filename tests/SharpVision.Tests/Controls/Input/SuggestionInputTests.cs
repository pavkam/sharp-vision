// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies suggestion-input construction, forwarding, validation, and retained ownership.</summary>
public sealed class SuggestionInputTests
{
    /// <summary>Verifies the detached control exposes every specified initial value.</summary>
    [Fact]
    public void Constructor_WhenCreated_ExposesSpecifiedDefaults()
    {
        // Arrange and act
        using var input = new SuggestionInput();

        // Assert
        input.Text.ShouldBe(string.Empty);
        input.Placeholder.ShouldBeNull();
        input.StartAffix.ShouldBeNull();
        input.EndAffix.ShouldBeNull();
        input.MinimumPrefixLength.ShouldBe(1);
        input.Resolver.ShouldBeNull();
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
        _ = input.ItemTemplate.ShouldNotBeNull();
        input.TextSelector.ShouldBeNull();
        input.DropDownHeight.ShouldBe(Length.Cells(8));
        input.RowHeight.ShouldBe(Length.Auto);
        input.ScrollBars.ShouldBe(ScrollBars.Vertical);
        input.ShowScrollBars.ShouldBe(ShowScrollBars.WhenNeeded);
        input.ScrollBarStyle.ShouldBeNull();
        input.PopupChrome.ShouldBe(default);
    }

    /// <summary>Verifies the empty suggestion snapshot cannot be mutated through a collection cast.</summary>
    [Fact]
    public void Suggestions_WhenEmpty_ExposesReadOnlyOwnedSnapshot()
    {
        // Arrange
        using var input = new SuggestionInput();
        var snapshot = input.Suggestions.ShouldBeAssignableTo<IList<object?>>();

        // Act and assert
        _ = Should.Throw<NotSupportedException>(() => snapshot.Add("foreign"));
        input.Suggestions.ShouldBeEmpty();
    }

    /// <summary>Verifies text uses the retained editor's null and control-character validation before mutation.</summary>
    [Fact]
    public void Text_WhenInvalid_ThrowsBeforeMutation()
    {
        // Arrange
        using var input = new SuggestionInput { Text = "valid" };

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => input.Text = null!);
        input.Text.ShouldBe("valid");
        _ = Should.Throw<ArgumentException>(() => input.Text = "invalid\u001b");
        input.Text.ShouldBe("valid");
    }

    /// <summary>Verifies a negative grapheme threshold is rejected before the previous value changes.</summary>
    [Fact]
    public void MinimumPrefixLength_WhenNegative_ThrowsBeforeMutation()
    {
        // Arrange
        using var input = new SuggestionInput { MinimumPrefixLength = 2 };

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => input.MinimumPrefixLength = -1);
        input.MinimumPrefixLength.ShouldBe(2);
    }

    /// <summary>Verifies the row factory rejects null before replacing the retained list policy.</summary>
    [Fact]
    public void ItemTemplate_WhenNull_ThrowsBeforeMutation()
    {
        // Arrange
        using var input = new SuggestionInput();
        var previous = input.ItemTemplate;

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => input.ItemTemplate = null!);
        input.ItemTemplate.ShouldBeSameAs(previous);
    }

    /// <summary>Verifies popup height accepts supported responsive kinds and rejects invalid limits before mutation.</summary>
    [Fact]
    public void DropDownHeight_WhenAssigned_ValidatesBeforeMutation()
    {
        // Arrange
        using var input = new SuggestionInput { DropDownHeight = Length.Percent(50) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => input.DropDownHeight = Length.Star(1));
        input.DropDownHeight.ShouldBe(Length.Percent(50));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => input.DropDownHeight = Length.Cells(0));
        input.DropDownHeight.ShouldBe(Length.Percent(50));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => input.DropDownHeight = Length.Percent(0));
        input.DropDownHeight.ShouldBe(Length.Percent(50));
        input.DropDownHeight = Length.Auto;
        input.DropDownHeight.ShouldBe(Length.Auto);
    }

    /// <summary>Verifies uniform row height accepts supported kinds and rejects invalid values before mutation.</summary>
    [Fact]
    public void RowHeight_WhenAssigned_ValidatesBeforeMutation()
    {
        // Arrange
        using var input = new SuggestionInput { RowHeight = Length.Percent(25) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => input.RowHeight = Length.Star(1));
        input.RowHeight.ShouldBe(Length.Percent(25));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => input.RowHeight = Length.Cells(0));
        input.RowHeight.ShouldBe(Length.Percent(25));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => input.RowHeight = Length.Percent(0));
        input.RowHeight.ShouldBe(Length.Percent(25));
        input.RowHeight = Length.Auto;
        input.RowHeight.ShouldBe(Length.Auto);
    }

    /// <summary>Verifies invalid scrollbar policies are rejected before retained state changes.</summary>
    [Fact]
    public void ScrollPolicy_WhenUnknown_ThrowsBeforeMutation()
    {
        // Arrange
        using var input = new SuggestionInput
        {
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Always
        };

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => input.ScrollBars = (ScrollBars) 64);
        input.ScrollBars.ShouldBe(ScrollBars.Horizontal);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => input.ShowScrollBars = (ShowScrollBars) 64);
        input.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
    }

    /// <summary>Verifies presentation and row policies forward to the permanent retained controls.</summary>
    [Fact]
    public void Presentation_WhenCustomized_ForwardsToRetainedParts()
    {
        // Arrange
        using var input = new SuggestionInput();
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        var popup = OwnedTree.FindAll<Popup>(input).Single(candidate => ReferenceEquals(candidate.Content, list));
        var border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Rounded,
            Color.Rgb(65, 43, 21),
            Color.Transparent,
            TerminalAttributes.None);
        var chrome = new PopupChrome { Border = border };
        ItemTemplate template = static item => new ControlText($"* {item}");
        static string SelectText(object? item) => $"selected {item}";

        // Act
        input.Placeholder = "Search";
        input.StartAffix = new Affix(">");
        input.EndAffix = new Affix("/");
        input.ItemTemplate = template;
        input.TextSelector = SelectText;
        input.DropDownHeight = Length.Percent(40);
        input.RowHeight = Length.Cells(2);
        input.ScrollBars = ScrollBars.Both;
        input.ShowScrollBars = ShowScrollBars.Always;
        input.ScrollBarStyle = ScrollBarStyle.ThinLine;
        input.PopupChrome = chrome;

        // Assert
        editor.Placeholder.ShouldBe("Search");
        editor.StartAffix.ShouldBe(new Affix(">"));
        editor.EndAffix.ShouldBe(new Affix("/"));
        list.ItemTemplate.ShouldBeSameAs(template);
        input.TextSelector.ShouldBeSameAs((Func<object?, string>) SelectText);
        popup.ContentHeightLimit.ShouldBe(Length.Percent(40));
        list.RowHeight.ShouldBe(Length.Cells(2));
        list.ScrollBars.ShouldBe(ScrollBars.Both);
        list.ShowScrollBars.ShouldBe(ShowScrollBars.Always);
        list.ScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
        input.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
        popup.Style.ShouldBe(chrome);
    }

    /// <summary>Verifies clearing the optional selector restores the public fallback marker.</summary>
    [Fact]
    public void TextSelector_WhenCleared_RestoresDefaultFormatter()
    {
        // Arrange
        using var input = new SuggestionInput
        {
            TextSelector = static item => $"custom {item}"
        };

        // Act
        input.TextSelector = null;

        // Assert
        input.TextSelector.ShouldBeNull();
    }

    /// <summary>Verifies popup chrome reset releases both local facets back to Popup ownership.</summary>
    [Fact]
    public void ResetPopupChrome_WhenCustomized_RestoresPopupOwnership()
    {
        // Arrange
        using var input = new SuggestionInput
        {
            PopupChrome = new PopupChrome
            {
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Rounded,
                    Color.Rgb(65, 43, 21),
                    Color.Transparent,
                    TerminalAttributes.None),
                Shadow = AppearanceTestValues.Shadow(visible: true)
            }
        };

        // Act
        input.ResetPopupChrome();

        // Assert
        input.PopupChrome.ShouldBe(default);
    }

    /// <summary>Verifies forwarded changes publish owner properties once and invalidate retained layout.</summary>
    [Fact]
    public void ForwardedProperties_WhenChanged_PublishOnceAndInvalidateOwner()
    {
        // Arrange
        using var input = new SuggestionInput();
        var notifications = new List<string?>();
        input.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        input.Clear(Invalidation.All);

        // Act
        input.Placeholder = "Search";
        input.DropDownHeight = Length.Cells(4);

        // Assert
        notifications.ShouldBe([
            nameof(SuggestionInput.Placeholder),
            nameof(SuggestionInput.DropDownHeight)
        ]);
        input.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies each public forwarding and scalar mutation publishes its owner property exactly once.</summary>
    [Fact]
    public void Properties_WhenChanged_PublishExactOwnerNotifications()
    {
        // Arrange
        using var input = new SuggestionInput();
        var notifications = new List<string?>();
        input.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        static ValueTask<IReadOnlyList<object?>> Resolve(string searchTerms, CancellationToken cancellationToken)
        {
            _ = searchTerms;
            _ = cancellationToken;
            return ValueTask.FromResult<IReadOnlyList<object?>>([]);
        }

        static ControlBase BuildItem(object? item) => new ControlText($"{item}");
        static string SelectText(object? item) => $"{item}";

        // Act
        input.Text = "query";
        input.Placeholder = "Search";
        input.StartAffix = new Affix(">");
        input.EndAffix = new Affix("/");
        input.MinimumPrefixLength = 0;
        input.Resolver = Resolve;
        input.ItemTemplate = BuildItem;
        input.TextSelector = SelectText;
        input.DropDownHeight = Length.Cells(4);
        input.RowHeight = Length.Cells(2);
        input.ScrollBars = ScrollBars.Both;
        input.ShowScrollBars = ShowScrollBars.Always;
        input.PopupChrome = new PopupChrome
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                Color.Rgb(65, 43, 21),
                Color.Transparent,
                TerminalAttributes.None)
        };

        // Assert
        notifications.ShouldBe([
            nameof(SuggestionInput.Text),
            nameof(SuggestionInput.Placeholder),
            nameof(SuggestionInput.StartAffix),
            nameof(SuggestionInput.EndAffix),
            nameof(SuggestionInput.MinimumPrefixLength),
            nameof(SuggestionInput.Resolver),
            nameof(SuggestionInput.ItemTemplate),
            nameof(SuggestionInput.TextSelector),
            nameof(SuggestionInput.DropDownHeight),
            nameof(SuggestionInput.RowHeight),
            nameof(SuggestionInput.ScrollBars),
            nameof(SuggestionInput.ShowScrollBars),
            nameof(SuggestionInput.PopupChrome)
        ]);
    }

    /// <summary>Verifies the forwarded rail style retains owner-level local and actual notifications.</summary>
    [Fact]
    public void ScrollBarStyle_WhenOwnershipChanges_PublishesLocalAndActualNotifications()
    {
        // Arrange
        using var input = new SuggestionInput();
        var notifications = new List<string?>();
        input.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Act
        input.ScrollBarStyle = ScrollBarStyle.ThinLine;
        input.ScrollBarStyle = null;

        // Assert
        notifications.ShouldBe([
            nameof(SuggestionInput.ScrollBarStyle),
            nameof(SuggestionInput.ActualScrollBarStyle),
            nameof(SuggestionInput.ScrollBarStyle),
            nameof(SuggestionInput.ActualScrollBarStyle)
        ]);
    }

    /// <summary>Verifies construction establishes the permanent editor, list, popup, focus, and traversal contract.</summary>
    [Fact]
    public void Constructor_WhenOwnedTreeIsInspected_HasOneStableConnectedSurface()
    {
        // Arrange and act
        using var input = new SuggestionInput();
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        var popup = OwnedTree.FindAll<Popup>(input).Single(candidate => ReferenceEquals(candidate.Content, list));

        // Assert
        input.OwnedControlCount.ShouldBe(2);
        editor.Parent.ShouldBeSameAs(input);
        popup.Parent.ShouldBeSameAs(input);
        popup.Content.ShouldBeSameAs(list);
        list.Parent.ShouldBeSameAs(popup);
        popup.Anchor.ShouldBeSameAs(editor);
        popup.ConnectsToAnchor.ShouldBeTrue();
        popup.Placement.ShouldBe(PopupPlacement.Below);
        popup.FocusOnOpen.ShouldBeFalse();
        popup.ModalBehavior.ShouldBe(PopupModalBehavior.None);
        popup.SuppressCloseOtherPopups.ShouldBeTrue();
        popup.TabNavigation.ShouldBe(TabNavigation.None);
        popup.TracksAnchorReflow.ShouldBeFalse();
        list.SelectionMode.ShouldBe(ListSelectionMode.Single);
        list.IsTabStop.ShouldBeFalse();
        editor.IsTabStop.ShouldBeTrue();
        editor.AcceptsTab.ShouldBeFalse();
        input.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies every public setter preserves the identities created by the constructor.</summary>
    [Fact]
    public void Properties_WhenAssigned_DoNotReconstructRetainedParts()
    {
        // Arrange
        using var input = new SuggestionInput();
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        var popup = OwnedTree.FindAll<Popup>(input).Single(candidate => ReferenceEquals(candidate.Content, list));

        // Act
        input.Text = "a";
        input.Placeholder = "Search";
        input.StartAffix = new Affix(">");
        input.EndAffix = new Affix("/");
        input.MinimumPrefixLength = 0;
        input.Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>([]);
        input.ItemTemplate = static item => new ControlText($"{item}");
        input.TextSelector = static item => Convert.ToString(item, CultureInfo.InvariantCulture)!;
        input.DropDownHeight = Length.Cells(4);
        input.RowHeight = Length.Cells(2);
        input.ScrollBars = ScrollBars.Both;
        input.ShowScrollBars = ShowScrollBars.Always;
        input.ScrollBarStyle = ScrollBarStyle.ThinLine;
        input.PopupChrome = new PopupChrome();
        input.IsOpen = true;
        input.Close();
        input.Refresh();

        // Assert
        OwnedTree.Find<TextInput>(input).ShouldBeSameAs(editor);
        OwnedTree.Find<UiListView>(input).ShouldBeSameAs(list);
        OwnedTree.FindAll<Popup>(input).Single(candidate => ReferenceEquals(candidate.Content, list)).ShouldBeSameAs(popup);
    }

    /// <summary>Verifies an empty control cannot pretend to open or resolve and publishes no false transitions.</summary>
    [Fact]
    public void OpenAndRefresh_WhenNoSnapshotExists_RemainInertAndPublishNoStateNotifications()
    {
        // Arrange
        using var input = new SuggestionInput();
        var notifications = new List<string?>();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(SuggestionInput.IsOpen) or nameof(SuggestionInput.IsResolving))
            {
                notifications.Add(eventArgs.PropertyName);
            }
        };

        // Act
        var focused = input.Open();
        input.Refresh();
        input.IsOpen = false;

        // Assert
        focused.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
        input.IsResolving.ShouldBeFalse();
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies public open and close requests publish the committed popup state once per transition.</summary>
    [Fact]
    public async Task IsOpen_WhenAttachedSnapshotExists_PublishesExactStateNotificationsAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var input = new SuggestionInput();
            var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
            list.Items = ["result"];
            var root = new Overlay { Children = { input } };
            new LayoutEngine().Layout(root, new Size(20, 6));
            root.Attach(dispatcher);
            var notifications = new List<bool>();
            input.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen))
                {
                    notifications.Add(input.IsOpen);
                }
            };

            // Act
            input.IsOpen = true;
            input.IsOpen = false;

            // Assert
            notifications.ShouldBe([true, false]);
            root.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Open focuses the one retained editor while an empty snapshot remains closed.</summary>
    [Fact]
    public async Task Open_WhenAttached_FocusesRetainedEditorAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var input = new SuggestionInput();
            var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();
            var root = new Overlay { Children = { input } };
            new LayoutEngine().Layout(root, new Size(20, 6));
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            // Act
            var accepted = input.Open();

            // Assert
            accepted.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(editor);
            input.IsOpen.ShouldBeFalse();
            root.Dispose();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies representative owner and retained-part mutations are rejected off-dispatcher.</summary>
    [Fact]
    public async Task Mutation_WhenAttachedOffDispatcher_ThrowsBeforeMutationAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var input = await dispatcher.InvokeAsync(() =>
        {
            var created = new SuggestionInput();
            created.Attach(dispatcher);
            return created;
        }, TestContext.Current.CancellationToken);

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => input.MinimumPrefixLength = 2);
        _ = Should.Throw<InvalidOperationException>(() => input.Placeholder = "Search");
        _ = Should.Throw<InvalidOperationException>(() => input.ScrollBarStyle = ScrollBarStyle.ThinLine);
        _ = Should.Throw<InvalidOperationException>(input.Refresh);
        input.MinimumPrefixLength.ShouldBe(1);
        input.Placeholder.ShouldBeNull();
        input.ScrollBarStyle.ShouldBeNull();
        await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposal releases the complete retained surface and rejects every public mutation path.</summary>
    [Fact]
    public void Mutation_WhenDisposed_ThrowsAndDisposesRetainedParts()
    {
        // Arrange
        var input = new SuggestionInput();
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        var popup = OwnedTree.FindAll<Popup>(input).Single(candidate => ReferenceEquals(candidate.Content, list));
        input.Dispose();
        Action[] mutations =
        [
            () => input.Text = "text",
            () => input.Placeholder = "Search",
            () => input.StartAffix = new Affix(">"),
            () => input.EndAffix = new Affix("/"),
            () => input.MinimumPrefixLength = 0,
            () => input.Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>([]),
            () => input.ItemTemplate = static item => new ControlText($"{item}"),
            () => input.TextSelector = static item => $"{item}",
            () => input.DropDownHeight = Length.Cells(4),
            () => input.RowHeight = Length.Cells(2),
            () => input.ScrollBars = ScrollBars.Both,
            () => input.ShowScrollBars = ShowScrollBars.Always,
            () => input.ScrollBarStyle = ScrollBarStyle.ThinLine,
            () => input.PopupChrome = default,
            input.ResetPopupChrome,
            () => input.IsOpen = true,
            () => _ = input.Open(),
            input.Close,
            input.Refresh
        ];

        // Act and assert
        foreach (var mutation in mutations)
        {
            _ = Should.Throw<ObjectDisposedException>(mutation);
        }

        editor.IsDisposed.ShouldBeTrue();
        list.IsDisposed.ShouldBeTrue();
        popup.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies null constructor arguments are rejected while an empty eligible query is retained.</summary>
    [Fact]
    public void SuggestionResolutionFailedEventArgs_WhenArgumentsVary_ValidatesBeforeAssignment()
    {
        // Arrange
        var exception = new InvalidOperationException("failure");

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => new SuggestionResolutionFailedEventArgs(null!, exception));
        _ = Should.Throw<ArgumentNullException>(() => new SuggestionResolutionFailedEventArgs("query", null!));
        var eventArgs = new SuggestionResolutionFailedEventArgs(string.Empty, exception);
        eventArgs.SearchTerms.ShouldBe(string.Empty);
        eventArgs.Exception.ShouldBeSameAs(exception);
    }
}
