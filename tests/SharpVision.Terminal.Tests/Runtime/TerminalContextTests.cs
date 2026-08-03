// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Backends;
using SharpVision.Terminal.Capabilities;

/// <summary>Verifies immutable runtime terminal context ownership.</summary>
public sealed class TerminalContextTests
{
    /// <summary>Verifies runtime context is available to bind profile and backend identity.</summary>
    [Fact]
    public void RuntimeAssembly_WhenTerminalContextIsRequested_ExposesContextType()
    {
        var contextType = typeof(TerminalOptions).Assembly.GetType(
            "SharpVision.Terminal.Runtime.TerminalContext",
            throwOnError: false);

        _ = contextType.ShouldNotBeNull();
    }

    /// <summary>Verifies capability refinement replaces the profile without changing backend identity.</summary>
    [Fact]
    public void WithCapabilities_WhenCapabilitiesAreRefined_PreservesBackendAndProfileSemantics()
    {
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        var context = new TerminalContext(profile, KittyBackend.Instance);
        var capabilities = profile.Capabilities with
        {
            ColorDepth = ColorDepth.Monochrome,
            ColorOrigin = Origin.Override
        };

        var refined = context.WithCapabilities(capabilities);

        refined.ShouldNotBeSameAs(context);
        refined.Backend.ShouldBeSameAs(KittyBackend.Instance);
        refined.Profile.Capabilities.ShouldBeSameAs(capabilities);
        refined.Profile.Description.ShouldBeSameAs(profile.Description);
        refined.Profile.Programs.ShouldBeSameAs(profile.Programs);
        refined.Profile.KeyMap.ShouldBeSameAs(profile.KeyMap);
    }

    /// <summary>Verifies context boundaries reject absent immutable values.</summary>
    [Fact]
    public void ContextBoundaries_WhenRequiredValueIsNull_ThrowArgumentNullException()
    {
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        var context = new TerminalContext(profile, VtBackend.Instance);

        _ = Should.Throw<ArgumentNullException>(() => new TerminalContext(null!, VtBackend.Instance));
        _ = Should.Throw<ArgumentNullException>(() => new TerminalContext(profile, null!));
        _ = Should.Throw<ArgumentNullException>(() => context.WithCapabilities(null!));
    }

    /// <summary>Verifies supplied negotiation environment selects the iTerm2 identity once.</summary>
    [Fact]
    public void CreateContext_WhenNegotiationContainsItermEnvironment_ResolvesItermBackend()
    {
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>
            {
                ["TERM"] = "xterm-256color",
                ["TERM_PROGRAM"] = "iTerm.app"
            })
        };

        var context = options.CreateContext();

        context.Profile.ShouldBeSameAs(options.Profile);
        context.Backend.ShouldBeSameAs(ItermBackend.Instance);
    }

    /// <summary>Verifies context creation uses description evidence and otherwise safely falls back to VT.</summary>
    [Fact]
    public void CreateContext_WhenNegotiationIsAbsent_UsesDescriptionEvidenceOrVtFallback()
    {
        var xtermProfile = new TerminalProfile(
            new Description("xterm-description", DescriptionOrigin.Explicit, Suitability.Missing),
            TerminalCapabilities.Conservative);
        var xtermOptions = TerminalOptions.Minimal with { Profile = xtermProfile };

        xtermOptions.CreateContext().Backend.ShouldBeSameAs(XtermBackend.Instance);
        TerminalOptions.Minimal.CreateContext().Backend.ShouldBeSameAs(VtBackend.Instance);
    }
}
