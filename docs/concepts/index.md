# Shared concept specifications

## Concept map

Concept pages own behavior that several controls share. A control page links
here for the common rule and documents only its own specialization.

```mermaid
flowchart TB
    Hosting["Hosting"] --> Screen["Screen and lifecycle"]
    Screen --> Threading["Dispatcher and threading"]
    Threading --> Input["Input routing and focus"]
    Input --> Controls["Retained controls"]
    Controls --> Surfaces["Floating surfaces"]
    Controls --> Invalidation["Invalidation and UI updates"]
    Invalidation --> Layout["Measure, arrange, scrolling"]
    Invalidation --> Rendering["Cell rendering"]
    Controls --> Styling["Styles and themes"]
    Layout --> Box["Margin, border, padding, content"]
    Styling --> Chrome["Intrinsic border and shadow"]
    Layout --> Geometry["Unicode cell geometry"]
    Styling --> Rendering
    Geometry --> Rendering
```

- [Unicode cell geometry](unicode-cell-geometry.md#overview)
- [Images](images.md#overview)
- [Styling](styling.md#overview)
- [Intrinsic chrome](intrinsic-chrome.md#overview)
- [Themes](themes.md#overview)
- [Markdown documents](markdown-documents.md#overview)
- [Syntax highlighting](syntax-highlighting.md#overview)
- [Layout](layout.md#overview)
- [Invalidation and UI updates](invalidation.md#overview)
- [Box model](box-model.md#overview)
- [Scrolling](scrolling.md#overview)
- [Focus](focus.md#overview)
- [Modality](modality.md#overview)
- [Input routing](input-routing.md#overview)
- [Access keys](access-keys.md#overview)
- [Threading](threading.md#overview)
- [Data binding](data-binding.md#overview)
- [Lifecycle events](lifecycle-events.md#overview)
- [Floating surfaces](floating-surfaces.md#overview)
- [Screen](screen.md#overview)
- [Custom components](custom-components.md#overview)
- [Safe degradation](safe-degradation.md#overview)
- [Hosting](hosting.md#overview)
