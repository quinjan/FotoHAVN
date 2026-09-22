# Canon EOS R100 control and monitoring on Windows

Research date: 2026-08-04

Scope: the fixed Canon EOS R100 used by the first FotoHAVN field-test build

Question: which officially supported integration path can provide discovery/readiness, live view, tethered capture, local image transfer, and disconnect/reconnect monitoring?

## Decision-ready answer

Use Canon's **EOS Digital Camera SDK (EDSDK) over a wired USB connection**. Do not build the booth around EOS Utility, EOS Webcam Utility/UVC, CCAPI over Wi-Fi, or Windows device presence alone.

This is the only Canon integration path publicly documented as all of the following:

- an SDK intended to be embedded in developer software;
- compatible with the EOS R100;
- able to use wired USB on Windows;
- able to provide remote still shooting, live-view images, image transfer, camera information, and camera status.

Canon added EOS R100 support in EDSDK 13.17.0 on 2023-05-25. The current public Canon South & Southeast Asia release page lists EDSDK 13.20.21, released 2026-05-12. FotoHAVN should acquire and pin an accepted current package rather than start from 13.17.0. [Canon EDSDK release notes](https://asia.canon/en/campaign/developerresources/camera/cap/edsdk-eos-digital-camera-sdk-release-note)

Canon explicitly lists the EOS R100 as supporting EDSDK camera control and describes EDSDK as a high-speed wired-USB path for Windows. [Canon Australia EDSDK overview and compatibility list](https://www.canon.com.au/apps/eos-digital-software-development-kit), [Canon South & Southeast Asia CAP overview](https://asia.canon/en/campaign/developerresources/camera/cap/cap)

## What EDSDK can cover

| FotoHAVN need | Canon-owned evidence | Consequence for the field-test design |
| --- | --- | --- |
| Discover and identify the camera | Canon lists acquisition of model name, serial number, battery, recording-media availability, and attached-lens status. Its public function summary also lists SDK initialization/termination and camera connection/disconnection. [CAP main functions](https://asia.canon/en/campaign/developerresources/camera/cap/cap), [Canon EDSDK function summary](https://www.canon.com.cn/supports/sdk/icp/template/EDSDKdetail.pdf) | EDSDK enumeration and a successfully opened camera session should be the source for camera identity. Windows Device Manager presence alone is not readiness. |
| Show mirrored live preview | Canon lists acquisition of live-view images, display metadata, and image-size control. [CAP live-view functions](https://asia.canon/en/campaign/developerresources/camera/cap/cap) | Retrieve live-view frames through EDSDK. Mirror only the on-screen rendering; keep transferred still files unmirrored for the strip. |
| Trigger each of four still captures | Canon lists still-image shooting, AF, AE, AWB, and focus control. The EOS R100 is not marked with the remote-capture limitation applied to some older EOS M models. [CAP remote-shooting functions](https://asia.canon/en/campaign/developerresources/camera/cap/cap), [Canon Australia compatible products](https://www.canon.com.au/apps/eos-digital-software-development-kit) | Send the still-capture command only after the five-second countdown and when the camera state machine is ready. |
| Save originals locally | Canon lists transfer of camera images to a computer, acquisition of image data, and setting the storage location of a shot image. [CAP image-transfer functions](https://asia.canon/en/campaign/developerresources/camera/cap/cap), [Canon EDSDK function summary](https://www.canon.com.cn/supports/sdk/icp/template/EDSDKdetail.pdf) | Configure a host transfer destination and count a capture as complete only after its JPEG is fully written and validated at the cycle's local path. Whether to also retain a copy on the camera card remains a design choice. |
| Detect loss of control during a session | Canon's function summary lists camera connection/disconnection and acquisition of camera state/events. [Canon EDSDK function summary](https://www.canon.com.cn/supports/sdk/icp/template/EDSDKdetail.pdf) | Use EDSDK session/event/error results plus live-view freshness, not a separate Windows USB-presence flag, to drive `Ready`, `Disconnected`, and `Recovering` states. |

Canon's public pages do **not** specify the exact event ordering or reconnect guarantees for an EOS R100 hot-unplug. Those details live in the licensed API specification and still need an on-rig test. Therefore, public evidence supports the integration choice, but not a claim that one callback is an authoritative, race-free “camera ready” signal.

## Recommended readiness contract

“Connected” and “ready for a Guest Cycle” should be different states.

At application startup, mark the camera **ready** only after all of these operations succeed:

1. initialize the accepted EDSDK package;
2. enumerate an EOS R100 and select the expected unit (prefer model plus body serial, not list position);
3. open a control session;
4. read identity and the camera status/properties exposed for that body;
5. configure still-image destination/quality for the field test;
6. enter remote live view and receive a fresh frame;
7. verify that the cycle output directory is writable.

During the active application session:

- keep processing EDSDK events and treat SDK/session errors or stale live-view delivery as an immediate loss of readiness;
- stop issuing new capture commands when readiness is lost;
- preserve every original already written locally;
- show the agreed “Please call the operator” recovery screen;
- after the cable/camera returns, discard the stale session, enumerate again, open a new session, replay the configuration, and prove liveness with a new live-view frame before enabling **Retry**.

This contract is an architectural recommendation inferred from Canon's advertised capabilities. The exact API calls, callback names, timeouts, and resource-release order must be taken from the licensed EDSDK API specification and samples, then verified with the physical R100.

## Capture and transfer boundary

For FotoHAVN, the useful completion signal is not merely “the shutter command returned.” A capture should become one of the four accepted originals only when:

1. the camera reports the newly created image through EDSDK;
2. its transfer completes successfully;
3. the target file is closed and can be decoded as the expected JPEG;
4. FotoHAVN durably associates it with the current cycle and capture index.

This boundary prevents composition from beginning with a partial file. It also leaves one unresolved recovery case: if the camera exposes the shutter but disconnects before transfer completion, FotoHAVN must decide whether **Retry** resumes/reconciles that image or takes the current numbered photo again. Canon's public material does not define enough event ordering to settle that case without a hardware spike.

## Windows, bitness, and native-runtime constraints

- Canon's CAP page lists **Windows 10 and Windows 11**, both 32-bit and 64-bit, for EDSDK in general. Canon stopped Windows 8.1 support in EDSDK 13.16.20 and Windows 7 support in EDSDK 13.13.20. [CAP supported operating systems](https://asia.canon/en/campaign/developerresources/camera/cap/cap), [EDSDK release notes](https://asia.canon/en/campaign/developerresources/camera/cap/edsdk-eos-digital-camera-sdk-release-note)
- Target **Windows x64** for the field build. The current public pages do not provide a model-specific standard-EDSDK bitness table for the R100, so the accepted package must confirm the shipped camera-control binaries. If the app loads EDSDK in-process, its architecture must match the native SDK DLL: Windows cannot load a 32-bit DLL into a 64-bit process or vice versa. [Microsoft process interoperability](https://learn.microsoft.com/en-us/windows/win32/winprog64/process-interoperability)
- Canon's separate EDSDK-with-RAW compatibility sheet lists the R100 only in the Windows 64-bit column and notes that HEIF and white-balance coefficients are unsupported for that RAW component. FotoHAVN does not need the RAW-development component for the JPEG field-test flow. [Canon EDSDK-with-RAW compatibility sheet](https://developercommunity.usa.canon.com/resource/1670450894000/CDC_EDSDKRAW_Compat_List)
- Canon's public pages do not identify the exact Microsoft Visual C++ runtime, .NET target, DLL set, or installer prerequisites for the current Windows package. Treat the accepted package's readme/license as authoritative and test installation on a clean field PC.

## Physical camera constraints that affect readiness

- The R100's USB terminal is USB Type-C but only Hi-Speed USB (USB 2.0), intended for PC communication. [EOS R100 specifications](https://cam.start.canon/en/C015/manual/html/UG-11_Reference_0080.html)
- USB cannot charge or power the R100. Canon supports continuous AC power with AC Adapter AC-E6N plus DC Coupler DR-E18 (or ACK-E18). [EOS R100 specifications](https://cam.start.canon/en/C015/manual/html/UG-11_Reference_0080.html), [Canon EOS R100 support](https://www.usa.canon.com/support/p/eos-r100)
- Canon says an interface-cable device cannot be used while the camera is actively connected through Wi-Fi; terminate Wi-Fi before wired operation. The field-test camera should therefore use wired USB with Wi-Fi disabled. [EOS R100 troubleshooting guide](https://cam.start.canon/en/C015/manual/html/UG-11_Reference_0040.html)
- The R100 does not support USB Video Class, so a generic Windows webcam/UVC integration is not a substitute for EDSDK live view and still capture. [Canon EOS R100 support](https://www.usa.canon.com/support/p/eos-r100)

For a booth session, the AC adapter/coupler is the more reliable assumption than an LP-E17 battery because the data cable provides no power. FotoHAVN may still display EDSDK-exposed power status, but the exact R100 property and a block/warn threshold must be verified on the rig.

## SDK acquisition, license, and redistribution

For a Philippine developer, acquisition is not an anonymous package download:

- Canon South & Southeast Asia accepts Philippine applications, but the applicant must be a legally registered entity; personal/non-business email applications are not considered. Canon says review usually takes 2–4 weeks and acceptance is not guaranteed. [Canon Digital Camera SDK application page](https://asia.canon/en/campaign/developerresources/camera)
- Canon describes CAP/EDSDK as free, but use is governed by an approved application and license. [CAP overview](https://asia.canon/en/campaign/developerresources/camera/cap/cap)
- The current regional terms grant a non-exclusive, revocable license for the purpose in the application, object-code use/distribution only, and use within the named country/region. [Canon SDK terms](https://asia.canon/en/campaign/developerresources/terms-conditions-for-digital-camera-software-development-kit-sdk)
- SDK object code may be distributed only as part of FotoHAVN for use with Canon products. The terms prohibit distributing the SDK separately, creating development tools around it, reverse engineering/modification, and unauthorized disclosure of confidential SDK information. The license is one year with automatic one-year renewals unless a listed termination condition occurs. [Canon SDK terms](https://asia.canon/en/campaign/developerresources/terms-conditions-for-digital-camera-software-development-kit-sdk)

Do not commit Canon SDK binaries, headers, gated documentation, or sample code to a public repository. Before shipping an installer, record exactly which files the accepted agreement/package permits FotoHAVN to redistribute. Expansion outside the country/region named in the application requires licensing review rather than an engineering assumption.

## Samples and implementation starting point

Canon says the EDSDK package includes an API specification, a function library, and Windows samples in VB, C++, and C#. The release history also records addition of a C# sample. [CAP SDK package contents](https://asia.canon/en/campaign/developerresources/camera/cap/cap), [EDSDK release notes](https://asia.canon/en/campaign/developerresources/camera/cap/edsdk-eos-digital-camera-sdk-release-note)

Use the sample that matches the chosen Windows stack to prove the unmodified SDK/package against the R100 first. Only then wrap the minimum camera operations behind a FotoHAVN camera boundary. The public Canon pages are inconsistent about sample inventory (an older generic page says VC++ only), so the accepted current package, not a web-page language list, is the final authority.

## Required on-rig proof before architecture is final

The integration decision is EDSDK; the following behavior remains empirical and should be one focused prototype using the exact R100, cable, Windows field PC, and accepted SDK version:

1. enumerate and open the R100 from a clean Windows 10/11 x64 install;
2. verify identity, live-view resolution/frame cadence, UI-only mirroring, AF, four captures, and host JPEG transfer;
3. establish which properties are reliably available for basic power, storage, shooting mode, and readiness;
4. hot-unplug/reconnect while idle, in live view, during countdown, immediately after shutter command, and during image transfer;
5. power-cycle the camera and verify that a fresh session can be opened without restarting FotoHAVN;
6. test absence/full/write-protected card under the selected host-only or host-plus-card save policy;
7. confirm behavior if EOS Utility or another Canon application already owns the camera;
8. identify exact native DLLs, runtime prerequisites, thread/callback rules, redistribution files, and cleanup sequence from the accepted package;
9. verify long-running operation with Wi-Fi off, camera power saving configured, and AC-E6N + DR-E18 power.

## Newly surfaced decisions

1. **SDK eligibility and lead time:** which legally registered Philippine entity applies for EDSDK, and can the 2–4-week approval window fit the MVP schedule?
2. **Field power:** require AC-E6N + DR-E18/ACK-E18 for the field-test rig, or explicitly accept battery depletion as a test risk?
3. **Image retention:** save captures to host only, or to both host and camera card?
4. **Ambiguous capture recovery:** after disconnect between shutter and completed transfer, should Retry reconcile/download the existing image or take that numbered photo again?
5. **Implementation stack:** choose the Windows language/runtime after the current Canon samples and redistribution package are inspected; whichever stack is chosen, publish an x64 build that matches the native EDSDK binaries.

## Source ownership and confidence

All product and SDK claims above come from Canon-owned documentation; the DLL-bitness rule comes from Microsoft. No third-party wrapper, reverse-engineered protocol, forum answer, or unofficial SDK mirror was used. Recommendations are labeled as such. Exact API names and event semantics are intentionally not asserted because the current Canon API specification is supplied only through the approved SDK process.
