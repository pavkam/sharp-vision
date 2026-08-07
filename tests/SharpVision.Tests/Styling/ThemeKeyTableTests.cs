// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>Verifies the registered-key table in themes.md lists every own member of every style
/// it documents.
///
/// <para>That table is the discovery path for theme authoring, and three of its rows misdescribed
/// their style type: <c>calendar</c> claimed a bare <c>-</c> for a ten-member style, and
/// <c>jsonView</c> and <c>chart</c> each omitted members. A bare <c>-</c> is a positive assertion
/// that the section offers nothing beyond the inherited face/border/shadow, so an author wanting to
/// restyle a calendar's weekday header concluded the section was useless and hand-wrote a local
/// style per instance - exactly what the section exists to avoid.</para>
///
/// <para>Checked mechanically rather than by eye. Two further rows - <c>scrollBar</c> and
/// <c>chaseIndicator</c> - were found short only once this ran, which is the argument for the test
/// over another careful read.</para>
/// </summary>
public sealed class ThemeKeyTableTests
{
    /// <summary>The regression this file exists to pin.</summary>
    [Fact]
    public void KeyTable_WhenComparedWithTheStyleTypes_ListsEveryOwnMember()
    {
        var rows = ReadKeyTable();
        List<string> problems = [];

        foreach (var (key, listed) in rows)
        {
            var style = StyleTypes.SingleOrDefault(type => StyleKeyFor(type) == key);

            if (style is null)
            {
                problems.Add($"themes.md documents '{key}', which no registrable style type claims");
                continue;
            }

            foreach (var member in OwnMembers(style).Where(member => !listed.Contains(member, StringComparer.Ordinal)))
            {
                problems.Add($"styles.{key} owns '{member}', which the themes.md row omits");
            }
        }

        problems.ShouldBeEmpty();
    }

    // Declared on the leaf itself, and settable: inherited members belong to the fallback style's
    // own row, and a get-only computed property - CheckBoxStyle.MarkWidth, RadioButtonStyle's
    // UncheckedText/CheckedText - is derived from an authorable one rather than authorable itself,
    // so it has no place in a table of what a theme may write.
    private static IEnumerable<string> OwnMembers(Type style) =>
        style
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static property => property is { CanRead: true, CanWrite: true })
            .Select(static property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..]);

    private static IEnumerable<Type> StyleTypes =>
        typeof(ControlStyle).Assembly
            .GetTypes()
            .Where(static type => type.IsSealed && typeof(ControlStyle).IsAssignableFrom(type));

    private static string StyleKeyFor(Type style) =>
        char.ToLowerInvariant(style.Name[0]) + style.Name[1..^"Style".Length];

    private static Dictionary<string, HashSet<string>> ReadKeyTable()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "concepts", "themes.md");
        var rows = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(path))
        {
            var match = Regex.Match(line, @"^\| `(\w+)`\s+\|[^|]*\|[^|]*\|(.*)\|\s*$");

            if (!match.Success)
            {
                continue;
            }

            rows[match.Groups[1].Value] = [.. Regex
                .Matches(match.Groups[2].Value, "`(\\w+)`")
                .Select(static glyph => glyph.Groups[1].Value)];
        }

        rows.ShouldNotBeEmpty("the themes.md key table must be readable for this test to mean anything");
        return rows;
    }

    private static string RepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;

        while (!Directory.Exists(Path.Combine(directory, "docs")))
        {
            directory = Path.GetDirectoryName(directory)
                ?? throw new InvalidOperationException("The repository root was not found.");
        }

        return directory;
    }
}
