import { existsSync, readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parseAnnotation, parseMatrix, parseWinuiMappingSource, responsiveOwnerState } from "./design-traceability-source.mjs";
import { sha256Bytes, sha256Text } from "./design-traceability-hash.mjs";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const handoffArgument = process.argv.indexOf("--handoff-root");
const handoffRoot = handoffArgument >= 0 ? path.resolve(process.argv[handoffArgument + 1]) : path.join(repositoryRoot, "docs", "design-system", "traceability");
const designRoot = path.join(repositoryRoot, "docs", "design-system");
const referenceRoot = path.join(designRoot, "reference-states");
const errors = [];
const read = (file) => readFileSync(file, "utf8");
const load = (file) => JSON.parse(read(file));
const report = (condition, message) => { if (!condition) errors.push(message); };

function duplicateIds(records, key, label) {
  const seen = new Set();
  for (const record of records) {
    const id = record[key];
    if (seen.has(id)) errors.push(`duplicate ${label} mapping: ${id}`);
    seen.add(id);
  }
}

function compareExact(actual, expected, label) {
  const actualSet = new Set(actual);
  const expectedSet = new Set(expected);
  for (const id of expectedSet) if (!actualSet.has(id)) errors.push(`missing ${label} mapping: ${id}`);
  for (const id of actualSet) if (!expectedSet.has(id)) errors.push(`extra ${label} mapping: ${id}`);
}

function compareFields(actualRecords, expectedRecords, key, label) {
  const actualById = new Map(actualRecords.map((record) => [record[key], record]));
  for (const expected of expectedRecords) {
    const actual = actualById.get(expected[key]);
    if (!actual) continue;
    for (const [fieldName, expectedValue] of Object.entries(expected)) {
      if (JSON.stringify(actual[fieldName]) !== JSON.stringify(expectedValue))
        errors.push(`${label} field mismatch for ${expected[key]}: ${fieldName}`);
    }
  }
}

const pascal = (value) => value.split("-").map((part) => part[0].toUpperCase() + part.slice(1)).join("");
const canonicalReferencePath = (location) => location.startsWith("../reference-states/")
  ? path.join(referenceRoot, location.slice("../reference-states/".length))
  : path.resolve(handoffRoot, location);
const batchFor = (surface) => ["saved-events", "event-setup", "confirmation"].includes(surface) ? 3 :
  ["guest-start", "guest-start-unavailable"].includes(surface) ? 4 : 5;
const isStringArray = (value) => Array.isArray(value) && value.every((item) => typeof item === "string");

function collectEvidenceReferences(value, key = "") {
  const referenceKeys = new Set([
    "verificationIdentifiers", "sharedVerificationIdentifiers", "manualChecks", "acceptanceScenario",
    "uiAutomationContract", "canonicalVisualFixture", "deterministicInjectionIdentity", "registryFrame",
    "owningResponsiveCase", "uiAutomationGeometryCheck", "visualFixture", "visualFixtures", "responsiveViewportCases",
    "applicationAcceptanceChecks", "uiAutomationChecks", "sharedPatternChecks",
  ]);
  if (referenceKeys.has(key)) return Array.isArray(value) ? value : [value];
  if (Array.isArray(value)) return value.flatMap((item) => collectEvidenceReferences(item));
  if (value && typeof value === "object") return Object.entries(value).flatMap(([childKey, child]) => collectEvidenceReferences(child, childKey));
  return [];
}

try {
  const mapping = load(path.join(handoffRoot, "mapping.json"));
  const evidenceIndex = load(path.join(handoffRoot, "evidence-index.json"));
  const waivers = load(path.join(handoffRoot, "waivers.json"));
  const manifest = load(path.join(handoffRoot, "manifest.json"));
  const registry = load(path.join(referenceRoot, "registry.json"));
  const matrix = parseMatrix(read(path.join(referenceRoot, "matrix.yaml")));
  const mappingSource = read(path.join(designRoot, "winui-mapping.yaml"));
  const expectedSourceMapping = parseWinuiMappingSource(mappingSource);
  const expectedTokens = expectedSourceMapping.semanticTokens;
  const expectedComponents = expectedSourceMapping.components;
  const expectedSurfaces = matrix.surfaces.map((surface) => ({
    semanticId: `surface.${surface.id}`,
    productionComposition: `FotoHavn.App.Surfaces.${pascal(surface.id)}Surface`,
    presentationSource: "FotoHavn.Core.ApplicationPresentation",
    semanticRootAutomationId: `FotoHavn.Surface.${pascal(surface.id)}`,
    verificationIdentifiers: ["UIA-SURFACE-STRUCTURE"],
    manualChecks: ["MANUAL-KEYBOARD-JOURNEY", "MANUAL-NARRATOR-JOURNEY"],
  }));
  const expectedCanonical = registry.frames.filter((frame) => frame.coverage === "canonical").map((frame) => ({
    identity: `${frame.surface}.${frame.state}`,
    deterministicInjectionIdentity: `injection.${frame.surface}.${frame.state}`,
    acceptanceScenario: `scenario.${frame.surface}.${frame.state}`,
    uiAutomationContract: `UIA-${frame.surface.toUpperCase()}-CONTRACT`,
    canonicalVisualFixture: frame.id,
    manualChecks: ["MANUAL-VISUAL-EQUIVALENCE", "MANUAL-NARRATOR-JOURNEY"],
  }));
  const expectedResponsive = registry.frames.filter((frame) => frame.coverage === "responsive-risk").map((frame) => ({
    registryFrame: frame.id,
    owningResponsiveCase: `scenario.${frame.surface}.${responsiveOwnerState[frame.surface]}`,
    uiAutomationGeometryCheck: "UIA-RESPONSIVE-GEOMETRY",
    visualFixture: frame.id,
    manualChecks: ["MANUAL-RESPONSIVE-REFLOW", "MANUAL-TOUCH-TARGETS"],
  }));

  report(mapping.schemaVersion === 2, "mapping schemaVersion must be 2");
  report(mapping.contractVersion === "1.0.1", "mapping contractVersion must be 1.0.1");
  report(mapping.semanticChange === false, "design-v1.0.1 must declare semanticChange false");
  report(mapping.inherits?.tag === "design-v1.0.0" && mapping.inherits?.commit === registry.contract.commit, "mapping must inherit the approved design-v1.0.0 anchor");

  duplicateIds(mapping.semanticTokens, "semanticId", "semantic-token");
  duplicateIds(mapping.components, "semanticId", "component");
  duplicateIds(mapping.surfaces, "semanticId", "surface");
  duplicateIds(mapping.canonicalStates, "identity", "canonical-state");
  duplicateIds(mapping.responsiveRiskFrames, "registryFrame", "responsive-risk");
  compareExact(mapping.semanticTokens.map((record) => record.semanticId), expectedTokens.map((record) => record.semanticId), "semantic-token");
  compareExact(mapping.components.map((record) => record.semanticId), expectedComponents.map((record) => record.semanticId), "component");
  compareExact(mapping.surfaces.map((record) => record.semanticId), expectedSurfaces.map((record) => record.semanticId), "surface");
  compareExact(mapping.canonicalStates.map((record) => record.identity), expectedCanonical.map((record) => record.identity), "canonical-state");
  compareExact(mapping.responsiveRiskFrames.map((record) => record.registryFrame), expectedResponsive.map((record) => record.registryFrame), "responsive-risk");
  compareFields(mapping.semanticTokens, expectedTokens, "semanticId", "semantic-token");
  compareFields(mapping.components, expectedComponents, "semanticId", "component");
  compareFields(mapping.surfaces, expectedSurfaces, "semanticId", "surface");
  compareFields(mapping.canonicalStates, expectedCanonical, "identity", "canonical-state");
  compareFields(mapping.responsiveRiskFrames, expectedResponsive, "registryFrame", "responsive-risk");

  report(mapping.semanticTokens.length === 91, `approved semantic-token cardinality is 91, found ${mapping.semanticTokens.length}`);
  report(mapping.components.length === 17, `approved component cardinality is 17, found ${mapping.components.length}`);
  report(mapping.surfaces.length === 8, `approved surface cardinality is 8, found ${mapping.surfaces.length}`);
  report(mapping.canonicalStates.length === 71, `approved canonical-state cardinality is 71, found ${mapping.canonicalStates.length}`);
  report(mapping.responsiveRiskFrames.length === 32, `approved responsive-risk cardinality is 32, found ${mapping.responsiveRiskFrames.length}`);
  report(registry.frames.length === 103 && registry.canonicalCount === 71 && registry.responsiveRiskCount === 32, "registry cardinality drifted from 103 / 71 / 32");

  const expectedRegistry = [
    ...matrix.surfaces.flatMap((surface) => surface.canonicalStates.map((state) => `${surface.id}.${state}.standard`)),
    ...matrix.surfaces.flatMap((surface) => matrix.viewports.filter((viewport) => viewport.coverage === "responsive-risk-per-surface").map((viewport) => `${surface.id}.${surface.responsiveRisk}.${viewport.id}`)),
  ];
  duplicateIds(registry.frames, "id", "registry-frame");
  compareExact(registry.frames.map((frame) => frame.id), expectedRegistry, "registry-frame");

  const catalogDirectory = path.join(handoffRoot, "scenarios");
  const catalogFiles = readdirSync(catalogDirectory).filter((file) => file.endsWith(".json")).sort();
  const catalogs = catalogFiles.map((file) => load(path.join(catalogDirectory, file)));
  const scenarios = catalogs.flatMap((catalog) => catalog.scenarios);
  duplicateIds(scenarios, "id", "scenario");
  compareExact(scenarios.map((scenario) => scenario.id), mapping.canonicalStates.map((state) => state.acceptanceScenario), "scenario");
  report(catalogs.length === 8 && scenarios.length === 71, `scenario catalogs must contain 8 surfaces and 71 scenarios, found ${catalogs.length} and ${scenarios.length}`);
  for (const catalog of catalogs) report(catalog.scenarioCount === catalog.scenarios.length, `scenarioCount mismatch in ${catalog.surface}`);
  const responsiveCases = scenarios.flatMap((scenario) => (scenario.responsiveViewportCases ?? []).map((frame) => ({ frame, scenario: scenario.id })));
  duplicateIds(responsiveCases, "frame", "responsive-viewport-case");
  compareExact(responsiveCases.map((item) => item.frame), expectedResponsive.map((item) => item.registryFrame), "responsive viewport case");
  for (const responsive of mapping.responsiveRiskFrames) {
    const owner = scenarios.find((scenario) => scenario.id === responsive.owningResponsiveCase);
    report(owner?.responsiveViewportCases.includes(responsive.registryFrame), `responsive frame ${responsive.registryFrame} is detached from owning scenario ${responsive.owningResponsiveCase}`);
  }
  for (const scenario of scenarios) {
    const frame = registry.frames.find((candidate) => candidate.id === scenario.visualFixtures?.[0]);
    const annotation = frame && parseAnnotation(read(path.join(referenceRoot, frame.annotation)));
    report(scenario.batch === batchFor(scenario.surface), `scenario batch is invalid: ${scenario.id}`);
    report(Boolean(scenario.given?.includes(`${scenario.surface}.${scenario.state}`)), `scenario precondition is not deterministic: ${scenario.id}`);
    report(Boolean(scenario.when?.includes(`injection.${scenario.surface}.${scenario.state}`)), `scenario transition does not name its injection: ${scenario.id}`);
    report(Boolean(annotation && scenario.then?.includes(annotation.heading) && scenario.then.includes(annotation.automationName) && scenario.then.includes(frame.id)), `scenario outcome does not resolve approved annotation evidence: ${scenario.id}`);
    report(JSON.stringify(scenario.contractReferences) === JSON.stringify(["foundations.md", "reference-states/matrix.yaml"]), `scenario contract references are incomplete: ${scenario.id}`);
    for (const reference of scenario.contractReferences ?? [])
      report(existsSync(path.join(designRoot, reference)), `scenario contract reference does not resolve: ${scenario.id} -> ${reference}`);
    report(isStringArray(scenario.componentReferences) && scenario.componentReferences.length > 0, `scenario component references are missing: ${scenario.id}`);
    for (const reference of scenario.componentReferences ?? [])
      report(mapping.components.some((component) => component.semanticId === reference), `scenario component reference does not resolve: ${scenario.id} -> ${reference}`);
    report(isStringArray(scenario.patternReferences) && scenario.patternReferences.length > 0, `scenario pattern references are missing: ${scenario.id}`);
    for (const reference of scenario.patternReferences ?? [])
      report(existsSync(path.join(designRoot, reference)), `scenario pattern reference does not resolve: ${scenario.id} -> ${reference}`);
    report(JSON.stringify(scenario.annotationReferences) === JSON.stringify(frame ? [frame.annotation] : []), `scenario annotation references do not match its fixture: ${scenario.id}`);
    for (const reference of scenario.annotationReferences ?? [])
      report(existsSync(path.join(referenceRoot, reference)), `scenario annotation reference does not resolve: ${scenario.id} -> ${reference}`);
    report(scenario.deterministicInjectionIdentity === `injection.${scenario.surface}.${scenario.state}`, `scenario injection identity is invalid: ${scenario.id}`);
    report(JSON.stringify(scenario.visualFixtures) === JSON.stringify(frame ? [frame.id] : []), `scenario visual fixture linkage is invalid: ${scenario.id}`);
    report(JSON.stringify(scenario.applicationAcceptanceChecks) === JSON.stringify([`ACCEPTANCE-${scenario.surface.toUpperCase()}`]), `scenario application acceptance checks are invalid: ${scenario.id}`);
    report(JSON.stringify(scenario.uiAutomationChecks) === JSON.stringify(["UIA-SURFACE-STRUCTURE", `UIA-${scenario.surface.toUpperCase()}-CONTRACT`]), `scenario UI Automation checks are invalid: ${scenario.id}`);
    report(JSON.stringify(scenario.sharedPatternChecks) === JSON.stringify(["UIA-KEYBOARD-COMPLETE", "UIA-LIVE-REGION-EVENTS"]), `scenario shared-pattern checks are invalid: ${scenario.id}`);
    report(JSON.stringify(scenario.manualChecks) === JSON.stringify(["MANUAL-VISUAL-EQUIVALENCE", "MANUAL-KEYBOARD-JOURNEY", "MANUAL-NARRATOR-JOURNEY", "MANUAL-HIGH-CONTRAST", "MANUAL-REDUCED-MOTION"]), `scenario manual checks are invalid: ${scenario.id}`);
    report(isStringArray(scenario.resultEvidence), `scenario resultEvidence must be an array: ${scenario.id}`);
    report(isStringArray(scenario.waiverReferences), `scenario waiverReferences must be an array: ${scenario.id}`);
    for (const waiver of scenario.waiverReferences ?? [])
      report(waivers.waivers.some((record) => record.id === waiver), `scenario waiver reference does not resolve: ${scenario.id} -> ${waiver}`);
    for (const expectation of ["focus", "announcement", "destination", "preservation", "recovery"])
      report(typeof scenario.expected?.[expectation] === "string" && scenario.expected[expectation].length > 0, `scenario expected.${expectation} is missing: ${scenario.id}`);
  }

  duplicateIds(evidenceIndex.evidence, "id", "evidence");
  const evidenceIds = new Set(evidenceIndex.evidence.map((item) => item.id));
  const references = collectEvidenceReferences(mapping).concat(collectEvidenceReferences(catalogs));
  const requiredEvidence = new Set(references);
  let discoveredDependency = true;
  while (discoveredDependency) {
    discoveredDependency = false;
    for (const id of [...requiredEvidence]) {
      const procedure = evidenceIndex.evidence.find((item) => item.id === id)?.procedure;
      if (procedure && !requiredEvidence.has(procedure)) { requiredEvidence.add(procedure); discoveredDependency = true; }
    }
  }
  for (const id of requiredEvidence) if (!evidenceIds.has(id)) errors.push(`dangling evidence identifier: ${id}`);
  for (const id of evidenceIds) if (!requiredEvidence.has(id)) errors.push(`extra evidence record: ${id}`);
  for (const item of evidenceIndex.evidence) {
    if (item.kind === "test-module") report(existsSync(path.join(repositoryRoot, item.location)), `evidence test module does not exist: ${item.id} -> ${item.location}`);
    if (item.kind === "named-manual-procedure") {
      const procedurePath = path.join(handoffRoot, item.location);
      report(existsSync(procedurePath), `manual procedure location does not resolve: ${item.id} -> ${item.location}`);
      if (existsSync(procedurePath)) report(read(procedurePath).includes(`## ${item.procedure}`), `manual procedure does not resolve: ${item.id} -> ${item.procedure}`);
    }
    if (item.kind === "visual-fixture") report(existsSync(canonicalReferencePath(item.location)), `visual fixture does not exist: ${item.id} -> ${item.location}`);
    if (["scenario", "deterministic-injection-spec"].includes(item.kind)) {
      const evidencePath = path.join(handoffRoot, item.location);
      report(existsSync(evidencePath) && read(evidencePath).includes(`\"${item.id}\"`), `${item.kind} does not resolve: ${item.id} -> ${item.location}`);
    }
    report(["test-module", "named-manual-procedure", "visual-fixture", "scenario", "deterministic-injection-spec"].includes(item.kind), `unknown evidence kind: ${item.id} -> ${item.kind}`);
  }

  report(Array.isArray(waivers.waivers) && waivers.waivers.length === 0, "design-v1.0.1 waiver register must be empty by default");
  report(manifest.baseline?.tag === "design-v1.0.0" && manifest.baseline?.commit === registry.contract.commit, "manifest baseline anchor does not match the approved registry");
  report(manifest.baseline?.registrySha256 === sha256Bytes(path.join(referenceRoot, "registry.json")), "registry hash does not match the anchored manifest");
  const requiredArtifactPaths = [
    "mapping.json", "evidence-index.json", "manual-procedures.md", "waivers.json", "README.md",
    ...matrix.surfaces.map((surface) => `scenarios/${surface.id}.json`),
  ];
  duplicateIds(manifest.artifacts, "path", "manifest-artifact");
  compareExact(manifest.artifacts.map((artifact) => artifact.path), requiredArtifactPaths, "manifest artifact");
  report(JSON.stringify(manifest.cardinalities) === JSON.stringify({ semanticTokens: 91, components: 17, surfaces: 8, canonicalStates: 71, responsiveRiskFrames: 32, targetFrames: 103 }), "manifest cardinalities do not match the approved handoff");
  duplicateIds(manifest.targetHashes, "id", "target-hash");
  compareExact(manifest.targetHashes.map((target) => target.id), registry.frames.map((frame) => frame.id), "target-hash");
  for (const target of manifest.targetHashes) {
    const frame = registry.frames.find((candidate) => candidate.id === target.id);
    const targetPath = canonicalReferencePath(target.path);
    report(frame?.sha256 === target.sha256, `inherited target hash differs from design-v1.0.0 registry: ${target.id}`);
    report(existsSync(targetPath) && sha256Bytes(targetPath) === target.sha256, `approved target bytes changed: ${target.id}`);
  }
  for (const artifact of manifest.artifacts) {
    const artifactPath = path.join(handoffRoot, artifact.path);
    report(existsSync(artifactPath), `manifest artifact is missing: ${artifact.path}`);
    if (existsSync(artifactPath)) report(sha256Text(artifactPath) === artifact.sha256, `manifest artifact hash mismatch: ${artifact.path}`);
  }
} catch (error) {
  errors.push(error.stack ?? error.message);
}

if (errors.length) {
  console.error(`design-v1.0.1 traceability validation failed:\n- ${errors.join("\n- ")}`);
  process.exitCode = 1;
} else {
  console.log("Validated design-v1.0.1 traceability: 91 tokens, 17 components, 8 surfaces, 71 canonical states, and 32 responsive-risk frames; 103 inherited target hashes unchanged.");
}
