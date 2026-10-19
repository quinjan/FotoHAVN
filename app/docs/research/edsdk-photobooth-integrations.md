# How photobooth software drives Canon cameras through EDSDK

Research date: 2026-10-19

Scope: how LumaBooth for Windows and open-source photobooth and tethering projects connect Canon EOS cameras through Canon's EOS Digital Camera SDK (EDSDK). This note informs [ADR 0009](../adr/0009-canon-eos-cameras-through-edsdk-beside-the-webcam.md). For Canon's own claims about the EOS R100, see [`canon-eos-r100-windows.md`](canon-eos-r100-windows.md).

This note is evidence, not authority. It describes SDK operations in plain words, not by Canon identifier, for the reason [`canon-eos-r100-windows.md`](canon-eos-r100-windows.md) gives: the exact API specification comes only through Canon's approved SDK process.

## LumaBooth for Windows (formerly dslrBooth)

- In 2024, dslrBooth was renamed LumaBooth for Windows. LumaBooth for iOS/iPadOS/macOS is a separate product. [dslrBooth announcement](https://dslrbooth.com/blog/dslrbooth-is-now-lumabooth-for-windows-introducing-version-8-0)
- The Windows product connects Canon cameras by **USB only**: the app recognizes a supported camera when it's plugged in. It documents no Wi-Fi, CCAPI, or HDMI capture path. [Product FAQ](https://dslrbooth.com/dslrBooth-Photo-Booth-Software)
- The **Canon R100 is supported from version 7**. [Supported cameras](https://support.lumasoft.co/en/articles/12831559-lumabooth-for-windows-dslrbooth-supported-cameras)
- The LumaBooth Apple-app guide recommends the R100 for photo-only booths. It points to the R50 for video and boomerang because of its better autofocus. [Camera setup guide](https://support.lumasoft.co/en/articles/12831712-lumabooth-supported-cameras-and-camera-setup-guide-for-webcam-canon-sony-and-nikon)
- **Recommended camera settings:**
  - JPEG, not RAW.
  - One-shot AF.
  - Single-shot drive.
  - Manual exposure.
  - Live-view shooting on, with Quick live-view AF.
  - A long metering timer.
  - HDR, creative filters, and Auto Lighting Optimizer off.
  - Auto power off disabled.
  - A card that is formatted or holds few files.

  Sources: [Supported cameras](https://support.lumasoft.co/en/articles/12831559-lumabooth-for-windows-dslrbooth-supported-cameras), [Camera not connecting](https://support.lumasoft.co/en/articles/12831690-camera-not-connecting-or-disconnects)
- **Known issues:**
  - Canon EOS Utility must not run alongside the app. [EOS Utility](https://support.lumasoft.co/en/articles/12831587-canon-eos-utility)
  - With flash, live view looks dark until exposure simulation is turned off. [Dark live view](https://support.lumasoft.co/en/articles/12831695-live-view-is-not-working-or-is-dark)
  - On recent AMD Ryzen laptops, Canon mirrorless bodies transfer the first photo, then fire but never transfer. The documented fix is a USB 2.0 hub between camera and laptop. [AMD transfer stall](https://support.lumasoft.co/en/articles/15062979-canon-camera-stops-downloading-after-the-first-photo-on-amd-laptops)

## Open-source projects

| Project | Binding | What it shows |
| --- | --- | --- |
| [thrixton/EDSDK.NET](https://github.com/thrixton/EDSDK.NET) | C# P/Invoke | The clearest reference. Every SDK call is serialized onto one dedicated STA thread, with long tasks on their own threads. It has a live-view loop, transfer-request handling, and a capacity declaration. |
| [dukus/digiCamControl](https://github.com/dukus/digiCamControl) (`Canon.Eos.Framework`) | C# P/Invoke | A reference-counted SDK lifetime: initialize on first use, terminate on last release. |
| [napi-canon-cameras](https://github.com/jamescrosslin/napi-canon-cameras) (mirror of the removed `dimensional-de` original) | Node-API C++ | Built for a photobooth. It sets save-to-host and large fine JPEG, delivers images through a download-request event, and leaves the SDK out of the package. |
| [sarraelhadj/photobooth_desktop_app](https://github.com/sarraelhadj/photobooth_desktop_app) | Qt/C++ | Without a native message pump it polls the SDK's event function itself, and it polls live-view frames on a timer because live view has no push callback. |
| [jangocheng/EVNTR](https://github.com/jangocheng/EVNTR) | C# WPF | A photobooth that **commits Canon's DLLs to a public repository**. This is the anti-pattern to avoid. |
| [pibooth/pibooth](https://github.com/pibooth/pibooth) | Python, gphoto2 | A contrast: an open PTP stack on Linux, with no Canon SDK. |

## The common pattern

1. **One SDK thread.** One thread initializes the SDK, makes every SDK call, and keeps camera events flowing, through a Windows message loop or by polling the SDK's event function. It terminates the SDK at exit.
2. **Discovery.** List the cameras, open a session with the chosen one, and register state, object, and property handlers. A camera-added handler signals hot-plug.
3. **Configuration.**
   - Save images to the host.
   - Declare free host capacity to the camera, which saving to the host requires.
   - Choose JPEG size and quality.
   - Optionally lock the camera's own controls.
4. **Live view.** Route the electronic viewfinder to the PC, then download one frame at a time in a loop. Each frame is a JPEG. Skip "not ready" answers, and route the viewfinder back to the camera when stopping.
5. **Capture.** Press the shutter (fully, with or without AF, or after a half-press), then release it. When the camera raises its transfer request for the new image, download it to memory or a file and mark the transfer complete.
6. **Robustness.**
   - Treat the shutdown state event as a disconnect: tear down, then enumerate again.
   - Retry "busy" answers.
   - Stop live view before movie recording.
   - Disable auto power off in the camera; no reviewed project extends the shutdown timer from software.

## Licensing in practice

- Well-behaved projects don't ship Canon's files: users must join Canon's developer programme and unpack the SDK locally ([napi-canon-cameras README](https://github.com/jamescrosslin/napi-canon-cameras/blob/master/README.md), [photobooth_desktop_app README](https://github.com/sarraelhadj/photobooth_desktop_app/blob/main/README.md)).
- FotoHAVN's repository is public, so the same rule applies to it, and Canon's terms also restrict disclosure of confidential SDK information ([Canon SDK terms](https://asia.canon/en/campaign/developerresources/terms-conditions-for-digital-camera-software-development-kit-sdk)).

## Checked against the SDK package

The owner's accepted package, EDSDK 13.20.21 (release note dated 2026-05-12), settles several points. It is kept outside the repository, and this note paraphrases it rather than quoting it.

- Its API reference lists the EOS R100 for camera control, and its revision history records R100 support from 13.17.0 (2023-05-25).
- The package ships 32-bit and 64-bit Windows DLLs. The 64-bit `EDSDK.dll` is file version 13.20.20.6402 and `EdsImage.dll` is 3.19.0.6400. It also has C#, VB, and C++ samples, including a C++ console sample.
- On Windows, any thread other than the main thread must initialize COM as single-threaded apartment before touching a camera. Event callbacks run on the thread that opened the session. A console application must call the SDK's event function regularly; Canon's own console sample does so every 200 ms.
- Canon's C# sample retries a command that failed because the device was busy after about 500 ms, because some cameras become unstable when commands are reissued back to back.
- Routing live view to the PC only locks the camera's own controls except SET.
- The capture-error event and the take-picture error list distinguish focus failure, no card, card error, write-protected card, missing lens, and similar causes.
- On R-series bodies other than the EOS R and RP, auto power-off can be read and disabled through the SDK. Power-saving settings don't take effect during a USB connection.
- The package's readme asks distributors of executable code to state that the software is based in part on the work of the Independent JPEG Group.

## Not verified here

- R100 live-view frame size and rate over USB 2.0, shutter latency, transfer time, and hot-unplug event order. These need the on-rig spike in ADR 0009.
