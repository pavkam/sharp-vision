// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Reflection;

using SharpVision.Controls.Input;

/// <summary>Verifies the one invariant that makes a <c>styles.*</c> key meaningful: a section the
/// theme parser accepts is a section something actually resolves.
///
/// <para>The registry discovers style types reflectively rather than from a hand-written list,
/// which already removed one class of drift - a key spelled one way at its definition and another
/// in the registry. It left a second: a type could be registered purely by deriving from
/// <c>ControlStyle</c> while defining itself as a secondary (part) style that owns no key. A theme
/// could then author that section and see nothing happen, which is a typo's failure mode with the
/// parser's blessing. <c>ColorPickerStyle</c> was exactly that, and its own documentation said so
/// while the registry disagreed.</para>
/// </summary>
public sealed class StyleSectionRegistryTests
{
    /// <summary>Verifies every accepted section resolves. Each registered key is authored into a
    /// document and must survive parsing and produce a retained section - which a key nothing owns
    /// would not.</summary>
    [Fact]
    public void Parse_WhenDocumentAuthorsEveryRegisteredSection_ResolvesEachOne()
    {
        foreach (var section in RegisteredSections())
        {
            var json = ThemeJson.Create(
                extraStyles: $$""", "{{section}}": { "normal": { "face": { "foreground": "accent" } } } """);

            var theme = Should.NotThrow(() => ThemeCatalog.Parse(json), $"section '{section}' must be accepted");

            theme.StyleSections.ShouldContainKey(section);
        }
    }

    /// <summary>The regression this file exists to pin. A secondary style type owns no section, so
    /// its derived key must be rejected exactly like any other unknown name rather than accepted
    /// and then ignored.</summary>
    [Fact]
    public void Parse_WhenSectionBelongsToASecondaryStyle_Throws()
    {
        var key = StyleKey.Of<ColorPickerStyle>();
        key.ShouldBe("colorPicker", "the key is still derived from the type name");

        var json = ThemeJson.Create(
            extraStyles: $$""", "{{key}}": { "normal": { "face": { "foreground": "accent" } } } """);

        var exception = Should.Throw<InvalidDataException>(() => ThemeCatalog.Parse(json));

        exception.Message.ShouldContain(key);
    }

    /// <summary>Verifies the rule holds for the whole assembly, not just the one type that broke
    /// it: no registered section belongs to a style type that declares itself secondary. A future
    /// part style cannot quietly acquire a theme key by deriving from <c>ControlStyle</c>.</summary>
    [Fact]
    public void RegisteredSections_WhenDerivedFromStyleTypes_ExcludeEverySecondaryStyle()
    {
        var registered = RegisteredSections();
        var secondary = new List<string>();

        foreach (var type in typeof(ControlStyle).Assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsAssignableTo(typeof(ControlStyle)))
            {
                continue;
            }

            // Read through the non-generic view: Definition is StyleDefinition<TStyle>, and this
            // walk only ever holds the open Type.
            if (type
                    .GetProperty("Definition", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetValue(null) is IStyleDefinition { IsControl: false })
            {
                secondary.Add(StyleKey.Of(type));
            }
        }

        secondary.ShouldNotBeEmpty("a secondary style must exist for this to be testing anything");
        registered.ShouldNotBeEmpty();

        foreach (var key in secondary)
        {
            registered.ShouldNotContain(key, $"'{key}' belongs to a secondary style and owns no theme section");
        }
    }

    // The registry itself is private, so it is read the way a theme author observes it: a section
    // is registered exactly when an unqualified key by that name is accepted.
    private static List<string> RegisteredSections()
    {
        var sections = new List<string>();

        foreach (var type in typeof(ControlStyle).Assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsAssignableTo(typeof(ControlStyle)))
            {
                continue;
            }

            var key = StyleKey.Of(type);
            var json = ThemeJson.Create(
                extraStyles: $$""", "{{key}}": { "normal": { "face": { "foreground": "accent" } } } """);

            try
            {
                _ = ThemeCatalog.Parse(json);
                sections.Add(key);
            }
            catch (InvalidDataException)
            {
                // Not a registered section.
            }
        }

        return sections;
    }
}
