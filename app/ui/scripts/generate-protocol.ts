import { writeFile } from "node:fs/promises";
import { generatedPath, generateFromCommittedSchema } from "./protocol-types.ts";

// Writes src/protocol/generated.ts from the committed schema. Run through app/scripts/generate-protocol.ps1, which
// exports the schema from the C# records first.
await writeFile(generatedPath, await generateFromCommittedSchema(), "utf8");
console.log(`Wrote ${generatedPath}`);
