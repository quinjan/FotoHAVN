import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const guardedExitSource = readFileSync(
  path.join(repositoryRoot, "src", "FotoHavn.App", "Controls", "GuardedExitAction.xaml.cs"),
  "utf8",
);
const motionPolicySource = readFileSync(
  path.join(repositoryRoot, "src", "FotoHavn.App", "Controls", "MotionPolicy.cs"),
  "utf8",
);

test("completed pointer holds release capture before replacing the active surface", () => {
  const tickStart = guardedExitSource.indexOf("private void HoldTimerTick");
  const completionStart = guardedExitSource.indexOf("HoldCompleted?.Invoke", tickStart);
  const releaseStart = guardedExitSource.indexOf("HoldButton.ReleasePointerCaptures()", tickStart);

  assert.notEqual(tickStart, -1, "guarded hold timer handler is missing");
  assert.notEqual(completionStart, -1, "guarded hold completion callback is missing");
  assert.ok(
    releaseStart > tickStart && releaseStart < completionStart,
    "pointer capture must be released before HoldCompleted can collapse the captured control",
  );
});

test("input-time motion lookup uses app-resolved resources without constructing UISettings", () => {
  assert.doesNotMatch(motionPolicySource, /new\s+UISettings\s*\(/);
  assert.doesNotMatch(motionPolicySource, /Windows\.UI\.ViewManagement/);
  assert.match(motionPolicySource, /FotoHavnSlowMotionDuration/);
});

test("the holding indicator remains reusable after an Event exit cycle", () => {
  const showBusyStart = guardedExitSource.indexOf("public void ShowBusy");
  const pointerPressedStart = guardedExitSource.indexOf(
    "private void HoldButtonPointerPressed",
    showBusyStart,
  );
  const showBusySource = guardedExitSource.slice(showBusyStart, pointerPressedStart);

  assert.notEqual(showBusyStart, -1, "guarded exit busy state is missing");
  assert.notEqual(pointerPressedStart, -1, "guarded exit pointer handler is missing");
  assert.match(showBusySource, /HoldingIndicatorHost\.Visibility = Visibility\.Collapsed/);
  assert.doesNotMatch(
    showBusySource,
    /HoldingIndicator\.Visibility = Visibility\.Collapsed/,
    "exiting an Event must not permanently collapse the reusable spinner glyph",
  );
});

test("the destructive Exit Event action keeps its visible idle surface", () => {
  const applyStart = guardedExitSource.indexOf("private void Apply");
  const announceStart = guardedExitSource.indexOf("private void Announce", applyStart);
  const applySource = guardedExitSource.slice(applyStart, announceStart);

  assert.notEqual(applyStart, -1, "guarded exit visual-state application is missing");
  assert.notEqual(announceStart, -1, "guarded exit announcement boundary is missing");
  assert.doesNotMatch(
    applySource,
    /HoldButton\.Background\s*=\s*holding\s*\?[\s\S]*?Colors\.Transparent/,
    "idle state must not replace the destructive style background with a transparent local value",
  );
  assert.match(applySource, /HoldButton\.ClearValue\(Control\.BorderBrushProperty\)/);
  assert.match(applySource, /HoldButton\.ClearValue\(Control\.BackgroundProperty\)/);
});
