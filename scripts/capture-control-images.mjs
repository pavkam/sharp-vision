import { execFileSync } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import process from "node:process";
import { setTimeout as sleep } from "node:timers/promises";
import { pathToFileURL } from "node:url";

import { controls } from "./control-image-manifest.mjs";
import {
    crop,
    findColumn,
    parseCapture,
    renderHtml,
    rowText,
} from "./render-terminal-capture.mjs";

/// Finds the 0-based row of the `occurrence`-th complete Example group box
/// and returns its border rectangle in 1-based rows and visual columns.
export const locateExampleBox = (rows, occurrence = 1) => {
    let remaining = occurrence;

    for (let index = 0; index < rows.length; index += 1) {
        const text = rowText(rows[index]);

        if (!text.includes("┌ Example")) continue;

        remaining -= 1;

        if (remaining > 0) continue;

        const left = findColumn(rows[index], "┌ Example");
        const right = left + segmentWidth(rows[index], left, "┐");

        if (right <= left) return null;

        for (let below = index + 1; below < rows.length; below += 1) {
            const glyph = cellTextAt(rows[below], left);

            if (glyph === "└")
                return {
                    top: index + 1,
                    left,
                    bottom: below + 1,
                    right,
                };

            // A new box starting at the same column means this box's bottom
            // is not visible, so its rectangle must not adopt the next one's.
            if (glyph === "┌") break;
        }

        return null;
    }

    return null;
};

/// Finds the complete Example group box selected by a capture state. A numeric
/// selector chooses the visible occurrence; a string selector chooses the box
/// containing that marker or whose visible heading immediately precedes it.
/// States default to the first example.
export const locateStateExampleBox = (rows, state) => {
    const selector = state.example ?? 1;

    if (typeof selector === "number")
        return locateExampleBox(rows, selector);

    let previousBottom = 0;

    for (let occurrence = 1; ; occurrence += 1) {
        const rect = locateExampleBox(rows, occurrence);

        if (rect === null) return null;

        for (let index = previousBottom; index < rect.bottom; index += 1) {
            const column = findColumn(rows[index], selector);

            if (column > 0) return rect;
        }

        previousBottom = rect.bottom;
    }
};

const cellTextAt = (row, visualColumn) => {
    let visual = 1;

    for (const cell of row) {
        if (visual === visualColumn) return cell.text;

        visual += cell.width;

        if (visual > visualColumn) return null;
    }

    return null;
};

const segmentWidth = (row, fromVisual, closing) => {
    let visual = 1;
    let offset = 0;
    let started = false;

    for (const cell of row) {
        if (started) {
            offset += cell.width;

            if (cell.text === closing) return offset;
        } else if (visual === fromVisual) {
            started = true;
        }

        visual += cell.width;
    }

    return 0;
};

/// Returns the 1-based row/visual-column bounding rectangle of every cell
/// that differs between two parsed captures, or null when they are equal.
export const diffBounds = (before, after) => {
    let bounds = null;

    const include = (rowIndex, columnStart, columnEnd) => {
        if (bounds === null)
            bounds = {
                top: rowIndex,
                left: columnStart,
                bottom: rowIndex,
                right: columnEnd,
            };
        else {
            bounds.top = Math.min(bounds.top, rowIndex);
            bounds.left = Math.min(bounds.left, columnStart);
            bounds.bottom = Math.max(bounds.bottom, rowIndex);
            bounds.right = Math.max(bounds.right, columnEnd);
        }
    };

    const total = Math.max(before.length, after.length);

    for (let index = 0; index < total; index += 1) {
        const first = expand(before[index] ?? []);
        const second = expand(after[index] ?? []);
        const width = Math.max(first.length, second.length);

        for (let column = 0; column < width; column += 1) {
            if ((first[column] ?? " |") !== (second[column] ?? " |"))
                include(index + 1, column + 1, column + 1);
        }
    }

    return bounds;
};

/// Selects the crop owned by one capture state. Ordinary and contained-popup
/// states use the example interior; overlay popups may widen to changed cells.
export const selectCaptureRegion = (rect, state, baseline, frame) => {
    const interior = {
        top: rect.top + 1,
        left: rect.left + 1,
        bottom: rect.bottom - 1,
        right: rect.right - 1,
    };

    if (state.popup !== true) return interior;

    return diffBounds(baseline, frame) ?? interior;
};

const expand = (row) => {
    const slots = [];

    for (const cell of row) {
        const value = `${cell.text}|${cell.style}`;

        for (let slot = 0; slot < cell.width; slot += 1) slots.push(value);
    }

    return slots;
};

const entryPattern = (marker, page) =>
    new RegExp(`${marker} ${page.replaceAll(" ", "\\s")}(?![0-9A-Za-z])`, "u");

// The captured tmux session's fixed viewport size. A page's own scrollable body reserves the
// session's rightmost couple of columns for its own vertical scrollbar track, which is the one
// screen position guaranteed to stay outside every specimen's own bounds regardless of how wide
// an individual DocExample happens to render - the reason the page-scroll search below targets
// `sessionWidth - 2` rather than a fixed column: a fixed column that happened to sit inside a
// wide specimen would have its wheel input consumed by that specimen's own scrolling/selection
// instead of scrolling the page underneath it.
const sessionWidth = 150;
const sessionHeight = 50;

const main = async () => {
    const requested = new Set(process.argv.slice(2));
    const root = path.resolve(import.meta.dirname, "..");
    const output = path.join(root, "docs", "images", "controls");
    const session = `sharpvision-controls-${process.pid}`;
    const temporary = mkdtempSync(path.join(tmpdir(), "sharpvision-controls-"));

    const tmux = (...args) =>
        execFileSync("tmux", ["-u", ...args], { encoding: "utf8" });
    const send = (...args) => tmux("send-keys", "-t", session, ...args);
    const sendSgr = (code, x, y, suffix) => {
        const bytes = [...Buffer.from(`\u001b[<${code};${x};${y}${suffix}`)];
        send("-H", ...bytes.map((byte) => byte.toString(16).padStart(2, "0")));
    };
    const capture = () =>
        parseCapture(tmux("capture-pane", "-t", session, "-p", "-e"));
    const plain = (rows) => rows.map(rowText).join("\n");

    const waitFor = async (predicate, failure, attempts = 60) => {
        for (let attempt = 0; attempt < attempts; attempt += 1) {
            const rows = capture();

            if (predicate(rows)) return rows;

            await sleep(100);
        }

        throw new Error(failure);
    };

    const settled = async (animated) => {
        if (animated) {
            await sleep(300);
            return capture();
        }

        let previous = plain(capture());

        for (let attempt = 0; attempt < 30; attempt += 1) {
            await sleep(120);
            const rows = capture();
            const current = plain(rows);

            if (current === previous) return rows;

            previous = current;
        }

        throw new Error("The pane did not settle into a stable frame.");
    };

    const findTarget = (rows, rect, text) => {
        for (let index = rect.top - 1; index < rect.bottom; index += 1) {
            const column = findColumn(rows[index], text);

            if (column >= rect.left && column <= rect.right)
                return { row: index + 1, column };
        }

        throw new Error(`The example does not show the target "${text}".`);
    };

    for (const dependency of ["dotnet", "tmux", "playwright"]) {
        execFileSync("which", [dependency], { stdio: "pipe" });
    }

    execFileSync(
        "dotnet",
        [
            "build",
            path.join(root, "SharpVision.slnx"),
            "--configuration",
            "Release",
            "--no-restore",
            "--verbosity",
            "quiet",
        ],
        { stdio: "inherit" },
    );
    await mkdir(output, { recursive: true });
    tmux(
        "new-session",
        "-d",
        "-x",
        String(sessionWidth),
        "-y",
        String(sessionHeight),
        "-s",
        session,
        "-c",
        root,
        "dotnet run --project examples/Showcase/SharpVision.Showcase.csproj --configuration Release --no-build",
    );
    tmux("set-option", "-t", session, "status", "off");

    try {
        const ready = await waitFor(
            (rows) => plain(rows).includes("Filter pages"),
            "The showcase did not render its page filter.",
            600,
        );
        // The filter textbox keeps its row across selections even after its
        // placeholder is replaced by typed text, so locate it exactly once.
        const filterRow =
            ready.findIndex((row) => rowText(row).includes("Filter pages")) + 1;

        const entries = controls.filter(
            (entry) =>
                requested.size === 0 ||
                requested.has(path.basename(entry.doc)),
        );

        const failures = [];

        for (const entry of entries) {
            try {
            const slug = path.basename(entry.doc);

            sendSgr(0, 10, filterRow, "M");
            sendSgr(0, 10, filterRow, "m");
            send("C-a");
            send("BSpace");
            send("-l", entry.page);

            // The currently open page lists itself as "›" instead of "·", so
            // accept either marker and only click entries not yet selected.
            // The list refilters progressively while the typed name streams
            // in, so re-read the entry's row before every click attempt.
            const listedPattern = entryPattern("[·›]", entry.page);
            const selectedPattern = entryPattern("›", entry.page);

            await waitFor(
                (rows) => listedPattern.test(plain(rows)),
                `The ${entry.page} navigation entry did not appear.`,
            );

            let opened = false;

            for (let attempt = 0; attempt < 5 && !opened; attempt += 1) {
                await sleep(250);
                const rows = capture();

                if (selectedPattern.test(plain(rows))) {
                    opened = true;
                    break;
                }

                const navigationRow =
                    rows.findIndex((row) =>
                        listedPattern.test(rowText(row)),
                    ) + 1;

                if (navigationRow === 0) continue;

                // Short page names end before the fixed column the original
                // capture script clicked, so click inside the matched text.
                const matched = listedPattern.exec(
                    rowText(rows[navigationRow - 1]),
                );
                const navigationColumn =
                    findColumn(rows[navigationRow - 1], matched[0]) + 2;

                sendSgr(0, navigationColumn, navigationRow, "M");
                sendSgr(0, navigationColumn, navigationRow, "m");

                try {
                    await waitFor(
                        (current) => selectedPattern.test(plain(current)),
                        "not selected yet",
                        20,
                    );
                    opened = true;
                } catch {
                    // The row moved between capture and click; try again.
                }
            }

            if (!opened)
                throw new Error(
                    `The showcase did not select the ${entry.page} page.`,
                );

            // The sidebar marker flips before the reading pane swaps, so wait
            // for the new page's overview headline or the box search could
            // settle on the previous page's geometry.
            await waitFor(
                (rows) => plain(rows).includes(`││ ${entry.page}`),
                `The ${entry.page} page content did not render.`,
                100,
            );

            for (let scroll = 0; scroll < 40; scroll += 1) {
                if (locateExampleBox(capture()) !== null) break;

                sendSgr(65, sessionWidth - 2, 25, "M");
                await sleep(80);
            }

            for (const state of entry.states ?? [{}]) {
                const name = state.name
                    ? `${slug}-${state.name}.png`
                    : `${slug}.png`;
                let held = null;

                let baseline = null;
                let rect = null;

                for (let scroll = 0; scroll < 40; scroll += 1) {
                    // Park the pointer outside the pane so no specimen renders
                    // its hover appearance in the captured frame.
                    sendSgr(35, 0, 0, "M");
                    baseline = await settled(entry.animated === true);
                    rect = locateStateExampleBox(baseline, state);

                    if (rect !== null) break;

                    sendSgr(65, sessionWidth - 2, 25, "M");
                    await sleep(80);
                }

                if (rect === null)
                    throw new Error(
                        `The ${entry.page} page did not show example ${state.example ?? 1} completely.`,
                    );

                for (const action of state.actions ?? []) {
                    if (action.click !== undefined) {
                        const target = findTarget(
                            capture(),
                            rect,
                            action.click,
                        );
                        sendSgr(0, target.column + 1, target.row, "M");
                        sendSgr(0, target.column + 1, target.row, "m");
                    } else if (action.press !== undefined) {
                        const target = findTarget(
                            capture(),
                            rect,
                            action.press,
                        );
                        sendSgr(0, target.column + 1, target.row, "M");
                        held = target;
                    } else if (action.drag !== undefined) {
                        if (held === null)
                            throw new Error(
                                "A drag action requires an earlier press action.",
                            );

                        const target = findTarget(
                            capture(),
                            rect,
                            action.drag,
                        );
                        sendSgr(32, target.column + 1, target.row, "M");
                        held = target;
                    } else if (action.hover !== undefined) {
                        const target = findTarget(
                            capture(),
                            rect,
                            action.hover,
                        );
                        sendSgr(35, target.column + 1, target.row, "M");
                    } else if (action.key !== undefined) {
                        send(action.key);
                    } else if (action.type !== undefined) {
                        send("-l", action.type);
                    } else if (action.wait !== undefined) {
                        await sleep(action.wait);
                    }

                    await sleep(150);
                }

                const frame = await settled(
                    entry.animated === true || held !== null,
                );
                // A popup overdraws neighboring content the example box does
                // not own, so overlay states crop to the cells actions changed.
                const region = selectCaptureRegion(rect, state, baseline, frame);

                const html = path.join(temporary, `${slug}.html`);
                await writeFile(
                    html,
                    renderHtml(crop(frame, region), { padding: 8 }),
                );
                execFileSync(
                    "playwright",
                    [
                        "screenshot",
                        "--browser",
                        "chromium",
                        "--viewport-size",
                        "50,50",
                        "--full-page",
                        pathToFileURL(html).href,
                        path.join(output, name),
                    ],
                    { stdio: "pipe" },
                );
                console.log(`captured ${name}`);

                if (held !== null) {
                    sendSgr(0, held.column + 1, held.row, "m");
                    await sleep(150);
                }

                if (state.actions !== undefined) {
                    send("Escape");
                    await sleep(200);
                }
            }
            } catch (error) {
                console.error(`failed ${entry.page}: ${error.message}`);
                failures.push(entry.page);
                send("Escape");
                await sleep(200);
            }
        }

        if (failures.length > 0) {
            console.error(`${failures.length} failed: ${failures.join(", ")}`);
            process.exitCode = 1;
        }
    } finally {
        try {
            tmux("kill-session", "-t", session);
        } catch {
            // The session is already gone when the showcase exited first.
        }

        rmSync(temporary, { recursive: true, force: true });
    }
};

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
    await main();
}
