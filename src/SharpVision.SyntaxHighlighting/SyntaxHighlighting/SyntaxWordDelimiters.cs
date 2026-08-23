// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>
/// Represents the set of characters KDE syntax rules treat as word boundaries when matching a
/// keyword list, an identifier-adjacent number, or a word-detect rule.
/// </summary>
/// <remarks>
/// <see cref="Default"/> is the exact built-in set from upstream KSyntaxHighlighting's
/// <c>WordDelimiters::WordDelimiters()</c>: the tab and space characters plus
/// <c>!%&amp;()*+,-./:;&lt;=&gt;?[\]^{|}~</c>. A <see cref="SyntaxGeneralOptions"/> and, layered on
/// top of that, an individual <see cref="SyntaxRule"/> or <see cref="SyntaxKeywordList"/> may each
/// add characters via <c>additionalDeliminator</c> and remove characters via
/// <c>weakDeliminator</c>, applied in that definition-then-rule order.
/// </remarks>
[PublicAPI]
public readonly struct SyntaxWordDelimiters: IEquatable<SyntaxWordDelimiters>
{
    private const string _builtIn = "\t !%&()*+,-./:;<=>?[\\]^{|}~";

    private readonly bool[] _ascii;
    private readonly string _nonAscii;

    private SyntaxWordDelimiters(bool[] ascii, string nonAscii)
    {
        _ascii = ascii;
        _nonAscii = nonAscii;
    }

    /// <summary>Gets the built-in default delimiter set with no definition or rule overrides applied.</summary>
    public static SyntaxWordDelimiters Default { get; } = FromBaseline(_builtIn);

    /// <summary>Gets whether <paramref name="value"/> is a word delimiter.</summary>
    /// <param name="value">The character to test.</param>
    /// <returns>True when <paramref name="value"/> is a delimiter.</returns>
    [Pure]
    public bool Contains(char value) => value < 128 ? _ascii[value] : _nonAscii.Contains(value);

    /// <summary>Builds a new set with additional characters appended and weak characters removed.</summary>
    /// <param name="additional">Non-null characters to add.</param>
    /// <param name="weak">Non-null characters to remove.</param>
    /// <returns>The derived set; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="additional"/> or <paramref name="weak"/> is null.</exception>
    [Pure]
    [MustUseReturnValue]
    public SyntaxWordDelimiters With(string additional, string weak)
    {
        ArgumentNullException.ThrowIfNull(additional);
        ArgumentNullException.ThrowIfNull(weak);

        if (additional.Length == 0 && weak.Length == 0)
        {
            return this;
        }

        var ascii = (bool[]) _ascii.Clone();
        var nonAscii = new StringBuilder(_nonAscii);

        foreach (var c in additional)
        {
            if (c < 128)
            {
                ascii[c] = true;
            }
            else if (!ContainsNonAscii(nonAscii, c))
            {
                _ = nonAscii.Append(c);
            }
        }

        foreach (var c in weak)
        {
            if (c < 128)
            {
                ascii[c] = false;
            }
            else
            {
                _ = nonAscii.Replace(c.ToString(), string.Empty);
            }
        }

        return new SyntaxWordDelimiters(ascii, nonAscii.ToString());
    }

    /// <inheritdoc/>
    public bool Equals(SyntaxWordDelimiters other) =>
        _ascii.AsSpan().SequenceEqual(other._ascii) && _nonAscii == other._nonAscii;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SyntaxWordDelimiters other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var flag in _ascii)
        {
            hash.Add(flag);
        }

        hash.Add(_nonAscii);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two delimiter sets are equal.</summary>
    /// <param name="left">The first set.</param>
    /// <param name="right">The second set.</param>
    /// <returns>True when the sets contain the same characters.</returns>
    public static bool operator ==(SyntaxWordDelimiters left, SyntaxWordDelimiters right) => left.Equals(right);

    /// <summary>Determines whether two delimiter sets are not equal.</summary>
    /// <param name="left">The first set.</param>
    /// <param name="right">The second set.</param>
    /// <returns>True when the sets contain different characters.</returns>
    public static bool operator !=(SyntaxWordDelimiters left, SyntaxWordDelimiters right) => !left.Equals(right);

    private static bool ContainsNonAscii(StringBuilder builder, char value)
    {
        for (var i = 0; i < builder.Length; i++)
        {
            if (builder[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxWordDelimiters FromBaseline(string characters)
    {
        var ascii = new bool[128];

        foreach (var c in characters)
        {
            ascii[c] = true;
        }

        return new SyntaxWordDelimiters(ascii, string.Empty);
    }
}
