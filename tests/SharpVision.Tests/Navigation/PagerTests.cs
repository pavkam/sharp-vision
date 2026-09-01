// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies Pager state invariants and ordered change publication.</summary>
public sealed class PagerTests
{
    /// <summary>Verifies the documented empty state and focus defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var theme = new Theme();
        theme.Freeze();
        var pager = new Pager();
        pager.SetTheme(theme);

        pager.PageCount.ShouldBe(0);
        pager.PageIndex.ShouldBe(-1);
        pager.MaximumVisiblePages.ShouldBe(5);
        pager.Style.ShouldBeNull();
        pager.ActualStyle.ShouldBe(PagerStyle.Default);
        pager.CanFocus.ShouldBeTrue();
        pager.CanTabStop.ShouldBeFalse();
        pager.TabNavigation.ShouldBe(TabNavigation.None);
    }

    /// <summary>Verifies immutable page-change payloads preserve a valid transition.</summary>
    [Fact]
    public void PageChangedEventArgs_WhenCreated_PreservesTransition()
    {
        var eventArgs = new PageChangedEventArgs(1, 2, ActivationCause.Pointer);

        eventArgs.PreviousPageIndex.ShouldBe(1);
        eventArgs.CurrentPageIndex.ShouldBe(2);
        eventArgs.Cause.ShouldBe(ActivationCause.Pointer);
    }

    /// <summary>Verifies invalid payloads fail before any observable object exists.</summary>
    [Theory]
    [InlineData(-2, 0, ActivationCause.Programmatic)]
    [InlineData(0, -2, ActivationCause.Programmatic)]
    [InlineData(0, 0, ActivationCause.Programmatic)]
    [InlineData(0, 1, (ActivationCause) 99)]
    public void PageChangedEventArgs_WhenTransitionIsInvalid_Throws(
        int previous,
        int current,
        ActivationCause cause) =>
        _ = Should.Throw<ArgumentException>(() => new PageChangedEventArgs(previous, current, cause));

    /// <summary>Verifies invalid assignments preserve the complete page invariant.</summary>
    [Fact]
    public void Properties_WhenAssignmentIsInvalid_PreserveState()
    {
        var pager = new Pager { PageCount = 4, PageIndex = 2, MaximumVisiblePages = 3 };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => pager.PageCount = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => pager.PageIndex = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => pager.PageIndex = 4);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => pager.MaximumVisiblePages = 0);

        pager.PageCount.ShouldBe(4);
        pager.PageIndex.ShouldBe(2);
        pager.MaximumVisiblePages.ShouldBe(3);
    }

    /// <summary>Verifies count repair stages both values before publishing ordered callbacks.</summary>
    [Fact]
    public void PageCount_WhenShrunk_ObserversSeeValidStateAndOrderedNotifications()
    {
        var pager = new Pager { PageCount = 10, PageIndex = 9 };
        List<string> observations = [];
        pager.PropertyChanged += (_, eventArgs) =>
        {
            (pager.PageCount == 0
                ? pager.PageIndex == -1
                : pager.PageIndex >= 0 && pager.PageIndex < pager.PageCount).ShouldBeTrue();
            observations.Add(eventArgs.PropertyName!);
        };
        pager.PageChanged += (_, eventArgs) => observations.Add(
            $"{eventArgs.PreviousPageIndex}>{eventArgs.CurrentPageIndex}:{eventArgs.Cause}");

        pager.PageCount = 3;

        pager.PageIndex.ShouldBe(2);
        observations.ShouldBe([
            nameof(Pager.PageCount),
            nameof(Pager.PageIndex),
            "9>2:Programmatic"
        ]);
    }

    /// <summary>Verifies count transitions establish and remove the exact empty-state sentinel.</summary>
    [Fact]
    public void PageCount_WhenCrossingEmptyBoundary_RepairsPageIndex()
    {
        var pager = new Pager { PageCount = 2 };

        pager.PageIndex.ShouldBe(0);
        pager.CanTabStop.ShouldBeTrue();

        pager.PageCount = 0;
        pager.PageIndex.ShouldBe(-1);
        pager.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies a page transition returns whether it committed and raises exactly once.</summary>
    [Fact]
    public void ChangePage_WhenTargetChanges_CommitsExactlyOnce()
    {
        var pager = new Pager { PageCount = 3 };
        var changes = new List<PageChangedEventArgs>();
        pager.PageChanged += (_, eventArgs) => changes.Add(eventArgs);

        pager.ChangePage(1).ShouldBeTrue();
        pager.ChangePage(1).ShouldBeFalse();

        pager.PageIndex.ShouldBe(1);
        changes.Count.ShouldBe(1);
        changes[0].Cause.ShouldBe(ActivationCause.Programmatic);
    }

    /// <summary>Verifies a newer commit from a property observer owns the typed event stream.</summary>
    [Fact]
    public void PageIndex_WhenPropertyObserverCommitsNewerIndex_SuppressesStalePageChanged()
    {
        var pager = new Pager { PageCount = 4 };
        List<int> changes = [];
        pager.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Pager.PageIndex) && pager.PageIndex == 1)
            {
                pager.PageIndex = 2;
            }
        };
        pager.PageChanged += (_, eventArgs) => changes.Add(eventArgs.CurrentPageIndex);

        pager.PageIndex = 1;

        pager.PageIndex.ShouldBe(2);
        changes.ShouldBe([2]);
    }

    /// <summary>Verifies becoming unavailable from a property observer suppresses stale typed publication.</summary>
    [Fact]
    public void PageIndex_WhenPropertyObserverHidesPager_SuppressesStalePageChanged()
    {
        var pager = new Pager { PageCount = 4 };
        var changes = 0;
        pager.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Pager.PageIndex))
            {
                pager.Visibility = Visibility.Hidden;
            }
        };
        pager.PageChanged += (_, _) => changes++;

        pager.PageIndex = 1;

        pager.PageIndex.ShouldBe(1);
        pager.Visibility.ShouldBe(Visibility.Hidden);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies scalar navigation keys share the same page transition path.</summary>
    [Fact]
    public void Dispatch_WhenNavigationKeysArrive_ChangesExpectedPages()
    {
        var pager = new Pager { PageCount = 5, PageIndex = 2 };

        _ = Key(pager, Code.Left);
        pager.PageIndex.ShouldBe(1);
        _ = Key(pager, Code.Up);
        pager.PageIndex.ShouldBe(0);
        _ = Key(pager, Code.End);
        pager.PageIndex.ShouldBe(4);
        _ = Key(pager, Code.PageUp);
        pager.PageIndex.ShouldBe(3);
        _ = Key(pager, Code.Home);
        pager.PageIndex.ShouldBe(0);
        _ = Key(pager, Code.PageDown);
        pager.PageIndex.ShouldBe(1);
        _ = Key(pager, Code.Down);
        pager.PageIndex.ShouldBe(2);
        _ = Key(pager, Code.Right);
        pager.PageIndex.ShouldBe(3);
    }

    /// <summary>Verifies modified navigation and activation keys remain available to ancestors.</summary>
    [Theory]
    [InlineData(Code.Right)]
    [InlineData(Code.Enter)]
    [InlineData(Code.Character)]
    public void Dispatch_WhenKeyIsNotScalarNavigation_RemainsUnhandled(Code code)
    {
        var pager = new Pager { PageCount = 3 };
        var character = code == Code.Character ? new Rune(' ') : (Rune?) null;
        var key = new KeyEventArgs(new Stroke(
            code,
            character,
            nativeCode: 0,
            code == Code.Right ? Modifiers.Shift : Modifiers.None,
            KeyAction.Press));

        _ = Router.Route(pager, Events.Key, key);

        key.IsHandled.ShouldBeFalse();
        pager.PageIndex.ShouldBe(0);
    }

    /// <summary>Verifies one-page controls accept no keyboard transition.</summary>
    [Fact]
    public void Dispatch_WhenOnlyOnePage_RemainsUnhandled()
    {
        var pager = new Pager { PageCount = 1 };
        var key = Key(pager, Code.Right);

        key.IsHandled.ShouldBeFalse();
        pager.PageIndex.ShouldBe(0);
    }

    private static KeyEventArgs Key(Pager pager, Code code)
    {
        var key = new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));
        _ = Router.Route(pager, Events.Key, key);
        return key;
    }
}
