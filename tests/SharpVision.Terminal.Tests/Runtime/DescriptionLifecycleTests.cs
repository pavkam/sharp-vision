// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Input;

/// <summary>Verifies description-owned terminal lifecycle programs and keypad selection.</summary>
public sealed class DescriptionLifecycleTests
{
    #region Value and argument contracts

    /// <summary>Verifies exact lease byte ownership and empty-command validation.</summary>
    [Fact]
    public void Constructor_WhenLeaseCommandsAreInvalid_ThrowsOrOwnsBytes()
    {
        // Arrange
        var enable = "on"u8.ToArray();
        var disable = "off"u8.ToArray();

        // Act
        var lease = new Lease(enable, disable);
        enable[0] = (byte) 'x';
        disable[0] = (byte) 'x';

        // Assert
        Encoding.ASCII.GetString(lease.Enable.Span).ShouldBe("on");
        Encoding.ASCII.GetString(lease.Disable.Span).ShouldBe("off");
        _ = Should.Throw<ArgumentException>(() => new Lease([], "off"u8));
        _ = Should.Throw<ArgumentException>(() => new Lease("on"u8, []));
    }

    /// <summary>Verifies lifecycle-pair expansion validates names and its session interpreter.</summary>
    [Fact]
    public void TryExpandPair_WhenArgumentIsInvalid_Throws()
    {
        // Arrange
        var programs = new Programs(KeypadPrograms());
        var interpreter = new Interpreter(ProgramLimits.Default);

        // Act / Assert
        _ = Should.Throw<ArgumentException>(() =>
            programs.TryExpandPair("", "rmkx", interpreter, out _, out _));
        _ = Should.Throw<ArgumentException>(() =>
            programs.TryExpandPair("smkx", " ", interpreter, out _, out _));
        _ = Should.Throw<ArgumentNullException>(() =>
            programs.TryExpandPair("smkx", "rmkx", null!, out _, out _));
    }

    /// <summary>Verifies every unavailable pair result clears both returned byte snapshots.</summary>
    [Fact]
    public void TryExpandPair_WhenPairIsUnavailable_ReturnsEmptyOutputs()
    {
        // Arrange
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["smkx"] = new DescriptionProgram("one-sided"u8)
        });
        var interpreter = new Interpreter(ProgramLimits.Default);

        // Act
        var expanded = programs.TryExpandPair(
            "smkx",
            "rmkx",
            interpreter,
            out var enable,
            out var disable);

        // Assert
        expanded.ShouldBeFalse();
        enable.IsEmpty.ShouldBeTrue();
        disable.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies successful pair expansion returns exact independently owned memories.</summary>
    [Fact]
    public void TryExpandPair_WhenProgramsAreValid_ReturnsIndependentExactOutputs()
    {
        // Arrange
        var programs = new Programs(KeypadPrograms());
        var interpreter = new Interpreter(ProgramLimits.Default);

        // Act
        var expanded = programs.TryExpandPair(
            "smkx",
            "rmkx",
            interpreter,
            out var enable,
            out var disable);

        // Assert
        expanded.ShouldBeTrue();
        Encoding.ASCII.GetString(enable.Span).ShouldBe("keys-in");
        Encoding.ASCII.GetString(disable.Span).ShouldBe("keys-out");
        MemoryMarshal.TryGetArray(enable, out var enableSegment).ShouldBeTrue();
        MemoryMarshal.TryGetArray(disable, out var disableSegment).ShouldBeTrue();
        enableSegment.Array.ShouldNotBeSameAs(disableSegment.Array);
    }

    /// <summary>Verifies mixed compiled/intrinsic pairs reject without changing interpreter statics.</summary>
    [Fact]
    public void TryExpandPair_WhenProgramKindsAreMixed_ReturnsEmptyAndPreservesStaticState()
    {
        // Arrange
        var programs = new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("%{42}%PAenter"u8),
            ["rmcup"] = DescriptionProgram.Intrinsic
        });
        var interpreter = new Interpreter(ProgramLimits.Default);
        var readStatic = new ArrayBufferWriter<byte>();

        // Act
        var expanded = programs.TryExpandPair(
            "smcup",
            "rmcup",
            interpreter,
            out var enable,
            out var disable);
        interpreter.Write(new DescriptionProgram("%gA%d"u8), [], readStatic);

        // Assert
        expanded.ShouldBeFalse();
        enable.IsEmpty.ShouldBeTrue();
        disable.IsEmpty.ShouldBeTrue();
        Encoding.ASCII.GetString(readStatic.WrittenSpan).ShouldBe("0");
    }

    #endregion

    #region Description pairs

    /// <summary>Verifies noncanonical lifecycle bytes are emitted exactly and restored in reverse order.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionProvidesLifecyclePairs_UsesExactProgramsAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["smcup"] = new("screen-in"u8),
                ["rmcup"] = new("screen-out"u8),
                ["civis"] = new("cursor-off"u8),
                ["cnorm"] = new("cursor-on"u8)
            });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("screen-incursor-offcursor-onscreen-out");
    }

    /// <summary>Verifies one-sided lifecycle descriptions never emit a half lease.</summary>
    [Theory]
    [InlineData("smcup")]
    [InlineData("rmcup")]
    [InlineData("civis")]
    [InlineData("cnorm")]
    public async Task RunAsync_WhenDescriptionLifecyclePairIsIncomplete_OmitsPairAsync(
        string retainedName)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            [retainedName] = new DescriptionProgram("one-sided"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies a zero-parameter lifecycle path rejects parameter-consuming programs atomically.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecycleProgramConsumesParameter_OmitsPairBeforeOutputAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("prefix%p1%dsuffix"u8),
            ["rmcup"] = new DescriptionProgram("restore"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    #endregion

    #region Keypad selection

    /// <summary>Verifies every exact SS3 application cursor spelling leases the described keypad pair.</summary>
    /// <param name="code">The logical cursor, Home, or End code.</param>
    /// <param name="final">The exact SS3 final byte.</param>
    [Theory]
    [InlineData(Code.Up, 'A')]
    [InlineData(Code.Down, 'B')]
    [InlineData(Code.Right, 'C')]
    [InlineData(Code.Left, 'D')]
    [InlineData(Code.Home, 'H')]
    [InlineData(Code.End, 'F')]
    public async Task RunAsync_WhenKeyMapContainsApplicationCursorBinding_LeasesKeypadAsync(
        Code code,
        char final)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var sequence = new[] { (byte) 0x1b, (byte) 'O', checked((byte) final) };
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["smkx"] = new("keys-in"u8),
                ["rmkx"] = new("keys-out"u8)
            },
            new KeyMap([new KeyBinding(sequence, code)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("keys-inkeys-out");
    }

    /// <summary>Verifies an eight-bit SS3 application cursor spelling selects the described keypad pair.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapContainsEightBitApplicationCursorBinding_LeasesKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["smkx"] = new("keys-in"u8),
                ["rmkx"] = new("keys-out"u8)
            },
            new KeyMap([new KeyBinding([0x8f, (byte) 'A'], Code.Up)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("keys-inkeys-out");
    }

    /// <summary>Verifies an SS3 final paired with the wrong logical code does not request application mode.</summary>
    [Fact]
    public async Task RunAsync_WhenSs3ApplicationFinalHasMismatchedCode_OmitsKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            KeypadPrograms(),
            new KeyMap([new KeyBinding("\u001bOA"u8, Code.Down)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies an application binding never permits a one-sided keypad lease.</summary>
    /// <param name="retainedName">The only retained keypad program name.</param>
    [Theory]
    [InlineData("smkx")]
    [InlineData("rmkx")]
    public async Task RunAsync_WhenApplicationKeyMapHasOneSidedKeypadPair_OmitsKeypadAsync(
        string retainedName)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                [retainedName] = new DescriptionProgram("one-sided"u8)
            },
            new KeyMap([new KeyBinding("\u001bOA"u8, Code.Up)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies normal cursor spellings do not require terminal application-key mode.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapContainsOnlyNormalCursorBinding_OmitsKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            KeypadPrograms(),
            new KeyMap([new KeyBinding("\u001b[A"u8, Code.Up)]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies SS3 function keys remain valid normal-mode spellings and do not force keypad mode.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapContainsOnlySs3FunctionKeys_OmitsKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            KeypadPrograms(),
            new KeyMap(
            [
                new KeyBinding("\u001bOP"u8, Code.F1),
                new KeyBinding("\u001bOQ"u8, Code.F2),
                new KeyBinding("\u001bOR"u8, Code.F3),
                new KeyBinding("\u001bOS"u8, Code.F4)
            ]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies a mixed normal/application map still requests its described application mode.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapMixesNormalAndApplicationCursorBindings_LeasesKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(
            KeypadPrograms(),
            new KeyMap(
            [
                new KeyBinding("\u001b[A"u8, Code.Up),
                new KeyBinding("\u001bOA"u8, Code.Up)
            ]));
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("keys-inkeys-out");
    }

    /// <summary>Verifies an empty key map never enters keypad application mode.</summary>
    [Fact]
    public async Task RunAsync_WhenKeyMapIsEmpty_OmitsKeypadAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(KeypadPrograms());
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with { Profile = profile });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    #endregion

    #region Failure recovery

    /// <summary>Verifies a partial acquire write is conservatively restored with the exact paired program.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionAcquirePartiallyWrites_RestoresAndPreservesFailureAsync()
    {
        // Arrange
        await using SessionTransport transport = new()
        {
            FailWriteAt = 1,
            PartialWriteBytes = 4
        };
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("screen-in"u8),
            ["rmcup"] = new DescriptionProgram("screen-out"u8)
        });
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        thrown.ShouldBeSameAs(transport.WriteFailure);
        transport.JoinedWrites.ShouldBe("screscreen-out");
    }

    /// <summary>Verifies cancellation after a partial acquire still runs exact uncancelled cleanup.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionAcquireIsCancelledAfterPartialWrite_RestoresAsync()
    {
        // Arrange
        await using SessionTransport transport = new()
        {
            CancelWriteAt = 1,
            PartialWriteBytes = 3
        };
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("screen-in"u8),
            ["rmcup"] = new DescriptionProgram("screen-out"u8)
        });
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        transport.JoinedWrites.ShouldBe("scrscreen-out");
    }

    /// <summary>Verifies a failed acquire flush still restores the exact possibly-active lease.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionAcquireFlushFails_RestoresAndPreservesFailureAsync()
    {
        // Arrange
        await using SessionTransport transport = new() { FailFlushAt = 1 };
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("screen-in"u8),
            ["rmcup"] = new DescriptionProgram("screen-out"u8)
        });
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        thrown.ShouldBeSameAs(transport.FlushFailure);
        transport.JoinedWrites.ShouldBe("screen-inscreen-out");
    }

    /// <summary>Verifies cleanup continues in reverse exact order while the original read failure remains primary.</summary>
    [Fact]
    public async Task RunAsync_WhenDescriptionCleanupAndReadFail_PreservesReadAndContinuesCleanupAsync()
    {
        // Arrange
        await using SessionTransport transport = new()
        {
            ReadFailure = new IOException("read failed"),
            FailWriteAt = 4
        };
        await using FakeResizeSource resize = new();
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["smcup"] = new("screen-in"u8),
                ["rmcup"] = new("screen-out"u8),
                ["civis"] = new("cursor-off"u8),
                ["cnorm"] = new("cursor-on"u8),
                ["smkx"] = new("keys-in"u8),
                ["rmkx"] = new("keys-out"u8)
            },
            new KeyMap([new KeyBinding("\u001bOA"u8, Code.Up)]));
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await session.RunAsync(TestContext.Current.CancellationToken));

        // Assert
        thrown.ShouldBeSameAs(transport.ReadFailure);
        session.LastCleanupException.ShouldBeSameAs(transport.WriteFailure);
        transport.JoinedWrites.ShouldBe(
            "screen-incursor-offkeys-incursor-onscreen-out");
    }

    #endregion

    #region Bounds, evidence, and interpreter state

    /// <summary>Verifies supported environment/default evidence never authorizes optional output.</summary>
    /// <param name="origin">The insufficient semantic evidence origin.</param>
    /// <param name="explicitProfile">Whether to use an explicit rather than built-in ANSI profile.</param>
    [Theory]
    [InlineData(Origin.Environment, false)]
    [InlineData(Origin.Default, false)]
    [InlineData(Origin.Environment, true)]
    [InlineData(Origin.Default, true)]
    public async Task RunAsync_WhenOptionalSupportOriginIsNotAuthoritative_OmitsModeAsync(
        Origin origin,
        bool explicitProfile)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var capabilities = TerminalCapabilities.Conservative with
        {
            FocusReporting = new Feature(CapabilitySupport.Supported, origin)
        };
        var profile = explicitProfile
            ? Profile(
                new Dictionary<string, DescriptionProgram>(),
                capabilities: capabilities,
                descriptionOrigin: DescriptionOrigin.Explicit)
            : TerminalProfile.CreateAnsi(capabilities);
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                Focus = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies bounded-query and explicit-override evidence authorize typed optional output.</summary>
    /// <param name="origin">The authoritative semantic evidence origin.</param>
    [Theory]
    [InlineData(Origin.Query)]
    [InlineData(Origin.Override)]
    public async Task RunAsync_WhenOptionalSupportOriginIsAuthoritative_UsesTypedModeAsync(
        Origin origin)
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var capabilities = TerminalCapabilities.Conservative with
        {
            FocusReporting = new Feature(CapabilitySupport.Supported, origin)
        };
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = TerminalProfile.CreateAnsi(capabilities),
                Focus = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("\u001b[?1004h\u001b[?1004l");
    }

    /// <summary>Verifies session expansion obeys the configured program-output bound before any write.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecycleExpansionExceedsLimit_OmitsPairBeforeOutputAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("1234"u8),
            ["rmcup"] = new DescriptionProgram("4321"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                Input = Options.Default with
                {
                    ProgramLimits = ProgramLimits.Default with { MaxProgramOutputBytes = 3 }
                }
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBeEmpty();
    }

    /// <summary>Verifies exact database focus backing permits the typed focus lease.</summary>
    [Fact]
    public async Task RunAsync_WhenDatabaseFocusBackingIsComplete_UsesTypedLeaseAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var supported = new Feature(CapabilitySupport.Supported, Origin.Database);
        var profile = Profile(
            new Dictionary<string, DescriptionProgram>
            {
                ["fe"] = new("focus-in"u8),
                ["fd"] = new("focus-out"u8),
                ["kxIN"] = new("event-in"u8),
                ["kxOUT"] = new("event-out"u8)
            },
            capabilities: TerminalCapabilities.Conservative with
            {
                FocusReporting = supported
            });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                Focus = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("\u001b[?1004h\u001b[?1004l");
    }

    /// <summary>Verifies one session preserves ncurses static variables across paired programs.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecycleProgramsShareStaticVariable_PreservesSessionStateAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("%{42}%PAenter"u8),
            ["rmcup"] = new DescriptionProgram("%gA%dexit"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("enter42exit");
    }

    /// <summary>Verifies a rejected pair cannot leak staged static-variable changes into a later pair.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecyclePairExpansionFails_RollsBackStaticVariablesAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("%{42}%PAenter"u8),
            ["rmcup"] = new DescriptionProgram("%p1%d"u8),
            ["civis"] = new DescriptionProgram("%gA%dhide"u8),
            ["cnorm"] = new DescriptionProgram("show"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("0hideshow");
    }

    /// <summary>Verifies an empty pair expansion is non-emittable and cannot commit static variables.</summary>
    [Fact]
    public async Task RunAsync_WhenLifecyclePairExpandsEmpty_RollsBackStaticVariablesAsync()
    {
        // Arrange
        await using SessionTransport transport = new();
        await using FakeResizeSource resize = new();
        var profile = Profile(new Dictionary<string, DescriptionProgram>
        {
            ["smcup"] = new DescriptionProgram("%{42}%PA%?%{0}%tenter%;"u8),
            ["rmcup"] = new DescriptionProgram("restore"u8),
            ["civis"] = new DescriptionProgram("%gA%dhide"u8),
            ["cnorm"] = new DescriptionProgram("show"u8)
        });
        transport.Close();
        await using Session session = new(
            transport,
            resize,
            new RuntimeSink(),
            RuntimeOptions.Minimal with
            {
                Profile = profile,
                AlternateScreen = true,
                HideCursor = true
            });

        // Act
        await session.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        transport.JoinedWrites.ShouldBe("0hideshow");
    }

    #endregion

    #region Helpers

    private static Dictionary<string, DescriptionProgram> KeypadPrograms() => new(StringComparer.Ordinal)
    {
        ["smkx"] = new DescriptionProgram("keys-in"u8),
        ["rmkx"] = new DescriptionProgram("keys-out"u8)
    };

    private static TerminalProfile Profile(
        IReadOnlyDictionary<string, DescriptionProgram> lifecyclePrograms,
        KeyMap? keyMap = null,
        TerminalCapabilities? capabilities = null,
        DescriptionOrigin descriptionOrigin = DescriptionOrigin.Database)
    {
        var programs = new Dictionary<string, DescriptionProgram>(StringComparer.Ordinal)
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("reset"u8),
            ["clear"] = new DescriptionProgram("clear"u8)
        };

        foreach (var pair in lifecyclePrograms)
        {
            programs.Add(pair.Key, pair.Value);
        }

        return new TerminalProfile(
            new Description("fixture", descriptionOrigin, Suitability.Usable),
            capabilities ?? TerminalCapabilities.Conservative,
            new Programs(programs),
            keyMap ?? KeyMap.Empty);
    }

    #endregion
}
