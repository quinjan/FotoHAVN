import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = (...parts) => readFileSync(path.join(repositoryRoot, ...parts), "utf8");

const resources = read("src", "FotoHavn.App", "DesignSystem", "FotoHavnDesignResources.xaml");
const eventCard = read("src", "FotoHavn.App", "Controls", "EventCard.xaml");
const dialogEventIdentity = read("src", "FotoHavn.App", "Controls", "DialogEventIdentity.xaml");
const dialogEventIdentityCode = read("src", "FotoHavn.App", "Controls", "DialogEventIdentity.xaml.cs");
const mainWindow = read("src", "FotoHavn.App", "MainWindow.xaml");
const mainWindowCode = read("src", "FotoHavn.App", "MainWindow.xaml.cs");
const applicationContract = read("src", "FotoHavn.Core", "ApplicationContract.cs");
const orchestrator = read("src", "FotoHavn.Core", "EventGuestCycleOrchestrator.cs");
const verificationWindowSession = read("tools", "FotoHavn.UiVerificationHost", "WindowSession.cs");

test("Event fields use matching chrome with vertically centered text", () => {
  const textFieldStyle = resources.match(/<Style x:Key="FotoHavnTextFieldStyle"[\s\S]*?<\/Style>/)?.[0] ?? "";
  assert.match(resources, /x:Key="TextFieldContentPadding">10,13,6,13</);
  assert.match(textFieldStyle, /Property="Padding" Value="\{StaticResource TextFieldContentPadding\}"/);

  const eventIdentityField = mainWindow.match(/<TextBox[\s\S]*?x:Name="EventIdentityValueText"[\s\S]*?\/>/)?.[0] ?? "";
  assert.match(eventIdentityField, /IsReadOnly="True"/);
  assert.match(eventIdentityField, /Style="\{StaticResource FotoHavnTextFieldStyle\}"/);
});

test("Event Card hover surface covers the full card", () => {
  const panelStyle = resources.match(/<Style x:Key="FotoHavnEventCardPanelStyle"[\s\S]*?<\/Style>/)?.[0] ?? "";
  assert.doesNotMatch(panelStyle, /Property="Padding"/);
  assert.match(eventCard, /x:Name="CardLayout"[^>]*Padding="\{StaticResource EventCardPadding\}"/);
});

test("Event Setup reserves a scrollbar gutter instead of covering the form", () => {
  assert.match(resources, /x:Key="SetupScrollContentPadding"/);
  assert.match(mainWindow, /Padding="\{StaticResource SetupScrollContentPadding\}"/);
});

test("successful deletion switches to one acknowledgement action", () => {
  assert.match(mainWindowCode, /deletion\?\.Stage == EventDeletionStage\.Deleted/);
  assert.match(mainWindowCode, /CancelEventDeletionButton\.Visibility = isDeletionSuccess \? Visibility\.Collapsed/);
  assert.match(mainWindowCode, /deletion\?\.Message/);
});

test("Printer defaults to an enabled Not printing selector", () => {
  const printerSelector = mainWindow.match(/<ComboBox[\s\S]*?x:Name="PrinterComboBox"[\s\S]*?<\/ComboBox>/)?.[0] ?? "";
  assert.match(printerSelector, /SelectedIndex="0"/);
  assert.match(printerSelector, /Not printing/);
  assert.doesNotMatch(printerSelector, /IsEnabled="False"/);
});

test("event confirmation spacing uses the compact shared anatomy", () => {
  assert.match(resources, /x:Key="ModalDialogPadding">24</);
  assert.match(resources, /x:Key="DialogContentSpacing">14</);
  assert.match(resources, /x:Key="DialogEventIdentityPadding">0,16</);
  assert.match(resources, /x:Key="DialogEventIdentityStressPadding">0,12</);
  assert.match(dialogEventIdentity, /<Border[\s\S]*?Grid\.Row="0"[\s\S]*?VerticalAlignment="Top"/);
  assert.match(dialogEventIdentity, /<StackPanel[\s\S]*?Grid\.Row="1"[\s\S]*?Margin="\{StaticResource DialogEventIdentityPadding\}"/);
  assert.match(dialogEventIdentity, /<Border[\s\S]*?Grid\.Row="2"[\s\S]*?VerticalAlignment="Bottom"/);
  assert.match(dialogEventIdentityCode, /IdentityContent\.Margin/);
  assert.doesNotMatch(dialogEventIdentityCode, /IdentityGrid\.Padding/);
  assert.match(resources, /x:Key="DialogSemanticIconSize">48</);
  const sharedStacks = mainWindow.match(/<StackPanel Spacing="\{StaticResource DialogContentSpacing\}"/g) ?? [];
  assert.ok(sharedStacks.length >= 5, `expected shared dialog spacing on at least five confirmations, found ${sharedStacks.length}`);
});

test("production asynchronous confirmations publish busy states", () => {
  assert.match(applicationContract, /StartEventConfirmationPresentation\([\s\S]*?bool IsBusy = false/);
  assert.match(applicationContract, /ActiveEventPresentation\([\s\S]*?bool IsExitBusy = false/);
  assert.match(applicationContract, /EventSetupPresentation\([\s\S]*?bool IsBusy = false/);
  assert.match(applicationContract, /EventSetupPresentation\([\s\S]*?bool IsSavingAndStarting = false/);
  assert.match(orchestrator, /StartEventConfirmation = confirmation with \{ IsBusy = true \}/);
  assert.match(orchestrator, /IsExitBusy = true/);
  assert.match(orchestrator, /draft = draft with \{ IsBusy = true, IsSavingAndStarting = startEvent \}/);
  assert.match(mainWindowCode, /presentation\.StartEventConfirmation\?\.IsBusy == true/);
  assert.match(mainWindowCode, /activeEvent\?\.IsExitBusy == true/);
  assert.match(mainWindowCode, /setup\.IsBusy/);
  assert.match(mainWindowCode, /setup\.IsSavingAndStarting/);
});

test("pinned host ignores terminated App SDK records with invalid handles", () => {
  assert.match(
    verificationWindowSession,
    /exception is Win32Exception or InvalidOperationException/,
  );
});

test("pinned host falls back to direct client capture when desktop copy is unavailable", () => {
  assert.match(verificationWindowSession, /CopyClientWithPrintWindow\(physical\)/);
  assert.match(verificationWindowSession, /PrintWindow\(Handle, deviceContext, PwClientOnly \| PwRenderFullContent\)/);
});
