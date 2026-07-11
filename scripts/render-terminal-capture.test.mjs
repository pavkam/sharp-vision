import assert from "node:assert/strict";
import test from "node:test";

import { toHtml } from "./render-terminal-capture.mjs";

test("toHtml preserves cells and translates terminal rendition", () => {
    const html = toHtml("plain <tag> \u001b[1;4;38;5;2mstyled\u001b[0m\nnext");

    assert.match(html, /plain &lt;tag&gt;/u);
    assert.match(html, /font-weight:700/u);
    assert.match(html, /text-decoration:underline/u);
    assert.match(html, /color:#00cd00/u);
    assert.match(html, /styled/u);
    assert.match(html, /next/u);
    assert.doesNotMatch(html, /\u001b/u);
});
