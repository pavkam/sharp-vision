// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Provides independent fixed-seed arithmetic for breadcrumb whole-item placement.</summary>
internal static class BreadcrumbLayoutModel
{
    /// <summary>Mutates one retained path and compares each production snapshot with an independent oracle.</summary>
    internal static (string Actual, string Expected, string Description) Run(int seed, int operationCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(operationCount);
        var random = new Random(seed);
        using var breadcrumb = new Breadcrumb();
        var nextIdentity = 0;
        var width = 20;
        List<string> operations = [];

        for (var operationIndex = 0; operationIndex < operationCount; operationIndex++)
        {
            var operation = Apply(random, breadcrumb, ref width, ref nextIdentity);
            operations.Add(operation);

            for (var index = 0; index < breadcrumb.Items.Count; index++)
            {
                breadcrumb.Items[index].Measure(new Constraint(width: null, 1));
            }

            var separatorExtent = breadcrumb.ActualStyle.SeparatorSpacingBefore
                .Add(1)
                .Add(breadcrumb.ActualStyle.SeparatorSpacingAfter);
            var layout = BreadcrumbLayout.Create(
                breadcrumb,
                width,
                triggerWidth: 1,
                breadcrumb.ActualStyle.SeparatorSpacingBefore,
                separatorExtent,
                operationIndex + 1);
            var actual = EncodeActual(layout);
            var expected = EncodeExpected(breadcrumb, width, separatorExtent);

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                var start = Math.Max(0, operations.Count - 12);
                var tail = string.Join(" | ", operations.Skip(start));
                return (
                    actual,
                    expected,
                    $"seed={seed}, case={operationIndex}, width={width}, recent={tail}");
            }
        }

        return ("matched", "matched", $"seed={seed}, operations={operationCount}");
    }

    private static string Apply(Random random, Breadcrumb breadcrumb, ref int width, ref int nextIdentity)
    {
        var operation = random.Next(0, 10);
        var count = breadcrumb.Items.Count;

        switch (operation)
        {
            case 0 when count < 9:
                var item = new BreadcrumbItem
                {
                    Text = random.Next(0, 4) switch
                    {
                        0 => "A",
                        1 => "Root",
                        2 => "界",
                        _ => "e\u0301"
                    },
                    Tag = nextIdentity++
                };
                breadcrumb.Items.Insert(random.Next(0, count + 1), item);
                return $"add:{item.Tag}";
            case 1 when count > 0:
                var removed = breadcrumb.Items[random.Next(count)];
                _ = breadcrumb.Items.Remove(removed);
                removed.Dispose();
                return $"remove:{removed.Tag}";
            case 2 when count > 1:
                var from = random.Next(count);
                var to = random.Next(count);
                breadcrumb.Items.Move(from, to);
                return $"move:{from}>{to}";
            case 3 when count > 0:
                var visibilityItem = breadcrumb.Items[random.Next(count)];
                visibilityItem.Visibility = (Visibility) random.Next(0, 3);
                return $"visibility:{visibilityItem.Tag}={visibilityItem.Visibility}";
            case 4 when count > 0:
                var enabledItem = breadcrumb.Items[random.Next(count)];
                enabledItem.IsEnabled = !enabledItem.IsEnabled;
                return $"enabled:{enabledItem.Tag}={enabledItem.IsEnabled}";
            case 5:
                width = random.Next(0, 25);
                return $"width:{width}";
            case 6 when count > 0:
                var available = breadcrumb.Items.Where(IsAvailable).ToArray();

                if (available.Length == 0 || random.Next(0, 4) == 0)
                {
                    breadcrumb.CurrentIndex = -1;
                    return "current:none";
                }

                var current = available[random.Next(available.Length)];
                breadcrumb.CurrentItem = current;
                return $"current:{current.Tag}";
            case 7 when count > 0:
                var textItem = breadcrumb.Items[random.Next(count)];
                textItem.Text = random.Next(0, 2) == 0 ? "Long" : "界";
                return $"text:{textItem.Tag}={textItem.Text}";
            case 8 when count > 0:
                var disposed = breadcrumb.Items[random.Next(count)];
                disposed.Dispose();
                return $"dispose:{disposed.Tag}";
            case 9:
                var before = random.Next(0, 4);
                var after = random.Next(0, 4);
                breadcrumb.Style = BreadcrumbStyle.Default with
                {
                    SeparatorSpacingBefore = before,
                    SeparatorSpacingAfter = after
                };
                return $"spacing:{before},{after}";
            default:
                return "noop";
        }
    }

    private static string EncodeActual(BreadcrumbLayout layout)
    {
        var primary = string.Join(
            ",",
            layout.Entries
                .Where(entry => entry.IsPrimary)
                .Select(entry => $"{entry.Item.Tag}@{entry.Bounds.X}:{entry.Bounds.Width}"));
        var overflow = string.Join(",", layout.OverflowItems.Select(item => item.Tag));
        return $"P[{primary}]O[{overflow}]T[{layout.TriggerBounds.X}:{layout.TriggerBounds.Width}:{layout.TriggerPrecedesPrimary}]";
    }

    private static string EncodeExpected(Breadcrumb breadcrumb, int width, int separatorExtent)
    {
        var participants = Enumerable.Range(0, breadcrumb.Items.Count)
            .Where(index => breadcrumb.Items[index].Visibility != Visibility.Collapsed)
            .ToList();
        var widths = Enumerable.Range(0, breadcrumb.Items.Count)
            .Select(index => breadcrumb.Items[index].DesiredSize.Width.Add(breadcrumb.Items[index].Margin.Horizontal))
            .ToArray();
        var natural = Sum(participants, widths, separatorExtent);
        var current = breadcrumb.CurrentIndex;
        List<int> primary = [];
        var trigger = false;
        var precedes = current >= 0;

        if (natural <= width && (current < 0 || participants.Count == 0 || participants[^1] == current))
        {
            primary.AddRange(participants);
        }
        else if (current >= 0)
        {
            var currentPosition = participants.IndexOf(current);

            for (var start = 0; start <= currentPosition; start++)
            {
                var candidate = participants.GetRange(start, currentPosition - start + 1);
                var omitted = HasAvailableOmission(breadcrumb, candidate);
                var candidateWidth = Sum(candidate, widths, separatorExtent)
                    .Add(omitted ? 1.Add(separatorExtent) : 0);

                if (candidateWidth <= width)
                {
                    primary.AddRange(candidate);
                    trigger = omitted;
                    break;
                }
            }

            if (primary.Count == 0 && width >= 1 && HasAvailableOmission(breadcrumb, primary))
            {
                trigger = true;
            }
        }
        else
        {
            for (var length = participants.Count; length >= 0; length--)
            {
                var candidate = participants.GetRange(0, length);

                if (HasAvailableOmission(breadcrumb, candidate) &&
                    Sum(candidate, widths, separatorExtent)
                        .Add(1)
                        .Add(candidate.Count > 0 ? separatorExtent : 0) <= width)
                {
                    primary.AddRange(candidate);
                    trigger = true;
                    break;
                }
            }

            if (!trigger)
            {
                for (var length = participants.Count; length >= 0; length--)
                {
                    var candidate = participants.GetRange(0, length);

                    if (Sum(candidate, widths, separatorExtent) <= width)
                    {
                        primary.AddRange(candidate);
                        break;
                    }
                }
            }
        }

        var overflow = Enumerable.Range(0, breadcrumb.Items.Count)
            .Where(index => !primary.Contains(index) && IsAvailable(breadcrumb.Items[index]))
            .ToArray();
        var x = trigger && precedes ? 1.Add(primary.Count > 0 ? separatorExtent : 0) : 0;
        List<string> encodedPrimary = [];

        for (var position = 0; position < primary.Count; position++)
        {
            var index = primary[position];
            encodedPrimary.Add($"{breadcrumb.Items[index].Tag}@{x}:{widths[index]}");
            x = x.Add(widths[index]).Add(position + 1 < primary.Count ? separatorExtent : 0);
        }

        var triggerX = 0;

        if (trigger)
        {
            triggerX = precedes ? 0 : x.Add(primary.Count > 0 ? separatorExtent : 0);
        }

        return $"P[{string.Join(",", encodedPrimary)}]O[{string.Join(",", overflow.Select(index => breadcrumb.Items[index].Tag))}]T[{triggerX}:{(trigger ? 1 : 0)}:{precedes}]";
    }

    private static int Sum(IEnumerable<int> indices, int[] widths, int separatorExtent)
    {
        var sum = 0;
        var count = 0;

        foreach (var index in indices)
        {
            sum = sum.Add(widths[index]);
            count++;
        }

        return sum.Add(Math.Max(0, count - 1).Multiply(separatorExtent));
    }

    private static bool HasAvailableOmission(Breadcrumb breadcrumb, IReadOnlyCollection<int> primary)
    {
        for (var index = 0; index < breadcrumb.Items.Count; index++)
        {
            if (!primary.Contains(index) && IsAvailable(breadcrumb.Items[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAvailable(BreadcrumbItem item) =>
        !item.IsDisposed &&
        item.Visibility == Visibility.Visible &&
        item.EffectiveIsVisible &&
        item.EffectiveIsEnabled;
}
