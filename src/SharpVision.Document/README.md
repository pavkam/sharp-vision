# SharpVision.Document

`SharpVision.Document` is the optional rich-document and Markdown package for
SharpVision. It provides flowing block and inline content, interactive controls,
and extensible source-format readers without adding document parsing to the core
UI package.

```csharp
var document = new Document();
document.Blocks.Add(new DocumentHeading(1, "Preferences"));
document.Blocks.Add(new DocumentBlockControl(new CheckBox("Send updates")));

document.Load(
    await File.ReadAllTextAsync("README.md"),
    new MarkdownDocumentReader(new MarkdownOptions
    {
        Extensions = MarkdownExtension.GitHubFlavored |
                     MarkdownExtension.WikiLinks |
                     MarkdownExtension.Callouts
    }));
```

Markdown extensions are opt-in. Task and radio list markers create genuine
SharpVision controls with ordinary focus, input, commands, and events.
