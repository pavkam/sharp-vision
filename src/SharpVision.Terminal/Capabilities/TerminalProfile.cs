// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using Discovery.Adapters;

/// <summary>
/// Owns one immutable terminal description and its semantic capabilities without
/// exposing native buffers or raw terminal command strings.
/// </summary>
[PublicAPI]
public sealed class TerminalProfile
{
    private static readonly Description _ansiDescription = new(
        "ansi",
        DescriptionOrigin.BuiltIn,
        Suitability.Usable,
        automaticMargins: true);

    private static readonly Programs _ansiPrograms = new(
        new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = DescriptionProgram.Intrinsic,
            ["sgr0"] = DescriptionProgram.Intrinsic,
            ["clear"] = DescriptionProgram.Intrinsic,
            ["smcup"] = DescriptionProgram.Intrinsic,
            ["rmcup"] = DescriptionProgram.Intrinsic,
            ["civis"] = DescriptionProgram.Intrinsic,
            ["cnorm"] = DescriptionProgram.Intrinsic,
            ["el"] = DescriptionProgram.Intrinsic,
            ["ed"] = DescriptionProgram.Intrinsic,
            ["bold"] = DescriptionProgram.Intrinsic,
            ["dim"] = DescriptionProgram.Intrinsic,
            ["sitm"] = DescriptionProgram.Intrinsic,
            ["smul"] = DescriptionProgram.Intrinsic,
            ["blink"] = DescriptionProgram.Intrinsic,
            ["rev"] = DescriptionProgram.Intrinsic,
            ["invis"] = DescriptionProgram.Intrinsic,
            ["smxx"] = DescriptionProgram.Intrinsic,
            ["Smol"] = DescriptionProgram.Intrinsic,
            ["setaf"] = DescriptionProgram.Intrinsic,
            ["setab"] = DescriptionProgram.Intrinsic,
            ["setrgbf"] = DescriptionProgram.Intrinsic,
            ["setrgbb"] = DescriptionProgram.Intrinsic,
            ["setdf"] = DescriptionProgram.Intrinsic,
            ["setdb"] = DescriptionProgram.Intrinsic,
            ["op"] = DescriptionProgram.Intrinsic,
            ["bel"] = DescriptionProgram.Intrinsic
        });

    /// <summary>
    /// Gets the stable safe profile used when no terminal description is
    /// available; it is not suitable for full-screen startup.
    /// </summary>
    public static TerminalProfile Conservative { get; } = new(
        new Description(
            "conservative",
            DescriptionOrigin.BuiltIn,
            Suitability.Missing),
        TerminalCapabilities.Conservative,
        Programs.Empty,
        KeyMap.Empty);

    /// <summary>
    /// Initializes one immutable semantic-only terminal profile without compiled
    /// programs or key bindings. A usable description is published as
    /// <see cref="Suitability.Incomplete"/> because required programs are absent.
    /// </summary>
    /// <param name="description">The non-null owned description metadata.</param>
    /// <param name="capabilities">The non-null owned semantic capabilities.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="description"/> or <paramref name="capabilities"/> is <see langword="null"/>.
    /// </exception>
    public TerminalProfile(
        Description description,
        TerminalCapabilities capabilities) :
        this(description, capabilities, Programs.Empty, KeyMap.Empty)
    {
    }

    /// <summary>Initializes an immutable profile from already-owned internal snapshots.</summary>
    /// <param name="description">The non-null validated description metadata.</param>
    /// <param name="capabilities">The non-null semantic capabilities.</param>
    /// <param name="programs">The non-null immutable compiled-program aggregate.</param>
    /// <param name="keyMap">The non-null immutable key-binding aggregate.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    internal TerminalProfile(
        Description description,
        TerminalCapabilities capabilities,
        Programs programs,
        KeyMap keyMap) : this(
        description,
        capabilities,
        programs,
        keyMap,
        preserveExactCapabilities: false)
    {
    }

    private TerminalProfile(
        Description description,
        TerminalCapabilities capabilities,
        Programs programs,
        KeyMap keyMap,
        bool preserveExactCapabilities)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(keyMap);

        Description = description.Suitability == Suitability.Usable && !programs.FullScreenReady
            ? description.WithSuitability(Suitability.Incomplete)
            : description;
        // Only the library-owned ANSI compatibility profile may retain caller
        // semantic evidence without database-program proof. Every database and
        // general profile still crosses the normal evidence-validation boundary.
        Capabilities = preserveExactCapabilities
            ? capabilities
            : capabilities.Apply(Description, programs);
        Programs = programs;
        KeyMap = keyMap;
    }

    /// <summary>Gets the immutable terminal-description metadata.</summary>
    public Description Description { get; }

    /// <summary>Gets the immutable semantic capabilities used by existing renderers and services.</summary>
    public TerminalCapabilities Capabilities { get; }

    /// <summary>Gets the owned immutable compiled-program aggregate.</summary>
    internal Programs Programs { get; }

    /// <summary>Gets the owned immutable key-binding aggregate.</summary>
    internal KeyMap KeyMap { get; }

    /// <summary>Gets whether this profile uses the built-in ANSI compatibility program set.</summary>
    /// <remarks>
    /// A compatibility profile carries no real terminal description, so description-driven output
    /// such as the <c>TS</c>/<c>fsl</c> title pair is unavailable and callers must fall back to the
    /// equivalent ANSI escape sequence.
    /// </remarks>
    public bool AnsiCompatible =>
        ReferenceEquals(Description, _ansiDescription) && ReferenceEquals(Programs, _ansiPrograms);

    /// <summary>Gets whether the explicit built-in ANSI grammar supplements exact described keys.</summary>
    internal bool UsesAnsiKeyGrammar => AnsiCompatible;

    /// <summary>Gets the effective renderer color tier backed by complete programs.</summary>
    internal ColorDepth RenderingColorDepth =>
        AnsiCompatible ? Capabilities.ColorDepth : Programs.EffectiveColorDepth(Capabilities.ColorDepth);

    /// <summary>Creates a usable built-in ANSI profile around exact caller-supplied capabilities.</summary>
    /// <param name="capabilities">The non-null immutable semantic capabilities to retain.</param>
    /// <returns>A new profile using the stable built-in ANSI description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is <see langword="null"/>.</exception>
    [Pure]
    public static TerminalProfile CreateAnsi(TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return new TerminalProfile(
            _ansiDescription,
            capabilities,
            _ansiPrograms,
            KeyMap.Empty,
            preserveExactCapabilities: true);
    }

    /// <summary>Creates an expander over this profile's compiled terminfo programs.</summary>
    /// <remarks>
    /// The returned expander owns bounded interpreter state and is therefore single-owner: its
    /// calls must be serialized and never overlap. Create one per owner instead of sharing it, and
    /// rebuild it when <see cref="ProgramExpander.AppliesTo"/> reports it is no longer current.
    /// </remarks>
    /// <param name="limits">The finite interpretation limits, or <see langword="null"/> for defaults.</param>
    /// <returns>A new expander bound to this profile.</returns>
    public ProgramExpander CreateProgramExpander(ProgramLimits? limits = null) => new(this, limits);

    /// <summary>Creates a profile retaining this description, programs, and key map with replacement semantics.</summary>
    /// <param name="capabilities">The non-null immutable semantic capabilities to publish.</param>
    /// <returns>A new profile retaining all non-semantic terminal-description state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is <see langword="null"/>.</exception>
    [Pure]
    public TerminalProfile WithCapabilities(TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return new TerminalProfile(
            Description,
            capabilities,
            Programs,
            KeyMap,
            preserveExactCapabilities: AnsiCompatible);
    }

    /// <summary>Compares all state that can change terminal rendering output.</summary>
    /// <param name="other">The comparison profile.</param>
    /// <returns>Whether capabilities, description semantics, and programs are equivalent.</returns>
    [Pure]
    internal bool IsRenderingEquivalentTo(TerminalProfile other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Equals(Capabilities, other.Capabilities) &&
               Description.Name == other.Description.Name &&
               Description.Origin == other.Description.Origin &&
               Description.Suitability == other.Description.Suitability &&
               Description.Columns == other.Description.Columns &&
               Description.Lines == other.Description.Lines &&
               Description.Colors == other.Description.Colors &&
               Description.AutomaticMargins == other.Description.AutomaticMargins &&
               Description.BackColorErase == other.Description.BackColorErase &&
               Description.EatNewlineGlitch == other.Description.EatNewlineGlitch &&
               Programs.SemanticallyEquals(other.Programs);
    }
}
