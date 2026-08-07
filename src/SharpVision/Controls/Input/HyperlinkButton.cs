// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines a focusable clickable text control styled as a classic hyperlink.</summary>
[PublicAPI]
public sealed class HyperlinkButton: PressableBase
{
    private static readonly AppearanceStatesOverlay _linkAppearance = new(
        normal: new AppearanceOverlay(
            face: new FaceOverlay(
                foreground: SemanticColor.Accent,
                attributes: SemanticDecoration.NormalText,
                underline: Underline.Straight,
                underlineColor: SemanticColor.Accent)));

    /// <summary>Initializes an empty focusable HyperlinkButton with accent-colored underlined text.</summary>
    public HyperlinkButton()
    {
    }

    /// <inheritdoc/>
    protected override AppearanceStates AppearanceStates =>
        base.AppearanceStates.Compose(_linkAppearance);

    /// <inheritdoc/>
    protected override AppearanceStates GetAppearanceStates(Theme? theme) =>
        base.GetAppearanceStates(theme).Compose(_linkAppearance);

    /// <summary>Initializes a focusable HyperlinkButton with the specified text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public HyperlinkButton(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Raised after released state commits and before command execution.</summary>
    public event EventHandler<ActivationEventArgs>? Click;

    /// <summary>Activates an available executable HyperlinkButton through its public API.</summary>
    /// <exception cref="InvalidOperationException">The attached control is accessed off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public void PerformClick()
    {
        VerifyMutable();

        if (EffectiveIsEnabled && EffectiveIsVisible)
        {
            Activate(ActivationCause.Programmatic);
        }
    }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        var command = Command;
        var parameter = CommandParameter;

        if (command is not null && !command.CanExecute(parameter))
        {
            return;
        }

        var eventArgs = new ActivationEventArgs(cause);
        Click?.Invoke(this, eventArgs);
        command?.Execute(parameter);
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);

        if (reason == ReleaseReason.Disposed)
        {
            Click = null;
        }
    }
}
