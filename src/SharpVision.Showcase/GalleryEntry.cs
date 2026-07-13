namespace SharpVision.Showcase;

using SharpVision.Showcase.Panes;

/// <summary>Registers one showcase control page in gallery navigation.</summary>
/// <param name="Name">The concrete control name shown in the sidebar.</param>
/// <param name="Create">A factory that returns a fresh detached showcase pane.</param>
internal readonly record struct GalleryEntry(string Name, Func<ShowcasePane> Create)
{
    /// <summary>Creates a fresh detached showcase pane owned by the caller.</summary>
    /// <returns>A new showcase pane instance.</returns>
    internal ShowcasePane CreatePane() => Create();
}
