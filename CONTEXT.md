# FotoHAVN Booth Experience

FotoHAVN coordinates the guest-facing flow from taking photos to receiving a physical photo-booth print.

## Language

**Event**:
A named booth run that fixes the camera and printer used by its Guest Cycles and groups their saved artifacts.
_Avoid_: Booth session, job

**Guest Cycle**:
One guest group's interaction with the booth, beginning at Start and ending when the booth returns to Start after the print is complete.
_Avoid_: Session, transaction

**Capture**:
One photograph taken during a Guest Cycle.
_Avoid_: Shot, image

**Camera Tuning**:
The operator's adjustment of the selected camera's supported image controls against its live preview during Event setup, retained for that Event.
_Avoid_: Camera Profile, filter

**Photo Strip**:
A single narrow keepsake composition containing an ordered set of Captures.
_Avoid_: Template, composite

**Print Sheet**:
The complete image submitted as one printer job, which may contain multiple copies of a Photo Strip.
_Avoid_: Photo Strip, printout
