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
