// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exposes protected style-slot initialization to prove definition-role validation.</summary>
public sealed class StyleDefinitionRoleProbe: ControlBase
{
    /// <summary>Initializes a candidate definition as the primary Style slot.</summary>
    /// <param name="definition">The non-null candidate definition.</param>
    public void InitializePrimary(StyleDefinition<ButtonStyle> definition) =>
        _ = InitializeStyle(definition);

    /// <summary>Initializes a candidate definition as a named part-style slot.</summary>
    /// <param name="definition">The non-null candidate definition.</param>
    public void InitializePart(StyleDefinition<ButtonStyle> definition) =>
        _ = InitializePartStyle(definition, nameof(ButtonStyle));
}
