# Setup readiness

## Event setup composition

- Standard: seamless left-form/right-live-preview surface, maximum 1100 px wide.
- Compact: near-full-width single scrollable column.
- Stress: full-window single scrollable column with tighter spacing.
- The title/header and 80 px footer are fixed; only content between them scrolls.
- Single-column order is Event identity/name, Camera and field-local status, fluid undistorted 16:9 preview, Printer, then Storage.
- Footer actions are Cancel, Save & Close, and Save & Start Event. Labels wrap only as a last resort and never truncate.

## Readiness behavior

- Event name and Camera are required. `No printer` is a fixed valid value.
- Storage is `C:\Program Files\FotoHAVN\Events` with a minimum 1 GB free contract.
- Successful fields remain silent. Do not add passive Ready/Selected checks or a global readiness banner.
- Camera checking starts after selection, appears directly beneath Camera, and clears silently when the Camera is Eligible.
- Invalid, unavailable, or insufficient-storage messages appear beside the affected field and explain recovery.
- Disabled save actions are never the only explanation. A setup-level callout is allowed only when multiple conditions block the next action.
- Validation, Camera failure, or storage failure reveals the affected field/status. Focus moves only after a user-submitted invalid form; asynchronous status does not steal focus.
