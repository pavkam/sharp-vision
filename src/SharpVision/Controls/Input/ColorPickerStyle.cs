// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Describes the presentation ColorPicker applies to its owned parts, without exposing
/// them directly.</summary>
/// <param name="SliderStyle">The style applied uniformly to the hue and RGB Sliders, or null to
/// let each use its own semantic input profile.</param>
/// <param name="StatusFace">The background, attributes, and underline applied to the hex-readout
/// status text, or null to use the library default. The Foreground component is always ignored:
/// ColorPicker recomputes it from the current value via ColorMath.Contrast on every commit, so
/// the readout stays legible regardless of the configured background.</param>
[PublicAPI]
public readonly record struct ColorPickerStyle(SliderStyle? SliderStyle, Face? StatusFace);
