// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using DisplayText = Display.Text;

/// <summary>Defines a focusable clickable text control styled as a classic hyperlink.</summary>
[PublicAPI]
public sealed class HyperlinkButton: PressableBase
{
    private static readonly AppearanceProfileSet _linkAppearance = new(
        normal: new AppearanceSet(
            face: new FaceSet(
                foreground: ThemeColor.Accent,
                attributes: ThemeDecoration.NormalText,
                underline: Underline.Straight,
                underlineColor: ThemeColor.Accent)));

    /// <summary>Initializes an empty focusable HyperlinkButton with accent-colored underlined text.</summary>
    public HyperlinkButton()
    {
    }

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Control;

    /// <inheritdoc/>
    protected override ThemeProfile AppearanceProfile =>
        StyleResolution.Apply(base.AppearanceProfile, _linkAppearance);

    /// <inheritdoc/>
    protected override ThemeProfile GetAppearanceProfile(Theme? theme) =>
        StyleResolution.Apply(base.GetAppearanceProfile(theme), _linkAppearance);

    /// <summary>Initializes a focusable HyperlinkButton with the specified text content.</summary>
    /// <param name="text">The non-null text content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public HyperlinkButton(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Content = new DisplayText(text);
    }

    /// <summary>Gets or sets the displayed link text.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public string? Text
    {
        get => Content is DisplayText text ? text.Content : null;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            if (Content is DisplayText existing)
            {
                existing.Content = value;
            }
            else
            {
                Content = new DisplayText(value);
            }
        }
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
