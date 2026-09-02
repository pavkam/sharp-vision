// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Demonstrates configurable Toast positions, entrance animations, and semantic styles.</summary>
internal sealed class ToastPane: CompositeControlBase
{
    private static readonly ToastAnimation[] _animations = Enum.GetValues<ToastAnimation>();
    private static readonly ToastPosition[] _positions = Enum.GetValues<ToastPosition>();
    private static readonly string[] _styleNames = ["Info", "Error", "Warning", "Success", "Trace"];

    /// <summary>Gets the exact catalog and page name.</summary>
    internal const string Title = "Toast";

    /// <summary>Initializes the retained Toast documentation page.</summary>
    internal ToastPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        var position = CreatePicker(Array.ConvertAll(_positions, static value => value.ToString()), 2);
        var animation = CreatePicker(Array.ConvertAll(_animations, static value => value.ToString()), 5);
        var style = CreatePicker(_styleNames, 0);
        var launch = new Button { Text = "Show &toast" };
        var selectedStatus = new Text("Ready: TopRight · Fade · Info") { Overflow = Overflow.Wrap };
        launch.Click += (_, _) =>
        {
            var selectedPosition = _positions[position.SelectedIndex];
            var selectedAnimation = _animations[animation.SelectedIndex];
            var selectedStyle = ResolveStyle(style.SelectedIndex);
            ShowToast(
                launch,
                selectedPosition,
                selectedAnimation,
                selectedStyle,
                _styleNames[style.SelectedIndex],
                selectedStatus);
        };

        var selector = new DocColumn(
            new DocRow(new Text("Position  "), position),
            new DocRow(new Text("Animation "), animation),
            new DocRow(new Text("Style     "), style),
            new DocRow(launch),
            selectedStatus)
        {
            Spacing = 1
        };

        var comparisonStatus = new Text("Trigger both presets to compare semantic accents.")
        {
            Overflow = Overflow.Wrap
        };
        var info = new Button { Text = "Show &info" };
        var error = new Button { Text = "Show &error" };
        info.Click += (_, _) => ShowToast(
            info,
            ToastPosition.TopRight,
            ToastAnimation.SlideLeft,
            ToastStyle.Info,
            "Info",
            comparisonStatus);
        error.Click += (_, _) => ShowToast(
            error,
            ToastPosition.BottomRight,
            ToastAnimation.SlideTop,
            ToastStyle.Error,
            "Error",
            comparisonStatus);

        return new DocPage(
            Title,
            "<info>Toast</info> presents arbitrary retained content as a non-modal notification, stacked at one of six screen-edge positions with a deterministic entrance animation.",
            new DocSection(
                "🎛️",
                "Position and animation",
                "Choose any edge slot and entrance. Fade uses the shared surface dissolve; Slide and Expand compose with it. The visible lifetime begins only after every entrance effect completes.",
                new DocExample(
                    "Interactive notification",
                    "Select a position, animation, and style, then activate Show toast. Repeated activations stack newest nearest the selected edge.",
                    new DocCard(selector),
                    "toast.Position = ToastPosition.TopRight;\ntoast.Animation = ToastAnimation.Fade;\ntoast.FadeOutDuration = TimeSpan.FromMilliseconds(160);\ntoast.Show(owner);")),
            new DocSection(
                "🚨",
                "Semantic styles",
                "Info and Error are complete ToastStyle presets. Warning, Success, and Trace use the same open style contract, so an application can replace any complete appearance without adding a severity enum.",
                new DocExample(
                    "Info versus error",
                    "Show both notifications to compare their title, adornment, close affordance, and border accents. Press Escape, Enter, Space, or the close glyph to dismiss a focused toast.",
                    new DocCard(new DocColumn(new DocRow(info, error), comparisonStatus)),
                    "toast.Style = ToastStyle.Error;")));
    }

    private static ComboBox CreatePicker(string[] items, int selectedIndex) => new()
    {
        Width = Length.Cells(20),
        Items = items,
        SelectedIndex = selectedIndex
    };

    private static ToastStyle ResolveStyle(int index) => index switch
    {
        0 => ToastStyle.Info,
        1 => ToastStyle.Error,
        2 => ToastStyle.Warning,
        3 => ToastStyle.Success,
        4 => ToastStyle.Trace,
        _ => throw new UnreachableException()
    };

    private static void ShowToast(
        ControlBase owner,
        ToastPosition position,
        ToastAnimation animation,
        ToastStyle style,
        string styleName,
        Text status)
    {
        var toast = new Toast
        {
            Title = $"{styleName} notification",
            Adornment = new Affix(styleName == "Error" ? "!" : "●", "*"),
            Position = position,
            Animation = animation,
            FadeInDuration = TimeSpan.FromMilliseconds(120),
            FadeOutDuration = TimeSpan.FromMilliseconds(160),
            DisplayDuration = TimeSpan.FromSeconds(8),
            Style = style,
            Content = new Stack
            {
                Spacing = 0,
                Face = style.Face with { Background = Color.Transparent },
                Children =
                {
                    new Text("Arbitrary component content"),
                    new Text($"{position} · {animation}")
                }
            }
        };
        toast.Closed += (_, _) => owner.Dispatcher?.Post(toast.Dispose);
        toast.Show(owner);
        status.Content = $"Shown: {position} · {animation} · {styleName}";
    }
}
