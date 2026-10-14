import { useBrainClient } from "../../brain/BrainClientContext.ts";
import { InlineStatus } from "../../components/InlineStatus/index.ts";
import { LivePreview, silentFrameSource } from "../../components/LivePreview/index.ts";
import { Select, type SelectOption } from "../../components/Select/index.ts";
import { noCamerasHelp } from "../../components/CameraDiagnosticsScreen/cameraText.ts";
import type { EventSetupCameraView } from "../../protocol/generated.ts";
import { cameraLine, copy, fieldIds } from "./setupCopy.ts";
import styles from "./EventSetup.module.css";

export interface CameraFieldProps {
  readonly camera: EventSetupCameraView;
  readonly error: string | undefined;
  readonly onSelect: (deviceId: string) => void;
}

/** The chosen camera's options: the connected cameras, plus the bound one, kept and marked, when it isn't connected. */
function cameraOptions(camera: EventSetupCameraView): SelectOption[] {
  const options: SelectOption[] = camera.cameras.map((option) => ({ value: option.deviceId, label: option.name }));
  const bound = camera.selectedDeviceId;
  if (bound !== null && !camera.cameras.some((option) => option.deviceId === bound)) {
    options.push({ value: bound, label: `${camera.selectedName ?? "Chosen camera"} (not connected)`, disabled: true });
  }
  return options;
}

/**
 * The Camera field of Event setup: the cameras to choose from, the state line (Checking, Ready, Not available with the
 * reason, In use by another app or by the running Event, Not checked, or not connected), and the live preview while the
 * brain sends this camera's frames. A bound camera that has gone stays chosen and visible, never swapped for another.
 */
export function CameraField({ camera, error, onSelect }: CameraFieldProps) {
  const client = useBrainClient();
  const line = cameraLine(camera);
  return (
    <div className={styles.cameraField}>
      <Select
        id={fieldIds.camera}
        label={copy.fields.camera}
        required
        placeholder={copy.fields.cameraPlaceholder}
        value={camera.selectedDeviceId ?? ""}
        options={cameraOptions(camera)}
        error={error}
        help={camera.cameras.length === 0 ? noCamerasHelp : undefined}
        onChange={(event) => {
          if (event.target.value !== "") {
            onSelect(event.target.value);
          }
        }}
      />
      <InlineStatus tone={line?.tone ?? "neutral"}>{line?.text}</InlineStatus>
      {camera.previewActive && (
        <div className={styles.preview}>
          <LivePreview source={client.frames ?? silentFrameSource} mirrored={camera.mirrored} />
        </div>
      )}
    </div>
  );
}
