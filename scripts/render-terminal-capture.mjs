import { readFile, writeFile } from "node:fs/promises";
import process from "node:process";
import { pathToFileURL } from "node:url";

const standard = [
    "#000000",
    "#cd0000",
    "#00cd00",
    "#cdcd00",
    "#0000ee",
    "#cd00cd",
    "#00cdcd",
    "#e5e5e5",
    "#7f7f7f",
    "#ff0000",
    "#00ff00",
    "#ffff00",
    "#5c5cff",
    "#ff00ff",
    "#00ffff",
    "#ffffff",
];
const cube = [0, 95, 135, 175, 215, 255];

const hex = (value) => value.toString(16).padStart(2, "0");

const color = (index) => {
    if (index < standard.length) return standard[index];

    if (index < 232) {
        const value = index - 16;
        const red = cube[Math.floor(value / 36)];
        const green = cube[Math.floor((value % 36) / 6)];
        const blue = cube[value % 6];
        return `#${hex(red)}${hex(green)}${hex(blue)}`;
    }

    const gray = 8 + (index - 232) * 10;
    return `#${hex(gray)}${hex(gray)}${hex(gray)}`;
};

const escapeHtml = (value) =>
    value
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");

const reset = () => ({
    bold: false,
    dim: false,
    italic: false,
    underline: false,
    reverse: false,
    foreground: undefined,
    background: undefined,
});

const apply = (state, values) => {
    for (let position = 0; position < values.length; position += 1) {
        const value = values[position];

        if (value === 0) Object.assign(state, reset());
        else if (value === 1) state.bold = true;
        else if (value === 2) state.dim = true;
        else if (value === 3) state.italic = true;
        else if (value === 4) state.underline = true;
        else if (value === 7) state.reverse = true;
        else if (value === 22) {
            state.bold = false;
            state.dim = false;
        } else if (value === 23) state.italic = false;
        else if (value === 24) state.underline = false;
        else if (value === 27) state.reverse = false;
        else if (value >= 30 && value <= 37)
            state.foreground = color(value - 30);
        else if (value === 39) state.foreground = undefined;
        else if (value >= 40 && value <= 47)
            state.background = color(value - 40);
        else if (value === 49) state.background = undefined;
        else if (value >= 90 && value <= 97)
            state.foreground = color(value - 90 + 8);
        else if (value >= 100 && value <= 107)
            state.background = color(value - 100 + 8);
        else if ((value === 38 || value === 48) && values[position + 1] === 5) {
            const selected = color(values[position + 2] ?? 0);

            if (value === 38) state.foreground = selected;
            else state.background = selected;

            position += 2;
        }
    }
};

const styleOf = (state) => {
    const styles = [];
    const foreground = state.reverse
        ? (state.background ?? "#0d1117")
        : state.foreground;
    const background = state.reverse
        ? (state.foreground ?? "#e6edf3")
        : state.background;

    if (state.bold) styles.push("font-weight:700");
    if (state.dim) styles.push("opacity:.7");
    if (state.italic) styles.push("font-style:italic");
    if (state.underline) styles.push("text-decoration:underline");
    if (foreground !== undefined) styles.push(`color:${foreground}`);
    if (background !== undefined) styles.push(`background:${background}`);
    return styles.join(";");
};

const wideRanges =
    /[ᄀ-ᅟ⺀-〾ぁ-㏿㐀-䶿一-鿿ꀀ-꓏가-힣豈-﫿︰-﹏＀-｠￠-￦]/u;

// Terminals render default-text-presentation pictographs such as ▶ in one
// cell, so only variation-selector emoji and the supplementary pictographic
// planes count as wide alongside the East Asian wide and fullwidth ranges.
export const graphemeWidth = (grapheme) => {
    if (grapheme.includes("\ufe0f")) return 2;

    const code = grapheme.codePointAt(0) ?? 0;

    if (code >= 0x1f000 && /\p{Extended_Pictographic}/u.test(grapheme))
        return 2;

    return wideRanges.test(grapheme) ? 2 : 1;
};

const segmenter = new Intl.Segmenter("en", { granularity: "grapheme" });

export const parseCapture = (capture) => {
    if (typeof capture !== "string")
        throw new TypeError("capture must be a string.");

    const state = reset();
    const pattern = /\u001b\[([0-9;]*)m/gu;
    const content = capture.replaceAll("\r", "");
    const rows = [[]];
    let position = 0;

    const append = (text) => {
        const parts = text.split("\n");

        parts.forEach((part, index) => {
            if (index > 0) rows.push([]);

            const row = rows[rows.length - 1];
            const style = styleOf(state);

            for (const { segment } of segmenter.segment(part)) {
                row.push({
                    text: segment,
                    style,
                    width: graphemeWidth(segment),
                });
            }
        });
    };

    for (const match of content.matchAll(pattern)) {
        append(content.slice(position, match.index));
        const values =
            match[1].length === 0
                ? [0]
                : match[1]
                      .split(";")
                      .map((value) => Number.parseInt(value, 10));
        apply(state, values);
        position = match.index + match[0].length;
    }

    append(content.slice(position));

    if (content.endsWith("\n")) rows.pop();

    return rows;
};

export const rowText = (row) => row.map((cell) => cell.text).join("");

/// Returns the 1-based visual column where `text` first starts in `row`, or 0.
export const findColumn = (row, text) => {
    let visual = 1;

    for (let start = 0; start < row.length; start += 1) {
        let candidate = "";
        let index = start;

        while (index < row.length && candidate.length < text.length) {
            candidate += row[index].text;
            index += 1;
        }

        if (candidate.startsWith(text)) return visual;

        visual += row[start].width;
    }

    return 0;
};

/// Crops parsed rows to a 1-based inclusive rectangle of visual columns.
export const crop = (rows, rect) => {
    const cropped = [];

    for (let index = rect.top - 1; index <= rect.bottom - 1; index += 1) {
        const row = rows[index] ?? [];
        const kept = [];
        let visual = 1;

        for (const cell of row) {
            if (visual >= rect.left && visual + cell.width - 1 <= rect.right)
                kept.push(cell);

            visual += cell.width;
        }

        cropped.push(kept);
    }

    return cropped;
};

export const renderHtml = (rows, { padding = 24 } = {}) => {
    const body = rows
        .map((row) => {
            let markup = "";
            let pendingText = "";
            let pendingStyle = "";

            const flush = () => {
                if (pendingText.length === 0) return;

                const escaped = escapeHtml(pendingText);
                markup +=
                    pendingStyle.length === 0
                        ? escaped
                        : `<span style="${pendingStyle}">${escaped}</span>`;
                pendingText = "";
            };

            for (const cell of row) {
                if (cell.style !== pendingStyle) {
                    flush();
                    pendingStyle = cell.style;
                }

                pendingText += cell.text;
            }

            flush();
            return `<span class="row">${markup}</span>`;
        })
        .join("");
    return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>SharpVision live terminal capture</title>
<style>
html, body { margin: 0; min-height: 100%; background: #0d1117; }
body { display: inline-block; }
pre {
  box-sizing: border-box;
  margin: 0;
  padding: ${padding}px;
  color: #e6edf3;
  background: #0d1117;
  font: 16px/20px Menlo, Monaco, "Cascadia Mono", monospace;
  white-space: pre;
}
/* An inline background covers only the glyph content box, which is shorter
   than the 20px line box and leaves one-pixel gap lines between rows. Block
   rows and full-height inline-block runs make every background tile exactly. */
pre .row { display: block; height: 20px; }
pre .row span { display: inline-block; height: 20px; vertical-align: top; }
</style>
</head>
<body><pre>${body}</pre></body>
</html>
`;
};

export const toHtml = (capture) => renderHtml(parseCapture(capture));

const main = async () => {
    const [, , input, output] = process.argv;

    if (!input || !output)
        throw new Error("Usage: render-terminal-capture.mjs <input> <output>");

    const capture = await readFile(input, "utf8");
    await writeFile(output, toHtml(capture));
};

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
    await main();
}
