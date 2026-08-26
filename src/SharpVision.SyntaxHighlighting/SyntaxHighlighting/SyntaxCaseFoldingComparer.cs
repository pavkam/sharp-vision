// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Provides string and alternate span equality under Qt-compatible Unicode case folding.</summary>
internal sealed class SyntaxCaseFoldingComparer:
    IEqualityComparer<string>,
    IAlternateEqualityComparer<ReadOnlySpan<char>, string>
{
    /// <summary>Gets the shared stateless comparer.</summary>
    internal static SyntaxCaseFoldingComparer Instance { get; } = new();

    private SyntaxCaseFoldingComparer()
    {
    }

    /// <inheritdoc/>
    public bool Equals(string? left, string? right) =>
        ReferenceEquals(left, right) ||
        (left is not null && right is not null && SyntaxCaseFolding.Equals(left, right));

    /// <inheritdoc/>
    public int GetHashCode(string value) => SyntaxCaseFolding.GetHashCode(value);

    /// <inheritdoc/>
    public bool Equals(ReadOnlySpan<char> alternate, string other) => SyntaxCaseFolding.Equals(alternate, other);

    /// <inheritdoc/>
    public int GetHashCode(ReadOnlySpan<char> alternate) => SyntaxCaseFolding.GetHashCode(alternate);

    /// <inheritdoc/>
    public string Create(ReadOnlySpan<char> alternate) => alternate.ToString();
}
