import assert from "node:assert/strict";
import test from "node:test";

import * as captureHelpers from "./capture-control-images.mjs";
import { controls } from "./control-image-manifest.mjs";
import { parseCapture } from "./render-terminal-capture.mjs";

const { diffBounds, locateExampleBox } = captureHelpers;

const page = [
    "Heading text",
    " ┌ Example ────┐ ",
    " │ [ Accept ]  │ ",
    " └─────────────┘ ",
    " ┌ Example ────┐ ",
    " │ second box  │ ",
    " └─────────────┘ ",
].join("\n");

test("locateExampleBox returns the requested occurrence's border rectangle", () => {
    const rows = parseCapture(page);

    assert.deepEqual(locateExampleBox(rows), {
        top: 2,
        left: 2,
        bottom: 4,
        right: 16,
    });
    assert.deepEqual(locateExampleBox(rows, 2), {
        top: 5,
        left: 2,
        bottom: 7,
        right: 16,
    });
    assert.equal(locateExampleBox(rows, 3), null);
});

test("locateStateExampleBox honors a state's example occurrence", () => {
    const rows = parseCapture(page);

    assert.equal(typeof captureHelpers.locateStateExampleBox, "function");
    assert.deepEqual(
        captureHelpers.locateStateExampleBox(rows, { example: 2 }),
        {
            top: 5,
            left: 2,
            bottom: 7,
            right: 16,
        },
    );
});

test("locateStateExampleBox selects an example by visible marker", () => {
    const rows = parseCapture(page);

    assert.deepEqual(
        captureHelpers.locateStateExampleBox(rows, {
            example: "second box",
        }),
        {
            top: 5,
            left: 2,
            bottom: 7,
            right: 16,
        },
    );
});

test("locateExampleBox returns null for a box without a bottom border", () => {
    const rows = parseCapture("┌ Example ──┐\n│ cut off");

    assert.equal(locateExampleBox(rows), null);
});

test("diffBounds reports the rectangle covering every changed cell", () => {
    const before = parseCapture("abcd\nefgh\nijkl");
    const after = parseCapture("abcd\nefGH\nijkL");

    assert.deepEqual(diffBounds(before, after), {
        top: 2,
        left: 3,
        bottom: 3,
        right: 4,
    });
    assert.equal(diffBounds(before, before), null);
});

test("locateExampleBox rejects a box whose bottom is hidden behind the next box", () => {
    const rows = parseCapture(
        ["┌ Example ──┐", "│ cut off", "┌ Example ──┐", "│ next", "└───────────┘"].join("\n"),
    );

    assert.equal(locateExampleBox(rows), null);
});

test("SuggestionInput open capture_WhenPopupIsContained_UsesStableExampleBounds", () => {
    const entry = controls.find(({ doc }) => doc === "input/suggestion-input");
    const open = entry.states.find(({ name }) => name === "open");

    assert.equal(open.popup, undefined);
});
