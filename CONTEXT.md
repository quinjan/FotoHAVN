# FotoHAVN Booth Experience

FotoHAVN coordinates the guest-facing flow from taking photos to receiving a physical photo-booth print.

## Language

**Event**:
A named booth run that fixes the camera and printer used by its Guest Cycles and groups their saved artifacts.
_Avoid_: Booth session, job

**Active Event**:
The single Event currently admitting Guest Cycles in the running FotoHAVN process. Activity does not survive closing or restarting FotoHAVN.
_Avoid_: Current session, resumed Event

**Guest Cycle**:
One guest group's interaction with the booth, beginning at Start and ending when the final Photo Strip preview completes and the booth returns to Start.
_Avoid_: Session, transaction

**Operator Assistance**:
The paused condition of an active Guest Cycle after a Camera or storage failure, retaining its durable progress until an operator can Retry.
_Avoid_: Error screen, recovery mode

**Capture**:
One photograph preserved from the Camera's live feed when a Guest Cycle countdown completes.
_Avoid_: Shot, image

**Available Camera**:
A Camera that Windows currently reports to FotoHAVN as a video-capture device. Availability allows an operator to select the Camera but does not prove that it is ready for an Event.
_Avoid_: Detected device, discovered Camera

**Eligible Camera**:
The selected Available Camera after FotoHAVN has opened its live feed and received a fresh frame in a format suitable for Captures.
_Avoid_: Supported Camera, compatible Camera, qualified Camera

**Camera Binding**:
An Event's association with the exact Available Camera chosen by the operator. FotoHAVN never silently substitutes another Camera when the bound Camera is unavailable.
_Avoid_: Camera name, default Camera

**Photo Strip**:
A single narrow keepsake composition containing an ordered set of Captures.
_Avoid_: Template, composite

**Print Sheet**:
The complete image submitted as one printer job, which may contain multiple copies of a Photo Strip.
_Avoid_: Photo Strip, printout
