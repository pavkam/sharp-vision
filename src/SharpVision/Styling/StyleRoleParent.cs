// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Pairs one cascade parent role's resolved per-state set, viewed through
/// <see cref="ControlStyle"/>, with the code-owned default its authored Normal delta is measured
/// from, so a child role can diff and re-apply exactly what the theme changed about its parent.</summary>
internal sealed class StyleRoleParent
{
    /// <summary>Initializes one resolved cascade parent.</summary>
    /// <param name="codeOwnedDefault">The non-null code-owned default of the parent's own style type.</param>
    /// <param name="set">The non-null resolved per-state set of the parent role.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal StyleRoleParent(ControlStyle codeOwnedDefault, StyleStates<ControlStyle> set)
    {
        ArgumentNullException.ThrowIfNull(codeOwnedDefault);
        ArgumentNullException.ThrowIfNull(set);
        CodeOwnedDefault = codeOwnedDefault;
        Set = set;
    }

    /// <summary>Gets the code-owned default the parent's Normal delta is diffed against.</summary>
    internal ControlStyle CodeOwnedDefault { get; }

    /// <summary>Gets the parent role's resolved per-state set.</summary>
    internal StyleStates<ControlStyle> Set { get; }
}
