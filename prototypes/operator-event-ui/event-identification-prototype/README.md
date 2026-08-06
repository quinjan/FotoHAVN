# Duplicate Event identification UI prototype

> PROTOTYPE — throwaway code. Do not promote directly to production.

Question: **How should FotoHAVN distinguish Events with duplicate names across saved-Event cards and consequential start, edit, and permanent-delete flows?**

Three identity contracts are mounted in one browser prototype, switchable with `?variant=A|B|C`. The same two `Summer Social` Events appear in every variant.

- **A — Event ID:** cards show a grouped UUIDv7 fingerprint such as `7A2F · 91C4`; edit and consequential flows show the complete UUID. Saved recency remains directly below the identity. Delete uses the ordinary confirmation.
- **B — Duplicate-aware:** creation metadata is elevated only for duplicate names; delete requires typing the Event name.
- **C — Identity band:** every surface repeats a compact name + creation identity block; delete requires checking that exact identity.

Run from this directory:

```powershell
.\run.ps1
```

Then open <http://127.0.0.1:4181/event-identification-prototype/?variant=A> at a 1280 × 720 viewport.

Use the on-screen flow and viewport controls to inspect cards, edit, start, delete, deleting, and result states at the canonical frame or a narrow/150%-scale stress case. `Left` / `Right` cycles variants without intercepting text entry.

This prototype records no data and makes no production mutations. Issue #54 stays open until the operator explicitly approves an identification contract.
