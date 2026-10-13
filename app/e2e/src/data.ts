import { existsSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import { DatabaseSync, type SQLInputValue } from "node:sqlite";

/** The Event database file inside a Data folder (created from M4 on). */
export const databaseFileName = "fotohavn.db";

/** Folders the host owns that are large and not part of Event data. */
const listingSkips = new Set(["webview2-user-data"]);

/**
 * Opens a SQLite database read-only. Tests never write Event data; the Event store is its only writer.
 * The caller closes it.
 */
export function openDatabase(file: string): DatabaseSync {
  if (!existsSync(file)) {
    throw new Error(`Expected a SQLite database at ${file}, but it does not exist.`);
  }
  return new DatabaseSync(file, { readOnly: true });
}

export function openDataDatabase(dataDir: string): DatabaseSync {
  return openDatabase(path.join(dataDir, databaseFileName));
}

/** Runs one read-only query and returns every row. */
export function queryAll<T = Record<string, unknown>>(file: string, sql: string, ...params: SQLInputValue[]): T[] {
  const db = openDatabase(file);
  try {
    return db.prepare(sql).all(...params) as T[];
  } finally {
    db.close();
  }
}

/** Runs one read-only query and returns the first row, or undefined. */
export function queryOne<T = Record<string, unknown>>(
  file: string,
  sql: string,
  ...params: SQLInputValue[]
): T | undefined {
  const db = openDatabase(file);
  try {
    return db.prepare(sql).get(...params) as T | undefined;
  } finally {
    db.close();
  }
}

export function assertDirectory(dir: string): void {
  if (!existsSync(dir) || !statSync(dir).isDirectory()) {
    throw new Error(`Expected a folder at ${dir}.`);
  }
}

export function assertFile(file: string, options: { minBytes?: number } = {}): void {
  if (!existsSync(file) || !statSync(file).isFile()) {
    throw new Error(`Expected a file at ${file}.`);
  }
  const minBytes = options.minBytes ?? 0;
  const size = statSync(file).size;
  if (size < minBytes) {
    throw new Error(`Expected ${file} to be at least ${minBytes} bytes, but it is ${size}.`);
  }
}

export interface DataEntry {
  /** Path relative to the Data folder, with forward slashes; folders end with `/`. */
  path: string;
  bytes: number;
}

/** Lists a Data folder recursively, skipping the contents of host-owned browser data. */
export function listDataFolder(dataDir: string): DataEntry[] {
  const entries: DataEntry[] = [];
  if (!existsSync(dataDir)) {
    return entries;
  }
  const walk = (dir: string, prefix: string): void => {
    for (const dirent of readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
      const relative = prefix === "" ? dirent.name : `${prefix}/${dirent.name}`;
      const full = path.join(dir, dirent.name);
      if (dirent.isDirectory()) {
        entries.push({ path: `${relative}/`, bytes: 0 });
        if (!listingSkips.has(relative)) {
          walk(full, relative);
        }
      } else {
        entries.push({ path: relative, bytes: statSync(full).size });
      }
    }
  };
  walk(dataDir, "");
  return entries;
}
