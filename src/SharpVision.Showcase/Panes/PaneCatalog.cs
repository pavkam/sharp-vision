using System.Collections.ObjectModel;

using SharpVision.Showcase.Panes.Border;
using SharpVision.Showcase.Panes.Button;
using SharpVision.Showcase.Panes.Canvas;
using SharpVision.Showcase.Panes.CheckBox;
using SharpVision.Showcase.Panes.ComboBox;
using SharpVision.Showcase.Panes.Dock;
using SharpVision.Showcase.Panes.FigletText;
using SharpVision.Showcase.Panes.Grid;
using SharpVision.Showcase.Panes.List;
using SharpVision.Showcase.Panes.Menu;
using SharpVision.Showcase.Panes.Overlay;
using SharpVision.Showcase.Panes.Popup;
using SharpVision.Showcase.Panes.RadioButton;
using SharpVision.Showcase.Panes.RichText;
using SharpVision.Showcase.Panes.ScrollBar;
using SharpVision.Showcase.Panes.ScrollView;
using SharpVision.Showcase.Panes.Shadow;
using SharpVision.Showcase.Panes.Stack;
using SharpVision.Showcase.Panes.Table;
using SharpVision.Showcase.Panes.Text;
using SharpVision.Showcase.Panes.TextInput;
using SharpVision.Showcase.Panes.Theming;
using SharpVision.Showcase.Panes.Window;

namespace SharpVision.Showcase.Panes;

/// <summary>Aggregates every showcase pane into one immutable navigation catalog.</summary>
internal static class PaneCatalog
{
    /// <summary>Gets one immutable page per concrete shipped control.</summary>
    internal static IReadOnlyList<Page> Pages { get; } = new ReadOnlyCollection<Page>(
    [
        BorderPane.Create(),
        ButtonPane.Create(),
        CanvasPane.Create(),
        CheckBoxPane.Create(),
        ComboBoxPane.Create(),
        DockPane.Create(),
        FigletTextPane.Create(),
        GridPane.Create(),
        ListPane.Create(),
        MenuPane.Create(),
        OverlayPane.Create(),
        PopupPane.Create(),
        RadioButtonPane.Create(),
        RichTextPane.Create(),
        ScrollBarPane.Create(),
        ScrollViewPane.Create(),
        ShadowPane.Create(),
        StackPane.Create(),
        TablePane.Create(),
        TextPane.Create(),
        TextInputPane.Create(),
        WindowPane.Create(),
        ThemingPane.Create(),
    ]);
}
