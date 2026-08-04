# PROTOTYPE — Windows camera rig probe

## Question

On the field-test Windows PC, does the capability-based `Windows.Media.Capture.MediaCapture` boundary reliably enumerate and select the integrated and a third-party plug-and-play camera, render a fresh mirrored guest preview, save four ordered unmirrored Captures, preflight readiness, and report disconnect/reconnect? What is the minimum stream capability an eligible Camera must pass?

This is disposable diagnostic code for the Wayfinder ticket **Validate Windows camera preview and capture on the field-test rig**. It is not production UI or architecture.

## Run

```powershell
dotnet run --project prototypes/windows-camera-rig/CameraRigPrototype.csproj
```

Choose a camera, select **Initialize**, wait for every readiness gate to pass, then select **Capture 4**. Run once for the integrated camera and once for a third-party plug-and-play camera. The ignored `evidence/` directory receives the four private JPEGs and a JSON report.

The prototype keeps the selected binding in the ignored `prototype-camera-binding.json` file so closing and reopening it can test exact interface-ID resolution. Move an external camera to another USB port to see whether Windows changes that identity.

Accepted minimums for the first field-test build:

- preview: at least 640×480 at 15 fps and a fresh decoded color frame;
- photo: at least 1280×720, successfully encoded to JPEG;
- exclusive `MediaCapture` initialization and writable local storage;
- exact selected interface identity, with no silent substitution.

See [`RESULT.md`](RESULT.md) for the accepted integrated-camera verdict and its explicit external-camera qualification.
