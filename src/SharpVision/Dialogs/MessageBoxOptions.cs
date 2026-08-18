// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using SharpVision.Controls.Input;

/// <summary>Carries optional layout, caption, and style configuration for the
/// <see cref="MessageBox.ShowAsync(ControlBase,string,MessageBoxOptions)"/> presentation helper, so
/// callers who need more than the title/buttons overloads offer do not force new overload growth.</summary>
[PublicAPI]
public sealed record MessageBoxOptions
{
    /// <summary>Gets the non-null window title.</summary>
    public string Title { get; init; } = "Message";

    /// <summary>Gets the standard button layout.</summary>
    public MessageBoxButtons Buttons { get; init; } = MessageBoxButtons.Ok;

    /// <summary>Gets the non-null OK action caption.</summary>
    public string OkText { get; init; } = "&OK";

    /// <summary>Gets the non-null Cancel action caption.</summary>
    public string CancelText { get; init; } = "&Cancel";

    /// <summary>Gets the non-null Yes action caption.</summary>
    public string YesText { get; init; } = "&Yes";

    /// <summary>Gets the non-null No action caption.</summary>
    public string NoText { get; init; } = "&No";

    /// <summary>Gets the complete local aggregate presentation, or null to let the active Theme own it.</summary>
    public MessageBoxStyle? Style { get; init; }

    /// <summary>Gets the complete local presentation applied to every generated action Button, or
    /// null to let each Button use its own semantic input profile.</summary>
    public ButtonStyle? ButtonStyle { get; init; }
}
