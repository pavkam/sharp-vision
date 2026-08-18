// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Layout;

using Terminal.Capabilities;

using Text;

using DisplayText = Display.Text;
using LayoutStack = Layout.Stack;
/// <summary>Selects one terminal color through capability-adaptive retained controls.</summary>
[PublicAPI]
public sealed class ColorPicker: CompositeControlBase, IStyled<ColorPickerStyle>
{
    private readonly LayoutStack _monochromeRoot;
    private readonly ControlBase _rgbRoot;
    private readonly DisplayText _status;
    private bool _synchronizing;
    private Color _value = Color.Rgb(255, 0, 0);
    private readonly StyleSlot<ColorPickerStyle> _style;

    #region Construction and public state

    /// <summary>Initializes a retained adaptive picker with an RGB-red detached value.</summary>
    public ColorPicker()
    {
        _style = InitializeStyle(ColorPickerStyle.Definition, OnStyleChanged);
        TabNavigation = TabNavigation.Continue;
        Plane = new ColorPlane();
        HueSlider = CreateSlider(359);
        RedSlider = CreateSlider(byte.MaxValue);
        GreenSlider = CreateSlider(byte.MaxValue);
        BlueSlider = CreateSlider(byte.MaxValue);
        Preview = new ColorSwatch { Width = Length.Cells(10) };
        _status = new DisplayText { Face = ColorPickerStyle.DefaultStatusFace };
        _rgbRoot = CreateRgbRoot();
        _monochromeRoot = CreateMonochromeRoot();
        var root = new Overlay { Children = { _rgbRoot, _monochromeRoot } };
        InitializeContent(root);
        Subscribe();
        SynchronizeParts();
        ApplyDepth(EffectiveColorDepth);
    }

    /// <summary>Raised after a changed color commits.</summary>
    public event EventHandler<ColorChangedEventArgs>? ValueChanged;

    /// <summary>Gets or sets the selected terminal color.</summary>
    /// <remarks>
    /// The picker preserves RGB authoring independently of the active terminal output depth.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is transparent.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Color Value
    {
        get => _value;
        set => _ = Commit(value);
    }

    /// <summary>Gets the active terminal color depth inherited from the application.</summary>
    public ColorDepth EffectiveColorDepth => Capabilities.ColorDepth;

    /// <summary>Gets or sets the complete local style, or null to use the definition fallback.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ColorPickerStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the resolved presentation currently applied to the owned Sliders, status
    /// readout, and plane selection marker.</summary>
    public ColorPickerStyle ActualStyle => new(
        ControlStyle.DefaultFace,
        ControlStyle.NoBorder,
        ControlStyle.NoShadow,
        _style.Actual.SliderStyle,
        _status.Face,
        Plane.SelectedMarker);

    /// <summary>Gets the uppercase RGB readout, or DEFAULT for the terminal default color.</summary>
    internal string HexText => _status.Content;

    /// <summary>Gets the retained true-color saturation/value surface for interaction tests.</summary>
    internal ColorPlane Plane { get; }

    /// <summary>Gets the retained preview surface.</summary>
    internal ColorSwatch Preview { get; }

    /// <summary>Gets the retained hue slider for interaction tests.</summary>
    internal Slider HueSlider { get; }

    /// <summary>Gets the retained red-component slider.</summary>
    internal Slider RedSlider { get; }

    /// <summary>Gets the retained green-component slider.</summary>
    internal Slider GreenSlider { get; }

    /// <summary>Gets the retained blue-component slider.</summary>
    internal Slider BlueSlider { get; }

    /// <summary>Gets whether the RGB editor participates in layout.</summary>
    internal bool IsRgbEditorVisible => _rgbRoot.Visibility == Visibility.Visible;

    #endregion

    #region Lifecycle and capability adaptation

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        ApplyDepth(EffectiveColorDepth);
    }

    /// <inheritdoc/>
    protected override void OnCapabilitiesChanged(
        TerminalCapabilities previous,
        TerminalCapabilities current)
    {
        base.OnCapabilitiesChanged(previous, current);
        ApplyDepth(current.ColorDepth);
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        Plane.Changed -= OnPlaneChanged;
        HueSlider.ValueChanged -= OnHueChanged;
        RedSlider.ValueChanged -= OnRgbChanged;
        GreenSlider.ValueChanged -= OnRgbChanged;
        BlueSlider.ValueChanged -= OnRgbChanged;
        ValueChanged = null;
        base.OnDisposing();
    }

    private void ApplyDepth(ColorDepth depth)
    {
        Debug.Assert(Enum.IsDefined(depth), "Capability profiles validate color depth.");
        _rgbRoot.Visibility = depth == ColorDepth.Monochrome
            ? Visibility.Collapsed
            : Visibility.Visible;
        _monochromeRoot.Visibility = depth == ColorDepth.Monochrome
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    #endregion

    #region Composition

    private Grid CreateRgbRoot()
    {
        var root = new Grid
        {
            RowSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Rows.Add(Track.Star(1, minimum: 4));
        root.Rows.Add(Track.Cells(1));
        root.Rows.Add(Track.Auto());
        root.Rows.Add(Track.Cells(1));

        Grid.SetRow(Plane, 0);
        root.Children.Add(Plane);

        var hueOverlay = new Overlay { Height = Length.Cells(1), Children = { new ColorRamp(), HueSlider } };
        Grid.SetRow(hueOverlay, 1);
        root.Children.Add(hueOverlay);

        var components = new LayoutStack
        {
            Spacing = 0,
            Children =
            {
                CreateSliderRow("R", RedSlider),
                CreateSliderRow("G", GreenSlider),
                CreateSliderRow("B", BlueSlider)
            }
        };
        Grid.SetRow(components, 2);
        root.Children.Add(components);

        _status.Width = Length.Cells(10);
        _status.HorizontalAlignment = HorizontalAlignment.Stretch;
        _status.TextAlignment = Alignment.Center;
        _status.VerticalAlignment = VerticalAlignment.Center;
        var status = new Overlay
        {
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            Children = { Preview, _status }
        };
        Grid.SetRow(status, 3);
        root.Children.Add(status);
        return root;
    }

    private static LayoutStack CreateMonochromeRoot()
    {
        var swatch = new ColorSwatch { Value = Color.Default };
        return new LayoutStack
        {
            IsEnabled = false,
            Spacing = 1,
            Children = { swatch, new DisplayText("Monochrome terminal · default color only") }
        };
    }

    private static Dock CreateSliderRow(string label, Slider slider)
    {
        var text = new DisplayText(label) { Width = Length.Cells(2) };
        Dock.SetSide(text, DockSide.Left);
        return new Dock { Height = Length.Cells(1), Children = { text, slider } };
    }

    private static Slider CreateSlider(int maximum) => new()
    {
        Maximum = maximum,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private void Subscribe()
    {
        Plane.Changed += OnPlaneChanged;
        HueSlider.ValueChanged += OnHueChanged;
        RedSlider.ValueChanged += OnRgbChanged;
        GreenSlider.ValueChanged += OnRgbChanged;
        BlueSlider.ValueChanged += OnRgbChanged;
    }

    private void OnStyleChanged(ColorPickerStyle previous, ColorPickerStyle current)
    {
        _ = previous;
        _ = current;
        var style = Style;
        var sliderStyle = style?.SliderStyle;
        HueSlider.Style = HueSliderStyle(sliderStyle);
        RedSlider.Style = sliderStyle;
        GreenSlider.Style = sliderStyle;
        BlueSlider.Style = sliderStyle;
        var face = style?.StatusFace ?? ColorPickerStyle.DefaultStatusFace;
        _status.Face = new Face(_status.Face.Foreground, face.Background, face.Attributes, face.Underline, face.UnderlineColor);
        Plane.SelectedMarker = style?.SelectedMarker;
    }

    // HueSlider sits directly on top of ColorRamp's per-column rainbow (the two overlap fully in
    // the Overlay built by CreateRgbRoot) and depends on its own background staying transparent so
    // that gradient shows through everywhere but the thumb. RedSlider/GreenSlider/BlueSlider have
    // no such backdrop, so only HueSlider's forwarded Face is pinned transparent here - an opaque
    // ColorPickerStyle.SliderStyle.Face.Background must not reach HueSlider's resolved appearance,
    // or Slider.OnRenderContent clears the gradient out from under it before drawing.
    private static SliderStyle? HueSliderStyle(SliderStyle? sliderStyle) => sliderStyle is null
        ? null
        : sliderStyle with { Face = sliderStyle.Face with { Background = Color.Transparent } };

    #endregion

    #region Synchronization

    private bool Commit(Color requested)
    {
        if (requested.IsTransparent)
        {
            throw new ArgumentException("ColorPicker requires a concrete terminal color.", nameof(requested));
        }

        VerifyMutable();
        var previous = _value;

        if (!SetProperty(ref _value, requested, InvalidationImpact.Render, nameof(Value)))
        {
            SynchronizeParts();
            return false;
        }

        SynchronizeParts();
        ValueChanged?.Invoke(this, new ColorChangedEventArgs(previous, requested));
        return true;
    }

    private void SynchronizeParts()
    {
        var rgb = _value.IsRgb ? _value : Color.Rgb(0, 0, 0);

        rgb.ToHsv(out var hue, out var saturation, out var value);
        _synchronizing = true;

        try
        {
            Plane.SetSelection(hue, saturation, value);
            HueSlider.Value = hue;
            RedSlider.Value = rgb.Red;
            GreenSlider.Value = rgb.Green;
            BlueSlider.Value = rgb.Blue;
            Preview.Value = _value;
            _status.Content = _value.IsDefault
                ? "DEFAULT"
                : $"#{rgb.Red:X2}{rgb.Green:X2}{rgb.Blue:X2}";
            var statusFace = _status.Face;
            _status.Face = new Face(
                rgb.Contrast(),
                statusFace.Background,
                statusFace.Attributes,
                statusFace.Underline,
                statusFace.UnderlineColor);

        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void OnPlaneChanged(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_synchronizing)
        {
            _ = Commit(Color.FromHsv(Plane.Hue, Plane.Saturation, Plane.Value));
        }
    }

    private void OnHueChanged(object? sender, SliderValueChangedEventArgs eventArgs)
    {
        _ = sender;

        if (!_synchronizing)
        {
            Plane.SetSelection(eventArgs.Value, Plane.Saturation, Plane.Value);
            _ = Commit(Color.FromHsv(eventArgs.Value, Plane.Saturation, Plane.Value));
        }
    }

    private void OnRgbChanged(object? sender, SliderValueChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (!_synchronizing)
        {
            _ = Commit(Color.Rgb(RedSlider.Value, GreenSlider.Value, BlueSlider.Value));
        }
    }

    #endregion
}
