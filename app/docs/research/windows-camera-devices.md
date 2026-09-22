# Windows camera devices for the first field test

Research date: 2026-08-04

Scope: replace the fixed Canon EOS R100/EDSDK assumption with a camera selected per Event from the cameras Windows exposes as video-capture devices

## Decision-ready answer

Use the Windows Runtime `Windows.Media.Capture.MediaCapture` path for the first field test. An eligible camera is any integrated, USB, or vendor-driven device that:

1. appears in `DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)`;
2. can be initialized by `MediaCapture` using its `DeviceInformation.Id` as `VideoDeviceId`;
3. supplies a usable color preview; and
4. successfully captures a photo in a format and resolution acceptable to FotoHAVN.

This is a capability boundary, not a model allow-list and not a promise that every device marketed as a camera will work. It includes normal laptop cameras and plug-and-play UVC webcams. It may include a DSLR or other vendor device only when its active Windows driver/mode exposes a video-capture interface that passes the same checks. A proprietary tether/control mode that does not appear as a Windows video-capture device remains outside this integration.

Microsoft documents `MediaCapture` for WinUI 3 desktop apps and uses `DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)` plus `MediaCaptureInitializationSettings.VideoDeviceId` for explicit device selection. [Microsoft: camera preview access with MediaCapture](https://learn.microsoft.com/en-us/windows/apps/develop/camera/simple-camera-preview-access), [Microsoft: camera profiles and device selection](https://learn.microsoft.com/en-us/windows/apps/develop/camera/camera-profiles)

The Canon-specific EDSDK path researched previously is therefore no longer the field-test default. It can remain a future vendor-specific adapter if FotoHAVN later needs DSLR-native controls or full-resolution tethered stills that a Windows video-capture mode does not expose.

## Recommended Windows integration

| FotoHAVN need | Windows API path | Field-test consequence |
| --- | --- | --- |
| List cameras | `DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)` | Show every returned device by `DeviceInformation.Name`; never select by array position. Microsoft says `Name` is display-only and may change, so it is not an identity key. [DeviceInformation](https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.deviceinformation?view=winrt-26100) |
| Select a camera | Set the selected `DeviceInformation.Id` on `MediaCaptureInitializationSettings.VideoDeviceId`, set `StreamingCaptureMode.Video`, and call `InitializeAsync` | Initialization is part of validation. Mere appearance in the list is not enough. [Microsoft WinUI camera preview quickstart](https://learn.microsoft.com/en-us/windows/apps/develop/camera/simple-camera-preview-access) |
| Own configuration | Request `MediaCaptureSharingMode.ExclusiveControl` | FotoHAVN can select stream formats and camera controls. Initialization can fail if another app has exclusive control; shared read-only mode cannot configure several capture properties. [SharingMode](https://learn.microsoft.com/en-us/uwp/api/windows.media.capture.mediacaptureinitializationsettings.sharingmode?view=winrt-28000) |
| Show live preview | In WinUI 3, create a `MediaSource` from the selected color/preview `MediaFrameSource` and bind it to `MediaPlayerElement` | `CaptureElement` is not available in WinUI 3. A fresh rendered frame, not initialization alone, should be the preview-ready signal. [Camera preview access](https://learn.microsoft.com/en-us/windows/apps/develop/camera/simple-camera-preview-access) |
| Capture a still | Query `VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.Photo)`, set a returned format when supported, and use `CapturePhotoToStorageFileAsync` for a JPEG written directly to the current Guest Cycle; a prepared `LowLagPhotoCapture` is available if repeated-capture latency needs it | Capture resolution and whether photo/preview streams are independent vary by device. Validate the actual returned formats instead of assuming DSLR resolution. [Set format, resolution, and frame rate](https://learn.microsoft.com/en-us/windows/apps/develop/camera/set-media-encoding-properties), [CapturePhotoToStorageFileAsync](https://learn.microsoft.com/en-us/uwp/api/windows.media.capture.mediacapture.capturephototostoragefileasync?view=winrt-28000), [LowLagPhotoCapture](https://learn.microsoft.com/en-us/uwp/api/windows.media.capture.lowlagphotocapture?view=winrt-26100) |
| Detect topology changes | Create a `DeviceWatcher` for `DeviceClass.VideoCapture` and handle `Added`, `Updated`, and `Removed` | The watcher performs initial enumeration and continues reporting changes. Microsoft says clients must subscribe to all three change events. [DeviceWatcher](https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.devicewatcher?view=winrt-26100), [CreateWatcher](https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.deviceinformation.createwatcher?view=winrt-26100) |
| Detect an open-device failure | Handle `MediaCapture.Failed` and exclusive-control status changes | Device presence and a healthy capture session are separate signals. Another app taking control can fail the active capture; loss of exclusivity also needs a visible operator state. [Camera preview access](https://learn.microsoft.com/en-us/windows/apps/develop/camera/simple-camera-preview-access), [exclusive-control status](https://learn.microsoft.com/en-us/uwp/api/windows.media.capture.mediacapture.capturedeviceexclusivecontrolstatuschanged?view=winrt-28000) |

The booth does not need microphone access. Keep `StreamingCaptureMode` at `Video` and declare/request only camera access.

## Camera identity stored by an Event

Store a camera binding with the Event, not just the friendly name:

- `videoDeviceInterfaceId`: the selected `DeviceInformation.Id`, used to reopen the camera;
- `deviceInstanceId`: requested as `System.Devices.DeviceInstanceId`;
- `containerId`: requested as `System.Devices.ContainerId`;
- `displayName`: the last observed `DeviceInformation.Name`, for the operator UI only.

`DeviceInformation.Id` returned by the usual enumeration is a **device-interface identifier**, which is the value `VideoDeviceId` expects. `System.Devices.DeviceInstanceId` identifies its parent PnP device, and `System.Devices.ContainerId` groups functions belonging to one physical device. [DeviceInformation.Id](https://learn.microsoft.com/en-us/uwp/api/windows.devices.enumeration.deviceinformation.id?view=winrt-28000), [device information properties](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/device-information-properties)

At **Start Event**, re-enumerate and match the exact interface ID first. If it is absent, use instance/container metadata only to offer a likely replacement to the operator; auto-bind only when the match is unambiguous. Require explicit re-selection when multiple candidates match.

This caution matters because Windows guarantees a device instance ID only to persist across restarts of that system. USB cameras are not required to have serial numbers, and Microsoft notes that a device without one can be set up as new when moved to another USB port. Container IDs can also be generated rather than supplied by the device; an internal camera may inherit the computer's container. No single generic property is therefore a portable, permanent camera serial number. [Device instance IDs](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/device-instance-ids), [Microsoft USB serial-number test guidance](https://learn.microsoft.com/en-us/windows-hardware/test/hlk/testref/0f2d5113-cf70-4cda-8afc-b7005d1e2739), [USB container-ID assignment](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/how-usb-devices-are-assigned-container-ids)

## Start Event and runtime readiness contract

Enable **Start Event** only after the Event's configured camera passes all of these checks:

1. its binding resolves to exactly one currently enumerated video-capture interface;
2. camera privacy access is allowed;
3. `MediaCapture.InitializeAsync` succeeds with exclusive control;
4. an acceptable photo format and a compatible preview format are present;
5. a fresh preview frame is received; and
6. the Event/Guest Cycle output location is writable.

During a Guest Cycle, a matching `DeviceWatcher.Removed`, `MediaCapture.Failed`, loss of exclusive control, a stale preview, or a capture failure should immediately block new captures and move the booth to operator recovery. Preserve completed files. On `Added`/reconnect, dispose the stale capture object, re-enumerate, resolve the Event binding again, create and initialize a new `MediaCapture`, restore the chosen stream settings, and require a fresh preview frame before retry. This recovery sequence is an architectural recommendation built on Microsoft's change/failure notifications; Microsoft does not promise that hot-unplug preserves or revives an existing `MediaCapture` instance.

## Permissions and deployment

- A packaged WinUI 3 app must declare `<DeviceCapability Name="webcam" />` in `Package.appxmanifest`.
- An unpackaged desktop app has no webcam manifest capability; access is governed by **Let desktop apps access your camera** in Windows Settings.
- In either case the user can deny access. `MediaCapture.InitializeAsync` then returns `E_ACCESSDENIED`; FotoHAVN should explain the problem, link the operator to `ms-settings:privacy-webcam`, and retry initialization after the setting changes. [Microsoft: desktop MediaCapture prerequisites](https://learn.microsoft.com/en-us/windows/apps/develop/camera/handle-device-orientation-with-mediacapture), [Microsoft: handle camera privacy](https://learn.microsoft.com/en-us/windows/apps/develop/camera/camera-privacy-setting)

## UVC and vendor-mode constraints

Windows ships the `Usbvideo.sys` UVC driver. A compliant UVC device can work without a vendor driver and is exposed to Media Foundation; the inbox driver supports streaming formats and UVC still-image methods. That is the expected plug-and-play path. [Microsoft UVC driver overview](https://learn.microsoft.com/en-us/windows-hardware/drivers/stream/usb-video-class-driver-overview)

However, the application boundary is **Windows video capture**, not “UVC only.” A vendor driver can also expose an eligible capture interface. Conversely, USB presence, PTP file transfer, or a proprietary DSLR remote-control mode does not qualify by itself. FotoHAVN gains portability by accepting only what the active Windows capture driver reports, but gives up assumptions about autofocus, exposure, flash, optical zoom, native sensor resolution, RAW capture, battery, or storage status. Those controls are optional and must be capability-checked per device; vendor-specific extension controls are not portable. Microsoft explicitly documents that camera profiles are not supported on all devices and that preview/photo streams and supported formats vary. [Camera profiles](https://learn.microsoft.com/en-us/windows/apps/develop/camera/camera-profiles), [stream properties](https://learn.microsoft.com/en-us/windows/apps/develop/camera/set-media-encoding-properties)

## Required field-test proof

Use the exact booth PC and Windows build to test at least its integrated camera and one third-party plug-and-play camera:

1. enumerate both, select each in Event setup, restart FotoHAVN, and resolve the saved binding;
2. move the external camera between USB ports and verify that changed identity produces a safe re-selection prompt rather than silently choosing another camera;
3. show preview and take four consecutive JPEGs; record actual preview/photo formats, latency, orientation, mirroring, and output quality;
4. unplug during preview, countdown, and file capture; reconnect and prove a newly initialized session can capture without restarting FotoHAVN;
5. deny/restore Windows camera privacy access;
6. contend with the Windows Camera app or another client holding exclusive control; and
7. sleep/resume and repeat the readiness contract.

The minimum acceptance bar should be based on the composition's pixel and aspect-ratio needs, not a camera brand. Until those minimums are fixed, “Windows can enumerate it” is necessary but not sufficient for field-test compatibility.
