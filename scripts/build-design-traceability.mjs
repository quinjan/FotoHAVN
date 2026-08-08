import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, unlinkSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parseAnnotation, parseMatrix, parseWinuiMappingSource, responsiveOwnerState } from "./design-traceability-source.mjs";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const designRoot = path.join(repositoryRoot, "docs", "design-system");
const referenceRoot = path.join(designRoot, "reference-states");
const handoffRoot = path.join(designRoot, "traceability");
const catalogRoot = path.join(handoffRoot, "scenarios");
const mappingSourcePath = path.join(designRoot, "winui-mapping.yaml");
const matrixPath = path.join(referenceRoot, "matrix.yaml");
const registryPath = path.join(referenceRoot, "registry.json");

const read = (file) => readFileSync(file, "utf8");
const json = (value) => `${JSON.stringify(value, null, 2)}\n`;
const sha256 = (file) => createHash("sha256").update(readFileSync(file)).digest("hex");
const acceptanceModules = {
  "saved-events": "tests/FotoHavn.AcceptanceTests/EventPersistenceAcceptanceTests.cs",
  "event-setup": "tests/FotoHavn.AcceptanceTests/EventSetupAcceptanceTests.cs",
  "guest-start": "tests/FotoHavn.AcceptanceTests/GuestCycleAcceptanceTests.cs",
  "guest-start-unavailable": "tests/FotoHavn.AcceptanceTests/GuestStartReadinessAcceptanceTests.cs",
  capture: "tests/FotoHavn.AcceptanceTests/GuestCycleAcceptanceTests.cs",
  "operator-assistance": "tests/FotoHavn.AcceptanceTests/GuestCycleAcceptanceTests.cs",
  "photo-strip": "tests/FotoHavn.AcceptanceTests/GuestCycleAcceptanceTests.cs",
  confirmation: "tests/FotoHavn.AcceptanceTests/ActiveEventAcceptanceTests.cs",
};

const surfaceComponents = {
  "saved-events": ["component.app-header", "component.event-card", "component.icon-action", "component.toast"],
  "event-setup": ["component.app-header", "component.setup-field-group", "component.text-field", "component.select-field", "component.camera-viewport", "component.action-button"],
  "guest-start": ["component.app-header", "component.action-button"],
  "guest-start-unavailable": ["component.app-header", "component.status-callout", "component.action-button"],
  capture: ["component.app-header", "component.camera-viewport", "component.capture-progress"],
  "operator-assistance": ["component.app-header", "component.operator-assistance", "component.action-button"],
  "photo-strip": ["component.app-header", "component.photo-strip-result"],
  confirmation: ["component.modal-dialog", "component.inline-status", "component.action-button"],
};

const surfacePatterns = {
  "saved-events": ["patterns/event-identification.md", "patterns/asynchronous-feedback.md"],
  "event-setup": ["patterns/setup-readiness.md", "patterns/asynchronous-feedback.md"],
  "guest-start": ["patterns/operator-exit.md", "patterns/guest-cycle-feedback.md"],
  "guest-start-unavailable": ["patterns/guest-cycle-feedback.md", "patterns/operator-exit.md"],
  capture: ["patterns/guest-cycle-feedback.md"],
  "operator-assistance": ["patterns/guest-cycle-feedback.md", "patterns/operator-exit.md"],
  "photo-strip": ["patterns/guest-cycle-feedback.md"],
  confirmation: ["patterns/modal-safety.md", "patterns/event-identification.md"],
};

const batchFor = (surface) => ["saved-events", "event-setup", "confirmation"].includes(surface) ? 3 :
  ["guest-start", "guest-start-unavailable"].includes(surface) ? 4 : 5;
const pascal = (value) => value.split("-").map((part) => part[0].toUpperCase() + part.slice(1)).join("");
mkdirSync(catalogRoot, { recursive: true });
for (const file of readdirSync(catalogRoot).filter((candidate) => candidate.endsWith(".json"))) unlinkSync(path.join(catalogRoot, file));
const sourceMapping = parseWinuiMappingSource(read(mappingSourcePath));
const matrix = parseMatrix(read(matrixPath));
const registry = JSON.parse(read(registryPath));
const canonicalFrames = registry.frames.filter((frame) => frame.coverage === "canonical");
const responsiveFrames = registry.frames.filter((frame) => frame.coverage === "responsive-risk");

const surfaces = matrix.surfaces.map((surface) => ({
  semanticId: `surface.${surface.id}`,
  productionComposition: `FotoHavn.App.Surfaces.${pascal(surface.id)}Surface`,
  presentationSource: "FotoHavn.Core.ApplicationPresentation",
  semanticRootAutomationId: `FotoHavn.Surface.${pascal(surface.id)}`,
  verificationIdentifiers: ["UIA-SURFACE-STRUCTURE"],
  manualChecks: ["MANUAL-KEYBOARD-JOURNEY", "MANUAL-NARRATOR-JOURNEY"],
}));

const canonicalStates = canonicalFrames.map((frame) => ({
  identity: `${frame.surface}.${frame.state}`,
  deterministicInjectionIdentity: `injection.${frame.surface}.${frame.state}`,
  acceptanceScenario: `scenario.${frame.surface}.${frame.state}`,
  uiAutomationContract: `UIA-${frame.surface.toUpperCase()}-CONTRACT`,
  canonicalVisualFixture: frame.id,
  manualChecks: ["MANUAL-VISUAL-EQUIVALENCE", "MANUAL-NARRATOR-JOURNEY"],
}));

const responsiveRiskFrames = responsiveFrames.map((frame) => ({
  registryFrame: frame.id,
  owningResponsiveCase: `scenario.${frame.surface}.${responsiveOwnerState[frame.surface]}`,
  uiAutomationGeometryCheck: "UIA-RESPONSIVE-GEOMETRY",
  visualFixture: frame.id,
  manualChecks: ["MANUAL-RESPONSIVE-REFLOW", "MANUAL-TOUCH-TARGETS"],
}));

const mapping = {
  schemaVersion: 2,
  contractVersion: "1.0.1",
  semanticChange: false,
  inherits: {
    contractVersion: "1.0.0",
    tag: registry.contract.tag,
    commit: registry.contract.commit,
    targetHashes: "manifest.json#targetHashes",
  },
  semanticTokens: sourceMapping.semanticTokens,
  components: sourceMapping.components,
  surfaces,
  canonicalStates,
  responsiveRiskFrames,
};

const scenarioCatalogs = [];
for (const surface of matrix.surfaces) {
  const scenarios = surface.canonicalStates.map((state) => {
    const frame = canonicalFrames.find((candidate) => candidate.surface === surface.id && candidate.state === state);
    const annotation = parseAnnotation(read(path.join(referenceRoot, frame.annotation)));
    const responsiveCases = responsiveFrames.filter((candidate) => candidate.surface === surface.id && candidate.state === surface.responsiveRisk).map((candidate) => candidate.id);
    const isOperatorAssistanceSurface = surface.id === "operator-assistance";
    const requiresRecoveryExpectation = /unavailable|failure|failed|retry|recovered|exit-only/.test(state) || surface.id === "guest-start-unavailable";
    const hasDestinationExpectation = /returning|success-destination|confirmation-open/.test(state);
    return {
      id: `scenario.${surface.id}.${state}`,
      surface: surface.id,
      state,
      batch: batchFor(surface.id),
      given: `the authoritative presentation is fixed to ${surface.id}.${state} with the approved ${frame.viewport} viewport, deterministic media, time, Camera, storage, and persistence outcomes`,
      when: `the production composition renders injection.${surface.id}.${state} and reports render settled`,
      then: `the level-one heading is “${annotation.heading}”; the ${annotation.automationRole} automation root is named “${annotation.automationName}” in state “${annotation.automationState}”; reading order is ${annotation.readingOrder}; and the pixels match ${frame.id}`,
      contractReferences: ["foundations.md", "reference-states/matrix.yaml"],
      componentReferences: surfaceComponents[surface.id],
      patternReferences: surfacePatterns[surface.id],
      annotationReferences: [frame.annotation],
      deterministicInjectionIdentity: `injection.${surface.id}.${state}`,
      visualFixtures: [frame.id],
      responsiveViewportCases: state === responsiveOwnerState[surface.id] ? responsiveCases : [],
      applicationAcceptanceChecks: [`ACCEPTANCE-${surface.id.toUpperCase()}`],
      uiAutomationChecks: ["UIA-SURFACE-STRUCTURE", `UIA-${surface.id.toUpperCase()}-CONTRACT`],
      sharedPatternChecks: ["UIA-KEYBOARD-COMPLETE", "UIA-LIVE-REGION-EVENTS"],
      manualChecks: ["MANUAL-VISUAL-EQUIVALENCE", "MANUAL-KEYBOARD-JOURNEY", "MANUAL-NARRATOR-JOURNEY", "MANUAL-HIGH-CONTRAST", "MANUAL-REDUCED-MOTION"],
      expected: {
        focus: `initial focus: ${annotation.initialFocus}; order: ${annotation.focusOrder}; return target: ${annotation.focusReturn}`,
        announcement: `${annotation.announcement} (${annotation.announcementPriority})`,
        destination: hasDestinationExpectation ? `the transition destination represented by ${surface.id}.${state} is reached` : "not applicable to this settled state",
        preservation: isOperatorAssistanceSurface ? "the injected durable Capture count remains visible and unchanged" : "not applicable to this surface",
        recovery: requiresRecoveryExpectation ? `only recovery actions in this reading order are exposed: ${annotation.readingOrder}` : "not applicable to this state",
      },
      resultEvidence: [],
      waiverReferences: [],
    };
  });
  const catalog = { schemaVersion: 1, contractVersion: "1.0.1", surface: surface.id, scenarioCount: scenarios.length, scenarios };
  const relativePath = `scenarios/${surface.id}.json`;
  writeFileSync(path.join(handoffRoot, relativePath), json(catalog));
  scenarioCatalogs.push(relativePath);
}

const manualProcedures = [
  ["MANUAL-SEMANTIC-RESOURCE-AUDIT", "Compare each semantic ID, XAML key, WinUI type, and dictionary owner against mapping.json; record every mismatch."],
  ["MANUAL-VISUAL-EQUIVALENCE", "Capture the production composition in the pinned environment and review target, actual, and diff at 100%; no unexplained visible difference passes."],
  ["MANUAL-KEYBOARD-JOURNEY", "Traverse every reachable action using keyboard only; verify logical order, visible focus, containment, restoration, and Escape behavior."],
  ["MANUAL-NARRATOR-JOURNEY", "Run the named journey with Narrator and record spoken names, roles, states, headings, status announcements, and reading order."],
  ["MANUAL-RESPONSIVE-REFLOW", "Review all four responsive-risk viewports for clipping, overlap, scroll policy, focus reachability, and essential-content priority."],
  ["MANUAL-TOUCH-TARGETS", "Measure operator and guest targets and verify the approved 48px and 64px minimums plus separation."],
  ["MANUAL-HIGH-CONTRAST", "Verify critical state, focus, action, and overlay meaning in Windows High Contrast without relying on custom color alone."],
  ["MANUAL-REDUCED-MOTION", "Verify progress, dialogs, guarded holds, Capture feedback, and Photo Strip transitions with reduced motion enabled."],
  ["MANUAL-UIA-CONTRACT-REVIEW", "Until the shared Windows UI verification host lands, inspect the named UI Automation contract against production controls and attach the inspection record."],
];
const procedureMarkdown = `# design-v1.0.1 manual procedures\n\nThese named procedures are evidence endpoints for the traceability contract. Record operator, environment, timestamp, result, and attachments for every execution.\n\n${manualProcedures.map(([id, steps]) => `## ${id}\n\n${steps}\n`).join("\n")}\n## Shared verification identifiers\n\nThe following design-system and UI Automation identifiers resolve to **MANUAL-UIA-CONTRACT-REVIEW** until their named automated suite is implemented by the rollout batch. The identifier must remain on the result record so automated evidence can replace the manual endpoint without changing scenario identity.\n`;

const evidence = [];
const addEvidence = (id, kind, location, procedure) => {
  if (!evidence.some((item) => item.id === id)) evidence.push({ id, kind, location, ...(procedure ? { procedure } : {}) });
};
for (const [id] of manualProcedures) addEvidence(id, "named-manual-procedure", "manual-procedures.md", id);
for (const surface of matrix.surfaces) {
  addEvidence(`ACCEPTANCE-${surface.id.toUpperCase()}`, "test-module", acceptanceModules[surface.id]);
  addEvidence(`UIA-${surface.id.toUpperCase()}-CONTRACT`, "named-manual-procedure", "manual-procedures.md", "MANUAL-UIA-CONTRACT-REVIEW");
}
for (const id of ["UIA-SURFACE-STRUCTURE", "UIA-RESPONSIVE-GEOMETRY", "UIA-KEYBOARD-COMPLETE", "UIA-LIVE-REGION-EVENTS"])
  addEvidence(id, "named-manual-procedure", "manual-procedures.md", "MANUAL-UIA-CONTRACT-REVIEW");
for (const component of sourceMapping.components)
  for (const id of component.sharedVerificationIdentifiers) addEvidence(id, "named-manual-procedure", "manual-procedures.md", "MANUAL-UIA-CONTRACT-REVIEW");
for (const frame of registry.frames) addEvidence(frame.id, "visual-fixture", `../reference-states/${frame.target}`);
for (const state of canonicalStates) {
  addEvidence(state.acceptanceScenario, "scenario", `scenarios/${state.identity.split(".")[0]}.json`);
  addEvidence(state.deterministicInjectionIdentity, "deterministic-injection-spec", `scenarios/${state.identity.split(".")[0]}.json`);
}

const sharedIds = evidence.filter((item) => item.procedure === "MANUAL-UIA-CONTRACT-REVIEW").map((item) => item.id).sort();
writeFileSync(path.join(handoffRoot, "manual-procedures.md"), `${procedureMarkdown}\n${sharedIds.map((id) => `- \`${id}\``).join("\n")}\n`);
writeFileSync(path.join(handoffRoot, "mapping.json"), json(mapping));
writeFileSync(path.join(handoffRoot, "evidence-index.json"), json({ schemaVersion: 1, evidence }));
writeFileSync(path.join(handoffRoot, "waivers.json"), json({ schemaVersion: 1, default: "empty", waivers: [] }));

const readme = `# FotoHAVN design-v1.0.1 traceability handoff\n\nThis nonvisual patch makes the approved \`design-v1.0.0\` contract auditable. It does not change visual or behavioral semantics, and it inherits the 103 approved target hashes anchored at tag \`${registry.contract.tag}\` and commit \`${registry.contract.commit}\`.\n\n## Contents\n\n- \`mapping.json\`: mapping schema v2 at cardinalities 91 / 17 / 8 / 71 / 32.\n- \`scenarios/*.json\`: one catalog per production surface, totaling 71 canonical Given/When/Then scenarios.\n- \`evidence-index.json\`: resolution for acceptance, UI Automation, visual, injection, shared-pattern, and manual identifiers.\n- \`manual-procedures.md\`: named procedures for checks that require human evidence.\n- \`waivers.json\`: empty-by-default exception register.\n- \`manifest.json\`: baseline anchor, artifact hashes, and inherited target hashes.\n\n## Validate\n\nFrom the repository root, run:\n\n\`\`\`powershell\nnode scripts/validate-design-traceability.mjs\nnode --test tests/design-traceability-validator.test.mjs\n\`\`\`\n\nValidation rejects missing, duplicate, dangling, or extra mapping and evidence records, altered handoff artifacts, changed target hashes, and matrix/registry drift. Waivers must remain empty for this patch. Future waivers require a named scope, justification, approval, expiry, affected scenarios, and affected fixtures.\n`;
writeFileSync(path.join(handoffRoot, "README.md"), readme);

const artifactPaths = ["mapping.json", "evidence-index.json", "manual-procedures.md", "waivers.json", "README.md", ...scenarioCatalogs];
const manifest = {
  schemaVersion: 1,
  contractVersion: "1.0.1",
  semanticChange: false,
  baseline: {
    contractVersion: "1.0.0",
    tag: registry.contract.tag,
    commit: registry.contract.commit,
    registry: "../reference-states/registry.json",
    registrySha256: sha256(registryPath),
  },
  cardinalities: { semanticTokens: 91, components: 17, surfaces: 8, canonicalStates: 71, responsiveRiskFrames: 32, targetFrames: 103 },
  artifacts: artifactPaths.map((relativePath) => ({ path: relativePath, sha256: sha256(path.join(handoffRoot, relativePath)) })),
  targetHashes: registry.frames.map((frame) => ({ id: frame.id, path: `../reference-states/${frame.target}`, sha256: frame.sha256 })),
};
writeFileSync(path.join(handoffRoot, "manifest.json"), json(manifest));
console.log(`Built design-v1.0.1 traceability handoff: ${sourceMapping.semanticTokens.length} / ${sourceMapping.components.length} / ${surfaces.length} / ${canonicalStates.length} / ${responsiveRiskFrames.length}.`);
