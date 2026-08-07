// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;


/// <summary>Verifies randomized scroll, resize, selection, and mutation sequences leave a
/// virtualized ListView (RowHeight set) in the exact same observable state as an identically
/// driven eager twin - the eager instance is the independent oracle, since its realize-everything
/// behavior is already trusted and unaffected by this change.</summary>
public sealed class ListViewVirtualizationRandomizedTests
{
    private const int _corpusSeed = 0x4C15757A;

    /// <summary>Verifies 40 independently seeded randomized cases of mixed operations never
    /// desynchronize a virtualized ListView from its eager twin.</summary>
    [Fact]
    public void Mutate_WhenOperationsAreRandomized_MatchesEagerRealization()
    {
        var corpus = new Random(_corpusSeed);

        for (var caseIndex = 0; caseIndex < 40; caseIndex++)
        {
            RunCase(caseIndex, corpus.Next());
        }
    }

    private static void RunCase(int caseIndex, int caseSeed)
    {
        var random = new Random(caseSeed);
        var itemCount = random.Next(10, 120);
        var items = Enumerable.Range(0, itemCount).Select(value => (object?) $"Item {value:D4}").ToArray();

        // Stretched to the full given size on both axes so Bounds never depends on content width -
        // eager reports the widest realized row's natural width while virtualized reports only its
        // currently realized subset's width (see the Extent.Width note below), an accepted
        // difference that is otherwise indistinguishable from a real desynchronization once it
        // changes which cell a render comparison is even looking at.
        var eager = new UiListView
        {
            SelectionMode = ListSelectionMode.Multiple,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Items = items
        };
        var virtualized = new UiListView
        {
            SelectionMode = ListSelectionMode.Multiple,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowHeight = 1,
            Items = items
        };

        var size = new Size(random.Next(6, 20), random.Next(2, 12));
        var engine = new LayoutEngine();
        engine.Layout(eager, size);
        engine.Layout(virtualized, size);
        AssertEquivalent(engine, eager, virtualized, size, caseIndex, caseSeed, step: -1);

        for (var step = 0; step < 120; step++)
        {
            switch (random.Next(7))
            {
                case 0:
                    {
                        var delta = random.Next(-10, 11);
                        _ = eager.ScrollBy(0, delta);
                        _ = virtualized.ScrollBy(0, delta);
                        break;
                    }
                case 1 when eager.Items.Count > 0:
                    {
                        var index = random.Next(eager.Items.Count);
                        var selected = random.Next(2) == 0;
                        _ = eager.SetSelected(index, selected);
                        _ = virtualized.SetSelected(index, selected);
                        break;
                    }
                case 2:
                    {
                        var code = RandomNavigationKey(random);
                        _ = eager.MoveCurrent(code);
                        _ = virtualized.MoveCurrent(code);
                        break;
                    }
                case 3:
                    {
                        size = new Size(random.Next(6, 20), random.Next(2, 12));
                        engine.Layout(eager, size);
                        engine.Layout(virtualized, size);
                        break;
                    }
                case 4:
                    {
                        var index = random.Next(eager.Items.Count + 1);
                        var value = (object?) $"New {step:D3}";
                        eager.InsertItem(index, value);
                        virtualized.InsertItem(index, value);
                        break;
                    }
                case 5 when eager.Items.Count > 0:
                    {
                        var index = random.Next(eager.Items.Count);
                        eager.RemoveItem(index);
                        virtualized.RemoveItem(index);
                        break;
                    }
                case 6 when eager.Items.Count > 0:
                    {
                        var index = random.Next(eager.Items.Count);
                        var value = (object?) $"Replaced {step:D3}";
                        eager.ReplaceItem(index, value);
                        virtualized.ReplaceItem(index, value);
                        break;
                    }
                default:
                    break;
            }

            AssertEquivalent(engine, eager, virtualized, size, caseIndex, caseSeed, step);
        }
    }

    private static Code RandomNavigationKey(Random random) => random.Next(6) switch
    {
        0 => Code.Up,
        1 => Code.Down,
        2 => Code.Home,
        3 => Code.End,
        4 => Code.PageUp,
        _ => Code.PageDown
    };

    private static void AssertEquivalent(
        LayoutEngine engine,
        UiListView eager,
        UiListView virtualized,
        Size size,
        int caseIndex,
        int caseSeed,
        int step)
    {
        var context = $"corpus {_corpusSeed:X}, case {caseIndex} (seed {caseSeed:X}), step {step}";

        // A structural mutation (insert/remove/replace) never implicitly re-runs layout for
        // either mode - eager's own newly built row is left unmeasured and unarranged exactly the
        // same way a virtualized row would be without Rewindow's direct-arrange bridge. Settling
        // layout first, before every assertion below, matches how a real render loop always
        // measures and arranges before every frame; it is not a parity target of realization mode
        // itself, and asserting state before this could observe a transient pre-settle snapshot
        // that the very next layout pass would still change.
        engine.Layout(eager, size);
        engine.Layout(virtualized, size);

        virtualized.Items.ShouldBe(eager.Items, context);
        virtualized.SelectedIndex.ShouldBe(eager.SelectedIndex, context);
        virtualized.ActiveIndex.ShouldBe(eager.ActiveIndex, context);
        virtualized.SelectedItems.ShouldBe(eager.SelectedItems, context);

        // Extent.Width is not a parity target: eager reports the widest realized row's natural
        // content width, while virtualized reports the incoming constraint width directly so
        // width never depends on which rows happen to be realized. Neither value
        // matters functionally, since ListView never enables horizontal scrolling by default.
        virtualized.Extent.Height.ShouldBe(eager.Extent.Height, context);
        virtualized.VerticalOffset.ShouldBe(eager.VerticalOffset, context);

        using var eagerFrame = new Frame(new Size(eager.Bounds.Width, eager.Bounds.Height));
        using var virtualizedFrame = new Frame(new Size(virtualized.Bounds.Width, virtualized.Bounds.Height));
        eager.Render(eagerFrame.Canvas);
        virtualized.Render(virtualizedFrame.Canvas);

        for (var y = 0; y < eager.Bounds.Height; y++)
        {
            for (var x = 0; x < eager.Bounds.Width; x++)
            {
                var point = new Point(x, y);
                FrameOracle.Get(virtualizedFrame, point).ShouldBe(FrameOracle.Get(eagerFrame, point), $"{context}, cell ({x},{y})");
            }
        }
    }
}
