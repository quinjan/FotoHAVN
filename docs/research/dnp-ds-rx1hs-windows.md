# DNP DS-RX1HS printing and status on Windows

Research date: 2026-08-04
Resolves: [#3](https://github.com/quinjan/FotoHAVN/issues/3)

## Decision summary

FotoHAVN can produce the required two identical 2x6 strips by sending one borderless 6x4 page through the installed DNP Windows printer driver with `2inch cut = Enable`. For the field-test build, compose an exact 1844 x 1240 pixel, 300 x 300 dpi raster, place the two identical strips in its two 1844 x 620 halves, and submit it as a single-page job to the local `DS-RX1` queue. Use the DNP driver's `Paper Size = (6x4)`, `Orientation = Portrait`, `Border = Disable`, `2inch cut = Enable`, and `Print Re-try = Disable` settings.

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

The 156.1 x 105.0 mm driver image area is 4.1 mm larger on each axis than the nominal 152 x 101 mm finished sheet. The reasonable inference is about 2.05 mm of symmetric borderless overprint on each outer edge, but DNP does not label this difference as a guaranteed bleed allowance. Use the exact 1844 x 1240 raster to avoid driver scaling; let the black background run to every raster edge.

Duplicate the strip at raster rows `0..619` and `620..1239`. This aligns the logical seam with the center of DNP's documented page raster. Because DNP publishes no cutter-position tolerance, kerf, or safe-zone specification, the exact pixels preserved next to the center cut and outer trims remain an empirical question. The field-test template's black border usefully masks small cut/trim variation; critical photo content must not depend on an unverified edge tolerance.

The driver guide says it enlarges or reduces the application image to the selected print area and handles the output direction from the chosen orientation. Supplying the exact documented raster and using `(6x4)` Portrait avoids a second application-side rotation or rescale. [DNP RX1HS Windows driver guide, sections 2.5-2.6](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_PrinterDriverInstruction_For7%2C8%2C10_V1.13_1_English.pdf)

## 2. Driver and queue submission path

### Recommended field-test integration

Use the installed DNP Unidrv Windows driver and the native Win32 GDI/spooler APIs. Do not make the MVP depend on Hot Folder Print or an undocumented DLL.

1. Install the official DNP driver as an administrator, connect the powered printer by USB, confirm the `DS-RX1` queue, and reboot after installation. The official guide covers 32- and 64-bit Windows driver packages and requires a reboot. [DNP RX1HS Windows driver guide, chapter 1](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_PrinterDriverInstruction_For7%2C8%2C10_V1.13_1_English.pdf)
2. Preconfigure the queue with the field-test settings above. `2inch cut` and `Print Re-try` are DNP private driver features, not portable public `DEVMODE` fields. Microsoft says a valid driver `DEVMODE` contains device-dependent private data whose size and contents vary by driver; applications should obtain and validate it with `DocumentProperties` rather than fabricate or edit the private bytes. [Microsoft: Modify printer settings with DocumentProperties](https://learn.microsoft.com/en-us/troubleshoot/windows/win32/modify-printer-settings-documentproperties)
3. Create a printer device context from that validated `DEVMODE`, call `StartDoc`, `StartPage`, draw the 1844 x 1240 bitmap into the full driver page, then call `EndPage` and `EndDoc` on a worker thread. Microsoft's GDI print API delegates bitmap drawing to the installed device driver, and `StartDoc` returns the Windows print job identifier that can be passed to `GetJob`. [Microsoft: About the GDI Print API](https://learn.microsoft.com/en-us/windows/win32/printdocs/about-the-gdi-print-api), [Microsoft: StartDoc](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-startdoca)
4. Subscribe to job/printer changes with `FindFirstPrinterChangeNotification`, then read the identified job with `GetJob`/`JOB_INFO_1` and the queue with `GetPrinter`/`PRINTER_INFO_2`. Keep blocking spooler calls off the UI thread. [Microsoft: printer change notifications](https://learn.microsoft.com/en-us/windows/win32/printdocs/findfirstprinterchangenotification)

Submitting a JPEG to DNP Hot Folder Print could make a useful manual diagnostic, but it weakens job correlation and does not expose a documented physical-completion contract. It is not the recommended FotoHAVN integration boundary.

### Public driver-version evidence and deployment risk

- DNP's catalog labels its RX1/RX1HS Windows 10 package as `v3.21` for 32/64-bit Windows. The downloaded official package's `DSRX1.inf` reports `DriverVer=08/20/2014,1.1.1.0`; its payload contains x86 and x64 Unidrv components and a DNP language monitor. This appears to mean `v3.21` is a package/catalog release label, not the Windows driver binary version. [Official DNP Windows 10 package](https://www.dnpphoto.com/files/downloads/rx1_driver_win10_v3.21_32-64bit.zip)
- DNP's current v3.02 hardware guide lists Windows 7/8/10 in 32/64-bit form and Windows 11 as 64-bit compatible. [DNP DS-RX1HS/RX1 User Guide v3.02, p. 24](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_UserGuide_ver.3.02_English.pdf)
- The public downloads catalog does not expose a separately labeled RX1HS Windows 11 package. The field test must therefore pin the exact Windows edition/build, package SHA-256, installed driver version, printer firmware, and whether the `2inch cut` option is enabled. Prefer Windows 10 x64 for the first test unless the actual booth PC is already fixed to Windows 11; if it is, make driver installation and two-strip output a go/no-go rig check.

The driver itself needs no FotoHAVN-managed runtime. DNP's separate RX1 Status Display Tool is a 32/64-bit common executable with 32- and 64-bit native DLLs and requires .NET Framework 2.0 or later (3.5 SP1 for Windows 8 in its 2015 manual). FotoHAVN should not depend on that utility's runtime because the utility is not the integration API. [DNP Status Display Tool v1.0 package](https://www.dnpphoto.com/files/downloads/status%20app%20for%20rx1%20and%20rx1hs%20v1.0.zip)

## 3. Queue acceptance is not physical completion

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

## 4. Readiness, media, offline, and jam signals

At startup and between Guest Cycles, FotoHAVN can safely require all of the following generic conditions:

- The intended local queue exists and uses the expected DNP driver and USB port.
- The queue is not paused and has no unresolved prior jobs.
- Windows does not report offline, not available, door open, paper out/problem/jam, error, or user intervention through `PRINTER_INFO_2`/`PRINTER_INFO_6`.
- The hardware prototype's supported DNP status source reports `Waiting`, correct 4x6 media, and at least one sheet remaining.

Windows defines the generic printer flags but their actual reporting depends on the driver/monitor. [Microsoft `PRINTER_INFO_2`](https://learn.microsoft.com/en-us/windows/win32/printdocs/printer-info-2), [Microsoft `PRINTER_INFO_6`](https://learn.microsoft.com/en-us/windows/win32/printdocs/printer-info-6)

DNP's public tools establish the device-level vocabulary and media data that exist, but not a redistributable API. DNP PrinterInfo lists media size/remaining plus `PAPEROUT`, `RIBBONOUT`, `COVEROPEN`, `RIBBONERR`, `PAPERERR`, `PAPERJAM`, `SCRAPBOXERR`, `MOTCOOLING`, `DATAERR`, `SYSTEMERR`, `HARDWAREERR`, `NOT_INITIALIZED`, and `OFFLINE`. [DNP PrinterInfo quick reference](https://new.dnpphoto.com/Portals/0/Resources/PrinterInfo%20Quick%20Reference.pdf)

The RX1HS user guide's front-panel fault table covers paper end, ribbon end, door open, paper error, ribbon error, system error, and automatic head cooling; its recovery instructions require removal of partial output after jams or interrupted cutting/printing. [DNP DS-RX1HS/RX1 User Guide v3.02, pp. 18-21](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_UserGuide_ver.3.02_English.pdf)

## 5. Retry semantics

Set the driver to `Print Re-try = Disable` for FotoHAVN's explicit `Please call the operator` -> fix media -> `Retry Print` flow.

DNP documents these semantics:

- With retry disabled, a printer error clears buffered print data; after recovery, the application must resend the image.
- With retry enabled, paper end, ribbon end, cover open, paper jam, ribbon error, paper-definition error, and data error can resume from printer-buffered data after recovery.
- Errors that require printer power cycling clear the buffer regardless of the setting; those include head-position, fan, cutter, head-voltage/temperature, media-temperature, ribbon-tension, RFID-module, and system errors.

Source: [DNP RX1HS Windows driver guide, section 2.2.2](https://new.dnpphoto.com/Portals/0/Resources/DS-RX1_PrinterDriverInstruction_For7%2C8%2C10_V1.13_1_English.pdf).

Disabling driver auto-retry gives FotoHAVN one owner of retry behavior and preserves the intended guest screen. Keep the saved 1844 x 1240 composite immutable. After the supported status source reports `Waiting` with correct media, submit a new one-copy job for the same composite.

There is no printer-side idempotency key. FotoHAVN must not offer Retry while the original job might still be buffered, printing, or automatically recovering, or it can produce duplicate strips. A lost/ambiguous completion signal after apparent output requires operator judgment; public DNP/Windows documentation provides no way to prove that no physical copy emerged.

## 6. Licensing and redistribution

DNP's download license grants a non-exclusive, non-transferable right to use one object-code copy, permits only one backup copy, and prohibits reproduction, sublicensing, export, or transfer to another person. It does not grant FotoHAVN a right to bundle or redistribute the driver, Status App, or their DLLs. [DNP Software License Agreement](https://dnpphoto.com/en-us/Support/Downloads/Drivers-Tools)

Therefore:

- The field-test operator should install the official DNP package directly from DNP, or FotoHAVN must obtain separate written redistribution terms.
- Do not copy `CyStat.dll`, `CyStat64.dll`, the Status App, or driver binaries into the FotoHAVN installer based on the public download license.
- No public RX1HS SDK package, API reference, or SDK license was found in DNP's official downloads/manuals. Contact DNP support for a supported status API and commercial terms before choosing an SDK path. [DNP downloads](https://www.dnpphoto.com/downloads), [DNP support](https://www.dnpphoto.com/support)

## 7. Resolved facts and remaining decisions

Resolved:

- One 1844 x 1240, 300 dpi, borderless `(6x4)` page can be cut into two 2x6 outputs by the RX1HS driver/firmware.
- The two identical strips belong in the two 1844 x 620 halves of one application-composed page.
- The Windows GDI + installed DNP driver + spooler job ID is the smallest documented submission path.
- DNP driver auto-retry must be disabled so FotoHAVN's Retry button is the sole retry owner.
- Windows queue acceptance/completion alone cannot satisfy physical-ejection confirmation.
- Public DNP licensing does not allow bundling the downloaded driver/status binaries.

New decisions/prototypes required:

1. Fix the field-test Windows version. Windows 10 x64 has the clearest public package path; Windows 11 x64 is listed by the current hardware guide but needs rig validation.
2. Pin and record the exact DNP package hash, installed binary version, firmware, and exposed `2inch cut` option on the rig.
3. Prototype physical completion and detailed fault reporting on the real RX1HS; decide between verified Windows TrueEndOfJob/device status and a separately licensed DNP SDK.
4. Measure outer-edge and center-cut variation on printed calibration sheets before freezing content-safe margins. DNP publishes the raster and nominal cut, but no cutter-tolerance specification.
5. Obtain written DNP redistribution/SDK terms if the installer is expected to carry any DNP software.
