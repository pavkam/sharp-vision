// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies RadioGroupCoordinator's extension members directly, complementing the public
/// RadioButton.IsChecked/PerformClick coverage in RadioButtonTests with the coordinator's own
/// argument validation, roving-tab-stop resolution, and reverse group navigation.</summary>
public sealed class RadioGroupCoordinatorTests
{
    /// <summary>Verifies ClearGroup rejects a null member.</summary>
    [Fact]
    public void ClearGroup_WhenValueIsNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() =>
            ((RadioButton) null!).ClearGroup(ActivationCause.Programmatic));

    /// <summary>Verifies SelectInGroup rejects a null member.</summary>
    [Fact]
    public void SelectInGroup_WhenValueIsNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() =>
            ((RadioButton) null!).SelectInGroup(ActivationCause.Programmatic));

    /// <summary>Verifies MoveGroup rejects a null member.</summary>
    [Fact]
    public void MoveGroup_WhenValueIsNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() =>
            ((RadioButton) null!).MoveGroup(reverse: false));

    /// <summary>Verifies IsRovingTabStop rejects a null member.</summary>
    [Fact]
    public void IsRovingTabStop_WhenValueIsNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() =>
            ((RadioButton) null!).IsRovingTabStop());

    /// <summary>Verifies ClearGroup rejects an undefined activation cause.</summary>
    [Fact]
    public void ClearGroup_WhenCauseIsUndefined_Throws()
    {
        var radio = new RadioButton { IsChecked = true };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => radio.ClearGroup((ActivationCause) 99));
    }

    /// <summary>Verifies SelectInGroup rejects an undefined activation cause.</summary>
    [Fact]
    public void SelectInGroup_WhenCauseIsUndefined_Throws()
    {
        var radio = new RadioButton();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => radio.SelectInGroup((ActivationCause) 99));
    }

    /// <summary>Verifies ClearGroup on an already-unchecked member is a hard no-op that raises no
    /// events, matching the "clears one selected member" contract, which presumes one is selected.</summary>
    [Fact]
    public void ClearGroup_WhenAlreadyUnchecked_RaisesNoEvents()
    {
        var radio = new RadioButton();
        var raised = 0;
        radio.Unchecked += (_, _) => raised++;
        radio.SelectionChanged += (_, _) => raised++;

        radio.ClearGroup(ActivationCause.Keyboard);

        radio.IsChecked.ShouldBeFalse();
        raised.ShouldBe(0);
    }

    /// <summary>Verifies ClearGroup propagates the supplied cause to both raised events.</summary>
    [Fact]
    public void ClearGroup_WhenMemberIsChecked_PropagatesCauseToRaisedEvents()
    {
        var radio = new RadioButton { IsChecked = true };
        ActivationCause? uncheckedCause = null;
        ActivationCause? changedCause = null;
        radio.Unchecked += (_, args) => uncheckedCause = args.Cause;
        radio.SelectionChanged += (_, args) => changedCause = args.Cause;

        radio.ClearGroup(ActivationCause.Keyboard);

        radio.IsChecked.ShouldBeFalse();
        uncheckedCause.ShouldBe(ActivationCause.Keyboard);
        changedCause.ShouldBe(ActivationCause.Keyboard);
    }

    /// <summary>Verifies SelectInGroup propagates the supplied cause to the Checked and
    /// SelectionChanged events on a group that starts empty.</summary>
    [Fact]
    public void SelectInGroup_WhenGroupStartsEmpty_PropagatesCauseToRaisedEvents()
    {
        var radio = new RadioButton();
        ActivationCause? checkedCause = null;
        ActivationCause? changedCause = null;
        radio.Checked += (_, args) => checkedCause = args.Cause;
        radio.SelectionChanged += (_, args) => changedCause = args.Cause;

        radio.SelectInGroup(ActivationCause.Pointer);

        radio.IsChecked.ShouldBeTrue();
        checkedCause.ShouldBe(ActivationCause.Pointer);
        changedCause.ShouldBe(ActivationCause.Pointer);
    }

    /// <summary>Verifies IsRovingTabStop returns false when no member in the group is eligible for
    /// focus (every member disabled), matching MoveGroup's identical eligibility filter.</summary>
    [Fact]
    public void IsRovingTabStop_WhenNoMemberIsEligible_ReturnsFalse()
    {
        var parent = new Stack();
        var first = new RadioButton { IsEnabled = false };
        var second = new RadioButton { IsEnabled = false };
        parent.Children.Add(first);
        parent.Children.Add(second);

        first.IsRovingTabStop().ShouldBeFalse();
        second.IsRovingTabStop().ShouldBeFalse();
    }

    /// <summary>Verifies IsRovingTabStop resolves to the first eligible member when no member in
    /// the group is checked.</summary>
    [Fact]
    public void IsRovingTabStop_WhenNoMemberIsChecked_ResolvesToFirstEligibleMember()
    {
        var parent = new Stack();
        var first = new RadioButton();
        var second = new RadioButton();
        parent.Children.Add(first);
        parent.Children.Add(second);

        first.IsRovingTabStop().ShouldBeTrue();
        second.IsRovingTabStop().ShouldBeFalse();
    }

    /// <summary>Verifies IsRovingTabStop resolves to the checked member even when it is not first
    /// in group order.</summary>
    [Fact]
    public void IsRovingTabStop_WhenAMemberIsChecked_ResolvesToTheCheckedMember()
    {
        var parent = new Stack();
        var first = new RadioButton();
        var second = new RadioButton { IsChecked = true };
        parent.Children.Add(first);
        parent.Children.Add(second);

        first.IsRovingTabStop().ShouldBeFalse();
        second.IsRovingTabStop().ShouldBeTrue();
    }

    /// <summary>Verifies IsRovingTabStop excludes a checked-but-ineligible member (disabled) from
    /// resolution, falling back to the first still-eligible member.</summary>
    [Fact]
    public void IsRovingTabStop_WhenCheckedMemberIsIneligible_FallsBackToFirstEligibleMember()
    {
        var parent = new Stack();
        var first = new RadioButton { IsChecked = true };
        var second = new RadioButton();
        parent.Children.Add(first);
        parent.Children.Add(second);
        first.IsEnabled = false;

        first.IsRovingTabStop().ShouldBeFalse();
        second.IsRovingTabStop().ShouldBeTrue();
    }

    /// <summary>Verifies MoveGroup(reverse: true) wraps to the preceding eligible member, selecting
    /// and focusing it with a Keyboard cause.</summary>
    [Fact]
    public async Task MoveGroup_WhenReverseFromFirstMember_WrapsToLastEligibleMemberAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Stack();
            var first = new RadioButton();
            var second = new RadioButton();
            var third = new RadioButton();
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(first).ShouldBeTrue();
            ActivationCause? cause = null;
            third.SelectionChanged += (_, args) => cause = args.Cause;

            var moved = first.MoveGroup(reverse: true);

            moved.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(third);
            third.IsChecked.ShouldBeTrue();
            cause.ShouldBe(ActivationCause.Keyboard);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveGroup(reverse: true) steps to the immediately preceding eligible
    /// member without wrapping when one is not already at the start of group order.</summary>
    [Fact]
    public async Task MoveGroup_WhenReverseFromMiddleMember_StepsToPrecedingMemberAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Stack();
            var first = new RadioButton();
            var second = new RadioButton();
            var third = new RadioButton();
            root.Children.Add(first);
            root.Children.Add(second);
            root.Children.Add(third);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(second).ShouldBeTrue();

            var moved = second.MoveGroup(reverse: true);

            moved.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
            first.IsChecked.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveGroup wraps to the caller itself as its own sole eligible target when
    /// it is the only member in its group, still selecting it and reporting success.</summary>
    [Fact]
    public async Task MoveGroup_WhenOnlyEligibleMemberIsTheCaller_WrapsToItselfAndSelectsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Stack();
            var only = new RadioButton();
            root.Children.Add(only);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            focus.Focus(only).ShouldBeTrue();
            var changed = 0;
            only.SelectionChanged += (_, _) => changed++;

            var moved = only.MoveGroup(reverse: false);

            moved.ShouldBeTrue();
            only.IsChecked.ShouldBeTrue();
            changed.ShouldBe(1);

            // Act - a second move re-targets the same sole member, which is already checked, so
            // the underlying SelectInGroup call is now a hard no-op and raises nothing further.
            var movedAgain = only.MoveGroup(reverse: true);

            movedAgain.ShouldBeTrue();
            changed.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies MoveGroup returns false when no member in the group can accept focus.</summary>
    [Fact]
    public void MoveGroup_WhenNoMemberIsEligible_ReturnsFalse()
    {
        var parent = new Stack();
        var first = new RadioButton { IsEnabled = false };
        var second = new RadioButton { IsEnabled = false };
        parent.Children.Add(first);
        parent.Children.Add(second);

        first.MoveGroup(reverse: false).ShouldBeFalse();
    }
}
