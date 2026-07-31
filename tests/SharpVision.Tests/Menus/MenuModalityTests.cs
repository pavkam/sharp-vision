// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Menus;

/// <summary>Proves that one top-level menu owns the complete submenu chain's modal plane.</summary>
public sealed class MenuModalityTests
{
    #region Session entry and transitions

    /// <summary>Verifies an armed sibling switch preserves one menu-rooted dismissing scope.</summary>
    [Fact]
    public async Task Pointer_WhenSiblingMainMenuItemIsSelected_ReusesOneModalScopeAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Content = new ControlText("Copy") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        ModalScope? scopeAtPopupExposure = null;
        filePopup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.IsOpen) && filePopup.IsOpen)
            {
                scopeAtPopupExposure = surface.Application.Modality.Active;
                scopeAtPopupExposure.ShouldNotBeNull().Root.ShouldBeSameAs(menu);
            }
        };

        // Act
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(edit);

        // Assert
        scope.Root.ShouldBeSameAs(menu);
        scope.OutsideInteraction.ShouldBe(OutsideInteraction.Dismiss);
        scopeAtPopupExposure.ShouldBeSameAs(scope);
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a command row can temporarily own an armed menu without ending its scope.</summary>
    [Fact]
    public async Task Pointer_WhenArmedSelectionMovesThroughCommand_ReopensInSameModalScopeAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var command = new MenuItem { Content = new ControlText("Exit") };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(command);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act command row and back
        await surface.Pointer.MoveToAsync(command);

        // Assert armed command row
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
        await surface.Pointer.MoveToAsync(file);

        // Assert reopened sibling
        popup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies activating the already-open top item ends both the visual and modal session.</summary>
    [Fact]
    public async Task PerformInvoke_WhenTopSubmenuIsAlreadyOpen_ClosesCompleteSessionAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open top submenu");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(file.PerformInvoke, "toggle top submenu closed");

        // Assert
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies activating an already-open nested item closes only its branch and retains the top scope.</summary>
    [Fact]
    public async Task PerformInvoke_WhenNestedSubmenuIsAlreadyOpen_ClosesBranchAndRetainsSessionAsync()
    {
        // Arrange
        var deepestMenu = new Menu { Orientation = Orientation.Vertical };
        deepestMenu.Items.Add(new MenuItem { Content = new ControlText("Leaf") });
        var nested = new MenuItem { Content = new ControlText("Nested"), Submenu = deepestMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open top submenu for nested toggle");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.UpdateAsync(nested.PerformInvoke, "open nested submenu");

        // Act
        await surface.UpdateAsync(nested.PerformInvoke, "toggle nested submenu closed");

        // Assert
        nestedPopup.IsOpen.ShouldBeFalse();
        firstPopup.IsOpen.ShouldBeTrue();
        scope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies leaving a hover-opened top anchor makes a later click toggle the complete session closed.</summary>
    [Fact]
    public async Task Pointer_WhenHoverOpenedTopAnchorIsLeftAndClickedLater_ClosesCompleteSessionAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var copy = new MenuItem { Content = new ControlText("Copy") };
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(copy);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(edit);
        editPopup.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(copy);

        // Act
        await surface.Pointer.ClickAsync(edit);

        // Assert
        editPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies leaving a hover-opened nested anchor makes a later click close only that branch.</summary>
    [Fact]
    public async Task Pointer_WhenHoverOpenedNestedAnchorIsLeftAndClickedLater_ClosesBranchOnlyAsync()
    {
        // Arrange
        var leaf = new MenuItem { Content = new ControlText("Today") };
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(leaf);
        var recent = new MenuItem { Content = new ControlText("Recent"), Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(recent);
        recentPopup.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(leaf);

        // Act
        await surface.Pointer.ClickAsync(recent);

        // Assert
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeTrue();
        scope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies a consumed move outside the menu plane expires a top anchor's one-shot click.</summary>
    [Fact]
    public async Task Pointer_WhenHoverOpenedTopAnchorIsLeftOutsidePlaneAndClickedLater_ClosesCompleteSessionAsync()
    {
        // Arrange
        var background = new Button
        {
            Content = new ControlText("Background"),
            Width = Length.Cells(12),
            Height = Length.Cells(1),
        };
        Overlay.SetTop(background, Length.Cells(7));
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Content = new ControlText("Copy") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        var root = new Overlay { Children = { menu, background } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(edit);
        editPopup.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(background);
        edit.IsPointerOver.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act
        await surface.Pointer.ClickAsync(edit);

        // Assert
        editPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a consumed move outside the menu plane expires a nested anchor's one-shot click.</summary>
    [Fact]
    public async Task Pointer_WhenHoverOpenedNestedAnchorIsLeftOutsidePlaneAndClickedLater_ClosesBranchOnlyAsync()
    {
        // Arrange
        var background = new Button
        {
            Content = new ControlText("Background"),
            Width = Length.Cells(12),
            Height = Length.Cells(1),
        };
        Overlay.SetTop(background, Length.Cells(8));
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Content = new ControlText("Today") });
        var recent = new MenuItem { Content = new ControlText("Recent"), Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var root = new Overlay { Children = { menu, background } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(recent);
        recentPopup.IsOpen.ShouldBeTrue();
        await surface.Pointer.MoveToAsync(background);
        recent.IsPointerOver.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act
        await surface.Pointer.ClickAsync(recent);

        // Assert
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeTrue();
        scope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
    }

    /// <summary>Verifies an unhandled wheel over an armed menu closes its complete dismissing session.</summary>
    [Fact]
    public async Task Pointer_WhenWheelCannotScrollArmedMenu_ClosesCompleteSessionAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Content = new ControlText("Copy") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(edit);
        editPopup.IsOpen.ShouldBeTrue();
        menu.SelectedIndex.ShouldBe(1);
        var wheelPoint = await surface.ResolvePointAsync(file);
        var wheelReport = Encoding.ASCII.GetBytes(
            FormattableString.Invariant($"\u001b[<64;{wheelPoint.X + 1};{wheelPoint.Y + 1}M"));
        await surface.SendAsync(wheelReport, "wheel outside hover-opened menu anchor");

        // Assert
        edit.IsPointerOver.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    #endregion

    #region Escape and outside dismissal

    /// <summary>Verifies a menu without an armed session leaves Escape for its containing Window.</summary>
    [Fact]
    public async Task Escape_WhenMenuHasNoOpenSession_BubblesToWindowCancelButtonAsync()
    {
        // Arrange
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(new MenuItem { Content = new ControlText("File") });
        var cancel = new Button
        {
            Content = new ControlText("Cancel"),
            IsCancel = true,
        };
        var content = new Stack { Orientation = Orientation.Vertical };
        content.Children.Add(menu);
        content.Children.Add(cancel);
        var window = new Window { Content = content };
        await using var surface = await ComponentSurface.MountAsync(
            window,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var cancellations = 0;
        cancel.Click += (_, _) => cancellations++;
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(menu).ShouldBeTrue(),
            "focus standalone menu in Window");

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        cancellations.ShouldBe(1);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(menu);
    }

    /// <summary>Verifies Escape removes one deepest popup at a time before ending the root session.</summary>
    [Fact]
    public async Task Escape_WhenSubmenuDepthExceedsThree_ClosesOneLevelThenRootSessionAsync()
    {
        // Arrange
        var deepestMenu = new Menu { Orientation = Orientation.Vertical };
        deepestMenu.Items.Add(new MenuItem { Content = new ControlText("Leaf") });
        var deepest = new MenuItem { Content = new ControlText("Third"), Submenu = deepestMenu };
        var secondMenu = new Menu { Orientation = Orientation.Vertical };
        secondMenu.Items.Add(deepest);
        var second = new MenuItem { Content = new ControlText("Second"), Submenu = secondMenu };
        var firstMenu = new Menu { Orientation = Orientation.Vertical };
        firstMenu.Items.Add(second);
        var first = new MenuItem { Content = new ControlText("First"), Submenu = firstMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(first);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(60, 15),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(first).ShouldNotBeNull();
        var secondPopup = OwnedTree.Find<Popup>(second).ShouldNotBeNull();
        var deepestPopup = OwnedTree.Find<Popup>(deepest).ShouldNotBeNull();

        // Act open every retained level
        await surface.Pointer.ClickAsync(first);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.MoveToAsync(second);
        await surface.Pointer.MoveToAsync(deepest);

        // Assert complete plane
        firstPopup.IsOpen.ShouldBeTrue();
        secondPopup.IsOpen.ShouldBeTrue();
        deepestPopup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act and assert one level per Escape
        await surface.Keyboard.PressAsync(Code.Escape);
        deepestPopup.IsOpen.ShouldBeFalse();
        secondPopup.IsOpen.ShouldBeTrue();
        firstPopup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        await surface.Keyboard.PressAsync(Code.Escape);
        secondPopup.IsOpen.ShouldBeFalse();
        firstPopup.IsOpen.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        await surface.Keyboard.PressAsync(Code.Escape);
        firstPopup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        await surface.Keyboard.PressAsync(Code.Escape);
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies outside press and wheel each dismiss without reaching the exposed background.</summary>
    [Fact]
    public async Task OutsideInput_WhenMenuSessionIsOpen_DismissesWithoutBackgroundInteractionAsync()
    {
        // Arrange
        var activations = 0;
        var wheelRoutes = 0;
        var background = new Button
        {
            Content = new ControlText("Background"),
            Width = Length.Cells(12),
            Height = Length.Cells(1),
        };
        background.Click += (_, _) => activations++;
        _ = background.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble && eventArgs.Pointer.Action == PointerAction.Wheel)
            {
                wheelRoutes++;
            }
        });
        Overlay.SetTop(background, Length.Cells(6));
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var root = new Overlay { Children = { menu, background } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(background).ShouldBeTrue(),
            "focus menu background");
        await surface.UpdateAsync(file.PerformInvoke, "open menu session for outside press");
        var pressScope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act outside press
        await surface.Pointer.ClickAsync(background);

        // Assert consumed dismissal
        pressScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(background);
        activations.ShouldBe(0);

        // Act outside wheel in a fresh session
        await surface.UpdateAsync(file.PerformInvoke, "open menu session for outside wheel");
        var wheelScope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Pointer.WheelAsync(background, default, wheelY: 1);

        // Assert consumed dismissal
        wheelScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(background);
        wheelRoutes.ShouldBe(0);
        activations.ShouldBe(0);
    }

    #endregion

    #region Failure recovery and reentrancy

    /// <summary>Verifies a submenu queued during entry replaces its sibling before its surface is exposed.</summary>
    [Fact]
    public async Task PerformInvoke_WhenEntryCallbackQueuesSibling_ReplaysCompleteSiblingTransitionAsync()
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Content = new ControlText("Copy") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        var queued = false;
        var fileWasOpenWhenEditExposed = false;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (!queued && ReferenceEquals(eventArgs.Current, menu))
            {
                queued = true;
                edit.PerformInvoke();
            }
        };
        editPopup.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Popup.IsOpen) && editPopup.IsOpen)
            {
                fileWasOpenWhenEditExposed = filePopup.IsOpen;
            }
        };

        // Act
        await surface.UpdateAsync(file.PerformInvoke, "queue sibling during modal entry");

        // Assert
        queued.ShouldBeTrue();
        fileWasOpenWhenEditExposed.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeTrue();
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        scope.Root.ShouldBeSameAs(menu);
    }

    /// <summary>Verifies failed modal entry discards a submenu queued from the failing focus callback.</summary>
    [Fact]
    public async Task PerformInvoke_WhenEntryCallbackQueuesSubmenuThenThrows_DiscardsQueuedOpenAsync()
    {
        // Arrange
        var expected = new InvalidOperationException("The modal-entry focus callback failed.");
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Content = new ControlText("Copy") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        var callbackCalls = 0;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (callbackCalls == 0 && ReferenceEquals(eventArgs.Current, menu))
            {
                callbackCalls++;
                edit.PerformInvoke();
                throw expected;
            }
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(file.PerformInvoke).ShouldBeSameAs(expected),
            "fail menu modal entry after queuing another submenu");

        // Assert
        callbackCalls.ShouldBe(1);
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies an entry callback cannot replay a submenu after invalidating its retained ownership.</summary>
    /// <param name="mutation">The structural or availability mutation applied after queuing the sibling.</param>
    [Theory]
    [InlineData("removed")]
    [InlineData("replaced-null")]
    [InlineData("disabled")]
    [InlineData("hidden")]
    [InlineData("menu-detached")]
    public async Task PerformInvoke_WhenQueuedSiblingBecomesInvalid_DiscardsTransitionAndSessionAsync(
        string mutation)
    {
        // Arrange
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var editMenu = new Menu { Orientation = Orientation.Vertical };
        editMenu.Items.Add(new MenuItem { Content = new ControlText("Copy") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var edit = new MenuItem { Content = new ControlText("Edit"), Submenu = editMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(edit);
        var root = new Overlay { Children = { menu } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var editPopup = OwnedTree.Find<Popup>(edit).ShouldNotBeNull();
        var callbackCalls = 0;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (callbackCalls != 0 || !ReferenceEquals(eventArgs.Current, menu))
            {
                return;
            }

            callbackCalls++;
            edit.PerformInvoke();

            switch (mutation)
            {
                case "removed":
                    _ = menu.Items.Remove(edit);
                    break;
                case "replaced-null":
                    edit.Submenu = null;
                    break;
                case "disabled":
                    edit.IsEnabled = false;
                    break;
                case "hidden":
                    edit.Visibility = Visibility.Hidden;
                    break;
                case "menu-detached":
                    _ = root.Children.Remove(menu);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown queued submenu mutation '{mutation}'.");
            }
        };

        // Act
        await surface.UpdateAsync(file.PerformInvoke, "invalidate queued sibling during modal entry");

        // Assert
        callbackCalls.ShouldBe(1);
        filePopup.IsOpen.ShouldBeFalse();
        editPopup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies an early failing submenu observer cannot suppress parent propagation or cleanup.</summary>
    [Fact]
    public async Task PerformInvoke_WhenEarlyNestedMenuSubscriberThrows_PropagatesAndClosesBeforeRethrowAsync()
    {
        // Arrange
        var expected = new InvalidOperationException("The early nested menu subscriber failed.");
        var leaf = new MenuItem { Content = new ControlText("Leaf") };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(leaf);
        submenu.ItemInvoked += (_, _) => throw expected;
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var rootInvocations = 0;
        menu.ItemInvoked += (_, eventArgs) =>
        {
            eventArgs.Item.ShouldBeSameAs(leaf);
            rootInvocations++;
        };
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open nested failure menu");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(leaf.PerformInvoke).ShouldBeSameAs(expected),
            "invoke leaf through early failing nested subscriber");

        // Assert
        rootInvocations.ShouldBe(1);
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies late nested observers run before the outer invocation transaction closes the chain.</summary>
    [Fact]
    public async Task PerformInvoke_WhenLateNestedSubscriberThrows_ObservesOpenChainThenClosesAsync()
    {
        // Arrange
        var expected = new InvalidOperationException("The late nested menu subscriber failed.");
        var leaf = new MenuItem { Content = new ControlText("Leaf") };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(leaf);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open menu for late invocation observer");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var observedOpen = false;
        submenu.ItemInvoked += (_, _) =>
        {
            observedOpen = popup.IsOpen && scope.IsActive;
            throw expected;
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(leaf.PerformInvoke).ShouldBeSameAs(expected),
            "invoke leaf through late failing nested observer");

        // Assert
        observedOpen.ShouldBeTrue();
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies external scope disposal closes visuals and leaves the menu reusable.</summary>
    [Fact]
    public async Task Dispose_WhenMenuScopeEndsExternally_ClosesVisualsAndAllowsLaterSessionAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open externally ended menu session");
        var first = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act external exit
        await surface.UpdateAsync(first.Dispose, "dispose menu scope externally");

        // Assert complete visual cleanup
        first.IsActive.ShouldBeFalse();
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        // Act reopen
        await surface.UpdateAsync(file.PerformInvoke, "reopen menu after external scope exit");
        var second = surface.Application.Modality.Active.ShouldNotBeNull();

        // Assert fresh reusable session
        second.ShouldNotBeSameAs(first);
        second.IsActive.ShouldBeTrue();
        popup.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies deepest-first visual cleanup attempts every callback and preserves the first failure.</summary>
    [Fact]
    public async Task Dispose_WhenNestedCloseAndExitCallbacksThrow_CompletesCleanupAndPreservesEarliestFailureAsync()
    {
        // Arrange
        var deepestMenu = new Menu { Orientation = Orientation.Vertical };
        deepestMenu.Items.Add(new MenuItem { Content = new ControlText("Leaf") });
        var nested = new MenuItem { Content = new ControlText("Nested"), Submenu = deepestMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        await surface.Pointer.MoveToAsync(nested);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var expected = new InvalidOperationException("The deepest close callback failed.");
        var order = new List<string>();
        nestedPopup.Closing += (_, _) =>
        {
            order.Add("deepest");
            throw expected;
        };
        firstPopup.Closing += (_, _) =>
        {
            order.Add("first");
            throw new InvalidOperationException("The first close callback failed.");
        };
        scope.Exited += (_, _) =>
        {
            order.Add("scope");
            throw new InvalidOperationException("The scope callback failed.");
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(scope.Dispose).ShouldBeSameAs(expected),
            "dispose menu scope with failing cleanup callbacks");

        // Assert
        order.ShouldBe(["deepest", "first", "scope"]);
        nestedPopup.IsOpen.ShouldBeFalse();
        firstPopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a close requested from modal-entry focus waits until the scope handle is tracked.</summary>
    [Fact]
    public async Task PerformInvoke_WhenEntryFocusCallbackClosesSession_DoesNotExposeOrStrandScopeAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var command = new MenuItem { Content = new ControlText("Close") };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        menu.Items.Add(command);
        var invocations = 0;
        command.Invoked += (_, _) => invocations++;
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (ReferenceEquals(eventArgs.Current, menu))
            {
                command.PerformInvoke();
            }
        };

        // Act
        await surface.UpdateAsync(file.PerformInvoke, "close menu from modal-entry focus callback");

        // Assert
        invocations.ShouldBe(1);
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies an old scope exit callback may open a replacement without stale identity cleanup.</summary>
    [Fact]
    public async Task Dispose_WhenExitedCallbackReopens_TracksReplacementByIdentityAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(30, 8),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open first identity session");
        var first = surface.Application.Modality.Active.ShouldNotBeNull();
        ModalScope? replacement = null;
        first.Exited += (_, _) =>
        {
            file.PerformInvoke();
            replacement = surface.Application.Modality.Active;
        };

        // Act
        await surface.UpdateAsync(first.Dispose, "replace session from old exit callback");

        // Assert
        first.IsActive.ShouldBeFalse();
        replacement.ShouldNotBeNull().IsActive.ShouldBeTrue();
        replacement.ShouldNotBeSameAs(first);
        surface.Application.Modality.Active.ShouldBeSameAs(replacement);
        popup.IsOpen.ShouldBeTrue();
    }

    #endregion

    #region Availability and parent modality

    /// <summary>Verifies submenu close completion releases its original owner after the anchor is reparented.</summary>
    [Fact]
    public async Task Escape_WhenClosingCallbackReparentsAnchor_ReleasesOriginalSessionAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var other = new Menu { Orientation = Orientation.Horizontal };
        var root = new Stack { Orientation = Orientation.Vertical };
        root.Children.Add(menu);
        root.Children.Add(other);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open menu before close-time reparenting");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var reparented = false;
        popup.Closing += (_, _) =>
        {
            if (!reparented)
            {
                reparented = true;
                menu.Items.Remove(file).ShouldBeTrue();
                other.Items.Add(file);
            }
        };

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, "close submenu while its anchor is reparented");

        // Assert
        reparented.ShouldBeTrue();
        other.Items.ShouldContain(file);
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies deep menu teardown uses bounded stack depth while closing every retained popup.</summary>
    [Fact]
    public void PerformInvoke_WhenMenuChainIsDeep_UsesBoundedTeardownStack()
    {
        // Arrange
        const int depth = 128;
        var leaf = new MenuItem { Content = new ControlText("Leaf") };
        var child = new Menu { Orientation = Orientation.Vertical };
        child.Items.Add(leaf);
        var anchors = new List<MenuItem>(depth);
        var popups = new List<Popup>(depth);

        for (var index = 0; index < depth; index++)
        {
            var anchor = new MenuItem
            {
                Content = new ControlText($"Level {index}"),
                Submenu = child,
            };
            var parent = new Menu { Orientation = Orientation.Vertical };
            parent.Items.Add(anchor);
            anchors.Add(anchor);
            popups.Add(OwnedTree.Find<Popup>(anchor).ShouldNotBeNull());
            child = parent;
        }

        anchors.Reverse();
        foreach (var anchor in anchors)
        {
            anchor.PerformInvoke();
        }

        var recursiveFrames = -1;
        popups[0].Closing += (_, _) =>
        {
            recursiveFrames = new StackTrace().GetFrames().Count(
                frame => string.Equals(
                    frame.GetMethod()?.Name,
                    "CloseOpenSubmenus",
                    StringComparison.Ordinal));
        };

        // Act
        leaf.PerformInvoke();

        // Assert
        recursiveFrames.ShouldBeLessThanOrEqualTo(1);
        popups.ShouldAllBe(popup => !popup.IsOpen);
    }

    /// <summary>Verifies loss of the primary menu root closes every retained popup and the scope.</summary>
    [Fact]
    public async Task Visibility_WhenPrimaryMenuBecomesUnavailable_ClosesCompleteSessionAsync()
    {
        // Arrange
        var nestedMenu = new Menu { Orientation = Orientation.Vertical };
        nestedMenu.Items.Add(new MenuItem { Content = new ControlText("Leaf") });
        var nested = new MenuItem { Content = new ControlText("Nested"), Submenu = nestedMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        await surface.Pointer.MoveToAsync(nested);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(() => menu.Visibility = Visibility.Hidden, "hide primary menu root");

        // Assert
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        firstPopup.IsOpen.ShouldBeFalse();
        nestedPopup.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies replacing an open submenu preserves its scope while removal ends the session.</summary>
    [Fact]
    public async Task Submenu_WhenOpenValueIsReplacedAndRemoved_DoesNotStrandModalSessionAsync()
    {
        // Arrange
        var firstSubmenu = new Menu { Orientation = Orientation.Vertical };
        firstSubmenu.Items.Add(new MenuItem { Content = new ControlText("First") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = firstSubmenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(file);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var replacement = new Menu { Orientation = Orientation.Vertical };
        replacement.Items.Add(new MenuItem { Content = new ControlText("Replacement") });

        // Act replace while open
        await surface.UpdateAsync(() => file.Submenu = replacement, "replace open submenu");
        var replacementPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();

        // Assert replacement keeps exact session
        firstPopup.IsDisposed.ShouldBeTrue();
        firstSubmenu.IsDisposed.ShouldBeFalse();
        replacementPopup.ShouldNotBeSameAs(firstPopup);
        replacementPopup.IsOpen.ShouldBeTrue();
        replacementPopup.Content.ShouldBeSameAs(replacement);
        surface.Application.Modality.Active.ShouldBeSameAs(scope);

        // Act remove while replacement remains open
        await surface.UpdateAsync(() => file.Submenu = null, "remove open submenu");

        // Assert complete teardown
        replacementPopup.IsDisposed.ShouldBeTrue();
        replacement.IsDisposed.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies unexpected nested-menu unavailability cannot leave an armed scope without a live chain.</summary>
    [Fact]
    public async Task Visibility_WhenOpenNestedMenuBecomesUnavailable_ClosesCompleteSessionAsync()
    {
        // Arrange
        var nestedMenu = new Menu { Orientation = Orientation.Vertical };
        nestedMenu.Items.Add(new MenuItem { Content = new ControlText("Leaf") });
        var nested = new MenuItem { Content = new ControlText("Nested"), Submenu = nestedMenu };
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(nested);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var firstPopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var nestedPopup = OwnedTree.Find<Popup>(nested).ShouldNotBeNull();
        await surface.Pointer.ClickAsync(file);
        await surface.Pointer.MoveToAsync(nested);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () => nestedMenu.Visibility = Visibility.Hidden,
            "hide open nested menu content");

        // Assert
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        firstPopup.IsOpen.ShouldBeFalse();
        nestedPopup.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies every unavailable top anchor ends its complete session without later resurrection.</summary>
    /// <param name="mutation">The availability or ownership transition applied to the open anchor.</param>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("removed")]
    [InlineData("cleared")]
    [InlineData("disposed")]
    public async Task Availability_WhenOpenTopAnchorBecomesUnavailable_ClosesSessionWithoutResurrectionAsync(
        string mutation)
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open top anchor before making it unavailable");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () => MakeAnchorUnavailable(menu, file, mutation),
            $"make open top anchor {mutation}");

        // Assert complete teardown
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        if (mutation == "disposed")
        {
            file.IsDisposed.ShouldBeTrue();
            popup.IsDisposed.ShouldBeTrue();
            menu.Items.ShouldNotContain(file);
        }
        else
        {
            popup.IsDisposed.ShouldBeFalse();

            // Act restore the same retained anchor
            await surface.UpdateAsync(
                () => RestoreAnchor(menu, file, mutation),
                $"restore unavailable top anchor after {mutation}");

            // Assert restoration does not resurrect stale popup or scope state
            menu.Items.ShouldContain(file);
            popup.IsOpen.ShouldBeFalse();
            surface.Application.Modality.Active.ShouldBeNull();

            // Act and assert an explicit later activation owns one fresh reusable session
            await surface.UpdateAsync(file.PerformInvoke, "explicitly reopen restored top anchor");
            var replacement = surface.Application.Modality.Active.ShouldNotBeNull();
            replacement.ShouldNotBeSameAs(scope);
            popup.IsOpen.ShouldBeTrue();
            await surface.UpdateAsync(file.PerformInvoke, "close replacement top session");
            replacement.IsActive.ShouldBeFalse();
            surface.Application.Modality.Active.ShouldBeNull();
        }
    }

    /// <summary>Verifies every unavailable nested anchor ends the exact complete top session.</summary>
    /// <param name="mutation">The availability or ownership transition applied to the open anchor.</param>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("removed")]
    [InlineData("cleared")]
    [InlineData("disposed")]
    public async Task Availability_WhenOpenNestedAnchorBecomesUnavailable_ClosesCompleteSessionWithoutResurrectionAsync(
        string mutation)
    {
        // Arrange
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Content = new ControlText("Today") });
        var recent = new MenuItem { Content = new ControlText("Recent"), Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open parent for unavailable nested anchor");
        await surface.UpdateAsync(recent.PerformInvoke, "open nested anchor before making it unavailable");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(
            () => MakeAnchorUnavailable(fileMenu, recent, mutation),
            $"make open nested anchor {mutation}");

        // Assert complete top-session teardown
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        if (mutation == "disposed")
        {
            recent.IsDisposed.ShouldBeTrue();
            recentPopup.IsDisposed.ShouldBeTrue();
            fileMenu.Items.ShouldNotContain(recent);
        }
        else
        {
            recentPopup.IsDisposed.ShouldBeFalse();
            await surface.UpdateAsync(
                () => RestoreAnchor(fileMenu, recent, mutation),
                $"restore unavailable nested anchor after {mutation}");
            fileMenu.Items.ShouldContain(recent);
        }

        // Act reopen only the parent branch
        await surface.UpdateAsync(file.PerformInvoke, "explicitly reopen parent after nested unavailability");
        var replacement = surface.Application.Modality.Active.ShouldNotBeNull();

        // Assert stale nested state does not resurrect
        replacement.ShouldNotBeSameAs(scope);
        filePopup.IsOpen.ShouldBeTrue();
        recentPopup.IsOpen.ShouldBeFalse();
        await surface.UpdateAsync(file.PerformInvoke, "close replacement parent session");
        replacement.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies press cleanup cannot redirect teardown away from the captured original session.</summary>
    [Fact]
    public async Task Visibility_WhenPressedOpenAnchorReparentsDuringCleanup_ClosesCapturedOriginalSessionAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var other = new Menu { Orientation = Orientation.Horizontal };
        var root = new Stack { Orientation = Orientation.Vertical };
        root.Children.Add(menu);
        root.Children.Add(other);
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open pressed anchor before cleanup reparenting");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(menu).ShouldBeTrue(),
            "return focus to top menu before holding its anchor");
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));
        file.IsPressed.ShouldBeTrue();
        var reparented = false;
        file.PropertyChanged += (_, eventArgs) =>
        {
            if (!reparented && eventArgs.PropertyName == nameof(Control.IsPressed) && !file.IsPressed)
            {
                reparented = true;
                menu.Items.Remove(file).ShouldBeTrue();
                other.Items.Add(file);
            }
        };

        // Act
        await surface.UpdateAsync(() => file.Visibility = Visibility.Hidden, "hide pressed open anchor");

        // Assert
        reparented.ShouldBeTrue();
        other.Items.ShouldContain(file);
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        await surface.UpdateAsync(() => file.Visibility = Visibility.Visible, "restore reparented anchor visibility");
        popup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies close failures remain authoritative after complete nested-anchor cleanup.</summary>
    [Fact]
    public async Task IsEnabled_WhenOpenNestedAnchorCleanupCallbacksThrow_PreservesEarliestFailureAfterTeardownAsync()
    {
        // Arrange
        var closeFailure = new InvalidOperationException("The unavailable anchor close callback failed.");
        var propertyFailure = new InvalidOperationException("The unavailable anchor property callback failed.");
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Content = new ControlText("Today") });
        var recent = new MenuItem { Content = new ControlText("Recent"), Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open parent before failing nested unavailability");
        await surface.UpdateAsync(recent.PerformInvoke, "open nested anchor before failing unavailability");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var closeCallbacks = 0;
        var propertyCallbacks = 0;
        recentPopup.Closing += (_, _) =>
        {
            closeCallbacks++;
            throw closeFailure;
        };
        recent.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Control.IsEnabled))
            {
                propertyCallbacks++;
                throw propertyFailure;
            }
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(() => recent.IsEnabled = false)
                .ShouldBeSameAs(closeFailure),
            "disable nested anchor with failing cleanup callbacks");

        // Assert
        closeCallbacks.ShouldBe(1);
        propertyCallbacks.ShouldBe(1);
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a top anchor becoming unavailable from its closing callback ends the captured session.</summary>
    /// <param name="mutation">The reentrant availability transition applied by the callback.</param>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("disposed")]
    public async Task IsOpen_WhenClosingCallbackMakesTopAnchorUnavailable_ClosesCapturedSessionAsync(
        string mutation)
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open top anchor before reentrant closing mutation");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var callbacks = 0;
        popup.Closing += (_, _) =>
        {
            callbacks++;
            MakeAnchorUnavailable(menu, file, mutation);
        };

        // Act
        await surface.UpdateAsync(() => popup.IsOpen = false, $"close top popup while anchor becomes {mutation}");

        // Assert complete exact-owner cleanup
        callbacks.ShouldBe(1);
        popup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        if (mutation == "disposed")
        {
            file.IsDisposed.ShouldBeTrue();
            popup.IsDisposed.ShouldBeTrue();
            menu.Items.ShouldNotContain(file);
        }
        else
        {
            await surface.UpdateAsync(
                () => RestoreAnchor(menu, file, mutation),
                $"restore top anchor after closing callback made it {mutation}");
            popup.IsOpen.ShouldBeFalse();
            surface.Application.Modality.Active.ShouldBeNull();
        }
    }

    /// <summary>Verifies a nested anchor becoming unavailable from its closing callback ends the top session.</summary>
    /// <param name="mutation">The reentrant availability transition applied by the callback.</param>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("disposed")]
    public async Task IsOpen_WhenClosingCallbackMakesNestedAnchorUnavailable_ClosesCompleteSessionAsync(
        string mutation)
    {
        // Arrange
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Content = new ControlText("Today") });
        var recent = new MenuItem { Content = new ControlText("Recent"), Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open parent before reentrant nested closing mutation");
        await surface.UpdateAsync(recent.PerformInvoke, "open nested anchor before reentrant closing mutation");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var callbacks = 0;
        recentPopup.Closing += (_, _) =>
        {
            callbacks++;
            MakeAnchorUnavailable(fileMenu, recent, mutation);
        };

        // Act
        await surface.UpdateAsync(
            () => recentPopup.IsOpen = false,
            $"close nested popup while anchor becomes {mutation}");

        // Assert complete top-session cleanup
        callbacks.ShouldBe(1);
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        if (mutation == "disposed")
        {
            recent.IsDisposed.ShouldBeTrue();
            recentPopup.IsDisposed.ShouldBeTrue();
            fileMenu.Items.ShouldNotContain(recent);
        }
        else
        {
            await surface.UpdateAsync(
                () => RestoreAnchor(fileMenu, recent, mutation),
                $"restore nested anchor after closing callback made it {mutation}");
            fileMenu.Items.ShouldContain(recent);
        }

        // Act reopen only the parent to prove stale nested state cannot resurrect
        await surface.UpdateAsync(file.PerformInvoke, "reopen parent after reentrant nested unavailability");
        var replacement = surface.Application.Modality.Active.ShouldNotBeNull();
        replacement.ShouldNotBeSameAs(scope);
        filePopup.IsOpen.ShouldBeTrue();
        recentPopup.IsOpen.ShouldBeFalse();
        await surface.UpdateAsync(file.PerformInvoke, "close replacement parent session");
        replacement.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies focus-restoration mutation retains its first failure after complete session cleanup.</summary>
    [Fact]
    public async Task IsOpen_WhenFocusRestorationDisablesNestedAnchor_PreservesFirstFailureAfterTeardownAsync()
    {
        // Arrange
        var focusFailure = new InvalidOperationException("The focus-restoration callback failed.");
        var closedFailure = new InvalidOperationException("The later closed callback failed.");
        var recentMenu = new Menu { Orientation = Orientation.Vertical };
        recentMenu.Items.Add(new MenuItem { Content = new ControlText("Today") });
        var recent = new MenuItem { Content = new ControlText("Recent"), Submenu = recentMenu };
        var fileMenu = new Menu { Orientation = Orientation.Vertical };
        fileMenu.Items.Add(recent);
        var file = new MenuItem { Content = new ControlText("File"), Submenu = fileMenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        await using var surface = await ComponentSurface.MountAsync(
            menu,
            new Size(40, 10),
            TestContext.Current.CancellationToken);
        var filePopup = OwnedTree.Find<Popup>(file).ShouldNotBeNull();
        var recentPopup = OwnedTree.Find<Popup>(recent).ShouldNotBeNull();
        await surface.UpdateAsync(file.PerformInvoke, "open parent before focus-restoration mutation");
        await surface.UpdateAsync(recent.PerformInvoke, "open nested anchor before focus-restoration mutation");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var focusCallbacks = 0;
        var closedCallbacks = 0;
        surface.Application.Focus.Gained += (_, eventArgs) =>
        {
            if (focusCallbacks == 0 && ReferenceEquals(eventArgs.Current, fileMenu))
            {
                focusCallbacks++;
                recent.IsEnabled = false;
                throw focusFailure;
            }
        };
        recentPopup.Closed += (_, _) =>
        {
            closedCallbacks++;
            throw closedFailure;
        };

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<InvalidOperationException>(() => recentPopup.IsOpen = false)
                .ShouldBeSameAs(focusFailure),
            "close nested popup through failing focus-restoration mutation");

        // Assert
        focusCallbacks.ShouldBe(1);
        closedCallbacks.ShouldBe(1);
        recent.IsEnabled.ShouldBeFalse();
        recentPopup.IsOpen.ShouldBeFalse();
        filePopup.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
        await surface.UpdateAsync(() => recent.IsEnabled = true, "restore nested anchor after focus failure");
        recentPopup.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a menu session nests under a modal Window and restores its parent scope.</summary>
    [Fact]
    public async Task CloseChain_WhenMenuIsInsideModalWindow_RestoresWindowScopeAsync()
    {
        // Arrange
        var submenu = new Menu { Orientation = Orientation.Vertical };
        submenu.Items.Add(new MenuItem { Content = new ControlText("Open") });
        var file = new MenuItem { Content = new ControlText("File"), Submenu = submenu };
        var menu = new Menu { Orientation = Orientation.Horizontal };
        menu.Items.Add(file);
        var window = new Window
        {
            Content = menu,
            Visibility = Visibility.Collapsed,
            Width = Length.Cells(24),
            Height = Length.Cells(8),
        };
        var root = new Overlay { Children = { window } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(40, 12),
            TestContext.Current.CancellationToken);
        ModalScope? windowScope = null;
        await surface.UpdateAsync(
            () => windowScope = window.ShowModal(initialFocus: menu),
            "show modal Window containing menu");
        await surface.UpdateAsync(file.PerformInvoke, "open nested menu plane");
        var menuScope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Assert nested child scope
        menuScope.ShouldNotBeSameAs(windowScope);
        menuScope.Root.ShouldBeSameAs(menu);
        windowScope.ShouldNotBeNull().IsActive.ShouldBeTrue();

        // Act close popup level, then root session
        await surface.Keyboard.PressAsync(Code.Escape);
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert parent scope restored
        menuScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeSameAs(windowScope);
        windowScope.IsActive.ShouldBeTrue();
        window.Visibility.ShouldBe(Visibility.Visible);
        surface.ShouldHaveFocus(menu);
    }

    private static void MakeAnchorUnavailable(Menu menu, MenuItem anchor, string mutation)
    {
        switch (mutation)
        {
            case "hidden":
                anchor.Visibility = Visibility.Hidden;
                break;
            case "disabled":
                anchor.IsEnabled = false;
                break;
            case "removed":
                menu.Items.Remove(anchor).ShouldBeTrue();
                break;
            case "cleared":
                menu.Items.Clear();
                break;
            case "disposed":
                anchor.Dispose();
                break;
            default:
                throw new InvalidOperationException($"Unknown menu-anchor mutation '{mutation}'.");
        }
    }

    private static void RestoreAnchor(Menu menu, MenuItem anchor, string mutation)
    {
        switch (mutation)
        {
            case "hidden":
                anchor.Visibility = Visibility.Visible;
                break;
            case "disabled":
                anchor.IsEnabled = true;
                break;
            case "removed":
            case "cleared":
                menu.Items.Add(anchor);
                break;
            default:
                throw new InvalidOperationException($"Unknown restorable menu-anchor mutation '{mutation}'.");
        }
    }

    #endregion
}
