namespace SharpVision.Showcase.Panes;

using System.Collections.ObjectModel;

using SharpVision.Showcase;

/// <summary>Aggregates every showcase pane into one immutable navigation catalog.</summary>
internal static class PaneCatalog
{
    /// <summary>Gets one immutable gallery entry per concrete shipped control.</summary>
    internal static IReadOnlyList<GalleryEntry> Pages { get; } = new ReadOnlyCollection<GalleryEntry>(
    [
        new(BorderShowcasePane.Title, static () => new BorderShowcasePane()),
        new(ButtonShowcasePane.Title, static () => new ButtonShowcasePane()),
        new(CanvasShowcasePane.Title, static () => new CanvasShowcasePane()),
        new(CheckBoxShowcasePane.Title, static () => new CheckBoxShowcasePane()),
        new(ComboBoxShowcasePane.Title, static () => new ComboBoxShowcasePane()),
        new(DockShowcasePane.Title, static () => new DockShowcasePane()),
        new(FigletTextShowcasePane.Title, static () => new FigletTextShowcasePane()),
        new(GridShowcasePane.Title, static () => new GridShowcasePane()),
        new(ListShowcasePane.Title, static () => new ListShowcasePane()),
        new(MenuShowcasePane.Title, static () => new MenuShowcasePane()),
        new(OverlayShowcasePane.Title, static () => new OverlayShowcasePane()),
        new(PopupShowcasePane.Title, static () => new PopupShowcasePane()),
        new(RadioButtonShowcasePane.Title, static () => new RadioButtonShowcasePane()),
        new(RichTextShowcasePane.Title, static () => new RichTextShowcasePane()),
        new(ScrollBarShowcasePane.Title, static () => new ScrollBarShowcasePane()),
        new(ScrollViewShowcasePane.Title, static () => new ScrollViewShowcasePane()),
        new(ShadowShowcasePane.Title, static () => new ShadowShowcasePane()),
        new(StackShowcasePane.Title, static () => new StackShowcasePane()),
        new(TableShowcasePane.Title, static () => new TableShowcasePane()),
        new(TextShowcasePane.Title, static () => new TextShowcasePane()),
        new(TextInputShowcasePane.Title, static () => new TextInputShowcasePane()),
        new(ThemingShowcasePane.Title, static () => new ThemingShowcasePane()),
        new(WindowShowcasePane.Title, static () => new WindowShowcasePane()),
    ]);
}
