// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Exercises command-bar overflow against fixed-seed layout invariants.</summary>
public sealed class CommandBarRandomizedTests
{
    private const int _seed = 0x434D_4442;

    /// <summary>Verifies every visible command appears exactly once, bounds stay contained, and resize round-trips deterministically.</summary>
    [Fact]
    public void Layout_WhenSeededWidthsVary_ShowsEveryActionExactlyOnce()
    {
        var cases = new Random(_seed);

        for (var caseIndex = 0; caseIndex < 80; caseIndex++)
        {
            var seed = cases.Next();
            using var first = CreateCase(new Random(seed), out var firstItems);
            using var second = CreateCase(new Random(seed), out var secondItems);
            var width = new Random(seed ^ _seed).Next(0, 41);

            Layout(first, width);
            Layout(second, width);

            firstItems.Select(static item => item.IsOverflowed)
                .ShouldBe(secondItems.Select(static item => item.IsOverflowed), $"case {caseIndex}");
            firstItems.Where(static item => item.Visibility == Visibility.Visible)
                .ShouldAllBe(
                    item => first.IsPrimaryEntry(item) != item.IsOverflowed,
                    $"exactly one plane, case {caseIndex}, seed {seed}");
            firstItems.ShouldAllBe(
                item => item.Visibility != Visibility.Visible ||
                    item.IsOverflowed ||
                    (item.Bounds.Width > 0 && item.Bounds.X >= 0 && item.Bounds.Right <= width),
                $"case {caseIndex}");
            firstItems.Where(static item => item.Visibility != Visibility.Visible)
                .ShouldAllBe(static item => !item.IsOverflowed, $"case {caseIndex}");

            var overflow = OwnedTree.Find<Menu>(first).ShouldNotBeNull().Items.OfType<MenuItem>().Count();
            overflow.ShouldBe(
                firstItems.Count(static item => item.Visibility == Visibility.Visible && item.IsOverflowed),
                $"case {caseIndex}");
            AssertSeparatorsNormalized(
                first.Items.Where(first.IsPrimaryEntry),
                $"primary, case {caseIndex}, seed {seed}");
            AssertSeparatorsNormalized(
                OwnedTree.Find<Menu>(first).ShouldNotBeNull().Items,
                $"overflow, case {caseIndex}, seed {seed}");

            Layout(first, 48);
            Layout(first, width);
            firstItems.Select(static item => item.IsOverflowed)
                .ShouldBe(secondItems.Select(static item => item.IsOverflowed), $"round-trip case {caseIndex}");
        }
    }

    private static CommandBar CreateCase(Random random, out List<CommandBarItem> items)
    {
        var bar = new CommandBar { Spacing = random.Next(0, 4) };
        items = [];
        var count = random.Next(1, 10);

        for (var index = 0; index < count; index++)
        {
            if (index > 0 && random.Next(3) == 0)
            {
                bar.Items.Add(new CommandBarSeparator());
            }

            var item = new CommandBarItem
            {
                Text = new string((char) ('A' + index), random.Next(1, 9)),
                IsEnabled = random.Next(4) != 0,
                Visibility = random.Next(7) switch
                {
                    0 => Visibility.Hidden,
                    1 => Visibility.Collapsed,
                    _ => Visibility.Visible
                }
            };
            items.Add(item);
            bar.Items.Add(item);
        }

        return bar;
    }

    private static void Layout(CommandBar bar, int width)
    {
        bar.Measure(new Constraint(width, 1));
        bar.Arrange(new Rect(0, 0, width, 1));
    }

    private static void AssertSeparatorsNormalized(IEnumerable<ControlBase> entries, string context)
    {
        var visible = entries.Where(static entry => entry.Visibility == Visibility.Visible).ToArray();

        if (visible.Length == 0)
        {
            return;
        }

        IsSeparator(visible[0]).ShouldBeFalse($"leading separator in {context}");
        IsSeparator(visible[^1]).ShouldBeFalse($"trailing separator in {context}");

        for (var index = 1; index < visible.Length; index++)
        {
            (IsSeparator(visible[index - 1]) && IsSeparator(visible[index]))
                .ShouldBeFalse($"adjacent separators in {context}");
        }
    }

    private static bool IsSeparator(ControlBase entry) =>
        entry is CommandBarSeparator or MenuSeparator;
}
