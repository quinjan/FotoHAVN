import type { ComponentFixture } from "../fixture.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { StorageErrorScreen, type StorageErrorScreenProps } from "./StorageErrorScreen.tsx";

const dataPath = "C:\\FOTOHAVN\\Data";

function fixture(id: string, title: string, props: Pick<StorageErrorScreenProps, "kind"> & Partial<StorageErrorScreenProps>): ComponentFixture {
  return {
    id,
    title,
    surface: "light",
    role: "operator",
    layout: "fill",
    render: () => <StorageErrorScreen dataPath={dataPath} onRetry={noop} {...props} />,
  };
}

export const fixtures: readonly ComponentFixture[] = [
  fixture("storage-error-screen-disk-full", "Storage error, disk full", { kind: "diskFull" }),
  fixture("storage-error-screen-access-denied", "Storage error, access denied", { kind: "accessDenied" }),
  fixture("storage-error-screen-locked", "Storage error, a file is locked", { kind: "locked" }),
  fixture("storage-error-screen-not-local-disk", "Storage error, network drive", {
    kind: "notLocalDisk",
    dataPath: "\\\\booth-server\\shared\\FOTOHAVN\\Data",
  }),
  fixture("storage-error-screen-cloud-synced", "Storage error, cloud-synced folder", {
    kind: "cloudSynced",
    dataPath: "C:\\Users\\Booth\\OneDrive\\FOTOHAVN\\Data",
  }),
  fixture("storage-error-screen-not-writable", "Storage error, not writable", { kind: "notWritable" }),
  fixture("storage-error-screen-integrity-failed", "Storage error, integrity check failed", { kind: "integrityFailed" }),
  fixture("storage-error-screen-corrupt", "Storage error, corrupt database", { kind: "corrupt" }),
  fixture("storage-error-screen-unknown", "Storage error, unknown", { kind: "unknown" }),
  fixture("storage-error-screen-retrying", "Storage error, trying again", { kind: "diskFull", retrying: true }),
  fixture("storage-error-screen-rejected", "Storage error, the try didn't go through", {
    kind: "locked",
    notice: "That isn't ready yet.",
  }),
  fixture("storage-error-screen-long-path", "Storage error, a very long Data folder path", {
    kind: "cloudSynced",
    dataPath:
      "C:\\Users\\Booth Operator\\OneDrive - Santos and Reyes Events Company\\Documents\\Photo Booth\\FOTOHAVN Installation\\Current Season\\Data",
  }),
];
