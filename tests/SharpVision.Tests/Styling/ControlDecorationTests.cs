// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies literal and semantic ControlDecoration authoring.</summary>
public sealed class ControlDecorationTests
{
    /// <summary>Verifies a known theme attribute remains a semantic reference.</summary>
    [Fact]
    public void ControlDecoration_WhenAssignedSemanticDecoration_PreservesSemanticReference()
    {
        ControlDecoration value = SemanticDecoration.FocusedText;

        value.Semantic.ShouldBeTrue();
        value.SemanticDecoration.ShouldBe(SemanticDecoration.FocusedText);
    }
}
