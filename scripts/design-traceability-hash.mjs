import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";

export const sha256Bytes = (file) => createHash("sha256")
  .update(readFileSync(file))
  .digest("hex");

export const sha256Text = (file) => createHash("sha256")
  .update(readFileSync(file, "utf8").replaceAll("\r\n", "\n"))
  .digest("hex");
