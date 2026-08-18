// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Backends;

using SharpVision.Terminal.Backends;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Discovery.Adapters;

/// <summary>Verifies terminal-backend resolution from source-specific redacted evidence.</summary>
public sealed class TerminalBackendResolverTests
{
    /// <summary>Verifies the terminal assembly exposes the backend-resolution evidence boundary.</summary>
    [Fact]
    public void BackendResolutionTypes_WhenLoadedFromTerminalAssembly_ArePresent()
    {
        var assembly = typeof(Renderer).Assembly;
        var names = new[]
        {
            "SharpVision.Terminal.Backends.BackendEvidenceOrigin",
            "SharpVision.Terminal.Backends.BackendEvidence",
            "SharpVision.Terminal.Backends.BackendResolution",
            "SharpVision.Terminal.Backends.TerminalBackendResolver",
            "SharpVision.Terminal.Discovery.Adapters.IBackendEvidenceAdapter",
            "SharpVision.Terminal.Discovery.Adapters.DescriptionBackendEvidenceAdapter",
            "SharpVision.Terminal.Discovery.Adapters.EnvironmentBackendEvidenceAdapter",
        };

        foreach (var name in names)
        {
            assembly.GetType(name).ShouldNotBeNull().FullName.ShouldBe(name);
        }
    }

    /// <summary>Verifies recognized environment identities select their canonical backend.</summary>
    [Fact]
    public void Resolve_WhenEnvironmentMatrixIsEvaluated_ReturnsCanonicalSingleton()
    {
        // Arrange
        (string Term, string? Program, TerminalBackend Expected)[] cases =
        [
            ("xterm-kitty", null, KittyBackend.Instance),
            ("xterm-256color", null, XtermBackend.Instance),
            ("xterm-256color", "iTerm.app", ItermBackend.Instance),
            ("screen-256color", null, VtBackend.Instance),
            ("tmux-256color", null, VtBackend.Instance),
            ("vt100", null, VtBackend.Instance),
        ];

        foreach (var (term, program, expected) in cases)
        {
            var environment = new Dictionary<string, string?> { ["TERM"] = term, ["TERM_PROGRAM"] = program };

            // Act
            var resolution = TerminalProfile.Conservative.Resolve(environment);

            // Assert
            resolution.Backend.ShouldBeSameAs(expected, $"TERM={term}; TERM_PROGRAM={program}");
        }
    }

    /// <summary>Verifies a TMUX session with TERM left at a bare xterm value — a common
    /// tmux.conf configuration (<c>set -g default-terminal "xterm-256color"</c>) — resolves the
    /// generic multiplexer-safe backend instead of Xterm, agreeing with the multiplexer-aware
    /// feature gating the rest of discovery already applies to the same environment.</summary>
    [Fact]
    public void Resolve_WhenTmuxEnvironmentVariableIsSetWithBareXtermTerm_ReturnsGenericBackend()
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-256color",
            ["TMUX"] = "/tmp/tmux-1000/default,1234,0"
        };

        var resolution = TerminalProfile.Conservative.Resolve(environment);

        resolution.Backend.ShouldBeSameAs(VtBackend.Instance);
    }

    /// <summary>Verifies a GNU screen session with TERM left at a bare xterm value — a common
    /// .screenrc configuration (<c>term xterm-256color</c>) — resolves the generic
    /// multiplexer-safe backend instead of Xterm, agreeing with the multiplexer-aware feature
    /// gating the rest of discovery already applies to the same environment.</summary>
    [Fact]
    public void Resolve_WhenStyEnvironmentVariableIsSetWithBareXtermTerm_ReturnsGenericBackend()
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-256color",
            ["STY"] = "1234.pts-0.hostname"
        };

        var resolution = TerminalProfile.Conservative.Resolve(environment);

        resolution.Backend.ShouldBeSameAs(VtBackend.Instance);
    }

    /// <summary>Verifies environment evidence wins an equally specific description candidate.</summary>
    [Fact]
    public void Resolve_WhenEnvironmentAndDescriptionRecognizeXterm_PublishesOrderedTypedEvidence()
    {
        // Arrange
        var profile = new TerminalProfile(
            new Description("xterm-description", DescriptionOrigin.BuiltIn, Suitability.Missing),
            TerminalCapabilities.Conservative);
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-256color" };

        // Act
        var resolution = profile.Resolve(environment);

        // Assert
        resolution.Backend.ShouldBeSameAs(XtermBackend.Instance);
        resolution.Evidence.ShouldBe(
        [
            new BackendEvidence(TerminalBackendKind.Xterm, BackendEvidenceOrigin.Description),
            new BackendEvidence(TerminalBackendKind.Xterm, BackendEvidenceOrigin.Environment),
        ]);
    }

    /// <summary>Verifies description recognition preserves the approved specificity order.</summary>
    [Fact]
    public void Resolve_WhenDescriptionContainsMultipleFragments_SelectsKitty()
    {
        // Arrange
        var profile = new TerminalProfile(
            new Description("xterm-iterm2-kitty", DescriptionOrigin.BuiltIn, Suitability.Missing),
            TerminalCapabilities.Conservative);

        // Act
        var resolution = profile.Resolve(new Dictionary<string, string?>());

        // Assert
        resolution.Backend.ShouldBeSameAs(KittyBackend.Instance);
        resolution.Evidence.ShouldBe(
        [new BackendEvidence(TerminalBackendKind.Kitty, BackendEvidenceOrigin.Description)]);
    }

    /// <summary>Verifies a more specific Kitty candidate wins a conflicting iTerm2 candidate.</summary>
    [Fact]
    public void Resolve_WhenKittyAndIterm2EvidenceConflict_SelectsKitty()
    {
        // Arrange
        var profile = new TerminalProfile(
            new Description("iTerm2", DescriptionOrigin.BuiltIn, Suitability.Missing),
            TerminalCapabilities.Conservative);
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" };

        // Act
        var resolution = profile.Resolve(environment);

        // Assert
        resolution.Backend.ShouldBeSameAs(KittyBackend.Instance);
        resolution.Evidence.ShouldBe(
        [
            new BackendEvidence(TerminalBackendKind.Iterm2, BackendEvidenceOrigin.Description),
            new BackendEvidence(TerminalBackendKind.Kitty, BackendEvidenceOrigin.Environment),
        ]);
    }

    /// <summary>Verifies optional capability evidence cannot change selected terminal identity.</summary>
    [Fact]
    public void Resolve_WhenKittyGraphicsIsSupportedWithoutKittyIdentity_SelectsXterm()
    {
        // Arrange
        var capabilities = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Query)
        };
        var profile = TerminalProfile.CreateAnsi(capabilities);
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-256color" };

        // Act
        var resolution = profile.Resolve(environment);

        // Assert
        resolution.Backend.ShouldBeSameAs(XtermBackend.Instance);
    }

    /// <summary>Verifies multiplexer TERM values never leak an outer terminal identity.</summary>
    [Fact]
    public void Resolve_WhenMultiplexerTermContainsXtermFragment_SelectsVtWithoutEvidence()
    {
        // Arrange
        var environment = new Dictionary<string, string?> { ["TERM"] = "screen-xterm-kitty" };

        // Act
        var resolution = TerminalProfile.Conservative.Resolve(environment);

        // Assert
        resolution.Backend.ShouldBeSameAs(VtBackend.Instance);
        resolution.Evidence.ShouldBeEmpty();
    }

    /// <summary>Verifies published evidence is typed metadata with no raw source values.</summary>
    [Fact]
    public void Resolve_WhenSourcesAreRecognized_PublishesOnlyRedactedTypedEvidence()
    {
        // Arrange
        const string descriptionName = "xterm-description-secret";
        const string term = "xterm-environment-secret";
        var profile = new TerminalProfile(
            new Description(descriptionName, DescriptionOrigin.BuiltIn, Suitability.Missing),
            TerminalCapabilities.Conservative);
        var environment = new Dictionary<string, string?> { ["TERM"] = term };

        // Act
        var resolution = profile.Resolve(environment);

        // Assert
        resolution.Evidence.ShouldAllBe(evidence =>
            Enum.IsDefined(evidence.Kind) && Enum.IsDefined(evidence.Origin));
        typeof(BackendEvidence)
            .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Select(property => property.Name)
            .ShouldBe(["Kind", "Origin"]);
        resolution.Evidence.Select(evidence => evidence.ToString()).ShouldNotContain(value =>
            value.Contains(descriptionName, StringComparison.Ordinal) || value.Contains(term, StringComparison.Ordinal));
    }

    /// <summary>Verifies backend resolution copies evidence before exposing an immutable collection.</summary>
    [Fact]
    public void Constructor_WhenEvidenceSourceChanges_PreservesAnImmutableSnapshot()
    {
        // Arrange
        var source = new List<BackendEvidence>
        {
            new(TerminalBackendKind.Xterm, BackendEvidenceOrigin.Environment)
        };

        // Act
        var resolution = new BackendResolution(XtermBackend.Instance, source);
        source.Clear();

        // Assert
        resolution.Evidence.ShouldBe(
        [new BackendEvidence(TerminalBackendKind.Xterm, BackendEvidenceOrigin.Environment)]);
        var mutableEvidence = resolution.Evidence.ShouldBeAssignableTo<IList<BackendEvidence>>();
        _ = Should.Throw<NotSupportedException>(mutableEvidence.Clear);
    }

    /// <summary>Verifies resolver and adapter public boundaries reject absent required input.</summary>
    [Fact]
    public void Boundaries_WhenRequiredInputIsNull_ThrowArgumentNullException()
    {
        // Arrange
        var environment = new Dictionary<string, string?>();

        // Act and assert
        _ = Should.Throw<ArgumentNullException>(() => ((TerminalProfile) null!).Resolve(environment));
        _ = Should.Throw<ArgumentNullException>(() => TerminalProfile.Conservative.Resolve(null!));
        _ = Should.Throw<ArgumentNullException>(() => new EnvironmentBackendEvidenceAdapter(null!));
        _ = Should.Throw<ArgumentNullException>(() => new DescriptionBackendEvidenceAdapter(null!));
        _ = Should.Throw<ArgumentNullException>(() => new BackendResolution(null!, []));
        _ = Should.Throw<ArgumentNullException>(() => new BackendResolution(VtBackend.Instance, null!));
    }

    /// <summary>Verifies TERM_PROGRAM=iTerm.app is still trusted under a multiplexer session, while
    /// TERM-based kitty/xterm detection is deliberately suppressed there: TMUX/GNU screen rewrite
    /// TERM to their own terminfo entry but leave TERM_PROGRAM (which reflects the outer physical
    /// terminal, not the multiplexer) untouched, so only TERM needs the multiplexer guard.</summary>
    [Fact]
    public void Resolve_WhenIterm2EnvironmentIsPresentUnderMultiplexer_StillResolvesIterm2()
    {
        var environment = new Dictionary<string, string?>
        {
            ["TERM"] = "screen-256color",
            ["TERM_PROGRAM"] = "iTerm.app",
            ["TMUX"] = "/tmp/tmux-1000/default,1234,0"
        };

        var resolution = TerminalProfile.Conservative.Resolve(environment);

        resolution.Backend.ShouldBeSameAs(ItermBackend.Instance);
    }

    /// <summary>Verifies typed backend evidence rejects undefined enum values.</summary>
    [Fact]
    public void Constructor_WhenEvidenceEnumIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new BackendEvidence((TerminalBackendKind) int.MaxValue, BackendEvidenceOrigin.Description));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new BackendEvidence(TerminalBackendKind.Vt, (BackendEvidenceOrigin) int.MaxValue));
    }
}
