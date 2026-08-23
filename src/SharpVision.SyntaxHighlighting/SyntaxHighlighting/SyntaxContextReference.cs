// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Names one target context by its own name and, for a cross-definition reference such as
/// <c>Normal##JavaScript</c>, the other syntax definition it belongs to.
/// </summary>
[PublicAPI]
public readonly record struct SyntaxContextReference
{
    /// <summary>Initializes a context reference.</summary>
    /// <param name="contextName">
    /// The referenced context's name, or an empty string to mean "the referenced definition's own
    /// start context" (only meaningful together with a non-null <paramref name="definitionName"/>).
    /// </param>
    /// <param name="definitionName">
    /// The other syntax definition's <see cref="SyntaxDefinition.Name"/>, or null when the
    /// reference stays within the same definition.
    /// </param>
    internal SyntaxContextReference(string contextName, string? definitionName)
    {
        ContextName = contextName;
        DefinitionName = definitionName;
    }

    /// <summary>
    /// Gets the referenced context's name, or an empty string meaning the referenced definition's
    /// own start context.
    /// </summary>
    public string ContextName { get; }

    /// <summary>
    /// Gets the other syntax definition's name, or null when the reference stays within the same
    /// definition.
    /// </summary>
    public string? DefinitionName { get; }
}
