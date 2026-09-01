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
        var failures = 0;
        var calls = 0;
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
        await using var dispatcher = Dispatcher.Start(capacity: 1);
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
        Exception? observationFailure = null;

        try
        {
            await observation.WaitAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception exception)
        {
            observationFailure = exception;
        }
        finally
        {
            release.Set();
        }

        await fillerDrained.Task.WaitAsync(TestContext.Current.CancellationToken);
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
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
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
        await using var dispatcher = Dispatcher.Start();
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
        try
        {
            var firstLifecycleBoundary = await Task.WhenAny(
                lifecycleWaited.Task,
                attachmentCommitted.Task).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before release
            firstLifecycleBoundary.ShouldBeSameAs(lifecycleWaited.Task);
            input.Dispatcher.ShouldBeNull();
            input.Suggestions.ShouldBeEmpty();
            input.IsResolving.ShouldBeTrue();
        }
        finally
        {
            releasePublication.Set();
        }

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
        await using var dispatcher = Dispatcher.Start();
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

        try
        {
            var firstSettlement = await Task.WhenAny(
                lifecycleWaited.Task,
                resolve).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before lifecycle release
            firstSettlement.ShouldBeSameAs(lifecycleWaited.Task);
            input.Suggestions.ShouldBeEmpty();
        }
        finally
        {
            releaseAttachment.Set();
        }

        _ = await resolve.WaitAsync(TestContext.Current.CancellationToken);
        await attach.WaitAsync(TestContext.Current.CancellationToken);

        // Assert after lifecycle release
        input.Dispatcher.ShouldBeSameAs(dispatcher);
        input.Suggestions.ShouldBeEmpty();
        input.IsResolving.ShouldBeFalse();
        await dispatcher.InvokeAsync(input.Dispose, TestContext.Current.CancellationToken);
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
                releaseMutation.Wait(TestContext.Current.CancellationToken);
            };
        }

        input.OwnedControls.PublicationWaitStarted = () => lifecycleWaited.TrySetResult();
        input.OwnedControls.DescendantDiscoveryStarted = () => discoveryStarted.TrySetResult();
        await using var dispatcher = Dispatcher.Start();
        var complete = Task.Run(
            () => completion.SetResult(["one", "two"]),
            TestContext.Current.CancellationToken);
        await mutationPaused.Task.WaitAsync(TestContext.Current.CancellationToken);
        var attach = dispatcher.InvokeAsync(
            () => input.Attach(dispatcher),
            TestContext.Current.CancellationToken).AsTask();

        // Act
        try
        {
            var firstBoundary = await Task.WhenAny(
                lifecycleWaited.Task,
                discoveryStarted.Task).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before mutation release
            firstBoundary.ShouldBeSameAs(lifecycleWaited.Task);
            input.Dispatcher.ShouldBeNull();
        }
        finally
        {
            releaseMutation.Set();
        }

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
        try
        {
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
        }
        finally
        {
            releasePublication.Set();
        }

        await clear.WaitAsync(TestContext.Current.CancellationToken);
        await disposal.ShouldNotBeNull().WaitAsync(TestContext.Current.CancellationToken);

        // Assert after release
        input.IsDisposed.ShouldBeTrue();
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

        try
        {
            var firstLifecycleBoundary = await Task.WhenAny(
                lifecycleWaited.Task,
                disposal).WaitAsync(TestContext.Current.CancellationToken);

            // Assert before release
            firstLifecycleBoundary.ShouldBeSameAs(lifecycleWaited.Task);
            input.IsDisposed.ShouldBeFalse();
            input.IsDisposing.ShouldBeFalse();
        }
        finally
        {
            releasePublication.Set();
        }

        var publicationFailure = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await observation.WaitAsync(TestContext.Current.CancellationToken));
        await completeResolver;
        await disposal.WaitAsync(TestContext.Current.CancellationToken);

        // Assert after release
        publicationFailure.ShouldBeSameAs(expected);
        input.IsResolving.ShouldBeFalse();
        input.IsDisposed.ShouldBeTrue();
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
