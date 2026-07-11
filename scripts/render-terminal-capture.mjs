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

const segment = (value, state) => {
    if (value.length === 0) return "";

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

    const escaped = escapeHtml(value);
    return styles.length === 0
        ? escaped
        : `<span style="${styles.join(";")}">${escaped}</span>`;
};

export const toHtml = (capture) => {
    if (typeof capture !== "string")
        throw new TypeError("capture must be a string.");

    const state = reset();
    const pattern = /\u001b\[([0-9;]*)m/gu;
    const content = capture.replaceAll("\r", "");
    let position = 0;
    let body = "";

    for (const match of content.matchAll(pattern)) {
        body += segment(content.slice(position, match.index), state);
        const values =
            match[1].length === 0
                ? [0]
                : match[1]
                      .split(";")
                      .map((value) => Number.parseInt(value, 10));
        apply(state, values);
        position = match.index + match[0].length;
    }

    body += segment(content.slice(position), state);
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
  padding: 24px;
  color: #e6edf3;
  background: #0d1117;
  font: 16px/20px Menlo, Monaco, "Cascadia Mono", monospace;
  white-space: pre;
}
</style>
</head>
<body><pre>${body}</pre></body>
</html>
`;
};

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
