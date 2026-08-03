// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery.Adapters;
using SharpVision.Terminal.Input;

/// <summary>Verifies immutable terminal profile ownership and built-in profiles.</summary>
public sealed class TerminalProfileTests
{
    #region Evidence precedence

    /// <summary>Verifies validated database evidence is representable without a database reader.</summary>
    [Fact]
    public void Capabilities_WhenDescriptionEvidenceExists_RecordsDatabaseOrigin()
    {
        // Arrange
        var description = new Description(
            "xterm-direct",
            DescriptionOrigin.Database,
            Suitability.Usable);
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram([1]),
            ["sgr0"] = new DescriptionProgram([2]),
            ["clear"] = new DescriptionProgram([3]),
            ["Ms"] = new DescriptionProgram("\u001b]52;%p1%s;%p2%s\a"u8)
        });

        // Act
        var capabilities = TerminalCapabilities.Conservative.Apply(description,
            programs);

        // Assert
        capabilities.Osc52.State.ShouldBe(CapabilitySupport.Supported);
        capabilities.Osc52.Origin.ShouldBe(Origin.Database);
    }

    /// <summary>Verifies transplanted database claims require every exact backing command.</summary>
    [Fact]
    public void Capabilities_WhenOptionalBackingSetIsPartial_NormalizesClaimsToUnknown()
    {
        var claimed = new Feature(CapabilitySupport.Supported, Origin.Database);
        var capabilities = TerminalCapabilities.Conservative with
        {
            FocusReporting = claimed,
            BracketedPaste = claimed,
            CellMouse = claimed
        };
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram([1]),
            ["sgr0"] = new DescriptionProgram([2]),
            ["clear"] = new DescriptionProgram([3]),
            ["fe"] = new DescriptionProgram([4]),
            ["fd"] = new DescriptionProgram([5]),
            ["BE"] = new DescriptionProgram([6]),
            ["BD"] = new DescriptionProgram([7]),
            ["kmous"] = new DescriptionProgram([8])
        });

        var profile = new TerminalProfile(DatabaseDescription(), capabilities, programs, KeyMap.Empty);

        profile.Capabilities.FocusReporting.ShouldBe(Feature.Unknown);
        profile.Capabilities.BracketedPaste.ShouldBe(Feature.Unknown);
        profile.Capabilities.CellMouse.ShouldBe(Feature.Unknown);
    }

    /// <summary>Verifies complete exact backing sets retain database support claims.</summary>
    [Fact]
    public void Capabilities_WhenOptionalBackingSetsAreComplete_PreservesClaims()
    {
        var claimed = new Feature(CapabilitySupport.Supported, Origin.Database);
        var capabilities = TerminalCapabilities.Conservative with
        {
            FocusReporting = claimed,
            BracketedPaste = claimed,
            CellMouse = claimed
        };
        var names = new[]
        {
            "cup", "sgr0", "clear", "fe", "fd", "kxIN", "kxOUT",
            "BE", "BD", "PS", "PE", "XM", "kmous"
        };
        var values = names.ToDictionary(
            name => name,
            _ => new DescriptionProgram([1]),
            StringComparer.Ordinal);
        values["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8);
        values["sgr0"] = new DescriptionProgram("\u001b[0m"u8);
        values["clear"] = new DescriptionProgram("\u001b[2J"u8);
        var programs = new Programs(values);

        var profile = new TerminalProfile(DatabaseDescription(), capabilities, programs, KeyMap.Empty);

        profile.Capabilities.FocusReporting.ShouldBe(claimed);
        profile.Capabilities.BracketedPaste.ShouldBe(claimed);
        profile.Capabilities.CellMouse.ShouldBe(claimed);
    }

    /// <summary>Verifies explicit evidence remains later than database evidence.</summary>
    [Fact]
    public void Capabilities_WhenExplicitEvidenceExists_PreservesExplicitEvidence()
    {
        // Arrange
        var existing = new Feature(CapabilitySupport.Supported, Origin.Override);
        var capabilities = TerminalCapabilities.Conservative with { Osc52 = existing };

        // Act
        var projected = capabilities.Apply(DatabaseDescription(),
            DatabasePrograms(includeOsc52: true));

        // Assert
        projected.Osc52.ShouldBe(existing);
    }

    /// <summary>Verifies query evidence remains later than database evidence.</summary>
    [Fact]
    public void Capabilities_WhenQueryEvidenceExists_PreservesQueryEvidence()
    {
        // Arrange
        var existing = new Feature(CapabilitySupport.Supported, Origin.Query);
        var capabilities = TerminalCapabilities.Conservative with { Osc52 = existing };

        // Act
        var projected = capabilities.Apply(DatabaseDescription(),
            DatabasePrograms(includeOsc52: true));

        // Assert
        projected.Osc52.ShouldBe(existing);
    }

    #endregion

    #region Profiles

    /// <summary>Verifies unsupported narrowing evidence is not re-enabled by database evidence.</summary>
    [Fact]
    public void Capabilities_WhenUnsupportedEvidenceExists_PreservesUnsupportedEvidence()
    {
        // Arrange
        var existing = new Feature(CapabilitySupport.Unsupported, Origin.Default);
        var capabilities = TerminalCapabilities.Conservative with { Osc52 = existing };

        // Act
        var projected = capabilities.Apply(DatabaseDescription(),
            DatabasePrograms(includeOsc52: true));

        // Assert
        projected.Osc52.ShouldBe(existing);
    }

    /// <summary>Verifies construction retains the exact immutable semantic values.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_ExposesSemanticValues()
    {
        // Arrange
        var description = new Description(
            "explicit",
            DescriptionOrigin.Explicit,
            Suitability.Usable);
        var capabilities = TerminalCapabilities.Conservative with
        {
            Osc52 = new Feature(CapabilitySupport.Supported, Origin.Override)
        };

        // Act
        var profile = new TerminalProfile(description, capabilities);

        // Assert
        profile.Description.Name.ShouldBe(description.Name);
        profile.Capabilities.ShouldBeSameAs(capabilities);
    }

    /// <summary>Verifies the public semantic-only constructor cannot publish full-screen suitability.</summary>
    [Fact]
    public void Constructor_WhenUsableDescriptionHasNoPrograms_ReportsIncomplete()
    {
        // Arrange
        var description = new Description(
            "explicit",
            DescriptionOrigin.Explicit,
            Suitability.Usable);

        // Act
        var profile = new TerminalProfile(description, TerminalCapabilities.Conservative);

        // Assert
        profile.Description.Suitability.ShouldBe(Suitability.Incomplete);
    }

    /// <summary>Verifies missing owned semantic values are rejected.</summary>
    [Fact]
    public void Constructor_WhenOwnedValueIsNull_Throws()
    {
        // Arrange
        var description = new Description(
            "explicit",
            DescriptionOrigin.Explicit,
            Suitability.Usable);

        // Act / Assert
        _ = Should.Throw<ArgumentNullException>(() =>
            new TerminalProfile(null!, TerminalCapabilities.Conservative));
        _ = Should.Throw<ArgumentNullException>(() =>
            new TerminalProfile(description, null!));
    }

    /// <summary>Verifies the conservative profile is stable and unsuitable for full-screen startup.</summary>
    [Fact]
    public void Conservative_WhenRead_IsStableAndNotUsable()
    {
        // Arrange / Act
        var first = TerminalProfile.Conservative;
        var second = TerminalProfile.Conservative;

        // Assert
        first.ShouldBeSameAs(second);
        first.Description.Name.ShouldBe("conservative");
        first.Description.Origin.ShouldBe(DescriptionOrigin.BuiltIn);
        first.Description.Suitability.ShouldBe(Suitability.Missing);
        first.Capabilities.ShouldBeSameAs(TerminalCapabilities.Conservative);
    }

    /// <summary>Verifies ANSI construction publishes usable built-in metadata with exact capabilities.</summary>
    [Fact]
    public void CreateAnsi_WhenCapabilitiesAreProvided_ProducesUsableProfile()
    {
        // Arrange
        var capabilities = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Indexed256
        };

        // Act
        var profile = TerminalProfile.CreateAnsi(capabilities);

        // Assert
        profile.Description.Name.ShouldBe("ansi");
        profile.Description.Origin.ShouldBe(DescriptionOrigin.BuiltIn);
        profile.Description.Suitability.ShouldBe(Suitability.Usable);
        profile.Description.Columns.ShouldBeNull();
        profile.Description.Lines.ShouldBeNull();
        profile.Description.Colors.ShouldBeNull();
        profile.Description.AutomaticMargins.ShouldBeTrue();
        profile.Description.BackColorErase.ShouldBeFalse();
        profile.Capabilities.ShouldBeSameAs(capabilities);
        profile.Programs.TryGet("cup", out var cursor).ShouldBeTrue();
        cursor.IsIntrinsic.ShouldBeTrue();
        cursor.Representation.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies trusted ANSI compatibility preserves exact caller evidence without database backing programs.</summary>
    [Fact]
    public void CreateAnsi_WhenCapabilitiesContainDatabaseEvidence_PreservesExactInstance()
    {
        var database = new Feature(CapabilitySupport.Supported, Origin.Database);
        var capabilities = TerminalCapabilities.Conservative with
        {
            Osc52 = database,
            FocusReporting = database
        };

        var profile = TerminalProfile.CreateAnsi(capabilities);
        var changed = profile.WithCapabilities(capabilities with
        {
            ColorDepth = ColorDepth.Monochrome,
            ColorOrigin = Origin.Override
        });

        profile.Capabilities.ShouldBeSameAs(capabilities);
        profile.Capabilities.Osc52.ShouldBe(database);
        profile.Capabilities.FocusReporting.ShouldBe(database);
        changed.Capabilities.Osc52.ShouldBe(database);
        changed.Capabilities.FocusReporting.ShouldBe(database);
    }

    /// <summary>Verifies ANSI construction rejects a missing capabilities value.</summary>
    [Fact]
    public void CreateAnsi_WhenCapabilitiesAreNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => TerminalProfile.CreateAnsi(null!));

    /// <summary>Verifies semantic replacement rejects a missing capability snapshot.</summary>
    [Fact]
    public void WithCapabilities_WhenCapabilitiesAreNull_Throws()
    {
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);

        _ = Should.Throw<ArgumentNullException>(() => profile.WithCapabilities(null!));
    }

    /// <summary>Verifies semantic replacement retains exact compiled programs and key-map ownership.</summary>
    [Fact]
    public void WithCapabilities_WhenCapabilitiesChange_RetainsDescriptionProgramsAndKeys()
    {
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram([1]),
            ["sgr0"] = new DescriptionProgram([2]),
            ["clear"] = new DescriptionProgram([3])
        });
        var keyMap = new KeyMap([new KeyBinding([0x1b, (byte) '[', (byte) 'A'], Code.Up)]);
        var profile = new TerminalProfile(
            DatabaseDescription(),
            TerminalCapabilities.Conservative,
            programs,
            keyMap);
        var capabilities = profile.Capabilities with
        {
            ColorDepth = ColorDepth.Monochrome,
            ColorOrigin = Origin.Override
        };

        var changed = profile.WithCapabilities(capabilities);

        changed.Description.ShouldBeSameAs(profile.Description);
        changed.Programs.ShouldBeSameAs(profile.Programs);
        changed.KeyMap.ShouldBeSameAs(profile.KeyMap);
        changed.Capabilities.ColorOrigin.ShouldBe(Origin.Override);
    }

    /// <summary>Verifies absent required terminal programs prevent a usable profile.</summary>
    [Fact]
    public void Constructor_WhenRequiredProgramIsMissing_ReportsIncomplete()
    {
        // Arrange
        var description = new Description(
            "database",
            DescriptionOrigin.Database,
            Suitability.Usable);
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["sgr0"] = new DescriptionProgram([1]),
            ["clear"] = new DescriptionProgram([2])
        });

        // Act
        var profile = new TerminalProfile(
            description,
            TerminalCapabilities.Conservative,
            programs,
            KeyMap.Empty);

        // Assert
        profile.Description.Suitability.ShouldBe(Suitability.Incomplete);
    }

    #endregion

    #region Snapshot ownership

    /// <summary>Verifies an empty required terminal program prevents a usable profile.</summary>
    [Fact]
    public void Constructor_WhenRequiredProgramIsEmpty_ReportsIncomplete()
    {
        // Arrange
        var description = new Description(
            "database",
            DescriptionOrigin.Database,
            Suitability.Usable);
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram([]),
            ["sgr0"] = new DescriptionProgram([1]),
            ["clear"] = new DescriptionProgram([2])
        });

        // Act
        var profile = new TerminalProfile(
            description,
            TerminalCapabilities.Conservative,
            programs,
            KeyMap.Empty);

        // Assert
        profile.Description.Suitability.ShouldBe(Suitability.Incomplete);
    }

    /// <summary>Verifies programs, bytecode, and key bindings are retained as owned snapshots.</summary>
    [Fact]
    public void Constructor_WhenInputCollectionsChange_OwnsSnapshots()
    {
        // Arrange
        var cursorInstructions = "\u001b[%i%p1%d;%p2%dH"u8.ToArray();
        var programSource = new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram(cursorInstructions),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8)
        };
        byte[] keySequence = [0x1b, (byte) '[', (byte) 'A'];
        var bindingSource = new List<KeyBinding>
        {
            new(keySequence, Code.Up, Modifiers.Control)
        };
        var profile = new TerminalProfile(
            new Description("database", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative,
            new Programs(programSource),
            new KeyMap(bindingSource));

        // Act
        cursorInstructions[0] = 9;
        keySequence[0] = 0xff;
        programSource.Clear();
        bindingSource.Clear();

        // Assert
        profile.Description.Suitability.ShouldBe(Suitability.Usable);
        profile.Programs.Count.ShouldBe(3);
        profile.Programs.TryGet("cup", out var cursor).ShouldBeTrue();
        cursor.Representation.Span[0].ShouldBe((byte) 0x1b);
        profile.KeyMap.Bindings.Count.ShouldBe(1);
        profile.KeyMap.Bindings[0].Sequence.Span.ToArray().ShouldBe([0x1b, (byte) '[', (byte) 'A']);
        profile.KeyMap.Bindings[0].Modifiers.ShouldBe(Modifiers.Control);
    }

    /// <summary>Verifies terminal key bytes are retained without UTF-8 decoding.</summary>
    [Fact]
    public void Constructor_WhenKeyBytesAreInvalidUtf8_OwnsExactBytes()
    {
        // Arrange
        byte[] invalidUtf8 = [0xff, 0xfe, 0x80];

        // Act
        var binding = new KeyBinding(invalidUtf8, Code.F1);
        invalidUtf8[0] = 0x00;

        // Assert
        binding.Sequence.Span.ToArray().ShouldBe([0xff, 0xfe, 0x80]);
    }

    /// <summary>Verifies one input sequence cannot identify conflicting logical keys.</summary>
    [Fact]
    public void Constructor_WhenKeyBytesHaveConflictingBindings_Throws()
    {
        // Arrange
        KeyBinding[] bindings =
        [
            new([0x1b, (byte) '[', (byte) 'A'], Code.Up),
            new([0x1b, (byte) '[', (byte) 'A'], Code.Down)
        ];

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() => new KeyMap(bindings));
    }

    /// <summary>Verifies a default key binding is rejected at the aggregate boundary.</summary>
    [Fact]
    public void Constructor_WhenKeyBindingIsDefault_Throws()
    {
        // Arrange
        KeyBinding[] bindings = [default];

        // Act
        var exception = Should.Throw<ArgumentException>(() => new KeyMap(bindings));

        // Assert
        exception.ParamName.ShouldBe("bindings");
    }

    #endregion

    #region Evidence validation

    /// <summary>Verifies public database evidence deconstructs and reconstructs without hidden proof.</summary>
    [Fact]
    public void Feature_WhenDatabaseEvidenceIsRoundTripped_PreservesValue()
    {
        // Arrange
        var original = new Feature(CapabilitySupport.Supported, Origin.Database);

        // Act
        var (state, origin) = original;
        var reconstructed = new Feature(state, origin);

        // Assert
        reconstructed.ShouldBe(original);
    }

    /// <summary>Verifies absent or empty backing programs cannot fabricate database support.</summary>
    [Fact]
    public void Capabilities_WhenBackingProgramIsMissing_DoesNotRecordDatabaseSupport()
    {
        // Arrange
        var description = new Description(
            "xterm-direct",
            DescriptionOrigin.Database,
            Suitability.Usable);
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram([1]),
            ["sgr0"] = new DescriptionProgram([2]),
            ["clear"] = new DescriptionProgram([3]),
            ["Ms"] = new DescriptionProgram([])
        });

        // Act
        var capabilities = TerminalCapabilities.Conservative.Apply(description,
            programs);

        // Assert
        capabilities.Osc52.ShouldBe(Feature.Unknown);
    }

    /// <summary>Verifies a database claim cannot move into a profile without its exact backing program.</summary>
    [Fact]
    public void Constructor_WhenDatabaseEvidenceIsTransplantedWithoutProgram_NormalizesClaim()
    {
        // Arrange
        var capabilities = TerminalCapabilities.Conservative with
        {
            Osc52 = new Feature(CapabilitySupport.Supported, Origin.Database),
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Database),
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Database)
        };

        // Act
        var profile = new TerminalProfile(
            DatabaseDescription(),
            capabilities,
            DatabasePrograms(includeOsc52: false),
            KeyMap.Empty);

        // Assert
        profile.Capabilities.Osc52.ShouldBe(Feature.Unknown);
        profile.Capabilities.SynchronizedOutput.ShouldBe(Feature.Unknown);
        profile.Capabilities.KittyGraphics.ShouldBe(Feature.Unknown);
    }

    #endregion

    #region Helpers

    private static Description DatabaseDescription() => new(
        "xterm-direct",
        DescriptionOrigin.Database,
        Suitability.Usable);

    private static Programs DatabasePrograms(bool includeOsc52)
    {
        var values = new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8)
        };

        if (includeOsc52)
        {
            values.Add("Ms", new DescriptionProgram("\u001b]52;%p1%s;%p2%s\a"u8));
        }

        return new Programs(values);
    }

    #endregion
}
