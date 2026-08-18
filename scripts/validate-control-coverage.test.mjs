import assert from "node:assert/strict";
import test from "node:test";

import {
  assertControlCoverage,
  parseControlCoverage,
} from "./validate-control-coverage.mjs";

const report = (...classes) => `
  <coverage>
    <packages>
      <package>
        <classes>${classes.join("")}</classes>
      </package>
    </packages>
  </coverage>`;

const fixture = (name, filename, lines) => `
  <class name="${name}" filename="${filename}">
    <lines>${lines
      .map(
        ([number, hits]) =>
          `<line number="${number}" hits="${hits}" branch="false" />`,
      )
      .join("")}</lines>
  </class>`;

test("parseControlCoverage_WhenReportMixesSources_CountsOnlyControlLines", () => {
  const xml = report(
    fixture("SharpVision.Controls.Input.Button", "src/SharpVision/Controls/Input/Button.cs", [
      [1, 1],
      [2, 3],
      [3, 0],
    ]),
    fixture("SharpVision.Menus.Menu", "src/SharpVision/Menus/Menu.cs", [[1, 1]]),
    fixture("SharpVision.Application", "src/SharpVision/Application.cs", [
      [1, 0],
      [2, 0],
    ]),
  );

  const coverage = parseControlCoverage(xml);

  assert.deepEqual(coverage, { covered: 3, total: 4, rate: 3 / 4 });
});

test("parseControlCoverage_WhenPathsUseBackslashes_NormalizesSourcePath", () => {
  const xml = report(
    fixture(
      "SharpVision.Windows.Window",
      String.raw`C:\repo\src\SharpVision\Windows\Window.cs`,
      [[1, 1]],
    ),
  );

  assert.equal(parseControlCoverage(xml).rate, 1);
});

test("parseControlCoverage_WhenSharedControlInfrastructureMoved_CountsRelocatedLines", () => {
  const xml = report(
    fixture("SharpVision.Input.AccessKeyText", "src/SharpVision/Input/AccessKeyText.cs", [[1, 1]]),
    fixture("SharpVision.Layout.LayoutMath", "src/SharpVision/Layout/LayoutMath.cs", [[1, 0]]),
    fixture(
      "SharpVision.Styling.ControlChrome",
      "src/SharpVision/Styling/ControlChrome.cs",
      [[1, 1]],
    ),
    fixture("SharpVision.Input.FocusManager", "src/SharpVision/Input/FocusManager.cs", [[1, 0]]),
  );

  assert.deepEqual(parseControlCoverage(xml), {
    covered: 2,
    total: 3,
    rate: 2 / 3,
  });
});

test("parseControlCoverage_WhenMethodsRepeatLines_CountsClassLinesOnce", () => {
  const xml = report(`
    <class name="SharpVision.Popups.Popup" filename="src/SharpVision/Popups/Popup.cs">
      <methods>
        <method name="Click"><lines><line number="1" hits="1" /></lines></method>
      </methods>
      <lines>
        <line number="1" hits="1" />
        <line number="2" hits="0" />
      </lines>
    </class>`);

  assert.deepEqual(parseControlCoverage(xml), {
    covered: 1,
    total: 2,
    rate: 0.5,
  });
});

test("assertControlCoverage_WhenRateMeetsMinimum_ReturnsMeasuredCoverage", () => {
  const xml = report(
    fixture("SharpVision.Dialogs.MessageBox", "src/SharpVision/Dialogs/MessageBox.cs", [
      [1, 1],
      [2, 0],
    ]),
  );

  assert.deepEqual(assertControlCoverage(xml, 0.5), {
    covered: 1,
    total: 2,
    rate: 0.5,
  });
});

test("assertControlCoverage_WhenRateIsBelowMinimum_ReportsCountsAndThreshold", () => {
  const xml = report(
    fixture("SharpVision.Navigation.NavigationView", "src/SharpVision/Navigation/NavigationView.cs", [
      [1, 1],
      [2, 0],
    ]),
  );

  assert.throws(
    () => assertControlCoverage(xml, 0.9),
    /Control line coverage 50\.00% \(1\/2\) is below 90\.00%/,
  );
});

test("parseControlCoverage_WhenReportHasNoControlLines_RejectsReport", () => {
  const xml = report(
    fixture("SharpVision.Application", "src/SharpVision/Application.cs", [[1, 1]]),
  );

  assert.throws(
    () => parseControlCoverage(xml),
    /does not contain instrumented SharpVision UI control lines/,
  );
});

test("parseControlCoverage_WhenXmlIsMalformed_RejectsReport", () => {
  assert.throws(
    () => parseControlCoverage("not coverage xml"),
    /does not contain Cobertura class entries/,
  );
});
