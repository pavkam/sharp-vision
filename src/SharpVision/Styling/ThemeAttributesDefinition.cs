// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines concrete values for every fixed semantic decoration role.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ThemeAttributesDefinition
{
    /// <summary>Gets or sets ordinary text attributes.</summary>
    [JsonPropertyName("normalText")]
    public JsonElement? NormalText { get; set; }

    /// <summary>Gets or sets active text attributes.</summary>
    [JsonPropertyName("activeText")]
    public JsonElement? ActiveText { get; set; }

    /// <summary>Gets or sets focused text attributes.</summary>
    [JsonPropertyName("focusedText")]
    public JsonElement? FocusedText { get; set; }

    /// <summary>Gets or sets pressed text attributes.</summary>
    [JsonPropertyName("pressedText")]
    public JsonElement? PressedText { get; set; }

    /// <summary>Gets or sets selected text attributes.</summary>
    [JsonPropertyName("selectedText")]
    public JsonElement? SelectedText { get; set; }

    /// <summary>Gets or sets disabled text attributes.</summary>
    [JsonPropertyName("disabledText")]
    public JsonElement? DisabledText { get; set; }

    /// <summary>Gets or sets border attributes.</summary>
    [JsonPropertyName("border")]
    public JsonElement? Border { get; set; }

    /// <summary>Gets or sets shadow attributes.</summary>
    [JsonPropertyName("shadow")]
    public JsonElement? Shadow { get; set; }

    /// <summary>Gets or sets access-key attributes.</summary>
    [JsonPropertyName("hotkey")]
    public JsonElement? Hotkey { get; set; }

    /// <summary>Gets one authored value by semantic role.</summary>
    internal JsonElement? Get(ThemeDecoration role) => role switch
    {
        ThemeDecoration.NormalText => NormalText,
        ThemeDecoration.ActiveText => ActiveText,
        ThemeDecoration.FocusedText => FocusedText,
        ThemeDecoration.PressedText => PressedText,
        ThemeDecoration.SelectedText => SelectedText,
        ThemeDecoration.DisabledText => DisabledText,
        ThemeDecoration.Border => Border,
        ThemeDecoration.Shadow => Shadow,
        ThemeDecoration.Hotkey => Hotkey,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The theme decoration role is unknown.")
    };
}
