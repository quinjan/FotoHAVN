import assert from "node:assert/strict";
import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const validator = path.join(repositoryRoot, "scripts", "validate-design-traceability.mjs");
const sourceHandoff = path.join(repositoryRoot, "docs", "design-system", "traceability");

function validate(handoffRoot = sourceHandoff) {
  return spawnSync(process.execPath, [validator, "--handoff-root", handoffRoot], {
    cwd: repositoryRoot,
    encoding: "utf8",
  });
}

function withHandoffCopy(mutate, assertion) {
  const temporaryRoot = mkdtempSync(path.join(tmpdir(), "fotohavn-traceability-"));
  const handoffRoot = path.join(temporaryRoot, "traceability");
  try {
    cpSync(sourceHandoff, handoffRoot, { recursive: true });
    mutate(handoffRoot);
    assertion(validate(handoffRoot));
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true });
  }
}

function mutateJson(handoffRoot, relativePath, mutate) {
  const target = path.join(handoffRoot, relativePath);
  const document = JSON.parse(readFileSync(target, "utf8"));
  mutate(document);
  writeFileSync(target, `${JSON.stringify(document, null, 2)}\n`);
}

test("the approved design-v1.0.1 traceability handoff validates", () => {
  const result = validate();
  assert.equal(result.status, 0, result.stderr || result.stdout);
  assert.match(result.stdout, /91 tokens, 17 components, 8 surfaces, 71 canonical states, and 32 responsive-risk frames/);
});

test("validation rejects a missing canonical-state mapping", () => {
  withHandoffCopy(
    (root) => mutateJson(root, "mapping.json", (mapping) => mapping.canonicalStates.pop()),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /missing canonical-state mapping/i);
    },
  );
});

test("validation rejects a duplicate semantic-token mapping", () => {
  withHandoffCopy(
    (root) => mutateJson(root, "mapping.json", (mapping) => mapping.semanticTokens.push(mapping.semanticTokens[0])),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /duplicate semantic-token mapping/i);
    },
  );
});

test("validation rejects dangling evidence identifiers", () => {
  withHandoffCopy(
    (root) => mutateJson(root, "mapping.json", (mapping) => mapping.surfaces[0].manualChecks.push("MANUAL-NOT-DEFINED")),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /dangling evidence identifier.*MANUAL-NOT-DEFINED/i);
    },
  );
});

test("validation rejects an extra responsive-risk mapping", () => {
  withHandoffCopy(
    (root) => mutateJson(root, "mapping.json", (mapping) => mapping.responsiveRiskFrames.push({ ...mapping.responsiveRiskFrames[0], registryFrame: "extra.frame" })),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /extra responsive-risk mapping/i);
    },
  );
});

test("validation rejects an incomplete semantic-token mapping", () => {
  withHandoffCopy(
    (root) => mutateJson(root, "mapping.json", (mapping) => delete mapping.semanticTokens[0].xamlResourceKey),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /semantic-token field mismatch.*xamlResourceKey/i);
    },
  );
});

test("validation rejects an extra evidence record", () => {
  withHandoffCopy(
    (root) => mutateJson(root, "evidence-index.json", (index) => index.evidence.push({ id: "UNUSED-EVIDENCE", kind: "named-manual-procedure", location: "manual-procedures.md", procedure: "MANUAL-NARRATOR-JOURNEY" })),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /extra evidence record.*UNUSED-EVIDENCE/i);
    },
  );
});

test("validation rejects a responsive frame detached from its owning scenario", () => {
  withHandoffCopy(
    (root) => {
      const mapping = JSON.parse(readFileSync(path.join(root, "mapping.json"), "utf8"));
      const owner = mapping.responsiveRiskFrames[0].owningResponsiveCase;
      const [, surface, state] = owner.split(".");
      mutateJson(root, path.join("scenarios", `${surface}.json`), (catalog) => {
        catalog.scenarios.find((scenario) => scenario.state === state).responsiveViewportCases = [];
      });
    },
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /missing responsive viewport case/i);
    },
  );
});

test("validation rejects a manual procedure with a dangling location", () => {
  withHandoffCopy(
    (root) => mutateJson(root, "evidence-index.json", (index) => {
      index.evidence.find((item) => item.kind === "named-manual-procedure").location = "missing-procedures.md";
    }),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /manual procedure location does not resolve/i);
    },
  );
});

test("validation rejects a scenario with a dangling design reference", () => {
  withHandoffCopy(
    (root) => mutateJson(root, path.join("scenarios", "saved-events.json"), (catalog) => {
      catalog.scenarios[0].patternReferences[0] = "patterns/not-a-contract.md";
    }),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /scenario pattern reference does not resolve/i);
    },
  );
});

test("validation rejects a manifest missing a required artifact anchor", () => {
  withHandoffCopy(
    (root) => mutateJson(root, "manifest.json", (manifest) => {
      manifest.artifacts = manifest.artifacts.filter((artifact) => artifact.path !== "mapping.json");
    }),
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /missing manifest artifact mapping.*mapping\.json/i);
    },
  );
});
