# Themeable Palettes Implementation Plan

<!-- markdownlint-disable MD013 MD034 -->
<!-- Historical snapshot: MD013 preserves exact commands; MD034 preserves captured source URLs. -->

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship JSON-defined palette themes — a curated set of ~10 recognizable
editor themes plus the built-in Light/Dark — that load from embedded resources
and arbitrary files at runtime and drive the showcase picker.

**Architecture:** A theme file is a palette (named colors) plus a semantic role
map. A loader resolves the role map (with fallbacks) into the 12 `ColorRole`
colors, and one built-in recipe turns those into a frozen `Theme` (roles + a
base `ControlStyle<Control>`). Themes ship as embedded `*.theme.json` resources
discovered by `ThemeCatalog`; the built-in Light/Dark become catalog-backed
indexed themes. This layers on the existing styling model — no per-control
stylesheet format, no new dependency.

**Tech Stack:** .NET 10 / C# 14, `System.Text.Json` (built-in), xUnit v3 +
Shouldly, embedded resources (same mechanism as `FigletCatalog`).

Spec: `docs/superpowers/specs/2026-07-14-themeable-palettes-design.md`.

## Global Constraints

- Target .NET 10 and C# 14. File-scoped namespaces; `using` directives after the
  namespace; `var` for locals.
- One named type per file, named after the type (generic arity omitted). No
  nested named types, no two types per file.
- Never use primary constructors or positional records. Declare constructors
  explicitly and validate arguments before assigning state.
- Add XML documentation to every public and internal type and member; document
  every thrown exception.
- No new runtime package dependency — `System.Text.Json` only.
  Culture-independent parsing (`CultureInfo.InvariantCulture`).
- Validate every argument of a public method/constructor before changing
  observable state.
- Zero build warnings/errors. Before declaring a phase complete run
  `make format`, `make lint`, `make build`, `make test`.
- Tests: xUnit v3, Shouldly, Arrange/Act/Assert,
  `MethodName_WhenThis_ThatIsExpected`. Watch each new test fail first.

**Setup (before Task 1):** Work proceeds **directly on
`codex/runtime-protocol-router`** (per decision 2026-07-14). `main` is 89
commits behind and does not contain the styling subsystem, so it cannot be the
base; the styling foundation this feature builds on lives only on this branch.
Before Task 1, the pre-existing staged pane-rename refactor and the spec/plan
docs are committed as their own commits so theme-task commits stay isolated.

---

### Task 1: `Color.FromHex` / `Color.TryFromHex`

**Files:**

- Modify: `src/SharpVision.Terminal/Protocols/Color.cs`
- Test: `tests/SharpVision.Terminal.Tests/Protocols/ColorHexTests.cs` (create)

**Interfaces:**

- Consumes: existing `Color.Rgb(int,int,int)`, `Color.Default`.
- Produces: `public static Color Color.FromHex(string value)`;
  `public static bool Color.TryFromHex(string value, out Color color)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Terminal.Tests/Protocols/ColorHexTests.cs
namespace SharpVision.Terminal.Tests.Protocols;

using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies hex parsing of terminal RGB colors.</summary>
public sealed class ColorHexTests
{
    [Fact]
    public void FromHex_WhenSixDigits_ParsesRgb()
    {
        Color color = Color.FromHex("#1a2b3c");

        color.Kind.ShouldBe(ColorKind.Rgb);
        color.Red.ShouldBe((byte) 0x1a);
        color.Green.ShouldBe((byte) 0x2b);
        color.Blue.ShouldBe((byte) 0x3c);
    }

    [Fact]
    public void FromHex_WhenThreeDigits_ExpandsNibbles()
    {
        Color color = Color.FromHex("f80");

        color.ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
    }

    [Fact]
    public void FromHex_WhenMixedCaseWithHash_ParsesRgb()
    {
        Color.FromHex("#AbCdEf").ShouldBe(Color.Rgb(0xab, 0xcd, 0xef));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#gg0000")]
    [InlineData("#12345678")] // no alpha
    public void FromHex_WhenMalformed_Throws(string? value)
    {
        if (value is null)
        {
            Should.Throw<ArgumentNullException>(() => Color.FromHex(value!));
        }
        else
        {
            Should.Throw<FormatException>(() => Color.FromHex(value));
        }
    }

    [Fact]
    public void TryFromHex_WhenMalformed_ReturnsFalseAndDefault()
    {
        Color.TryFromHex("#nope", out Color color).ShouldBeFalse();
        color.ShouldBe(Color.Default);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ColorHexTests" --timeout 60s`
Expected: FAIL — `Color` has no `FromHex`/`TryFromHex`.

- [ ] **Step 3: Write minimal implementation**

Add to `src/SharpVision.Terminal/Protocols/Color.cs` (after `Rgb`):

```csharp
    /// <summary>Parses a hex RGB color string (<c>#rgb</c> or <c>#rrggbb</c>, case-insensitive, leading <c>#</c> optional).</summary>
    /// <param name="value">The hex color string.</param>
    /// <returns>The parsed 24-bit RGB color.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">The string is not a 3- or 6-digit hex color.</exception>
    public static Color FromHex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TryFromHex(value, out Color color)
            ? color
            : throw new FormatException($"'{value}' is not a valid #rgb or #rrggbb color.");
    }

    /// <summary>Attempts to parse a hex RGB color string without throwing.</summary>
    /// <param name="value">The candidate hex color string.</param>
    /// <param name="color">The parsed color, or <see cref="Default"/> when parsing fails.</param>
    /// <returns>Whether <paramref name="value"/> is a valid 3- or 6-digit hex color.</returns>
    public static bool TryFromHex(string value, out Color color)
    {
        color = Default;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        ReadOnlySpan<char> digits = value[0] == '#' ? value.AsSpan(1) : value.AsSpan();

        if (digits.Length == 3)
        {
            if (!TryNibble(digits[0], out int r) || !TryNibble(digits[1], out int g) || !TryNibble(digits[2], out int b))
            {
                return false;
            }

            color = Rgb((r << 4) | r, (g << 4) | g, (b << 4) | b);
            return true;
        }

        if (digits.Length == 6)
        {
            if (!TryByte(digits[..2], out int r) || !TryByte(digits[2..4], out int g) || !TryByte(digits[4..], out int b))
            {
                return false;
            }

            color = Rgb(r, g, b);
            return true;
        }

        return false;
    }

    private static bool TryNibble(char c, out int value) =>
        int.TryParse(stackalloc char[] { c }, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

    private static bool TryByte(ReadOnlySpan<char> pair, out int value) =>
        int.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
```

Add `using System.Globalization;` after the file-scoped namespace in `Color.cs`
(or confirm it via `GlobalUsings`; the Terminal project's global usings do not
include it, so add the local `using`).

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Terminal.Tests --filter-class "*ColorHexTests" --timeout 60s`
Expected: PASS (all cases).

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Terminal/Protocols/Color.cs tests/SharpVision.Terminal.Tests/Protocols/ColorHexTests.cs
git commit -m "feat(terminal): add Color hex parsing (FromHex/TryFromHex)"
```

---

### Task 2: Expand and rename `ColorRole`

**Files:**

- Modify: `src/SharpVision/Styling/ColorRole.cs`
- Modify: `src/SharpVision/Styling/Themes.cs:59` (only reference to
  `ColorRole.Selection`)
- Test: `tests/SharpVision.Tests/Styling/ColorRoleTests.cs` (add cases)

**Interfaces:**

- Produces: renames `ColorRole.Selection` → `ColorRole.SelectionBackground`;
  adds `ColorRole.SelectionForeground`, `ColorRole.Error`, `ColorRole.Warning`,
  `ColorRole.Success`, `ColorRole.Info`.

- [ ] **Step 1: Write the failing test** — add to `ColorRoleTests`:

```csharp
    [Fact]
    public void SetColor_WhenStatusRole_RoundTrips()
    {
        Theme theme = new();
        theme.SetColor(ColorRole.Error, Color.Rgb(255, 0, 0));
        theme.SetColor(ColorRole.SelectionBackground, Color.Indexed(4));
        theme.SetColor(ColorRole.SelectionForeground, Color.Indexed(15));

        theme.TryGetColor(ColorRole.Error, out Color error).ShouldBeTrue();
        error.ShouldBe(Color.Rgb(255, 0, 0));
        theme.TryGetColor(ColorRole.SelectionBackground, out Color selBg).ShouldBeTrue();
        selBg.ShouldBe(Color.Indexed(4));
        theme.TryGetColor(ColorRole.SelectionForeground, out Color selFg).ShouldBeTrue();
        selFg.ShouldBe(Color.Indexed(15));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ColorRoleTests" --timeout 60s`
Expected: FAIL — `ColorRole.SelectionBackground`/`Error`/`SelectionForeground`
do not exist (compile error).

- [ ] **Step 3: Write minimal implementation** — in `ColorRole.cs`, rename the
      `Selection` member to `SelectionBackground` (keep its existing doc comment
      "The background color of a selected item.") and add the four status
      members plus `SelectionForeground` after it:

```csharp
    /// <summary>The background color of a selected item.</summary>
    SelectionBackground,

    /// <summary>The text color of a selected item.</summary>
    SelectionForeground,

    /// <summary>The color signaling an error or failed state.</summary>
    Error,

    /// <summary>The color signaling a caution or degraded state.</summary>
    Warning,

    /// <summary>The color signaling a successful or healthy state.</summary>
    Success,

    /// <summary>The color signaling neutral informational emphasis.</summary>
    Info,
```

Then update the one caller in `Themes.cs:59` — `ColorRole.Selection` →
`ColorRole.SelectionBackground`. (Task 9 rewrites `Themes.cs` entirely; this
keeps the build green in between.)

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ColorRoleTests" --timeout 60s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Styling/ColorRole.cs src/SharpVision/Styling/Themes.cs tests/SharpVision.Tests/Styling/ColorRoleTests.cs
git commit -m "feat(styling): rename Selection to SelectionBackground and add roles"
```

---

### Task 3: `ThemeColorValue` grammar helper

**Files:**

- Create: `src/SharpVision/Styling/ThemeColorValue.cs`
- Test: `tests/SharpVision.Tests/Styling/ThemeColorValueTests.cs`

**Interfaces:**

- Consumes: `Color.FromHex` (Task 1), `Color.Indexed`.
- Produces: `internal static class ThemeColorValue` with
  `static bool IsLiteral(string value)` and
  `static Color ParseLiteral(string value)` (throws `FormatException` for
  malformed/out-of-range).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Styling/ThemeColorValueTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the theme color-value grammar (hex, indexed, palette-key discrimination).</summary>
public sealed class ThemeColorValueTests
{
    [Theory]
    [InlineData("#fff")]
    [InlineData("#1a2b3c")]
    [InlineData("idx:0")]
    [InlineData("idx:255")]
    public void IsLiteral_WhenLiteral_ReturnsTrue(string value) =>
        ThemeColorValue.IsLiteral(value).ShouldBeTrue();

    [Theory]
    [InlineData("blue")]
    [InlineData("bg-dark")]
    public void IsLiteral_WhenPaletteKey_ReturnsFalse(string value) =>
        ThemeColorValue.IsLiteral(value).ShouldBeFalse();

    [Fact]
    public void ParseLiteral_WhenHex_ReturnsRgb() =>
        ThemeColorValue.ParseLiteral("#1a2b3c").ShouldBe(Color.Rgb(0x1a, 0x2b, 0x3c));

    [Fact]
    public void ParseLiteral_WhenIndexed_ReturnsIndexed() =>
        ThemeColorValue.ParseLiteral("idx:8").ShouldBe(Color.Indexed(8));

    [Theory]
    [InlineData("idx:256")]
    [InlineData("idx:-1")]
    [InlineData("idx:")]
    [InlineData("idx:x")]
    [InlineData("#gg0000")]
    public void ParseLiteral_WhenMalformed_Throws(string value) =>
        Should.Throw<FormatException>(() => ThemeColorValue.ParseLiteral(value));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeColorValueTests" --timeout 60s`
Expected: FAIL — `ThemeColorValue` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/SharpVision/Styling/ThemeColorValue.cs
namespace SharpVision.Styling;

using System.Globalization;

using SharpVision.Terminal.Protocols;

/// <summary>Parses the theme color-value grammar: <c>#hex</c>, <c>idx:N</c>, or (elsewhere) a palette key.</summary>
internal static class ThemeColorValue
{
    private const string _indexPrefix = "idx:";

    /// <summary>Gets whether the value is an inline literal (<c>#hex</c> or <c>idx:N</c>) rather than a palette key.</summary>
    /// <param name="value">The candidate value; must be non-null.</param>
    /// <returns>Whether the value is an inline color literal.</returns>
    public static bool IsLiteral(string value) =>
        value.StartsWith('#') || value.StartsWith(_indexPrefix, StringComparison.Ordinal);

    /// <summary>Parses an inline literal into a color.</summary>
    /// <param name="value">A <c>#hex</c> or <c>idx:N</c> literal.</param>
    /// <returns>The parsed color.</returns>
    /// <exception cref="FormatException">The literal is malformed or the index is outside 0-255.</exception>
    public static Color ParseLiteral(string value)
    {
        if (value.StartsWith(_indexPrefix, StringComparison.Ordinal))
        {
            ReadOnlySpan<char> digits = value.AsSpan(_indexPrefix.Length);

            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index is < 0 or > 255)
            {
                throw new FormatException($"'{value}' is not a valid idx:0-255 color.");
            }

            return Color.Indexed(index);
        }

        return Color.FromHex(value);
    }
}
```

(`NumberStyles.None` rejects signs/whitespace, so `idx:-1` fails.)

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeColorValueTests" --timeout 60s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Styling/ThemeColorValue.cs tests/SharpVision.Tests/Styling/ThemeColorValueTests.cs
git commit -m "feat(styling): add theme color-value grammar helper"
```

---

### Task 4: `ThemeDefinition` DTO + JSON deserialize

**Files:**

- Create: `src/SharpVision/Styling/ThemeDefinition.cs`
- Create: `src/SharpVision/Styling/ThemeLoader.cs` (with `Deserialize` only for
  now)
- Test: `tests/SharpVision.Tests/Styling/ThemeDeserializeTests.cs`

**Interfaces:**

- Produces: `internal sealed class ThemeDefinition` with public get/set
  properties `Name`, `Slug`, `ColorScheme` (string?), `Order` (int), `Author`,
  `License`, `Source` (all `string?`), `Palette` and `Roles`
  (`Dictionary<string, string>?`). `internal static class ThemeLoader` with
  `static ThemeDefinition Deserialize(string json, string source)` (throws
  `InvalidDataException`).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Styling/ThemeDeserializeTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Styling;

using Shouldly;

/// <summary>Verifies theme JSON deserialization into the definition DTO.</summary>
public sealed class ThemeDeserializeTests
{
    private const string _json = """
        {
          "name": "Sample", "slug": "sample", "colorScheme": "dark", "order": 5,
          "author": "A", "license": "MIT", "source": "https://example.test",
          "palette": { "bg": "#000000", "fg": "#ffffff" },
          "roles": { "background": "bg", "foreground": "fg" }
        }
        """;

    [Fact]
    public void Deserialize_WhenValidJson_MapsFields()
    {
        ThemeDefinition definition = ThemeLoader.Deserialize(_json, "sample");

        definition.Slug.ShouldBe("sample");
        definition.Order.ShouldBe(5);
        definition.ColorScheme.ShouldBe("dark");
        definition.Palette!["bg"].ShouldBe("#000000");
        definition.Roles!["foreground"].ShouldBe("fg");
    }

    [Fact]
    public void Deserialize_WhenMalformedJson_Throws()
    {
        InvalidDataException error = Should.Throw<InvalidDataException>(
            () => ThemeLoader.Deserialize("{ not json", "broken"));

        error.Message.ShouldContain("broken");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeDeserializeTests" --timeout 60s`
Expected: FAIL — types missing.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/SharpVision/Styling/ThemeDefinition.cs
namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Deserialization target for a theme JSON file. Validation happens in <see cref="ThemeLoader"/>.</summary>
internal sealed class ThemeDefinition
{
    /// <summary>Gets or sets the display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the stable catalog slug.</summary>
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    /// <summary>Gets or sets the color-scheme token (<c>dark</c> or <c>light</c>).</summary>
    [JsonPropertyName("colorScheme")]
    public string? ColorScheme { get; set; }

    /// <summary>Gets or sets the deterministic catalog sort key.</summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>Gets or sets the attribution author.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>Gets or sets the license identifier.</summary>
    [JsonPropertyName("license")]
    public string? License { get; set; }

    /// <summary>Gets or sets the source URL.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Gets or sets the named palette (name to color-value string).</summary>
    [JsonPropertyName("palette")]
    public Dictionary<string, string>? Palette { get; set; }

    /// <summary>Gets or sets the semantic role map (role name to color-value or palette key).</summary>
    [JsonPropertyName("roles")]
    public Dictionary<string, string>? Roles { get; set; }
}
```

```csharp
// src/SharpVision/Styling/ThemeLoader.cs
namespace SharpVision.Styling;

using System.Text.Json;

/// <summary>Turns theme JSON and definitions into frozen <see cref="Theme"/> instances.</summary>
internal static class ThemeLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    /// <summary>Deserializes theme JSON into a definition.</summary>
    /// <param name="json">The theme JSON text.</param>
    /// <param name="source">A label (slug or path) used in error messages.</param>
    /// <returns>The deserialized definition.</returns>
    /// <exception cref="InvalidDataException">The JSON is malformed or empty.</exception>
    public static ThemeDefinition Deserialize(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<ThemeDefinition>(json, _options)
                ?? throw new InvalidDataException($"Theme '{source}' deserialized to null.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Theme '{source}' is not valid JSON.", error);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeDeserializeTests" --timeout 60s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Styling/ThemeDefinition.cs src/SharpVision/Styling/ThemeLoader.cs tests/SharpVision.Tests/Styling/ThemeDeserializeTests.cs
git commit -m "feat(styling): add theme definition DTO and JSON deserialize"
```

---

### Task 5: `ThemeBuilder` — roles → frozen `Theme`

**Files:**

- Create: `src/SharpVision/Styling/ThemeBuilder.cs`
- Test: `tests/SharpVision.Tests/Styling/ThemeBuilderTests.cs`

**Interfaces:**

- Consumes: `Theme`, `ControlStyle<Control>`, `Control.*Property`, `State`,
  `TerminalAttributes.Underline`.
- Produces: `internal static class ThemeBuilder` with
  `static Theme Build(IReadOnlyDictionary<ColorRole, Color> roles)` returning a
  frozen theme. Assumes all 12 roles are present (the loader guarantees this).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Styling/ThemeBuilderTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the builder produces a frozen theme with roles and a base control style.</summary>
public sealed class ThemeBuilderTests
{
    private static Dictionary<ColorRole, Color> Roles()
    {
        Dictionary<ColorRole, Color> roles = [];

        foreach (ColorRole role in Enum.GetValues<ColorRole>())
        {
            roles[role] = Color.Indexed((int) role + 1);
        }

        return roles;
    }

    [Fact]
    public void Build_ProducesFrozenThemeWithRoles()
    {
        Theme theme = ThemeBuilder.Build(Roles());

        theme.IsFrozen.ShouldBeTrue();
        theme.TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
        accent.ShouldBe(Color.Indexed((int) ColorRole.Accent + 1));
    }

    [Fact]
    public void Build_SetsBaseControlStyleForRepresentativeStates()
    {
        Dictionary<ColorRole, Color> roles = Roles();
        Theme theme = ThemeBuilder.Build(roles);

        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Normal)
            .ShouldBe(roles[ColorRole.Foreground]);
        ThemeResolver.Resolve(theme, typeof(Control), Control.BackgroundProperty, State.Selected)
            .ShouldBe(roles[ColorRole.SelectionBackground]);
        ThemeResolver.Resolve(theme, typeof(Control), Control.ForegroundProperty, State.Disabled)
            .ShouldBe(roles[ColorRole.Muted]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeBuilderTests" --timeout 60s`
Expected: FAIL — `ThemeBuilder` missing.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/SharpVision/Styling/ThemeBuilder.cs
namespace SharpVision.Styling;

using SharpVision.Terminal.Protocols;

/// <summary>Builds a frozen <see cref="Theme"/> from resolved semantic role colors using the standard recipe.</summary>
internal static class ThemeBuilder
{
    /// <summary>Builds and freezes a theme from the twelve resolved role colors.</summary>
    /// <param name="roles">The resolved colors for every <see cref="ColorRole"/> member.</param>
    /// <returns>The frozen theme carrying the roles and one base control style.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="roles"/> is null.</exception>
    public static Theme Build(IReadOnlyDictionary<ColorRole, Color> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        Theme theme = new();

        foreach (ColorRole role in Enum.GetValues<ColorRole>())
        {
            theme.SetColor(role, roles[role]);
        }

        theme.SetStyle(BuildBaseStyle(roles));
        theme.Freeze();
        return theme;
    }

    private static ControlStyle<Control> BuildBaseStyle(IReadOnlyDictionary<ColorRole, Color> roles)
    {
        ControlStyle<Control> style = new();
        Color foreground = roles[ColorRole.Foreground];
        Color background = roles[ColorRole.Background];
        Color border = roles[ColorRole.Border];
        Color accent = roles[ColorRole.Accent];
        Color selectionBackground = roles[ColorRole.SelectionBackground];
        Color selectionForeground = roles[ColorRole.SelectionForeground];
        Color muted = roles[ColorRole.Muted];

        style.Set(Control.ForegroundProperty, State.Normal, foreground);
        style.Set(Control.BackgroundProperty, State.Normal, background);
        style.Set(Control.BorderColorProperty, State.Normal, border);
        style.Set(Control.ForegroundProperty, State.Hovered, accent);
        style.Set(Control.AttributesProperty, State.Focused, TerminalAttributes.Underline);
        style.Set(Control.ForegroundProperty, State.Checked, selectionForeground);
        style.Set(Control.BackgroundProperty, State.Checked, selectionBackground);
        style.Set(Control.ForegroundProperty, State.Selected, selectionForeground);
        style.Set(Control.BackgroundProperty, State.Selected, selectionBackground);
        style.Set(Control.ForegroundProperty, State.Disabled, muted);
        style.Set(Control.ShadowForegroundProperty, State.Normal, border);

        return style;
    }
}
```

Note the property types:
`ForegroundProperty`/`BackgroundProperty`/`BorderColorProperty`/`ShadowForegroundProperty`
are `StyleProperty<Color?>`; passing a non-null `Color` is valid.
`AttributesProperty` is `StyleProperty<TerminalAttributes?>`.

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeBuilderTests" --timeout 60s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Styling/ThemeBuilder.cs tests/SharpVision.Tests/Styling/ThemeBuilderTests.cs
git commit -m "feat(styling): add theme builder producing frozen themes from roles"
```

---

### Task 6: `ThemeLoader.FromDefinition` — resolve palette, roles, fallbacks

**Files:**

- Modify: `src/SharpVision/Styling/ThemeLoader.cs`
- Test: `tests/SharpVision.Tests/Styling/ThemeLoaderTests.cs`

**Interfaces:**

- Consumes: `ThemeDefinition`, `ThemeColorValue`, `ThemeBuilder.Build`.
- Produces:
  `static Theme ThemeLoader.FromDefinition(ThemeDefinition definition, string source)`
  (throws `InvalidDataException`). Also
  `static Theme FromJson(string json, string source)` chaining `Deserialize` +
  `FromDefinition`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Styling/ThemeLoaderTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies role resolution, fallbacks, and failure modes of the theme loader.</summary>
public sealed class ThemeLoaderTests
{
    private static string Json(string roles, string palette = "\"bg\": \"#101010\", \"fg\": \"#e0e0e0\"") =>
        $$"""
          { "name": "T", "slug": "t", "colorScheme": "dark", "order": 1,
            "author": "A", "license": "MIT", "source": "s",
            "palette": { {{palette}} }, "roles": { {{roles}} } }
          """;

    [Fact]
    public void FromJson_WhenPaletteKeyAndInlineHexAndIndex_Resolves()
    {
        Theme theme = ThemeLoader.FromJson(
            Json("\"background\": \"bg\", \"foreground\": \"fg\", \"accent\": \"#ff8800\", \"border\": \"idx:8\""),
            "t");

        theme.TryGetColor(ColorRole.Background, out Color bg).ShouldBeTrue();
        bg.ShouldBe(Color.Rgb(0x10, 0x10, 0x10));
        theme.TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
        accent.ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
        theme.TryGetColor(ColorRole.Border, out Color border).ShouldBeTrue();
        border.ShouldBe(Color.Indexed(8));
    }

    [Fact]
    public void FromJson_WhenOnlyBackgroundAndForeground_FillsFallbacks()
    {
        Theme theme = ThemeLoader.FromJson(
            Json("\"background\": \"bg\", \"foreground\": \"fg\""), "t");

        // accent -> foreground; surface -> background; border/muted -> foreground; selection -> accent(=fg)
        theme.TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
        accent.ShouldBe(Color.Rgb(0xe0, 0xe0, 0xe0));
        theme.TryGetColor(ColorRole.Surface, out Color surface).ShouldBeTrue();
        surface.ShouldBe(Color.Rgb(0x10, 0x10, 0x10));
        theme.TryGetColor(ColorRole.Info, out Color info).ShouldBeTrue();
        info.ShouldBe(accent);
    }

    [Fact]
    public void FromJson_WhenBorderPresentMutedAbsent_MutedTakesBorder()
    {
        Theme theme = ThemeLoader.FromJson(
            Json("\"background\": \"bg\", \"foreground\": \"fg\", \"border\": \"#123456\""), "t");

        theme.TryGetColor(ColorRole.Muted, out Color muted).ShouldBeTrue();
        muted.ShouldBe(Color.Rgb(0x12, 0x34, 0x56));
    }

    [Theory]
    [InlineData("\"foreground\": \"fg\"")]                                   // missing background
    [InlineData("\"background\": \"bg\"")]                                   // missing foreground
    [InlineData("\"background\": \"bg\", \"foreground\": \"missing\"")]       // unknown palette key
    [InlineData("\"background\": \"bg\", \"foreground\": \"#zz\"")]           // bad hex
    [InlineData("\"background\": \"bg\", \"foreground\": \"fg\", \"nope\": \"fg\"")] // unknown role
    public void FromJson_WhenInvalid_Throws(string roles) =>
        Should.Throw<InvalidDataException>(() => ThemeLoader.FromJson(Json(roles), "t"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeLoaderTests" --timeout 60s`
Expected: FAIL — `FromDefinition`/`FromJson` missing.

- [ ] **Step 3: Write minimal implementation** — add to `ThemeLoader.cs`:

```csharp
    private static readonly IReadOnlyDictionary<string, ColorRole> _roleNames = new Dictionary<string, ColorRole>(StringComparer.Ordinal)
    {
        ["foreground"] = ColorRole.Foreground,
        ["background"] = ColorRole.Background,
        ["surface"] = ColorRole.Surface,
        ["border"] = ColorRole.Border,
        ["accent"] = ColorRole.Accent,
        ["muted"] = ColorRole.Muted,
        ["selectionBackground"] = ColorRole.SelectionBackground,
        ["selectionForeground"] = ColorRole.SelectionForeground,
        ["error"] = ColorRole.Error,
        ["warning"] = ColorRole.Warning,
        ["success"] = ColorRole.Success,
        ["info"] = ColorRole.Info,
    };

    /// <summary>Deserializes and builds a frozen theme from JSON.</summary>
    /// <param name="json">The theme JSON text.</param>
    /// <param name="source">A label (slug or path) used in error messages.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="InvalidDataException">The JSON is malformed or the definition is invalid.</exception>
    public static Theme FromJson(string json, string source) =>
        FromDefinition(Deserialize(json, source), source);

    /// <summary>Resolves a definition's palette and roles and builds a frozen theme.</summary>
    /// <param name="definition">The deserialized definition.</param>
    /// <param name="source">A label (slug or path) used in error messages.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="InvalidDataException">A required role is missing or a value cannot be resolved.</exception>
    public static Theme FromDefinition(ThemeDefinition definition, string source)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Dictionary<string, Color> palette = ResolvePalette(definition, source);
        Dictionary<ColorRole, Color> roles = ResolveRoles(definition, palette, source);
        FillFallbacks(roles, source);
        return ThemeBuilder.Build(roles);
    }

    private static Dictionary<string, Color> ResolvePalette(ThemeDefinition definition, string source)
    {
        Dictionary<string, Color> palette = new(StringComparer.Ordinal);

        if (definition.Palette is null)
        {
            return palette;
        }

        foreach (KeyValuePair<string, string> entry in definition.Palette)
        {
            if (!ThemeColorValue.IsLiteral(entry.Value))
            {
                throw new InvalidDataException(
                    $"Theme '{source}' palette entry '{entry.Key}' must be a #hex or idx:N value.");
            }

            palette[entry.Key] = ParseOrThrow(entry.Value, source, $"palette entry '{entry.Key}'");
        }

        return palette;
    }

    private static Dictionary<ColorRole, Color> ResolveRoles(
        ThemeDefinition definition,
        IReadOnlyDictionary<string, Color> palette,
        string source)
    {
        Dictionary<ColorRole, Color> roles = [];

        if (definition.Roles is null)
        {
            return roles;
        }

        foreach (KeyValuePair<string, string> entry in definition.Roles)
        {
            if (!_roleNames.TryGetValue(entry.Key, out ColorRole role))
            {
                throw new InvalidDataException($"Theme '{source}' has unknown role '{entry.Key}'.");
            }

            if (ThemeColorValue.IsLiteral(entry.Value))
            {
                roles[role] = ParseOrThrow(entry.Value, source, $"role '{entry.Key}'");
            }
            else if (palette.TryGetValue(entry.Value, out Color color))
            {
                roles[role] = color;
            }
            else
            {
                throw new InvalidDataException(
                    $"Theme '{source}' role '{entry.Key}' references unknown palette key '{entry.Value}'.");
            }
        }

        return roles;
    }

    private static void FillFallbacks(Dictionary<ColorRole, Color> roles, string source)
    {
        if (!roles.ContainsKey(ColorRole.Background) || !roles.ContainsKey(ColorRole.Foreground))
        {
            throw new InvalidDataException(
                $"Theme '{source}' must define both 'background' and 'foreground'.");
        }

        // Fixed order so the Border/Muted cross-reference terminates at a required role.
        Fallback(roles, ColorRole.Accent, ColorRole.Foreground);
        Fallback(roles, ColorRole.Muted, ColorRole.Foreground); // explicit Border, if any, wins next line
        if (roles.ContainsKey(ColorRole.Border))
        {
            roles[ColorRole.Muted] = roles.TryGetValue(ColorRole.Muted, out Color existingMuted) && HadExplicitMuted(roles)
                ? existingMuted
                : roles[ColorRole.Border];
        }

        Fallback(roles, ColorRole.Border, ColorRole.Muted);
        Fallback(roles, ColorRole.Surface, ColorRole.Background);
        Fallback(roles, ColorRole.SelectionBackground, ColorRole.Accent);
        Fallback(roles, ColorRole.SelectionForeground, ColorRole.Foreground);
        Fallback(roles, ColorRole.Error, ColorRole.Accent);
        Fallback(roles, ColorRole.Warning, ColorRole.Accent);
        Fallback(roles, ColorRole.Success, ColorRole.Accent);
        Fallback(roles, ColorRole.Info, ColorRole.Accent);
    }

    private static bool HadExplicitMuted(IReadOnlyDictionary<ColorRole, Color> roles) => false;

    private static void Fallback(Dictionary<ColorRole, Color> roles, ColorRole target, ColorRole source)
    {
        if (!roles.ContainsKey(target))
        {
            roles[target] = roles[source];
        }
    }

    private static Color ParseOrThrow(string value, string source, string where)
    {
        try
        {
            return ThemeColorValue.ParseLiteral(value);
        }
        catch (FormatException error)
        {
            throw new InvalidDataException($"Theme '{source}' {where} has invalid color '{value}'.", error);
        }
    }
```

> **Implementer note on Border/Muted:** the snippet above is over-complicated.
> Replace the `Muted`/`Border` block with the clean fixed-order below (matches
> spec §4.2), and delete `HadExplicitMuted`:
>
> ```csharp
> // Muted first: takes explicit Border if present, else Foreground.
> if (!roles.ContainsKey(ColorRole.Muted))
> {
>     roles[ColorRole.Muted] = roles.TryGetValue(ColorRole.Border, out Color border)
>         ? border
>         : roles[ColorRole.Foreground];
> }
>
> // Border then resolves to (now-present) Muted.
> Fallback(roles, ColorRole.Border, ColorRole.Muted);
> ```
>
> Keep the `Accent → Foreground` fallback _before_ this block
> (SelectionBackground/status depend on Accent). Final order: Accent, Muted,
> Border, Surface, SelectionBackground, SelectionForeground, Error, Warning,
> Success, Info.

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeLoaderTests" --timeout 60s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Styling/ThemeLoader.cs tests/SharpVision.Tests/Styling/ThemeLoaderTests.cs
git commit -m "feat(styling): resolve theme palette, roles, and fallbacks"
```

---

### Task 7: Public `ThemeFile` loader

**Files:**

- Create: `src/SharpVision/Styling/ThemeFile.cs`
- Test: `tests/SharpVision.Tests/Styling/ThemeFileTests.cs`

**Interfaces:**

- Consumes: `ThemeLoader.FromJson`.
- Produces: `public static class ThemeFile` with
  `static Theme Parse(string json)`, `static Theme Load(Stream stream)`,
  `static Theme LoadFile(string path)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/SharpVision.Tests/Styling/ThemeFileTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the public runtime theme-file loader.</summary>
public sealed class ThemeFileTests
{
    private const string _json = """
        { "name": "Ext", "slug": "ext", "colorScheme": "dark", "order": 1,
          "author": "A", "license": "MIT", "source": "s",
          "palette": { "bg": "#101020", "fg": "#f0f0ff" },
          "roles": { "background": "bg", "foreground": "fg", "accent": "#77aaff" } }
        """;

    [Fact]
    public void Parse_WhenValid_ReturnsFrozenTheme()
    {
        Theme theme = ThemeFile.Parse(_json);

        theme.IsFrozen.ShouldBeTrue();
        theme.TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
        accent.ShouldBe(Color.Rgb(0x77, 0xaa, 0xff));
    }

    [Fact]
    public void Load_WhenStream_ReturnsFrozenTheme()
    {
        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(_json));

        Theme theme = ThemeFile.Load(stream);

        theme.TryGetColor(ColorRole.Background, out Color bg).ShouldBeTrue();
        bg.ShouldBe(Color.Rgb(0x10, 0x10, 0x20));
    }

    [Fact]
    public void Parse_WhenNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ThemeFile.Parse(null!));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeFileTests" --timeout 60s`
Expected: FAIL — `ThemeFile` missing.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/SharpVision/Styling/ThemeFile.cs
namespace SharpVision.Styling;

/// <summary>Loads themes from JSON text, streams, or files at runtime.</summary>
public static class ThemeFile
{
    /// <summary>Parses a theme from JSON text.</summary>
    /// <param name="json">The theme JSON.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="InvalidDataException">The JSON is malformed or invalid.</exception>
    public static Theme Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return ThemeLoader.FromJson(json, "<parsed>");
    }

    /// <summary>Loads a theme from a UTF-8 JSON stream. The caller owns the stream.</summary>
    /// <param name="stream">The readable JSON stream.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">The content is malformed or invalid.</exception>
    public static Theme Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using StreamReader reader = new(stream, leaveOpen: true);
        return ThemeLoader.FromJson(reader.ReadToEnd(), "<stream>");
    }

    /// <summary>Loads a theme from a JSON file path.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The frozen theme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The content is malformed or invalid.</exception>
    public static Theme LoadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ThemeLoader.FromJson(File.ReadAllText(path), path);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeFileTests" --timeout 60s`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Styling/ThemeFile.cs tests/SharpVision.Tests/Styling/ThemeFileTests.cs
git commit -m "feat(styling): add public ThemeFile runtime loader"
```

---

### Task 8: `ThemeCatalog` + built-in default theme resources

**Files:**

- Create: `src/SharpVision/Styling/ColorScheme.cs`
- Create: `src/SharpVision/Styling/ThemeCatalogEntry.cs`
- Create: `src/SharpVision/Styling/ThemeCatalog.cs`
- Create: `src/SharpVision/Styling/Themes/default-dark.theme.json`
- Create: `src/SharpVision/Styling/Themes/default-light.theme.json`
- Modify: `src/SharpVision/SharpVision.csproj` (embed the theme glob)
- Test: `tests/SharpVision.Tests/Styling/ThemeCatalogTests.cs`

**Interfaces:**

- Consumes: `ThemeLoader.FromJson`, `ThemeLoader.Deserialize`.
- Produces: `public enum ColorScheme { Dark, Light }`;
  `public sealed class ThemeCatalogEntry` with read-only `Name`, `Slug`,
  `ColorScheme` (ColorScheme), `Author`, `License`, `Source`;
  `public sealed class ThemeCatalog` with `static ThemeCatalog Default`,
  `IReadOnlyList<ThemeCatalogEntry> Entries`, `IReadOnlyList<string> Slugs`,
  `Theme Load(string slug)`.

- [ ] **Step 1: Author the two default theme files** (indexed values reproduce
      today's built-ins exactly):

`src/SharpVision/Styling/Themes/default-dark.theme.json`:

```json
{
  "name": "Dark",
  "slug": "default-dark",
  "colorScheme": "dark",
  "order": 0,
  "author": "SharpVision",
  "license": "MIT",
  "source": "https://github.com/sharpvision/sharpvision",
  "palette": {},
  "roles": {
    "foreground": "idx:15",
    "background": "idx:0",
    "surface": "idx:8",
    "border": "idx:8",
    "accent": "idx:14",
    "muted": "idx:8",
    "selectionBackground": "idx:4",
    "selectionForeground": "idx:15",
    "error": "idx:9",
    "warning": "idx:11",
    "success": "idx:10",
    "info": "idx:12"
  }
}
```

`src/SharpVision/Styling/Themes/default-light.theme.json`:

```json
{
  "name": "Light",
  "slug": "default-light",
  "colorScheme": "light",
  "order": 1,
  "author": "SharpVision",
  "license": "MIT",
  "source": "https://github.com/sharpvision/sharpvision",
  "palette": {},
  "roles": {
    "foreground": "idx:0",
    "background": "idx:15",
    "surface": "idx:7",
    "border": "idx:8",
    "accent": "idx:4",
    "muted": "idx:8",
    "selectionBackground": "idx:4",
    "selectionForeground": "idx:15",
    "error": "idx:1",
    "warning": "idx:3",
    "success": "idx:2",
    "info": "idx:4"
  }
}
```

- [ ] **Step 2: Embed the theme glob** — add to
      `src/SharpVision/SharpVision.csproj` inside a new `<ItemGroup>`:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Styling/Themes/*.theme.json">
      <LogicalName>SharpVision.Styling.Themes.%(Filename)%(Extension)</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
```

- [ ] **Step 3: Write the failing test**

```csharp
// tests/SharpVision.Tests/Styling/ThemeCatalogTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies the embedded theme catalog discovers, orders, loads, and caches themes.</summary>
public sealed class ThemeCatalogTests
{
    [Fact]
    public void Default_ContainsBuiltInDefaults()
    {
        ThemeCatalog catalog = ThemeCatalog.Default;

        catalog.Slugs.ShouldContain("default-dark");
        catalog.Slugs.ShouldContain("default-light");
    }

    [Fact]
    public void Entries_AreOrderedByOrderThenSlug()
    {
        IReadOnlyList<ThemeCatalogEntry> entries = ThemeCatalog.Default.Entries;

        entries[0].Slug.ShouldBe("default-dark"); // order 0
        entries[1].Slug.ShouldBe("default-light"); // order 1
    }

    [Fact]
    public void Load_WhenDefaultDark_ReproducesIndexedRoles()
    {
        Theme theme = ThemeCatalog.Default.Load("default-dark");

        theme.IsFrozen.ShouldBeTrue();
        theme.TryGetColor(ColorRole.Foreground, out Color fg).ShouldBeTrue();
        fg.ShouldBe(Color.Indexed(15));
        theme.TryGetColor(ColorRole.Background, out Color bg).ShouldBeTrue();
        bg.ShouldBe(Color.Indexed(0));
    }

    [Fact]
    public void Load_WhenCalledTwice_ReturnsSameInstance()
    {
        Theme first = ThemeCatalog.Default.Load("default-dark");
        Theme second = ThemeCatalog.Default.Load("default-dark");

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Fact]
    public void Load_WhenUnknownSlug_Throws() =>
        Should.Throw<KeyNotFoundException>(() => ThemeCatalog.Default.Load("nope"));
}
```

- [ ] **Step 4: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeCatalogTests" --timeout 60s`
Expected: FAIL — catalog types missing.

- [ ] **Step 5: Write minimal implementation**

```csharp
// src/SharpVision/Styling/ColorScheme.cs
namespace SharpVision.Styling;

/// <summary>Identifies whether a theme is designed for a dark or light background (CSS color-scheme naming).</summary>
public enum ColorScheme
{
    /// <summary>A dark-background theme.</summary>
    Dark,

    /// <summary>A light-background theme.</summary>
    Light,
}
```

```csharp
// src/SharpVision/Styling/ThemeCatalogEntry.cs
namespace SharpVision.Styling;

/// <summary>Immutable metadata for one embedded theme, independent of loading it.</summary>
public sealed class ThemeCatalogEntry
{
    /// <summary>Initializes a catalog entry.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="slug">The stable catalog key.</param>
    /// <param name="colorScheme">The dark/light color scheme.</param>
    /// <param name="author">The attribution author.</param>
    /// <param name="license">The license identifier.</param>
    /// <param name="source">The source URL.</param>
    /// <exception cref="ArgumentException">A required string is null or empty.</exception>
    public ThemeCatalogEntry(string name, string slug, ColorScheme colorScheme, string author, string license, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(license);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Name = name;
        Slug = slug;
        ColorScheme = colorScheme;
        Author = author;
        License = license;
        Source = source;
    }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the stable catalog key.</summary>
    public string Slug { get; }

    /// <summary>Gets the dark/light color scheme.</summary>
    public ColorScheme ColorScheme { get; }

    /// <summary>Gets the attribution author.</summary>
    public string Author { get; }

    /// <summary>Gets the license identifier.</summary>
    public string License { get; }

    /// <summary>Gets the source URL.</summary>
    public string Source { get; }
}
```

```csharp
// src/SharpVision/Styling/ThemeCatalog.cs
namespace SharpVision.Styling;

using System.Reflection;

/// <summary>Discovers and loads the embedded theme resources shipped with SharpVision.</summary>
public sealed class ThemeCatalog
{
    private const string _prefix = "SharpVision.Styling.Themes.";
    private const string _suffix = ".theme.json";
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _json = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Theme> _cache = new(StringComparer.Ordinal);

    private ThemeCatalog()
    {
        Assembly assembly = typeof(ThemeCatalog).Assembly;
        List<ThemeCatalogEntry> entries = [];

        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(_prefix, StringComparison.Ordinal) ||
                !resource.EndsWith(_suffix, StringComparison.Ordinal))
            {
                continue;
            }

            string json = ReadResource(assembly, resource);
            ThemeDefinition definition = ThemeLoader.Deserialize(json, resource);
            ThemeCatalogEntry entry = ToEntry(definition, resource);

            if (!_json.TryAdd(entry.Slug, json))
            {
                throw new InvalidDataException($"Duplicate theme slug '{entry.Slug}'.");
            }

            entries.Add(entry);
        }

        entries.Sort(static (left, right) =>
        {
            int byOrder = ByOrder(left.Slug).CompareTo(ByOrder(right.Slug));
            return byOrder != 0 ? byOrder : string.CompareOrdinal(left.Slug, right.Slug);
        });

        Entries = entries;
        Slugs = entries.ConvertAll(static e => e.Slug);

        int ByOrder(string slug) => _orders[slug];
    }

    /// <summary>Gets the process-wide embedded theme catalog.</summary>
    public static ThemeCatalog Default { get; } = new();

    /// <summary>Gets the theme metadata entries ordered by (order, slug).</summary>
    public IReadOnlyList<ThemeCatalogEntry> Entries { get; }

    /// <summary>Gets the ordered theme slugs.</summary>
    public IReadOnlyList<string> Slugs { get; }

    /// <summary>Loads and freezes one theme by slug, caching the result.</summary>
    /// <param name="slug">The catalog slug.</param>
    /// <returns>The frozen theme; the same instance on repeated calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slug"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The slug is not in the catalog.</exception>
    public Theme Load(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        lock (_gate)
        {
            if (_cache.TryGetValue(slug, out Theme? cached))
            {
                return cached;
            }

            if (!_json.TryGetValue(slug, out string? json))
            {
                throw new KeyNotFoundException($"The theme catalog does not contain '{slug}'.");
            }

            Theme theme = ThemeLoader.FromJson(json, slug);
            _cache[slug] = theme;
            return theme;
        }
    }

    private readonly Dictionary<string, int> _orders = new(StringComparer.Ordinal);

    private ThemeCatalogEntry ToEntry(ThemeDefinition definition, string resource)
    {
        string slug = Require(definition.Slug, resource, "slug");
        _orders[slug] = definition.Order;
        return new ThemeCatalogEntry(
            Require(definition.Name, resource, "name"),
            slug,
            ParseColorScheme(definition.ColorScheme, resource),
            Require(definition.Author, resource, "author"),
            Require(definition.License, resource, "license"),
            Require(definition.Source, resource, "source"));
    }

    private static ColorScheme ParseColorScheme(string? value, string resource) => value switch
    {
        "dark" => ColorScheme.Dark,
        "light" => ColorScheme.Light,
        _ => throw new InvalidDataException($"Theme '{resource}' has invalid colorScheme '{value}'."),
    };

    private static string Require(string? value, string resource, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"Theme '{resource}' is missing required field '{field}'.")
            : value;

    private static string ReadResource(Assembly assembly, string name)
    {
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"Embedded theme resource '{name}' is missing.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
```

> **Implementer note:** the `_orders` field is declared after the constructor
> uses it — move `private readonly Dictionary<string, int> _orders = ...;` up
> with the other fields, and reorder members so fields precede the constructor.
> The local `ByOrder` in the constructor reads `_orders`, which is populated
> during `ToEntry` in the same loop before the sort. Keep members in the
> standard order (fields, constructor, properties, methods); this note only
> flags the drafting artifact.

- [ ] **Step 6: Run test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeCatalogTests" --timeout 60s`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/SharpVision/Styling/ColorScheme.cs src/SharpVision/Styling/ThemeCatalogEntry.cs src/SharpVision/Styling/ThemeCatalog.cs src/SharpVision/Styling/Themes/ src/SharpVision/SharpVision.csproj tests/SharpVision.Tests/Styling/ThemeCatalogTests.cs
git commit -m "feat(styling): add embedded ThemeCatalog with built-in default themes"
```

---

### Task 9: Make `Themes.White`/`Dark` catalog-backed

**Files:**

- Modify: `src/SharpVision/Styling/Themes.cs`
- Test: existing `tests/SharpVision.Tests/Styling/StandardThemeTests.cs`,
  `ColorRoleTests.cs` must still pass; add one identity test.

**Interfaces:**

- Consumes: `ThemeCatalog.Default.Load`.
- Produces: unchanged public surface `Themes.White`, `Themes.Dark`.

- [ ] **Step 1: Add the failing identity test** — append to
      `StandardThemeTests`:

```csharp
    [Fact]
    public void Themes_AreCachedFrozenInstances()
    {
        Themes.Dark.IsFrozen.ShouldBeTrue();
        ReferenceEquals(Themes.Dark, Themes.Dark).ShouldBeTrue();
        ReferenceEquals(Themes.White, Themes.Dark).ShouldBeFalse();
    }
```

Add `using Shouldly;` after the namespace in `StandardThemeTests.cs` if not
already present (the file relies on global usings; confirm build).

- [ ] **Step 2: Run test to verify it fails/builds**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*StandardThemeTests" --timeout 60s`
Expected: PASS currently (old hardcoded themes are frozen). This test guards
behavior across the refactor.

- [ ] **Step 3: Replace `Themes.cs` body** with catalog-backed properties:

```csharp
// src/SharpVision/Styling/Themes.cs
namespace SharpVision.Styling;

/// <summary>Exposes the frozen built-in standard themes, loaded from embedded JSON resources.</summary>
public static class Themes
{
    /// <summary>Gets the frozen light standard theme.</summary>
    public static Theme White { get; } = ThemeCatalog.Default.Load("default-light");

    /// <summary>Gets the frozen dark standard theme.</summary>
    public static Theme Dark { get; } = ThemeCatalog.Default.Load("default-dark");
}
```

Delete the old `CreateWhite`/`CreateDark`/`ApplyColors`/`CreateBaseControlStyle`
code and the now-unused `using SharpVision.Terminal.Protocols;`.

- [ ] **Step 4: Run the full styling suite**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*Styling*" --timeout 120s`
Expected: PASS — `StandardThemeTests` (indexed fg/bg/border), `ColorRoleTests`
(white accent idx:4 ≠ dark accent idx:14), and the new identity test all pass.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Styling/Themes.cs tests/SharpVision.Tests/Styling/StandardThemeTests.cs
git commit -m "refactor(styling): back built-in Light/Dark themes with JSON catalog"
```

---

### Task 10: Add the curated ~10 editor themes

**Files:**

- Create: `src/SharpVision/Styling/Themes/<slug>.theme.json` for each theme
  below.
- Test: `tests/SharpVision.Tests/Styling/CuratedThemesTests.cs`

**Interfaces:**

- Consumes: `ThemeCatalog.Default`.
- Produces: catalog slugs listed below.

Curated set and slugs (order values leave gaps for future insertion):

| Slug                | Name              | colorScheme | order |
| ------------------- | ----------------- | ----------- | ----- |
| `tokyo-night`       | Tokyo Night       | dark        | 10    |
| `tokyo-night-storm` | Tokyo Night Storm | dark        | 11    |
| `tokyo-night-day`   | Tokyo Night Day   | light       | 12    |
| `catppuccin-mocha`  | Catppuccin Mocha  | dark        | 20    |
| `catppuccin-latte`  | Catppuccin Latte  | light       | 21    |
| `gruvbox-dark`      | Gruvbox Dark      | dark        | 30    |
| `gruvbox-light`     | Gruvbox Light     | light       | 31    |
| `dracula`           | Dracula           | dark        | 40    |
| `nord`              | Nord              | dark        | 50    |
| `monokai`           | Monokai           | dark        | 60    |
| `solarized-dark`    | Solarized Dark    | dark        | 70    |
| `solarized-light`   | Solarized Light   | light       | 71    |
| `one-dark`          | One Dark          | dark        | 80    |

> **Palette accuracy:** transcribe hex values from each project's official
> source (recorded in `source`). Two fully-worked canonical examples are given
> below; author the rest with the same structure. After authoring, the
> `CuratedThemesTests` count assertion is the guardrail.

- [ ] **Step 1: Author each theme file.** Role mapping template (use per theme):
      `background←bg`, `foreground←fg`, `surface←a slightly raised bg`,
      `border←a muted/overlay color`, `muted←comment/subtle`,
      `accent←the signature color (blue/purple/etc.)`,
      `selectionBackground←the editor selection bg`, `selectionForeground←fg`,
      `error←red`, `warning←yellow/orange`, `success←green`, `info←blue/cyan`.

Canonical example — `dracula.theme.json`
([source](https://github.com/dracula/dracula-theme)):

```json
{
  "name": "Dracula",
  "slug": "dracula",
  "colorScheme": "dark",
  "order": 40,
  "author": "Dracula Theme",
  "license": "MIT",
  "source": "https://github.com/dracula/dracula-theme",
  "palette": {
    "bg": "#282a36",
    "current": "#44475a",
    "fg": "#f8f8f2",
    "comment": "#6272a4",
    "cyan": "#8be9fd",
    "green": "#50fa7b",
    "orange": "#ffb86c",
    "pink": "#ff79c6",
    "purple": "#bd93f9",
    "red": "#ff5555",
    "yellow": "#f1fa8c"
  },
  "roles": {
    "background": "bg",
    "foreground": "fg",
    "surface": "current",
    "border": "comment",
    "muted": "comment",
    "accent": "purple",
    "selectionBackground": "current",
    "selectionForeground": "fg",
    "error": "red",
    "warning": "orange",
    "success": "green",
    "info": "cyan"
  }
}
```

Canonical example — `nord.theme.json`
([source](https://github.com/nordtheme/nord)):

```json
{
  "name": "Nord",
  "slug": "nord",
  "colorScheme": "dark",
  "order": 50,
  "author": "Sven Greb",
  "license": "MIT",
  "source": "https://github.com/nordtheme/nord",
  "palette": {
    "nord0": "#2e3440",
    "nord1": "#3b4252",
    "nord2": "#434c5e",
    "nord3": "#4c566a",
    "nord4": "#d8dee9",
    "nord6": "#eceff4",
    "nord8": "#88c0d0",
    "nord10": "#5e81ac",
    "nord11": "#bf616a",
    "nord13": "#ebcb8b",
    "nord14": "#a3be8c"
  },
  "roles": {
    "background": "nord0",
    "foreground": "nord4",
    "surface": "nord1",
    "border": "nord3",
    "muted": "nord3",
    "accent": "nord8",
    "selectionBackground": "nord2",
    "selectionForeground": "nord6",
    "error": "nord11",
    "warning": "nord13",
    "success": "nord14",
    "info": "nord10"
  }
}
```

Tokyo Night is fully specified in the spec (§3); reuse it verbatim for
`tokyo-night.theme.json`. Author the remaining slugs from their official
sources: Tokyo Night Storm/Day (folke/tokyonight.nvim), Catppuccin Mocha/Latte
(catppuccin/catppuccin, MIT), Gruvbox Dark/Light (morhetz/gruvbox, MIT), Monokai
(classic Monokai palette), Solarized Dark/Light (altercation/solarized, MIT),
One Dark (atom/one-dark-syntax / joshdick/onedark.vim, MIT).

- [ ] **Step 2: Write the guardrail test**

```csharp
// tests/SharpVision.Tests/Styling/CuratedThemesTests.cs
namespace SharpVision.Tests.Styling;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

/// <summary>Verifies every embedded theme loads and the curated set is complete.</summary>
public sealed class CuratedThemesTests
{
    private static readonly string[] _expected =
    [
        "default-dark", "default-light", "tokyo-night", "tokyo-night-storm",
        "tokyo-night-day", "catppuccin-mocha", "catppuccin-latte", "gruvbox-dark",
        "gruvbox-light", "dracula", "nord", "monokai", "solarized-dark",
        "solarized-light", "one-dark",
    ];

    [Fact]
    public void Catalog_ContainsExactlyTheCuratedSet()
    {
        ThemeCatalog.Default.Slugs.OrderBy(static s => s, StringComparer.Ordinal)
            .ShouldBe(_expected.OrderBy(static s => s, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryTheme_LoadsFrozenWithAllRoles()
    {
        foreach (string slug in ThemeCatalog.Default.Slugs)
        {
            Theme theme = ThemeCatalog.Default.Load(slug);
            theme.IsFrozen.ShouldBeTrue();

            foreach (ColorRole role in Enum.GetValues<ColorRole>())
            {
                theme.TryGetColor(role, out _).ShouldBeTrue($"{slug} missing {role}");
            }
        }
    }

    [Fact]
    public void EditorThemes_UseRgbAccents()
    {
        ThemeCatalog.Default.Load("dracula").TryGetColor(ColorRole.Accent, out Color accent).ShouldBeTrue();
        accent.Kind.ShouldBe(ColorKind.Rgb);
    }
}
```

- [ ] **Step 3: Run test to verify it fails, then passes as files are added**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*CuratedThemesTests" --timeout 60s`
Expected: FAIL until all files exist; PASS once the set matches.

- [ ] **Step 4: Commit**

```bash
git add src/SharpVision/Styling/Themes/ tests/SharpVision.Tests/Styling/CuratedThemesTests.cs
git commit -m "feat(styling): add curated editor theme resources"
```

---

### Task 11: Showcase picker + Theming pane

**Files:**

- Modify: `src/SharpVision.Showcase/Gallery.cs` (lines ~48-53 catalog array,
  ~106-113 picker, ~285-303 SetTheme/OnThemeSelected)
- Modify: `src/SharpVision.Showcase/Panes/ThemingShowcasePane.cs`
- Test: `tests/SharpVision.Showcase.Tests/ThemeGalleryTests.cs`

**Interfaces:**

- Consumes: `ThemeCatalog.Default`, `Application.Theme`.
- Produces: picker populated from the catalog; selecting an entry publishes
  `ThemeCatalog.Default.Load(slug)`.

- [ ] **Step 1: Update the gallery test** — replace
      `Theme_WhenLightIsSelected_PublishesWhiteThemeAsync` selection to use the
      display name from the catalog and assert against the loaded theme:

```csharp
    [Fact]
    public async Task Theme_WhenLightIsSelected_PublishesLightThemeAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(100, 30)));
        using Gallery gallery = new();
        await using Application application = new(gallery, terminal, terminal, TerminalOptions.Minimal);
        gallery.Attach(application);
        await application.StartAsync(TestContext.Current.CancellationToken);

        ComboBox themePicker = (await application.Dispatcher.InvokeAsync(
            () => Find<ComboBox>(gallery.Sidebar, static _ => true),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                int light = themePicker.Items.ToList().IndexOf("Light");
                light.ShouldBeGreaterThanOrEqualTo(0);
                themePicker.SelectedIndex = light;
            },
            TestContext.Current.CancellationToken);

        await WaitUntilAsync(
            () => ReferenceEquals(application.Theme, Themes.White),
            application,
            "Light theme selection");

        await application.StopAsync(TestContext.Current.CancellationToken);
    }
```

(`Themes.White` is now `ThemeCatalog.Default.Load("default-light")` — the same
cached instance, so `ReferenceEquals` holds. The display name "Light" comes from
`default-light.theme.json`.)

- [ ] **Step 2: Run test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Showcase.Tests --filter-class "*ThemeGalleryTests" --timeout 120s`
Expected: FAIL — the gallery still uses the old hardcoded array (names/wiring
differ) until Step 3.

- [ ] **Step 3: Rewire the gallery.** In `Gallery.cs`:

Replace the `ThemeCatalog` field (lines ~48-53) — remove the local tuple array;
add a slug lookup built from the catalog:

```csharp
    // Ordered themes surfaced by the sidebar picker, sourced from the embedded catalog.
    private static readonly IReadOnlyList<ThemeCatalogEntry> _themeEntries = OrderForPicker(SharpVision.Styling.ThemeCatalog.Default.Entries);
```

Add a helper (dark group first, then light, preserving catalog order within
each):

```csharp
    private static IReadOnlyList<ThemeCatalogEntry> OrderForPicker(IReadOnlyList<ThemeCatalogEntry> entries)
    {
        List<ThemeCatalogEntry> dark = [];
        List<ThemeCatalogEntry> light = [];

        foreach (ThemeCatalogEntry entry in entries)
        {
            (entry.ColorScheme == ColorScheme.Dark ? dark : light).Add(entry);
        }

        dark.AddRange(light);
        return dark;
    }
```

Update the picker construction (lines ~106-113):

```csharp
        int darkIndex = _themeEntries.ToList().FindIndex(static entry => entry.Slug == "default-dark");
        _themePicker = new ControlComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Items = _themeEntries.Select(static entry => (object?) entry.Name).ToArray(),
            SelectedIndex = darkIndex >= 0 ? darkIndex : 0,
        };
```

Update `OnThemeSelected` (lines ~294-303):

```csharp
    private void OnThemeSelected(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        int index = _themePicker.SelectedIndex;

        if ((uint) index < (uint) _themeEntries.Count)
        {
            SetTheme(SharpVision.Styling.ThemeCatalog.Default.Load(_themeEntries[index].Slug));
        }
    }
```

Confirm `using SharpVision.Styling;` is present (or use the fully-qualified
names shown). Keep `SetTheme` and `OnAttach` (which sets `Themes.Dark`)
unchanged.

- [ ] **Step 4: Update the Theming pane** — in
      `ThemingShowcasePane.BuildExamples`, add a role-swatch section and a note
      about the file format. Append before the method's end:

```csharp
        ControlStack swatches = new() { Spacing = 0 };

        foreach (ColorRole role in Enum.GetValues<ColorRole>())
        {
            ControlText label = new($"{role}");
            ControlBorder chip = new()
            {
                Width = Length.Cells(4),
                BorderThickness = new Thickness(0),
                FillMode = FillMode.Opaque,
            };
            chip.SetValue(Control.BackgroundProperty, RoleColorOrDefault(role));
            swatches.Children.Add(new ControlStack
            {
                Orientation = Orientation.Horizontal,
                Spacing = 1,
                Children = { chip, label },
            });
        }

        examples.Children.Add(PaneSupport.SampleSection(
            "Theme roles",
            "Every theme resolves these semantic roles. Themes are JSON files (palette + role map) in SharpVision.Styling.Themes and load via ThemeCatalog or ThemeFile at runtime.",
            swatches));
```

Add the helper to the pane:

```csharp
    private static Color RoleColorOrDefault(ColorRole role) =>
        Application.Current?.Theme is { } theme && theme.TryGetColor(role, out Color color)
            ? color
            : Color.Default;
```

> **Implementer note:** verify the exact API for reading the active theme in the
> showcase (`Application.Current?.Theme`, or the pane's attached application).
> If a static `Application.Current` is not available, thread the swatch fill
> through the existing theme-change path the pane already uses; the key
> deliverable is a visible per-role swatch column. Adjust
> `ControlBorder`/`FillMode` usage to the real chrome API if the sampled call
> differs — the ButtonShowcasePane and ControlChrome are references for painting
> a solid cell.

- [ ] **Step 5: Run tests**

Run:
`dotnet test --project tests/SharpVision.Showcase.Tests --filter-class "*ThemeGalleryTests" --timeout 120s`
Expected: PASS (both gallery tests).

- [ ] **Step 6: Verify the app renders** — build and launch the showcase, open
      the Theming page, switch a few themes:

Run: `dotnet run --project src/SharpVision.Showcase` (or the repo's run skill).
Confirm the picker lists the catalog themes and switching repaints. Exit with
the Quit button / Ctrl+C.

- [ ] **Step 7: Commit**

```bash
git add src/SharpVision.Showcase/Gallery.cs src/SharpVision.Showcase/Panes/ThemingShowcasePane.cs tests/SharpVision.Showcase.Tests/ThemeGalleryTests.cs
git commit -m "feat(showcase): drive theme picker from the embedded ThemeCatalog"
```

---

### Task 12: Documentation

**Files:**

- Create: `docs/concepts/themes.md`
- Modify: the `ColorRole` and Theming references under `docs/` (locate via
  `grep -rl ColorRole docs`), `docs/architecture/showcase.md`, and any coverage
  matrix that lists themes.

**Interfaces:** none (docs only).

- [ ] **Step 1: Write `docs/concepts/themes.md`** — normative concept doc
      covering: the theme-file JSON format (fields, `palette`, `roles`), the
      color-value grammar (`#hex`, `idx:N`, palette key), the 12 `ColorRole`s
      and their fallback derivations (reproduce the §4.2 order), the base
      control-style recipe (§4.3 table), authoring a new theme (drop a
      `*.theme.json` into `Styling/Themes`), loading at runtime
      (`ThemeCatalog.Default.Load`, `ThemeFile.Parse/Load/LoadFile`), and the
      attribution/license policy. No TODO/TBD (AGENTS.md).

- [ ] **Step 2: Update references** — add the five new roles to the `ColorRole`
      reference; update the Theming control page and
      `docs/architecture/showcase.md` for the catalog-backed picker and the
      role-swatch section; update any theme coverage list to the curated set.

- [ ] **Step 3: Run doc gates**

Run: `make lint` Expected: no Markdown or link failures.

- [ ] **Step 4: Commit**

```bash
git add docs/
git commit -m "docs(styling): document theme-file format, roles, and catalog"
```

---

### Final verification

- [ ] Run the full repository gates:

```bash
make format && make lint && make build && make test
```

Expected: zero warnings, zero errors, all tests pass, no Markdown/link failures.

- [ ] Confirm `git status` shows only intended files; the branch
      `feat/themeable-palettes` holds the complete feature.

## Self-Review notes (author)

- **Spec coverage:** §1 hex→T1; §2 roles→T2; §3/§3.1 format & grammar→T3,T4; §4
  pipeline→T4,T5,T6; §5 catalog→T8; §6 runtime loading→T7; §7 built-ins→T8,T9;
  §8 showcase→T11; §9 tests→each task; §10 docs→T12. No uncovered section.
- **Type consistency:** `ThemeLoader.Deserialize/FromDefinition/FromJson`,
  `ThemeBuilder.Build`, `ThemeColorValue.IsLiteral/ParseLiteral`,
  `ThemeFile.Parse/Load/LoadFile`, `ThemeCatalog.Default/Entries/Slugs/Load`,
  `ThemeCatalogEntry.ColorScheme`, `ColorScheme` enum are used with identical
  names/signatures across tasks.
  `ColorRole.SelectionBackground`/`SelectionForeground` replace the old
  `Selection`.
- **Known drafting artifacts flagged inline** for the implementer: Task 6
  Border/Muted block (use the clean fixed-order note), Task 8 field/member
  ordering, Task 11 active-theme read API. These are called out, not left as
  silent placeholders.
