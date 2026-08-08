import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const applicationRoot = path.join(repositoryRoot, "src", "FotoHavn.App");
const mapping = JSON.parse(
  readFileSync(path.join(repositoryRoot, "docs", "design-system", "traceability", "mapping.json"), "utf8"),
);

const sharedComponents = new Set([
  "component.action-button",
  "component.icon-action",
  "component.text-field",
  "component.select-field",
  "component.read-only-value",
  "component.inline-status",
  "component.status-callout",
  "component.progress-indicator",
  "component.toast",
  "component.modal-dialog",
]);

function applicationFiles(extension) {
  const visit = (directory) => readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory() && ["bin", "obj"].includes(entry.name)) return [];
    return entry.isDirectory() ? visit(target) : target.endsWith(extension) ? [target] : [];
  });
  return visit(applicationRoot);
}

test("every approved semantic token resolves to its mapped WinUI resource", () => {
  const dictionaries = new Map();

  for (const token of mapping.semanticTokens) {
    const resourcePath = path.join(repositoryRoot, ...token.resourceDictionary.split("/"));
    assert.equal(existsSync(resourcePath), true, `${token.semanticId} dictionary is missing`);
    const contents = dictionaries.get(resourcePath) ?? readFileSync(resourcePath, "utf8");
    dictionaries.set(resourcePath, contents);
    assert.match(contents, new RegExp(`x:Key=["']${token.xamlResourceKey}["']`), token.semanticId);
  }
});

test("all ten shared component families resolve their styles, controls, and Automation ID conventions", () => {
  const xaml = applicationFiles(".xaml").map((file) => readFileSync(file, "utf8")).join("\n");
  const csharp = applicationFiles(".cs").map((file) => readFileSync(file, "utf8")).join("\n");

  for (const component of mapping.components.filter(({ semanticId }) => sharedComponents.has(semanticId))) {
    for (const style of component.styleOwnership) {
      assert.match(xaml, new RegExp(`x:Key=["']${style}["']`), `${component.semanticId} style ${style}`);
    }

    if (component.controlType.startsWith("FotoHavn.App.Controls.")) {
      const className = component.controlType.split(".").at(-1);
      assert.match(`${xaml}\n${csharp}`, new RegExp(`(?:x:Class=["']${component.controlType}["']|class\\s+${className}\\b)`), component.semanticId);
    }

    assert.match(`${xaml}\n${csharp}`, new RegExp(component.automationIdPrefix.replaceAll(".", "\\.")), `${component.semanticId} Automation ID prefix`);
  }
});

test("the application loads the governed design resources and retires the prohibited danger color", () => {
  const app = readFileSync(path.join(applicationRoot, "App.xaml"), "utf8");
  const productionXaml = applicationFiles(".xaml").map((file) => readFileSync(file, "utf8")).join("\n");

  assert.match(app, /DesignSystem\/FotoHavnDesignResources\.xaml/);
  assert.match(productionXaml, /ResourceDictionary x:Key="HighContrast"/);
  assert.match(productionXaml, /ThemeResource SystemColorWindowTextColor/);
  assert.doesNotMatch(productionXaml, /#(?:FF)?FF4D4F/i);
});

test("portable publishing carries every compiled WinUI resource", () => {
  const project = readFileSync(path.join(applicationRoot, "FotoHavn.App.csproj"), "utf8");
  assert.match(project, /<_WinUIXbf Include="\$\(TargetDir\)\*\*\\\*\.xbf"/);
  assert.match(project, /ResolvedFileToPublish Include="@\(_WinUIXbf\)"/);
  assert.match(project, /<RelativePath>%\(_WinUIXbf\.RecursiveDir\)%\(_WinUIXbf\.Filename\)%\(_WinUIXbf\.Extension\)<\/RelativePath>/);
});

test("shared behavior helpers implement asynchronous, validation, announcement, and modal safety obligations", () => {
  const csharp = applicationFiles(".cs").map((file) => readFileSync(file, "utf8")).join("\n");
  const controlXaml = applicationFiles(".xaml")
    .filter((file) => file.includes(`${path.sep}Controls${path.sep}`))
    .map((file) => readFileSync(file, "utf8"))
    .join("\n");

  for (const contract of [
    /class ActionButtonVisuals/,
    /BeginBusy\(Button/,
    /class FieldFeedback/,
    /FieldCondition\.Invalid/,
    /visibleStatus\.Present/,
    /RaiseAutomationEvent\(AutomationEvents\.LiveRegionChanged\)/,
    /ConfigureDecision\(ContentDialog/,
    /if \(!confirmed\)/,
    /args\.Cancel = true/,
    /dialog\.CloseButtonText = string\.Empty/,
    /Grid\.SetRow\(secondary, stress \? 1 : 0\)/,
  ]) {
    assert.match(csharp, contract);
  }

  assert.doesNotMatch(
    controlXaml,
    /(Padding|Spacing|ColumnSpacing|CornerRadius|BorderThickness|MinHeight|MinWidth|Width|Height|FontSize)="\d/,
  );
});
