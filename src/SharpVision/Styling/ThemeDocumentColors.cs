// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines concrete values for every fixed semantic color.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ThemeDocumentColors
{
    /// <summary>Gets or sets the application-window surface.</summary>
    [JsonPropertyName("window")]
    public string? Window { get; set; }

    /// <summary>Gets or sets the raised floating-Window surface.</summary>
    [JsonPropertyName("windowSurface")]
    public string? WindowSurface { get; set; }

    /// <summary>Gets or sets application-window text.</summary>
    [JsonPropertyName("windowText")]
    public string? WindowText { get; set; }

    /// <summary>Gets or sets raised surface color.</summary>
    [JsonPropertyName("surface")]
    public string? Surface { get; set; }

    /// <summary>Gets or sets raised surface text.</summary>
    [JsonPropertyName("surfaceText")]
    public string? SurfaceText { get; set; }

    /// <summary>Gets or sets the normal control face.</summary>
    [JsonPropertyName("control")]
    public string? Control { get; set; }

    /// <summary>Gets or sets normal control text.</summary>
    [JsonPropertyName("controlText")]
    public string? ControlText { get; set; }

    /// <summary>Gets or sets the normal control border.</summary>
    [JsonPropertyName("controlBorder")]
    public string? ControlBorder { get; set; }

    /// <summary>Gets or sets the normal control shadow.</summary>
    [JsonPropertyName("controlShadow")]
    public string? ControlShadow { get; set; }

    /// <summary>Gets or sets the active control face.</summary>
    [JsonPropertyName("activeControl")]
    public string? ActiveControl { get; set; }

    /// <summary>Gets or sets active control text.</summary>
    [JsonPropertyName("activeText")]
    public string? ActiveText { get; set; }

    /// <summary>Gets or sets the active control border.</summary>
    [JsonPropertyName("activeBorder")]
    public string? ActiveBorder { get; set; }

    /// <summary>Gets or sets the focused control face.</summary>
    [JsonPropertyName("focusedControl")]
    public string? FocusedControl { get; set; }

    /// <summary>Gets or sets focused control text.</summary>
    [JsonPropertyName("focusedText")]
    public string? FocusedText { get; set; }

    /// <summary>Gets or sets the focused control border.</summary>
    [JsonPropertyName("focusedBorder")]
    public string? FocusedBorder { get; set; }

    /// <summary>Gets or sets the pressed control face.</summary>
    [JsonPropertyName("pressedControl")]
    public string? PressedControl { get; set; }

    /// <summary>Gets or sets pressed control text.</summary>
    [JsonPropertyName("pressedText")]
    public string? PressedText { get; set; }

    /// <summary>Gets or sets the pressed control border.</summary>
    [JsonPropertyName("pressedBorder")]
    public string? PressedBorder { get; set; }

    /// <summary>Gets or sets the selected control face.</summary>
    [JsonPropertyName("selectedControl")]
    public string? SelectedControl { get; set; }

    /// <summary>Gets or sets selected control text.</summary>
    [JsonPropertyName("selectedText")]
    public string? SelectedText { get; set; }

    /// <summary>Gets or sets the disabled control face.</summary>
    [JsonPropertyName("disabledControl")]
    public string? DisabledControl { get; set; }

    /// <summary>Gets or sets disabled control text.</summary>
    [JsonPropertyName("disabledText")]
    public string? DisabledText { get; set; }

    /// <summary>Gets or sets the disabled control border.</summary>
    [JsonPropertyName("disabledBorder")]
    public string? DisabledBorder { get; set; }

    /// <summary>Gets or sets the primary accent.</summary>
    [JsonPropertyName("accent")]
    public string? Accent { get; set; }

    /// <summary>Gets or sets muted content color.</summary>
    [JsonPropertyName("muted")]
    public string? Muted { get; set; }

    /// <summary>Gets or sets access-key color.</summary>
    [JsonPropertyName("hotkey")]
    public string? Hotkey { get; set; }

    /// <summary>Gets or sets error color.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>Gets or sets warning color.</summary>
    [JsonPropertyName("warning")]
    public string? Warning { get; set; }

    /// <summary>Gets or sets success color.</summary>
    [JsonPropertyName("success")]
    public string? Success { get; set; }

    /// <summary>Gets or sets informational color.</summary>
    [JsonPropertyName("info")]
    public string? Info { get; set; }

    /// <summary>Gets one authored value by semantic name.</summary>
    [Pure]
    internal string? Get(SemanticColor color) => color switch
    {
        SemanticColor.Window => Window,
        SemanticColor.WindowSurface => WindowSurface,
        SemanticColor.WindowText => WindowText,
        SemanticColor.Surface => Surface,
        SemanticColor.SurfaceText => SurfaceText,
        SemanticColor.Control => Control,
        SemanticColor.ControlText => ControlText,
        SemanticColor.ControlBorder => ControlBorder,
        SemanticColor.ControlShadow => ControlShadow,
        SemanticColor.ActiveControl => ActiveControl,
        SemanticColor.ActiveText => ActiveText,
        SemanticColor.ActiveBorder => ActiveBorder,
        SemanticColor.FocusedControl => FocusedControl,
        SemanticColor.FocusedText => FocusedText,
        SemanticColor.FocusedBorder => FocusedBorder,
        SemanticColor.PressedControl => PressedControl,
        SemanticColor.PressedText => PressedText,
        SemanticColor.PressedBorder => PressedBorder,
        SemanticColor.SelectedControl => SelectedControl,
        SemanticColor.SelectedText => SelectedText,
        SemanticColor.DisabledControl => DisabledControl,
        SemanticColor.DisabledText => DisabledText,
        SemanticColor.DisabledBorder => DisabledBorder,
        SemanticColor.Accent => Accent,
        SemanticColor.Muted => Muted,
        SemanticColor.Hotkey => Hotkey,
        SemanticColor.Error => Error,
        SemanticColor.Warning => Warning,
        SemanticColor.Success => Success,
        SemanticColor.Info => Info,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, "The theme color is unknown.")
    };
}
