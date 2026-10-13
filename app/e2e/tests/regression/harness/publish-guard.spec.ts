import { spawnSync } from "node:child_process";
import { expect, test } from "@playwright/test";
import { paths } from "../../../src/paths";

const guardText = "FOTOHAVN field builds must not include E2E hooks";

test("publishing with FotoHavnE2E=true is refused by the publish guard", { tag: ["@m1"] }, () => {
  test.setTimeout(10 * 60_000);
  const result = spawnSync(
    "dotnet",
    ["publish", paths.hostProject, "-c", "Release", "-p:FotoHavnE2E=true"],
    { cwd: paths.appDir, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 },
  );
  expect(result.error, "dotnet must be on PATH").toBeUndefined();
  const output = `${result.stdout}\n${result.stderr}`;
  expect(result.status, output).not.toBe(0);
  expect(output).toContain(guardText);
});
