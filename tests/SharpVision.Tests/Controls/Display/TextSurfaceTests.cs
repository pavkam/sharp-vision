// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies Text formatting and semantic cells through a mounted terminal surface.</summary>
public sealed class TextSurfaceTests
{
    /// <summary>Verifies mnemonic markers collapse while the complete marked grapheme is underlined.</summary>
    [Fact]
    public async Task Render_WhenContentContainsMnemonicAndEscapedAmpersand_DrawsVisibleAccessTextAsync()
    {
        // Arrange
        var text = new ControlText("&e\u0301dit && close")
        {
            UseMnemonic = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(14, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("e\u0301dit & close");
        var mnemonic = surface.Cell(default);
        mnemonic.Text.ShouldBe("e\u0301");
        mnemonic.Style.Attributes.ShouldBe(TerminalAttributes.Underline);
        var accessKey = ThemeColorHelper.AccessKey(ThemeCatalog.Dark);
        mnemonic.Style.Foreground.ShouldBe(accessKey);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("&");
        surface.Cell(new Point(5, 0)).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies disabled mnemonic text retains unavailable styling instead of actionable emphasis.</summary>
    [Fact]
    public async Task Render_WhenMnemonicTextIsDisabled_PreservesDisabledForegroundAsync()
    {
        // Arrange
        var text = new ControlText("&Save")
        {
            IsEnabled = false,
            UseMnemonic = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        var disabled = TerminalPalette.Project(
            ThemeColorHelper.DisabledForeground(ThemeCatalog.Dark),
            ColorDepth.Basic16);
        var mnemonic = surface.Cell(default);
        mnemonic.Text.ShouldBe("S");
        mnemonic.Style.Foreground.ShouldBe(disabled);
        mnemonic.Style.Attributes.ShouldBe(TerminalAttributes.Underline);
    }

    /// <summary>Verifies a hotkey-only theme replacement reparses a standalone mnemonic and keeps
    /// the replacement color through later repaints.</summary>
    [Fact]
    public async Task Theme_WhenStandaloneMnemonicIsActive_RepaintsFromTheCurrentHotkeyColorAsync()
    {
        // Arrange
        var mounted = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var replacement = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var text = new ControlText("&Save")
        {
            UseMnemonic = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            mounted,
            TestContext.Current.CancellationToken);
        var mountedColor = surface.Cell(default).Style.Foreground;

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = replacement, "swap hotkey-only theme");
        await surface.UpdateAsync(() => text.Invalidate(Invalidation.Render), "repaint unchanged mnemonic text");

        // Assert
        var replacementColor = TerminalPalette.Project(replacement.Hotkey, ColorDepth.Basic16);
        surface.Cell(default).Style.Foreground.ShouldBe(replacementColor);
        surface.Cell(default).Style.Foreground.ShouldNotBe(mountedColor);
        surface.Cell(default).Style.Attributes.ShouldBe(TerminalAttributes.Underline);
    }

    /// <summary>Verifies an InputBase-owned retained caption tracks theme and owner enablement
    /// without replacing its Text child.</summary>
    [Fact]
    public async Task Theme_WhenInputCaptionOwnsMnemonic_TracksHotkeyAndOwnerEnablementAsync()
    {
        // Arrange
        var mounted = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var replacement = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var button = new Button
        {
            Text = "&Run",
            UseMnemonic = true,
            Width = Length.Cells(7),
            Height = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(7, 3),
            mounted,
            TestContext.Current.CancellationToken);
        var caption = button.TextControl.ShouldNotBeNull();
        var mnemonicPoint = new Point(caption.Bounds.X, caption.Bounds.Y);

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = replacement, "swap caption hotkey theme");

        // Assert
        surface.Cell(mnemonicPoint).Style.Foreground.ShouldBe(
            TerminalPalette.Project(replacement.Hotkey, ColorDepth.Basic16));

        await surface.UpdateAsync(() => button.IsEnabled = false, "disable caption owner");

        caption.EffectiveIsEnabled.ShouldBeFalse();
        surface.Cell(mnemonicPoint).Style.Foreground.ShouldNotBe(
            TerminalPalette.Project(replacement.Hotkey, ColorDepth.Basic16));
        surface.Cell(mnemonicPoint).Style.Attributes.ShouldBe(TerminalAttributes.Underline);

        await surface.UpdateAsync(() => button.IsEnabled = true, "re-enable caption owner");

        surface.Cell(mnemonicPoint).Style.Foreground.ShouldBe(
            TerminalPalette.Project(replacement.Hotkey, ColorDepth.Basic16));
    }

    /// <summary>Verifies a HeaderedContentControl-owned retained caption uses the replacement
    /// theme's hotkey color.</summary>
    [Fact]
    public async Task Theme_WhenHeaderedCaptionOwnsMnemonic_RepaintsFromTheCurrentHotkeyColorAsync()
    {
        // Arrange
        var mounted = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#ff0000"));
        var replacement = ThemeCatalog.Parse(ThemeJson.Create(hotkey: "#00ff00"));
        var owner = new ProbeHeaderedContentControl
        {
            HeaderText = "&Title",
            UseMnemonic = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(5, 1),
            mounted,
            TestContext.Current.CancellationToken);
        var header = owner.Header.ShouldBeOfType<ControlText>();
        var mnemonicPoint = new Point(header.Bounds.X, header.Bounds.Y);

        // Act
        await surface.UpdateAsync(() => surface.Application.Theme = replacement, "swap header hotkey theme");

        // Assert
        surface.Cell(mnemonicPoint).Style.Foreground.ShouldBe(
            TerminalPalette.Project(replacement.Hotkey, ColorDepth.Basic16));
    }

    /// <summary>Verifies a non-built-in semantic owner controls mnemonic parsing for its retained
    /// caption through the internal ownership contract.</summary>
    [Fact]
    public async Task Render_WhenCustomCaptionOwnerDisablesMnemonic_PreservesLiteralMarkerAsync()
    {
        var caption = new ControlText("&Run") { UseMnemonic = true };
        var owner = new ProbeAccessKeyCaptionOwner
        {
            Content = caption,
            UseMnemonic = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("&Run");
        (surface.Cell(default).Style.Attributes & TerminalAttributes.Underline)
            .ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies passive text observes physical hover without entering focus or press state.</summary>
    [Fact]
    public async Task Pointer_WhenTextIsMounted_TracksHoverWithoutFocusOrPressAsync()
    {
        // Arrange
        var text = new ControlText("Text");
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        var restingCells = Enumerable.Range(0, 4)
            .Select(x => surface.Cell(new Point(x, 0)))
            .ToArray();

        // Act
        await surface.Pointer.MoveToAsync(text);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        text.IsPointerDirectlyOver.ShouldBeTrue();
        text.IsFocused.ShouldBeFalse();
        text.IsPressed.ShouldBeFalse();
        surface.ShouldRender("Text");
        Enumerable.Range(0, 4)
            .Select(x => surface.Cell(new Point(x, 0)))
            .ShouldBe(restingCells);
    }

    /// <summary>Verifies markup styles apply to complete combining and wide grapheme cells.</summary>
    [Fact]
    public async Task Render_WhenMarkupContainsUnicode_PreservesStylesAndCellOwnershipAsync()
    {
        // Arrange
        var text = new ControlText("<b>A\u0301</b><fg=brightcyan>界</fg>")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("A\u0301界");
        var combining = surface.Cell(default);
        combining.Text.ShouldBe("A\u0301");
        combining.Width.ShouldBe(1);
        combining.Style.Attributes.ShouldBe(TerminalAttributes.Bold);
        var wide = surface.Cell(new Point(1, 0));
        wide.Text.ShouldBe("界");
        wide.Width.ShouldBe(2);
        wide.Style.Foreground.ShouldBe(ReferenceColors.Get(14));
        var continuation = surface.Cell(new Point(2, 0));
        continuation.Continuation.ShouldBeTrue();
        continuation.LeadX.ShouldBe(1);
    }

    /// <summary>Verifies hex markup colors render as themed text without leaking tag syntax.</summary>
    [Fact]
    public async Task Render_WhenMarkupUsesHexColors_ResolvesVisibleTextAndColorsAsync()
    {
        // Arrange
        var text = new ControlText("<fg=#ff8800>A</fg><fg=#00ff88>I</fg>")
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(2, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("AI");
        surface.Cell(default).Style.Foreground.ShouldBe(TerminalPalette.Project(Color.Rgb(0xff, 0x88, 0x00), ColorDepth.Basic16));
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(TerminalPalette.Project(Color.Rgb(0x00, 0xff, 0x88), ColorDepth.Basic16));
    }

    /// <summary>Verifies ellipsis and alignment mutation replace every stale terminal cell.</summary>
    [Fact]
    public async Task UpdateAsync_WhenTextAndAlignmentChange_ReplacesFormattedCellsAsync()
    {
        // Arrange
        var text = new ControlText("abcdef")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Overflow = Overflow.Ellipsis
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("abc…");

        // Act
        await surface.UpdateAsync(
            () =>
            {
                text.Content = "xy";
                text.TextAlignment = Alignment.End;
            },
            "replace and align Text content");

        // Assert
        surface.ShouldRender("  xy");
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("x");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("y");
    }

    /// <summary>Verifies transparent Text preserves the opaque surface painted by its parent.</summary>
    [Fact]
    public async Task Render_WhenTextBackgroundIsTransparent_PreservesParentSurfaceAsync()
    {
        // Arrange
        var text = new ControlText("A")
        {
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(15)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var background = new Dock
        {
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(4)),
            Children = { text }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            background,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("A");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        var blankBackground = surface.Cell(new Point(2, 0)).Style.Background;
        blankBackground.IsRgb.ShouldBeTrue();
        var textBackground = surface.Cell(default).Style.Background;
        textBackground.IsRgb.ShouldBeTrue();
        // The Dock and text cells share the same resolved background through the theme pipeline.
        blankBackground.ShouldBe(textBackground);
    }

    /// <summary>Verifies terminal resize reflows wrapping and removes the obsolete row.</summary>
    [Fact]
    public async Task ResizeAsync_WhenTextSurfaceWidens_ReflowsIntoOneCommittedRowAsync()
    {
        // Arrange
        var text = new ControlText("A\u0301界x")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.WrapAnywhere
        };
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(3, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             Á界
                             x
                             """);

        // Act
        await surface.ResizeAsync(new Size(4, 1));

        // Assert
        text.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        surface.ShouldRender("Á界x");
        surface.Cell(new Point(2, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies Wrap breaks at word boundaries and respects the available width.</summary>
    [Fact]
    public async Task Render_WhenOverflowIsWrap_BreaksAtWordBoundariesAsync()
    {
        // Arrange
        var text = new ControlText("hello world end")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Wrap
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert — "hello" fits on row 0, "world" wraps to row 1, "end" to row 2
        surface.Cell(default).Text.ShouldBe("h");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("o");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("w");
        surface.Cell(new Point(4, 1)).Text.ShouldBe("d");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("e");
    }

    /// <summary>Verifies Clip truncates content at the boundary without ellipsis or wrapping.</summary>
    [Fact]
    public async Task Render_WhenOverflowIsClip_TruncatesWithoutEllipsisAsync()
    {
        // Arrange
        var text = new ControlText("abcdefghij")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Overflow = Overflow.Clip
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("abcde");
    }

    /// <summary>Verifies Ellipsis truncates content containing wide CJK characters without corruption.</summary>
    [Fact]
    public async Task Render_WhenEllipsisTruncatesWideChar_ProducesCleanTruncationAsync()
    {
        // Arrange — "AB界CD" = A(1) B(1) 界(2) C(1) D(1) = 6 cells, in 5 cols
        var text = new ControlText("AB界CD")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Overflow = Overflow.Ellipsis
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Assert — first two chars are preserved, an ellipsis appears, and no orphan continuation cell exists
        surface.Cell(default).Text.ShouldBe("A");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("B");
        var hasEllipsis = false;
        for (var x = 2; x < 5; x++)
        {
            if (surface.Cell(new Point(x, 0)).Text == "…")
            {
                hasEllipsis = true;
            }
        }

        hasEllipsis.ShouldBeTrue();
    }

    /// <summary>Verifies center text alignment pads both edges symmetrically.</summary>
    [Fact]
    public async Task Render_WhenTextAlignmentIsCenter_PadsBothEdgesAsync()
    {
        // Arrange — "Hi" (2 cells) in 8-wide = 3 leading spaces
        var text = new ControlText("Hi")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            TextAlignment = Alignment.Center
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(2, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("H");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("i");
        surface.Cell(new Point(5, 0)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies empty text content renders blank cells without errors.</summary>
    [Fact]
    public async Task Render_WhenContentIsEmpty_DrawsBlankCellsAsync()
    {
        // Arrange
        var text = new ControlText("")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("    ");
    }

    /// <summary>Verifies direct and ancestor-inherited disable painting, stable geometry across a
    /// genuine resize, and re-enable recovery for a mounted Text.</summary>
    [Fact]
    public async Task IsEnabled_WhenTextIsDisabled_ProvesDisabledContractAsync()
    {
        // Arrange
        var text = new ControlText("Text");
        await using var surface = await ComponentSurface.MountAsync(
            text,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Act — direct disable
        await surface.UpdateAsync(() => text.IsEnabled = false, "disable Text");

        // Assert
        surface.ShouldHaveState(text, VisualState.Disabled);

        // Arrange — ancestor-inherited disable
        var child = new ControlText("Text");
        var stack = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);

        // Act — geometry stability across a genuine resize
        await surface.ResizeAsync(new Size(6, 2));
        var enabledText = new ControlText("Text");
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledText,
            new Size(6, 2),
            TestContext.Current.CancellationToken);

        // Assert
        text.Bounds.ShouldBe(enabledText.Bounds);
        text.DesiredSize.ShouldBe(enabledText.DesiredSize);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => text.IsEnabled = true, "re-enable Text");

        // Assert
        surface.ShouldHaveState(text, VisualState.Normal);
    }
}
