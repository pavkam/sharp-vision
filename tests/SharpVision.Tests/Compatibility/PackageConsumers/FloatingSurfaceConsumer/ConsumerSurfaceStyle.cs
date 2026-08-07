// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.FloatingSurfaceConsumer;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the external consumer's complete validated surface chrome. Derives from
/// <see cref="ControlStyle"/> purely to satisfy <c>StyleDefinitions.Part&lt;TStyle&gt;</c>'s
/// generic constraint - this fixture proves a style type declared OUTSIDE the SharpVision
/// assembly still compiles and resolves using only public members.</summary>
public sealed record ConsumerSurfaceStyle: ControlStyle
{
    /// <summary>Gets the aggregate policy shared by external typed controls.</summary>
    internal static StyleDefinition<ConsumerSurfaceStyle> Definition { get; } = StyleDefinitions.Part(
        static _ => Default,
        static (previous, _, current, _) => previous == current
            ? InvalidationImpact.None
            : InvalidationImpact.Measure);

    /// <summary>Initializes one complete external surface presentation.</summary>
    /// <param name="face">The complete surface face.</param>
    /// <param name="border">The complete surface border.</param>
    /// <param name="shadow">The complete surface shadow.</param>
    /// <exception cref="ArgumentException">A visible shadow is combined with reserved border sides.</exception>
    [SetsRequiredMembers]
    public ConsumerSurfaceStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
    {
        if (shadow.Visible && border.Sides != BorderSide.None)
        {
            throw new ArgumentException(
                "A consumer surface cannot reserve border sides while its shadow is visible.",
                nameof(shadow));
        }
    }

    /// <summary>Gets the valid borderless and shadowless fallback.</summary>
    public static new ConsumerSurfaceStyle Default { get; } = new(
        new Face(Color.Default, Color.Transparent, TerminalAttributes.None, Underline.None, Color.Default),
        new Border(BorderSide.None, BorderGlyphStyle.Default, Color.Default, Color.Transparent, TerminalAttributes.None),
        new Shadow(false, ShadowMode.Composite, default, default, Color.Default, Color.Transparent, TerminalAttributes.None));
}
