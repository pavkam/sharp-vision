# SharpVision Document

Markdown and programmatic content share the **same** semantic tree and wrapping
engine. Inline text supports _emphasis_, **strong emphasis**, **_combined
emphasis_**, ~~literal tildes without the extension~~, `inline code`, and
``code containing a ` backtick``.
[Activatable links](https://github.com/pavkam/sharp-vision) and angle autolinks
such as <https://example.com> work too.
[Titled links](https://example.com/guide "opens the guide") keep the title out
of their target. Angle-delimited destinations permit spaces.

Backslash escapes keep \*punctuation\* literal.

Paragraph boundary spaces and tabs are structural, while interior spacing stays
authored content.

Setext heading level 1\
can span multiple lines
=======================

Setext heading level 2
----------------------

## Heading level 2

### Heading level 3

#### Heading level 4

##### Heading level 5

###### Heading level 6

An ordinary source newline becomes a soft break inside the same paragraph. Two
trailing spaces create a hard break.  
This sentence starts on the next rendered line. A trailing backslash also makes
a hard break.\
So this sentence does too.

Only valid fence openers split paragraphs into code blocks.

- Unicode-safe text: café, 你好, and 👩‍💻
- Bulleted lists use flowing inline content
  - Nested items retain their own marker gutter
  - Continuation text stays inside the parent item

0. Numbered lists may begin at zero
1. Later items share the measured marker gutter
2.
3. Empty items retain their authored position
4. Multiple marker spaces stay structural: `4.    item`
5. Delimiter changes such as `-` to `+` start a new list block

   > Three leading spaces still form a block quote with **inline formatting**.

---

```csharp
var document = new Document();
document.Load(markdown, new MarkdownDocumentReader());
```

```text
Tilde fences are supported alongside backtick fences.
```
