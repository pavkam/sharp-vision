# Theming System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace ancestor `Style`/`Appearance` inheritance with typed
`StyleProperty<T>`, type-keyed `Theme`, application-wide theme ownership, base
`Control` chrome, and frozen `Themes.White`/`Themes.Dark`.

**Architecture:** `StyleProperty<T>` metadata registers on declaring control
types with class defaults. `ControlStyle<TControl>` stores typed per-state
values; `Theme` holds one style per control type. An internal `ThemeContext` on
attached controls resolves: default → class default → theme chain
(Control→runtime type) → per-instance style → visual-state overlays → explicit
local value. `Application.Theme` publishes context changes and invalidates the
tree.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, existing
dispatcher/layout/canvas pipeline.

---

## File structure

### New files (`src/SharpVision/Styling/`)

| File                       | Responsibility                                                   |
| -------------------------- | ---------------------------------------------------------------- |
| `StyleProperty.cs`         | Generic immutable metadata, registration, class defaults         |
| `IStyleProperty.cs`        | Non-generic metadata contract for resolver                       |
| `StylePropertyRegistry.cs` | Internal per-type property and class-default storage             |
| `IControlStyle.cs`         | Non-generic style contract for `Control.Style` and theme storage |
| `ControlStyle.cs`          | Generic mutable style with typed Set/Remove/TryGet               |
| `Theme.cs`                 | Type-keyed style collection, freeze/clone, caching               |
| `ThemeResolver.cs`         | Typed value resolution through cascade and states                |
| `ThemeContext.cs`          | Internal snapshot + version attached to controls                 |
| `Themes.cs`                | Frozen `White` and `Dark` standard themes                        |
| `ThemeChangedEventArgs.cs` | Theme/style change notification payload                          |
| `FillMode.cs`              | Transparent vs opaque body fill enum                             |

### New files (`src/SharpVision/Controls/`)

| File                        | Responsibility                                         |
| --------------------------- | ------------------------------------------------------ |
| `ControlChrome.cs`          | Shared border/shadow/fill rasterization helpers        |
| `ControlStyleProperties.cs` | Static registration of base `Control` style properties |

### New files (`tests/`)

| File                                                        | Responsibility                              |
| ----------------------------------------------------------- | ------------------------------------------- |
| `tests/SharpVision.Tests/Styling/StylePropertyTests.cs`     | Registration, validation, class defaults    |
| `tests/SharpVision.Tests/Styling/ControlStyleTests.cs`      | Style mutation, states, freeze              |
| `tests/SharpVision.Tests/Styling/ThemeTests.cs`             | Theme collection, inheritance, freeze/clone |
| `tests/SharpVision.Tests/Styling/ThemeResolverTests.cs`     | Precedence, local override, clearing        |
| `tests/SharpVision.Tests/Styling/ThirdPartyControlTests.cs` | Test-assembly extensibility control         |
| `tests/SharpVision.Tests/Styling/ThemeApplicationTests.cs`  | Application.Theme switching                 |
| `tests/SharpVision.Tests/Styling/ControlChromeTests.cs`     | Border/shadow rendering                     |
| `tests/SharpVision.Tests/Styling/StandardThemeTests.cs`     | White/Dark semantic cells                   |
| `tests/SharpVision.Showcase.Tests/ThemeGalleryTests.cs`     | Theme switch + inheritance page             |

### Modified files

- `Control.cs` — style-property backing, theme context, base chrome
  render/measure
- `Application.cs` — `Theme` property and tree invalidation
- `Button.cs`, `Window.cs`, `Border.cs`, `Shadow.cs` — migrate to shared chrome
  / class defaults
- All controls using `Appearance` — migrate to `GetValue`/`ResolvedStyle`
- `docs/concepts/styling.md` — normative theming contract
- `src/SharpVision.Showcase/` — theme switch, inheritance page, third-party demo
  control
- Remove/replace old `Style.cs`, `Appearance.cs`, `Resolver.cs` after migration

---

## Task 1: StyleProperty registration core

**Files:** Create `StyleProperty.cs`, `IStyleProperty.cs`,
`StylePropertyRegistry.cs`

- [ ] Write failing tests for registration, duplicate rejection, validation,
      class defaults
- [ ] Implement
      `StyleProperty<T>.Register<TControl>(name, default, impact, validate?)`
- [ ] Implement `RegisterClassDefault<TDerived>(value)` on metadata
- [ ] Verify tests pass

## Task 2: ControlStyle and IControlStyle

**Files:** Create `ControlStyle.cs`, `IControlStyle.cs`,
`ThemeChangedEventArgs.cs`

- [ ] Write failing tests for Set/Remove/TryGet, state validation,
      measure-impact rejection in overlay states
- [ ] Implement mutable style with atomic snapshot + Changed event
- [ ] Verify tests pass

## Task 3: Theme collection

**Files:** Create `Theme.cs`

- [ ] Write failing tests for add/replace/remove, style chain cache, freeze,
      clone
- [ ] Implement theme with versioned snapshots
- [ ] Verify tests pass

## Task 4: ThemeResolver and ThemeContext

**Files:** Create `ThemeResolver.cs`, `ThemeContext.cs`

- [ ] Write failing tests for full precedence cascade and combined visual states
- [ ] Implement resolver: default → class default → theme chain → instance style
      → states → local
- [ ] Verify tests pass

## Task 5: Control style-property integration

**Files:** Create `FillMode.cs`, `ControlStyleProperties.cs`, modify
`Control.cs`

- [ ] Register base properties: Margin, Padding, Foreground, Background,
      Attributes, FillMode, BorderThickness, BorderStyle, BorderColor,
      BorderAttributes, HasShadow, ShadowMode, ShadowOffset, ShadowGlyph, shadow
      colors/attributes
- [ ] Add protected GetValue/SetValue, public ClearValue
- [ ] Back existing CLR properties through style properties
- [ ] Replace `Style?` with `IControlStyle?` per-instance overlay (no ancestor
      inheritance)
- [ ] Write ControlChromeTests for measure with border thickness

## Task 6: Base Control chrome rendering

**Files:** Create `ControlChrome.cs`, modify `Control.cs`

- [ ] Implement shared border/shadow/fill draw helpers (reuse Border/Shadow
      logic)
- [ ] Integrate into Control.RenderCore before content
- [ ] Update ContentBounds to deflate border then padding
- [ ] Write rendering tests: partial edges, tiny bounds, shadow overflow,
      wide-grapheme repair

## Task 7: Button class defaults and migration

**Files:** Modify `Button.cs`, `ButtonStyleProperties.cs` (if needed)

- [ ] Register Button class defaults: rounded border edges, padding 1, shadow
      (1,1)
- [ ] Remove constructor/local chrome duplication; use base Control chrome path
- [ ] Write tests for defaults and theme override

## Task 8: Application.Theme integration

**Files:** Modify `Application.cs`, `Control.cs` attach/detach

- [ ] Add `Theme` property (default `Themes.Dark`, null throws)
- [ ] Theme setter: dispatcher-affine, unsubscribe/subscribe, full-tree measure
      invalidation
- [ ] Attach controls to internal ThemeContext; raise empty PropertyChanged on
      theme change
- [ ] Write ThemeApplicationTests including cross-thread mutation

## Task 9: Standard themes

**Files:** Create `Themes.cs`

- [ ] Build White and Dark using public Theme/ControlStyle API only
- [ ] Style Control + Button + List + representative controls per spec table
- [ ] Freeze both themes
- [ ] Write StandardThemeTests for exact indexed semantic cells

## Task 10: Control migration off Appearance

**Files:** All controls referencing `Appearance`, `EffectiveStyle`, old `Style`

- [ ] Migrate Text, RichText, Border, Popup, Window, List, etc. to
      style-property resolution
- [ ] Update `ResolvedStyle`/`NormalStyle` to use ThemeResolver
- [ ] Remove old `Style.cs`, `Appearance.cs`, `Resolver.cs`, update
      `StyleTests.cs` → new tests
- [ ] Fix all existing control and rendering tests

## Task 11: Third-party extensibility proof

**Files:** `tests/SharpVision.Tests/Styling/ThirdPartyControlTests.cs`, test
control in test assembly

- [ ] Define `DemoPanel : Control` with custom `StyleProperty<LabelPlacement>`
- [ ] Prove theme inheritance, local override/clear, theme switch, rendering

## Task 12: Showcase

**Files:** `Gallery.cs`, `Catalog.cs`, `Examples.cs`, new demo control in
Showcase

- [ ] Add live White/Dark theme toggle to gallery chrome
- [ ] Add "Theming" page: inherited base styling vs control-specific overrides
- [ ] Add showcase-local third-party-style demo control outside built-in theme
      engine catalog
- [ ] Update GalleryRenderingTests and ThemeGalleryTests

## Task 13: Documentation and quality gates

- [ ] Rewrite `docs/concepts/styling.md` for typed theming
- [ ] Update affected control docs to link styling section
- [ ] Run `make format`, `make lint`, `make build`, `make test`

---

## Spec coverage checklist

| Spec section               | Task                                            |
| -------------------------- | ----------------------------------------------- |
| §4 Style properties        | 1, 5                                            |
| §5 Control styles          | 2, 5                                            |
| §6 Theme inheritance       | 3, 4                                            |
| §7 Freeze/clone            | 3                                               |
| §8 Application             | 8                                               |
| §9 Base chrome             | 5, 6, 7                                         |
| §10 Standard themes        | 9                                               |
| §11 Third-party            | 11                                              |
| §12 Serialization boundary | Metadata names only (no parser)                 |
| §14 Tests                  | All test tasks + tmux-style gallery interaction |
| §15 Migration              | 10                                              |
| Showcase §428–431          | 12                                              |
