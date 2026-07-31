// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Input;

/// <summary>Verifies the deterministic Windows virtual-terminal description.</summary>
public sealed class WindowsVtProviderTests
{
    /// <summary>Verifies every renderer and lifecycle command is retained as exact compiled bytes.</summary>
    [Fact]
    public void Load_WhenWindowsVtIsEstablished_CompilesExactCanonicalPrograms()
    {
        var expected = ExpectedPrograms();
        var provider = new WindowsVtProvider();

        var result = provider.Load(Request(windowsVirtualTerminal: true));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        var profile = result.Profile.ShouldNotBeNull();
        profile.Programs.Count.ShouldBe(expected.Count);

        foreach (var pair in expected)
        {
            profile.Programs.TryGet(pair.Key, out var program).ShouldBeTrue(pair.Key);
            program.IsCompiled.ShouldBeTrue(pair.Key);
            program.IsIntrinsic.ShouldBeFalse(pair.Key);
            program.Representation.Span.ToArray().ShouldBe(pair.Value, pair.Key);
        }

        profile.Programs.Has("dim").ShouldBeFalse();
        profile.Programs.Has("sitm").ShouldBeFalse();
        profile.Programs.Has("blink").ShouldBeFalse();
        profile.Programs.Has("invis").ShouldBeFalse();
        profile.Programs.Has("smxx").ShouldBeFalse();
    }

    /// <summary>Verifies the built-in profile records only guaranteed Windows VT evidence.</summary>
    [Fact]
    public void Load_WhenWindowsVtIsEstablished_ReportsConservativeOptionalFeatures()
    {
        var provider = new WindowsVtProvider();

        var result = provider.Load(Request(windowsVirtualTerminal: true));

        var profile = result.Profile.ShouldNotBeNull();
        profile.Description.Name.ShouldBe("windows-vt");
        profile.Description.Origin.ShouldBe(DescriptionOrigin.BuiltIn);
        profile.Description.Suitability.ShouldBe(Suitability.Usable);
        profile.Description.Colors.ShouldBe(16);
        profile.Description.AutomaticMargins.ShouldBeTrue();
        profile.Description.BackColorErase.ShouldBeFalse();
        profile.Capabilities.ColorDepth.ShouldBe(ColorDepth.Basic16);
        profile.Capabilities.SynchronizedOutput.ShouldBe(Feature.Unknown);
        profile.Capabilities.FocusReporting.ShouldBe(Feature.Unknown);
        profile.Capabilities.BracketedPaste.ShouldBe(Feature.Unknown);
        profile.Capabilities.PixelMouse.ShouldBe(Feature.Unknown);
        profile.Capabilities.CellMouse.ShouldBe(Feature.Unknown);
        profile.Capabilities.KittyKeyboard.ShouldBe(Feature.Unknown);
        profile.Capabilities.Osc52.ShouldBe(Feature.Unknown);
        profile.Capabilities.KittyClipboard.ShouldBe(Feature.Unknown);
        profile.Capabilities.KittyGraphics.ShouldBe(Feature.Unknown);
        profile.Capabilities.Sixel.ShouldBe(Feature.Unknown);
        profile.Capabilities.ItermImages.ShouldBe(Feature.Unknown);
        profile.Capabilities.StyledUnderlines.ShouldBe(Feature.Unknown);
        profile.Capabilities.UnderlineColor.ShouldBe(Feature.Unknown);
        profile.Capabilities.Overline.ShouldBe(Feature.Unknown);
    }

    /// <summary>Verifies the fixed map owns every legacy sequence decoded by the current ANSI grammar.</summary>
    [Fact]
    public void Load_WhenWindowsVtIsEstablished_OwnsFixedAnsiKeyMap()
    {
        var expected = ExpectedKeys();
        var provider = new WindowsVtProvider();

        var result = provider.Load(Request(windowsVirtualTerminal: true));

        var actual = result.Profile.ShouldNotBeNull().KeyMap.Bindings.ToDictionary(
            binding => Convert.ToHexString(binding.Sequence.Span),
            binding => (binding.Code, binding.Modifiers),
            StringComparer.Ordinal);
        actual.Count.ShouldBe(expected.Count);

        foreach (var pair in expected)
        {
            actual.ShouldContainKey(pair.Key);
            actual[pair.Key].ShouldBe(pair.Value);
        }
    }

    /// <summary>Verifies Windows evidence is required instead of assuming VT support from the OS name.</summary>
    [Theory]
    [InlineData(false, (int) DescriptionPlatform.Windows)]
    [InlineData(false, (int) DescriptionPlatform.Unix)]
    public void Load_WhenWindowsVtIsNotEstablished_ReturnsUnavailable(
        bool windowsVirtualTerminal,
        int platformValue)
    {
        var provider = new WindowsVtProvider();
        var platform = (DescriptionPlatform) platformValue;

        var result = provider.Load(Request(windowsVirtualTerminal, platform));

        result.Status.ShouldBe(DescriptionLoadStatus.PlatformUnavailable);
        result.Profile.ShouldBeNull();
    }

    /// <summary>Verifies Windows VT evidence cannot be attached to a Unix lookup request.</summary>
    [Fact]
    public void Constructor_WhenWindowsVtIsClaimedForUnix_Throws()
    {
        _ = Should.Throw<ArgumentException>(() => new DescriptionRequest(
            "fixture",
            DescriptionPlatform.Unix,
            outputFileDescriptor: 1,
            DescriptionLimits.Default,
            windowsVirtualTerminal: true));
    }

    /// <summary>Verifies caller limits still bound deterministic built-in profile construction.</summary>
    [Fact]
    public void Load_WhenKeyMapExceedsConfiguredLimit_ReturnsProviderFailure()
    {
        var provider = new WindowsVtProvider();
        var limits = DescriptionLimits.Default with { MaxDescriptionKeyBindings = ExpectedKeys().Count - 1 };
        var request = new DescriptionRequest(
            "windows-vt",
            DescriptionPlatform.Windows,
            outputFileDescriptor: 1,
            limits,
            windowsVirtualTerminal: true);

        var result = provider.Load(request);

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        result.Diagnostics.ShouldContain(item => item.Code == DescriptionDiagnosticCode.DescriptionLimit);
    }

    /// <summary>Verifies the complete fixed profile is rejected one byte below its exact snapshot size.</summary>
    [Fact]
    public void Load_WhenOwnedSnapshotExceedsConfiguredLimit_ReturnsProviderFailure()
    {
        var provider = new WindowsVtProvider();
        var limits = DescriptionLimits.Default with { MaxDescriptionSnapshotBytes = ExpectedSnapshotBytes() - 1 };
        var request = new DescriptionRequest(
            "windows-vt",
            DescriptionPlatform.Windows,
            outputFileDescriptor: 1,
            limits,
            windowsVirtualTerminal: true);

        var result = provider.Load(request);

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        result.Diagnostics.ShouldContain(item => item.Code == DescriptionDiagnosticCode.DescriptionLimit);
    }

    /// <summary>Verifies the complete fixed profile is accepted at its exact snapshot size.</summary>
    [Fact]
    public void Load_WhenOwnedSnapshotEqualsConfiguredLimit_LoadsProfile()
    {
        var provider = new WindowsVtProvider();
        var limits = DescriptionLimits.Default with { MaxDescriptionSnapshotBytes = ExpectedSnapshotBytes() };
        var request = new DescriptionRequest(
            "windows-vt",
            DescriptionPlatform.Windows,
            outputFileDescriptor: 1,
            limits,
            windowsVirtualTerminal: true);

        var result = provider.Load(request);

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
    }

    /// <summary>Verifies a request is required.</summary>
    [Fact]
    public void Load_WhenRequestIsNull_Throws()
    {
        var provider = new WindowsVtProvider();

        _ = Should.Throw<ArgumentNullException>(() => provider.Load(request: null!));
    }

    private static DescriptionRequest Request(
        bool windowsVirtualTerminal,
        DescriptionPlatform platform = DescriptionPlatform.Windows) => new(
            "windows-vt",
            platform,
            outputFileDescriptor: 1,
            DescriptionLimits.Default,
            windowsVirtualTerminal: windowsVirtualTerminal);

    private static Dictionary<string, byte[]> ExpectedPrograms() => new(StringComparer.Ordinal)
    {
        ["bel"] = "\a"u8.ToArray(),
        ["clear"] = "\u001b[H\u001b[2J"u8.ToArray(),
        ["cup"] = "\u001b[%i%p1%d;%p2%dH"u8.ToArray(),
        ["home"] = "\u001b[H"u8.ToArray(),
        ["cud1"] = "\u001b[B"u8.ToArray(),
        ["cuu1"] = "\u001b[A"u8.ToArray(),
        ["cub1"] = "\u001b[D"u8.ToArray(),
        ["cuf1"] = "\u001b[C"u8.ToArray(),
        ["cud"] = "\u001b[%p1%dB"u8.ToArray(),
        ["cuu"] = "\u001b[%p1%dA"u8.ToArray(),
        ["cub"] = "\u001b[%p1%dD"u8.ToArray(),
        ["cuf"] = "\u001b[%p1%dC"u8.ToArray(),
        ["ed"] = "\u001b[J"u8.ToArray(),
        ["el"] = "\u001b[K"u8.ToArray(),
        ["el1"] = "\u001b[1K"u8.ToArray(),
        ["ech"] = "\u001b[%p1%dX"u8.ToArray(),
        ["sgr0"] = "\u001b[0m"u8.ToArray(),
        ["bold"] = "\u001b[1m"u8.ToArray(),
        ["smul"] = "\u001b[4m"u8.ToArray(),
        ["rmul"] = "\u001b[24m"u8.ToArray(),
        ["rev"] = "\u001b[7m"u8.ToArray(),
        ["smso"] = "\u001b[7m"u8.ToArray(),
        ["rmso"] = "\u001b[27m"u8.ToArray(),
        ["setaf"] = "\u001b[%?%p1%{8}%<%t3%p1%d%e%p1%{16}%<%t9%p1%{8}%-%d%e38;5;%p1%d%;m"u8.ToArray(),
        ["setab"] = "\u001b[%?%p1%{8}%<%t4%p1%d%e%p1%{16}%<%t10%p1%{8}%-%d%e48;5;%p1%d%;m"u8.ToArray(),
        ["setrgbf"] = "\u001b[38;2;%p1%d;%p2%d;%p3%dm"u8.ToArray(),
        ["setrgbb"] = "\u001b[48;2;%p1%d;%p2%d;%p3%dm"u8.ToArray(),
        ["setdf"] = "\u001b[39m"u8.ToArray(),
        ["setdb"] = "\u001b[49m"u8.ToArray(),
        ["op"] = "\u001b[39;49m"u8.ToArray(),
        ["smcup"] = "\u001b[?1049h"u8.ToArray(),
        ["rmcup"] = "\u001b[?1049l"u8.ToArray(),
        ["civis"] = "\u001b[?25l"u8.ToArray(),
        ["cnorm"] = "\u001b[?25h"u8.ToArray(),
        ["smkx"] = "\u001b[?1h\u001b="u8.ToArray(),
        ["rmkx"] = "\u001b[?1l\u001b>"u8.ToArray()
    };

    private static Dictionary<string, (Code Code, Modifiers Modifiers)> ExpectedKeys() =>
        new(StringComparer.Ordinal)
        {
            ["7F"] = (Code.Backspace, Modifiers.None),
            ["09"] = (Code.Tab, Modifiers.None),
            ["0D"] = (Code.Enter, Modifiers.None),
            ["1B5B5A"] = (Code.Tab, Modifiers.Shift),
            ["1B5B41"] = (Code.Up, Modifiers.None),
            ["1B5B42"] = (Code.Down, Modifiers.None),
            ["1B5B43"] = (Code.Right, Modifiers.None),
            ["1B5B44"] = (Code.Left, Modifiers.None),
            ["1B5B48"] = (Code.Home, Modifiers.None),
            ["1B5B46"] = (Code.End, Modifiers.None),
            ["1B5B327E"] = (Code.Insert, Modifiers.None),
            ["1B5B337E"] = (Code.Delete, Modifiers.None),
            ["1B5B357E"] = (Code.PageUp, Modifiers.None),
            ["1B5B367E"] = (Code.PageDown, Modifiers.None),
            ["1B4F50"] = (Code.F1, Modifiers.None),
            ["1B4F51"] = (Code.F2, Modifiers.None),
            ["1B4F52"] = (Code.F3, Modifiers.None),
            ["1B4F53"] = (Code.F4, Modifiers.None),
            ["1B5B31357E"] = (Code.F5, Modifiers.None),
            ["1B5B31377E"] = (Code.F6, Modifiers.None),
            ["1B5B31387E"] = (Code.F7, Modifiers.None),
            ["1B5B31397E"] = (Code.F8, Modifiers.None),
            ["1B5B32307E"] = (Code.F9, Modifiers.None),
            ["1B5B32317E"] = (Code.F10, Modifiers.None),
            ["1B5B32337E"] = (Code.F11, Modifiers.None),
            ["1B5B32347E"] = (Code.F12, Modifiers.None),
            ["1B4F41"] = (Code.Up, Modifiers.None),
            ["1B4F42"] = (Code.Down, Modifiers.None),
            ["1B4F43"] = (Code.Right, Modifiers.None),
            ["1B4F44"] = (Code.Left, Modifiers.None),
            ["1B4F48"] = (Code.Home, Modifiers.None),
            ["1B4F46"] = (Code.End, Modifiers.None)
        };

    private static int ExpectedSnapshotBytes()
    {
        var bytes = Encoding.UTF8.GetByteCount("windows-vt");
        bytes = checked(bytes + Encoding.UTF8.GetByteCount("am") + 1);
        bytes = checked(bytes + Encoding.UTF8.GetByteCount("colors") + sizeof(int));

        foreach (var pair in ExpectedPrograms())
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(pair.Key) + pair.Value.Length);
        }

        foreach (var sequence in ExpectedKeys().Keys)
        {
            bytes = checked(bytes + (sequence.Length / 2));
        }

        return bytes;
    }

}
