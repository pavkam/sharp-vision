// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves detached ComboBox selection, validation, type-ahead, and keyboard-clearing
/// conditions that need no mounted surface: every branch of the SelectedItem and SelectedIndex
/// setters, the type-ahead prefix state machine, and the Delete/Backspace clearing gates.</summary>
public sealed class ComboBoxConditionTests
{
    #region SelectedItem and SelectedIndex setters

    /// <summary>Verifies assigning null through SelectedItem clears the selection and publishes the
    /// removed index exactly once.</summary>
    [Fact]
    public void SelectedItem_WhenAssignedNull_ClearsSelectionAndPublishesRemoval()
    {
        var combo = new ComboBox { Items = ["A", "B", "C"], SelectedIndex = 2 };
        var changes = new List<ListSelectionChangedEventArgs>();
        combo.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);

        combo.SelectedItem = null;

        combo.SelectedIndex.ShouldBe(-1);
        combo.SelectedItem.ShouldBeNull();
        combo.GetDropDownList().SelectedIndex.ShouldBe(-1);
        changes.Count.ShouldBe(1);
        changes[0].AddedIndexes.ToArray().ShouldBeEmpty();
        changes[0].RemovedIndexes.ToArray().ShouldBe([2]);
    }

    /// <summary>Verifies assigning an item that is not in Items resolves to no selection, as the
    /// API table documents ("assignment resolves to the matching item"), and publishes once.</summary>
    [Fact]
    public void SelectedItem_WhenAssignedMissingItem_ClearsSelectionAndPublishesOnce()
    {
        var combo = new ComboBox { Items = ["A", "B", "C"], SelectedIndex = 1 };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        combo.SelectedItem = "Z";

        combo.SelectedIndex.ShouldBe(-1);
        combo.SelectedItem.ShouldBeNull();
        changes.ShouldBe(1);
    }

    /// <summary>Verifies a missing item assigned while nothing is selected is a silent no-op.</summary>
    [Fact]
    public void SelectedItem_WhenAssignedMissingItemWhileUnselected_PublishesNothing()
    {
        var combo = new ComboBox { Items = ["A", "B"], SelectedIndex = -1 };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;
        combo.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ComboBox.SelectedIndex))
            {
                changes++;
            }
        };

        combo.SelectedItem = "Z";

        combo.SelectedIndex.ShouldBe(-1);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies duplicate items resolve to the first equal entry, using value equality
    /// rather than reference identity.</summary>
    [Fact]
    public void SelectedItem_WhenItemsContainDuplicates_SelectsFirstEqualEntry()
    {
        var combo = new ComboBox
        {
            Items = ["Same", "Other", "Same"],
            SelectedIndex = 2
        };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        combo.SelectedItem = new string(['S', 'a', 'm', 'e']);

        combo.SelectedIndex.ShouldBe(0);
        combo.SelectedItem.ShouldBe("Same");
        changes.ShouldBe(1);
    }

    /// <summary>Verifies reassigning the already-selected item raises no notification at all.</summary>
    [Fact]
    public void SelectedItem_WhenAssignedCurrentItem_PublishesNothing()
    {
        var combo = new ComboBox { Items = ["A", "B"], SelectedIndex = 1 };
        var notifications = 0;
        combo.SelectionChanged += (_, _) => notifications++;
        combo.PropertyChanged += (_, _) => notifications++;

        combo.SelectedItem = "B";

        combo.SelectedIndex.ShouldBe(1);
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies SelectedItem resolves null entries inside Items: assigning null always
    /// means "no selection", never "select the null item".</summary>
    [Fact]
    public void SelectedItem_WhenItemsContainNullAndNullIsAssigned_ClearsInsteadOfSelectingNullEntry()
    {
        var combo = new ComboBox { Items = [null, "B"], SelectedIndex = 1 };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        combo.SelectedItem = null;

        combo.SelectedIndex.ShouldBe(-1);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies every out-of-range SelectedIndex is rejected before any mutation or
    /// publication, including on an empty list.</summary>
    [Theory]
    [InlineData(new[] { "A", "B", "C" }, 1, -2)]
    [InlineData(new[] { "A", "B", "C" }, 1, 3)]
    [InlineData(new[] { "A", "B", "C" }, 1, int.MaxValue)]
    [InlineData(new string[0], -1, 0)]
    public void SelectedIndex_WhenOutOfRange_ThrowsBeforeMutation(string[] items, int initial, int requested)
    {
        var combo = new ComboBox { Items = items, SelectedIndex = initial };
        var notifications = 0;
        combo.SelectionChanged += (_, _) => notifications++;
        combo.PropertyChanged += (_, _) => notifications++;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => combo.SelectedIndex = requested);

        combo.SelectedIndex.ShouldBe(initial);
        combo.SelectedItem.ShouldBe(initial < 0 ? null : items[initial]);
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies assigning the current SelectedIndex is a silent no-op.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void SelectedIndex_WhenAssignedSameValue_PublishesNothing(int index)
    {
        var combo = new ComboBox { Items = ["A", "B"], SelectedIndex = index };
        var notifications = 0;
        combo.SelectionChanged += (_, _) => notifications++;
        combo.PropertyChanged += (_, _) => notifications++;

        combo.SelectedIndex = index;

        notifications.ShouldBe(0);
    }

    /// <summary>Verifies replacing Items while the selection stays in range publishes Items but
    /// not SelectionChanged, and the closed face text follows the new item at that index.</summary>
    [Fact]
    public void Items_WhenReplacedWithSelectionInRange_PublishesItemsWithoutSelectionChanged()
    {
        var combo = new ComboBox { Items = ["A", "B", "C"], SelectedIndex = 1 };
        var selectionChanges = 0;
        var itemsChanges = 0;
        combo.SelectionChanged += (_, _) => selectionChanges++;
        combo.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ComboBox.Items))
            {
                itemsChanges++;
            }
        };

        combo.Items = ["X", "Y"];

        combo.SelectedIndex.ShouldBe(1);
        combo.SelectedItem.ShouldBe("Y");
        selectionChanges.ShouldBe(0);
        itemsChanges.ShouldBe(1);
    }

    /// <summary>Verifies replacing Items with an empty list clears a previous selection and reports
    /// the removed index.</summary>
    [Fact]
    public void Items_WhenReplacedWithEmptyList_ClearsSelectionAndPublishesRemoval()
    {
        var combo = new ComboBox { Items = ["A", "B"], SelectedIndex = 1 };
        var changes = new List<ListSelectionChangedEventArgs>();
        combo.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);

        combo.Items = [];

        combo.SelectedIndex.ShouldBe(-1);
        combo.SelectedItem.ShouldBeNull();
        changes.Count.ShouldBe(1);
        changes[0].RemovedIndexes.ToArray().ShouldBe([1]);
        changes[0].AddedIndexes.ToArray().ShouldBeEmpty();
    }

    /// <summary>Verifies a SelectionChanged handler that replaces Items during the auto-select of a
    /// fresh assignment leaves the newest item domain and a consistent in-range selection.</summary>
    [Fact]
    public void Items_WhenSelectionChangedHandlerReplacesItems_KeepsNewestDomainConsistent()
    {
        var combo = new ComboBox();
        var reentered = false;
        combo.SelectionChanged += (_, _) =>
        {
            if (!reentered)
            {
                reentered = true;
                combo.Items = ["Only"];
            }
        };

        combo.Items = ["First", "Second"];

        reentered.ShouldBeTrue();
        combo.Items.ShouldBe(["Only"]);
        combo.SelectedIndex.ShouldBe(0);
        combo.SelectedItem.ShouldBe("Only");
    }

    #endregion

    #region Type-ahead

    /// <summary>Verifies printable text while the popup is closed is left unhandled and changes
    /// nothing, matching the documented "while the popup is open" scope of type-to-select.</summary>
    [Fact]
    public void Dispatch_WhenPrintableTypedWhileClosed_LeavesSelectionAndStaysClosed()
    {
        var combo = new ComboBox { Items = ["Alpha", "Beta", "Gamma"], SelectedIndex = 0 };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        var typed = Router.Route(combo, Events.Key, CharacterKey('g'));

        typed.IsHandled.ShouldBeFalse();
        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies a repeated first letter cycles forward through every match and wraps
    /// around to the first one, falling back to the latest character once the doubled prefix has
    /// no match.</summary>
    [Fact]
    public void Dispatch_WhenSameLetterRepeats_CyclesThroughMatchesAndWraps()
    {
        var combo = new ComboBox
        {
            Items = ["Apple", "Avocado", "Banana", "Apricot"],
            SelectedIndex = 0,
            IsOpen = true
        };

        _ = Router.Route(combo, Events.Key, CharacterKey('a'));
        combo.SelectedIndex.ShouldBe(1, "Avocado is the next match after the selected Apple");

        _ = Router.Route(combo, Events.Key, CharacterKey('a'));
        combo.SelectedIndex.ShouldBe(3, "'aa' has no match so the latest 'a' searches on from Avocado");

        _ = Router.Route(combo, Events.Key, CharacterKey('a'));
        combo.SelectedIndex.ShouldBe(0, "the search wraps around to Apple");
    }

    /// <summary>Verifies successive different characters accumulate into a longer prefix that
    /// narrows the match instead of restarting from the latest character.</summary>
    [Fact]
    public void Dispatch_WhenPrefixAccumulates_NarrowsToLongestMatch()
    {
        var combo = new ComboBox
        {
            Items = ["Apple", "Avocado", "Banana", "Apricot"],
            SelectedIndex = 0,
            IsOpen = true
        };

        _ = Router.Route(combo, Events.Key, CharacterKey('a'));
        combo.SelectedIndex.ShouldBe(1);

        var second = Router.Route(combo, Events.Key, CharacterKey('p'));

        second.IsHandled.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(3, "'ap' matches Apricot, the only 'ap' item after Avocado");
    }

    /// <summary>Verifies a character with no match at all leaves the selection, is unhandled, and
    /// discards the prefix so the next character starts a fresh search.</summary>
    [Fact]
    public void Dispatch_WhenNoItemMatches_LeavesSelectionUnhandledAndResetsPrefix()
    {
        var combo = new ComboBox
        {
            Items = ["Apple", "Banana", "Cherry"],
            SelectedIndex = 0,
            IsOpen = true
        };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        var miss = Router.Route(combo, Events.Key, CharacterKey('z'));

        miss.IsHandled.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
        changes.ShouldBe(0);

        var hit = Router.Route(combo, Events.Key, CharacterKey('c'));

        hit.IsHandled.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(2, "the failed 'z' must not linger as a 'zc' prefix");
        changes.ShouldBe(1);
    }

    /// <summary>Verifies digits and punctuation participate in type-ahead exactly like letters.</summary>
    [Fact]
    public void Dispatch_WhenNonLetterCharacterIsTyped_MatchesDigitsAndPunctuation()
    {
        var combo = new ComboBox
        {
            Items = ["1st", "2nd", "#tag", "(paren)"],
            SelectedIndex = -1,
            IsOpen = true
        };

        _ = Router.Route(combo, Events.Key, CharacterKey('2'));
        combo.SelectedIndex.ShouldBe(1);

        _ = Router.Route(combo, Events.Key, CharacterKey('#'));
        combo.SelectedIndex.ShouldBe(2);

        _ = Router.Route(combo, Events.Key, CharacterKey('('));
        combo.SelectedIndex.ShouldBe(3);
    }

    /// <summary>Verifies accented and wide (CJK) characters match case-insensitively through the
    /// same ordinal-ignore-case prefix search.</summary>
    [Fact]
    public void Dispatch_WhenAccentedOrWideCharacterIsTyped_MatchesCaseInsensitively()
    {
        var combo = new ComboBox
        {
            Items = ["Éclair", "日本語", "eagle"],
            SelectedIndex = -1,
            IsOpen = true
        };

        _ = Router.Route(combo, Events.Key, CharacterKey('é'));
        combo.SelectedIndex.ShouldBe(0, "lower-case é matches the upper-case É prefix");

        _ = Router.Route(combo, Events.Key, CharacterKey('日'));
        combo.SelectedIndex.ShouldBe(1);

        _ = Router.Route(combo, Events.Key, CharacterKey('E'));
        combo.SelectedIndex.ShouldBe(2, "upper-case E matches 'eagle' but not the accented É");
    }

    /// <summary>Verifies a type-ahead match commits the selection immediately (publishing
    /// SelectionChanged) and Escape afterwards keeps it rather than restoring the opening row, since
    /// the typed commit supersedes the opening snapshot.</summary>
    [Fact]
    public void Dispatch_WhenTypeAheadCommitsThenEscape_KeepsTypedSelection()
    {
        var combo = new ComboBox
        {
            Items = ["Alpha", "Beta", "Gamma"],
            SelectedIndex = 0,
            IsOpen = true
        };
        var changes = new List<int[]>();
        combo.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs.AddedIndexes.ToArray());

        _ = Router.Route(combo, Events.Key, CharacterKey('g'));
        combo.SelectedIndex.ShouldBe(2);
        changes.ShouldBe([[2]]);

        var escape = Router.Route(combo, Events.Key, Key(Code.Escape));

        escape.IsHandled.ShouldBeTrue();
        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(2);
        combo.GetDropDownList().SelectedIndex.ShouldBe(2);
        changes.Count.ShouldBe(1);
    }

    /// <summary>Verifies type-ahead on an empty list is unhandled instead of dividing by the item
    /// count.</summary>
    [Fact]
    public void Dispatch_WhenTypedWithNoItems_IsUnhandled()
    {
        var combo = new ComboBox { IsOpen = true };

        var typed = Should.NotThrow(() => Router.Route(combo, Events.Key, CharacterKey('a')));

        typed.IsHandled.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies TextSelector output, not ToString, is what a prefix is matched against, so
    /// items whose ToString would match are skipped when the projection says otherwise.</summary>
    [Fact]
    public void Dispatch_WhenTextSelectorHidesToStringPrefix_SkipsThatItem()
    {
        var combo = new ComboBox
        {
            Items = ["Apple", "Banana"],
            TextSelector = static item => item is "Apple" ? "Zed" : (string) item!,
            SelectedIndex = -1,
            IsOpen = true
        };

        var apple = Router.Route(combo, Events.Key, CharacterKey('a'));

        apple.IsHandled.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(-1);

        _ = Router.Route(combo, Events.Key, CharacterKey('z'));
        combo.SelectedIndex.ShouldBe(0);
    }

    #endregion

    #region Delete and Backspace

    /// <summary>Verifies Delete and Backspace while the popup is open clear the committed
    /// selection, clear the list's own selection, consume the key, and leave the popup open.</summary>
    [Theory]
    [InlineData(Code.Delete)]
    [InlineData(Code.Backspace)]
    public void Dispatch_WhenClearingKeyPressedWhileOpen_ClearsSelectionAndKeepsPopupOpen(Code code)
    {
        var combo = new ComboBox { Items = ["Alpha", "Beta"], SelectedIndex = 1, IsOpen = true };
        var changes = new List<ListSelectionChangedEventArgs>();
        combo.SelectionChanged += (_, eventArgs) => changes.Add(eventArgs);

        var cleared = Router.Route(combo, Events.Key, Key(code));

        cleared.IsHandled.ShouldBeTrue();
        combo.IsOpen.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(-1);
        combo.GetDropDownList().SelectedIndex.ShouldBe(-1);
        changes.Count.ShouldBe(1);
        changes[0].RemovedIndexes.ToArray().ShouldBe([1]);
    }

    /// <summary>Verifies AllowNull=false leaves Delete and Backspace unhandled whether the popup is
    /// open or closed, so an ancestor can still observe the stroke.</summary>
    [Theory]
    [InlineData(Code.Delete, false)]
    [InlineData(Code.Delete, true)]
    [InlineData(Code.Backspace, false)]
    [InlineData(Code.Backspace, true)]
    public void Dispatch_WhenAllowNullIsFalse_LeavesClearingKeyUnhandled(Code code, bool open)
    {
        var combo = new ComboBox { Items = ["Alpha", "Beta"], SelectedIndex = 1, AllowNull = false, IsOpen = open };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        var routed = Router.Route(combo, Events.Key, Key(code));

        routed.IsHandled.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(1);
        combo.IsOpen.ShouldBe(open);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies a clearing key with nothing selected is unhandled and publishes nothing.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispatch_WhenClearingKeyPressedWhileUnselected_IsUnhandled(bool open)
    {
        var combo = new ComboBox { Items = ["Alpha"], SelectedIndex = -1, IsOpen = open };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        var routed = Router.Route(combo, Events.Key, Key(Code.Backspace));

        routed.IsHandled.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(-1);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies a clearing key release (not a press) does nothing.</summary>
    [Fact]
    public void Dispatch_WhenDeleteIsReleased_DoesNotClear()
    {
        var combo = new ComboBox { Items = ["Alpha"], SelectedIndex = 0 };

        var routed = Router.Route(combo, Events.Key, Key(Code.Delete, KeyAction.Release));

        routed.IsHandled.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies a Delete that clears the selection also discards an accumulated type-ahead
    /// prefix, so the next typed character starts a fresh search.</summary>
    [Fact]
    public void Dispatch_WhenDeleteClearsAfterTypeAhead_ResetsPrefix()
    {
        var combo = new ComboBox
        {
            Items = ["Apple", "Apricot", "Banana"],
            SelectedIndex = -1,
            IsOpen = true
        };

        _ = Router.Route(combo, Events.Key, CharacterKey('a'));
        combo.SelectedIndex.ShouldBe(0);
        _ = Router.Route(combo, Events.Key, Key(Code.Delete));
        combo.SelectedIndex.ShouldBe(-1);

        _ = Router.Route(combo, Events.Key, CharacterKey('b'));

        combo.SelectedIndex.ShouldBe(2, "a lingering 'ab' prefix would have matched nothing");
    }

    #endregion

    #region Closed-state keys that must not open or move

    /// <summary>Verifies closed navigation with Shift, Alt, or Control leaves the selection and
    /// the popup untouched and unhandled, matching the scalar-navigation modifier policy.</summary>
    [Theory]
    [InlineData(Code.Down, Modifiers.Shift)]
    [InlineData(Code.Down, Modifiers.Alt)]
    [InlineData(Code.Down, Modifiers.Control)]
    [InlineData(Code.Up, Modifiers.Alt)]
    [InlineData(Code.Home, Modifiers.Control)]
    [InlineData(Code.End, Modifiers.Shift)]
    public void Dispatch_WhenClosedNavigationCarriesModifier_LeavesSelectionAndStaysClosed(
        Code code,
        Modifiers modifiers)
    {
        var combo = new ComboBox { Items = ["Zero", "One", "Two"], SelectedIndex = 1 };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        var routed = Router.Route(combo, Events.Key, Key(code, modifiers));

        routed.IsHandled.ShouldBeFalse();
        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(1);
        changes.ShouldBe(0);
    }

    /// <summary>Verifies lock-state modifiers (Caps Lock, Num Lock) still allow closed navigation.</summary>
    [Theory]
    [InlineData(Modifiers.CapsLock)]
    [InlineData(Modifiers.NumLock)]
    [InlineData(Modifiers.CapsLock | Modifiers.NumLock)]
    public void Dispatch_WhenClosedNavigationCarriesLockState_StillCommits(Modifiers modifiers)
    {
        var combo = new ComboBox { Items = ["Zero", "One", "Two"], SelectedIndex = 1 };

        var routed = Router.Route(combo, Events.Key, Key(Code.Down, modifiers));

        routed.IsHandled.ShouldBeTrue();
        combo.SelectedIndex.ShouldBe(2);
        combo.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies keys that other toolkits bind to "open" (F4, Alt+Down) are not bound here:
    /// they stay unhandled and the popup stays closed.</summary>
    [Theory]
    [InlineData(Code.F4, Modifiers.None)]
    [InlineData(Code.Down, Modifiers.Alt)]
    public void Dispatch_WhenUnboundOpenKeyIsPressed_StaysClosedAndUnhandled(Code code, Modifiers modifiers)
    {
        var combo = new ComboBox { Items = ["Zero", "One"], SelectedIndex = 0 };

        var routed = Router.Route(combo, Events.Key, Key(code, modifiers));

        routed.IsHandled.ShouldBeFalse();
        combo.IsOpen.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies a navigation key release never commits a closed-state move.</summary>
    [Fact]
    public void Dispatch_WhenClosedNavigationKeyIsReleased_DoesNotCommit()
    {
        var combo = new ComboBox { Items = ["Zero", "One"], SelectedIndex = 0 };

        var routed = Router.Route(combo, Events.Key, Key(Code.Down, KeyAction.Release));

        routed.IsHandled.ShouldBeFalse();
        combo.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Verifies a closed field at the last item ignores Down/End and at the first item
    /// ignores Up/Home without publishing, instead of wrapping.</summary>
    [Theory]
    [InlineData(Code.Down, 2)]
    [InlineData(Code.End, 2)]
    [InlineData(Code.Right, 2)]
    [InlineData(Code.Up, 0)]
    [InlineData(Code.Home, 0)]
    [InlineData(Code.Left, 0)]
    public void Dispatch_WhenClosedNavigationHitsAnEndpoint_DoesNotWrapOrPublish(Code code, int start)
    {
        var combo = new ComboBox { Items = ["Zero", "One", "Two"], SelectedIndex = start };
        var changes = 0;
        combo.SelectionChanged += (_, _) => changes++;

        _ = Router.Route(combo, Events.Key, Key(code));

        combo.SelectedIndex.ShouldBe(start);
        changes.ShouldBe(0);
        combo.IsOpen.ShouldBeFalse();
    }

    #endregion

    #region Presentation edge cases

    /// <summary>Verifies a field too narrow for any label still draws its frame and the indicator
    /// inside the content box without spilling over the border.</summary>
    [Theory]
    [InlineData(4, "┏━━┓\n┃ ▼┃\n┗━━┛")]
    [InlineData(3, "┏━┓\n┃▼┃\n┗━┛")]
    public void Render_WhenWidthIsTiny_DrawsFrameAndIndicatorOnly(int width, string expected)
    {
        var combo = new ComboBox
        {
            Items = ["Alpha"],
            SelectedIndex = 0,
            Width = Length.Cells(width),
            Height = Length.Cells(3)
        };
        var size = new Size(width, 3);
        new LayoutEngine().Layout(combo, size);
        using Frame frame = new(size);

        combo.Render(frame.Canvas);

        var rows = expected.Split('\n');

        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // An untouched frame cell reports empty text; the expectation spells it as a blank.
                var actual = FrameOracle.Get(frame, new Point(x, y));
                (actual.Length == 0 ? " " : actual).ShouldBe(rows[y][x].ToString(), $"cell ({x},{y})");
            }
        }
    }

    /// <summary>Verifies a one-row field (no room for a border) renders the label and indicator
    /// on that row without throwing.</summary>
    [Fact]
    public void Render_WhenHeightIsOneRow_DoesNotThrow()
    {
        var combo = new ComboBox
        {
            Items = ["Alpha"],
            SelectedIndex = 0,
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        var size = new Size(10, 1);
        new LayoutEngine().Layout(combo, size);
        using Frame frame = new(size);

        Should.NotThrow(() => combo.Render(frame.Canvas));
    }

    /// <summary>Verifies a wide-character label is clipped at the field box so the indicator cell
    /// is never overdrawn by a double-width grapheme.</summary>
    [Fact]
    public void Render_WhenLabelIsWide_ClipsBeforeTheIndicator()
    {
        var combo = new ComboBox
        {
            Items = ["日本語テキスト"],
            SelectedIndex = 0,
            Width = Length.Cells(9),
            Height = Length.Cells(3)
        };
        var size = new Size(9, 3);
        new LayoutEngine().Layout(combo, size);
        using Frame frame = new(size);

        combo.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("日");
        FrameOracle.Get(frame, new Point(3, 1)).ShouldBe("本");
        FrameOracle.Get(frame, new Point(7, 1)).ShouldBe("▼");
        FrameOracle.Get(frame, new Point(8, 1)).ShouldBe("┃");
    }

    /// <summary>Verifies the drop-down list's ScrollBars/ShowScrollBars/RowHeight/PopupChrome
    /// setters publish exactly once for a change and never for a same-value reassignment.</summary>
    [Fact]
    public void ForwardedListProperties_WhenReassignedSameValue_PublishNothing()
    {
        var combo = new ComboBox
        {
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always,
            RowHeight = Length.Cells(2),
            PopupChrome = new PopupChrome { Border = ControlStyle.NoBorder }
        };
        var published = new List<string?>();
        combo.PropertyChanged += (_, eventArgs) => published.Add(eventArgs.PropertyName);

        combo.ScrollBars = ScrollBars.Both;
        combo.ShowScrollBars = ShowScrollBars.Always;
        combo.RowHeight = Length.Cells(2);
        combo.PopupChrome = new PopupChrome { Border = ControlStyle.NoBorder };

        published.ShouldBeEmpty();

        combo.ScrollBars = ScrollBars.Vertical;
        combo.ShowScrollBars = ShowScrollBars.Never;
        combo.RowHeight = Length.Cells(3);
        combo.ResetPopupChrome();

        published.ShouldBe([
            nameof(ComboBox.ScrollBars),
            nameof(ComboBox.ShowScrollBars),
            nameof(ComboBox.RowHeight),
            nameof(ComboBox.PopupChrome)
        ]);
        combo.GetDropDownList().ScrollBars.ShouldBe(ScrollBars.Vertical);
        combo.GetDropDownList().ShowScrollBars.ShouldBe(ShowScrollBars.Never);
        combo.GetDropDownList().RowHeight.ShouldBe(Length.Cells(3));
        combo.PopupChrome.ShouldBe(default);
    }

    /// <summary>Verifies Placeholder rejects null before mutation and publishes a change once.</summary>
    [Fact]
    public void Placeholder_WhenNullOrChanged_ValidatesAndPublishesOnce()
    {
        var combo = new ComboBox();
        var published = 0;
        combo.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ComboBox.Placeholder))
            {
                published++;
            }
        };

        _ = Should.Throw<ArgumentNullException>(() => combo.Placeholder = null!);
        combo.Placeholder.ShouldBe("Select…");

        combo.Placeholder = "Pick";
        combo.Placeholder = "Pick";

        combo.Placeholder.ShouldBe("Pick");
        published.ShouldBe(1);
    }

    /// <summary>Verifies disposal clears every public event so no late subscriber observes a
    /// post-disposal notification, and public mutation afterwards throws.</summary>
    [Fact]
    public void Dispose_WhenCalled_ClearsEventsAndRejectsMutation()
    {
        var combo = new ComboBox { Items = ["A"], SelectedIndex = 0 };
        var raised = 0;
        combo.SelectionChanged += (_, _) => raised++;
        combo.DropDownOpened += (_, _) => raised++;
        combo.DropDownClosed += (_, _) => raised++;

        combo.Dispose();
        combo.Dispose();

        raised.ShouldBe(0);
        _ = Should.Throw<ObjectDisposedException>(() => combo.SelectedIndex = -1);
        _ = Should.Throw<ObjectDisposedException>(() => combo.Items = ["B"]);
        _ = Should.Throw<ObjectDisposedException>(() => combo.IsOpen = true);
    }

    #endregion

    private static KeyEventArgs Key(Code code, KeyAction action = KeyAction.Press) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        Modifiers.None,
        action));

    private static KeyEventArgs Key(Code code, Modifiers modifiers) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        modifiers,
        KeyAction.Press));

    private static KeyEventArgs CharacterKey(char character) => new(new Stroke(
        Code.Character,
        new Rune(character),
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));
}
