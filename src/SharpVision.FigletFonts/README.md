# SharpVision.FigletFonts

`SharpVision.FigletFonts` is the optional audited font catalog for
[SharpVision](https://github.com/pavkam/sharp-vision). It embeds each font as an
independent resource so loading one font does not read a catalog-wide archive.

```csharp
var font = FigletCatalog.Default.Load("standard");
var title = new FigletText(font) { Content = "SharpVision" };
```

The catalog contains the 18 fonts from the official FIGlet distribution under
BSD-3-Clause and the `Classy` font under MIT. See `THIRD-PARTY-NOTICES.md` and
the packaged `licenses/` directory for provenance and license text.
