// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;
using SharpVision.Terminal.Input;

using Text;

using DisplayText = Controls.Display.Text;
using LayoutStack = Controls.Layout.Stack;

/// <summary>Provides a measured, centered modal message surface with standard action buttons.</summary>
/// <remarks>
/// The static <c>ShowAsync</c> helpers attach a temporary instance to the owning Screen's private
/// presentation plane, or to an explicit or fallback container outside a hosted Screen. They enter
/// the shared Window modality plane and remove the instance after a result is selected. Message text
/// wraps against the application host's available width; no fixed message-box width is assumed.
/// </remarks>
[PublicAPI]
public sealed class MessageBox: Dialog<MessageBoxResult>
{
    private const int _minimumWidth = 32;
    private const int _minimumHeight = 8;

    private readonly Button[] _buttons;

    #region Construction and contract

    /// <summary>Initializes a message box with the requested title, message, and button layout.</summary>
    /// <param name="message">The non-null message text.</param>
    /// <param name="title">The non-null window title.</param>
    /// <param name="buttons">The defined standard button layout.</param>
    /// <exception cref="ArgumentNullException">A message or title is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is undefined.</exception>
    public MessageBox(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.Ok)
        : base(MessageBoxResult.Cancel)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);

        if (!Enum.IsDefined(buttons))
        {
            throw new ArgumentOutOfRangeException(nameof(buttons), buttons, "The message-box buttons are unknown.");
        }

        Message = message;
        Title = title;
        Buttons = buttons;
        _buttons = CreateButtons(buttons);
        Header = title;
        CanMove = true;
        CanClose = false;
        MinWidth = _minimumWidth;
        MinHeight = _minimumHeight;
        MaxWidth = 60;
        MaxHeight = 20;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Content = CreateContent(message, _buttons);
        _ = AddHandler(Events.Key, OnMessageBoxKey);
    }

    /// <summary>Gets the non-null message rendered by this box.</summary>
    public string Message { get; }

    /// <summary>Gets the non-null title rendered in the window frame.</summary>
    public string Title { get; }

    /// <summary>Gets the standard button layout rendered by this box.</summary>
    public MessageBoxButtons Buttons { get; }

    /// <summary>Gets or sets the complete local presentation applied to every generated action
    /// Button, or null to let each Button use its own semantic input profile.</summary>
    /// <exception cref="InvalidOperationException">The attached MessageBox is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The MessageBox is disposed.</exception>
    public ButtonStyle? ButtonStyle
    {
        get;
        set
        {
            if (!SetProperty(ref field, value, InvalidationImpact.Measure))
            {
                return;
            }

            foreach (var button in _buttons)
            {
                button.Style = value;
            }
        }
    }

    /// <summary>Gets the resolved Button style applied to every generated action.</summary>
    public ButtonStyle ActualButtonStyle => _buttons[0].ActualStyle;

    #endregion

    #region Static presentation helpers

    /// <summary>Shows an OK message box owned by the supplied attached control.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <returns>A task completing with <see cref="MessageBoxResult.Ok"/> or dismissal.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(Control owner, string message) =>
        ShowAsync(owner, message, "Message", MessageBoxButtons.Ok);

    /// <summary>Shows an OK message box with a custom title.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <param name="title">The non-null window title.</param>
    /// <returns>A task completing with the selected result.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(Control owner, string message, string title) =>
        ShowAsync(owner, message, title, MessageBoxButtons.Ok);

    /// <summary>Shows a message box with a custom button layout and the default title.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <param name="buttons">The defined standard button layout.</param>
    /// <returns>A task completing with the selected result.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(Control owner, string message, MessageBoxButtons buttons) =>
        ShowAsync(owner, message, "Message", buttons);

    /// <summary>Shows a titled message box with the requested standard button layout.</summary>
    /// <param name="owner">The attached control whose ancestry identifies the application host.</param>
    /// <param name="message">The non-null message text.</param>
    /// <param name="title">The non-null window title.</param>
    /// <param name="buttons">The defined standard button layout.</param>
    /// <param name="buttonStyle">The complete local Button presentation, or null to use each
    /// generated Button's own semantic input profile.</param>
    /// <returns>A task completing when a button or dismissal selects a result.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is undefined.</exception>
    /// <exception cref="ArgumentException">The owner has no attached presentation host.</exception>
    /// <exception cref="InvalidOperationException">The call is made off the owner's dispatcher.</exception>
    public static Task<MessageBoxResult> ShowAsync(
        Control owner,
        string message,
        string title,
        MessageBoxButtons buttons,
        ButtonStyle? buttonStyle = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);
        owner.Dispatcher?.VerifyAccess();

        var host = PresentationHost.Resolve(owner) ??
            throw new ArgumentException("The owner must be attached beneath a presentation host.", nameof(owner));
        var messageBox = new MessageBox(message, title, buttons) { ButtonStyle = buttonStyle };
        host.Add(messageBox);

        try
        {
            return messageBox.PresentAsync(host, messageBox._buttons[0], CancellationToken.None);
        }
        catch
        {
            _ = host.Remove(messageBox);
            messageBox.Dispose();
            throw;
        }
    }

    #endregion

    private static Grid CreateContent(string message, Button[] buttons)
    {
        var text = new DisplayText(message)
        {
            Overflow = Overflow.Wrap,
            TextAlignment = Alignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(1, 2, 1, 0)
        };
        var actionStack = new LayoutStack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        foreach (var button in buttons)
        {
            actionStack.Children.Add(button);
        }
        var actions = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        actions.Columns.Add(Track.Star(1));
        actions.Columns.Add(Track.Auto());
        actions.Columns.Add(Track.Star(1));
        Grid.SetColumn(actionStack, 1);
        actions.Children.Add(actionStack);
        var content = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowSpacing = 3
        };
        content.Rows.Add(Track.Auto(minimum: 3));
        content.Rows.Add(Track.Auto(minimum: 3));
        Grid.SetRow(text, 0);
        Grid.SetRow(actions, 1);
        content.Children.Add(text);
        content.Children.Add(actions);
        return content;
    }

    private Button[] CreateButtons(MessageBoxButtons buttons)
    {
        Button[] result = buttons switch
        {
            MessageBoxButtons.Ok => [CreateButton("&OK", MessageBoxResult.Ok, isDefault: true)],
            MessageBoxButtons.OkCancel => [
                CreateButton("&OK", MessageBoxResult.Ok, isDefault: true),
                CreateButton("&Cancel", MessageBoxResult.Cancel, isCancel: true)],
            MessageBoxButtons.YesNo => [
                CreateButton("&Yes", MessageBoxResult.Yes, isDefault: true),
                CreateButton("&No", MessageBoxResult.No)],
            MessageBoxButtons.YesNoCancel => [
                CreateButton("&Yes", MessageBoxResult.Yes, isDefault: true),
                CreateButton("&No", MessageBoxResult.No),
                CreateButton("&Cancel", MessageBoxResult.Cancel, isCancel: true)],
            _ => throw new ArgumentOutOfRangeException(nameof(buttons), buttons, "The message-box buttons are unknown.")
        };
        var width = result.Max(button => button.Content is DisplayText text
            ? Input.AccessKeyText.Measure(text.Content, CellPolicy.AmbiguousWidth, button.UseMnemonic) + 4
            : 4);
        foreach (var button in result)
        {
            button.Width = Length.Cells(width);
            button.Margin = new Thickness(0, 0, 1, 1);
        }
        return result;
    }

    private Button CreateButton(string label, MessageBoxResult result, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button
        {
            Content = new DisplayText(label),
            IsDefault = isDefault,
            IsCancel = isCancel
        };
        button.Click += (_, _) => Complete(result);
        return button;
    }

    private void OnMessageBoxKey(object? sender, KeyEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.Phase == RoutingPhase.Bubble &&
            eventArgs.Stroke.Action == KeyAction.Press &&
            eventArgs.Stroke.Code == Code.Escape)
        {
            eventArgs.Handled = Cancel();
        }
    }

}
