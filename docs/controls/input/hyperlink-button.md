# HyperlinkButton

## HyperlinkButton contract

`HyperlinkButton` is a focusable clickable text control styled as a classic
hyperlink with accent foreground and underline.

## API

| Member                        | Default        | Contract                                                                |
| ----------------------------- | -------------- | ----------------------------------------------------------------------- |
| `Content`                     | `null`         | Inherited replaceable visual face.                                      |
| `Text`                        | `null`         | Convenience access to retained `Text` content; assignment rejects null. |
| `Command`, `CommandParameter` | `null`, `null` | Optional command and borrowed parameter.                                |
| `Click`                       | no subscribers | Raised after released state commits and before command execution.       |
| `PerformClick()`              | —              | Runs programmatic activation when visible, enabled, and executable.     |

## Example

```csharp
var link = new HyperlinkButton { Content = new Text("Visit site") };
link.Click += (_, _) => OpenUrl("https://example.com");
```

## Test obligations

| Layer       | Required evidence                                                                                  |
| ----------- | -------------------------------------------------------------------------------------------------- |
| Unit        | Constructors, text/content synchronization, command gating, event order, disposal, and validation. |
| Surface     | Accent underline, hover/focus/pressed/disabled states, Unicode text, and tiny clipping.            |
| Integration | Space, Enter, pointer capture, access key, and programmatic activation parity.                     |
