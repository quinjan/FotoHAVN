# Desktop booth context

This context extends the shared language in `../CONTEXT.md` for the Windows booth application.

## Language

**Active Event**:
The single Event currently admitting Guest Cycles in the running FotoHAVN process. Activity does not survive closing or restarting FotoHAVN.
_Avoid_: Current session, resumed Event

**Guest Cycle**:
One guest group's interaction with the physical booth, beginning at Start and ending when the final Photo Strip preview completes and the booth returns to Start.
_Avoid_: Session, transaction, Online Booth Cycle

**Operator Assistance**:
The paused condition of an Active Event after a Camera or storage failure, retaining durable Guest Cycle progress until an operator can Retry.
_Avoid_: Error screen, recovery mode

**Available Camera**:
A Camera that Windows currently reports to FotoHAVN as a video-capture device. Availability allows selection but does not prove readiness for an Event.
_Avoid_: Detected device, discovered Camera

**Eligible Camera**:
The selected Available Camera after FotoHAVN has opened its live feed and received a fresh frame in a format suitable for Captures.
_Avoid_: Supported Camera, compatible Camera, qualified Camera

**Camera Binding**:
An Event's association with the exact Available Camera chosen by the operator. FotoHAVN never silently substitutes another Camera when the bound Camera is unavailable.
_Avoid_: Camera name, default Camera
