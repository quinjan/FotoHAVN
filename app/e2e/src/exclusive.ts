import { mkdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import path from "node:path";
import { paths } from "./paths";

/**
 * Tags for a test that needs a resource only one test may use at a time:
 * `@camera` opens the real camera, `@cursor` moves or depends on the OS cursor or the real screens,
 * `@build` runs `dotnet build`/`dotnet publish` or a nested Playwright run. Every other test is parallel-safe.
 */
export const exclusiveTags = ["@camera", "@cursor", "@build"] as const;

/** The pattern the config uses to split the exclusive pool from the parallel pool. */
export const exclusivePattern = new RegExp(exclusiveTags.join("|"));

export function isExclusive(tags: readonly string[]): boolean {
  return tags.some((tag) => (exclusiveTags as readonly string[]).includes(tag));
}

const lockDir = path.join(paths.e2eOutput, "exclusive.lock");
const ownerFile = path.join(lockDir, "owner.json");
const pollMs = 500;
const maxWaitMs = 30 * 60_000;
/** A lock folder with no owner file this old was abandoned between creating it and writing the owner. */
const ownerlessGraceMs = 10_000;

interface Owner {
  pid: number;
  label: string;
}

/**
 * A machine-wide mutex (a lock folder in the run's output folder) that keeps exclusive tests of every worker, and of
 * both the smoke and regression projects, from running at the same time. A worker holds it for the exclusive test and
 * for the app it leaves running afterwards (the camera stays open until that app closes), counted by holder name.
 * A lock whose owner process is gone is taken over.
 */
export class ExclusiveLock {
  private readonly holders = new Set<string>();
  private held = false;

  async acquire(holder: string, label: string): Promise<void> {
    this.holders.add(holder);
    if (this.held) {
      return;
    }
    const startedAt = Date.now();
    while (!this.tryLock(label)) {
      if (Date.now() - startedAt > maxWaitMs) {
        this.holders.delete(holder);
        throw new Error(`Waited ${maxWaitMs / 60_000} minutes for the exclusive resource lock held by ${describeOwner()}.`);
      }
      if (isStale()) {
        rmSync(lockDir, { recursive: true, force: true });
        continue;
      }
      await new Promise((resolve) => setTimeout(resolve, pollMs));
    }
    this.held = true;
  }

  release(holder: string): void {
    this.holders.delete(holder);
    if (this.held && this.holders.size === 0) {
      this.held = false;
      if (readOwner()?.pid === process.pid) {
        rmSync(lockDir, { recursive: true, force: true });
      }
    }
  }

  private tryLock(label: string): boolean {
    mkdirSync(paths.e2eOutput, { recursive: true });
    try {
      mkdirSync(lockDir);
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === "EEXIST") {
        return false;
      }
      throw error;
    }
    const owner: Owner = { pid: process.pid, label };
    writeFileSync(ownerFile, JSON.stringify(owner));
    return true;
  }
}

function readOwner(): Owner | undefined {
  try {
    return JSON.parse(readFileSync(ownerFile, "utf8")) as Owner;
  } catch {
    return undefined;
  }
}

function describeOwner(): string {
  const owner = readOwner();
  return owner === undefined ? "an unknown worker" : `"${owner.label}" (PID ${owner.pid})`;
}

function isStale(): boolean {
  const owner = readOwner();
  if (owner === undefined) {
    try {
      return Date.now() - statSync(lockDir).mtimeMs > ownerlessGraceMs;
    } catch {
      return false;
    }
  }
  try {
    process.kill(owner.pid, 0);
    return false;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code !== "EPERM";
  }
}
