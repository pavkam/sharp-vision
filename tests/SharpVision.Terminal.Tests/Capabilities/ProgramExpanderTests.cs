// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies the public program-expansion seam that lets code outside the terminal assembly reach
/// description-driven output without touching the compiled program table or the interpreter.
/// </summary>
public sealed class ProgramExpanderTests
{
    /// <summary>Verifies a declared program expands to its exact described bytes.</summary>
    [Fact]
    public void TryWrite_WhenProgramIsDeclared_AppendsExactBytes()
    {
        var expander = Profile(("bel", "\u0007")).CreateProgramExpander();
        var destination = new ArrayBufferWriter<byte>();

        var written = expander.TryWrite("bel", [], destination);

        written.ShouldBeTrue();
        destination.WrittenSpan.ToArray().ShouldBe("\u0007"u8.ToArray());
    }

    /// <summary>
    /// Verifies an absent program reports failure and appends nothing, so a caller can treat the
    /// result as unsupported without inspecting or rewinding the destination.
    /// </summary>
    [Fact]
    public void TryWrite_WhenProgramIsAbsent_LeavesDestinationUntouched()
    {
        var expander = Profile(("bel", "\u0007")).CreateProgramExpander();
        var destination = new ArrayBufferWriter<byte>();

        var written = expander.TryWrite("flash", [], destination);

        written.ShouldBeFalse();
        destination.WrittenCount.ShouldBe(0);
    }

    /// <summary>Verifies a declared lifecycle pair expands into two independently owned snapshots.</summary>
    [Fact]
    public void TryExpandPair_WhenBothProgramsAreDeclared_ReturnsOwnedSnapshots()
    {
        var expander = Profile(("TS", "\u001b]0;"), ("fsl", "\u0007")).CreateProgramExpander();

        var expanded = expander.TryExpandPair("TS", "fsl", out var enable, out var disable);

        expanded.ShouldBeTrue();
        enable.ToArray().ShouldBe("\u001b]0;"u8.ToArray());
        disable.ToArray().ShouldBe("\u0007"u8.ToArray());
    }

    /// <summary>Verifies an incomplete pair commits nothing and reports both snapshots empty.</summary>
    [Fact]
    public void TryExpandPair_WhenOneProgramIsMissing_ReturnsEmptySnapshots()
    {
        var expander = Profile(("TS", "\u001b]0;")).CreateProgramExpander();

        var expanded = expander.TryExpandPair("TS", "fsl", out var enable, out var disable);

        expanded.ShouldBeFalse();
        enable.IsEmpty.ShouldBeTrue();
        disable.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Verifies presence queries agree with what expansion can actually produce.</summary>
    [Fact]
    public void HasAndHasPair_WhenProgramsAreDeclared_MatchExpansionAvailability()
    {
        var expander = Profile(("bel", "\u0007"), ("TS", "\u001b]0;"), ("fsl", "\u0007"))
            .CreateProgramExpander();

        expander.Has("bel").ShouldBeTrue();
        expander.Has("flash").ShouldBeFalse();
        expander.HasPair("TS", "fsl").ShouldBeTrue();
        expander.HasPair("TS", "missing").ShouldBeFalse();
    }

    /// <summary>
    /// Verifies capability refinement alone does not stale an expander. Negotiation replaces the
    /// profile object on every step while leaving the compiled description untouched, so rebuilding
    /// on every reference change would discard warmed interpreter state for nothing.
    /// </summary>
    [Fact]
    public void AppliesTo_WhenOnlyCapabilitiesChange_RemainsCurrentUntilProgramsDiffer()
    {
        var profile = Profile(("bel", "\u0007"));
        var expander = profile.CreateProgramExpander();
        var refined = profile.WithCapabilities(profile.Capabilities with
        {
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Query)
        });
        var different = Profile(("bel", "\u0007"), ("flash", "\u001b[?5h"));

        expander.AppliesTo(profile).ShouldBeTrue();
        expander.AppliesTo(refined).ShouldBeFalse();
        expander.AppliesTo(different).ShouldBeFalse();
    }

    /// <summary>Verifies the expander validates its arguments before touching interpreter state.</summary>
    [Fact]
    public void Members_WhenArgumentsAreInvalid_Throw()
    {
        var expander = Profile(("bel", "\u0007")).CreateProgramExpander();
        var destination = new ArrayBufferWriter<byte>();

        _ = Should.Throw<ArgumentNullException>(() => expander.Has(null!));
        _ = Should.Throw<ArgumentException>(() => expander.Has("  "));
        _ = Should.Throw<ArgumentNullException>(() => expander.AppliesTo(null!));
        _ = Should.Throw<ArgumentNullException>(() => expander.TryWrite("bel", [], null!));
        _ = Should.Throw<ArgumentException>(
            () => expander.TryExpandPair(" ", "fsl", out _, out _));
    }

    private static TerminalProfile Profile(params (string Name, string Source)[] programs)
    {
        var values = programs.ToDictionary(
            static program => program.Name,
            static program => new Program(Encoding.ASCII.GetBytes(program.Source)));
        var description = new Description(
            "expander-test",
            DescriptionOrigin.Database,
            Suitability.Usable,
            automaticMargins: true);

        return new TerminalProfile(
            description,
            TerminalCapabilities.Conservative,
            new Programs(values),
            KeyMap.Empty);
    }
}
