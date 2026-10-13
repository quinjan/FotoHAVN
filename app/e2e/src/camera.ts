/** A camera as reported by the `test.listCameras` hook. */
export interface Camera {
  id: string;
  name: string;
}

export const cameraEnvVar = "FOTOHAVN_E2E_CAMERA";

export const noCameraMessage = "End-to-end tests require at least one connected camera.";

export function unmatchedCameraMessage(envValue: string, cameras: readonly Camera[]): string {
  const names = cameras.map((camera) => camera.name).join(", ");
  return `${noCameraMessage} ${cameraEnvVar}="${envValue}" matched none of: ${names}.`;
}

export function describeCamera(camera: Camera): string {
  return `E2E camera: ${camera.name} (${camera.id})`;
}

/**
 * Applies the end-to-end camera precondition.
 * Returns the camera whose id or name matches `envValue`, or the first camera when it's unset.
 * Throws the exact required-camera message when there is no camera or nothing matches.
 */
export function selectCamera(cameras: readonly Camera[], envValue: string | undefined): Camera {
  const first = cameras[0];
  if (first === undefined) {
    throw new Error(noCameraMessage);
  }
  if (envValue === undefined || envValue.trim() === "") {
    return first;
  }
  const wanted = envValue.trim();
  const match =
    cameras.find((camera) => camera.id === wanted) ??
    cameras.find((camera) => camera.name === wanted) ??
    cameras.find((camera) => camera.name.toLowerCase() === wanted.toLowerCase());
  if (match === undefined) {
    throw new Error(unmatchedCameraMessage(envValue, cameras));
  }
  return match;
}
