// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Verifies the immutable FilePickerDialog/SaveFileDialog aggregate presentation records:
/// their declared one-hop fallback to <see cref="WindowStyle"/>'s "window" key, and their
/// invalidation policy.</summary>
public sealed class FileDialogStyleTests
{
    /// <summary>Verifies Default carries WindowStyle's own Face/Border/Shadow and the
    /// established root padding, content spacing, and file-list border.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Default_ResolvesThemeWindowStyleDefaultsWithEstablishedGeometry(bool isFilePicker)
    {
        FileDialogStyle style = isFilePicker ? FilePickerDialogStyle.Default : SaveFileDialogStyle.Default;

        style.Face.ShouldBe(WindowStyle.Default.Face);
        style.Border.ShouldBe(WindowStyle.Default.Border);
        style.Shadow.ShouldBe(WindowStyle.Default.Shadow);
        style.RootPadding.ShouldBe(new Thickness(left: 1, top: 1, right: 1, bottom: 0));
        style.ContentSpacing.ShouldBe(1);
        style.FileListBorder.Sides.ShouldBe(BorderSide.All);
        style.FileListBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
    }

    /// <summary>Verifies an unauthored theme resolves each dialog style to the Window fallback appearance.</summary>
    [Fact]
    public void Definition_Resolve_WhenNoLocalAndThemeDoesNotAuthorDialog_FallsBackToWindow()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());
        var window = theme.GetWindowStyleSet();

        var picker = FilePickerDialogStyle.Definition.Resolve(null, theme);
        var save = SaveFileDialogStyle.Definition.Resolve(null, theme);

        picker.Border.ShouldBe(window.Normal.Border);
        save.Border.ShouldBe(window.Normal.Border);
    }

    /// <summary>Verifies a local override always wins over both the theme and the fallback.</summary>
    [Fact]
    public void Definition_Resolve_WhenLocalIsSupplied_LocalWins()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());
        var local = FilePickerDialogStyle.Default with { ContentSpacing = 4 };

        var resolved = FilePickerDialogStyle.Definition.Resolve(local, theme);

        resolved.ShouldBe(local);
    }

    /// <summary>Verifies a content-geometry change is classified as a measure-affecting invalidation.</summary>
    [Fact]
    public void Definition_Compare_WhenRootPaddingChanges_IsMeasure()
    {
        var previous = SaveFileDialogStyle.Default;
        var current = previous with { RootPadding = new Thickness(2) };

        SaveFileDialogStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies a change that alters neither geometry member is non-invalidating.</summary>
    [Fact]
    public void Definition_Compare_WhenOnlyTheOwnFaceChanges_IsNone()
    {
        var previous = FilePickerDialogStyle.Default;
        var current = previous with { Face = previous.Face with { Foreground = SemanticColor.Accent } };

        FilePickerDialogStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.None);
    }

    /// <summary>Verifies a FileListBorder change is NOT irrelevant. This test previously asserted
    /// the opposite: the border is applied to the retained file list from MeasureOverride, so a
    /// Compare returning None means nothing schedules the pass that would apply it.</summary>
    [Fact]
    public void Definition_Compare_WhenTheFileListBorderChanges_IsMeasure()
    {
        var previous = FilePickerDialogStyle.Default;
        var current = previous with { FileListBorder = previous.FileListBorder with { GlyphStyle = BorderGlyphStyle.Heavy } };

        FilePickerDialogStyle.Definition.Compare(previous, null, current, null).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>Verifies both dialog definitions inherit Window close-chrome comparison.</summary>
    [Fact]
    public void Definition_Compare_WhenCloseChromeChanges_IsRenderForBothDialogs()
    {
        var picker = FilePickerDialogStyle.Default;
        var save = SaveFileDialogStyle.Default;

        FilePickerDialogStyle.Definition.Compare(
            picker,
            null,
            picker with { CloseLeftBracket = new Rune('{') },
            null).ShouldBe(InvalidationImpact.Render);
        SaveFileDialogStyle.Definition.Compare(
            save,
            null,
            save with { CloseMarkPressedColor = SemanticColor.Error },
            null).ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies both public constructors reject invalid spacing at style creation.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenContentSpacingIsNegative_ThrowsBeforePublication(bool isFilePicker)
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            CreateStyle(isFilePicker, contentSpacing: -1));
    }

    /// <summary>Verifies record-copy initialization cannot bypass the spacing invariant.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ContentSpacing_WhenRecordCopyIsNegative_ThrowsBeforePublication(bool isFilePicker)
    {
        FileDialogStyle style = isFilePicker ? FilePickerDialogStyle.Default : SaveFileDialogStyle.Default;

        _ = Should.Throw<ArgumentOutOfRangeException>(() => style with { ContentSpacing = -1 });
    }

    private static FileDialogStyle CreateStyle(bool isFilePicker, int contentSpacing) =>
        isFilePicker
            ? new FilePickerDialogStyle(
                WindowStyle.Default.Face,
                WindowStyle.Default.Border,
                WindowStyle.Default.Shadow,
                new Thickness(1),
                contentSpacing,
                ControlStyle.NoBorder)
            : new SaveFileDialogStyle(
                WindowStyle.Default.Face,
                WindowStyle.Default.Border,
                WindowStyle.Default.Shadow,
                new Thickness(1),
                contentSpacing,
                ControlStyle.NoBorder);
}
