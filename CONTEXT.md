# FotoHAVN Booth Experience

FotoHAVN coordinates the guest-facing flow from taking photos to receiving a physical photo-booth print.

## Language

**Website Intro Experience**:
A skippable brand threshold before the website's main experience, using the enclosed booth, a curtain opening, and one fixed FOTOHAVN Brand Strip to introduce FOTOHAVN before yielding to the brand site.
_Avoid_: Landing page, startup splash, loading screen

**FOTOHAVN Brand Strip**:
A narrow printed brand object used by the Website Intro Experience, with four typographic panels for FOTOHAVN, Enclosed, Printed, and Distinctive and no photographic Captures.
_Avoid_: Photo Strip, placeholder strip, template

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

## Website Analytics

**Inquiry Intent**:
An anonymous website interaction in which a visitor activates a FOTOHAVN Instagram inquiry link. It does not establish that a message was sent, a conversation began, or a booking was made.
_Avoid_: Lead, customer, confirmed inquiry

**Qualified Lead**:
A person who begins a relevant inquiry conversation and is confirmed outside website analytics as a plausible booking or booth customer.
_Avoid_: Inquiry Intent, click, visitor

**Customer**:
A person or organization that completes a FOTOHAVN purchase or booking.
_Avoid_: Qualified Lead, Inquiry Intent, visitor

**Section Reach**:
An anonymous analytics signal that a visitor kept at least half of a named website section visible for one continuous second while the page was visible.
_Avoid_: Scroll depth, impression, Section Engagement

**Section Engagement**:
An anonymous analytics signal that a visitor kept at least half of a named website section visible for five cumulative seconds while the page was visible.
_Avoid_: Exact dwell time, proof of reading, Section Reach

**Cookieless Analytics Page Activity**:
Privacy-masked activity that Clarity measures without persistent browser identity. It represents one isolated page view and cannot establish a reliable journey across pages.
_Avoid_: Anonymous journey, user history, Consented Anonymous Journey

**Analytics Cookie Consent**:
A visitor's permission for Clarity to use its anonymous first-party cookie to connect measured activity across pages. It does not authorize personal identification or collection of customer-created media.
_Avoid_: Analytics consent, marketing consent, photograph consent

**Consented Anonymous Journey**:
A sequence of website activity that Clarity connects across pages through a visitor's Analytics Cookie Consent. It represents anonymous browser activity rather than a known person.
_Avoid_: User profile, identified visitor, Qualified Lead

**Online Booth Cycle**:
One in-browser use of the online booth, beginning when a visitor continues from template selection into photographs and ending when they leave or start another keepsake. It does not persist across a refresh.
_Avoid_: Guest Cycle, analytics session, transaction
