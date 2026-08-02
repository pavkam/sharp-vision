// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Input;

/// <summary>Verifies bounded ncurses description loading and native restoration.</summary>
public sealed class NcursesProviderTests
{
    /// <summary>Verifies an unavailable dynamic library is reported without pretending a description is missing.</summary>
    [Fact]
    public void Load_WhenLibraryIsUnavailable_ReportsPlatformUnavailable()
    {
        var provider = new Provider(_ => null);

        var result = provider.Load(Request("xterm-256color"));

        result.Status.ShouldBe(DescriptionLoadStatus.PlatformUnavailable);
        result.Profile.ShouldBeNull();
    }

    /// <summary>Verifies the native factory receives this request's own configured search list
    /// rather than one bound at construction time, so a caller can override the ncurses discovery
    /// path per request without recompiling (see #98).</summary>
    [Fact]
    public void Load_WhenLimitsConfigureLibraryNames_ForwardsThemToTheNativeFactory()
    {
        IReadOnlyList<string>? observed = null;
        var provider = new Provider(names =>
        {
            observed = names;
            return null;
        });
        var limits = DescriptionLimits.Default with { NcursesLibraryNames = ["custom-ncurses.so"] };

        _ = provider.Load(new DescriptionRequest("fixture", DescriptionPlatform.Unix, 1, limits));

        observed.ShouldBe(["custom-ncurses.so"]);
    }

    /// <summary>Verifies setupterm distinguishes an absent entry from a database failure.</summary>
    [Theory]
    [InlineData(0, (int) DescriptionLoadStatus.MissingOrGeneric)]
    [InlineData(-1, (int) DescriptionLoadStatus.ProviderFailed)]
    public void Load_WhenSetupTermRejectsEntry_ReportsTypedStatus(
        int error,
        int expected)
    {
        var native = new FakeNcursesNative
        {
            SetupStatus = -1,
            SetupError = error,
            SetupChangesTerminal = false
        };
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe((DescriptionLoadStatus) expected);
        native.RestoredTerminal.ShouldBe(0);
        native.DeletedTerminal.ShouldBe(0);
    }

    /// <summary>Verifies hardcopy and generic entries cannot become full-screen profiles.</summary>
    [Theory]
    [InlineData("gn", Suitability.Generic)]
    [InlineData("hc", Suitability.Hardcopy)]
    public void Load_WhenEntryIsNotInteractive_ReportsUnsuitable(
        string capability,
        Suitability expected)
    {
        var native = ReadyNative();
        native.SetFlag(capability, 1);
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Suitability.ShouldBe(expected);
    }

    /// <summary>Verifies ERR/errret=1 publishes hardcopy metadata without reading the previous cur_term.</summary>
    [Fact]
    public void Load_WhenSetupReportsHardcopy_DoesNotReadOrDeletePreviousTerminal()
    {
        var native = new FakeNcursesNative
        {
            SetupStatus = -1,
            SetupError = 1,
            SetupChangesTerminal = false
        };
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("printer"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Suitability.ShouldBe(Suitability.Hardcopy);
        native.CapabilityReads.ShouldBe(0);
        native.DeletedTerminal.ShouldBe(0);
        native.RestoredTerminal.ShouldBe(0);
    }

    /// <summary>Verifies wrong native types are diagnosed and cannot satisfy a required command.</summary>
    [Fact]
    public void Load_WhenRequiredStringHasWrongType_ReportsIncomplete()
    {
        var native = ReadyNative();
        native.SetString("cup", NativeString.WrongType);
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Suitability.ShouldBe(Suitability.Incomplete);
        result.Diagnostics.ShouldContain(value =>
            value.Code == DescriptionDiagnosticCode.WrongType && value.Capability == "cup");
    }

    /// <summary>Verifies copied native bytes remain owned after terminal storage and the library are released.</summary>
    [Fact]
    public void Load_WhenNativeStorageIsReleased_OwnsCompiledBytes()
    {
        var source = "\u001b[%i%p1%d;%p2%dH"u8.ToArray();
        var native = ReadyNative();
        native.SetString("cup", NativeString.Present(source));
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));
        source.AsSpan().Fill((byte) 'x');

        native.IsDisposed.ShouldBeTrue();
        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Programs.TryGet("cup", out var program).ShouldBeTrue();
        program.Representation.Span.SequenceEqual("\u001b[%i%p1%d;%p2%dH"u8).ShouldBeTrue();
    }

    /// <summary>Verifies exact case-sensitive extended names produce only their documented evidence.</summary>
    [Fact]
    public void Load_WhenExtendedProgramsExist_RecordsDatabaseEvidence()
    {
        var native = ReadyNative();
        native.SetNumber("colors", 256);
        native.SetString("Ms", NativeString.Present("\u001b]52;%p1%s;%p2%s\a"u8));
        native.SetString("ms", NativeString.Present("ignored"u8));
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Capabilities.ColorDepth.ShouldBe(ColorDepth.Indexed256);
        result.Profile.Capabilities.ColorOrigin.ShouldBe(Origin.Database);
        result.Profile.Capabilities.Osc52.ShouldBe(new Feature(CapabilitySupport.Supported, Origin.Database));
        result.Profile.Programs.Has("ms").ShouldBeFalse();
    }

    /// <summary>Verifies renderer-facing direct and default color extensions are retained exactly.</summary>
    [Fact]
    public void Load_WhenRendererColorExtensionsExist_RetainsCompiledPrograms()
    {
        var native = ReadyNative();
        native.SetNumber("colors", 16_777_216);
        native.SetString("setrgbf", NativeString.Present("F%p1%d;%p2%d;%p3%d"u8));
        native.SetString("setrgbb", NativeString.Present("B%p1%d;%p2%d;%p3%d"u8));
        native.SetString("setdf", NativeString.Present("DF"u8));
        native.SetString("setdb", NativeString.Present("DB"u8));
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        var profile = result.Profile.ShouldNotBeNull();
        profile.Capabilities.ColorDepth.ShouldBe(ColorDepth.TrueColor);
        profile.Programs.Has("setrgbf").ShouldBeTrue();
        profile.Programs.Has("setrgbb").ShouldBeTrue();
        profile.Programs.Has("setdf").ShouldBeTrue();
        profile.Programs.Has("setdb").ShouldBeTrue();
    }

    /// <summary>Verifies xenl metadata and the TS/fsl title pair survive native snapshot ownership.</summary>
    [Fact]
    public void Load_WhenXenlAndTitlePairExist_PreservesDescriptionAndPrograms()
    {
        var native = ReadyNative();
        native.SetFlag("xenl", 1);
        native.SetString("TS", NativeString.Present("PREFIX"u8));
        native.SetString("fsl", NativeString.Present("SUFFIX"u8));
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        var profile = result.Profile.ShouldNotBeNull();
        profile.Description.EatNewlineGlitch.ShouldBeTrue();
        profile.Programs.HasZeroParameterPair("TS", "fsl").ShouldBeTrue();
    }

    /// <summary>Verifies partial focus, paste, and mouse command sets never authorize active use.</summary>
    [Fact]
    public void Load_WhenOptionalCommandSetsArePartial_LeavesFeaturesUnknown()
    {
        var native = ReadyNative();
        native.SetString("fe", NativeString.Present("focus-on"u8));
        native.SetString("fd", NativeString.Present("focus-off"u8));
        native.SetString("BE", NativeString.Present("paste-on"u8));
        native.SetString("BD", NativeString.Present("paste-off"u8));
        native.SetString("kmous", NativeString.Present("mouse"u8));
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Capabilities.FocusReporting.ShouldBe(Feature.Unknown);
        result.Profile.Capabilities.BracketedPaste.ShouldBe(Feature.Unknown);
        result.Profile.Capabilities.CellMouse.ShouldBe(Feature.Unknown);
    }

    /// <summary>Verifies provider exceptions still restore and release the installed native terminal.</summary>
    [Fact]
    public void Load_WhenCapabilityReadThrows_RestoresAndDeletesTerminal()
    {
        var native = new FakeNcursesNative { ReadException = new InvalidOperationException("fixture") };
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        native.RestoredTerminal.ShouldBe(41);
        native.DeletedTerminal.ShouldBe(73);
        native.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies a del_curterm failure remains diagnostic and cannot replace a successful snapshot.</summary>
    [Fact]
    public void Load_WhenDeleteTerminalFails_PreservesProfileAndReportsCleanup()
    {
        var native = ReadyNative();
        native.DeleteException = new InvalidOperationException("fixture cleanup");
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.CleanupFailure);
    }

    /// <summary>Verifies restore failure abandons the active native terminal rather than deleting or unloading it.</summary>
    [Fact]
    public void Load_WhenRestoreFails_LeaksActiveTerminalSafelyAndPreservesProfile()
    {
        var native = ReadyNative();
        native.RestoreException = new InvalidOperationException("restore");
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        native.DeletedTerminal.ShouldBe(0);
        native.IsDisposed.ShouldBeFalse();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.CleanupFailure);
    }

    /// <summary>Verifies a failure reading the active terminal before Setup ever runs still frees the
    /// native library handle — nothing has mutated ncurses' process-global cur_term at that point,
    /// so refusing to unload would leak the handle for no safety benefit (see #143).</summary>
    [Fact]
    public void Load_WhenCurrentTerminalReadFailsBeforeSetup_StillDisposesNativeLibrary()
    {
        var native = ReadyNative();
        native.CurrentTerminalException = new InvalidOperationException("current terminal");
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        native.SetupCalls.ShouldBe(0);
        native.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies setup exceptions after state change still restore and release the new terminal.</summary>
    [Fact]
    public void Load_WhenSetupThrowsAfterChangingTerminal_RestoresAndDeletesNewTerminal()
    {
        var native = ReadyNative();
        native.SetupException = new InvalidOperationException("setup");
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        native.RestoredTerminal.ShouldBe(41);
        native.DeletedTerminal.ShouldBe(73);
        native.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies extended-name restoration failure is diagnostic without replacing a loaded profile.</summary>
    [Fact]
    public void Load_WhenExtendedNameRestoreFails_PreservesProfileAndReportsCleanup()
    {
        var native = ReadyNative();
        native.ExtendedRestoreException = new InvalidOperationException("extended");
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        native.IsDisposed.ShouldBeTrue();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.CleanupFailure);
    }

    /// <summary>Verifies library-disposal failure is diagnostic without replacing a loaded profile.</summary>
    [Fact]
    public void Load_WhenDisposeFails_PreservesProfileAndReportsCleanup()
    {
        var native = ReadyNative();
        native.DisposeException = new InvalidOperationException("dispose");
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.CleanupFailure);
    }

    /// <summary>Verifies success without an installed cur_term is rejected before capability reads.</summary>
    [Fact]
    public void Load_WhenSetupSucceedsWithoutCurrentTerminal_ReportsProviderFailure()
    {
        var native = new FakeNcursesNative
        {
            CurrentTerminal = 0,
            SetupChangesTerminal = false
        };
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        native.CapabilityReads.ShouldBe(0);
    }

    /// <summary>Verifies inconsistent setupterm return/error combinations are provider failures.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, 2)]
    [InlineData(7, 1)]
    [InlineData(-7, 1)]
    public void Load_WhenSetupStatusCombinationIsInvalid_ReportsProviderFailure(int status, int error)
    {
        var native = new FakeNcursesNative
        {
            SetupStatus = status,
            SetupError = error,
            SetupChangesTerminal = false
        };

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.NativeFailure);
    }

    /// <summary>Verifies ERR may clear cur_term and cleanup still restores a non-null previous terminal.</summary>
    [Fact]
    public void Load_WhenSetupErrorClearsCurrentTerminal_RestoresPreviousWithoutDeletingNull()
    {
        var native = new FakeNcursesNative
        {
            SetupStatus = -1,
            SetupError = 0,
            LoadedTerminal = 0
        };

        var result = new Provider(_ => native).Load(Request("missing"));

        result.Status.ShouldBe(DescriptionLoadStatus.MissingOrGeneric);
        native.RestoredTerminal.ShouldBe(41);
        native.DeletedTerminal.ShouldBe(0);
        native.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies an unexpected set_curterm return is treated as unsafe restoration.</summary>
    [Fact]
    public void Load_WhenRestoreReturnsUnexpectedTerminal_DoesNotDeleteOrUnload()
    {
        var native = ReadyNative();
        native.RestoreReturn = 999;

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        native.DeletedTerminal.ShouldBe(0);
        native.IsDisposed.ShouldBeFalse();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.CleanupFailure);
    }

    /// <summary>Verifies every exact optional command set establishes database support.</summary>
    [Fact]
    public void Load_WhenOptionalCommandSetsAreComplete_RecordsDatabaseSupport()
    {
        var native = ReadyNative();

        foreach (var name in new[] { "fe", "fd", "kxIN", "kxOUT", "BE", "BD", "PS", "PE", "XM", "kmous" })
        {
            native.SetString(name, NativeString.Present(Encoding.ASCII.GetBytes(name)));
        }

        var result = new Provider(_ => native).Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Capabilities.FocusReporting.IsSupported.ShouldBeTrue();
        result.Profile.Capabilities.BracketedPaste.IsSupported.ShouldBeTrue();
        result.Profile.Capabilities.CellMouse.IsSupported.ShouldBeTrue();
    }

    /// <summary>Verifies relevant live path-list evidence is bounded before setupterm.</summary>
    [Fact]
    public void Load_WhenLivePathListExceedsLimit_RejectsBeforeNativeLookup()
    {
        var native = ReadyNative();
        var limits = DescriptionLimits.Default with { MaxDescriptionPathEntries = 1 };
        var provider = new Provider(
            _ => native,
            name => name == "TERMINFO_DIRS" ? "/first:/second" : null);

        var result = provider.Load(new DescriptionRequest("fixture", DescriptionPlatform.Unix, 1, limits));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.EnvironmentLimit);
        native.SetupCalls.ShouldBe(0);
    }

    /// <summary>Verifies a successful setupterm result is final even when termcap configuration exists.</summary>
    [Fact]
    public void Load_WhenSetupTermSucceeds_PerformsOneNativeLookup()
    {
        var native = ReadyNative();
        var provider = new Provider(
            _ => native,
            name => name == "TERMCAP" ? "/configured/termcap" : null);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        native.SetupCalls.ShouldBe(1);
    }

    /// <summary>Verifies canonical wrong-type sentinels are diagnosed by their exact native family.</summary>
    [Fact]
    public void Load_WhenCanonicalValuesHaveWrongTypes_ReportsEachIdentifier()
    {
        var native = ReadyNative();
        native.SetFlag("am", -1);
        native.SetNumber("colors", -2);
        native.SetString("bold", NativeString.WrongType);

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.WrongType && value.Capability == "am");
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.WrongType && value.Capability == "colors");
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.WrongType && value.Capability == "bold");
    }

    /// <summary>Verifies absent/cancelled native sentinels remain absence rather than wrong-type evidence.</summary>
    [Fact]
    public void Load_WhenCanonicalValuesAreAbsent_DoesNotReportWrongType()
    {
        var native = ReadyNative();
        native.SetFlag("am", 0);
        native.SetNumber("colors", -1);
        native.SetString("bold", NativeString.Absent);

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Diagnostics.ShouldNotContain(value => value.Code == DescriptionDiagnosticCode.WrongType);
        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Colors.ShouldBeNull();
    }

    /// <summary>Verifies an over-limit required string rejects the whole native description.</summary>
    [Fact]
    public void Load_WhenRequiredStringIsOverLimit_RejectsDescription()
    {
        var native = ReadyNative();
        native.SetString("cup", NativeString.OverLimit);

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.InvalidProgram && value.Capability == "cup");
    }

    /// <summary>Verifies an over-limit optional string also rejects before any profile publication.</summary>
    [Fact]
    public void Load_WhenOptionalStringIsOverLimit_RejectsDescription()
    {
        var native = ReadyNative();
        native.SetString("bold", NativeString.OverLimit);

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.InvalidProgram && value.Capability == "bold");
    }

    /// <summary>Verifies optional malformed and padded programs degrade without rejecting a usable core.</summary>
    [Theory]
    [InlineData("%", (int) DescriptionDiagnosticCode.InvalidProgram)]
    [InlineData("$<1>", (int) DescriptionDiagnosticCode.UnsupportedPadding)]
    public void Load_WhenOptionalProgramIsInvalid_OmitsItAndPreservesUsability(
        string bytes,
        int expected)
    {
        var native = ReadyNative();
        native.SetString("bold", NativeString.Present(Encoding.ASCII.GetBytes(bytes)));

        var result = new Provider(_ => native).Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Suitability.ShouldBe(Suitability.Usable);
        result.Profile.Programs.Has("bold").ShouldBeFalse();
        result.Diagnostics.ShouldContain(value => value.Code == (DescriptionDiagnosticCode) expected && value.Capability == "bold");
    }

    /// <summary>Verifies required malformed and padded programs produce their exact unsuitable result.</summary>
    [Theory]
    [InlineData("%", Suitability.Incomplete, (int) DescriptionDiagnosticCode.InvalidProgram)]
    [InlineData("$<1>", Suitability.UnsupportedPadding, (int) DescriptionDiagnosticCode.UnsupportedPadding)]
    public void Load_WhenRequiredProgramIsInvalid_ReportsExactUnsuitability(
        string bytes,
        Suitability suitability,
        int expected)
    {
        var native = ReadyNative();
        native.SetString("cup", NativeString.Present(Encoding.ASCII.GetBytes(bytes)));

        var result = new Provider(_ => native).Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Suitability.ShouldBe(suitability);
        result.Diagnostics.ShouldContain(value => value.Code == (DescriptionDiagnosticCode) expected && value.Capability == "cup");
    }

    /// <summary>Verifies every documented RGB representation validates against the retained color count.</summary>
    [Theory]
    [InlineData("flag", 256, 1, -1, null)]
    [InlineData("number", 8, 0, 1, null)]
    [InlineData("string", 16, 0, -1, "2/1/1")]
    public void Load_WhenRgbDescriptorIsValid_DoesNotDiagnoseIt(
        string _,
        int colors,
        int flag,
        int number,
        string? text)
    {
        var native = ReadyNative();
        native.SetNumber("colors", colors);
        native.SetFlag("RGB", flag);
        native.SetNumber("RGB", number);

        if (text is not null)
        {
            native.SetString("RGB", NativeString.Present(Encoding.ASCII.GetBytes(text)));
        }

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Diagnostics.ShouldNotContain(value =>
            value.Code == DescriptionDiagnosticCode.InvalidProgram && value.Capability == "RGB");
    }

    /// <summary>Verifies malformed and color-inconsistent RGB descriptors remain diagnostics only.</summary>
    [Theory]
    [InlineData(256, -1, "8/8")]
    [InlineData(256, 1, null)]
    [InlineData(8, 0, null)]
    [InlineData(8, 17, null)]
    [InlineData(8, int.MaxValue, null)]
    [InlineData(8, -1, "0/1/2")]
    [InlineData(8, -1, "17/1/1")]
    [InlineData(8, -1, "1/1/1/1")]
    [InlineData(8, -1, "x/1/1")]
    [InlineData(8, -1, "1/1")]
    [InlineData(8, -1, "63/63/63")]
    public void Load_WhenRgbDescriptorIsInvalid_DiagnosesIt(int colors, int number, string? text)
    {
        var native = ReadyNative();
        native.SetNumber("colors", colors);
        native.SetNumber("RGB", number);

        if (text is not null)
        {
            native.SetString("RGB", NativeString.Present(Encoding.ASCII.GetBytes(text)));
        }

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Diagnostics.ShouldContain(value =>
            value.Code == DescriptionDiagnosticCode.InvalidProgram && value.Capability == "RGB");
    }

    /// <summary>Verifies an over-limit RGB string rejects the whole description before publication.</summary>
    [Fact]
    public void Load_WhenRgbDescriptorIsOverLimit_RejectsDescription()
    {
        var native = ReadyNative();
        native.SetNumber("colors", 256);
        native.SetString("RGB", NativeString.OverLimit);

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        result.Diagnostics.ShouldContain(value =>
            value.Code == DescriptionDiagnosticCode.InvalidProgram && value.Capability == "RGB");
    }

    /// <summary>Verifies numeric U8 and Boolean XF are queried only through their canonical types.</summary>
    [Fact]
    public void Load_WhenU8AndXfHaveCanonicalTypes_DoesNotDiagnoseThem()
    {
        var native = ReadyNative();
        native.SetNumber("U8", 1);
        native.SetFlag("XF", 1);

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Diagnostics.ShouldNotContain(value =>
            value.Code == DescriptionDiagnosticCode.WrongType &&
            (value.Capability == "U8" || value.Capability == "XF"));
    }

    /// <summary>Verifies the allowlist is case-sensitive, finite, and duplicate-free.</summary>
    [Fact]
    public void Names_WhenEnumerated_AreCaseSensitiveAndDuplicateFree()
    {
        Names.Strings.Distinct(StringComparer.Ordinal).Count().ShouldBe(Names.Strings.Count);
        Names.Strings.ShouldContain("Ms");
        Names.Strings.ShouldNotContain("ms");
    }

    /// <summary>Verifies conflicting key bytes are removed rather than published ambiguously.</summary>
    [Fact]
    public void Load_WhenKeyBytesConflict_OmitsBothBindingsAndDiagnosesConflict()
    {
        var native = ReadyNative();
        native.SetString("kbs", NativeString.Present([0x08]));
        native.SetString("kcub1", NativeString.Present([0x08]));

        var result = new Provider(_ => native).Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.KeyMap.Bindings.ShouldBeEmpty();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.ConflictingKey);
    }

    /// <summary>Verifies conflict removal happens before enforcing the retained key limit.</summary>
    [Fact]
    public void Load_WhenConflictingKeysMeetSourceLimit_RetainsNoneWithoutFailure()
    {
        var native = ReadyNative();
        native.SetString("kbs", NativeString.Present([0x08]));
        native.SetString("kcub1", NativeString.Present([0x08]));
        var limits = DescriptionLimits.Default with { MaxDescriptionKeyBindings = 1 };

        var result = new Provider(_ => native).Load(
            new DescriptionRequest("fixture", DescriptionPlatform.Unix, 1, limits));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        result.Profile.KeyMap.Bindings.ShouldBeEmpty();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.ConflictingKey);
    }

    /// <summary>Verifies back-tab maps to a shifted logical Tab binding.</summary>
    [Fact]
    public void Load_WhenBackTabExists_MapsShiftTab()
    {
        var native = ReadyNative();
        native.SetString("kcbt", NativeString.Present("\u001b[Z"u8));

        var result = new Provider(_ => native).Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        var binding = result.Profile.KeyMap.Bindings.ShouldHaveSingleItem();
        binding.Code.ShouldBe(Code.Tab);
        binding.Modifiers.ShouldBe(Modifiers.Shift);
    }

    /// <summary>Verifies the complete allowlisted legacy key families map to stable logical identities.</summary>
    [Theory]
    [InlineData("kbeg", Code.Begin, Modifiers.None)]
    [InlineData("ka1", Code.Home, Modifiers.None)]
    [InlineData("ka3", Code.PageUp, Modifiers.None)]
    [InlineData("kb2", Code.Begin, Modifiers.None)]
    [InlineData("kc1", Code.End, Modifiers.None)]
    [InlineData("kc3", Code.PageDown, Modifiers.None)]
    [InlineData("kf36", Code.F36, Modifiers.None)]
    [InlineData("kf63", Code.F63, Modifiers.None)]
    [InlineData("kUP", Code.Up, Modifiers.Shift)]
    [InlineData("kRIT3", Code.Right, Modifiers.Alt)]
    [InlineData("kEND6", Code.End, Modifiers.Shift | Modifiers.Control)]
    [InlineData("kPRV8", Code.PageUp, Modifiers.Shift | Modifiers.Alt | Modifiers.Control)]
    public void Load_WhenAllowlistedKeyExists_MapsLogicalIdentity(
        string name,
        Code expectedCode,
        Modifiers expectedModifiers)
    {
        var native = ReadyNative();
        native.SetString(name, NativeString.Present("\u001b[99~"u8));

        var result = new Provider(_ => native).Load(Request("fixture"));

        var binding = result.Profile.ShouldNotBeNull().KeyMap.Bindings.ShouldHaveSingleItem();
        binding.Code.ShouldBe(expectedCode);
        binding.Modifiers.ShouldBe(expectedModifiers);
    }

    /// <summary>Verifies equivalent seven-bit and eight-bit parser signatures cannot publish ambiguity.</summary>
    [Fact]
    public void Load_WhenEquivalentKeySignaturesConflict_OmitsBothBindingsAndDiagnosesConflict()
    {
        var native = ReadyNative();
        native.SetString("kcuu1", NativeString.Present("\u001b[A"u8));
        native.SetString("kcud1", NativeString.Present([0x9b, (byte) 'A']));

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Profile.ShouldNotBeNull().KeyMap.Bindings.ShouldBeEmpty();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.ConflictingKey);
    }

    /// <summary>Verifies seven-bit and eight-bit SS3 aliases conflict before profile publication.</summary>
    [Fact]
    public void Load_WhenEquivalentSs3SignaturesConflict_OmitsBothBindingsAndDiagnosesConflict()
    {
        var native = ReadyNative();
        native.SetString("kcuu1", NativeString.Present("\u001bOA"u8));
        native.SetString("kcud1", NativeString.Present([0x8f, (byte) 'A']));

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Profile.ShouldNotBeNull().KeyMap.Bindings.ShouldBeEmpty();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.ConflictingKey);
    }

    /// <summary>Verifies every allowlisted function-key identifier maps through F63.</summary>
    [Fact]
    public void TryMapKey_WhenFunctionRangeIsAllowlisted_MapsF1ThroughF63()
    {
        for (var function = 1; function <= 63; function++)
        {
            var name = $"kf{function.ToString(CultureInfo.InvariantCulture)}";

            name.TryMapKey(out var code, out var modifiers).ShouldBeTrue(name);
            code.ShouldBe(Enum.Parse<Code>($"F{function.ToString(CultureInfo.InvariantCulture)}"), name);
            modifiers.ShouldBe(Modifiers.None, name);
        }
    }

    /// <summary>Verifies every extended modified-key identifier maps its base key and modifier suffix.</summary>
    [Fact]
    public void TryMapKey_WhenExtendedModifiedRangeIsAllowlisted_MapsEveryName()
    {
        (string Name, Code Code)[] bases =
        [
            ("kUP", Code.Up),
            ("kDN", Code.Down),
            ("kLFT", Code.Left),
            ("kRIT", Code.Right),
            ("kHOM", Code.Home),
            ("kEND", Code.End),
            ("kIC", Code.Insert),
            ("kDC", Code.Delete),
            ("kNXT", Code.PageDown),
            ("kPRV", Code.PageUp)
        ];
        (int Suffix, Modifiers Modifiers)[] suffixes =
        [
            (0, Modifiers.Shift),
            (3, Modifiers.Alt),
            (4, Modifiers.Shift | Modifiers.Alt),
            (5, Modifiers.Control),
            (6, Modifiers.Shift | Modifiers.Control),
            (7, Modifiers.Alt | Modifiers.Control),
            (8, Modifiers.Shift | Modifiers.Alt | Modifiers.Control)
        ];

        foreach (var item in bases)
        {
            foreach (var suffix in suffixes)
            {
                var name = suffix.Suffix == 0
                    ? item.Name
                    : $"{item.Name}{suffix.Suffix.ToString(CultureInfo.InvariantCulture)}";

                name.TryMapKey(out var code, out var modifiers).ShouldBeTrue(name);
                code.ShouldBe(item.Code, name);
                modifiers.ShouldBe(suffix.Modifiers, name);
            }
        }
    }

    /// <summary>Verifies malformed optional keys are diagnosed locally while valid keys remain published.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("\u001b[")]
    [InlineData("\u001bO")]
    public void Load_WhenOptionalKeyIsMalformed_OmitsOnlyInvalidBinding(string malformed)
    {
        var native = ReadyNative();
        native.SetString("kcuu1", NativeString.Present(Encoding.Latin1.GetBytes(malformed)));
        native.SetString("kcud1", NativeString.Present("\u001b[B"u8));

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        result.Profile.ShouldNotBeNull().KeyMap.Bindings.ShouldHaveSingleItem().Code.ShouldBe(Code.Down);
        result.Diagnostics.ShouldContain(value =>
            value.Code == DescriptionDiagnosticCode.InvalidKey && value.Capability == "kcuu1");
    }

    /// <summary>Verifies optional key signatures compile against the provider request limits.</summary>
    [Fact]
    public void Load_WhenOptionalKeyExceedsParserLimit_OmitsOnlyInvalidBinding()
    {
        var native = ReadyNative();
        native.SetString("kcuu1", NativeString.Present("\u001b[123A"u8));
        native.SetString("kcud1", NativeString.Present("\u001b[12B"u8));
        var parserLimits = ParserLimits.Default with { MaxParameterBytes = 2 };

        var result = new Provider(_ => native).Load(
            new DescriptionRequest(
                "fixture", DescriptionPlatform.Unix, 1, DescriptionLimits.Default, parserLimits: parserLimits));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        result.Profile.ShouldNotBeNull().KeyMap.Bindings.ShouldHaveSingleItem().Code.ShouldBe(Code.Down);
        result.Diagnostics.ShouldContain(value =>
            value.Code == DescriptionDiagnosticCode.InvalidKey && value.Capability == "kcuu1");
    }

    /// <summary>Verifies optional Escape keys use the provider's intermediate-byte limit.</summary>
    [Fact]
    public void Load_WhenOptionalEscapeKeyExceedsIntermediateLimit_OmitsOnlyInvalidBinding()
    {
        var native = ReadyNative();
        native.SetString("kcuu1", NativeString.Present("\u001b()B"u8));
        native.SetString("kcud1", NativeString.Present("\u001b(B"u8));
        var parserLimits = ParserLimits.Default with { MaxIntermediateBytes = 1 };

        var result = new Provider(_ => native).Load(
            new DescriptionRequest(
                "fixture", DescriptionPlatform.Unix, 1, DescriptionLimits.Default, parserLimits: parserLimits));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        result.Profile.ShouldNotBeNull().KeyMap.Bindings.ShouldHaveSingleItem().Code.ShouldBe(Code.Down);
        result.Diagnostics.ShouldContain(value =>
            value.Code == DescriptionDiagnosticCode.InvalidKey && value.Capability == "kcuu1");
    }

    /// <summary>Verifies low color counts degrade instead of overclaiming the basic 16-color tier.</summary>
    [Theory]
    [InlineData(1, ColorDepth.Monochrome)]
    [InlineData(8, ColorDepth.Monochrome)]
    [InlineData(15, ColorDepth.Monochrome)]
    [InlineData(16, ColorDepth.Basic16)]
    public void Load_WhenColorCountIsLow_ProjectsSafeDepth(int colors, ColorDepth expected)
    {
        var native = ReadyNative();
        native.SetNumber("colors", colors);

        var result = new Provider(_ => native).Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Capabilities.ColorDepth.ShouldBe(expected);
    }

    /// <summary>Verifies relevant environment snapshot bounds include names even when values are absent.</summary>
    [Fact]
    public void Load_WhenLiveEnvironmentSnapshotExceedsLimit_RejectsBeforeSetup()
    {
        var native = ReadyNative();
        var limits = DescriptionLimits.Default with { MaxDescriptionSnapshotBytes = 16 };
        var provider = new Provider(_ => native, _ => null);

        var result = provider.Load(new DescriptionRequest("fixture", DescriptionPlatform.Unix, 1, limits));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.EnvironmentLimit);
        native.SetupCalls.ShouldBe(0);
    }

    /// <summary>Verifies live environment and canonical facts share one accepted-snapshot budget.</summary>
    [Fact]
    public void Load_WhenCombinedSnapshotExceedsLimit_RejectsDescription()
    {
        var native = ReadyNative();
        var limits = DescriptionLimits.Default with { MaxDescriptionSnapshotBytes = 80 };
        var provider = new Provider(_ => native, _ => null);

        var result = provider.Load(new DescriptionRequest("fixture", DescriptionPlatform.Unix, 1, limits));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        native.CapabilityReads.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies each relevant live path is bounded before setupterm.</summary>
    [Fact]
    public void Load_WhenLivePathExceedsLimit_RejectsBeforeSetup()
    {
        var native = ReadyNative();
        var limits = DescriptionLimits.Default with { MaxDescriptionPathBytes = 1 };
        var provider = new Provider(_ => native, name => name == "HOME" ? "/too-long" : null);

        var result = provider.Load(new DescriptionRequest("fixture", DescriptionPlatform.Unix, 1, limits));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.EnvironmentLimit);
        native.SetupCalls.ShouldBe(0);
    }

    /// <summary>Verifies terminal-name limits count UTF-8 bytes rather than UTF-16 code units.</summary>
    [Fact]
    public void Request_WhenTerminalNameExceedsUtf8Limit_Throws()
    {
        var limits = DescriptionLimits.Default with { MaxTerminalNameBytes = 1 };

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new DescriptionRequest("é", DescriptionPlatform.Unix, 1, limits));
    }

    /// <summary>Verifies wrong-type U8 and XF evidence is rejected through their canonical native families.</summary>
    [Fact]
    public void Load_WhenU8AndXfHaveWrongTypes_DiagnosesThem()
    {
        var native = ReadyNative();
        native.SetNumber("U8", -2);
        native.SetFlag("XF", -1);

        var result = new Provider(_ => native).Load(Request("fixture"));

        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.WrongType && value.Capability == "U8");
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.WrongType && value.Capability == "XF");
    }

    /// <summary>Verifies an oversized inline TERMCAP value is rejected before native lookup.</summary>
    [Fact]
    public void Load_WhenInlineTermcapExceedsHistoricalLimit_ReportsProviderFailure()
    {
        var native = ReadyNative();
        var provider = new Provider(
            _ => native,
            name => name == "TERMCAP" ? new string('x', 1_024) : null);

        var result = provider.Load(Request("fixture"));

        result.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        result.Profile.ShouldBeNull();
        result.Diagnostics.ShouldContain(value => value.Code == DescriptionDiagnosticCode.TermcapLimit);
        native.SetupCalls.ShouldBe(0);
    }

    /// <summary>Verifies TERM=dumb loads as explicitly unsuitable without emitting terminal control bytes.</summary>
    [Fact]
    public void Load_WhenSystemDumbEntryExists_IsNotFullScreenUsable()
    {
        if (OperatingSystem.IsWindows())
        {
            var unavailable = new Provider().Load(Request("dumb", DescriptionPlatform.Windows));
            unavailable.Status.ShouldBe(DescriptionLoadStatus.PlatformUnavailable);
            return;
        }

        var result = new Provider().Load(Request("dumb"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Suitability.ShouldNotBe(Suitability.Usable);
    }

    /// <summary>Verifies a common system database entry exposes its core programs and color count when installed.</summary>
    [Fact]
    public void Load_WhenSystemXtermEntryExists_LoadsCoreAndColors()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var result = new Provider().Load(Request("xterm-256color"));

        result.Status.ShouldBe(DescriptionLoadStatus.Loaded);
        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Colors.ShouldBe(256);
        result.Profile.Programs.Has("cup").ShouldBeTrue();
        result.Profile.Programs.Has("sgr0").ShouldBeTrue();
    }

    /// <summary>Verifies the provider enables extended names only inside its serialized native lease.</summary>
    [Fact]
    public void Load_WhenExtendedNamesWereDisabled_RestoresPreviousState()
    {
        var native = ReadyNative();
        var provider = new Provider(_ => native);

        _ = provider.Load(Request("fixture"));

        native.ExtendedNameChanges.ShouldBe([true, false]);
        native.ExtendedNames.ShouldBeFalse();
    }

    /// <summary>Verifies a present empty required native string differs from an absent native pointer.</summary>
    [Fact]
    public void Load_WhenRequiredStringIsPresentButEmpty_DiagnosesInvalidProgram()
    {
        var native = ReadyNative();
        native.SetString("cup", NativeString.Present([]));
        var provider = new Provider(_ => native);

        var result = provider.Load(Request("fixture"));

        _ = result.Profile.ShouldNotBeNull();
        result.Profile.Description.Suitability.ShouldBe(Suitability.Incomplete);
        result.Diagnostics.ShouldContain(value =>
            value.Code == DescriptionDiagnosticCode.InvalidProgram && value.Capability == "cup");
    }

    /// <summary>Verifies TERMINFO is consumed by ncurses in the child rather than parsed by the test runner.</summary>
    [Fact]
    public void Probe_WhenTerminfoSelectsCompiledFixture_LoadsExactEntry()
    {
        if (OperatingSystem.IsWindows())
        {
            ProbeRunner.Run("fixture").Values["status"].ShouldBe("PlatformUnavailable");
            return;
        }

        using var fixture = TerminfoFixture.TryCreate("sv-ti-fixture", 8, includeExtensions: true);

        if (!fixture.IsAvailable)
        {
            fixture.Availability.ShouldBe("ToolUnavailable");
            return;
        }

        var result = ProbeRunner.Run(
            fixture.Name,
            new Dictionary<string, string?>
            {
                ["TERM"] = fixture.Name,
                ["TERMINFO"] = fixture.Database,
                ["TERMINFO_DIRS"] = null,
                ["TERMCAP"] = null,
                ["TERMPATH"] = null
            });

        result.ExitCode.ShouldBe(0, result.Error);
        result.Values["status"].ShouldBe("Loaded");
        result.Values["suitability"].ShouldBe("Usable");
        result.Values["colors"].ShouldBe("8");
        result.Values["Ms"].ShouldBe("True");
    }

    /// <summary>Verifies TERMINFO_DIRS preserves declared directory order.</summary>
    [Fact]
    public void Probe_WhenTerminfoDirectoriesContainSameName_UsesFirstDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            ProbeRunner.Run("fixture").Values["status"].ShouldBe("PlatformUnavailable");
            return;
        }

        using var first = TerminfoFixture.TryCreate("sv-dirs-fixture", 8, includeExtensions: false);
        using var second = TerminfoFixture.TryCreate("sv-dirs-fixture", 256, includeExtensions: false);

        if (!first.IsAvailable || !second.IsAvailable)
        {
            (first.IsAvailable ? second.Availability : first.Availability).ShouldBe("ToolUnavailable");
            return;
        }

        var result = ProbeRunner.Run(
            first.Name,
            new Dictionary<string, string?>
            {
                ["TERM"] = first.Name,
                ["TERMINFO"] = null,
                ["TERMINFO_DIRS"] = $"{first.Database}:{second.Database}",
                ["TERMCAP"] = null,
                ["TERMPATH"] = null
            });

        result.ExitCode.ShouldBe(0, result.Error);
        result.Values["status"].ShouldBe("Loaded");
        result.Values["colors"].ShouldBe("8");
    }

    /// <summary>Verifies ncurses normalizes an inline TERMCAP entry to canonical core names.</summary>
    [Fact]
    public void Probe_WhenInlineTermcapMatches_LoadsCanonicalCoreWithoutExtensions()
    {
        if (OperatingSystem.IsWindows())
        {
            ProbeRunner.Run("fixture").Values["status"].ShouldBe("PlatformUnavailable");
            return;
        }

        const string name = "sv-inline-termcap";
        var result = ProbeRunner.Run(
            name,
            new Dictionary<string, string?>
            {
                ["TERM"] = name,
                ["TERMINFO"] = null,
                ["TERMINFO_DIRS"] = null,
                ["TERMCAP"] = $"{name}|fixture:am:co#80:li#24:Co#8:cm=\\E[%i%d;%dH:me=\\E[0m:cl=\\E[H\\E[2J:",
                ["TERMPATH"] = null
            });

        result.ExitCode.ShouldBe(0, result.Error);
        result.Values["status"].ShouldBeOneOf("Loaded", "ProviderFailed");

        if (result.Values["status"] == "ProviderFailed")
        {
            int.Parse(result.Values["diagnostics"], CultureInfo.InvariantCulture).ShouldBeGreaterThan(0);
            return;
        }

        result.Values["cup"].ShouldBe("True");
        result.Values["Ms"].ShouldBe("False");
    }

    /// <summary>Verifies TERMCAP uses a compatibility file when the native build supports it.</summary>
    [Fact]
    public void Probe_WhenTermcapNamesFixtureFile_UsesNativeCompatibilityOrReportsMissing()
    {
        if (OperatingSystem.IsWindows())
        {
            ProbeRunner.Run("fixture").Values["status"].ShouldBe("PlatformUnavailable");
            return;
        }

        const string name = "sv-file-termcap";
        var directory = Directory.CreateTempSubdirectory("sharpvision-termcap-");
        var file = Path.Combine(directory.FullName, "termcap");

        try
        {
            File.WriteAllText(
                file,
                $"{name}|fixture:am:co#80:li#24:Co#16:cm=\\E[%i%d;%dH:me=\\E[0m:cl=\\E[H\\E[2J:{Environment.NewLine}",
                Encoding.ASCII);
            var result = ProbeRunner.Run(
                name,
                new Dictionary<string, string?>
                {
                    ["TERM"] = name,
                    ["TERMINFO"] = null,
                    ["TERMINFO_DIRS"] = null,
                    ["TERMCAP"] = file,
                    ["TERMPATH"] = null
                });

            result.ExitCode.ShouldBe(0, result.Error);
            result.Values["status"].ShouldBeOneOf("Loaded", "MissingOrGeneric");

            if (result.Values["status"] == "MissingOrGeneric")
            {
                int.Parse(result.Values["diagnostics"], CultureInfo.InvariantCulture).ShouldBeGreaterThan(0);
                return;
            }

            result.Values["suitability"].ShouldBe("Usable");
            result.Values["Smulx"].ShouldBe("False");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Verifies TERMPATH uses its file list when the native build supports compatibility files.</summary>
    [Fact]
    public void Probe_WhenTermpathContainsFixture_UsesNativeCompatibilityOrReportsMissing()
    {
        if (OperatingSystem.IsWindows())
        {
            ProbeRunner.Run("fixture").Values["status"].ShouldBe("PlatformUnavailable");
            return;
        }

        const string name = "sv-termpath-termcap";
        var directory = Directory.CreateTempSubdirectory("sharpvision-termpath-");
        var missing = Path.Combine(directory.FullName, "missing");
        var file = Path.Combine(directory.FullName, "termcap");

        try
        {
            File.WriteAllText(
                file,
                $"{name}|fixture:am:co#80:li#24:Co#16:cm=\\E[%i%d;%dH:me=\\E[0m:cl=\\E[H\\E[2J:{Environment.NewLine}",
                Encoding.ASCII);
            var result = ProbeRunner.Run(
                name,
                new Dictionary<string, string?>
                {
                    ["TERM"] = name,
                    ["TERMINFO"] = null,
                    ["TERMINFO_DIRS"] = null,
                    ["TERMCAP"] = null,
                    ["TERMPATH"] = $"{missing}:{file}"
                });

            result.ExitCode.ShouldBe(0, result.Error);
            result.Values["status"].ShouldBeOneOf("Loaded", "MissingOrGeneric");

            if (result.Values["status"] == "MissingOrGeneric")
            {
                int.Parse(result.Values["diagnostics"], CultureInfo.InvariantCulture).ShouldBeGreaterThan(0);
                return;
            }

            result.Values["suitability"].ShouldBe("Usable");
            result.Values["Ms"].ShouldBe("False");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static FakeNcursesNative ReadyNative()
    {
        var native = new FakeNcursesNative();
        native.SetString("cup", NativeString.Present("\u001b[%i%p1%d;%p2%dH"u8));
        native.SetString("sgr0", NativeString.Present("\u001b[0m"u8));
        native.SetString("clear", NativeString.Present("\u001b[H\u001b[2J"u8));
        return native;
    }

    private static DescriptionRequest Request(
        string terminalName,
        DescriptionPlatform platform = DescriptionPlatform.Unix) => new(
            terminalName,
            platform,
            outputFileDescriptor: 1,
            DescriptionLimits.Default);
}
