import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { TestInfo } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { paths } from "../../../src/paths";

const guardText = "FOTOHAVN field builds must not include E2E hooks";
const debug = ["-c", "Debug"];

interface DotnetResult {
  status: number | null;
  output: string;
}

function dotnet(args: string[]): DotnetResult {
  const result = spawnSync("dotnet", args, { cwd: paths.appDir, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
  expect(result.error, "dotnet must be on PATH").toBeUndefined();
  return { status: result.status, output: `> dotnet ${args.join(" ")}\n${result.stdout}\n${result.stderr}` };
}

function expectSuccess(result: DotnetResult): void {
  expect(result.status, result.output).toBe(0);
}

function expectRefused(result: DotnetResult): void {
  expect(result.status, result.output).not.toBe(0);
  expect(result.output).toContain(guardText);
}

/** The serialized value of [assembly: AssemblyMetadata("FotoHavnE2E", "true")]: prolog, two SerStrings, no named arguments. */
const e2eMarkerBlob = Buffer.concat([
  Buffer.from([0x01, 0x00, 0x0b]),
  Buffer.from("FotoHavnE2E", "utf8"),
  Buffer.from([0x04]),
  Buffer.from("true", "utf8"),
  Buffer.from([0x00, 0x00]),
]);
/** A namespace ending in ".E2E", as stored null-terminated in the #Strings heap. */
const e2eNamespace = Buffer.from(".E2E\0", "utf8");

/** FOTOHAVN.dll and FotoHavn.*.dll in a publish folder that were compiled with the E2E hooks. */
function hookAssemblies(publishDir: string): string[] {
  return readdirSync(publishDir)
    .filter((name) => name === "FOTOHAVN.dll" || (name.startsWith("FotoHavn.") && name.endsWith(".dll")))
    .filter((name) => {
      const bytes = readFileSync(path.join(publishDir, name));
      return bytes.includes(e2eMarkerBlob) || bytes.includes(e2eNamespace);
    });
}

function outputDir(testInfo: TestInfo, name: string): string {
  const dir = testInfo.outputPath(name);
  rmSync(dir, { recursive: true, force: true });
  return dir;
}

test.describe("publish guard", { tag: ["@m1", "@build"] }, () => {
  test.setTimeout(10 * 60_000);

  test("publishing with FotoHavnE2E=true is refused by the publish guard", () => {
    expectRefused(dotnet(["publish", paths.hostProject, "-c", "Release", "-p:FotoHavnE2E=true"]));
  });

  test("publishing with DefineConstants=E2E_HOOKS is refused by the publish guard", () => {
    expectRefused(dotnet(["publish", paths.hostProject, "-c", "Release", "-p:DefineConstants=E2E_HOOKS"]));
  });

  test("a publish without a build after an E2E build ships no hooks, and one reaching E2E assemblies is refused", async (
    {},
    testInfo,
  ) => {
    const e2eBuild = outputDir(testInfo, "e2e-build");
    const published = outputDir(testInfo, "published");
    const bypass = outputDir(testInfo, "bypass");

    await test.step("a normal Debug build, then the E2E build (the order that once leaked hooks)", () => {
      expectSuccess(dotnet(["build", paths.hostProject, ...debug]));
      // Its own output folder, so the build never touches the output/e2e-build an app may still be running from.
      expectSuccess(dotnet(["build", paths.hostProject, ...debug, "-p:FotoHavnE2E=true", "-o", e2eBuild]));
      expect(hookAssemblies(e2eBuild), "the E2E build itself must carry the hooks").toContain("FOTOHAVN.dll");
    });

    await test.step("dotnet publish --no-build without the flag publishes the field build", () => {
      expectSuccess(dotnet(["publish", paths.hostProject, ...debug, "--no-build", "-o", published]));
      expect(existsSync(path.join(published, "FOTOHAVN.dll"))).toBe(true);
      expect(hookAssemblies(published)).toEqual([]);
      expect(existsSync(path.join(published, "wwwroot", "e2e-build.txt"))).toBe(false);
    });

    await test.step("a publish pointed at the E2E intermediate folder is refused by the assembly check", () => {
      const result = dotnet([
        "publish",
        paths.hostProject,
        ...debug,
        "--no-build",
        "-p:BaseIntermediateOutputPath=obj\\e2e\\",
        "-o",
        bypass,
      ]);
      expectRefused(result);
      expect(result.output).toContain("FOTOHAVN0002");
    });

    rmSync(e2eBuild, { recursive: true, force: true });
    rmSync(published, { recursive: true, force: true });
    rmSync(bypass, { recursive: true, force: true });
  });

  test("publishing an E2E UI build is refused", ({}, testInfo) => {
    const ui = outputDir(testInfo, "ui-dist-e2e");
    mkdirSync(ui, { recursive: true });
    writeFileSync(path.join(ui, "index.html"), "<!doctype html><title>FOTOHAVN</title>\n");
    writeFileSync(path.join(ui, "e2e-build.txt"), "E2E UI build\n");
    const result = dotnet([
      "publish",
      paths.hostProject,
      ...debug,
      "--no-build",
      `-p:FotoHavnUiDist=${ui}`,
      "-o",
      outputDir(testInfo, "published"),
    ]);
    expectRefused(result);
    expect(result.output).toContain("FOTOHAVN0003");
  });
});
