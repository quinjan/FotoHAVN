# Prototype Instructions

This is throwaway prototype code for GitHub issue #57. Do not promote it directly to production.

## Fixed decisions inherited from approved work

- Preserve FotoHAVN's calm monochrome light-theme direction.
- Operator controls and dialog actions use a minimum 48 px target.
- Event identity uses the approved neutral bordered panel with Event name and full lowercase UUID in consequential flows.
- Event Setup keeps the approved seamless left-form/right-live-preview layout from issue #56.
- Successful setup fields remain silent; checking, warning, and error feedback stays beneath the affected field.
- Event Setup keeps a compact persistent footer; scrolling belongs to the content above it.
- Delayed action progress appears only in the initiating button.
- Modal initial focus goes to the safest sensible action; focus is trapped, the background is inert, Escape dismisses only while idle, and focus returns to the invoking control.
- Destructive meaning belongs to the action and surrounding feedback, never the neutral Event identity panel.

## Prototype question

Compare three structurally different modal anatomies without reopening the fixed decisions above. Variants are selected with `?variant=A|B|C`, and the current task with `?scenario=`.
