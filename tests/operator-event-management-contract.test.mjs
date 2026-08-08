import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const app = path.join(root, "src", "FotoHavn.App");
const controls = path.join(app, "Controls");

const operatorComponents = [
  ["AppHeader", "FotoHavn.AppHeader"],
  ["EventCard", "FotoHavn.EventCard"],
  ["SetupFieldGroup", "FotoHavn.SetupFieldGroup"],
  ["CameraViewport", "FotoHavn.CameraViewport"],
];

test("operator surfaces are composed from the four approved reusable families", () => {
  const mainWindow = readFileSync(path.join(app, "MainWindow.xaml"), "utf8");

  for (const [component, automationPrefix] of operatorComponents) {
    const xamlPath = path.join(controls, `${component}.xaml`);
    const codePath = `${xamlPath}.cs`;
    assert.equal(existsSync(xamlPath), true, `${component} XAML is missing`);
    assert.equal(existsSync(codePath), true, `${component} behavior is missing`);
    assert.match(mainWindow, new RegExp(`controls:${component}\\b`), `${component} is not used by production`);
    assert.match(
      `${readFileSync(xamlPath, "utf8")}\n${readFileSync(codePath, "utf8")}`,
      new RegExp(automationPrefix.replaceAll(".", "\\.")),
      `${component} has no stable Automation ID convention`,
    );
  }
});

test("Event cards and consequential dialogs expose complete disambiguating Event identity", () => {
  const eventCard = readFileSync(path.join(controls, "EventCard.xaml.cs"), "utf8");
  const mainWindow = readFileSync(path.join(app, "MainWindow.xaml"), "utf8");
  const behavior = readFileSync(path.join(app, "MainWindow.xaml.cs"), "utf8");

  assert.match(eventCard, /CompactEventId/);
  assert.match(eventCard, /AccessibleEventId/);
  assert.match(mainWindow, /EventDeletionIdentityText/);
  assert.match(mainWindow, /StartEventIdentityText/);
  assert.match(behavior, /deletion\?\.EventId\.Value/);
  assert.match(behavior, /StartEventConfirmation\?\.EventId\.Value/);
});

test("setup feedback is field-owned, successful fields stay quiet, and storage states state the one-gigabyte rule", () => {
  const setupGroup = readFileSync(path.join(controls, "SetupFieldGroup.xaml.cs"), "utf8");
  const mainWindow = readFileSync(path.join(app, "MainWindow.xaml"), "utf8");
  const behavior = readFileSync(path.join(app, "MainWindow.xaml.cs"), "utf8");

  assert.match(setupGroup, /SetupFieldState\.Ready/);
  assert.match(setupGroup, /StatusHost\.Visibility\s*=\s*Visibility\.Collapsed/);
  assert.match(mainWindow, /C:\\Program Files\\FotoHAVN\\Events/);
  assert.match(mainWindow, /1 GB free/);
  assert.match(behavior, /CameraConnectionState\.Connecting/);
  assert.match(behavior, /SetupFieldState\.Invalid/);
  assert.match(behavior, /Enter an Event name/);
});

test("operator surfaces reflow instead of preserving a fixed 1280 by 720 canvas", () => {
  const mainWindow = readFileSync(path.join(app, "MainWindow.xaml"), "utf8");
  const behavior = readFileSync(path.join(app, "MainWindow.xaml.cs"), "utf8");
  const eventCard = readFileSync(path.join(controls, "EventCard.xaml.cs"), "utf8");

  assert.doesNotMatch(mainWindow, /x:Name="FixedCanvas"[\s\S]{0,160}Width="1280"/);
  assert.match(mainWindow, /SizeChanged="OperatorCanvasSizeChanged"/);
  assert.match(mainWindow, /x:Name="SetupScrollViewer"/);
  assert.match(behavior, /ResponsiveLayoutMode\.Stress/);
  assert.match(behavior, /MaximumRowsOrColumns/);
  assert.match(eventCard, /Height\s*=\s*stress\s*\?\s*168\s*:\s*256/);
  assert.match(eventCard, /CardActionsColumn/);
  assert.match(
    mainWindow,
    /x:Name="SetupScrollViewer"[\s\S]*<\/ScrollViewer>[\s\S]*x:Name="SetupFooter"/,
    "setup header and footer must remain outside the scrolling body",
  );
});

test("all operator confirmations share the responsive modal shell", () => {
  const mainWindow = readFileSync(path.join(app, "MainWindow.xaml"), "utf8");
  const modalFrame = readFileSync(path.join(controls, "ConfirmationDialogFrame.cs"), "utf8");
  const matches = mainWindow.match(/<controls:ConfirmationDialogFrame(?:\s|>)/g) ?? [];
  assert.equal(matches.length, 5);
  assert.match(modalFrame, /previousFocus/);
  assert.match(modalFrame, /AccessibilityView\.Raw/);
  assert.match(modalFrame, /VirtualKey\.Tab/);
  assert.match(modalFrame, /VirtualKey\.Escape/);
});

test("the verification host pins this workstation as the canonical evidence environment", () => {
  const pinned = JSON.parse(readFileSync(path.join(
    root,
    "tools",
    "FotoHavn.UiVerificationHost",
    "pinned-environment.json",
  ), "utf8"));

  assert.deepEqual(pinned, {
    windowsBuild: 26200,
    osArchitecture: "X64",
    processArchitecture: "X64",
    dotnetSdk: "10.0.302",
    culture: "en-PH",
    uiCulture: "en-US",
    dpi: 120,
    theme: "Light",
    fontSmoothing: "ClearType",
  });
});

test("batch 3 owns exactly 48 canonical and responsive fixtures", () => {
  const scenarioFiles = ["saved-events", "event-setup", "confirmation"];
  const fixtureIds = scenarioFiles.flatMap((surface) => {
    const catalog = JSON.parse(readFileSync(path.join(
      root,
      "docs",
      "design-system",
      "traceability",
      "scenarios",
      `${surface}.json`,
    ), "utf8"));
    return catalog.scenarios.flatMap(({ batch, visualFixtures, responsiveViewportCases }) => {
      assert.equal(batch, 3);
      return [...visualFixtures, ...responsiveViewportCases];
    });
  });

  assert.equal(fixtureIds.length, 48);
  assert.equal(new Set(fixtureIds).size, 48);
});
