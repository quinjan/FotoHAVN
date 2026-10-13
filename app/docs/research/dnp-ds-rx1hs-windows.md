# DNP DS-RX1HS printing and status on Windows

Original research date: 2026-08-04

Open-source and hardware-free test update: 2026-09-22
Resolves: [#3](https://github.com/quinjan/FotoHAVN/issues/3)

## Decision summary

FotoHAVN can produce the required two identical 2x6 strips by sending one borderless 6x4 page through the installed DNP Windows printer driver with `2inch cut = Enable`. For the field-test build, compose an exact 1844 x 1240 pixel, 300 x 300 dpi **Print Sheet**, place the two identical Photo Strips in its two 1844 x 620 halves, and submit it as a single-page job to the local `DS-RX1` queue. Use the DNP driver's `Paper Size = (6x4)`, `Orientation = Portrait`, `Border = Disable`, `2inch cut = Enable`, and `Print Re-try = Disable` settings.

The production integration should be a small Windows-spooler adapter behind a FotoHAVN-owned printer boundary, not a raw-USB implementation copied from Linux and not either of the small GitHub C# projects reviewed below. Keep Print Sheet composition and the Guest Cycle state machine in testable FotoHAVN code; isolate DNP driver/queue calls and eventual licensed status calls in the Windows adapter.

That path is supported for cutting and queue submission. It does **not** yet support the product requirement to return to Start only after both strips have physically exited. Windows job completion can mean only that data reached the printer, and DNP's public material does not state that its port/language monitor implements Windows `TrueEndOfJob`. DNP publishes a status application that can show `Waiting`, `Printing`, media remaining, and specific faults, but it is a standalone utility/sample, not a documented or licensed application API. A hardware prototype or a supported DNP SDK agreement must therefore settle physical completion and in-process device status.

## 1. Media, cut, raster, orientation, border, and bleed

### Confirmed output path

- The DS-RX1HS product page explicitly lists two 2x6 strips on one 4x6 sheet and identifies the 4x6 media as `RX1HS4x6`. It lists 300 dpi as the printer resolution and about 12 seconds for a 2x6-strip print. [DNP DS-RX1HS product page](https://www.dnpphoto.com/products/printers/rx1hs)
- The driver guide says `2inch cut = Enable` cuts `(6x4)` or `PR(4x6)` output into two sheets, and `(6x8)` output into four. It also warns that older firmware can expose only `Disable`, so the field rig must verify that `Enable` is actually present. [DNP RX1HS Windows driver guide, section 2.2.2](https://www.dnpphoto.com/Portals/0/Resources/DSRX1HS_PrinterDriverGuide_For7%2C8%2C10_V1.13_English.pdf)
- The current hardware guide identifies PC media as 152 mm wide, connects by shielded USB 2.0, and says to remove 2x6 output from the tray frequently. [DNP DS-RX1HS/RX1 User Guide v3.02, pp. 12-17](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_UserGuide_ver.3.02_English.pdf)

### Exact raster for the MVP

Use high-speed mode and render the complete print sheet, not two separate Windows jobs:

| Driver setting | Field-test value |
| --- | --- |
| Paper size | `(6x4)` |
| DNP orientation name | `Portrait` |
| Application raster | 1844 x 1240 pixels |
| Print quality | 300 x 300 dpi |
| Border | `Disable` |
| 2-inch cut | `Enable` |
| Pages per sheet | `1` |
| Copies | `1` |
| Print re-try | `Disable` |

The DNP print-area table maps `(6x4)`, Portrait, 300 x 300 dpi to 1844 x 1240 pixels and 156.1 x 105.0 mm. It maps the same page at 300 x 600 dpi to 1844 x 2480 pixels; 600 x 600 is converted inside the driver to 300 x 600. [DNP RX1HS Windows driver guide, sections 2.2.2 and 2.5](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_PrinterDriverInstruction_For7%2C8%2C10_V1.13_1_English.pdf)

The 156.1 x 105.0 mm driver image area is 4.1 mm larger on each axis than the nominal 152 x 101 mm finished sheet. The reasonable inference is about 2.05 mm of symmetric borderless overprint on each outer edge, but DNP does not label this difference as a guaranteed bleed allowance. Use the exact 1844 x 1240 raster to avoid driver scaling; extend the current white background to every raster edge.

The current compositor produces one lossless 600 x 1800 PNG Photo Strip on a white background. For the first Print Sheet implementation, rotate that image 90 degrees to 1800 x 600 without resampling, duplicate it into one 1800 x 1200 content block, and center that block in the 1844 x 1240 driver raster. Place the first copy at `(22, 20)` and the second at `(22, 620)`. This leaves 22 white pixels at the two 6-inch outer edges and 20 white pixels only at the top and bottom outer edges; it introduces no artificial gap at the center cutter seam. This is a codebase-fit inference from nominal 6 x 4 inches at 300 dpi versus DNP's larger driver raster, not a DNP safe-zone specification. The current dimensions and white background are defined by [`PhotoStripGeometry`](../../src/FotoHavn.Core/PhotoStripGeometry.cs) and [`PhotoStripCompositor`](../../src/FotoHavn.App/PhotoStripCompositor.cs).

The logical cut seam is between raster rows `619` and `620`. Because DNP publishes no cutter-position tolerance, kerf, or safe-zone specification, the exact pixels preserved next to the center cut and outer trims remain an empirical question. Keep the existing white background through every unused Print Sheet pixel, and do not place critical photo or text content against an unverified edge.

The driver guide says it enlarges or reduces the application image to the selected print area and handles the output direction from the chosen orientation. Supplying the exact documented raster and using `(6x4)` Portrait avoids a second application-side rotation or rescale. [DNP RX1HS Windows driver guide, sections 2.5-2.6](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_PrinterDriverInstruction_For7%2C8%2C10_V1.13_1_English.pdf)

### Color-management contract

Keep the Print Sheet in sRGB and assign exactly one color-management owner. For the first rig, leave FotoHAVN's pixels unadjusted, use the DNP-installed RX1 ICC through driver ICM, keep the driver's gamma/brightness/contrast/chroma at zero, sharpness at zero, and disable any second automatic correction. DNP documents `None`, driver adjustments, `ICM`, and `ICM & Driver`; the Windows 11 guide says the installed profile is associated by default and that ICM can be combined with additional driver adjustment. Using both application-side ICC conversion and driver/HFP ICM would risk a double transform. [DNP Windows 11 driver guide v1.23, sections 2.2.4-2.4](https://www.ansystem.com/GlobDownloads/DNP/Drivers/RX1_RX1HS/RX1_RX1HS_Driver_V1.23_Instruction_Win_11_En.pdf), [DNP official color-profile instructions](https://new.dnpphoto.com/Portals/0/Resources/DNP_Printer_Multi_OS_ICM_Update_March_2014.pdf)

An ordinary display or substitute printer can test the composition and a broad visual intent, but it cannot certify RX1HS dye-sublimation color, density, gloss/matte overcoat, or the selected ICC path. Freeze a reference sRGB test target now, then print that same target on the RX1HS during acceptance and record the driver/profile/version used.

## 2. Driver and queue submission path

### Recommended field-test integration

Use the installed DNP Unidrv Windows driver and the native Win32 GDI/spooler APIs. Do not make the MVP depend on Hot Folder Print or an undocumented DLL.

1. Install the official DNP driver as an administrator, connect the powered printer by USB, confirm the `DS-RX1` queue, and reboot after installation. The official guide covers 32- and 64-bit Windows driver packages and requires a reboot. [DNP RX1HS Windows driver guide, chapter 1](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_PrinterDriverInstruction_For7%2C8%2C10_V1.13_1_English.pdf)
2. Preconfigure the queue with the field-test settings above. `2inch cut` and `Print Re-try` are DNP private driver features, not portable public `DEVMODE` fields. Microsoft says a valid driver `DEVMODE` contains device-dependent private data whose size and contents vary by driver; applications should obtain and validate it with `DocumentProperties` rather than fabricate or edit the private bytes. [Microsoft: Modify printer settings with DocumentProperties](https://learn.microsoft.com/en-us/troubleshoot/windows/win32/modify-printer-settings-documentproperties)
3. Create a printer device context from that validated `DEVMODE`, call `StartDoc`, `StartPage`, draw the 1844 x 1240 bitmap into the full driver page, then call `EndPage` and `EndDoc` on a worker thread. Microsoft's GDI print API delegates bitmap drawing to the installed device driver, and `StartDoc` returns the Windows print job identifier that can be passed to `GetJob`. [Microsoft: About the GDI Print API](https://learn.microsoft.com/en-us/windows/win32/printdocs/about-the-gdi-print-api), [Microsoft: StartDoc](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-startdoca)
4. Subscribe to job/printer changes with `FindFirstPrinterChangeNotification`, then read the identified job with `GetJob`/`JOB_INFO_1` and the queue with `GetPrinter`/`PRINTER_INFO_2`. Keep blocking spooler calls off the UI thread. [Microsoft: printer change notifications](https://learn.microsoft.com/en-us/windows/win32/printdocs/findfirstprinterchangenotification)

DNP Hot Folder Print (HFP) is a genuine alternative integration surface, not only a manual utility. Its current guide has a "Third Party Application Integration" section: an application drops an image into `<InstallDir>\HotFolderPrint\Prints\<Print Size>\<Printer Name>\`, uses `s6x2_2` for a 4x6 composite cut into two 2x6 strips, and can read `<InstallDir>\HotFolderPrint\Logs\printer_status.txt` for connected-printer model, serial, media size, and remaining media. The folders are created and removed dynamically from attached hardware and loaded media. [DNP HFP v3.3 guide, section 17](https://dnpphoto.com/Portals/0/Resources/HFP_User_Guide_3.3.pdf)

HFP therefore deserves a short field spike as the lowest-effort DNP-supported path, especially for readiness and media reporting. It still weakens per-job correlation: the documented status file is printer-level, no application-supplied idempotency key or physical-ejection event is documented, and a consumed/moved input file is not proof that both strips exited. The direct spooler path remains the recommended FotoHAVN boundary when exact Windows job identity is required; HFP remains a viable separately installed adapter if the hardware spike shows its status and archive behavior are sufficient.

### Public driver-version evidence and deployment risk

- DNP's catalog labels its RX1/RX1HS Windows 10 package as `v3.21` for 32/64-bit Windows. The downloaded official package's `DSRX1.inf` reports `DriverVer=08/20/2014,1.1.1.0`; its payload contains x86 and x64 Unidrv components and a DNP language monitor. This appears to mean `v3.21` is a package/catalog release label, not the Windows driver binary version. [Official DNP Windows 10 package](https://www.dnpphoto.com/files/downloads/rx1_driver_win10_v3.21_32-64bit.zip)
- DNP's current v3.02 hardware guide lists Windows 7/8/10 in 32/64-bit form and Windows 11 as 64-bit compatible. [DNP DS-RX1HS/RX1 User Guide v3.02, p. 24](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_UserGuide_ver.3.02_English.pdf)
- DNP Europe now publishes a dedicated `DRIVER_RX1HS_WIN_11 v1.14` package, dated 2023-06-15, while DNP Imagingcomm America's legacy catalog still labels `v3.21` for Windows 10/8/7. These regional package labels are not interchangeable evidence of the installed binary version. Select the official regional source appropriate to the booth, then pin the download URL, SHA-256, Windows edition/build, installed driver version, printer firmware, and whether `2inch cut = Enable` is present. [DNP Europe RX1HS driver folder](https://www.dnpphoto.eu/en/support-and-downloads/drivers-tools/drivers/rx1hs-1), [DNP America legacy driver catalog](https://dnpphoto.com/en-us/support/legacy-equipment/legacy-drivers-and-tools)

The driver itself needs no FotoHAVN-managed runtime. DNP's separate RX1 Status Display Tool is a 32/64-bit common executable with 32- and 64-bit native DLLs and requires .NET Framework 2.0 or later (3.5 SP1 for Windows 8 in its 2015 manual). FotoHAVN should not depend on that utility's runtime because the utility is not the integration API. [DNP Status Display Tool v1.0 package](https://www.dnpphoto.com/files/downloads/status%20app%20for%20rx1%20and%20rx1hs%20v1.0.zip)

## 3. Open-source implementations reviewed on 2026-09-22

### Gutenprint: credible protocol evidence, but a Linux/GPL implementation

The current upstream Gutenprint tree contains an actively maintained `backend_dnpds40.c` CUPS backend for the DNP DS series. This is an actual libusb implementation, not a README-only claim:

- It binds the DS-RX1 family at USB VID/PID `0x1343:0x0005`, gives the RX1 a native width of 1920 pixels, and treats firmware `>= 2.00` as RX1HS. For RX1HS it requires media to return a media lot code, because the backend uses that as its HS-media check.
- It parses and emits DNP command groups such as `STATUS`, `INFO MEDIA`, `INFO MQTY_DEFAULT`, `INFO FREE_PBUFFER`, `CNTRL QTY`, `CNTRL BUFFCNTRL`, `CNTRL OVERCOAT`, `CNTRL CUTTER`, and `IMAGE MULTICUT`. Cutter value `120` is treated as the legacy 2-inch-cut mode and is accepted only for 6x4 or 6x8 jobs on firmware that advertises 2x6 support.
- It distinguishes idle/printing/cooling from cover-open, scrap-box, paper-end, ribbon-end, paper-jam, ribbon, paper-definition, data, and fatal errors. In normal wait mode it polls once per second and reports completion only after it has observed a non-idle state and then a return to status `0` (idle).
- It queries serial number, media type, remaining media, job quantity, free buffers, lifetime/user counters, sensor data, firmware, and color-control data. This independently corroborates that richer device state exists below the generic Windows queue.

Source: [Gutenprint upstream `backend_dnpds40.c`, commit `76b0f3571a1cf3d07fe9068e87840be94d28df5a`](https://sourceforge.net/p/gimp-print/source/ci/76b0f3571a1cf3d07fe9068e87840be94d28df5a/tree/src/cups/backend_dnpds40.c), especially the RX1 capability block near lines 1508-1525, cutter validation near 2819-2830, RX1HS media check near 2899-2916, command emission near 3079-3147, completion loop near 3178-3210, and USB table near 4618-4620.

This is useful evidence about device concepts and a possible future Linux print appliance. It is **not** the Windows pixel contract: Gutenprint uses a 1920-pixel native width, while DNP's official Windows driver table defines the FotoHAVN page as 1844 x 1240. It is also not code to copy into FotoHAVN casually. The backend is GPL-2.0-or-later, so incorporating or adapting its code in a distributed FotoHAVN binary would require a licensing review and may impose source-distribution obligations. Calling an independently installed, unmodified CUPS/Gutenprint service is a different architecture, but still adds a Linux/CUPS deployment and support surface. [Gutenprint license header](https://sourceforge.net/p/gimp-print/source/ci/76b0f3571a1cf3d07fe9068e87840be94d28df5a/tree/src/cups/backend_dnpds40.c#l16)

### `TiepHoangDev/DNP-Printer`: useful cautionary example, not an SDK

The MIT-licensed C# project submits through `System.Drawing.Printing.PrintDocument`; it is not a DNP protocol or SDK implementation. Source inspection shows that only orientation, color, copy count, and a paper-size name are applied. Its exposed `TwoInchCut`, `Quality`, `EnableBorder`, and `OvercoatFinish` settings are never applied to the driver. It also equates `PrinterSettings.IsValid` with `Ready`, searches paper sizes on a new default `PrinterSettings` rather than the selected queue, stretches images to `PageBounds`, and returns when `PrintDocument.Print()` returns. That does not establish DNP readiness, exact no-scale output, private cut/retry settings, a job identifier, or physical completion. [Printing implementation](https://github.com/TiepHoangDev/DNP-Printer/blob/a0712729882ad7c3d026556fb44c68b36f703135/DNP.Core/Services/DNPPrinter.cs), [unapplied settings model](https://github.com/TiepHoangDev/DNP-Printer/blob/a0712729882ad7c3d026556fb44c68b36f703135/DNP.Core/Models/PrintJobSettings.cs), [MIT license](https://github.com/TiepHoangDev/DNP-Printer/blob/a0712729882ad7c3d026556fb44c68b36f703135/LICENSE)

Do not import this project. The small amount of useful structure - an application-owned interface and a Windows-specific implementation - should be written against FotoHAVN's own Print Sheet and lifecycle contracts.

### `HungTran68/TEST-DNP-SDK`: do not copy or depend on it

This repository is valuable only as evidence that developers have experimented with DNP's native status DLLs. It is not a safe dependency:

- It commits `CyStat64.dll` and `cspstat64.dll`, identifies them as DNP/Snapbox SDK binaries, and has no visible repository license or included DNP redistribution terms. The public DNP download license does not give FotoHAVN permission to copy those binaries from GitHub.
- Although its interop comments claim direct image printing, its actual image path constructs `System.Drawing.Printing.PrintDocument`, draws the file to `PageBounds`, and calls `Print()`. It marks the job `Completed` immediately afterward, without tracking the Windows job or observing printer idle/ejection.
- If no queue name contains `DS-RX1`, it logs a warning and silently prints to the Windows default printer.
- If the DLL is unavailable, its mock reports a connected, ready RX1HS and every print advances through fixed sleeps to success. It cannot exercise failure, disconnection, retry ambiguity, media exhaustion, or completion ordering.

Sources: [`DnpPrinterService.cs` at commit `84edeb8d77ce34b96ef38732a3638af2938610f8`](https://github.com/HungTran68/TEST-DNP-SDK/blob/84edeb8d77ce34b96ef38732a3638af2938610f8/DNPPrintService/Services/DnpPrinterService.cs), [`CspStatInterop.cs`](https://github.com/HungTran68/TEST-DNP-SDK/blob/84edeb8d77ce34b96ef38732a3638af2938610f8/DNPPrintService/Interop/CspStatInterop.cs), [project file that packages the native DLL](https://github.com/HungTran68/TEST-DNP-SDK/blob/84edeb8d77ce34b96ef38732a3638af2938610f8/DNPPrintService/DNPPrintService.csproj).

### Public DNP SDK status

No public, RX1HS-specific SDK download, API reference, or redistribution license was found in DNP's official RX1HS driver/tool catalogs. DNP officially distributes the Windows driver, Status App, PrinterInfo, Hot Folder Print, firmware, ICC profiles, and manuals; that is not the same as granting an application integration license for the native DLLs found inside those packages. Treat the function names seen in third-party P/Invoke code as unverified until DNP supplies the matching SDK package, headers, documentation, supported-printer matrix, and written commercial/redistribution terms. [DNP America downloads](https://www.dnpphoto.com/downloads), [DNP Europe downloads](https://dnpphoto.eu/downloads/), [DNP support](https://www.dnpphoto.com/support)

Microsoft's own classic GDI sample is still a sound implementation reference for printer enumeration, device capabilities, a background printing thread, and GDI rendering, but it is deliberately printer-generic; it does not solve DNP private `DEVMODE`, device status, or physical completion. [Microsoft Windows classic GDI printer sample](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/multimedia/gdi/printer/Printer.c)

## 4. Queue acceptance is not physical completion

Windows exposes useful job states, but it does not make every driver's `Printed` state a physical truth:

- `JOB_STATUS_COMPLETE` means the job was sent to the printer and may not be printed yet.
- `JOB_STATUS_PRINTED` nominally means printed, but Microsoft explicitly says a port monitor that does not support `TrueEndOfJob` sets this status immediately after submission to the printer.
- `JOB_STATUS_PRINTING`, `OFFLINE`, `PAPEROUT`, `ERROR`, `BLOCKED_DEVQ`, and `USER_INTERVENTION` can describe intermediate or fault states.

Source: [Microsoft `JOB_INFO_1`](https://learn.microsoft.com/en-us/windows/win32/printdocs/job-info-1).

DNP does not publicly document whether the RX1HS language/port-monitor stack supports `TrueEndOfJob`. Queue disappearance, `EndDoc` success, `JOB_STATUS_COMPLETE`, and an unvalidated `JOB_STATUS_PRINTED` must not trigger `Your photo strips are ready!`.

DNP's official Status Display Tool demonstrates that the printer can report `Waiting` (printing possible), `Printing`, media size and remaining count, and faults. It polls once per second. While a spooler job exists, it instead shows `Printing (sending data)` and withholds other information; after the job leaves the spooler it resumes device information. Its documented statuses include door open, ribbon end, paper end, offline, head cooling, ribbon error, paper-size mismatch, paper jam, and system error. [DNP Status Display Tool v1.0 package and manual](https://www.dnpphoto.com/files/downloads/status%20app%20for%20rx1%20and%20rx1hs%20v1.0.zip)

This suggests a promising completion condition - the tracked queue job is gone and the device has transitioned from `Printing` to `Waiting` - but DNP documents it only as utility behavior. There is no public API contract saying that `Waiting` occurs only after cutter completion and physical ejection.

### Required hardware prototype

Before the Guest Cycle can be considered field-testable, run one focused prototype on the exact Windows PC, driver package, firmware, USB path, and RX1HS unit:

1. Log every status and timestamp from `StartDoc` job creation through queue removal and physical ejection.
2. Determine whether this DNP installation reports a trustworthy `JOB_STATUS_PRINTED`/TrueEndOfJob or a reliable `PRINTER_STATUS_PRINTING -> ready` transition.
3. Repeat for normal 2x6x2 output, cover open, paper/ribbon end, paper jam, USB removal, and printer power loss.
4. Confirm whether a documented DNP SDK is available under terms that allow FotoHAVN to read the same status used by the DNP utility. If not, obtain DNP's written integration guidance rather than calling the utility's private DLLs.
5. Accept a completion signal only when testing shows it changes after both cuts and final ejection. Do not substitute a fixed timer for this product requirement.

## 5. Readiness, media, offline, and jam signals

At startup and between Guest Cycles, FotoHAVN can safely require all of the following generic conditions:

- The intended local queue exists and uses the expected DNP driver and USB port.
- The queue is not paused and has no unresolved prior jobs.
- Windows does not report offline, not available, door open, paper out/problem/jam, error, or user intervention through `PRINTER_INFO_2`/`PRINTER_INFO_6`.
- The hardware prototype's supported DNP status source reports `Waiting`, correct 4x6 media, and at least one sheet remaining.

Windows defines the generic printer flags but their actual reporting depends on the driver/monitor. [Microsoft `PRINTER_INFO_2`](https://learn.microsoft.com/en-us/windows/win32/printdocs/printer-info-2), [Microsoft `PRINTER_INFO_6`](https://learn.microsoft.com/en-us/windows/win32/printdocs/printer-info-6)

DNP's public tools establish the device-level vocabulary and media data that exist, but not a redistributable API. DNP PrinterInfo lists media size/remaining plus `PAPEROUT`, `RIBBONOUT`, `COVEROPEN`, `RIBBONERR`, `PAPERERR`, `PAPERJAM`, `SCRAPBOXERR`, `MOTCOOLING`, `DATAERR`, `SYSTEMERR`, `HARDWAREERR`, `NOT_INITIALIZED`, and `OFFLINE`. [DNP PrinterInfo quick reference](https://new.dnpphoto.com/Portals/0/Resources/PrinterInfo%20Quick%20Reference.pdf)

The RX1HS user guide's front-panel fault table covers paper end, ribbon end, door open, paper error, ribbon error, system error, and automatic head cooling; its recovery instructions require removal of partial output after jams or interrupted cutting/printing. [DNP DS-RX1HS/RX1 User Guide v3.02, pp. 18-21](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_UserGuide_ver.3.02_English.pdf)

## 6. Retry semantics

Set the driver to `Print Re-try = Disable` for FotoHAVN's explicit `Please call the operator` -> fix media -> `Retry Print` flow.

DNP documents these semantics:

- With retry disabled, a printer error clears buffered print data; after recovery, the application must resend the image.
- With retry enabled, paper end, ribbon end, cover open, paper jam, ribbon error, paper-definition error, and data error can resume from printer-buffered data after recovery.
- Errors that require printer power cycling clear the buffer regardless of the setting; those include head-position, fan, cutter, head-voltage/temperature, media-temperature, ribbon-tension, RFID-module, and system errors.

Source: [DNP RX1HS Windows driver guide, section 2.2.2](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_PrinterDriverInstruction_For7%2C8%2C10_V1.13_1_English.pdf).

Disabling driver auto-retry gives FotoHAVN one owner of retry behavior and preserves the intended guest screen. Keep the saved 1844 x 1240 composite immutable. After the supported status source reports `Waiting` with correct media, submit a new one-copy job for the same composite.

There is no printer-side idempotency key. FotoHAVN must not offer Retry while the original job might still be buffered, printing, or automatically recovering, or it can produce duplicate strips. A lost/ambiguous completion signal after apparent output requires operator judgment; public DNP/Windows documentation provides no way to prove that no physical copy emerged.

## 7. Hardware-free test plan for FotoHAVN

Hardware-free testing can establish the **Print Sheet**, application state machine, adapter contract, Windows submission plumbing, and most error UX. It cannot qualify the RX1HS mechanism. Keep those claims separate.

### Layer A - deterministic Print Sheet tests (do now)

Add a pure `PrintSheetComposer` below `FotoHavn.Core`'s printer boundary and test it without a printer:

1. Feed it a deliberately asymmetric 600 x 1800 Photo Strip with labeled corners and a one-pixel orientation marker.
2. Assert an 1844 x 1240 opaque sRGB PNG, with the strip rotated exactly once to 1800 x 600 and duplicated byte-for-byte at `(22, 20)` and `(22, 620)`.
3. Assert every unused pixel is white, both copies are identical, the center seam has no gap, no content crosses the sheet bounds, and the source has not been stretched or re-compressed.
4. Digitally crop rows `0..619` and `620..1239`; overlay the expected cut line and outer finished-size guides. Save the full Print Sheet plus both simulated outputs as test artifacts so a reviewer can inspect them at 100%.
5. Add golden tests for text near safe areas, white/near-white edges, dark edges, skin-tone/color patches, very high contrast, and transparent input flattened onto white.
6. Store a manifest beside the artifact: pixel dimensions, source SHA-256, composed SHA-256, intended queue, paper size, orientation, border, quality, cut, retry, color-management owner, and copies. This makes every later physical comparison reproducible.

The current compositor's 96-DPI render-target metadata must not silently drive scaling. The submission adapter should render the exact pixel rectangle to the driver's exact printable pixel bounds. If metadata is retained, make it 300 x 300 dpi and assert it in tests.

### Layer B - fake printer adapter and fault scripts (do now)

Define the application boundary in FotoHAVN terms, for example: enumerate/bind a printer, query readiness, submit an immutable Print Sheet, observe the tracked job/device state, and return `Completed`, `RecoverableFailure`, `FatalFailure`, or `CompletionUnknown`. Implement an artifact-sink fake that saves the submitted bytes/manifest and runs from a deterministic virtual clock.

Script at least these sequences:

| Scenario | Required application result |
| --- | --- |
| ready -> accepted -> printing -> idle/completed | Leave printing only after the fake completion signal. |
| paper/ribbon end, cover open, jam | Enter Operator Assistance; preserve the immutable Print Sheet. |
| USB/offline before acceptance | Do not claim a print; allow retry after readiness is re-proven. |
| disconnect after acceptance | Mark completion unknown; never automatically resubmit and risk a duplicate. |
| application restart with an in-flight manifest | Require operator reconciliation; do not infer that no paper emerged. |
| stale completion from a previous job | Ignore it by correlation ID. |

Do not copy the `TEST-DNP-SDK` mock: a timer that always succeeds tests only a happy-path animation, not print ownership or recovery.

### Layer C - Windows print-pipeline tests without RX1HS (do now, with limits)

Use two complementary sinks:

- Submit the exact page to Microsoft Print to PDF/XPS or a test local-port printer and inspect the resulting page at 100%. Microsoft's print OEM sample documents a local test port that writes XPS, which is useful for detecting extra margins, wrong orientation, and unintended application scaling. It does not exercise DNP private features. [Microsoft print OEM sample: local-port test workflow](https://github.com/microsoft/print-oem-samples#test-this-sample)
- On a disposable Windows VM, install the official DNP package without redistributing it and try creating a DS-RX1 queue on `FILE:` or a file-backed local port. If the driver permits offline submission, capture the `.prn` bytes and retain the validated `DEVMODE`. Check that changing `2inch cut`, retry, border, quality, and orientation changes the captured job as expected. Searching for known strings such as `CNTRL CUTTER` or `IMAGE MULTICUT` is a diagnostic informed by Gutenprint, not an official Windows contract. It remains unknown whether this driver/language-monitor stack will work correctly without the USB device, so treat this as a spike rather than a promised test.

Gutenprint also has a no-device regression mode (`TEST_MODE=2`) and an RX1HS matrix that can drive PPD/PDF/CUPS-raster output through its parser without attaching hardware. Run it only as an independent protocol sanity check; passing it validates the Gutenprint/Linux path, not FotoHAVN's Windows driver bytes or DNP's licensing. [Gutenprint regression harness](https://git.shaftnet.org/gitea/slp/selphy_print/src/commit/70c2f3878f47ef7c72c99d590a95fe2d7bb83971/regression-gp.pl#L59), [RX1HS regression matrix](https://git.shaftnet.org/gitea/slp/selphy_print/src/commit/70c2f3878f47ef7c72c99d590a95fe2d7bb83971/regression-gp.csv#L593)

If HFP is evaluated, fake its folder and `printer_status.txt` in tests, verify atomic placement into `s6x2_2\<Printer Name>`, and replay ready/printing/error/media fixtures. Do not reuse the direct-driver 1844 x 1240 raster blindly: HFP documents a nominal 4x6 input as 1200 x 1800 pixels, so an HFP adapter has a different image-size contract and may perform its own rotate/scale/color pipeline. [DNP HFP v3.3 guide](https://dnpphoto.com/Portals/0/Resources/HFP_User_Guide_3.3.pdf)

### Layer D - a physical proof without the DNP (do now)

Generate a proof PDF that places the 1844 x 1240 raster at exactly 300 pixels per inch: about 156.13 x 104.99 mm. Overlay non-printing or separate-reference guides for the nominal 1800 x 1200 content block (152.4 x 101.6 mm), the center cut, outer finished edge, and a millimetre ruler.

Print it at **Actual size / 100%** on the best available color printer, preferably on 4x6 photo stock; otherwise use A4/Letter and trim manually. Also order one uncropped/no-enhancement 4x6 print from a local photo lab if practical, then cut it into two 2x6 strips at the center. Measure the physical dimensions, inspect orientation, compare the two halves, and check text/photo safety. This proves the composition is usable as a physical object and catches application scaling. It does not predict RX1HS transport, cutter position, borderless overprint, ICC color, overcoat, or ejection timing.

### Layer E - unavoidable RX1HS acceptance (later release gate)

Before enabling printing for guests, borrow, rent, or arrange remote/vendor access to the exact RX1HS/Windows rig and verify:

- one 1844 x 1240 Print Sheet produces two correctly oriented, identical 2x6 Photo Strips;
- measured outer trim and center-cut variation across a calibration batch;
- sRGB -> pinned RX1 ICC color, density, and glossy/matte output;
- job ID, printing, both physical ejections, and return-to-ready ordering;
- paper/ribbon end, cover open, jam, cooling, USB removal, power loss, recovery, and duplicate prevention;
- exact regional driver package/hash, installed binary version, firmware, media, Windows build, USB port/iSerial behavior, and private settings.

Until Layer E passes, the honest release status is: **Print Sheet and workflow simulator qualified; RX1HS output, cutting, status, and recovery unqualified.**

## 8. Licensing and redistribution

DNP's download license grants a non-exclusive, non-transferable right to use one object-code copy, permits only one backup copy, and prohibits reproduction, sublicensing, export, or transfer to another person. It does not grant FotoHAVN a right to bundle or redistribute the driver, Status App, or their DLLs. [DNP Software License Agreement](https://dnpphoto.com/en-us/Support/Downloads/Drivers-Tools)

Therefore:

- The field-test operator should install the official DNP package directly from DNP, or FotoHAVN must obtain separate written redistribution terms.
- Do not copy `CyStat.dll`, `CyStat64.dll`, the Status App, or driver binaries into the FotoHAVN installer based on the public download license.
- If HFP is chosen, install it separately from DNP and review the EULA displayed by that exact version; do not rebundle its installer or files by assumption.
- No public RX1HS SDK package, API reference, or SDK license was found in DNP's official downloads/manuals. Contact DNP support for a supported status API and commercial terms before choosing an SDK path. [DNP downloads](https://www.dnpphoto.com/downloads), [DNP support](https://www.dnpphoto.com/support)
- Gutenprint's RX1 backend is GPL-2.0-or-later and the surrounding project is GPL-licensed. Do not copy or link its implementation into FotoHAVN without legal review. A separately installed CUPS service is operationally and legally cleaner, but still needs product/license review.
- `TiepHoangDev/DNP-Printer` is MIT-licensed but functionally incomplete; its license does not make its behavior correct.
- `HungTran68/TEST-DNP-SDK` has no visible repository license and contains DNP native binaries with no visible redistribution grant. Do not copy code or binaries from it.

## 9. Resolved facts and remaining decisions

Resolved:

- One 1844 x 1240, 300 dpi, borderless `(6x4)` page can be cut into two 2x6 outputs by the RX1HS driver/firmware.
- The current 600 x 1800 white Photo Strip should be rotated without resampling to 1800 x 600 and placed twice at `(22, 20)` and `(22, 620)` in the direct-driver Print Sheet. That placement is an inferred starting geometry, not a calibrated safe zone.
- The Windows GDI + installed DNP driver + spooler job ID is the smallest documented submission path.
- DNP HFP explicitly supports third-party hot-folder submission, an `s6x2_2` route, and a printer-level status file; it is a viable separately installed alternative adapter but has weaker documented job correlation and a different input-raster contract.
- Gutenprint provides mature, current source evidence for RX1HS protocol, media, cutter, status, and completion concepts, but its 1920-pixel/Linux/GPL path is not the Windows raster or implementation contract.
- Neither reviewed C# repository is suitable to import: the MIT wrapper leaves advertised DNP settings unapplied, and the unlicensed SDK experiment packages proprietary DLLs and reports false completion/mock confidence.
- DNP driver auto-retry must be disabled so FotoHAVN's Retry button is the sole retry owner.
- Windows queue acceptance/completion alone cannot satisfy physical-ejection confirmation.
- Public DNP licensing does not allow bundling the downloaded driver/status binaries.

New decisions/prototypes required:

1. Fix the booth's Windows edition and official regional driver source. Windows 11 x64 has a public DNP Europe v1.14 package; DNP America's catalog still presents v3.21 for Windows 10. Do not infer equivalence from those labels.
2. Implement and test the FotoHAVN printer boundary, Print Sheet artifact/manifest, fake state machine, and 100%-scale physical proof before hardware arrives.
3. Spike direct GDI/spooler versus separately installed HFP on the real rig. Prefer the direct queue when exact job identity is required; select HFP only if its printer-status/archive behavior satisfies the lifecycle and support trade-off.
4. Pin and record the exact DNP package hash, installed binary version, firmware, media, Windows build, and exposed `2inch cut` option on the rig.
5. Prototype physical completion and detailed fault reporting on the real RX1HS; decide between verified Windows TrueEndOfJob/device status and a separately licensed DNP SDK.
6. Measure outer-edge and center-cut variation on printed calibration sheets before freezing content-safe margins. DNP publishes the raster and nominal cut, but no cutter-tolerance specification.
7. Obtain written DNP redistribution/SDK terms if the installer is expected to carry any DNP software.
