import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
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

test("locateStateExampleBox selects an example by its visible heading above the box", () => {
    const rows = parseCapture(
        [
            "Item activation and availability",
            "Description",
            " ┌ Example ────┐ ",
            " │ [ Invoke ]  │ ",
            " └─────────────┘ ",
            "Separator styling and participation",
            "Description",
            " ┌ Example ────┐ ",
            " │ [ Toggle ]  │ ",
            " └─────────────┘ ",
        ].join("\n"),
    );

    assert.deepEqual(
        captureHelpers.locateStateExampleBox(rows, {
            example: "Separator styling and participation",
        }),
        {
            top: 8,
            left: 2,
            bottom: 10,
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
    const entry = controls.find(
        ({ doc }) => doc === "controls/input/suggestion-input",
    );
    const open = entry.states.find(({ name }) => name === "open");

    assert.equal(open.popup, undefined);
});

test("selectCaptureRegion_WhenPopupIsContained_UsesInsetExampleBounds", () => {
    const before = parseCapture("abcd\nefgh\nijkl");
    const after = parseCapture("abcd\nefGh\nijkl");

    assert.deepEqual(
        captureHelpers.selectCaptureRegion(
            { top: 1, left: 1, bottom: 3, right: 4 },
            { popup: undefined },
            before,
            after,
        ),
        { top: 2, left: 2, bottom: 2, right: 3 },
    );
});

test("InfoBar capture_WhenMultipleExamplesExist_SelectsInteractiveSpecimen", () => {
    const entry = controls.find(
        ({ doc }) => doc === "controls/notifications/info-bar",
    );
    const [defaultState] = entry.states;

    assert.equal(defaultState.example, "Allow close");
});

test("manifest_WhenHelperDocsSharePrimaryPages_UsesStableNamedExamples", () => {
    const expected = new Map([
        [
            "controls/navigation/breadcrumb-item",
            ["Breadcrumb", "Invoke Design", false],
        ],
        [
            "controls/input/command-bar-item",
            ["CommandBar", "Invoke Deploy", false],
        ],
        [
            "controls/input/command-bar-separator",
            ["CommandBar", "Cycle glyph", false],
        ],
        ["controls/menus/menu-item", ["Menu", "More options", true]],
    ]);

    for (const [doc, [pageName, example, popup]] of expected) {
        const entry = controls.find((candidate) => candidate.doc === doc);

        assert.equal(entry.page, pageName);
        assert.equal(entry.states[0].example, example);
        assert.equal(entry.states[0].popup === true, popup);
    }
});

test("manifest_WhenDialogsAreCaptured_MapsEachDialogDocToItsPrimaryPage", () => {
    const expected = new Map([
        ["dialogs/message-box", ["MessageBox", "Yes / No", "Yes / No"]],
        [
            "dialogs/file-picker-dialog",
            ["OpenFilePicker", "Open one file", "Open one file"],
        ],
        [
            "dialogs/save-file-dialog",
            ["SaveFilePicker", "Overwrite report", "Overwrite report"],
        ],
    ]);

    for (const [doc, [pageName, example, action]] of expected) {
        const entry = controls.find((candidate) => candidate.doc === doc);
        const [state] = entry.states;

        assert.equal(entry.page, pageName);
        assert.equal(state.example, example);
        assert.equal(state.popup, true);
        assert.deepEqual(state.actions, [{ click: action }]);
    }
});

test("manifest_WhenContainedPopupSurfacesAreCaptured_ShowsTheOpenSurfaceInItsExample", () => {
    const contextMenu = controls.find(
        ({ doc }) => doc === "controls/menus/context-menu",
    );
    const popup = controls.find(({ doc }) => doc === "controls/popups/popup");

    assert.deepEqual(contextMenu.states, [
        {
            actions: [{ secondaryClick: "Right-click me" }],
        },
    ]);
    assert.deepEqual(popup.states, [
        {
            actions: [{ click: "Actions" }],
        },
    ]);
});

test("manifest_WhenFocusedExamplesMutate_CapturesBothWrapAxesAndWindowDefault", () => {
    const wrap = controls.find(({ doc }) => doc === "controls/layout/wrap");
    const window = controls.find(({ doc }) => doc === "controls/windows/window");

    assert.deepEqual(wrap.states, [
        {},
        {
            name: "horizontal-reflow",
            example: "Narrow rows",
            actions: [{ click: "Narrow rows" }],
        },
        {
            name: "vertical-reflow",
            example: "Shorten columns",
            actions: [{ click: "Shorten columns" }],
        },
    ]);
    assert.deepEqual(window.states, [
        {},
        {
            name: "default-action",
            example: "Focus command target",
            actions: [{ click: "Focus command target" }, { key: "Enter" }],
        },
    ]);
});

test("manifest_WhenStylingConceptIsCaptured_UsesPaletteAndRealInputStates", () => {
    const styling = controls.find(({ doc }) => doc === "concepts/styling");

    assert.equal(styling.page, "Styling");
    assert.equal(styling.imageDirectory, "concepts");
    assert.deepEqual(styling.states, [
        { name: "palette", example: "Complete semantic palette" },
        {
            name: "states",
            example: "Live element states",
            actions: [{ click: "Selected row" }],
        },
        {
            name: "states-focused",
            example: "Live element states",
            actions: [{ click: "Focus target" }],
        },
        {
            name: "states-pressed",
            example: "Live element states",
            actions: [{ press: "Press target" }],
        },
    ]);
});

test("StylingPane_WhenBuildingSemanticPalette_EnumeratesTheEnumDirectly", async () => {
    const root = path.resolve(import.meta.dirname, "..");
    const source = await readFile(
        path.join(root, "examples", "Showcase", "Panes", "StylingPane.cs"),
        "utf8",
    );

    assert.match(
        source,
        /foreach \(var semanticColor in Enum\.GetValues<SemanticColor>\(\)\)/u,
    );
    assert.doesNotMatch(source, /ThemeCatalog\.|ResolveAppearance\(/u);
});

test("manifest_WhenOverflowSurfacesOpen_UsesPopupCaptureAndSourceExamples", () => {
    const breadcrumb = controls.find(
        ({ doc }) => doc === "controls/navigation/breadcrumb",
    );
    const commandBar = controls.find(
        ({ doc }) => doc === "controls/input/command-bar",
    );
    const breadcrumbOverflow = breadcrumb.states.find(
        ({ name }) => name === "overflow",
    );
    const breadcrumbOverflowOpen = breadcrumb.states.find(
        ({ name }) => name === "overflow-open",
    );
    const commandBarOverflow = commandBar.states.find(
        ({ name }) => name === "open",
    );

    assert.equal(breadcrumbOverflow.example, "Clear current");
    assert.equal(breadcrumbOverflow.popup, undefined);
    assert.deepEqual(breadcrumbOverflow.actions, [{ click: "Narrow path" }]);
    assert.equal(breadcrumbOverflowOpen.example, "Widen path");
    assert.equal(breadcrumbOverflowOpen.popup, true);
    assert.deepEqual(breadcrumbOverflowOpen.actions, [{ click: " …" }]);
    assert.equal(commandBarOverflow.example, "Narrow bar");
    assert.equal(commandBarOverflow.popup, true);
    assert.deepEqual(commandBarOverflow.actions, [{ click: " …" }]);
});
