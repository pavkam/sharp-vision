// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

using Capabilities;

/// <summary>
/// Expands the named terminfo programs of one terminal profile into caller-owned bytes.
/// </summary>
/// <remarks>
/// <para>
/// This is the supported way for code outside the terminal assembly to reach description-driven
/// output such as <c>bel</c> or the <c>TS</c>/<c>fsl</c> title pair. It owns the compiled program
/// table and the bounded interpreter that executes it, so the ncurses execution machinery, its
/// static-variable transactions, and the compiled program representation all stay internal.
/// </para>
/// <para>
/// An expander is a single-owner object. The interpreter it owns holds reusable stacks, staging
/// buffers, and ncurses static variables, so calls must never overlap: the owner serializes them,
/// and no call may be made reentrantly from inside another. Create one expander per owner rather
/// than sharing one across threads.
/// </para>
/// <para>
/// An expander is bound to the profile that created it. Recreate it when the active profile
/// changes so expansion never runs against a stale description.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class ProgramExpander
{
    private readonly TerminalProfile _profile;
    private readonly Programs _programs;
    private readonly Interpreter _interpreter;

    internal ProgramExpander(TerminalProfile profile, ProgramLimits? limits = null)
    {
        Debug.Assert(profile is not null, "A profile always creates its own expander.");

        _profile = profile;
        _programs = profile.Programs;
        _interpreter = new Interpreter(limits ?? ProgramLimits.Default);
    }

    /// <summary>
    /// Tests whether this expander is still current for the given profile.
    /// </summary>
    /// <remarks>
    /// Capability refinement replaces the profile object while leaving the compiled description
    /// untouched, so an owner that caches an expander should rebuild it only when this returns
    /// <see langword="false"/> rather than on every profile reference change.
    /// </remarks>
    /// <param name="profile">The non-null profile to compare against.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="profile"/> produces the same terminal output as
    /// the profile this expander was created from.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is <see langword="null"/>.</exception>
    [Pure]
    public bool AppliesTo(TerminalProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return _profile.IsRenderingEquivalentTo(profile);
    }

    /// <summary>Tests whether the profile declares a usable program under the given name.</summary>
    /// <param name="name">The non-blank ordinal program name, such as <c>bel</c>.</param>
    /// <returns><see langword="true"/> when the program is present and non-empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or only white space.</exception>
    [Pure]
    public bool Has(string name) => _programs.Has(name);

    /// <summary>
    /// Tests whether the profile declares a matched lifecycle pair that both execute with no
    /// parameters, such as the <c>TS</c>/<c>fsl</c> title prefix and suffix.
    /// </summary>
    /// <param name="prefixName">The non-blank leading program name.</param>
    /// <param name="suffixName">The non-blank trailing program name.</param>
    /// <returns><see langword="true"/> only when both programs are present and executable.</returns>
    /// <exception cref="ArgumentNullException">Either name is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Either name is empty or only white space.</exception>
    [Pure]
    public bool HasPair(string prefixName, string suffixName) =>
        _programs.HasZeroParameterPair(prefixName, suffixName);

    /// <summary>Expands one named program and appends its exact bytes to the destination.</summary>
    /// <remarks>
    /// Nothing is appended when the program is absent, empty, or fails to execute, so a caller can
    /// treat a <see langword="false"/> result as "this terminal cannot do that" without inspecting
    /// or rewinding the destination.
    /// </remarks>
    /// <param name="name">The non-blank ordinal program name.</param>
    /// <param name="parameters">Numeric parameters in the description program's documented order.</param>
    /// <param name="destination">The non-null synchronous destination.</param>
    /// <returns><see langword="true"/> only when a present program appended non-empty output.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or only white space.</exception>
    /// <exception cref="InvalidOperationException">The call overlapped another expansion.</exception>
    public bool TryWrite(
        string name,
        ReadOnlySpan<int> parameters,
        IBufferWriter<byte> destination) =>
        _programs.TryWrite(name, parameters, _interpreter, destination);

    /// <summary>
    /// Expands a matched zero-parameter lifecycle pair atomically into two independently owned
    /// byte snapshots.
    /// </summary>
    /// <remarks>
    /// The pair is one transaction over the description's static variables: either both programs
    /// produce output and the profile commits, or neither does and nothing is retained. Both
    /// results stay valid after later expansions because each is an owned copy.
    /// </remarks>
    /// <param name="enableName">The non-blank acquisition program name.</param>
    /// <param name="disableName">The non-blank restoration program name.</param>
    /// <param name="enable">Receives the owned acquisition bytes, or empty on failure.</param>
    /// <param name="disable">Receives the owned restoration bytes, or empty on failure.</param>
    /// <returns><see langword="true"/> only when both owned non-empty outputs are ready.</returns>
    /// <exception cref="ArgumentNullException">Either name is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Either name is empty or only white space.</exception>
    public bool TryExpandPair(
        string enableName,
        string disableName,
        out ReadOnlyMemory<byte> enable,
        out ReadOnlyMemory<byte> disable) =>
        _programs.TryExpandPair(enableName, disableName, _interpreter, out enable, out disable);
}
