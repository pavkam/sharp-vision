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
            nameof(SuggestionInput.IsResolving),
            nameof(SuggestionInput.IsResolving),
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
            var input = new SuggestionInput
            {
                Text = "query",
                Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["result"])
            };
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

    /// <summary>Verifies attaching cancels detached work without making its empty snapshot current,
    /// so a later Open starts one fresh request for the live attachment.</summary>
    [Fact]
    public async Task Open_WhenDetachedRequestWasCancelledByAttachment_RefreshesStaleSnapshotAsync()
    {
        // Arrange
        var first = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CancellationToken firstCancellation = default;
        var input = new SuggestionInput
        {
            Resolver = (_, cancellationToken) =>
            {
                calls++;

                if (calls == 1)
                {
                    firstCancellation = cancellationToken;
                    return new ValueTask<IReadOnlyList<object?>>(first.Task);
                }

                return ValueTask.FromResult<IReadOnlyList<object?>>(["fresh"]);
            },
            Text = "query"
        };
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() => input.Attach(dispatcher), TestContext.Current.CancellationToken);

        // Act
        _ = await dispatcher.InvokeAsync(input.Open, TestContext.Current.CancellationToken);

        // Assert
        firstCancellation.IsCancellationRequested.ShouldBeTrue();
        calls.ShouldBe(2);
        input.Suggestions.ShouldBe(["fresh"]);
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeTrue();
        await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
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

    /// <summary>Verifies a synchronous current result is copied, published in contract order,
    /// and opened from the intent recorded by the committed text.</summary>
    [Fact]
    public void Resolver_WhenCompletionIsSynchronous_CopiesPublishesAndOpensInOrder()
    {
        // Arrange
        using var input = new SuggestionInput
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>([])
        };
        var source = new List<object?> { "first" };
        input.Resolver = (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(source);
        var publications = new List<string>();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.IsResolving))
            {
                publications.Add($"resolving:{input.IsResolving}");
            }
            else if (eventArgs.PropertyName == nameof(SuggestionInput.Suggestions))
            {
                publications.Add($"property:{input.Suggestions.Count}");
            }
            else if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen))
            {
                publications.Add($"open:{input.IsOpen}");
            }
        };
        input.SuggestionsChanged += (_, _) => publications.Add($"event:{input.Suggestions.Count}");

        // Act
        input.Text = "f";
        source[0] = "mutated";
        source.Add("foreign");

        // Assert
        input.Suggestions.ShouldBe(["first"]);
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeTrue();
        publications.ShouldBe([
            "resolving:True",
            "resolving:False",
            "property:1",
            "event:1"
        ]);
    }

    /// <summary>Verifies attached synchronous success finishes on the initiating dispatcher,
    /// records a non-faulted inline boundary, and retires its lease before later work begins.</summary>
    [Fact]
    public async Task Resolver_WhenAttachedCompletionIsSynchronous_CompletesInlineAndRetiresLeaseAsync()
    {
        // Arrange
        var publications = new List<string>();
        CancellationToken completedToken = default;
        Task? inlineObservation = null;
        object?[] completedSuggestions = [];
        string[] completedPublications = [];
        var completedResolving = true;
        var completedOpen = false;
        var completedTokenWasCancelled = true;
        var input = new SuggestionInput
        {
            Resolver = (_, cancellationToken) =>
            {
                completedToken = cancellationToken;
                return ValueTask.FromResult<IReadOnlyList<object?>>(["result"]);
            }
        };
        await using var dispatcher = Dispatcher.Start();

        // Act
        await dispatcher.InvokeAsync(() =>
        {
            input.Attach(dispatcher);
            input.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(SuggestionInput.IsResolving))
                {
                    publications.Add($"resolving:{input.IsResolving}");
                }
                else if (eventArgs.PropertyName == nameof(SuggestionInput.Suggestions))
                {
                    publications.Add($"property:{input.Suggestions.Count}");
                }
                else if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen))
                {
                    publications.Add($"open:{input.IsOpen}");
                }
            };
            input.SuggestionsChanged += (_, _) => publications.Add($"event:{input.Suggestions.Count}");

            input.Text = "query";
            inlineObservation = input.LastInlineResolutionObservation;
            completedSuggestions = [.. input.Suggestions];
            completedPublications = [.. publications];
            completedResolving = input.IsResolving;
            completedOpen = input.IsOpen;
            input.Resolver = null;
            completedTokenWasCancelled = completedToken.IsCancellationRequested;
            input.Dispose();
        }, TestContext.Current.CancellationToken);

        // Assert
        completedSuggestions.ShouldBe(["result"]);
        completedResolving.ShouldBeFalse();
        completedOpen.ShouldBeTrue();
        completedPublications.ShouldBe([
            "resolving:True",
            "resolving:False",
            "property:1",
            "event:1",
            "open:True"
        ]);
        inlineObservation.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        completedTokenWasCancelled.ShouldBeFalse();
    }

    /// <summary>Verifies each attached synchronous success callback propagates its original
    /// exception after the required transition while leaving no duplicate fault observation.</summary>
    [Theory]
    [InlineData("PropertyChanged")]
    [InlineData("SuggestionsChanged")]
    [InlineData("PopupOpened")]
    public async Task Resolver_WhenAttachedSynchronousSuccessCallbackThrows_PropagatesOnceAndCompletesInlineAsync(
        string callback)
    {
        // Arrange
        var expected = new InvalidOperationException($"{callback} failed");
        var publications = new List<string>();
        var throwFromCallback = true;
        var throwingCallbackCalls = 0;
        CancellationToken completedToken = default;
        Exception? thrown = null;
        Task? inlineObservation = null;
        object?[] completedSuggestions = [];
        string[] completedPublications = [];
        var completedResolving = true;
        var completedOpen = false;
        var completedTokenWasCancelled = true;
        var input = new SuggestionInput
        {
            Resolver = (_, cancellationToken) =>
            {
                completedToken = cancellationToken;
                return ValueTask.FromResult<IReadOnlyList<object?>>(["result"]);
            }
        };
        await using var dispatcher = Dispatcher.Start();

        // Act
        await dispatcher.InvokeAsync(() =>
        {
            input.Attach(dispatcher);
            input.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(SuggestionInput.IsResolving))
                {
                    publications.Add($"resolving:{input.IsResolving}");
                }
                else if (eventArgs.PropertyName == nameof(SuggestionInput.Suggestions))
                {
                    publications.Add($"property:{input.Suggestions.Count}");

                    if (throwFromCallback && callback == "PropertyChanged")
                    {
                        throwingCallbackCalls++;
                        throw expected;
                    }
                }
                else if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen))
                {
                    publications.Add($"open:{input.IsOpen}");

                    if (throwFromCallback && callback == "PopupOpened" && input.IsOpen)
                    {
                        throwingCallbackCalls++;
                        throw expected;
                    }
                }
            };
            input.SuggestionsChanged += (_, _) =>
            {
                publications.Add($"event:{input.Suggestions.Count}");

                if (throwFromCallback && callback == "SuggestionsChanged")
                {
                    throwingCallbackCalls++;
                    throw expected;
                }
            };

            thrown = Record.Exception(() => input.Text = "query");
            inlineObservation = input.LastInlineResolutionObservation;
            completedSuggestions = [.. input.Suggestions];
            completedPublications = [.. publications];
            completedResolving = input.IsResolving;
            completedOpen = input.IsOpen;
            throwFromCallback = false;
            input.Resolver = null;
            completedTokenWasCancelled = completedToken.IsCancellationRequested;
            input.Dispose();
        }, TestContext.Current.CancellationToken);

        // Assert
        thrown.ShouldBeSameAs(expected);
        throwingCallbackCalls.ShouldBe(1);
        completedSuggestions.ShouldBe(["result"]);
        completedResolving.ShouldBeFalse();
        completedOpen.ShouldBe(callback != "PopupOpened");
        completedPublications.ShouldBe([
            "resolving:True",
            "resolving:False",
            "property:1",
            "event:1",
            "open:True"
        ]);
        inlineObservation.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        completedTokenWasCancelled.ShouldBeFalse();
    }

    /// <summary>Verifies attached synchronous failure callbacks propagate their original exception
    /// after close and failure publication while leaving no duplicate fault observation.</summary>
    [Theory]
    [InlineData("ResolutionFailed")]
    [InlineData("PopupClosed")]
    public async Task Resolver_WhenAttachedSynchronousFailureCallbackThrows_PropagatesOnceAndCompletesInlineAsync(
        string callback)
    {
        // Arrange
        var resolverFailure = new InvalidOperationException("resolver failed");
        var expected = new InvalidOperationException($"{callback} failed");
        var publications = new List<string>();
        var throwFromCallback = true;
        var throwingCallbackCalls = 0;
        CancellationToken failedToken = default;
        Exception? publishedResolverFailure = null;
        Exception? thrown = null;
        Task? inlineObservation = null;
        object?[] completedSuggestions = [];
        string[] completedPublications = [];
        var completedResolving = true;
        var completedOpen = true;
        var completedTokenWasCancelled = true;
        var input = new SuggestionInput
        {
            Resolver = (searchTerms, cancellationToken) =>
            {
                if (searchTerms == "initial")
                {
                    return ValueTask.FromResult<IReadOnlyList<object?>>(["initial"]);
                }

                failedToken = cancellationToken;
                throw resolverFailure;
            }
        };
        await using var dispatcher = Dispatcher.Start();

        // Act
        await dispatcher.InvokeAsync(() =>
        {
            input.Attach(dispatcher);
            input.Text = "initial";
            input.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(SuggestionInput.IsResolving))
                {
                    publications.Add($"resolving:{input.IsResolving}");
                }
                else if (eventArgs.PropertyName == nameof(SuggestionInput.Suggestions))
                {
                    publications.Add($"property:{input.Suggestions.Count}");
                }
                else if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen))
                {
                    publications.Add($"open:{input.IsOpen}");

                    if (throwFromCallback && callback == "PopupClosed" && !input.IsOpen)
                    {
                        throwingCallbackCalls++;
                        throw expected;
                    }
                }
            };
            input.SuggestionsChanged += (_, _) => publications.Add($"event:{input.Suggestions.Count}");
            input.ResolutionFailed += (_, eventArgs) =>
            {
                publishedResolverFailure = eventArgs.Exception;
                publications.Add($"failure:{eventArgs.SearchTerms}");

                if (throwFromCallback && callback == "ResolutionFailed")
                {
                    throwingCallbackCalls++;
                    throw expected;
                }
            };

            thrown = Record.Exception(() => input.Text = "failure");
            inlineObservation = input.LastInlineResolutionObservation;
            completedSuggestions = [.. input.Suggestions];
            completedPublications = [.. publications];
            completedResolving = input.IsResolving;
            completedOpen = input.IsOpen;
            throwFromCallback = false;
            input.Resolver = null;
            completedTokenWasCancelled = failedToken.IsCancellationRequested;
            input.Dispose();
        }, TestContext.Current.CancellationToken);

        // Assert
        thrown.ShouldBeSameAs(expected);
        throwingCallbackCalls.ShouldBe(1);
        publishedResolverFailure.ShouldBeSameAs(resolverFailure);
        completedSuggestions.ShouldBeEmpty();
        completedResolving.ShouldBeFalse();
        completedOpen.ShouldBeFalse();
        completedPublications.ShouldBe([
            "resolving:True",
            "resolving:False",
            "property:0",
            "open:False",
            "event:0",
            "failure:failure"
        ]);
        inlineObservation.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        completedTokenWasCancelled.ShouldBeFalse();
    }

    /// <summary>Verifies equivalent detached direct settlement propagates its callback exception
    /// once, completes the transition, and records only a non-faulted inline boundary.</summary>
    [Fact]
    public void Resolver_WhenDetachedSynchronousCallbackThrows_PropagatesOnceAndCompletesInline()
    {
        // Arrange
        var expected = new InvalidOperationException("suggestions callback failed");
        var throwFromCallback = true;
        var callbackCalls = 0;
        CancellationToken completedToken = default;
        using var input = new SuggestionInput
        {
            Resolver = (_, cancellationToken) =>
            {
                completedToken = cancellationToken;
                return ValueTask.FromResult<IReadOnlyList<object?>>(["result"]);
            }
        };
        input.SuggestionsChanged += (_, _) =>
        {
            if (throwFromCallback)
            {
                callbackCalls++;
                throw expected;
            }
        };

        // Act
        var thrown = Record.Exception(() => input.Text = "query");
        var inlineObservation = input.LastInlineResolutionObservation.ShouldNotBeNull();
        var completedSuggestions = input.Suggestions.ToArray();
        var completedResolving = input.IsResolving;
        var completedOpen = input.IsOpen;
        throwFromCallback = false;
        input.Resolver = null;

        // Assert
        thrown.ShouldBeSameAs(expected);
        callbackCalls.ShouldBe(1);
        completedSuggestions.ShouldBe(["result"]);
        completedResolving.ShouldBeFalse();
        completedOpen.ShouldBeTrue();
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
        inlineObservation.IsCompletedSuccessfully.ShouldBeTrue();
        completedToken.IsCancellationRequested.ShouldBeFalse();
    }

    /// <summary>Verifies detached asynchronous completion publishes only the immutable text
    /// snapshot passed to the current resolver invocation.</summary>
    [Fact]
    public async Task Resolver_WhenStillDetachedAtCompletion_PublishesCurrentSnapshotAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? receivedSearchTerms = null;
        using var input = new SuggestionInput
        {
            Resolver = (searchTerms, _) =>
            {
                receivedSearchTerms = searchTerms;
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            }
        };
        input.SuggestionsChanged += (_, _) => published.TrySetResult();
        input.Text = "query";

        // Act
        await Task.Run(() => completion.SetResult(["result"]), TestContext.Current.CancellationToken);
        await published.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        receivedSearchTerms.ShouldBe("query");
        input.Text.ShouldBe("query");
        input.Suggestions.ShouldBe(["result"]);
        input.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies a completion captured while attached publishes only on that exact
    /// attachment's dispatcher.</summary>
    [Fact]
    public async Task Resolver_WhenAttachedCompletionIsCurrent_PublishesOnDispatcherAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var published = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var input = new SuggestionInput
        {
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            input.Attach(dispatcher);
            input.SuggestionsChanged += (_, _) =>
                _ = published.TrySetResult(dispatcher.CheckAccess());
            input.Text = "query";
        }, TestContext.Current.CancellationToken);

        // Act
        await Task.Run(() => completion.SetResult(["result"]), TestContext.Current.CancellationToken);
        var publishedOnDispatcher = await published.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        publishedOnDispatcher.ShouldBeTrue();
        input.Suggestions.ShouldBe(["result"]);
        input.IsResolving.ShouldBeFalse();
        await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a detached popup-publication failure is not converted into a resolver
    /// failure, does not clear the successful snapshot, and cannot strand the completed lease.</summary>
    [Fact]
    public async Task Resolver_WhenDetachedSuccessPublicationThrows_PreservesOutcomeAndRetiresLeaseAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("suggestions publication failed");
        var failures = 0;
        CancellationToken completedToken = default;
        using var input = new SuggestionInput
        {
            Resolver = (_, cancellationToken) =>
            {
                completedToken = cancellationToken;
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            }
        };
        input.SuggestionsChanged += (_, _) => throw expected;
        input.ResolutionFailed += (_, _) => failures++;
        input.Text = "query";
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        // Act
        completion.SetResult(["successful"]);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await observation.WaitAsync(TestContext.Current.CancellationToken));
        input.Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["successful"]);

        // Assert
        exception.ShouldBeSameAs(expected);
        failures.ShouldBe(0);
        input.Suggestions.ShouldBe(["successful"]);
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeTrue();
        completedToken.IsCancellationRequested.ShouldBeFalse();
    }

    /// <summary>Verifies attached settlement aggregates later popup work after a throwing result
    /// callback and retires the successful lease before a subsequent request begins.</summary>
    [Fact]
    public async Task Resolver_WhenAttachedSuccessCallbackThrows_CompletesTransitionAndRetiresLeaseAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("suggestions callback failed");
        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failures = 0;
        var throwFromCallback = true;
        CancellationToken completedToken = default;
        var input = new SuggestionInput
        {
            Resolver = (_, cancellationToken) =>
            {
                completedToken = cancellationToken;
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            }
        };
        await using var dispatcher = Dispatcher.Start();
        dispatcher.UnhandledException += (_, eventArgs) =>
        {
            eventArgs.IsHandled = true;
            _ = unhandled.TrySetResult(eventArgs.Exception);
        };
        await dispatcher.InvokeAsync(() =>
        {
            input.Attach(dispatcher);
            input.SuggestionsChanged += (_, _) =>
            {
                if (throwFromCallback)
                {
                    throw expected;
                }
            };
            input.ResolutionFailed += (_, _) => failures++;
            input.Text = "query";
        }, TestContext.Current.CancellationToken);

        // Act
        completion.SetResult(["successful"]);
        var exception = await unhandled.Task.WaitAsync(TestContext.Current.CancellationToken);
        var (completedSuggestions, completedOpen) = await dispatcher.InvokeAsync(
            () => (Suggestions: input.Suggestions.ToArray(), input.IsOpen),
            TestContext.Current.CancellationToken);
        throwFromCallback = false;
        await dispatcher.InvokeAsync(() => { input.Resolver = null; }, TestContext.Current.CancellationToken);

        // Assert
        exception.ShouldBeSameAs(expected);
        completedSuggestions.ShouldBe(["successful"]);
        completedOpen.ShouldBeTrue();
        failures.ShouldBe(0);
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
        completedToken.IsCancellationRequested.ShouldBeFalse();
        await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies rejected attached publication is reported as dispatcher infrastructure,
    /// abandons and retires the lease, and never masquerades as a resolver failure.</summary>
    [Fact]
    public async Task Resolver_WhenAttachedCompletionPostIsRejected_AbandonsLeaseWithoutResolverFailureAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        try
        {
            var failures = 0;
            var calls = 0;
            dispatcher.UnhandledException += (_, eventArgs) => eventArgs.IsHandled = true;
            CancellationToken rejectedToken = default;
            var input = new SuggestionInput
            {
                Resolver = (_, cancellationToken) =>
                {
                    calls++;

                    if (calls == 1)
                    {
                        return ValueTask.FromResult<IReadOnlyList<object?>>(["prior"]);
                    }

                    rejectedToken = cancellationToken;
                    return new ValueTask<IReadOnlyList<object?>>(completion.Task);
                },
                Text = "query"
            };
            await dispatcher.InvokeAsync(() =>
            {
                input.Attach(dispatcher);
                input.ResolutionFailed += (_, _) => failures++;
                input.Refresh();
            }, TestContext.Current.CancellationToken);
            var observation = input.LastResolutionObservation.ShouldNotBeNull();
            dispatcher.Post(() =>
            {
                entered.SetResult();
                release.Wait();
            });
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            dispatcher.Post(fillerDrained.SetResult);

            // Act
            completion.SetResult(["rejected"]);
            release.Set();
            Exception? observationFailure = null;

            try
            {
                await observation.WaitAsync(TestContext.Current.CancellationToken);
            }
            catch (Exception exception)
            {
                observationFailure = exception;
            }

            await fillerDrained.Task.WaitAsync(TestContext.Current.CancellationToken);
            var resolvingAfterRejectedPublication = await dispatcher.InvokeAsync(
                () => input.IsResolving,
                TestContext.Current.CancellationToken);
            await dispatcher.InvokeAsync(
                () =>
                {
                    input.Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>([]);
                },
                TestContext.Current.CancellationToken);

            // Assert
            observationFailure.ShouldBeNull();
            failures.ShouldBe(0);
            rejectedToken.IsCancellationRequested.ShouldBeFalse();
            resolvingAfterRejectedPublication.ShouldBeFalse();
            input.Suggestions.ShouldBeEmpty();
            input.IsResolving.ShouldBeFalse();
            await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
        }
        finally
        {
            release.Set();
        }
    }

    /// <summary>Verifies dispatcher shutdown retires a rejected completion that cannot reach idle.</summary>
    [Fact]
    public async Task Resolver_WhenRejectedAttachedCompletionDispatcherStops_SettlesWithoutIdleAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var rejectionObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var calls = 0;
        var input = new SuggestionInput
        {
            Resolver = (_, _) => ++calls == 1
                ? ValueTask.FromResult<IReadOnlyList<object?>>(["prior"])
                : new ValueTask<IReadOnlyList<object?>>(completion.Task),
            Text = "query"
        };
        await dispatcher.InvokeAsync(
            () =>
            {
                input.Attach(dispatcher);
                input.Refresh();
            },
            TestContext.Current.CancellationToken);
        var observation = input.LastResolutionObservation.ShouldNotBeNull();
        dispatcher.BackgroundCompletionRetryHookForTests = rejectionObserved.SetResult;
        dispatcher.Post(() =>
        {
            callbackEntered.SetResult();
            release.Wait();
        });
        await callbackEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        dispatcher.Post(static () => { });

        try
        {
            completion.SetResult(["rejected"]);
            await rejectionObserved.Task.WaitAsync(TestContext.Current.CancellationToken);
            var stopping = dispatcher.DisposeAsync().AsTask();
            release.Set();

            await observation.WaitAsync(TestContext.Current.CancellationToken);
            await stopping.WaitAsync(TestContext.Current.CancellationToken);

            input.IsResolving.ShouldBeFalse();
            input.Suggestions.ShouldBe(["prior"]);
        }
        finally
        {
            release.Set();
        }
    }

    /// <summary>Verifies a newer text generation cancels and outranks an older non-cooperative
    /// detached resolver completion.</summary>
    [Fact]
    public async Task Text_WhenEarlierResolutionCompletesLast_DiscardsStaleSnapshotAsync()
    {
        // Arrange
        var first = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var currentPublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken firstCancellation = default;
        using var input = new SuggestionInput
        {
            Resolver = (searchTerms, cancellationToken) =>
            {
                if (searchTerms == "first")
                {
                    firstCancellation = cancellationToken;
                    return new ValueTask<IReadOnlyList<object?>>(first.Task);
                }

                return new ValueTask<IReadOnlyList<object?>>(second.Task);
            }
        };
        input.SuggestionsChanged += (_, _) =>
        {
            if (input.Suggestions is ["current"])
            {
                _ = currentPublished.TrySetResult();
            }
        };

        // Act
        input.Text = "first";
        var firstObservation = input.LastResolutionObservation.ShouldNotBeNull();
        input.Text = "second";
        var secondObservation = input.LastResolutionObservation.ShouldNotBeNull();
        second.SetResult(["current"]);
        await currentPublished.Task.WaitAsync(TestContext.Current.CancellationToken);
        await secondObservation.WaitAsync(TestContext.Current.CancellationToken);
        first.SetResult(["stale"]);
        await firstObservation.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        firstCancellation.IsCancellationRequested.ShouldBeTrue();
        input.Suggestions.ShouldBe(["current"]);
        input.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies a resolver replacement with null cancels the pending generation and
    /// synchronously clears and closes its stale snapshot.</summary>
    [Fact]
    public void Resolver_WhenCleared_CancelsPendingGenerationAndClearsSnapshot()
    {
        // Arrange
        var pending = new TaskCompletionSource<IReadOnlyList<object?>>();
        CancellationToken cancellation = default;
        using var input = new SuggestionInput
        {
            Resolver = static (searchTerms, _) =>
                ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]),
            Text = "query"
        };
        input.Resolver = (_, cancellationToken) =>
        {
            cancellation = cancellationToken;
            return new ValueTask<IReadOnlyList<object?>>(pending.Task);
        };

        // Act
        input.Resolver = null;

        // Assert
        cancellation.IsCancellationRequested.ShouldBeTrue();
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies publishing equal resolver values still replaces caller storage but does
    /// not raise a false changed-snapshot notification.</summary>
    [Fact]
    public void Refresh_WhenSnapshotValuesAreEqual_CopiesWithoutRepublishingChange()
    {
        // Arrange
        var source = new List<object?> { "same" };
        using var input = new SuggestionInput
        {
            Resolver = (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(source),
            Text = "query"
        };
        var changes = 0;
        input.SuggestionsChanged += (_, _) => changes++;

        // Act
        input.Refresh();
        source[0] = "mutated";

        // Assert
        changes.ShouldBe(0);
        input.Suggestions.ShouldBe(["same"]);
    }

    /// <summary>Verifies explicitly closing while a current request is pending preserves the
    /// request and snapshot publication but suppresses its automatic open.</summary>
    [Fact]
    public async Task Close_WhenResolutionIsPending_PublishesWithoutReopeningAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var input = new SuggestionInput
        {
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        input.SuggestionsChanged += (_, _) => _ = published.TrySetResult();
        input.Text = "query";

        // Act
        input.Close();
        completion.SetResult(["result"]);
        await published.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        input.Suggestions.ShouldBe(["result"]);
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies threshold eligibility counts Unicode extended grapheme clusters,
    /// including combining and emoji ZWJ sequences, rather than UTF-16 code units.</summary>
    [Fact]
    public void MinimumPrefixLength_WhenTextContainsCombiningAndZwjSequences_CountsGraphemes()
    {
        // Arrange
        var queries = new List<string>();
        using var input = new SuggestionInput
        {
            MinimumPrefixLength = 2,
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
            }
        };
        queries.Clear();

        // Act
        input.Text = "e\u0301";
        input.Text = "e\u0301👩‍👩‍👧‍👦";

        // Assert
        queries.ShouldBe(["e\u0301👩‍👩‍👧‍👦"]);
        input.Suggestions.ShouldBe(["e\u0301👩‍👩‍👧‍👦"]);
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies raising the threshold past the committed grapheme count publishes the
    /// threshold first, then synchronously clears results and closes.</summary>
    [Fact]
    public void MinimumPrefixLength_WhenRaisedPastCurrentGraphemeCount_ClearsAndCloses()
    {
        // Arrange
        using var input = new SuggestionInput
        {
            Resolver = static (searchTerms, _) =>
                ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]),
            Text = "e\u0301👩‍👩‍👧‍👦"
        };
        var publications = new List<string>();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.MinimumPrefixLength))
            {
                publications.Add("threshold");
            }
            else if (eventArgs.PropertyName == nameof(SuggestionInput.Suggestions))
            {
                publications.Add("suggestions");
            }
            else if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen))
            {
                publications.Add("closed");
            }
        };
        input.SuggestionsChanged += (_, _) => publications.Add("event");

        // Act
        input.MinimumPrefixLength = 3;

        // Assert
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
        publications.ShouldBe(["threshold", "suggestions", "event", "closed"]);
    }

    /// <summary>Verifies lowering the threshold starts resolution only after the changed
    /// threshold is visible to its property observers.</summary>
    [Fact]
    public void MinimumPrefixLength_WhenLowered_ReevaluatesAfterPropertyNotification()
    {
        // Arrange
        var publications = new List<string>();
        using var input = new SuggestionInput
        {
            MinimumPrefixLength = 2,
            Text = "e\u0301",
            Resolver = (searchTerms, _) =>
            {
                publications.Add($"resolver:{searchTerms}");
                return ValueTask.FromResult<IReadOnlyList<object?>>(["eligible"]);
            }
        };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.MinimumPrefixLength))
            {
                publications.Add($"threshold:{input.Suggestions.Count}");
            }
        };

        // Act
        input.MinimumPrefixLength = 1;

        // Assert
        publications.ShouldBe(["threshold:0", "resolver:e\u0301"]);
        input.Suggestions.ShouldBe(["eligible"]);
    }

    /// <summary>Verifies a failing observer cannot strand later observers or the dependent
    /// resolution transition after the threshold has committed.</summary>
    [Fact]
    public void MinimumPrefixLength_WhenPropertyObserverThrows_CompletesCurrentTransitionBeforeRethrow()
    {
        // Arrange
        var failure = new InvalidOperationException("observer failure");
        var laterObserverCalls = 0;
        var resolverCalls = 0;
        using var input = new SuggestionInput
        {
            MinimumPrefixLength = 2,
            Text = "q",
            Resolver = (searchTerms, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
            }
        };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.MinimumPrefixLength))
            {
                throw failure;
            }
        };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.MinimumPrefixLength))
            {
                laterObserverCalls++;
            }
        };

        // Act
        var thrown = Should.Throw<InvalidOperationException>(() => input.MinimumPrefixLength = 1);

        // Assert
        thrown.ShouldBeSameAs(failure);
        laterObserverCalls.ShouldBe(1);
        resolverCalls.ShouldBe(1);
        input.Suggestions.ShouldBe(["q"]);
    }

    /// <summary>Verifies resolver replacement uses the same failure-safe current transition as
    /// threshold changes.</summary>
    [Fact]
    public void Resolver_WhenPropertyObserverThrows_CompletesCurrentTransitionBeforeRethrow()
    {
        // Arrange
        var failure = new InvalidOperationException("observer failure");
        var laterObserverCalls = 0;
        var resolverCalls = 0;
        using var input = new SuggestionInput { Text = "query" };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.Resolver))
            {
                throw failure;
            }
        };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.Resolver))
            {
                laterObserverCalls++;
            }
        };
        ValueTask<IReadOnlyList<object?>> Resolve(string searchTerms, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            resolverCalls++;
            return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
        }

        // Act
        var thrown = Should.Throw<InvalidOperationException>(() => input.Resolver = Resolve);

        // Assert
        thrown.ShouldBeSameAs(failure);
        laterObserverCalls.ShouldBe(1);
        resolverCalls.ShouldBe(1);
        input.Suggestions.ShouldBe(["query"]);
    }

    /// <summary>Verifies resolver storage preserves its reference-identity contract even when two
    /// delegate instances compare equal by invocation list.</summary>
    [Fact]
    public void Resolver_WhenDelegateIsValueEqualButDistinct_ReplacesStoredReference()
    {
        // Arrange
        static ValueTask<IReadOnlyList<object?>> Resolve(
            string searchTerms,
            CancellationToken cancellationToken)
        {
            _ = searchTerms;
            _ = cancellationToken;
            return ValueTask.FromResult<IReadOnlyList<object?>>([]);
        }

        var first = new SuggestionResolver(Resolve);
        var second = new SuggestionResolver(Resolve);
        ReferenceEquals(first, second).ShouldBeFalse();
        first.ShouldBe(second);
        using var input = new SuggestionInput { Resolver = first };
        var changes = 0;
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.Resolver))
            {
                changes++;
            }
        };

        // Act
        input.Resolver = second;

        // Assert
        ReferenceEquals(input.Resolver, second).ShouldBeTrue();
        changes.ShouldBe(1);
    }

    /// <summary>Verifies a reentrant resolver-property observer supersedes the outer assignment
    /// even when the outer continuation would otherwise start a request.</summary>
    [Fact]
    public void Resolver_WhenPropertyObserverReplacesResolver_InvokesOnlyReentrantResolver()
    {
        // Arrange
        var calls = new List<string>();
        using var input = new SuggestionInput { Text = "query" };
        SuggestionResolver replacement = (searchTerms, _) =>
        {
            calls.Add($"replacement:{searchTerms}");
            return ValueTask.FromResult<IReadOnlyList<object?>>(["new"]);
        };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.Resolver) &&
                !ReferenceEquals(input.Resolver, replacement))
            {
                input.Resolver = replacement;
            }
        };

        // Act
        input.Resolver = (searchTerms, _) =>
        {
            calls.Add($"outer:{searchTerms}");
            return ValueTask.FromResult<IReadOnlyList<object?>>(["old"]);
        };

        // Assert
        calls.ShouldBe(["replacement:query"]);
        input.Suggestions.ShouldBe(["new"]);
    }

    /// <summary>Verifies a reentrant Text property observer makes the nested commit the only
    /// resolver search admitted after notification.</summary>
    [Fact]
    public void Text_WhenPropertyObserverCommitsNewerText_ResolvesOnlyNewestCommit()
    {
        // Arrange
        var queries = new List<string>();
        using var input = new SuggestionInput
        {
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
            }
        };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.Text) && input.Text == "old")
            {
                input.Text = "new";
            }
        };

        // Act
        input.Text = "old";

        // Assert
        input.Text.ShouldBe("new");
        queries.ShouldBe(["new"]);
        input.Suggestions.ShouldBe(["new"]);
    }

    /// <summary>Verifies a Text observer that replaces the resolver starts the dependent
    /// generation once rather than letting the outer Text continuation repeat it.</summary>
    [Fact]
    public void Text_WhenPropertyObserverReplacesResolver_ResolvesCurrentTextOnce()
    {
        // Arrange
        var queries = new List<string>();
        using var input = new SuggestionInput
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>([])
        };
        ValueTask<IReadOnlyList<object?>> ResolveReplacement(
            string searchTerms,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            queries.Add(searchTerms);
            return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
        }

        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.Text))
            {
                input.Resolver = ResolveReplacement;
            }
        };

        // Act
        input.Text = "query";

        // Assert
        queries.ShouldBe(["query"]);
        input.Suggestions.ShouldBe(["query"]);
        input.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies cancellation reentry can start the authoritative text generation without
    /// the superseded outer Begin call stranding the resolving flag.</summary>
    [Fact]
    public void Text_WhenCancellationCallbackStartsNewGeneration_KeepsReentrantCompletionCurrent()
    {
        // Arrange
        var queries = new List<string>();
        var first = new TaskCompletionSource<IReadOnlyList<object?>>();
        using var input = new SuggestionInput();
        input.Resolver = (searchTerms, cancellationToken) =>
        {
            queries.Add(searchTerms);

            if (searchTerms == "first")
            {
                _ = cancellationToken.Register(() => input.Text = "third");
                return new ValueTask<IReadOnlyList<object?>>(first.Task);
            }

            return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
        };
        input.Text = "first";

        // Act
        input.Text = "second";

        // Assert
        input.Text.ShouldBe("third");
        queries.ShouldBe(["first", "third"]);
        input.Suggestions.ShouldBe(["third"]);
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a current synchronous failure clears prior suggestions, closes, and
    /// publishes the failure after state notifications in the specified order.</summary>
    [Fact]
    public void Resolver_WhenCurrentCallFailsSynchronously_ClearsClosesThenRaisesFailure()
    {
        // Arrange
        using var input = new SuggestionInput
        {
            Resolver = static (searchTerms, _) => searchTerms == "success"
                ? ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms])
                : throw new InvalidOperationException("broken"),
            Text = "success"
        };
        var publications = new List<string>();
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.IsResolving))
            {
                publications.Add($"resolving:{input.IsResolving}");
            }
            else if (eventArgs.PropertyName == nameof(SuggestionInput.Suggestions))
            {
                publications.Add("property");
            }
            else if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen))
            {
                publications.Add("closed");
            }
        };
        input.SuggestionsChanged += (_, _) => publications.Add("changed");
        input.ResolutionFailed += (_, eventArgs) => publications.Add(
            $"failure:{eventArgs.SearchTerms}:{eventArgs.Exception.Message}");

        // Act
        input.Text = "failure";

        // Assert
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
        publications.ShouldBe([
            "resolving:True",
            "resolving:False",
            "property",
            "closed",
            "changed",
            "failure:failure:broken"
        ]);
    }

    /// <summary>Verifies a null asynchronous completion is a current failure with the captured
    /// search text and never publishes an invalid snapshot.</summary>
    [Fact]
    public async Task Resolver_WhenAsyncCompletionIsNull_RaisesCurrentFailureAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = new TaskCompletionSource<SuggestionResolutionFailedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var input = new SuggestionInput
        {
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        input.ResolutionFailed += (_, eventArgs) => failed.TrySetResult(eventArgs);
        input.Text = "query";

        // Act
        completion.SetResult(null!);
        var failure = await failed.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        failure.SearchTerms.ShouldBe("query");
        _ = failure.Exception.ShouldBeOfType<InvalidOperationException>();
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a current cancelled asynchronous resolver settles silently without a
    /// failure event or stale results.</summary>
    [Fact]
    public async Task Resolver_WhenCurrentCompletionIsCancelled_SettlesSilentlyAsync()
    {
        // Arrange
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failures = 0;
        using var input = new SuggestionInput
        {
            Resolver = static (_, _) => ValueTask.FromCanceled<IReadOnlyList<object?>>(
                new CancellationToken(canceled: true))
        };
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.IsResolving) && !input.IsResolving)
            {
                _ = settled.TrySetResult();
            }
        };
        input.ResolutionFailed += (_, _) => failures++;

        // Act
        input.Text = "cancel";
        await settled.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        failures.ShouldBe(0);
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies attaching after a detached request starts revokes that request and drops
    /// its later non-cooperative completion.</summary>
    [Fact]
    public async Task Resolver_WhenDetachedRequestIsAttachedBeforeCompletion_DiscardsCompletionAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken cancellation = default;
        var input = new SuggestionInput
        {
            Resolver = (_, cancellationToken) =>
            {
                cancellation = cancellationToken;
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            },
            Text = "query"
        };
        var observation = input.LastResolutionObservation.ShouldNotBeNull();
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() => input.Attach(dispatcher), TestContext.Current.CancellationToken);

        // Act
        completion.SetResult(["stale"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        cancellation.IsCancellationRequested.ShouldBeTrue();
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detached result publication excludes an attachment context commit until
    /// the complete result transition and its callbacks release structural authority.</summary>
    [Fact]
    public async Task Resolver_WhenDetachedPublicationOwnsBoundary_AttachmentWaitsForCompletePublicationAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var publicationAcquired = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleWaited = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var attachmentCommitted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releasePublication = new ManualResetEventSlim();
        await using var dispatcher = Dispatcher.Start();
        try
        {
            var publications = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var input = new SuggestionInput
            {
                Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task),
                BeforeDetachedResolutionPublication = () =>
                {
                    publicationAcquired.SetResult();
                    releasePublication.Wait();
                },
                Text = "query"
            };
            input.OwnedControls.PublicationWaitStarted = () => lifecycleWaited.TrySetResult();
            var observation = input.LastResolutionObservation.ShouldNotBeNull();
            input.SuggestionsChanged += (_, _) => publications.Enqueue("suggestions");
            var completeResolver = Task.Run(
                () => completion.SetResult(["stale"]),
                TestContext.Current.CancellationToken);
            await publicationAcquired.Task.WaitAsync(TestContext.Current.CancellationToken);
            var attach = dispatcher.InvokeAsync(
                () => input.Attach(
                    dispatcher,
                    UnicodePolicy.Default,
                    new Theme(),
                    () =>
                    {
                        publications.Enqueue("attachment");
                        attachmentCommitted.SetResult();
                    }),
                TestContext.Current.CancellationToken).AsTask();

            // Act
            var firstLifecycleBoundary = await Task.WhenAny(
                lifecycleWaited.Task,
                attachmentCommitted.Task).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before release
            firstLifecycleBoundary.ShouldBeSameAs(lifecycleWaited.Task);
            input.Dispatcher.ShouldBeNull();
            input.Suggestions.ShouldBeEmpty();
            input.IsResolving.ShouldBeTrue();
            releasePublication.Set();

            await observation.WaitAsync(TestContext.Current.CancellationToken);
            await completeResolver;
            await attach;

            // Assert after release
            publications.ShouldBe(["suggestions", "attachment"]);
            input.Dispatcher.ShouldBeSameAs(dispatcher);
            input.Suggestions.ShouldBe(["stale"]);
            input.IsResolving.ShouldBeFalse();
            await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
        }
        finally
        {
            releasePublication.Set();
        }
    }

    /// <summary>Verifies inline resolver completion from ParentChanged retries after ownership publication closes.</summary>
    [Fact]
    public async Task Resolver_WhenParentChangedCompletesPendingRequest_SettlesAfterOwnershipPublicationAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>();
        var input = new SuggestionInput
        {
            Text = "query",
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        var observation = input.LastResolutionObservation.ShouldNotBeNull();
        var owner = new ProbeOwnedControl();
        input.ParentChanged += (_, _) => _ = completion.TrySetResult(["resolved"]);

        owner.AddPrimary(input);
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        input.Parent.ShouldBeSameAs(owner);
        input.Suggestions.ShouldBe(["resolved"]);
        input.IsResolving.ShouldBeFalse();
        input.Dispose();
        owner.Dispose();
    }

    /// <summary>Verifies a synchronous resolver installed from ParentChanged publishes after ownership commits.</summary>
    [Fact]
    public async Task Resolver_WhenParentChangedSetsSynchronousResolver_SettlesAfterOwnershipPublicationAsync()
    {
        var input = new SuggestionInput { Text = "query" };
        var owner = new ProbeOwnedControl();
        input.ParentChanged += (_, _) =>
            input.Resolver = static (_, _) =>
                ValueTask.FromResult<IReadOnlyList<object?>>(["resolved"]);

        owner.AddPrimary(input);
        var observation = input.LastResolutionObservation.ShouldNotBeNull();
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        input.Parent.ShouldBeSameAs(owner);
        input.Suggestions.ShouldBe(["resolved"]);
        input.IsResolving.ShouldBeFalse();
        input.Dispose();
        owner.Dispose();
    }

    /// <summary>Verifies a first result completed during direct unavailability stays closed until explicit recovery.</summary>
    /// <param name="hide">Whether visibility rather than enabled state makes the owner unavailable.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolver_WhenFirstResultCompletesDuringDirectUnavailability_PreservesClosedSnapshotAsync(
        bool hide)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var input = new SuggestionInput
        {
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => input.Text = "query", "start pending first suggestion request");
        var observation = input.LastResolutionObservation.ShouldNotBeNull();
        await surface.UpdateAsync(
            () =>
            {
                if (hide)
                {
                    input.Visibility = Visibility.Hidden;
                }
                else
                {
                    input.IsEnabled = false;
                }
            },
            "make pending suggestion owner unavailable");

        completion.SetResult(["resolved"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        input.Suggestions.ShouldBe(["resolved"]);
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.UpdateAsync(
            () =>
            {
                input.Visibility = Visibility.Visible;
                input.IsEnabled = true;
                _ = input.Open();
            },
            "restore and explicitly open suggestion owner");

        input.IsOpen.ShouldBeTrue();
        _ = surface.Application.Modality.Active.ShouldNotBeNull();
    }

    /// <summary>Verifies the property setter waits for the current generation instead of reopening stale rows.</summary>
    [Fact]
    public async Task IsOpen_WhenNewerResolutionIsPending_DoesNotOpenStaleSnapshotAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var input = new SuggestionInput
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["old"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(20, 6),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => input.Text = "query", "resolve initial suggestion snapshot");
        await surface.UpdateAsync(
            () =>
            {
                input.Close();
                input.Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task);
            },
            "start newer pending suggestion generation");
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        await surface.UpdateAsync(() => input.IsOpen = true, "request opening while snapshot is stale");

        input.Suggestions.ShouldBe(["old"]);
        input.IsResolving.ShouldBeTrue();
        input.IsOpen.ShouldBeFalse();

        completion.SetResult(["new"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "drain current suggestion publication");

        input.Suggestions.ShouldBe(["new"]);
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a resolver that returns synchronously after attachment commits cannot
    /// bypass its captured detached authority or mutate the newly attached tree.</summary>
    [Fact]
    public async Task Resolver_WhenAttachmentCommitsBeforeSynchronousReturn_DiscardsCompletionAsync()
    {
        // Arrange
        var resolverEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var attachmentCommitted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleWaited = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseResolver = new ManualResetEventSlim();
        using var releaseAttachment = new ManualResetEventSlim();
        await using var dispatcher = Dispatcher.Start();
        try
        {
            var input = new SuggestionInput
            {
                Resolver = (_, cancellationToken) =>
                {
                    resolverEntered.SetResult();
                    releaseResolver.Wait(cancellationToken);
                    return ValueTask.FromResult<IReadOnlyList<object?>>(["stale"]);
                }
            };
            input.OwnedControls.PublicationWaitStarted = () => lifecycleWaited.TrySetResult();
            var resolve = Task.Run(
                () => input.Text = "query",
                TestContext.Current.CancellationToken);
            await resolverEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
            var attach = dispatcher.InvokeAsync(
                () => input.Attach(
                    dispatcher,
                    UnicodePolicy.Default,
                    new Theme(),
                    () =>
                    {
                        attachmentCommitted.SetResult();
                        releaseAttachment.Wait();
                    }),
                TestContext.Current.CancellationToken).AsTask();
            await attachmentCommitted.Task.WaitAsync(TestContext.Current.CancellationToken);

            // Act
            releaseResolver.Set();

            var firstSettlement = await Task.WhenAny(
                lifecycleWaited.Task,
                resolve).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before lifecycle release
            firstSettlement.ShouldBeSameAs(lifecycleWaited.Task);
            input.Suggestions.ShouldBeEmpty();
            releaseAttachment.Set();

            _ = await resolve.WaitAsync(TestContext.Current.CancellationToken);
            await attach.WaitAsync(TestContext.Current.CancellationToken);

            // Assert after lifecycle release
            input.Dispatcher.ShouldBeSameAs(dispatcher);
            input.Suggestions.ShouldBeEmpty();
            input.IsResolving.ShouldBeFalse();
            await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseResolver.Set();
            releaseAttachment.Set();
        }
    }

    /// <summary>Verifies attachment reserves stable ancestry before it discovers descendants while
    /// result-list ownership is between slot and parent-identity commits.</summary>
    [Fact]
    public async Task Resolver_WhenListStructureIsMutating_AttachmentWaitsBeforeDescendantDiscoveryAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var mutationPaused = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleWaited = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var discoveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseMutation = new ManualResetEventSlim();
        await using var dispatcher = Dispatcher.Start();
        try
        {
            var pauseCount = 0;
            var input = new SuggestionInput
            {
                Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task),
                Text = "query"
            };
            var observation = input.LastResolutionObservation.ShouldNotBeNull();

            foreach (var control in OwnedTree.FindAll<ControlBase>(input))
            {
                control.OwnedControls.StructuralMutationPaused = () =>
                {
                    if (Interlocked.Exchange(ref pauseCount, 1) != 0)
                    {
                        return;
                    }

                    mutationPaused.SetResult();
                    releaseMutation.Wait();
                };
            }

            input.OwnedControls.PublicationWaitStarted = () => lifecycleWaited.TrySetResult();
            input.OwnedControls.DescendantDiscoveryStarted = () => discoveryStarted.TrySetResult();
            var complete = Task.Run(
                () => completion.SetResult(["one", "two"]),
                TestContext.Current.CancellationToken);
            await mutationPaused.Task.WaitAsync(TestContext.Current.CancellationToken);
            var attach = dispatcher.InvokeAsync(
                () => input.Attach(dispatcher),
                TestContext.Current.CancellationToken).AsTask();

            // Act
            var firstBoundary = await Task.WhenAny(
                lifecycleWaited.Task,
                discoveryStarted.Task).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before mutation release
            firstBoundary.ShouldBeSameAs(lifecycleWaited.Task);
            input.Dispatcher.ShouldBeNull();
            releaseMutation.Set();

            await observation.WaitAsync(TestContext.Current.CancellationToken);
            await complete.WaitAsync(TestContext.Current.CancellationToken);
            await attach.WaitAsync(TestContext.Current.CancellationToken);

            // Assert after mutation release
            input.Suggestions.ShouldBe(["one", "two"]);
            input.Dispatcher.ShouldBeSameAs(dispatcher);

            foreach (var control in OwnedTree.FindAll<ControlBase>(input))
            {
                control.Dispatcher.ShouldBeSameAs(dispatcher);
            }

            await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseMutation.Set();
        }
    }

    /// <summary>Verifies threshold and resolver clearing use the same detached publication
    /// authority as resolver completion.</summary>
    /// <param name="clearWithThreshold">Whether the threshold, rather than resolver, causes clearing.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Settlement_WhenEligibilityClears_UsesDetachedPublicationAuthorityAsync(
        bool clearWithThreshold)
    {
        // Arrange
        var publicationAcquired = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleWaited = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releasePublication = new ManualResetEventSlim();
        using var input = new SuggestionInput
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["current"]),
            Text = "query"
        };
        try
        {
            input.BeforeDetachedResolutionPublication = () =>
            {
                publicationAcquired.SetResult();
                releasePublication.Wait();
            };
            input.OwnedControls.PublicationWaitStarted = () => lifecycleWaited.TrySetResult();
            var clear = Task.Run(
                () =>
                {
                    if (clearWithThreshold)
                    {
                        input.MinimumPrefixLength = 100;
                    }
                    else
                    {
                        input.Resolver = null;
                    }
                },
                TestContext.Current.CancellationToken);
            Task? disposal = null;

            // Act
            var firstSettlement = await Task.WhenAny(
                publicationAcquired.Task,
                clear).WaitAsync(TestContext.Current.CancellationToken);
            firstSettlement.ShouldBeSameAs(publicationAcquired.Task);
            disposal = Task.Run(input.Dispose, TestContext.Current.CancellationToken);
            var firstLifecycleBoundary = await Task.WhenAny(
                lifecycleWaited.Task,
                disposal).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before release
            firstLifecycleBoundary.ShouldBeSameAs(lifecycleWaited.Task);
            input.Suggestions.ShouldBe(["current"]);
            input.IsDisposed.ShouldBeFalse();
            releasePublication.Set();

            await clear.WaitAsync(TestContext.Current.CancellationToken);
            await disposal.ShouldNotBeNull().WaitAsync(TestContext.Current.CancellationToken);

            // Assert after release
            input.IsDisposed.ShouldBeTrue();
        }
        finally
        {
            releasePublication.Set();
        }
    }

    /// <summary>Verifies terminal disposal of a retained ancestor prevents a reentrant refresh
    /// from invoking the resolver before descendant teardown begins.</summary>
    [Fact]
    public void Refresh_WhenRetainedAncestorStartsTerminalDisposal_DoesNotStartResolution()
    {
        // Arrange
        var resolverCalls = 0;
        using var input = new SuggestionInput
        {
            MinimumPrefixLength = 100,
            Resolver = (_, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<IReadOnlyList<object?>>(["unexpected"]);
            },
            Text = "query"
        };
        var owner = new ProbeOwnedControl();
        owner.AddPrimary(input);
        owner.DirectDisposalRequesting = _ => input.MinimumPrefixLength = 0;

        // Act
        owner.Dispose();

        // Assert
        resolverCalls.ShouldBe(0);
        owner.IsDisposed.ShouldBeTrue();
        input.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies an attached completion is discarded after same-dispatcher detach and
    /// reattach invalidate its captured identity.</summary>
    [Fact]
    public async Task Resolver_WhenAttachmentChangesBeforeCompletion_DiscardsCompletionAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>();
        var publications = 0;
        var input = new SuggestionInput
        {
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        await using var dispatcher = Dispatcher.Start();
        Task observation = null!;
        var detachCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        try
        {
            await dispatcher.InvokeAsync(() =>
            {
                input.Attach(dispatcher);
                input.SuggestionsChanged += (_, _) => publications++;
                input.Text = "query";
                observation = input.LastResolutionObservation.ShouldNotBeNull();
            }, TestContext.Current.CancellationToken);
            dispatcher.Post(release.Wait);
            dispatcher.Post(() =>
            {
                input.Detach();
                input.Attach(dispatcher);
                detachCompleted.SetResult();
            });

            // Act
            completion.SetResult(["stale"]);
            release.Set();
            await detachCompleted.Task.WaitAsync(TestContext.Current.CancellationToken);
            await observation.WaitAsync(TestContext.Current.CancellationToken);
            await dispatcher.InvokeAsync(() => { }, TestContext.Current.CancellationToken);

            // Assert
            publications.ShouldBe(0);
            input.Suggestions.ShouldBeEmpty();
            input.IsResolving.ShouldBeFalse();
            await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
        }
        finally
        {
            release.Set();
        }
    }

    /// <summary>Verifies disposal revokes a detached pending request and suppresses every late
    /// state and event publication.</summary>
    [Fact]
    public async Task Resolver_WhenDisposedBeforeCompletion_DiscardsCompletionAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var publications = 0;
        CancellationToken cancellation = default;
        var input = new SuggestionInput
        {
            Resolver = (_, cancellationToken) =>
            {
                cancellation = cancellationToken;
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            },
            Text = "query"
        };
        input.SuggestionsChanged += (_, _) => publications++;
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        // Act
        input.Dispose();
        completion.SetResult(["late"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        cancellation.IsCancellationRequested.ShouldBeTrue();
        input.IsResolving.ShouldBeFalse();
        publications.ShouldBe(0);
    }

    /// <summary>Verifies detach aggregates a throwing popup close with resolver cancellation and
    /// clearing the resolving flag before the first lifecycle failure is rethrown.</summary>
    [Fact]
    public async Task Detach_WhenPopupCleanupThrows_StillCancelsAndSettlesResolverAsync()
    {
        // Arrange
        var pending = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("popup close failed");
        CancellationToken cancellation = default;
        var input = new SuggestionInput
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["current"]),
            Text = "query"
        };
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            input.Attach(dispatcher);
            input.Resolver = (_, cancellationToken) =>
            {
                cancellation = cancellationToken;
                return new ValueTask<IReadOnlyList<object?>>(pending.Task);
            };
            input.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen) && !input.IsOpen)
                {
                    throw expected;
                }
            };
        }, TestContext.Current.CancellationToken);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.InvokeAsync(input.Detach, TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeSameAs(expected);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        input.Dispatcher.ShouldBeNull();
        input.IsResolving.ShouldBeFalse();
        input.Dispose();
    }

    /// <summary>Verifies resolver cancellation cannot reenter detach to start a request that no
    /// attachment will own, and a late non-cooperative completion stays discarded.</summary>
    [Fact]
    public async Task Detach_WhenCancellationReentersRefresh_SuppressesReplacementAndLateCompletionAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CancellationTokenRegistration registration = default;
        var input = new SuggestionInput();
        await using var dispatcher = Dispatcher.Start();
        Task observation = null!;
        await dispatcher.InvokeAsync(() =>
        {
            input.Attach(dispatcher);
            input.Resolver = (_, cancellationToken) =>
            {
                calls++;
                registration = cancellationToken.Register(input.Refresh);
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            };
            input.Text = "query";
            observation = input.LastResolutionObservation.ShouldNotBeNull();
        }, TestContext.Current.CancellationToken);

        // Act
        await dispatcher.InvokeAsync(input.Detach, TestContext.Current.CancellationToken);
        completion.SetResult(["late"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(() => { }, TestContext.Current.CancellationToken);

        // Assert
        calls.ShouldBe(1);
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        registration.Dispose();
        input.Dispose();
    }

    /// <summary>Verifies resolver cancellation cannot reenter disposal to strand a replacement,
    /// and terminal cleanup rejects the original request's late completion.</summary>
    [Fact]
    public async Task Dispose_WhenCancellationReentersRefresh_SuppressesReplacementAndLateCompletionAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CancellationTokenRegistration registration = default;
        var input = new SuggestionInput();
        input.Resolver = (_, cancellationToken) =>
        {
            calls++;
            registration = cancellationToken.Register(input.Refresh);
            return new ValueTask<IReadOnlyList<object?>>(completion.Task);
        };
        input.Text = "query";
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        // Act
        input.Dispose();
        completion.SetResult(["late"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        calls.ShouldBe(1);
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        registration.Dispose();
    }

    /// <summary>Verifies detached result publication excludes terminal disposal, releases its
    /// structural authority after callback failure, and lets disposal invalidate later work.</summary>
    [Fact]
    public async Task Dispose_WhenDetachedPublicationOwnsBoundary_WaitsAndCompletesAfterCallbackFailureAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var publicationAcquired = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleWaited = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releasePublication = new ManualResetEventSlim();
        try
        {
            var expected = new InvalidOperationException("late suggestion publication");
            var input = new SuggestionInput
            {
                Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["current"]),
                Text = "query",
                BeforeDetachedResolutionPublication = () =>
                {
                    publicationAcquired.SetResult();
                    releasePublication.Wait();
                }
            };
            input.OwnedControls.PublicationWaitStarted = () => lifecycleWaited.TrySetResult();
            input.Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task);
            var observation = input.LastResolutionObservation.ShouldNotBeNull();
            input.SuggestionsChanged += (_, _) => throw expected;
            var completeResolver = Task.Run(
                () => completion.SetResult(["late"]),
                TestContext.Current.CancellationToken);
            await publicationAcquired.Task.WaitAsync(TestContext.Current.CancellationToken);

            // Act
            var disposal = Task.Run(input.Dispose, TestContext.Current.CancellationToken);
            var firstLifecycleBoundary = await Task.WhenAny(
                lifecycleWaited.Task,
                disposal).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before release
            firstLifecycleBoundary.ShouldBeSameAs(lifecycleWaited.Task);
            input.IsDisposed.ShouldBeFalse();
            input.IsDisposing.ShouldBeFalse();
            releasePublication.Set();

            var publicationFailure = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await observation.WaitAsync(TestContext.Current.CancellationToken));
            await completeResolver;
            await disposal.WaitAsync(TestContext.Current.CancellationToken);

            // Assert after release
            publicationFailure.ShouldBeSameAs(expected);
            input.IsResolving.ShouldBeFalse();
            input.IsDisposed.ShouldBeTrue();
        }
        finally
        {
            releasePublication.Set();
        }
    }

    /// <summary>Verifies a lifecycle mutation attempted from a detached result callback is
    /// rejected before disposal starts and does not strand the publication authority.</summary>
    [Fact]
    public async Task SuggestionsChanged_WhenDetachedPublicationDisposesOwner_RejectsBeforeLifecycleMutationAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? disposalFailure = null;
        var disposedDuringCallback = false;
        var input = new SuggestionInput
        {
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task),
            Text = "query"
        };
        input.SuggestionsChanged += (_, _) =>
        {
            disposalFailure = Record.Exception(input.Dispose);
            disposedDuringCallback = input.IsDisposed;
        };
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        // Act
        completion.SetResult(["published"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        _ = disposalFailure.ShouldBeOfType<InvalidOperationException>();
        disposedDuringCallback.ShouldBeFalse();
        input.IsDisposed.ShouldBeFalse();
        input.Suggestions.ShouldBe(["published"]);
        input.Dispose();
    }

    /// <summary>Verifies a newer asynchronous generation can publish on the same thread while an
    /// outer detached result callback still owns the lifecycle boundary.</summary>
    [Fact]
    public async Task SuggestionsChanged_WhenNewerDetachedCompletionNests_SettlesNewGenerationAsync()
    {
        // Arrange
        var firstCompletion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var nestedCompletion = new TaskCompletionSource<IReadOnlyList<object?>>();
        var input = new SuggestionInput
        {
            Resolver = (searchTerms, _) => searchTerms == "first"
                ? new ValueTask<IReadOnlyList<object?>>(firstCompletion.Task)
                : new ValueTask<IReadOnlyList<object?>>(nestedCompletion.Task),
            Text = "first"
        };
        var firstObservation = input.LastResolutionObservation.ShouldNotBeNull();
        Task? nestedObservation = null;
        input.SuggestionsChanged += (_, _) =>
        {
            if (input.Suggestions.SequenceEqual(["first result"]))
            {
                input.Text = "newer";
                nestedObservation = input.LastResolutionObservation.ShouldNotBeNull();
                nestedCompletion.SetResult(["newer result"]);
            }
        };

        // Act
        firstCompletion.SetResult(["first result"]);
        await firstObservation.WaitAsync(TestContext.Current.CancellationToken);
        await nestedObservation.ShouldNotBeNull().WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        input.Suggestions.ShouldBe(["newer result"]);
        input.IsResolving.ShouldBeFalse();
        input.Dispose();
    }

    /// <summary>Verifies reciprocal detached callbacks reject cross-root lifecycle waits before
    /// either worker can deadlock and release both publication reservations.</summary>
    [Fact]
    public async Task SuggestionsChanged_WhenTwoRootsDisposeEachOther_RejectsCycleAndReleasesReservationsAsync()
    {
        // Arrange
        var firstCompletion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var callbacksReady = new CountdownEvent(initialCount: 2);
        using var releaseCallbacks = new ManualResetEventSlim();
        try
        {
            Exception? firstFailure = null;
            Exception? secondFailure = null;
            var first = new SuggestionInput
            {
                Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(firstCompletion.Task),
                Text = "first"
            };
            var second = new SuggestionInput
            {
                Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(secondCompletion.Task),
                Text = "second"
            };
            var firstObservation = first.LastResolutionObservation.ShouldNotBeNull();
            var secondObservation = second.LastResolutionObservation.ShouldNotBeNull();
            first.SuggestionsChanged += (_, _) =>
            {
                _ = callbacksReady.Signal();
                releaseCallbacks.Wait(TestContext.Current.CancellationToken);
                firstFailure = Record.Exception(second.Dispose);
            };
            second.SuggestionsChanged += (_, _) =>
            {
                _ = callbacksReady.Signal();
                releaseCallbacks.Wait(TestContext.Current.CancellationToken);
                secondFailure = Record.Exception(first.Dispose);
            };
            var completeFirst = Task.Run(
                () => firstCompletion.SetResult(["first result"]),
                TestContext.Current.CancellationToken);
            var completeSecond = Task.Run(
                () => secondCompletion.SetResult(["second result"]),
                TestContext.Current.CancellationToken);
            callbacksReady.Wait(TestContext.Current.CancellationToken);

            // Act
            releaseCallbacks.Set();
            await Task.WhenAll(firstObservation, secondObservation)
                .WaitAsync(TestContext.Current.CancellationToken);
            await Task.WhenAll(completeFirst, completeSecond)
                .WaitAsync(TestContext.Current.CancellationToken);

            // Assert
            (firstFailure is InvalidOperationException || secondFailure is InvalidOperationException)
                .ShouldBeTrue();

            if (firstFailure is null)
            {
                second.IsDisposed.ShouldBeTrue();
            }
            else
            {
                _ = firstFailure.ShouldBeOfType<InvalidOperationException>();
            }

            if (secondFailure is null)
            {
                first.IsDisposed.ShouldBeTrue();
            }
            else
            {
                _ = secondFailure.ShouldBeOfType<InvalidOperationException>();
            }

            if (!first.IsDisposed)
            {
                first.Dispose();
            }

            if (!second.IsDisposed)
            {
                second.Dispose();
            }

            first.IsDisposed.ShouldBeTrue();
            second.IsDisposed.ShouldBeTrue();
        }
        finally
        {
            releaseCallbacks.Set();
        }
    }

    /// <summary>Verifies a current failure callback that starts a successful request prevents the
    /// stale outer failure continuation from closing or publishing its typed event.</summary>
    [Fact]
    public void SuggestionsChanged_WhenFailureClearStartsNewResolution_KeepsNewerSuccess()
    {
        // Arrange
        var failures = 0;
        using var input = new SuggestionInput
        {
            Resolver = static (searchTerms, _) => searchTerms == "old"
                ? ValueTask.FromResult<IReadOnlyList<object?>>(["old result"])
                : ValueTask.FromResult<IReadOnlyList<object?>>(["new result"]),
            Text = "old"
        };
        input.SuggestionsChanged += (_, _) =>
        {
            if (input.Suggestions.Count == 0)
            {
                input.Resolver = static (_, _) =>
                    ValueTask.FromResult<IReadOnlyList<object?>>(["new result"]);
            }
        };
        input.ResolutionFailed += (_, _) => failures++;

        // Act
        input.Resolver = static (_, _) => throw new InvalidOperationException("stale failure");

        // Assert
        failures.ShouldBe(0);
        input.Suggestions.ShouldBe(["new result"]);
        input.IsOpen.ShouldBeFalse();
        input.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies accepted text starts its own resolver generation while the completed
    /// acceptance still publishes exactly once, after the popup has closed.</summary>
    [Fact]
    public async Task Accept_WhenTextCommitStartsNewResolution_RaisesSuggestionAcceptedOnceAsync()
    {
        // Arrange
        var queries = new List<string>();
        var accepted = new List<ItemInvokedEventArgs>();
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>(
                    searchTerms == "q" ? ["accepted"] : [$"resolved {searchTerms}"]);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        input.SuggestionAccepted += (_, eventArgs) =>
        {
            input.IsOpen.ShouldBeFalse();
            input.Text.ShouldBe("accepted");
            accepted.Add(eventArgs);
        };
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");
        input.IsOpen.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Text.ShouldBe("accepted");
        input.IsOpen.ShouldBeFalse();
        input.IsResolving.ShouldBeFalse();
        queries.ShouldBe(["q", "accepted"]);
        accepted.Count.ShouldBe(1);
        accepted[0].Index.ShouldBe(0);
        accepted[0].Item.ShouldBe("accepted");
        accepted[0].Cause.ShouldBe(ActivationCause.Keyboard);
    }

    /// <summary>Verifies replacing the activated result after close publication supersedes the
    /// pending acceptance notification even though the already-committed text remains unchanged.</summary>
    [Fact]
    public async Task Accept_WhenResultSnapshotIsReplacedDuringClose_DoesNotPublishStaleAcceptanceAsync()
    {
        // Arrange
        var accepted = 0;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["accepted"])
        };
        input.SuggestionAccepted += (_, _) => accepted++;
        input.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen) && !input.IsOpen)
            {
                input.Resolver = null;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Text.ShouldBe("accepted");
        input.Suggestions.ShouldBeEmpty();
        input.IsOpen.ShouldBeFalse();
        input.IsResolving.ShouldBeFalse();
        accepted.ShouldBe(0);
    }

    /// <summary>Verifies selector failure occurs before accepted text or popup state can mutate
    /// and propagates the original failure to the routed input caller.</summary>
    [Fact]
    public async Task Accept_WhenSelectorThrows_PropagatesWithoutChangingTextOrPopupAsync()
    {
        // Arrange
        var expected = new InvalidOperationException("selector failed");
        var accepted = 0;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["result"]),
            TextSelector = _ => throw expected
        };
        input.SuggestionAccepted += (_, _) => accepted++;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await surface.Application.Dispatcher.InvokeAsync(
                () => _ = Router.Route(
                    editor,
                    Events.Key,
                    new KeyEventArgs(new Stroke(
                        Code.Enter,
                        character: null,
                        nativeCode: 0,
                        Modifiers.None,
                        KeyAction.Press))),
                TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeSameAs(expected);
        input.Text.ShouldBe("q");
        input.Suggestions.ShouldBe(["result"]);
        input.IsOpen.ShouldBeTrue();
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
        accepted.ShouldBe(0);
    }

    /// <summary>Verifies a custom selector cannot return null and that validation precedes every
    /// observable mutation in the acceptance transaction.</summary>
    [Fact]
    public async Task Accept_WhenSelectorReturnsNull_ThrowsWithoutChangingTextOrPopupAsync()
    {
        // Arrange
        var accepted = 0;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["result"]),
            TextSelector = static _ => null!
        };
        input.SuggestionAccepted += (_, _) => accepted++;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await surface.Application.Dispatcher.InvokeAsync(
                () => _ = Router.Route(
                    editor,
                    Events.Key,
                    new KeyEventArgs(new Stroke(
                        Code.Enter,
                        character: null,
                        nativeCode: 0,
                        Modifiers.None,
                        KeyAction.Press))),
                TestContext.Current.CancellationToken));

        // Assert
        exception.Message.ShouldContain("null");
        input.Text.ShouldBe("q");
        input.Suggestions.ShouldBe(["result"]);
        input.IsOpen.ShouldBeTrue();
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
        accepted.ShouldBe(0);
    }

    /// <summary>Verifies every terminal owner-lifecycle boundary releases deferred first-row work
    /// before it can target unavailable or disposed retained parts.</summary>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("detached")]
    [InlineData("disposed")]
    public async Task Lifecycle_WhenFirstSelectionIsDeferred_ClearsPendingWorkAsync(string transition)
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();
        var input = new SuggestionInput
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["result"]),
            Text = "q"
        };
        input.HasPendingFirstSuggestionSelection.ShouldBeTrue();

        // Act
        switch (transition)
        {
            case "hidden":
                input.Visibility = Visibility.Hidden;
                break;
            case "disabled":
                input.IsEnabled = false;
                break;
            case "detached":
                await dispatcher.InvokeAsync(
                    () =>
                    {
                        input.Attach(dispatcher);
                        input.Detach();
                    },
                    TestContext.Current.CancellationToken);
                break;
            case "disposed":
                input.Dispose();
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(transition),
                    transition,
                    "The lifecycle transition is unknown.");
        }

        // Assert
        input.HasPendingFirstSuggestionSelection.ShouldBeFalse();

        if (!input.IsDisposed)
        {
            input.Dispose();
        }
    }

    /// <summary>Verifies stale completions, attachment changes, popup intent, provisional
    /// navigation, selector failure, and acceptance remain equivalent to an independent model.</summary>
    [Fact]
    public async Task Transcript_WhenSeeded_PreservesLatestResolutionAndAcceptanceInvariantsAsync()
    {
        const int seed = 0x51A7_2026;
        var random = new Random(seed);
        var transcript = new List<string>();
        var completions = new Dictionary<int, TaskCompletionSource<IReadOnlyList<object?>>>();
        var observations = new Dictionary<int, Task>();
        var queries = new Dictionary<int, string>();
        var cancellationTokens = new Dictionary<int, CancellationToken>();
        var settled = new HashSet<int>();
        var issued = 0;
        var resolveSynchronously = false;
        var actualAcceptances = new List<(object? Item, string Text, int Index, ActivationCause Cause)>();

        object?[] ResultsFor(string query) =>
        [
            $"{query}:0",
            $"{query}:1",
            $"{query}:2"
        ];

        ValueTask<IReadOnlyList<object?>> ResolveSuggestion(
            string searchTerms,
            CancellationToken cancellationToken)
        {
            if (resolveSynchronously)
            {
                return ValueTask.FromResult<IReadOnlyList<object?>>(ResultsFor(searchTerms));
            }

            var id = ++issued;
            var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            completions.Add(id, completion);
            queries.Add(id, searchTerms);
            cancellationTokens.Add(id, cancellationToken);
            return new ValueTask<IReadOnlyList<object?>>(completion.Task);
        }

        var input = new SuggestionInput
        {
            Width = Length.Cells(18),
            Height = Length.Cells(3),
            DropDownHeight = Length.Cells(4),
            Resolver = ResolveSuggestion
        };
        input.SuggestionAccepted += (_, eventArgs) => actualAcceptances.Add(
            (eventArgs.Item, input.Text, eventArgs.Index, eventArgs.Cause));
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();

        var modelText = string.Empty;
        object?[] modelSuggestions = [];
        var modelResolving = false;
        var modelOpen = false;
        var modelWantsOpen = false;
        var modelAttached = true;
        var modelEnabled = true;
        var modelVisible = true;
        var modelAncestorEnabled = true;
        var modelAncestorVisible = true;
        var modelGeneration = 1;
        var modelResultSessionIdentity = 1;
        var modelAttachmentIdentity = 1;
        var modelCloseTransition = 0;
        var modelAcceptanceIdentity = 0;
        int? modelPendingAcceptanceIdentity = null;
        int? modelAcceptanceResultSessionIdentity = null;
        int? modelAcceptanceAttachmentIdentity = null;
        object? modelPendingAcceptedItem = null;
        string? modelPendingAcceptedText = null;
        var modelPendingAcceptedIndex = -1;
        var modelPendingAcceptanceCause = ActivationCause.Programmatic;
        var modelDisposed = false;
        int? modelSnapshotGeneration = 1;
        int? currentRequest = null;
        var modelSelectedIndex = -1;
        int? openingSnapshotGeneration = null;
        var openingSelectedIndex = -1;
        var modelAcceptances = new List<(object? Item, string Text, int Index, ActivationCause Cause)>();
        var minimumPrefixLength = 1;

        bool IsAvailable() =>
            modelAttached && !modelDisposed && modelEnabled && modelVisible &&
            modelAncestorEnabled && modelAncestorVisible;

        bool IsOperationEligible(int operation) => operation switch
        {
            0 => modelAttached,
            1 => completions.Keys.Any(id => !settled.Contains(id)),
            2 => modelAttached && modelOpen,
            3 => modelAttached && !modelOpen && !modelResolving &&
                 modelSnapshotGeneration == modelGeneration &&
                 modelSuggestions.Length > 0 && IsAvailable(),
            4 => modelOpen && !modelResolving &&
                 modelSnapshotGeneration == modelGeneration && IsAvailable(),
            5 or 6 => modelOpen && !modelResolving && modelSelectedIndex >= 0 &&
                      modelSnapshotGeneration == modelGeneration && IsAvailable(),
            7 => modelAttached && modelOpen && modelEnabled && modelVisible,
            8 => modelAttached && (!modelEnabled || !modelVisible),
            9 => true,
            10 => modelAttached,
            11 => true,
            _ => false
        };

        void SupersedeModelAcceptance()
        {
            modelPendingAcceptanceIdentity = null;
            modelAcceptanceResultSessionIdentity = null;
            modelAcceptanceAttachmentIdentity = null;
            modelPendingAcceptedItem = null;
            modelPendingAcceptedText = null;
            modelPendingAcceptedIndex = -1;
            modelPendingAcceptanceCause = ActivationCause.Programmatic;
        }

        void BeginModelAcceptance(int index, ActivationCause cause)
        {
            modelAcceptanceIdentity++;
            modelPendingAcceptanceIdentity = modelAcceptanceIdentity;
            modelAcceptanceResultSessionIdentity = modelResultSessionIdentity;
            modelAcceptanceAttachmentIdentity = modelAttachmentIdentity;
            modelPendingAcceptedItem = modelSuggestions[index];
            modelPendingAcceptedText = (string) modelSuggestions[index]!;
            modelPendingAcceptedIndex = index;
            modelPendingAcceptanceCause = cause;
        }

        void ReplaceModelResultSession(bool preservePendingAcceptance)
        {
            modelResultSessionIdentity++;

            if (preservePendingAcceptance && modelPendingAcceptanceIdentity is not null)
            {
                modelAcceptanceResultSessionIdentity = modelResultSessionIdentity;
            }
            else
            {
                SupersedeModelAcceptance();
            }
        }

        void PublishModelAcceptanceIfEligible()
        {
            if (modelPendingAcceptanceIdentity is not null &&
                modelAcceptanceResultSessionIdentity == modelResultSessionIdentity &&
                modelAcceptanceAttachmentIdentity == modelAttachmentIdentity &&
                IsAvailable() &&
                !modelOpen)
            {
                modelAcceptances.Add((
                    modelPendingAcceptedItem,
                    modelPendingAcceptedText.ShouldNotBeNull(),
                    modelPendingAcceptedIndex,
                    modelPendingAcceptanceCause));
            }

            SupersedeModelAcceptance();
        }

        void OpenModel()
        {
            if (modelOpen || !IsAvailable() || modelSuggestions.Length == 0)
            {
                return;
            }

            modelOpen = true;
            openingSnapshotGeneration = modelSnapshotGeneration;
            openingSelectedIndex = modelSelectedIndex;
            modelSelectedIndex = 0;
        }

        void CloseModel(bool accepted)
        {
            if (!modelOpen)
            {
                modelWantsOpen = false;
                return;
            }

            modelOpen = false;
            modelWantsOpen = false;
            modelCloseTransition++;

            if (!accepted && openingSnapshotGeneration == modelSnapshotGeneration)
            {
                modelSelectedIndex = openingSelectedIndex;
            }

            openingSnapshotGeneration = null;
            openingSelectedIndex = -1;
        }

        void CaptureCurrentObservation()
        {
            if (currentRequest is { } previousRequest && !settled.Contains(previousRequest))
            {
                cancellationTokens[previousRequest].IsCancellationRequested.ShouldBeTrue();
            }

            currentRequest = issued;
            observations[issued] = input.LastResolutionObservation.ShouldNotBeNull();
        }

        void ApplySynchronousModelResolution(bool preservePendingAcceptance)
        {
            modelGeneration++;
            ReplaceModelResultSession(preservePendingAcceptance);
            modelSuggestions = ResultsFor(modelText);
            modelSnapshotGeneration = modelGeneration;
            modelResolving = false;
            currentRequest = null;

            if (modelWantsOpen)
            {
                if (modelOpen)
                {
                    modelSelectedIndex = 0;
                }
                else
                {
                    OpenModel();
                }
            }
        }

        void AssertModel(int step)
        {
            input.Text.ShouldBe(modelText, $"seed {seed}, step {step}");
            input.Suggestions.ShouldBe(modelSuggestions, $"seed {seed}, step {step}");
            input.IsResolving.ShouldBe(modelResolving, $"seed {seed}, step {step}");
            input.IsOpen.ShouldBe(modelOpen, $"seed {seed}, step {step}");
            actualAcceptances.Count.ShouldBe(modelAcceptances.Count, $"seed {seed}, step {step}");
            (input.Dispatcher is not null).ShouldBe(modelAttached, $"seed {seed}, step {step}");

            for (var index = 0; index < modelAcceptances.Count; index++)
            {
                actualAcceptances[index].Item.ShouldBe(
                    modelAcceptances[index].Item,
                    $"seed {seed}, step {step}, acceptance {index}");
                actualAcceptances[index].Text.ShouldBe(
                    modelAcceptances[index].Text,
                    $"seed {seed}, step {step}, acceptance {index}");
                actualAcceptances[index].Index.ShouldBe(
                    modelAcceptances[index].Index,
                    $"seed {seed}, step {step}, acceptance {index}");
                actualAcceptances[index].Cause.ShouldBe(
                    modelAcceptances[index].Cause,
                    $"seed {seed}, step {step}, acceptance {index}");
            }

            if (modelOpen && modelSnapshotGeneration is not null && !modelResolving && IsAvailable())
            {
                list.SelectedIndex.ShouldBe(modelSelectedIndex, $"seed {seed}, step {step}");
                list.ActiveIndex.ShouldBe(modelSelectedIndex, $"seed {seed}, step {step}");
            }
        }

        try
        {
            for (var step = 0; step < 96; step++)
            {
                var eligibleOperations = Enumerable.Range(0, 12).Where(IsOperationEligible).ToArray();
                int? coverageOperation =
                    !transcript.Any(entry => entry.Contains(":edit:", StringComparison.Ordinal)) &&
                    eligibleOperations.Contains(0) ? 0 :
                    !transcript.Any(entry => entry.Contains(":complete:", StringComparison.Ordinal)) &&
                    eligibleOperations.Contains(1) ? 1 :
                    !transcript.Any(entry => entry.Contains(":selector-failure", StringComparison.Ordinal)) &&
                    eligibleOperations.Contains(6) ? 6 :
                    !transcript.Any(entry => entry.Contains(":accept:", StringComparison.Ordinal)) &&
                    eligibleOperations.Contains(5) ? 5 :
                    !transcript.Any(entry => entry.EndsWith(":detach", StringComparison.Ordinal)) &&
                    modelAttached ? 9 :
                    !transcript.Any(entry => entry.EndsWith(":attach", StringComparison.Ordinal)) &&
                    !modelAttached ? 9 :
                    null;
                var operation = coverageOperation ?? eligibleOperations[random.Next(eligibleOperations.Length)];

                switch (operation)
                {
                    case 0 when modelAttached:
                        {
                            var nextText = $"q{step}";
                            transcript.Add($"{step}:edit:{nextText}");
                            await surface.UpdateAsync(() => input.Text = nextText, $"transcript edit {step}");
                            modelText = nextText;
                            modelGeneration++;
                            ReplaceModelResultSession(preservePendingAcceptance: false);
                            modelWantsOpen = true;

                            if (nextText.Length < minimumPrefixLength)
                            {
                                if (currentRequest is { } previousRequest && !settled.Contains(previousRequest))
                                {
                                    cancellationTokens[previousRequest].IsCancellationRequested.ShouldBeTrue();
                                }

                                currentRequest = null;
                                modelSnapshotGeneration = modelGeneration;
                                modelSuggestions = [];
                                modelSelectedIndex = -1;
                                modelResolving = false;

                                if (modelOpen)
                                {
                                    CloseModel(accepted: false);
                                }
                            }
                            else
                            {
                                modelSnapshotGeneration = null;
                                modelResolving = true;
                                CaptureCurrentObservation();
                            }

                            break;
                        }

                    case 1 when completions.Keys.Any(id => !settled.Contains(id)):
                        {
                            var candidates = completions.Keys.Where(id => !settled.Contains(id)).ToArray();
                            var id = candidates[random.Next(candidates.Length)];
                            var results = ResultsFor(queries[id]);
                            transcript.Add($"{step}:complete:{id}:{queries[id]}");
                            completions[id].SetResult(results);
                            _ = settled.Add(id);
                            await observations[id].WaitAsync(TestContext.Current.CancellationToken);
                            await surface.UpdateAsync(static () => { }, $"render transcript completion {step}");

                            if (currentRequest == id)
                            {
                                modelSuggestions = results;
                                modelResolving = false;
                                modelSnapshotGeneration = modelGeneration;
                                currentRequest = null;
                                modelSelectedIndex = -1;

                                if (modelWantsOpen && modelSuggestions.Length > 0)
                                {
                                    if (modelOpen)
                                    {
                                        modelSelectedIndex = 0;
                                    }
                                    else
                                    {
                                        OpenModel();
                                    }
                                }
                                else if (modelOpen)
                                {
                                    CloseModel(accepted: false);
                                }
                            }

                            break;
                        }

                    case 2 when modelAttached && modelOpen:
                        transcript.Add($"{step}:close");
                        await surface.UpdateAsync(input.Close, $"transcript close {step}");
                        CloseModel(accepted: false);
                        break;

                    case 3 when modelAttached && !modelOpen && !modelResolving &&
                                     modelSnapshotGeneration == modelGeneration &&
                                     modelSuggestions.Length > 0 && IsAvailable():
                        transcript.Add($"{step}:open");
                        modelWantsOpen = true;
                        await surface.UpdateAsync(() => _ = input.Open(), $"transcript open {step}");
                        OpenModel();
                        break;

                    case 4 when modelOpen && !modelResolving &&
                                     modelSnapshotGeneration == modelGeneration && IsAvailable():
                        {
                            var down = random.Next(2) == 0;
                            transcript.Add($"{step}:navigate:{(down ? "down" : "up")}");
                            await surface.UpdateAsync(() => _ = input.Open(), $"focus transcript owner {step}");

                            if (down)
                            {
                                await surface.Keyboard.PressAsync(Code.Down);
                                modelSelectedIndex = Math.Min(modelSuggestions.Length - 1, modelSelectedIndex + 1);
                            }
                            else
                            {
                                await surface.Keyboard.PressAsync(Code.Up);
                                modelSelectedIndex = Math.Max(0, modelSelectedIndex - 1);
                            }

                            break;
                        }

                    case 5 when modelOpen && !modelResolving && modelSelectedIndex >= 0 &&
                                     modelSnapshotGeneration == modelGeneration && IsAvailable():
                        {
                            var pointer = random.Next(2) == 0;
                            var acceptedIndex = pointer ? 0 : modelSelectedIndex;
                            var acceptedText = (string) modelSuggestions[acceptedIndex]!;
                            transcript.Add($"{step}:accept:{(pointer ? "pointer" : "enter")}:{acceptedIndex}");
                            BeginModelAcceptance(
                                acceptedIndex,
                                pointer ? ActivationCause.Pointer : ActivationCause.Keyboard);

                            if (pointer)
                            {
                                await surface.ResizeAsync(new Size(24, 10));
                                await surface.Pointer.ClickAsync(list, new Point(1, acceptedIndex));
                            }
                            else
                            {
                                await surface.UpdateAsync(() => _ = input.Open(), $"focus transcript acceptance {step}");
                                await surface.Keyboard.PressAsync(Code.Enter);
                            }

                            modelText = acceptedText;
                            modelSelectedIndex = acceptedIndex;
                            modelGeneration++;
                            ReplaceModelResultSession(preservePendingAcceptance: true);
                            modelSnapshotGeneration = null;
                            modelResolving = true;
                            CloseModel(accepted: true);
                            CaptureCurrentObservation();
                            PublishModelAcceptanceIfEligible();
                            break;
                        }

                    case 6 when modelOpen && !modelResolving && modelSelectedIndex >= 0 &&
                                     modelSnapshotGeneration == modelGeneration && IsAvailable():
                        {
                            var expected = new InvalidOperationException($"selector {step}");
                            var failingIndex = (modelSelectedIndex + 1) % modelSuggestions.Length;
                            var selectedBefore = list.SelectedIndex;
                            var activeBefore = list.ActiveIndex;
                            var closeTransitionBefore = modelCloseTransition;
                            transcript.Add($"{step}:selector-failure:pointer:{failingIndex}");
                            await surface.UpdateAsync(
                                () => input.TextSelector = item => ReferenceEquals(item, modelSuggestions[failingIndex])
                                    ? throw expected
                                    : (string) item!,
                                $"arm transcript selector failure {step}");
                            var point = new Point(
                                list.Bounds.X + 1,
                                list.Bounds.Y + failingIndex - list.VerticalOffset);
                            var thrown = await surface.Application.Dispatcher.InvokeAsync(
                                () =>
                                {
                                    _ = surface.Application.Capture.Dispatch(new Pointer(
                                        point,
                                        pixels: null,
                                        Buttons.Primary,
                                        PointerAction.Press,
                                        wheelX: 0,
                                        wheelY: 0,
                                        Modifiers.None,
                                        isMotion: false,
                                        isCellPositionInferred: false));
                                    return Should.Throw<InvalidOperationException>(() =>
                                        _ = surface.Application.Capture.Dispatch(new Pointer(
                                            point,
                                            pixels: null,
                                            Buttons.Primary,
                                            PointerAction.Release,
                                            wheelX: 0,
                                            wheelY: 0,
                                            Modifiers.None,
                                            isMotion: false,
                                            isCellPositionInferred: false)));
                                },
                                TestContext.Current.CancellationToken);
                            thrown.ShouldBeSameAs(expected);
                            list.SelectedIndex.ShouldBe(selectedBefore);
                            list.ActiveIndex.ShouldBe(activeBefore);
                            modelCloseTransition.ShouldBe(closeTransitionBefore);
                            modelPendingAcceptanceIdentity.ShouldBeNull();
                            await surface.UpdateAsync(
                                () => input.TextSelector = null,
                                $"clear transcript selector failure {step}");
                            break;
                        }

                    case 7 when modelAttached && modelOpen && modelEnabled && modelVisible:
                        {
                            var hide = random.Next(2) == 0;
                            transcript.Add($"{step}:owner-unavailable:{(hide ? "hidden" : "disabled")}");
                            await surface.UpdateAsync(
                                () =>
                                {
                                    if (hide)
                                    {
                                        input.Visibility = Visibility.Hidden;
                                    }
                                    else
                                    {
                                        input.IsEnabled = false;
                                    }
                                },
                                $"transcript owner unavailable {step}");
                            CloseModel(accepted: false);
                            modelVisible = !hide;
                            modelEnabled = hide;
                            break;
                        }

                    case 8 when modelAttached && (!modelEnabled || !modelVisible):
                        transcript.Add($"{step}:owner-available");
                        await surface.UpdateAsync(
                            () =>
                            {
                                input.IsEnabled = true;
                                input.Visibility = Visibility.Visible;
                            },
                            $"transcript owner available {step}");
                        modelEnabled = true;
                        modelVisible = true;
                        break;

                    case 9 when modelAttached:
                        var detachedRequest = currentRequest;
                        transcript.Add($"{step}:detach");
                        await surface.UpdateAsync(() => root.Children.Remove(input), $"transcript detach {step}");

                        if (detachedRequest is { } request && !settled.Contains(request))
                        {
                            cancellationTokens[request].IsCancellationRequested.ShouldBeTrue();
                        }

                        modelAttached = false;
                        modelAttachmentIdentity++;
                        modelGeneration++;
                        ReplaceModelResultSession(preservePendingAcceptance: false);
                        modelSnapshotGeneration = null;
                        modelResolving = false;
                        currentRequest = null;
                        CloseModel(accepted: false);
                        break;

                    case 9 when !modelAttached:
                        transcript.Add($"{step}:attach");
                        await surface.UpdateAsync(() => root.Children.Add(input), $"transcript attach {step}");
                        modelAttached = true;
                        modelAttachmentIdentity++;
                        break;

                    case 10 when modelAttached:
                        {
                            minimumPrefixLength = minimumPrefixLength == 1 ? 100 : 1;
                            transcript.Add($"{step}:threshold:{minimumPrefixLength}");
                            await surface.UpdateAsync(
                                () => input.MinimumPrefixLength = minimumPrefixLength,
                                $"transcript threshold {step}");
                            modelGeneration++;
                            ReplaceModelResultSession(preservePendingAcceptance: false);

                            if (minimumPrefixLength > modelText.Length)
                            {
                                if (currentRequest is { } previousRequest && !settled.Contains(previousRequest))
                                {
                                    cancellationTokens[previousRequest].IsCancellationRequested.ShouldBeTrue();
                                }

                                modelSnapshotGeneration = modelGeneration;
                                modelSuggestions = [];
                                modelSelectedIndex = -1;
                                modelResolving = false;
                                currentRequest = null;

                                if (modelOpen)
                                {
                                    CloseModel(accepted: false);
                                }
                            }
                            else
                            {
                                modelSnapshotGeneration = null;
                                modelResolving = true;
                                CaptureCurrentObservation();
                            }

                            break;
                        }

                    case 11:
                    default:
                        {
                            var width = random.Next(1, 25);
                            var height = random.Next(1, 12);
                            transcript.Add($"{step}:resize:{width}x{height}");
                            await surface.ResizeAsync(new Size(width, height));
                            break;
                        }
                }

                AssertModel(step);
            }

            foreach (var id in completions.Keys.Where(id => !settled.Contains(id)).ToArray())
            {
                transcript.Add($"drain:complete:{id}:{queries[id]}");
                var results = ResultsFor(queries[id]);
                completions[id].SetResult(results);
                _ = settled.Add(id);
                await observations[id].WaitAsync(TestContext.Current.CancellationToken);

                if (currentRequest == id)
                {
                    modelSuggestions = results;
                    modelResolving = false;
                    modelSnapshotGeneration = modelGeneration;
                    currentRequest = null;
                    modelSelectedIndex = -1;

                    if (modelWantsOpen && modelSuggestions.Length > 0)
                    {
                        if (modelOpen)
                        {
                            modelSelectedIndex = 0;
                        }
                        else
                        {
                            OpenModel();
                        }
                    }
                }
            }

            await surface.UpdateAsync(static () => { }, "render drained randomized suggestion completions");
            AssertModel(step: 96);

            if (!modelAttached)
            {
                transcript.Add("guarantee:attach");
                await surface.UpdateAsync(() => root.Children.Add(input), "attach for guaranteed oracle cases");
                modelAttached = true;
                modelAttachmentIdentity++;
            }

            await surface.UpdateAsync(
                () =>
                {
                    input.IsEnabled = true;
                    input.Visibility = Visibility.Visible;
                    root.IsEnabled = true;
                    root.Visibility = Visibility.Visible;

                    if (input.IsOpen)
                    {
                        input.Close();
                    }
                },
                "normalize randomized suggestion availability");
            modelEnabled = true;
            modelVisible = true;
            modelAncestorEnabled = true;
            modelAncestorVisible = true;

            if (modelOpen)
            {
                CloseModel(accepted: false);
            }

            if (minimumPrefixLength != 1)
            {
                minimumPrefixLength = 1;
                await surface.UpdateAsync(
                    () => input.MinimumPrefixLength = minimumPrefixLength,
                    "restore randomized suggestion threshold");
                modelGeneration++;
                ReplaceModelResultSession(preservePendingAcceptance: false);

                if (modelText.Length >= minimumPrefixLength)
                {
                    modelSnapshotGeneration = null;
                    modelResolving = true;
                    CaptureCurrentObservation();
                }
                else
                {
                    modelSnapshotGeneration = modelGeneration;
                    modelSuggestions = [];
                    modelSelectedIndex = -1;
                    modelResolving = false;
                    currentRequest = null;
                }
            }

            var staleQuery = $"stale-{random.Next(1000, 9999)}";
            transcript.Add($"guarantee:edit:{staleQuery}");
            await surface.UpdateAsync(() => input.Text = staleQuery, "start guaranteed stale suggestion request");
            modelText = staleQuery;
            modelGeneration++;
            ReplaceModelResultSession(preservePendingAcceptance: false);
            modelWantsOpen = true;
            modelSnapshotGeneration = null;
            modelResolving = true;
            CaptureCurrentObservation();
            var staleRequest = currentRequest.ShouldNotBeNull();

            var currentQuery = $"current-{random.Next(1000, 9999)}";
            transcript.Add($"guarantee:edit:{currentQuery}");
            await surface.UpdateAsync(() => input.Text = currentQuery, "supersede guaranteed stale suggestion request");
            modelText = currentQuery;
            modelGeneration++;
            ReplaceModelResultSession(preservePendingAcceptance: false);
            modelWantsOpen = true;
            modelSnapshotGeneration = null;
            modelResolving = true;
            CaptureCurrentObservation();
            var currentGuaranteedRequest = currentRequest.ShouldNotBeNull();
            cancellationTokens[staleRequest].IsCancellationRequested.ShouldBeTrue();

            transcript.Add($"guarantee:stale-completion:{staleRequest}");
            completions[staleRequest].SetResult(ResultsFor(staleQuery));
            _ = settled.Add(staleRequest);
            await observations[staleRequest].WaitAsync(TestContext.Current.CancellationToken);
            await surface.UpdateAsync(static () => { }, "observe guaranteed stale suggestion completion");
            AssertModel(step: 97);

            var currentGuaranteedResults = ResultsFor(currentQuery);
            transcript.Add($"guarantee:current-completion:{currentGuaranteedRequest}");
            completions[currentGuaranteedRequest].SetResult(currentGuaranteedResults);
            _ = settled.Add(currentGuaranteedRequest);
            await observations[currentGuaranteedRequest].WaitAsync(TestContext.Current.CancellationToken);
            await surface.UpdateAsync(static () => { }, "observe guaranteed current suggestion completion");
            modelSuggestions = currentGuaranteedResults;
            modelResolving = false;
            modelSnapshotGeneration = modelGeneration;
            currentRequest = null;
            modelSelectedIndex = -1;
            OpenModel();
            AssertModel(step: 98);

            transcript.Add("guarantee:owner-disabled");
            await surface.UpdateAsync(() => input.IsEnabled = false, "disable randomized suggestion owner");
            modelEnabled = false;
            CloseModel(accepted: false);
            AssertModel(step: 99);
            await surface.UpdateAsync(() => input.IsEnabled = true, "enable randomized suggestion owner");
            modelEnabled = true;

            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before owner visibility transition");
            OpenModel();
            transcript.Add("guarantee:owner-hidden");
            await surface.UpdateAsync(
                () => input.Visibility = Visibility.Hidden,
                "hide randomized suggestion owner");
            modelVisible = false;
            CloseModel(accepted: false);
            AssertModel(step: 100);
            await surface.UpdateAsync(
                () => input.Visibility = Visibility.Visible,
                "show randomized suggestion owner");
            modelVisible = true;

            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before ancestor availability transitions");
            OpenModel();
            transcript.Add("guarantee:ancestor-disabled");
            await surface.UpdateAsync(() => root.IsEnabled = false, "disable randomized suggestion ancestor");
            modelAncestorEnabled = false;
            input.IsOpen.ShouldBeTrue();
            surface.Application.Modality.Active.ShouldBeNull();
            AssertModel(step: 101);
            await surface.UpdateAsync(() => root.IsEnabled = true, "enable randomized suggestion ancestor");
            modelAncestorEnabled = true;
            _ = surface.Application.Modality.Active.ShouldNotBeNull();

            transcript.Add("guarantee:ancestor-hidden");
            await surface.UpdateAsync(
                () => root.Visibility = Visibility.Hidden,
                "hide randomized suggestion ancestor");
            modelAncestorVisible = false;
            input.IsOpen.ShouldBeTrue();
            surface.Application.Modality.Active.ShouldBeNull();
            AssertModel(step: 102);
            await surface.UpdateAsync(
                () => root.Visibility = Visibility.Visible,
                "show randomized suggestion ancestor");
            modelAncestorVisible = true;
            _ = surface.Application.Modality.Active.ShouldNotBeNull();

            transcript.Add("guarantee:resize:7x4");
            await surface.ResizeAsync(new Size(7, 4));
            AssertModel(step: 103);

            transcript.Add("guarantee:dismiss:escape");
            await surface.Keyboard.PressAsync(Code.Escape);
            CloseModel(accepted: false);
            AssertModel(step: 104);

            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before randomized Tab dismissal");
            OpenModel();
            transcript.Add("guarantee:dismiss:tab");
            await surface.Keyboard.PressAsync(Code.Tab);
            CloseModel(accepted: false);
            AssertModel(step: 105);

            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before randomized light dismissal");
            OpenModel();
            await surface.ResizeAsync(new Size(24, 10));
            transcript.Add("guarantee:dismiss:light");
            await surface.Pointer.ClickAsync(root, new Point(23, 9));
            CloseModel(accepted: false);
            AssertModel(step: 106);

            resolveSynchronously = true;
            transcript.Add("guarantee:resolver:synchronous");
            await surface.UpdateAsync(input.Refresh, "switch randomized oracle to synchronous results");
            modelWantsOpen = true;
            ApplySynchronousModelResolution(preservePendingAcceptance: false);
            AssertModel(step: 107);

            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before guaranteed successful acceptance");
            OpenModel();
            var successfulIndex = random.Next(modelSuggestions.Length);

            for (var index = 0; index < successfulIndex; index++)
            {
                await surface.Keyboard.PressAsync(Code.Down);
                modelSelectedIndex++;
            }

            var successfulCause = random.Next(2) == 0
                ? ActivationCause.Keyboard
                : ActivationCause.Pointer;
            var successfulText = (string) modelSuggestions[successfulIndex]!;
            transcript.Add($"guarantee:accept:{successfulCause}:{successfulIndex}:{successfulText}");
            BeginModelAcceptance(successfulIndex, successfulCause);

            if (successfulCause == ActivationCause.Pointer)
            {
                await surface.Pointer.ClickAsync(
                    list,
                    new Point(1, successfulIndex - list.VerticalOffset));
            }
            else
            {
                await surface.Keyboard.PressAsync(Code.Enter);
            }

            modelText = successfulText;
            ApplySynchronousModelResolution(preservePendingAcceptance: true);
            CloseModel(accepted: true);
            PublishModelAcceptanceIfEligible();
            AssertModel(step: 108);

            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before replaced acceptance");
            OpenModel();
            var replacedText = (string) modelSuggestions[0]!;
            var acceptancesBeforeReplacement = modelAcceptances.Count;
            BeginModelAcceptance(index: 0, ActivationCause.Keyboard);
            void ReplaceResultsOnClose(object? _, System.ComponentModel.PropertyChangedEventArgs eventArgs)
            {
                if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen) && !input.IsOpen)
                {
                    input.Resolver = null;
                }
            }

            input.PropertyChanged += ReplaceResultsOnClose;
            transcript.Add($"guarantee:accept:result-replaced:{replacedText}");

            try
            {
                await surface.Keyboard.PressAsync(Code.Enter);
            }
            finally
            {
                input.PropertyChanged -= ReplaceResultsOnClose;
            }

            modelText = replacedText;
            ApplySynchronousModelResolution(preservePendingAcceptance: true);
            CloseModel(accepted: true);
            modelGeneration++;
            ReplaceModelResultSession(preservePendingAcceptance: false);
            modelSuggestions = [];
            modelSnapshotGeneration = modelGeneration;
            modelSelectedIndex = -1;
            modelResolving = false;
            currentRequest = null;
            PublishModelAcceptanceIfEligible();
            modelAcceptances.Count.ShouldBe(acceptancesBeforeReplacement);
            AssertModel(step: 109);

            await surface.UpdateAsync(
                () => input.Resolver = ResolveSuggestion,
                "restore resolver after result replacement");
            ApplySynchronousModelResolution(preservePendingAcceptance: false);
            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before competing acceptance");
            OpenModel();
            var competingOuterText = (string) modelSuggestions[0]!;
            BeginModelAcceptance(index: 0, ActivationCause.Keyboard);
            var competeOnClose = true;
            void CompeteDuringClose(object? _, System.ComponentModel.PropertyChangedEventArgs eventArgs)
            {
                if (!competeOnClose ||
                    eventArgs.PropertyName != nameof(SuggestionInput.IsOpen) ||
                    input.IsOpen)
                {
                    return;
                }

                competeOnClose = false;
                _ = input.Open();
                _ = Router.Route(
                    editor,
                    Events.Key,
                    new KeyEventArgs(new Stroke(
                        Code.Enter,
                        character: null,
                        nativeCode: 0,
                        Modifiers.None,
                        KeyAction.Press)));
            }

            input.PropertyChanged += CompeteDuringClose;
            transcript.Add($"guarantee:accept:competing:{competingOuterText}");

            try
            {
                await surface.Keyboard.PressAsync(Code.Enter);
            }
            finally
            {
                input.PropertyChanged -= CompeteDuringClose;
            }

            modelText = competingOuterText;
            ApplySynchronousModelResolution(preservePendingAcceptance: true);
            CloseModel(accepted: true);
            modelWantsOpen = true;
            OpenModel();
            BeginModelAcceptance(index: 0, ActivationCause.Keyboard);
            var competingWinnerText = (string) modelSuggestions[0]!;
            modelText = competingWinnerText;
            ApplySynchronousModelResolution(preservePendingAcceptance: true);
            CloseModel(accepted: true);
            PublishModelAcceptanceIfEligible();
            PublishModelAcceptanceIfEligible();
            AssertModel(step: 110);

            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before attachment supersession");
            OpenModel();
            var detachedAcceptedText = (string) modelSuggestions[0]!;
            var acceptancesBeforeDetach = modelAcceptances.Count;
            BeginModelAcceptance(index: 0, ActivationCause.Keyboard);
            void DetachDuringClose(object? _, System.ComponentModel.PropertyChangedEventArgs eventArgs)
            {
                if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen) && !input.IsOpen)
                {
                    _ = root.Children.Remove(input);
                }
            }

            input.PropertyChanged += DetachDuringClose;
            transcript.Add($"guarantee:accept:detached:{detachedAcceptedText}");

            try
            {
                await surface.Keyboard.PressAsync(Code.Enter);
            }
            finally
            {
                input.PropertyChanged -= DetachDuringClose;
            }

            modelText = detachedAcceptedText;
            ApplySynchronousModelResolution(preservePendingAcceptance: true);
            CloseModel(accepted: true);
            modelAttached = false;
            modelAttachmentIdentity++;
            modelGeneration++;
            ReplaceModelResultSession(preservePendingAcceptance: false);
            modelSnapshotGeneration = null;
            modelResolving = false;
            currentRequest = null;
            PublishModelAcceptanceIfEligible();
            modelAcceptances.Count.ShouldBe(acceptancesBeforeDetach);
            AssertModel(step: 111);

            await surface.UpdateAsync(() => root.Children.Add(input), "reattach after acceptance supersession");
            modelAttached = true;
            modelAttachmentIdentity++;
            AssertModel(step: 112);

            modelWantsOpen = true;
            await surface.UpdateAsync(() => _ = input.Open(), "open before disposal supersession");
            OpenModel();
            var disposedAcceptedText = (string) modelSuggestions[0]!;
            var acceptancesBeforeDisposal = modelAcceptances.Count;
            BeginModelAcceptance(index: 0, ActivationCause.Keyboard);
            void DisposeDuringClose(object? _, System.ComponentModel.PropertyChangedEventArgs eventArgs)
            {
                if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen) && !input.IsOpen)
                {
                    _ = root.Children.Remove(input);
                    input.Dispose();
                }
            }

            input.PropertyChanged += DisposeDuringClose;
            transcript.Add($"guarantee:accept:disposed:{disposedAcceptedText}");

            try
            {
                await surface.Keyboard.PressAsync(Code.Enter);
            }
            finally
            {
                input.PropertyChanged -= DisposeDuringClose;
            }

            modelText = disposedAcceptedText;
            ApplySynchronousModelResolution(preservePendingAcceptance: true);
            CloseModel(accepted: true);
            modelAttached = false;
            modelDisposed = true;
            modelAttachmentIdentity++;
            modelGeneration++;
            ReplaceModelResultSession(preservePendingAcceptance: false);
            modelSnapshotGeneration = null;
            modelResolving = false;
            currentRequest = null;
            PublishModelAcceptanceIfEligible();
            actualAcceptances.Count.ShouldBe(acceptancesBeforeDisposal);
            input.IsDisposed.ShouldBeTrue();
            modelPendingAcceptanceIdentity.ShouldBeNull();
            modelCloseTransition.ShouldBeGreaterThan(0);

            transcript.Any(entry => entry.Contains(":complete:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":accept:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":selector-failure", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.EndsWith(":detach", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.EndsWith(":attach", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":stale-completion:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":accept:result-replaced:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":accept:competing:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":accept:detached:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":accept:disposed:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":ancestor-disabled", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":ancestor-hidden", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":resize:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Count(entry => entry.Contains(":dismiss:", StringComparison.Ordinal)).ShouldBe(3);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"SuggestionInput randomized transcript failed for seed {seed}.\n{string.Join(Environment.NewLine, transcript)}",
                exception);
        }
        finally
        {
            if (!modelAttached && !modelDisposed)
            {
                await surface.UpdateAsync(() => root.Children.Add(input), "reattach randomized suggestion input for disposal");
            }
        }
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
