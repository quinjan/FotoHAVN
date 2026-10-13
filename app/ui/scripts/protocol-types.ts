import { readFile } from "node:fs/promises";
import path from "node:path";
import { compile, type JSONSchema } from "json-schema-to-typescript";

/** The schema exported from FotoHavn.Protocol (the source of truth) and the TypeScript generated from it. */
export const schemaPath = path.resolve(import.meta.dirname, "../../src/FotoHavn.Protocol/schema/protocol.schema.json");
export const generatedPath = path.resolve(import.meta.dirname, "../src/protocol/generated.ts");

const banner = `/*
 * generated — do not edit.
 * Generated from app/src/FotoHavn.Protocol/schema/protocol.schema.json by json-schema-to-typescript.
 * Change the C# records in FotoHavn.Protocol, then run app/scripts/generate-protocol.ps1.
 */`;

/** Generates the UI's protocol types from the schema text, exactly as committed in src/protocol/generated.ts. */
export async function generateProtocolTypes(schemaText: string): Promise<string> {
  const schema = JSON.parse(schemaText) as JSONSchema & { properties: { protocolVersion: { const: number } } };
  const version = schema.properties.protocolVersion.const;
  const types = await compile(schema, "FotoHavnProtocol", {
    bannerComment: banner,
    additionalProperties: false,
    unreachableDefinitions: true,
    strictIndexSignatures: true,
    unknownAny: true,
    format: true,
    style: { printWidth: 120, singleQuote: false, trailingComma: "all", bracketSpacing: true, endOfLine: "lf" },
  });
  return `${types}
/** The protocol version this UI build speaks; it sends it in \`hello\`. */
export const PROTOCOL_VERSION = ${String(version)};
`;
}

/** Reads the committed schema and generates the types from it. */
export async function generateFromCommittedSchema(): Promise<string> {
  return generateProtocolTypes(await readFile(schemaPath, "utf8"));
}
