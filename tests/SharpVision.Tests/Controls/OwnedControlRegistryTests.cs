// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.DataBinding;

/// <summary>Verifies central ownership slots, transactional publication, and disposal, plus
/// cross-cutting Tab, render, and hit-test traversal over every central ownership slot.</summary>
public sealed class OwnedControlRegistryTests
{
    /// <summary>Verifies a disabled ordinary owner publishes each changed derived state once for
    /// every affected level on add and remove, while an already-disabled descendant stays silent.</summary>
    [Fact]
    public void AddAndRemove_WhenOwnerIsDisabled_PublishesChangedDerivedStateAcrossSubtreeExactlyOnce()
    {
        var owner = new ProbeContainer { IsEnabled = false };
        var branch = new ProbeOwnedControl { IsFocusable = true };
        var leaf = new ProbeControl { IsFocusable = true };
        var locallyDisabled = new ProbeControl { IsFocusable = true, IsEnabled = false };
        branch.AddPrimary(leaf);
        branch.AddPrimary(locallyDisabled);
        var branchNotifications = new List<string?>();
        var leafNotifications = new List<string?>();
        var locallyDisabledNotifications = new List<string?>();
        branch.PropertyChanged += (_, eventArgs) => branchNotifications.Add(eventArgs.PropertyName);
        leaf.PropertyChanged += (_, eventArgs) => leafNotifications.Add(eventArgs.PropertyName);
        locallyDisabled.PropertyChanged += (_, eventArgs) => locallyDisabledNotifications.Add(eventArgs.PropertyName);
        string?[] expected =
        [
            nameof(ControlBase.EffectiveIsEnabled),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.CanTabStop),
        ];

        owner.Children.Add(branch);

        branchNotifications.ShouldBe(expected);
        leafNotifications.ShouldBe(expected);
        locallyDisabledNotifications.ShouldBeEmpty();
        branchNotifications.Clear();
        leafNotifications.Clear();

        owner.Children.Remove(branch).ShouldBeTrue();

        branchNotifications.ShouldBe(expected);
        leafNotifications.ShouldBe(expected);
        locallyDisabledNotifications.ShouldBeEmpty();
    }

    /// <summary>Verifies a collapsed ordinary owner publishes only visibility-derived changes on
    /// add and remove, while an already-collapsed descendant remains unchanged and silent.</summary>
    [Fact]
    public void AddAndRemove_WhenOwnerIsCollapsed_PublishesChangedVisibilityStateAcrossSubtreeExactlyOnce()
    {
        var owner = new ProbeContainer { Visibility = Visibility.Collapsed };
        var branch = new ProbeOwnedControl { IsFocusable = true };
        var leaf = new ProbeControl { IsFocusable = true };
        var locallyCollapsed = new ProbeControl { IsFocusable = true, Visibility = Visibility.Collapsed };
        branch.AddPrimary(leaf);
        branch.AddPrimary(locallyCollapsed);
        var branchNotifications = new List<string?>();
        var leafNotifications = new List<string?>();
        var locallyCollapsedNotifications = new List<string?>();
        branch.PropertyChanged += (_, eventArgs) => branchNotifications.Add(eventArgs.PropertyName);
        leaf.PropertyChanged += (_, eventArgs) => leafNotifications.Add(eventArgs.PropertyName);
        locallyCollapsed.PropertyChanged += (_, eventArgs) => locallyCollapsedNotifications.Add(eventArgs.PropertyName);
        string?[] expected =
        [
            nameof(ControlBase.EffectiveIsVisible),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.CanTabStop),
        ];

        owner.Children.Add(branch);

        branchNotifications.ShouldBe(expected);
        leafNotifications.ShouldBe(expected);
        locallyCollapsedNotifications.ShouldBeEmpty();
        branchNotifications.Clear();
        leafNotifications.Clear();

        owner.Children.Remove(branch).ShouldBeTrue();

        branchNotifications.ShouldBe(expected);
        leafNotifications.ShouldBe(expected);
        locallyCollapsedNotifications.ShouldBeEmpty();
    }

    /// <summary>Verifies a failing derived-state listener cannot suppress remaining property or
    /// slot publications after a framework-part ownership commit.</summary>
    [Fact]
    public void Add_WhenDerivedStateNotificationThrows_PublishesRemainingStateAndSlotThenRethrows()
    {
        var owner = new ProbeOwnedControl { IsEnabled = false };
        var branch = new ProbeOwnedControl { IsFocusable = true };
        var leaf = new ProbeControl { IsFocusable = true };
        branch.AddPrimary(leaf);
        var branchNotifications = new List<string?>();
        var leafNotifications = new List<string?>();
        var publicationOrder = new List<string>();
        branch.ParentChanging = (_, _, _) => publicationOrder.Add("parent");
        owner.PrimaryChanging = _ => publicationOrder.Add("slot");
        branch.PropertyChanged += (_, eventArgs) =>
        {
            branchNotifications.Add(eventArgs.PropertyName);
            publicationOrder.Add($"branch:{eventArgs.PropertyName}");
            if (eventArgs.PropertyName == nameof(ControlBase.EffectiveIsEnabled))
            {
                throw new InvalidOperationException("derived state publication failed");
            }
        };
        leaf.PropertyChanged += (_, eventArgs) =>
        {
            leafNotifications.Add(eventArgs.PropertyName);
            publicationOrder.Add($"leaf:{eventArgs.PropertyName}");
        };

        var exception = Should.Throw<InvalidOperationException>(() => owner.AddPrimary(branch));

        exception.Message.ShouldBe("derived state publication failed");
        branch.Parent.ShouldBeSameAs(owner);
        branchNotifications.ShouldBe(
        [
            nameof(ControlBase.EffectiveIsEnabled),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.CanTabStop),
        ]);
        leafNotifications.ShouldBe(branchNotifications);
        owner.PrimaryChanges.ShouldBe(1);
        publicationOrder.ShouldBe(
        [
            "parent",
            $"branch:{nameof(ControlBase.EffectiveIsEnabled)}",
            $"branch:{nameof(ControlBase.CanFocus)}",
            $"branch:{nameof(ControlBase.CanTabStop)}",
            $"leaf:{nameof(ControlBase.EffectiveIsEnabled)}",
            $"leaf:{nameof(ControlBase.CanFocus)}",
            $"leaf:{nameof(ControlBase.CanTabStop)}",
            "slot",
        ]);
    }

    /// <summary>Verifies one-way bindings sourced from every derived availability property remain
    /// current when a source control enters and leaves a disabled, collapsed owner.</summary>
    [Fact]
    public void AddAndRemove_WhenDerivedStateIsBound_UpdatesEveryBinding()
    {
        var owner = new ProbeContainer { IsEnabled = false, Visibility = Visibility.Collapsed };
        var source = new ProbeControl { IsFocusable = true };
        var visibleTarget = new ProbeControl();
        var enabledTarget = new ProbeControl();
        var focusTarget = new ProbeControl();
        var tabStopTarget = new ProbeControl();
        using var visibleBinding = visibleTarget.BindProperty(
            target => target.IsEnabled,
            source,
            model => model.EffectiveIsVisible,
            BindingMode.OneWay);
        using var enabledBinding = enabledTarget.BindProperty(
            target => target.IsEnabled,
            source,
            model => model.EffectiveIsEnabled,
            BindingMode.OneWay);
        using var focusBinding = focusTarget.BindProperty(
            target => target.IsEnabled,
            source,
            model => model.CanFocus,
            BindingMode.OneWay);
        using var tabStopBinding = tabStopTarget.BindProperty(
            target => target.IsEnabled,
            source,
            model => model.CanTabStop,
            BindingMode.OneWay);

        owner.Children.Add(source);

        visibleTarget.IsEnabled.ShouldBeFalse();
        enabledTarget.IsEnabled.ShouldBeFalse();
        focusTarget.IsEnabled.ShouldBeFalse();
        tabStopTarget.IsEnabled.ShouldBeFalse();

        owner.Children.Remove(source).ShouldBeTrue();

        visibleTarget.IsEnabled.ShouldBeTrue();
        enabledTarget.IsEnabled.ShouldBeTrue();
        focusTarget.IsEnabled.ShouldBeTrue();
        tabStopTarget.IsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies compound mutations that name both an ancestor and one of its descendants
    /// capture and publish the descendant's derived state only once.</summary>
    [Fact]
    public void CommitCompound_WhenChangedRootsOverlap_PublishesEachDerivedStateOnce()
    {
        var owner = new ProbeContainer { IsEnabled = false };
        var branch = new ProbeOwnedControl { IsFocusable = true };
        var leaf = new ProbeControl { IsFocusable = true };
        branch.AddPrimary(leaf);
        owner.Children.Add(branch);
        var leafNotifications = new List<string?>();
        leaf.PropertyChanged += (_, eventArgs) => leafNotifications.Add(eventArgs.PropertyName);

        OwnedControlRegistry.CommitCompound(
            static () => { },
            (owner.Children.OwnedSlot, Array.Empty<ControlBase>()),
            (branch.PrimarySlot, Array.Empty<ControlBase>()));

        leaf.Parent.ShouldBeNull();
        leafNotifications.ShouldBe(
        [
            nameof(ControlBase.EffectiveIsEnabled),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.CanTabStop),
        ]);
    }

    /// <summary>Verifies a compound commit publishes lifecycle only after both owned hosts expose
    /// their final snapshots, rejects reentry across either host, and continues later callbacks.</summary>
    [Fact]
    public void CommitCompound_WhenFirstParentCallbackFails_KeepsBothHostsCoherentAndGuarded()
    {
        var root = new ProbeContainer();
        var pages = new ProbeContainer();
        var headers = new ProbeContainer();
        root.Children.Add(pages);
        root.Children.Add(headers);
        var page = new OwnershipObserverControl();
        var header = new OwnershipObserverControl();
        var headerParentChanges = 0;
        page.ParentChanging = (_, _, current) =>
        {
            current.ShouldBeSameAs(pages);
            pages.Children.ShouldBe([page]);
            headers.Children.ShouldBe([header]);
            _ = Should.Throw<InvalidOperationException>(() => headers.Children.Add(new ProbeControl()));
            throw new InvalidOperationException("page parent publication failed");
        };
        header.ParentChanging = (_, _, current) =>
        {
            current.ShouldBeSameAs(headers);
            headerParentChanges++;
        };

        var exception = Should.Throw<InvalidOperationException>(() =>
            OwnedControlRegistry.CommitCompound(
                static () => { },
                (pages.Children.OwnedSlot, new ControlBase[] { page }),
                (headers.Children.OwnedSlot, new ControlBase[] { header })));

        exception.Message.ShouldBe("page parent publication failed");
        pages.Children.ShouldBe([page]);
        headers.Children.ShouldBe([header]);
        headerParentChanges.ShouldBe(1);
    }

    /// <summary>Verifies a failing first slot notification cannot suppress the later participant's
    /// notification or roll back either committed snapshot.</summary>
    [Fact]
    public void CommitCompound_WhenSlotNotificationThrows_PublishesEveryParticipantThenRethrows()
    {
        var pages = new ProbeContainer();
        var headers = new ProbeContainer();
        var page = new ProbeControl();
        var header = new ProbeControl();
        var headerChanges = 0;
        pages.Children.Changed += () => throw new InvalidOperationException("page slot publication failed");
        headers.Children.Changed += () => headerChanges++;

        var exception = Should.Throw<InvalidOperationException>(() =>
            OwnedControlRegistry.CommitCompound(
                static () => { },
                (pages.Children.OwnedSlot, new ControlBase[] { page }),
                (headers.Children.OwnedSlot, new ControlBase[] { header })));

        exception.Message.ShouldBe("page slot publication failed");
        pages.Children.ShouldBe([page]);
        headers.Children.ShouldBe([header]);
        headerChanges.ShouldBe(1);
    }

    /// <summary>Verifies every participating snapshot is validated before the first slot or
    /// framework bookkeeping continuation can change.</summary>
    [Fact]
    public void CommitCompound_WhenDetachedCandidateRepeatsAcrossSlots_RejectsBeforeMutation()
    {
        var pages = new ProbeContainer();
        var headers = new ProbeContainer();
        var candidate = new ProbeControl();
        var continuationCalls = 0;

        _ = Should.Throw<ArgumentException>(() =>
            OwnedControlRegistry.CommitCompound(
                () => continuationCalls++,
                (pages.Children.OwnedSlot, new ControlBase[] { candidate }),
                (headers.Children.OwnedSlot, new ControlBase[] { candidate })));

        continuationCalls.ShouldBe(0);
        pages.Children.ShouldBeEmpty();
        headers.Children.ShouldBeEmpty();
        candidate.Parent.ShouldBeNull();
    }


    /// <summary>Verifies add preflights Theme impact before changing slot ownership or context.</summary>
    [Fact]
    public void Add_WhenThemeImpactHookThrows_PreservesOwnershipAndContext()
    {
        var theme = new Theme();
        theme.Freeze();
        var owner = new ProbeOwnedControl();
        owner.PropagateTheme(theme);
        var child = new StyledProbe { ThrowOnThemeImpact = true };
        var expectedFace = child.ActualFace;
        var expectedResolutions = child.UncachedAppearanceResolutionCount;
        var notifications = new List<string?>();
        child.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        _ = Should.Throw<InvalidOperationException>(() => owner.AddPrimary(child));

        child.ThrowOnThemeImpact = false;
        owner.PrimaryCount.ShouldBe(0);
        owner.PrimaryChanges.ShouldBe(0);
        child.Parent.ShouldBeNull();
        child.OwningSlot.ShouldBeNull();
        child.Dispatcher.ShouldBeNull();
        child.Theme.ShouldBeNull();
        child.ActualFace.ShouldBe(expectedFace);
        child.UncachedAppearanceResolutionCount.ShouldBe(expectedResolutions);
        owner.Pending.ShouldBe(Invalidation.None);
        child.Pending.ShouldBe(Invalidation.None);
        child.AttachedCalls.ShouldBe(0);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies direct attachment preflights prospective appearance before context assignment.</summary>
    [Fact]
    public async Task Attach_WhenProspectiveAppearanceThrows_PreservesContextAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var theme = new Theme();
        theme.Freeze();
        var control = new StyledProbe { AppearanceStatesFailureTheme = theme };
        var expectedFace = control.ActualFace;
        var expectedResolutions = control.UncachedAppearanceResolutionCount;
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.Clear(Invalidation.All);

        await dispatcher.InvokeAsync(
            () =>
            {
                _ = Should.Throw<InvalidOperationException>(
                    () => control.Attach(dispatcher, UnicodePolicy.Default, theme, static () => { }));
            },
            TestContext.Current.CancellationToken);

        control.AppearanceStatesFailureTheme = null;
        control.Dispatcher.ShouldBeNull();
        control.Theme.ShouldBeNull();
        control.Parent.ShouldBeNull();
        control.OwningSlot.ShouldBeNull();
        control.ActualFace.ShouldBe(expectedFace);
        control.UncachedAppearanceResolutionCount.ShouldBe(expectedResolutions);
        control.Pending.ShouldBe(Invalidation.None);
        control.AttachedCalls.ShouldBe(0);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies remove preflights resolved-style selection before clearing ownership or context.</summary>
    [Fact]
    public void Remove_WhenResolvedStyleHookThrows_PreservesOwnershipAndContext()
    {
        var theme = new Theme();
        theme.Freeze();
        var owner = new ProbeOwnedControl();
        var child = new StyledProbe();
        owner.AddPrimary(child);
        owner.PropagateTheme(theme);
        var expectedChanges = owner.PrimaryChanges;
        var expectedSlot = child.OwningSlot;
        var expectedFace = child.ActualFace;
        var expectedResolutions = child.UncachedAppearanceResolutionCount;
        var notifications = new List<string?>();
        child.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        child.ThrowOnResolvedStyleSelection = true;
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        _ = Should.Throw<InvalidOperationException>(() => owner.RemovePrimary(child));

        child.ThrowOnResolvedStyleSelection = false;
        owner.PrimaryCount.ShouldBe(1);
        owner.PrimaryAt(0).ShouldBeSameAs(child);
        owner.PrimaryChanges.ShouldBe(expectedChanges);
        child.Parent.ShouldBeSameAs(owner);
        child.OwningSlot.ShouldBeSameAs(expectedSlot);
        child.Theme.ShouldBeSameAs(theme);
        child.ActualFace.ShouldBe(expectedFace);
        child.UncachedAppearanceResolutionCount.ShouldBe(expectedResolutions);
        owner.Pending.ShouldBe(Invalidation.None);
        child.Pending.ShouldBe(Invalidation.None);
        child.DetachedCalls.ShouldBe(0);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies direct detachment preflights Theme impact before clearing inherited context.</summary>
    [Fact]
    public async Task Detach_WhenThemeImpactHookThrows_PreservesContextAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var theme = new Theme();
        theme.Freeze();
        var control = new StyledProbe();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher, UnicodePolicy.Default, theme, static () => { }),
            TestContext.Current.CancellationToken);
        var expectedFace = control.ActualFace;
        var expectedResolutions = control.UncachedAppearanceResolutionCount;
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.ThrowOnThemeImpact = true;
        control.Clear(Invalidation.All);

        await dispatcher.InvokeAsync(
            () =>
            {
                _ = Should.Throw<InvalidOperationException>(control.Detach);
            },
            TestContext.Current.CancellationToken);

        control.ThrowOnThemeImpact = false;
        control.Dispatcher.ShouldBeSameAs(dispatcher);
        control.Theme.ShouldBeSameAs(theme);
        control.Parent.ShouldBeNull();
        control.OwningSlot.ShouldBeNull();
        control.ActualFace.ShouldBe(expectedFace);
        control.UncachedAppearanceResolutionCount.ShouldBe(expectedResolutions);
        control.Pending.ShouldBe(Invalidation.None);
        control.AttachedCalls.ShouldBe(1);
        control.DetachedCalls.ShouldBe(0);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies replacement preflights prospective appearance before changing either ownership tree.</summary>
    [Fact]
    public void Replace_WhenProspectiveAppearanceThrows_PreservesBothOwnershipTrees()
    {
        var theme = new Theme();
        theme.Freeze();
        var owner = new ProbeOwnedControl();
        var original = new StyledProbe();
        var replacement = new StyledProbe { AppearanceStatesFailureTheme = theme };
        owner.AddPrimary(original);
        owner.PropagateTheme(theme);
        var expectedChanges = owner.PrimaryChanges;
        var expectedSlot = original.OwningSlot;
        var originalNotifications = new List<string?>();
        var replacementNotifications = new List<string?>();
        original.PropertyChanged += (_, eventArgs) => originalNotifications.Add(eventArgs.PropertyName);
        replacement.PropertyChanged += (_, eventArgs) => replacementNotifications.Add(eventArgs.PropertyName);
        owner.Clear(Invalidation.All);
        original.Clear(Invalidation.All);
        replacement.Clear(Invalidation.All);

        _ = Should.Throw<InvalidOperationException>(() => owner.ReplacePrimary(0, replacement));

        replacement.AppearanceStatesFailureTheme = null;
        owner.PrimaryCount.ShouldBe(1);
        owner.PrimaryAt(0).ShouldBeSameAs(original);
        owner.PrimaryChanges.ShouldBe(expectedChanges);
        original.Parent.ShouldBeSameAs(owner);
        original.OwningSlot.ShouldBeSameAs(expectedSlot);
        original.Theme.ShouldBeSameAs(theme);
        replacement.Parent.ShouldBeNull();
        replacement.OwningSlot.ShouldBeNull();
        replacement.Theme.ShouldBeNull();
        owner.Pending.ShouldBe(Invalidation.None);
        original.Pending.ShouldBe(Invalidation.None);
        replacement.Pending.ShouldBe(Invalidation.None);
        original.DetachedCalls.ShouldBe(0);
        replacement.AttachedCalls.ShouldBe(0);
        originalNotifications.ShouldBeEmpty();
        replacementNotifications.ShouldBeEmpty();
    }

    /// <summary>Verifies removal preserves old ambient resolution and strengthens committed invalidation.</summary>
    [Fact]
    public void Remove_WhenTransparentStyledChildLosesAmbientFace_InvalidatesAndPublishesExactAppearance()
    {
        var parentForeground = Color.Rgb(1, 2, 3);
        var theme = new Theme();
        theme.Freeze();
        var child = new StyledProbe { Style = ButtonStyle.Standard };
        var detached = new StyledProbe { Style = ButtonStyle.Standard };
        var expectedDetachedFace = detached.ActualFace;
        var parent = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: parentForeground)
        };
        parent.Children.Add(child);
        parent.PropagateTheme(theme);
        child.ActualFace.Foreground.Literal.ShouldBe(parentForeground);
        expectedDetachedFace.Foreground.Literal.ShouldNotBe(parentForeground);
        var notifications = new List<string?>();
        child.PropertyChanged += (_, eventArgs) =>
        {
            child.Parent.ShouldBeNull();
            child.Theme.ShouldBeNull();
            child.ActualFace.ShouldBe(expectedDetachedFace);
            child.Pending.ShouldBe(Invalidation.Render);
            notifications.Add(eventArgs.PropertyName);
        };
        child.Clear(Invalidation.All);

        var removed = parent.Children.Remove(child);

        removed.ShouldBeTrue();
        child.ActualFace.ShouldBe(expectedDetachedFace);
        child.Pending.ShouldBe(Invalidation.Render);
        notifications.ShouldBe([nameof(ControlBase.Theme), nameof(ControlBase.ActualFace)]);
        notifications.ShouldNotContain(nameof(StyledProbe.ActualStyle));
    }

    /// <summary>Verifies detach and attach snapshots preserve true old and new ambient ownership trees.</summary>
    [Fact]
    public void Reparent_WhenTransparentChildInheritsFaces_PublishesBothCommittedAmbientTransitions()
    {
        var firstForeground = Color.Rgb(1, 2, 3);
        var secondForeground = Color.Rgb(4, 5, 6);
        var theme = new Theme();
        theme.Freeze();
        var child = new ProbeControl();
        var first = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: firstForeground)
        };
        first.Children.Add(child);
        first.PropagateTheme(theme);
        var actualFaces = new List<Color>();
        child.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.ActualFace))
            {
                actualFaces.Add(child.ActualFace.Foreground.Literal);
            }
        };

        _ = first.Children.Remove(child);
        var detachedForeground = child.ActualFace.Foreground.Literal;
        var second = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: secondForeground)
        };
        second.PropagateTheme(theme);
        second.Children.Add(child);

        child.Parent.ShouldBeSameAs(second);
        actualFaces.ShouldBe([detachedForeground, secondForeground]);
        detachedForeground.ShouldNotBe(firstForeground);
        detachedForeground.ShouldNotBe(secondForeground);
    }

    /// <summary>Verifies slot impact is pending before the committed slot notification runs.</summary>
    [Fact]
    public void Add_WhenSlotPublishesChange_InvalidatesBeforeNotification()
    {
        var owner = new ProbeOwnedControl();
        new LayoutEngine().Layout(owner, new Size(4, 1));
        (owner.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
        var pendingDuringNotification = Invalidation.None;
        owner.PrimaryChanging = control =>
            pendingDuringNotification = control.Pending & Invalidation.Measure;

        owner.AddPrimary(new ProbeControl());

        pendingDuringNotification.ShouldBe(Invalidation.Measure);
    }

    /// <summary>Verifies same-role slots remain independent and traverse in registration order.</summary>
    [Fact]
    public void Slots_WhenOwnerRegistersSameRoleTwice_RemainDistinctAndOrdered()
    {
        var owner = new ProbeOwnedControl(primaryCapacity: 2);
        var first = new ProbeControl();
        var second = new ProbeControl();
        var part = new ProbeControl();

        owner.AddPrimary(first);
        owner.AddPrimary(second);
        owner.AddSecondary(part);

        owner.PrimaryCount.ShouldBe(2);
        owner.SecondaryCount.ShouldBe(1);
        owner.GetOwnedOrder().ShouldBe([first, second, part]);
        first.Parent.ShouldBeSameAs(owner);
        part.Parent.ShouldBeSameAs(owner);
        owner.PrimaryOptions.Role.ShouldBe(OwnedControlRole.FrameworkPart);
        owner.PrimaryOptions.PartKey.ShouldBe("primary");
        owner.PrimaryOptions.ParticipatesInNavigation.ShouldBeFalse();
    }

    /// <summary>Verifies complete batch validation preserves the original tree for every invalid candidate.</summary>
    [Fact]
    public async Task ReplaceAll_WhenAnyCandidateIsInvalid_PreservesCompleteOldTreeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl(primaryCapacity: 2);
        var other = new ProbeOwnedControl();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var crossOwned = new ProbeControl();
        var disposed = new ProbeControl();
        var attached = new ProbeControl();
        owner.ReplaceAllPrimary([first, second]);
        other.AddPrimary(crossOwned);
        disposed.Dispose();
        await dispatcher.InvokeAsync(
            () => attached.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<ArgumentNullException>(() => owner.ReplaceAllPrimary([new ProbeControl(), null!]));
        _ = Should.Throw<ArgumentException>(() => owner.ReplaceAllPrimary([crossOwned]));
        _ = Should.Throw<ObjectDisposedException>(() => owner.ReplaceAllPrimary([disposed]));
        _ = Should.Throw<ArgumentException>(() => owner.ReplaceAllPrimary([attached]));
        var duplicate = new ProbeControl();
        _ = Should.Throw<ArgumentException>(() => owner.ReplaceAllPrimary([duplicate, duplicate]));
        _ = Should.Throw<InvalidOperationException>(() =>
            owner.ReplaceAllPrimary([new ProbeControl(), new ProbeControl(), new ProbeControl()]));

        owner.PrimaryCount.ShouldBe(2);
        owner.PrimaryAt(0).ShouldBeSameAs(first);
        owner.PrimaryAt(1).ShouldBeSameAs(second);
        first.Parent.ShouldBeSameAs(owner);
        second.Parent.ShouldBeSameAs(owner);
        crossOwned.Parent.ShouldBeSameAs(other);
    }

    /// <summary>Verifies cycles, indexes, and per-slot capacity reject before ownership mutation.</summary>
    [Fact]
    public void Mutation_WhenCandidateOrIndexIsInvalid_PreservesCompleteOldTree()
    {
        var root = new ProbeOwnedControl(primaryCapacity: 1);
        var branch = new ProbeOwnedControl();
        var child = new ProbeControl();
        root.AddPrimary(branch);
        branch.AddPrimary(child);

        _ = Should.Throw<ArgumentException>(() => branch.AddSecondary(root));
        _ = Should.Throw<InvalidOperationException>(() => root.AddPrimary(new ProbeControl()));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => branch.InsertPrimary(5, new ProbeControl()));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => branch.ReplacePrimary(5, new ProbeControl()));

        root.PrimaryAt(0).ShouldBeSameAs(branch);
        branch.PrimaryAt(0).ShouldBeSameAs(child);
        branch.Parent.ShouldBeSameAs(root);
        child.Parent.ShouldBeSameAs(branch);
    }

    /// <summary>Verifies manager cleanup observes the old tree and lifecycle observes the complete new tree.</summary>
    [Fact]
    public async Task Replace_WhenManagersRelease_NotificationsSeeOldTreeThenLifecycleSeesNewTreeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl(primaryCapacity: 1);
        var previous = new OwnershipObserverControl { IsFocusable = true };
        var replacement = new OwnershipObserverControl();
        owner.AddPrimary(previous);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);
            using PointerManager capture = new(owner);
            focus.Focus(previous).ShouldBeTrue();
            capture.Capture(previous).ShouldBeTrue();
            focus.Lost += (_, _) =>
            {
                previous.Parent.ShouldBeSameAs(owner);
                owner.PrimaryAt(0).ShouldBeSameAs(previous);
                previous.Dispatcher.ShouldBeSameAs(dispatcher);
            };
            previous.LostPointerCapture += (_, eventArgs) =>
            {
                eventArgs.Reason.ShouldBe(PointerCaptureLossReason.Unavailable);
                previous.Parent.ShouldBeSameAs(owner);
                owner.PrimaryAt(0).ShouldBeSameAs(previous);
            };
            previous.ParentChanging = (control, oldParent, newParent) =>
            {
                oldParent.ShouldBeSameAs(owner);
                newParent.ShouldBeNull();
                control.Parent.ShouldBeNull();
                owner.PrimaryAt(0).ShouldBeSameAs(replacement);
                replacement.Parent.ShouldBeSameAs(owner);
                control.Dispatcher.ShouldBeNull();
            };
            replacement.ParentChanging = (control, oldParent, newParent) =>
            {
                oldParent.ShouldBeNull();
                newParent.ShouldBeSameAs(owner);
                control.Parent.ShouldBeSameAs(owner);
                owner.PrimaryAt(0).ShouldBeSameAs(replacement);
                previous.Parent.ShouldBeNull();
                control.Dispatcher.ShouldBeSameAs(dispatcher);
                control.InheritedFocusOwner.ShouldBeSameAs(focus);
                control.InheritedCaptureOwner.ShouldBeSameAs(capture);
            };

            owner.ReplacePrimary(0, replacement);

            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies every callback observes a fully committed attached or detached subtree.</summary>
    [Fact]
    public async Task AddAndRemove_WhenSubtreeSpansSlots_PublishesAfterWholeContextCommitsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var policy = new UnicodePolicy(Ambiguous.Wide);
        var root = new ProbeOwnedControl();
        var branch = new ProbeOwnedControl();
        var first = new OwnershipObserverControl();
        var second = new OwnershipObserverControl();
        branch.AddPrimary(first);
        branch.AddSecondary(second);
        var context = new Theme();

        await dispatcher.InvokeAsync(() =>
        {
            root.Attach(dispatcher, policy);
            root.PropagateTheme(context);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);

            first.Attaching = _ => AssertAttachedSubtree();
            second.Attaching = _ => AssertAttachedSubtree();
            first.Detaching = _ => AssertDetachedSubtree();
            second.Detaching = _ => AssertDetachedSubtree();

            root.AddPrimary(branch);
            root.RemovePrimary(branch).ShouldBeTrue();
            return;

            void AssertAttachedSubtree()
            {
                foreach (var control in new[] { first, second })
                {
                    control.Dispatcher.ShouldBeSameAs(dispatcher);
                    control.InheritedThemeValue.ShouldBeSameAs(context);
                    control.InheritedCellPolicy.ShouldBeSameAs(policy);
                    control.InheritedFocusOwner.ShouldBeSameAs(focus);
                    control.InheritedCaptureOwner.ShouldBeSameAs(capture);
                }
            }

            void AssertDetachedSubtree()
            {
                foreach (var control in new[] { first, second })
                {
                    control.Dispatcher.ShouldBeNull();
                    control.InheritedThemeValue.ShouldBeNull();
                    control.InheritedCellPolicy.ShouldBeSameAs(UnicodePolicy.Default);
                    control.InheritedFocusOwner.ShouldBeNull();
                    control.InheritedCaptureOwner.ShouldBeNull();
                }
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies publication failures cannot strand any edge or suppress later callbacks.</summary>
    [Fact]
    public async Task Clear_WhenPublicationCallbackThrows_CommitsAllEdgesAndContinuesCleanupAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var first = new OwnershipObserverControl { ThrowWhenParentClears = true, ThrowOnDetached = true };
        var second = new OwnershipObserverControl();
        owner.AddPrimary(first);
        owner.AddPrimary(second);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);

            var exception = Should.Throw<InvalidOperationException>(owner.ClearPrimary);

            exception.Message.ShouldBe("The parent callback failed.");
            owner.PrimaryCount.ShouldBe(0);
            first.Parent.ShouldBeNull();
            second.Parent.ShouldBeNull();
            first.Dispatcher.ShouldBeNull();
            second.Dispatcher.ShouldBeNull();
            first.DetachedCalls.ShouldBe(1);
            second.DetachedCalls.ShouldBe(1);
            owner.AddPrimary(new ProbeControl());
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies direct child disposal removes through its exact slot with one reason.</summary>
    [Fact]
    public async Task Dispose_WhenOwnedChildIsDisposedDirectly_RemovesThroughExactSlotOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl { IsFocusable = true };
        owner.AddPrimary(child);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);
            using PointerManager capture = new(owner);
            focus.Focus(child).ShouldBeTrue();
            capture.Capture(child).ShouldBeTrue();
            var cancelled = 0;
            child.LostPointerCapture += (_, eventArgs) =>
            {
                eventArgs.Reason.ShouldBe(PointerCaptureLossReason.Unavailable);
                cancelled++;
            };

            child.Dispose();

            owner.PrimaryCount.ShouldBe(0);
            owner.PrimaryChanges.ShouldBe(2);
            child.Parent.ShouldBeNull();
            child.IsDisposed.ShouldBeTrue();
            child.DisposingCalls.ShouldBe(1);
            child.UnavailableReasons.ShouldBe([ReleaseReason.Disposed]);
            cancelled.ShouldBe(1);
            owner.Dispose();
            child.DisposingCalls.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies owner disposal continues across slots after one child callback fails.</summary>
    [Fact]
    public void Dispose_WhenOwnerHasChildrenAcrossSlots_DisposesEveryDescendantOnce()
    {
        var owner = new ProbeOwnedControl();
        var first = new OwnershipObserverControl { ThrowOnDisposing = true };
        var second = new OwnershipObserverControl();
        owner.AddPrimary(first);
        owner.AddSecondary(second);

        var exception = Should.Throw<InvalidOperationException>(owner.Dispose);

        exception.Message.ShouldBe("The disposal callback failed.");
        owner.IsDisposed.ShouldBeTrue();
        first.IsDisposed.ShouldBeTrue();
        second.IsDisposed.ShouldBeTrue();
        first.DisposingCalls.ShouldBe(1);
        second.DisposingCalls.ShouldBe(1);
        owner.PrimaryCount.ShouldBe(0);
        owner.SecondaryCount.ShouldBe(0);
    }

    /// <summary>Verifies callbacks cannot mutate any registry participating in an active tree transaction.</summary>
    [Fact]
    public void Replace_WhenCallbackMutatesRemovedSubtreeRegistry_RejectsReentrancy()
    {
        var owner = new ProbeOwnedControl(primaryCapacity: 1);
        var previous = new ProbeOwnedControl();
        var replacement = new ProbeOwnedControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(previous);
        previous.ParentChanging = (_, _, current) =>
        {
            if (current is not null)
            {
                return;
            }

            try
            {
                previous.AddPrimary(new ProbeControl());
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }
        };

        owner.ReplacePrimary(0, replacement);

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        previous.PrimaryCount.ShouldBe(0);
        owner.PrimaryAt(0).ShouldBeSameAs(replacement);
    }

    /// <summary>Verifies disposal cannot reenter structural publication through a removed root.</summary>
    [Fact]
    public void Replace_WhenCallbackDisposesRemovedRoot_RejectsReentrancy()
    {
        var owner = new ProbeOwnedControl(primaryCapacity: 1);
        var previous = new ProbeOwnedControl();
        var replacement = new ProbeOwnedControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(previous);
        previous.ParentChanging = (_, _, current) =>
        {
            if (current is not null)
            {
                return;
            }

            try
            {
                previous.Dispose();
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }
        };

        owner.ReplacePrimary(0, replacement);

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        previous.IsDisposed.ShouldBeFalse();
        owner.PrimaryAt(0).ShouldBeSameAs(replacement);
    }

    /// <summary>Verifies a disposing hook cannot detach through the ordinary slot path.</summary>
    [Fact]
    public void Dispose_WhenDisposingHookRequestsOrdinaryRemoval_RejectsBeforeMutation()
    {
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(child);
        child.Disposing = control =>
        {
            try
            {
                _ = owner.RemovePrimary(control);
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }

            owner.PrimaryCount.ShouldBe(1);
            control.Parent.ShouldBeSameAs(owner);
        };

        child.Dispose();

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        child.UnavailableReasons.ShouldBe([ReleaseReason.Disposed]);
        child.IsDisposed.ShouldBeTrue();
        child.Parent.ShouldBeNull();
        owner.PrimaryCount.ShouldBe(0);
    }

    /// <summary>Verifies an unavailable hook cannot publish Detached during disposal.</summary>
    [Fact]
    public void Dispose_WhenUnavailableHookRequestsOrdinaryRemoval_PublishesOnlyDisposed()
    {
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(child);
        child.BecomingUnavailable = (control, reason) =>
        {
            if (reason != ReleaseReason.Disposed)
            {
                return;
            }

            try
            {
                _ = owner.RemovePrimary(control);
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }

            owner.PrimaryCount.ShouldBe(1);
            control.Parent.ShouldBeSameAs(owner);
        };

        child.Dispose();

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        child.UnavailableReasons.ShouldBe([ReleaseReason.Disposed]);
        child.IsDisposed.ShouldBeTrue();
        child.Parent.ShouldBeNull();
        owner.PrimaryCount.ShouldBe(0);
    }

    /// <summary>Verifies a removed root cannot reparent itself to an unrelated owner during publication.</summary>
    [Fact]
    public void Remove_WhenParentCallbackReparentsToUnrelatedOwner_RejectsCandidateBeforeMutation()
    {
        var owner = new ProbeOwnedControl();
        var unrelated = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(child);
        child.ParentChanging = (control, _, current) =>
        {
            if (current is not null)
            {
                return;
            }

            try
            {
                unrelated.AddPrimary(control);
            }
            catch (Exception exception)
            {
                nestedFailure = exception;
            }
        };

        owner.RemovePrimary(child).ShouldBeTrue();

        _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
        owner.PrimaryCount.ShouldBe(0);
        unrelated.PrimaryCount.ShouldBe(0);
        child.Parent.ShouldBeNull();
        child.UnavailableReasons.ShouldBe([ReleaseReason.Detached]);
    }

    /// <summary>Verifies a disposing root cannot reparent itself from its detach callback.</summary>
    [Fact]
    public async Task Dispose_WhenDetachedCallbackReparentsToUnrelatedOwner_RejectsCandidateBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var unrelated = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        Exception? nestedFailure = null;
        owner.AddPrimary(child);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            unrelated.Attach(dispatcher);
            child.Detaching = control =>
            {
                try
                {
                    unrelated.AddPrimary(control);
                }
                catch (Exception exception)
                {
                    nestedFailure = exception;
                }
            };

            try
            {
                child.Dispose();

                _ = nestedFailure.ShouldBeOfType<InvalidOperationException>();
                owner.PrimaryCount.ShouldBe(0);
                unrelated.PrimaryCount.ShouldBe(0);
                child.Parent.ShouldBeNull();
                child.UnavailableReasons.ShouldBe([ReleaseReason.Disposed]);
                child.IsDisposed.ShouldBeTrue();
            }
            finally
            {
                unrelated.Dispose();
                owner.Dispose();
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an owned control cannot attach independently from its detached owner.</summary>
    [Fact]
    public async Task Attach_WhenControlIsOwned_ThrowsBeforeContextMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        owner.AddPrimary(child);

        await dispatcher.InvokeAsync(() =>
        {
            _ = Should.Throw<InvalidOperationException>(() => child.Attach(dispatcher));

            owner.Dispatcher.ShouldBeNull();
            child.Dispatcher.ShouldBeNull();
            child.Parent.ShouldBeSameAs(owner);
            child.AttachedCalls.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an owned control cannot detach independently from its attached owner.</summary>
    [Fact]
    public async Task Detach_WhenControlIsOwned_ThrowsBeforeContextMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeOwnedControl();
        var child = new OwnershipObserverControl();
        owner.AddPrimary(child);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);

            _ = Should.Throw<InvalidOperationException>(child.Detach);

            owner.Dispatcher.ShouldBeSameAs(dispatcher);
            child.Dispatcher.ShouldBeSameAs(dispatcher);
            child.Parent.ShouldBeSameAs(owner);
            child.DetachedCalls.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Tab navigation descends eligible slots on an owner that is not a Container.</summary>
    [Fact]
    public async Task MoveNext_WhenNonContainerOwnsFocusableControls_UsesNavigationMetadataAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new TraversalOwner();
            var first = new ProbeControl { IsFocusable = true };
            var firstSlotSecond = new ProbeControl { IsFocusable = true };
            var excluded = new ProbeControl { IsFocusable = true };
            var second = new ProbeControl { IsFocusable = true };
            root.AddNormal(first);
            root.AddNormal(firstSlotSecond);
            root.AddExcluded(excluded);
            root.AddSecondary(second);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(firstSlotSecond);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(firstSlotSecond);
            focus.Focus(first).ShouldBeTrue();
            focus.MoveNext(reverse: true).ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies default render and hit traversal apply slot order, layer, and eligibility metadata.</summary>
    [Fact]
    public void RenderAndHitTest_WhenNonContainerOwnsMultipleLayers_UseRegistryOrderAndFilters()
    {
        var root = new TraversalOwner { Bounds = new Rect(0, 0, 3, 1) };
        var first = new ProbeControl { Bounds = root.Bounds, Content = "A".AsMemory() };
        var second = new ProbeControl { Bounds = root.Bounds, Content = "B".AsMemory() };
        var excluded = new ProbeControl { Bounds = root.Bounds, Content = "X".AsMemory() };
        var secondary = new ProbeControl { Bounds = root.Bounds, Content = "S".AsMemory() };
        var popupFirst = new ProbeControl { Bounds = root.Bounds, Content = "Q".AsMemory() };
        var popupLast = new ProbeControl { Bounds = root.Bounds, Content = "P".AsMemory() };
        root.AddNormal(first);
        root.AddNormal(second);
        root.AddExcluded(excluded);
        root.AddSecondary(secondary);
        root.AddPopup(popupFirst);
        root.AddPopup(popupLast);
        using Frame frame = new(new Size(3, 1));

        root.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("P");
        first.RenderCalls.ShouldBe(1);
        second.RenderCalls.ShouldBe(1);
        excluded.RenderCalls.ShouldBe(1);
        secondary.RenderCalls.ShouldBe(1);
        popupFirst.RenderCalls.ShouldBe(1);
        popupLast.RenderCalls.ShouldBe(1);
        root.HitTest(default).ShouldBeSameAs(popupLast);
        popupLast.IsHitTestVisible = false;
        popupFirst.IsHitTestVisible = false;
        root.HitTest(default).ShouldBeSameAs(secondary);
        secondary.IsHitTestVisible = false;
        root.HitTest(default).ShouldBeSameAs(second);
    }

    /// <summary>Verifies the generic owner can intentionally expose ordinary descendants outside its box.</summary>
    [Fact]
    public void HitTest_WhenNonContainerDoesNotClipChildren_ReachesOutsideNormalChild()
    {
        var root = new TraversalOwner { Bounds = new Rect(0, 0, 1, 1), ClipChildren = false };
        var child = new ProbeControl { Bounds = new Rect(1, 0, 1, 1) };
        root.AddNormal(child);

        root.HitTest(new Point(1, 0)).ShouldBeSameAs(child);
    }

    /// <summary>Verifies an ineligible intermediate owner suppresses elevated descendants without clipping them by bounds.</summary>
    [Fact]
    public void HitTestPopup_WhenIntermediateOwnerIsNotHitTestVisible_SuppressesPopupSubtree()
    {
        var root = new TraversalOwner { Bounds = new Rect(0, 0, 4, 1) };
        var intermediate = new TraversalOwner { Bounds = root.Bounds, IsHitTestVisible = false };
        var popup = new ProbeControl { Bounds = new Rect(4, 0, 1, 1) };
        intermediate.AddPopup(popup);
        root.AddNormal(intermediate);

        root.HitTest(new Point(4, 0)).ShouldNotBeSameAs(popup);
    }

    /// <summary>Verifies OwnedControlOptions rejects an undefined Role, Layer, or Impact before
    /// constructing the immutable metadata every registered slot carries.</summary>
    [Fact]
    public void OwnedControlOptions_WhenRoleLayerOrImpactIsUndefined_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new OwnedControlOptions(
            (OwnedControlRole) 99,
            OwnedControlLayer.Normal,
            participatesInHitTesting: true,
            participatesInNavigation: true,
            partKey: null,
            InvalidationImpact.None));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new OwnedControlOptions(
            OwnedControlRole.FrameworkPart,
            (OwnedControlLayer) 99,
            participatesInHitTesting: true,
            participatesInNavigation: true,
            partKey: null,
            InvalidationImpact.None));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new OwnedControlOptions(
            OwnedControlRole.FrameworkPart,
            OwnedControlLayer.Normal,
            participatesInHitTesting: true,
            participatesInNavigation: true,
            partKey: null,
            (InvalidationImpact) 99));
    }

    /// <summary>Verifies OwnedControlOptions rejects a whitespace-only part key, since a named
    /// framework part must resolve to a stable, non-empty lookup key.</summary>
    [Fact]
    public void OwnedControlOptions_WhenPartKeyIsWhitespace_Throws()
    {
        _ = Should.Throw<ArgumentException>(() => new OwnedControlOptions(
            OwnedControlRole.FrameworkPart,
            OwnedControlLayer.Normal,
            participatesInHitTesting: true,
            participatesInNavigation: true,
            partKey: "   ",
            InvalidationImpact.None));
    }

    /// <summary>Verifies every OwnedControlOptions member round-trips exactly as constructed.</summary>
    [Fact]
    public void OwnedControlOptions_WhenConstructed_ExposesEveryValidatedMember()
    {
        var options = new OwnedControlOptions(
            OwnedControlRole.FrameworkPart,
            OwnedControlLayer.Popup,
            participatesInHitTesting: true,
            participatesInNavigation: false,
            partKey: "drop-down",
            InvalidationImpact.Measure);

        options.Role.ShouldBe(OwnedControlRole.FrameworkPart);
        options.Layer.ShouldBe(OwnedControlLayer.Popup);
        options.ParticipatesInHitTesting.ShouldBeTrue();
        options.ParticipatesInNavigation.ShouldBeFalse();
        options.PartKey.ShouldBe("drop-down");
        options.Impact.ShouldBe(InvalidationImpact.Measure);
    }
}
