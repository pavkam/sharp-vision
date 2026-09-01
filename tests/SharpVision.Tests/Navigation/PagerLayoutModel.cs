// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Builds a literal-specification Pager layout without calling production layout helpers.</summary>
internal static class PagerLayoutModel
{
    /// <summary>Creates the exact default-glyph finite layout for one valid page state.</summary>
    /// <param name="pageCount">The non-negative page count.</param>
    /// <param name="pageIndex">The valid current page, or -1 for an empty range.</param>
    /// <param name="maximumVisiblePages">The positive neighboring interior-page budget.</param>
    /// <param name="width">The non-negative available cells.</param>
    /// <returns>The independently selected targets in visual order with whole-cell bounds.</returns>
    internal static IReadOnlyList<(
        PagerTargetKind Kind,
        int PageIndex,
        string Text,
        Rect Bounds,
        bool IsEnabled,
        bool IsCurrent)> Create(
            int pageCount,
            int pageIndex,
            int maximumVisiblePages,
            int width)
    {
        Debug.Assert(pageCount >= 0);
        Debug.Assert(pageCount == 0 ? pageIndex == -1 : pageIndex >= 0 && pageIndex < pageCount);
        Debug.Assert(maximumVisiblePages > 0);
        Debug.Assert(width >= 0);

        if (pageCount == 0 || width == 0)
        {
            return [];
        }

        var selected = new List<(
            long Order,
            PagerTargetKind Kind,
            int PageIndex,
            string Text,
            bool IsEnabled,
            bool IsCurrent)>();
        var currentText = FormatPage(pageIndex);

        if (currentText.Length > width)
        {
            return [];
        }

        Add(selected, Number(pageIndex, pageIndex, pageCount), NumberOrder(pageIndex), width);

        if (pageCount > 1)
        {
            AddNumber(selected, 0, pageIndex, pageCount, width);
            AddNumber(selected, pageCount - 1, pageIndex, pageCount, width);

            var interior = Math.Max(0, pageCount - 2);

            if (pageIndex > 0 && pageIndex < pageCount - 1)
            {
                interior--;
            }

            var remaining = Math.Min(maximumVisiblePages, interior);

            for (var distance = 1; remaining > 0; distance++)
            {
                var found = false;
                var left = pageIndex - distance;

                if (left > 0 && left < pageCount - 1)
                {
                    AddNumber(selected, left, pageIndex, pageCount, width);
                    remaining--;
                    found = true;

                    if (remaining == 0)
                    {
                        break;
                    }
                }

                var right = (long) pageIndex + distance;

                if (right > 0 && right < pageCount - 1)
                {
                    AddNumber(selected, (int) right, pageIndex, pageCount, width);
                    remaining--;
                    found = true;
                }

                if (!found && left <= 0 && right >= pageCount - 1L)
                {
                    break;
                }
            }

            var numbers = selected
                .Where(static target => target.Kind == PagerTargetKind.Number)
                .Select(static target => target.PageIndex)
                .Order()
                .ToArray();

            for (var index = 1; index < numbers.Length; index++)
            {
                if (numbers[index] - numbers[index - 1] <= 1)
                {
                    continue;
                }

                Add(
                    selected,
                    (PagerTargetKind.Omitted, -1, "…", false, false),
                    NumberOrder(numbers[index - 1]) + 1,
                    width);
            }

            Add(
                selected,
                (PagerTargetKind.Previous, Math.Max(0, pageIndex - 1), "‹", pageIndex > 0, false),
                1,
                width);
            Add(
                selected,
                (PagerTargetKind.Next, Math.Min(pageCount - 1, pageIndex + 1), "›", pageIndex < pageCount - 1, false),
                long.MaxValue - 1,
                width);
            Add(
                selected,
                (PagerTargetKind.First, 0, "«", pageIndex > 0, false),
                0,
                width);
            Add(
                selected,
                (PagerTargetKind.Last, pageCount - 1, "»", pageIndex < pageCount - 1, false),
                long.MaxValue,
                width);
        }

        selected.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        var result = new (
            PagerTargetKind Kind,
            int PageIndex,
            string Text,
            Rect Bounds,
            bool IsEnabled,
            bool IsCurrent)[selected.Count];
        var x = 0;

        for (var index = 0; index < selected.Count; index++)
        {
            if (index > 0)
            {
                x++;
            }

            var target = selected[index];
            var cellWidth = target.Text.Length;
            result[index] = (
                target.Kind,
                target.PageIndex,
                target.Text,
                new Rect(x, 0, cellWidth, 1),
                target.IsEnabled,
                target.IsCurrent);
            x += cellWidth;
        }

        return result;
    }

    private static void AddNumber(
        List<(long Order, PagerTargetKind Kind, int PageIndex, string Text, bool IsEnabled, bool IsCurrent)> selected,
        int candidate,
        int current,
        int pageCount,
        int width)
    {
        if (selected.Any(target => target.Kind == PagerTargetKind.Number && target.PageIndex == candidate))
        {
            return;
        }

        Add(selected, Number(candidate, current, pageCount), NumberOrder(candidate), width);
    }

    private static (PagerTargetKind Kind, int PageIndex, string Text, bool IsEnabled, bool IsCurrent) Number(
        int candidate,
        int current,
        int pageCount) => (
            PagerTargetKind.Number,
            candidate,
            FormatPage(candidate),
            pageCount > 1 && candidate != current,
            candidate == current);

    private static void Add(
        List<(long Order, PagerTargetKind Kind, int PageIndex, string Text, bool IsEnabled, bool IsCurrent)> selected,
        (PagerTargetKind Kind, int PageIndex, string Text, bool IsEnabled, bool IsCurrent) target,
        long order,
        int width)
    {
        var nextWidth = selected.Sum(static candidate => candidate.Text.Length) + target.Text.Length + selected.Count;

        if (nextWidth <= width)
        {
            selected.Add((order, target.Kind, target.PageIndex, target.Text, target.IsEnabled, target.IsCurrent));
        }
    }

    private static string FormatPage(int pageIndex) =>
        ((long) pageIndex + 1).ToString(CultureInfo.InvariantCulture);

    private static long NumberOrder(int pageIndex) => 2L + (pageIndex * 2L);
}
